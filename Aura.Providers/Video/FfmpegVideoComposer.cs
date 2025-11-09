using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Aura.Core.Dependencies;
using Aura.Core.Errors;
using Aura.Core.Models;
using Aura.Core.Models.Export;
using Aura.Core.Providers;
using Aura.Core.Services.FFmpeg;
using Aura.Core.Services.Render;
using Microsoft.Extensions.Logging;

namespace Aura.Providers.Video;

public class FfmpegVideoComposer : IVideoComposer
{
    private readonly ILogger<FfmpegVideoComposer> _logger;
    private readonly IFfmpegLocator _ffmpegLocator;
    private readonly string? _configuredFfmpegPath;
    private readonly string _workingDirectory;
    private string _outputDirectory;
    private readonly string _logsDirectory;
    private readonly HardwareEncoder _hardwareEncoder;

    public FfmpegVideoComposer(ILogger<FfmpegVideoComposer> logger, IFfmpegLocator ffmpegLocator, string? configuredFfmpegPath = null, string? outputDirectory = null)
    {
        _logger = logger;
        _ffmpegLocator = ffmpegLocator;
        _configuredFfmpegPath = configuredFfmpegPath;
        _workingDirectory = Path.Combine(Path.GetTempPath(), "AuraVideoStudio", "Render");
        _outputDirectory = outputDirectory ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.MyVideos),
            "AuraVideoStudio");
        _logsDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Aura", "Logs", "ffmpeg");
        
        // Ensure working directory exists
        if (!Directory.Exists(_workingDirectory))
        {
            Directory.CreateDirectory(_workingDirectory);
        }
        
        // Ensure output directory exists
        if (!Directory.Exists(_outputDirectory))
        {
            Directory.CreateDirectory(_outputDirectory);
        }
        
        // Ensure logs directory exists
        if (!Directory.Exists(_logsDirectory))
        {
            Directory.CreateDirectory(_logsDirectory);
        }
        
        // Initialize placeholder - actual hardware encoder will be created at render time with proper path
        _hardwareEncoder = new HardwareEncoder(
            Microsoft.Extensions.Logging.Abstractions.NullLogger<HardwareEncoder>.Instance, 
            "ffmpeg");
    }

    public async Task<string> RenderAsync(Timeline timeline, RenderSpec spec, IProgress<RenderProgress> progress, CancellationToken ct)
    {
        var jobId = Guid.NewGuid().ToString("N");
        var correlationId = System.Diagnostics.Activity.Current?.Id ?? jobId;
        
        _logger.LogInformation("Starting FFmpeg render (JobId={JobId}, CorrelationId={CorrelationId}) at {Resolution}p", 
            jobId, correlationId, spec.Res.Height);
        
        // Set up FFmpeg log file path
        var ffmpegLogPath = Path.Combine(_logsDirectory, $"{jobId}.log");
        StreamWriter? logWriter = null;
        
        // Resolve FFmpeg path once at the start - this is the single source of truth for this render job
        string ffmpegPath;
        try
        {
            ffmpegPath = await _ffmpegLocator.GetEffectiveFfmpegPathAsync(_configuredFfmpegPath, ct);
            _logger.LogInformation("Resolved FFmpeg path for job {JobId}: {FfmpegPath}", jobId, ffmpegPath);
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogError(ex, "Failed to resolve FFmpeg path for job {JobId}", jobId);
            throw FfmpegException.NotFound(correlationId: correlationId);
        }
        
        // Validate FFmpeg binary before starting
        await ValidateFfmpegBinaryAsync(ffmpegPath, jobId, correlationId, ct);
        
        // Pre-validate audio files - pass the resolved ffmpeg path
        await PreValidateAudioAsync(timeline, ffmpegPath, jobId, correlationId, ct);
        
        // Create output file path using configured output directory
        string outputFilePath = Path.Combine(
            _outputDirectory,
            $"AuraVideoStudio_{DateTime.Now:yyyyMMddHHmmss}.{spec.Container}");
        
        // Build the FFmpeg command with hardware acceleration support
        string ffmpegCommand = await BuildFfmpegCommandAsync(timeline, spec, outputFilePath, ffmpegPath, ct);
        
        _logger.LogInformation("FFmpeg command (JobId={JobId}): {FFmpegPath} {Command}", 
            jobId, ffmpegPath, ffmpegCommand);
        
        // Create process to run FFmpeg with stderr/stdout capture
        var stderrBuilder = new StringBuilder();
        var stdoutBuilder = new StringBuilder();
        
        var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = ffmpegPath,
                Arguments = ffmpegCommand,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardError = true,
                RedirectStandardOutput = true
            },
            EnableRaisingEvents = true
        };
        
        // Track progress
        var totalDuration = timeline.Scenes.Count > 0 
            ? timeline.Scenes[^1].Start + timeline.Scenes[^1].Duration 
            : TimeSpan.FromMinutes(1);
        
        var startTime = DateTime.Now;
        var lastReportTime = DateTime.Now;
        
        // Initialize log writer for FFmpeg output
        try
        {
            logWriter = new StreamWriter(ffmpegLogPath, append: false, encoding: Encoding.UTF8)
            {
                AutoFlush = true
            };
            logWriter.WriteLine($"FFmpeg Render Log - Job ID: {jobId}");
            logWriter.WriteLine($"Correlation ID: {correlationId}");
            logWriter.WriteLine($"Started: {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC");
            logWriter.WriteLine($"Resolution: {spec.Res.Width}x{spec.Res.Height}");
            logWriter.WriteLine($"FFmpeg Path: {ffmpegPath}");
            logWriter.WriteLine($"Command: {ffmpegCommand}");
            logWriter.WriteLine(new string('-', 80));
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to create FFmpeg log file at {LogPath}", ffmpegLogPath);
        }

        // Set up output handler to parse progress and capture output
        process.ErrorDataReceived += (sender, args) =>
        {
            if (string.IsNullOrEmpty(args.Data)) return;
            
            // Capture for error reporting
            stderrBuilder.AppendLine(args.Data);
            
            // Write to log file
            try
            {
                logWriter?.WriteLine($"[stderr] {args.Data}");
            }
            catch
            {
                // Ignore log write errors
            }
            
            // Log the output
            _logger.LogTrace("FFmpeg stderr: {Output}", args.Data);
            
            // Parse progress if it contains time information
            if (args.Data.Contains("time="))
            {
                // Parse the time value (format: time=00:00:12.34)
                var timeMatch = System.Text.RegularExpressions.Regex.Match(args.Data, @"time=(\d{2}:\d{2}:\d{2}\.\d{2})");
                if (timeMatch.Success)
                {
                    var timeStr = timeMatch.Groups[1].Value;
                    if (TimeSpan.TryParse(timeStr, out var currentTime))
                    {
                        var now = DateTime.Now;
                        
                        // Report progress at most once per second
                        if ((now - lastReportTime).TotalSeconds >= 1)
                        {
                            lastReportTime = now;
                            
                            // Calculate progress percentage
                            float percentage = (float)(currentTime.TotalSeconds / totalDuration.TotalSeconds * 100);
                            percentage = Math.Clamp(percentage, 0, 100);
                            
                            // Calculate time remaining
                            var elapsed = now - startTime;
                            var estimatedTotal = TimeSpan.FromSeconds(
                                elapsed.TotalSeconds / (percentage / 100));
                            var remaining = estimatedTotal - elapsed;
                            
                            // Report progress
                            progress.Report(new RenderProgress(
                                percentage,
                                elapsed,
                                remaining,
                                "Rendering video"));
                        }
                    }
                }
            }
        };
        
        // Capture stdout as well
        process.OutputDataReceived += (sender, args) =>
        {
            if (!string.IsNullOrEmpty(args.Data))
            {
                stdoutBuilder.AppendLine(args.Data);
                
                // Write to log file
                try
                {
                    logWriter?.WriteLine($"[stdout] {args.Data}");
                }
                catch
                {
                    // Ignore log write errors
                }
                
                _logger.LogTrace("FFmpeg stdout: {Output}", args.Data);
            }
        };
        
        // Set up task to wait for process completion
        var tcs = new TaskCompletionSource<bool>();
        
        process.Exited += (sender, args) =>
        {
            if (process.ExitCode == 0)
            {
                tcs.SetResult(true);
            }
            else
            {
                var stderr = stderrBuilder.ToString();
                var exception = FfmpegException.FromProcessFailure(
                    process.ExitCode, 
                    stderr, 
                    jobId, 
                    correlationId);
                tcs.SetException(exception);
            }
        };
        
        // Start the process
        process.Start();
        process.BeginErrorReadLine();
        process.BeginOutputReadLine();
        
        // Register cancellation with graceful termination
        var cancellationRegistration = ct.Register(() =>
        {
            try
            {
                if (!process.HasExited)
                {
                    _logger.LogWarning("Cancelling FFmpeg render (JobId={JobId})", jobId);
                    
                    // Try graceful termination first (send 'q' to stdin if possible)
                    try
                    {
                        process.StandardInput?.Write('q');
                        process.StandardInput?.Flush();
                        
                        // Give it 2 seconds to exit gracefully
                        if (!process.WaitForExit(2000))
                        {
                            _logger.LogWarning("Graceful termination timeout, killing process (JobId={JobId})", jobId);
                            process.Kill(entireProcessTree: true);
                        }
                    }
                    catch
                    {
                        // If graceful termination fails, kill immediately
                        process.Kill(entireProcessTree: true);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to terminate FFmpeg process during cancellation");
            }
        });
        
        // Wait for completion or cancellation with timeout
        try
        {
            // Set a reasonable timeout (30 minutes for renders)
            var timeoutTask = Task.Delay(TimeSpan.FromMinutes(30), ct);
            var completedTask = await Task.WhenAny(tcs.Task, timeoutTask);
            
            if (completedTask == timeoutTask)
            {
                _logger.LogError("FFmpeg render timeout after 30 minutes (JobId={JobId})", jobId);
                
                // Kill the process
                try
                {
                    if (!process.HasExited)
                    {
                        process.Kill(entireProcessTree: true);
                    }
                }
                catch (Exception killEx)
                {
                    _logger.LogWarning(killEx, "Failed to kill timed-out FFmpeg process");
                }
                
                throw new FfmpegException(
                    "FFmpeg render operation timed out after 30 minutes",
                    FfmpegErrorCategory.Timeout,
                    jobId: jobId,
                    correlationId: correlationId,
                    suggestedActions: new[]
                    {
                        "Try with shorter content or lower resolution",
                        "Check system resources (CPU, disk space)",
                        "Ensure FFmpeg is not hanging on a corrupted input file"
                    });
            }
            
            await tcs.Task; // This will throw if the process failed
        }
        catch (FfmpegException)
        {
            // Already properly formatted FFmpeg exception
            throw;
        }
        catch (Exception ex)
        {
            // Unexpected error
            _logger.LogError(ex, "Unexpected FFmpeg error (JobId={JobId})", jobId);
            throw new InvalidOperationException($"FFmpeg render failed unexpectedly: {ex.Message} (JobId: {jobId}, CorrelationId: {correlationId})", ex);
        }
        finally
        {
            // Dispose cancellation registration
            cancellationRegistration.Dispose();
            
            // Ensure process is cleaned up
            try
            {
                if (!process.HasExited)
                {
                    process.Kill(entireProcessTree: true);
                }
            }
            catch
            {
                // Ignore cleanup errors
            }
            
            // Close log file
            try
            {
                logWriter?.WriteLine(new string('-', 80));
                logWriter?.WriteLine($"Completed: {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC");
                logWriter?.WriteLine($"Exit Code: {process.ExitCode}");
                logWriter?.Dispose();
            }
            catch
            {
                // Ignore cleanup errors
            }
            
            // Dispose process
            process.Dispose();
        }
        
        _logger.LogInformation("Render completed successfully (JobId={JobId}): {OutputPath}", jobId, outputFilePath);
        _logger.LogInformation("FFmpeg log written to: {LogPath}", ffmpegLogPath);
        
        // Report 100% completion
        progress.Report(new RenderProgress(
            100,
            DateTime.Now - startTime,
            TimeSpan.Zero,
            "Render complete"));
        
        return outputFilePath;
    }
    /// <summary>
    /// Build FFmpeg command using FFmpegCommandBuilder with hardware acceleration support
    /// </summary>
    private async Task<string> BuildFfmpegCommandAsync(Timeline timeline, RenderSpec spec, string outputPath, string ffmpegPath, CancellationToken ct)
    {
        _logger.LogInformation("Building FFmpeg command for render spec: {Codec} @ {Width}x{Height}, {Fps}fps, {VideoBitrate}kbps", 
            spec.Codec, spec.Res.Width, spec.Res.Height, spec.Fps, spec.VideoBitrateK);
        
        // Validate input files
        if (string.IsNullOrEmpty(timeline.NarrationPath) || !File.Exists(timeline.NarrationPath))
        {
            throw new ArgumentException($"Narration file not found: {timeline.NarrationPath}", nameof(timeline));
        }
        
        if (!string.IsNullOrEmpty(timeline.MusicPath) && !File.Exists(timeline.MusicPath))
        {
            _logger.LogWarning("Music file not found, will skip music: {Path}", timeline.MusicPath);
        }
        
        // Determine aspect ratio from resolution
        var aspectRatio = spec.Res.Width == spec.Res.Height ? AspectRatio.OneByOne :
                         spec.Res.Width > spec.Res.Height ? AspectRatio.SixteenByNine :
                         AspectRatio.NineBySixteen;
        
        // Create export preset from render spec
        var exportPreset = new ExportPreset(
            Name: "Custom",
            Description: $"Custom render at {spec.Res.Width}x{spec.Res.Height}",
            Platform: Platform.Generic,
            Container: spec.Container,
            VideoCodec: spec.Codec.ToLowerInvariant() switch
            {
                "h264" => "libx264",
                "h265" or "hevc" => "libx265",
                "vp9" => "libvpx-vp9",
                "av1" => "libaom-av1",
                _ => "libx264"
            },
            AudioCodec: "aac",
            Resolution: spec.Res,
            FrameRate: spec.Fps,
            VideoBitrate: spec.VideoBitrateK,
            AudioBitrate: spec.AudioBitrateK,
            PixelFormat: "yuv420p",
            ColorSpace: "bt709",
            AspectRatio: aspectRatio,
            Quality: spec.QualityLevel >= 90 ? QualityLevel.Maximum :
                     spec.QualityLevel >= 75 ? QualityLevel.High :
                     spec.QualityLevel >= 50 ? QualityLevel.Good :
                     QualityLevel.Draft
        );
        
        // Detect hardware capabilities and select best encoder
        // Create hardware encoder with ffmpeg path (use NullLogger since we log from FfmpegVideoComposer)
        var hardwareEncoderWithPath = new HardwareEncoder(
            Microsoft.Extensions.Logging.Abstractions.NullLogger<HardwareEncoder>.Instance, 
            ffmpegPath);
        EncoderConfig encoderConfig;
        
        try
        {
            encoderConfig = await hardwareEncoderWithPath.SelectBestEncoderAsync(exportPreset, preferHardware: true);
            _logger.LogInformation("Selected encoder: {Encoder} (Hardware: {IsHardware})", 
                encoderConfig.EncoderName, encoderConfig.IsHardwareAccelerated);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to detect hardware encoder, falling back to software");
            encoderConfig = new EncoderConfig(
                exportPreset.VideoCodec,
                "Software encoder (fallback)",
                false,
                new Dictionary<string, string>());
        }
        
        // Build command using FFmpegCommandBuilder
        var builder = new FFmpegCommandBuilder()
            .SetOverwrite(true);
        
        // Add hardware acceleration if available
        if (encoderConfig.IsHardwareAccelerated)
        {
            // Set hwaccel based on encoder type
            if (encoderConfig.EncoderName.Contains("nvenc", StringComparison.OrdinalIgnoreCase))
            {
                builder.SetHardwareAcceleration("cuda");
            }
            else if (encoderConfig.EncoderName.Contains("qsv", StringComparison.OrdinalIgnoreCase))
            {
                builder.SetHardwareAcceleration("qsv");
            }
            else if (encoderConfig.EncoderName.Contains("amf", StringComparison.OrdinalIgnoreCase))
            {
                builder.SetHardwareAcceleration("d3d11va");
            }
        }
        
        // Add input files
        builder.AddInput(timeline.NarrationPath);
        
        bool hasMusicInput = !string.IsNullOrEmpty(timeline.MusicPath) && File.Exists(timeline.MusicPath);
        if (hasMusicInput)
        {
            builder.AddInput(timeline.MusicPath);
        }
        
        // Set video codec (use hardware encoder if available)
        builder.SetVideoCodec(encoderConfig.EncoderName);
        
        // Apply encoder-specific parameters
        foreach (var param in encoderConfig.Parameters)
        {
            // These would need to be added as options to the command
            _logger.LogDebug("Encoder parameter: {Key}={Value}", param.Key, param.Value);
        }
        
        // Set encoding preset based on quality
        var preset = exportPreset.Quality switch
        {
            QualityLevel.Draft => "ultrafast",
            QualityLevel.Good => "fast",
            QualityLevel.High => "medium",
            QualityLevel.Maximum => "slow",
            _ => "medium"
        };
        builder.SetPreset(preset);
        
        // Set CRF for quality (lower is better, 18-28 is good range)
        var crf = spec.QualityLevel >= 90 ? 18 :
                 spec.QualityLevel >= 75 ? 23 :
                 spec.QualityLevel >= 50 ? 28 :
                 33;
        builder.SetCRF(crf);
        
        // Set resolution and frame rate
        builder.SetResolution(spec.Res.Width, spec.Res.Height);
        builder.SetFrameRate(spec.Fps);
        
        // Set pixel format
        builder.SetPixelFormat(exportPreset.PixelFormat);
        
        // Set audio codec and bitrate
        builder.SetAudioCodec(exportPreset.AudioCodec);
        builder.SetAudioBitrate(spec.AudioBitrateK);
        
        // Set video bitrate
        builder.SetVideoBitrate(spec.VideoBitrateK);
        
        // Set audio settings for better quality
        builder.SetAudioSampleRate(48000); // Standard sample rate
        builder.SetAudioChannels(2); // Stereo
        
        // Add metadata
        builder.AddMetadata("title", "Generated by Aura Video Studio");
        builder.AddMetadata("encoder", "Aura Video Studio");
        builder.AddMetadata("creation_time", DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ"));
        
        // Set output file
        builder.SetOutput(outputPath);
        
        // Build the command
        var command = builder.Build();
        
        _logger.LogInformation("FFmpeg command built successfully: {Length} characters", command.Length);
        _logger.LogDebug("Full command: ffmpeg {Command}", command);
        
        return command;
    }
    
    /// <summary>
    /// Validate FFmpeg binary before use
    /// </summary>
    private async Task ValidateFfmpegBinaryAsync(string ffmpegPath, string jobId, string correlationId, CancellationToken ct)
    {
        _logger.LogInformation("Validating FFmpeg binary: {Path}", ffmpegPath);
        
        // For executables in PATH (like "ffmpeg"), File.Exists() will return false
        // So we skip the file existence check and go straight to running the command
        // The command execution will fail if FFmpeg is not found, giving us better error info
        bool isPathExecutable = !Path.IsPathRooted(ffmpegPath) && 
                                !ffmpegPath.Contains(Path.DirectorySeparatorChar) &&
                                !ffmpegPath.Contains(Path.AltDirectorySeparatorChar);
        
        // Check file exists only if it's an absolute or relative path (not just an executable name)
        if (!isPathExecutable && !File.Exists(ffmpegPath))
        {
            var error = new
            {
                code = "E302-FFMPEG_VALIDATION",
                message = "FFmpeg binary not found",
                ffmpegPath = ffmpegPath,
                correlationId,
                howToFix = new[]
                {
                    "Install FFmpeg via Download Center",
                    "Attach an existing FFmpeg installation",
                    "Check FFmpeg installation path in settings",
                    "Add FFmpeg to system PATH"
                }
            };
            
            _logger.LogError("FFmpeg validation failed: {Error}", System.Text.Json.JsonSerializer.Serialize(error));
            throw new InvalidOperationException($"FFmpeg binary not found at {ffmpegPath}. " +
                $"Install FFmpeg via Download Center or attach an existing installation. (CorrelationId: {correlationId})");
        }
        
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = ffmpegPath,
                Arguments = "-version",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            
            using var process = Process.Start(psi);
            if (process == null)
            {
                throw new InvalidOperationException("Failed to start FFmpeg process for validation");
            }
            
            await process.WaitForExitAsync(ct);
            
            var output = await process.StandardOutput.ReadToEndAsync(ct);
            var stderr = await process.StandardError.ReadToEndAsync(ct);
            
            if (process.ExitCode != 0)
            {
                var error = new
                {
                    code = "E302-FFMPEG_VALIDATION",
                    message = "FFmpeg validation failed",
                    exitCode = process.ExitCode,
                    stderr = stderr.Length > 1000 ? stderr.Substring(0, 1000) + "..." : stderr,
                    correlationId,
                    howToFix = new[]
                    {
                        "FFmpeg binary may be corrupted - try reinstalling",
                        "Check system dependencies (Visual C++ Redistributable on Windows)",
                        "Use 'Repair' option in Download Center"
                    }
                };
                
                _logger.LogError("FFmpeg validation failed: {Error}", System.Text.Json.JsonSerializer.Serialize(error));
                throw new InvalidOperationException($"FFmpeg validation failed with exit code {process.ExitCode}. " +
                    $"See logs for details. (CorrelationId: {correlationId})");
            }
            
            _logger.LogInformation("FFmpeg validation successful: {Version}", 
                output.Split('\n').FirstOrDefault(l => l.Contains("ffmpeg version")) ?? "version unknown");
        }
        catch (Exception ex) when (ex is not InvalidOperationException)
        {
            var error = new
            {
                code = "E302-FFMPEG_VALIDATION",
                message = "FFmpeg validation exception",
                exception = ex.Message,
                correlationId,
                howToFix = new[]
                {
                    "Ensure FFmpeg has execute permissions",
                    "Check antivirus/security software blocking FFmpeg",
                    "Try attaching a different FFmpeg installation"
                }
            };
            
            _logger.LogError(ex, "FFmpeg validation exception: {Error}", System.Text.Json.JsonSerializer.Serialize(error));
            throw new InvalidOperationException($"FFmpeg validation failed: {ex.Message} (CorrelationId: {correlationId})", ex);
        }
    }
    
    /// <summary>
    /// Pre-validate audio files before rendering and attempt remediation if needed
    /// </summary>
    private async Task PreValidateAudioAsync(Timeline timeline, string ffmpegPath, string jobId, string correlationId, CancellationToken ct)
    {
        _logger.LogInformation("Pre-validating audio files (JobId={JobId})", jobId);
        
        // Get ffprobe path (same directory as ffmpeg)
        var ffmpegDir = Path.GetDirectoryName(ffmpegPath);
        var ffprobePath = ffmpegDir != null ? Path.Combine(ffmpegDir, "ffprobe.exe") : null;
        if (ffprobePath != null && !File.Exists(ffprobePath))
        {
            ffprobePath = null;
        }
        
        var validator = new Aura.Core.Audio.AudioValidator(
            Microsoft.Extensions.Logging.Abstractions.NullLogger<Aura.Core.Audio.AudioValidator>.Instance,
            ffmpegPath,
            ffprobePath);
        
        // Validate narration
        if (!string.IsNullOrEmpty(timeline.NarrationPath))
        {
            await ValidateAndRemediateAudioFileAsync(
                validator, 
                timeline.NarrationPath, 
                "narration", 
                jobId, 
                correlationId, 
                ct);
        }
        
        // Validate music if present
        if (!string.IsNullOrEmpty(timeline.MusicPath))
        {
            await ValidateAndRemediateAudioFileAsync(
                validator, 
                timeline.MusicPath, 
                "music", 
                jobId, 
                correlationId, 
                ct);
        }
        
        _logger.LogInformation("Audio pre-validation complete (JobId={JobId})", jobId);
    }
    
    /// <summary>
    /// Validate a single audio file and attempt remediation if corrupted
    /// </summary>
    private async Task ValidateAndRemediateAudioFileAsync(
        Aura.Core.Audio.AudioValidator validator,
        string audioPath,
        string audioType,
        string jobId,
        string correlationId,
        CancellationToken ct)
    {
        _logger.LogInformation("Validating {AudioType} audio: {Path} (JobId={JobId})", 
            audioType, audioPath, jobId);
        
        var validation = await validator.ValidateAsync(audioPath, ct);
        
        if (validation.IsValid)
        {
            _logger.LogInformation("{AudioType} audio validation passed (JobId={JobId})", audioType, jobId);
            return;
        }
        
        // Audio is invalid - attempt remediation
        _logger.LogWarning("{AudioType} audio validation failed: {Error} (JobId={JobId})", 
            audioType, validation.ErrorMessage, jobId);
        
        if (validation.IsCorrupted)
        {
            _logger.LogInformation("Attempting to re-encode corrupted {AudioType} audio (JobId={JobId})", 
                audioType, jobId);
            
            // Try re-encoding to a clean WAV
            var reEncodedPath = Path.Combine(
                Path.GetDirectoryName(audioPath) ?? Path.GetTempPath(),
                $"{Path.GetFileNameWithoutExtension(audioPath)}_reencoded.wav");
            
            var (success, errorMessage) = await validator.ReencodeAsync(audioPath, reEncodedPath, ct);
            
            if (success)
            {
                _logger.LogInformation("Successfully re-encoded {AudioType} audio (JobId={JobId})", 
                    audioType, jobId);
                
                // Replace the original file with the re-encoded version
                File.Delete(audioPath);
                File.Move(reEncodedPath, audioPath);
                
                _logger.LogInformation("Replaced corrupted {AudioType} with re-encoded version (JobId={JobId})", 
                    audioType, jobId);
                return;
            }
            
            _logger.LogError("Re-encoding failed: {Error}. Attempting to generate silent fallback (JobId={JobId})", 
                errorMessage, jobId);
            
            // Re-encoding failed - generate silent WAV as fallback
            var (silentSuccess, silentError) = await validator.GenerateSilentWavAsync(
                audioPath + ".silent.wav", 
                10.0, // 10 seconds default
                ct);
            
            if (silentSuccess)
            {
                _logger.LogWarning("Generated silent {AudioType} fallback (JobId={JobId})", audioType, jobId);
                
                // Replace with silent version
                File.Delete(audioPath);
                File.Move(audioPath + ".silent.wav", audioPath);
                return;
            }
            
            // All remediation attempts failed
            var error = new
            {
                code = "E305-AUDIO_VALIDATION",
                message = $"{audioType} audio file is corrupted and could not be repaired",
                audioPath,
                validationError = validation.ErrorMessage,
                reencodeError = errorMessage,
                silentFallbackError = silentError,
                jobId,
                correlationId,
                howToFix = new[]
                {
                    $"Re-generate the {audioType} audio",
                    "Ensure TTS provider is working correctly",
                    "Check audio file is not in use by another application",
                    "Try using a different TTS provider"
                }
            };
            
            _logger.LogError("All audio remediation attempts failed: {Error}", 
                System.Text.Json.JsonSerializer.Serialize(error));
            
            throw new InvalidOperationException(
                $"{audioType} audio validation failed and remediation unsuccessful: {validation.ErrorMessage}. " +
                $"CorrelationId: {correlationId}, JobId: {jobId}");
        }
        
        // Not corrupted but still invalid (e.g., file not found)
        throw new InvalidOperationException(
            $"{audioType} audio validation failed: {validation.ErrorMessage}. " +
            $"CorrelationId: {correlationId}, JobId: {jobId}");
    }
    

}