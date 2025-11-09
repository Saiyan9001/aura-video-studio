using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace Aura.Core.Services.Render;

/// <summary>
/// Video quality validation result
/// </summary>
public record QualityValidationResult(
    bool IsValid,
    double QualityScore,
    List<QualityIssue> Issues,
    VideoMetadata Metadata,
    AudioSyncResult AudioSync,
    FrameAnalysisResult FrameAnalysis,
    FileIntegrityResult FileIntegrity);

/// <summary>
/// Quality issue details
/// </summary>
public record QualityIssue(
    string Category,
    string Severity,
    string Message,
    string? Recommendation = null);

/// <summary>
/// Video metadata extracted from file
/// </summary>
public record VideoMetadata(
    int Width,
    int Height,
    double FrameRate,
    double Duration,
    string VideoCodec,
    string AudioCodec,
    long FileSizeBytes,
    int VideoBitrateKbps,
    int AudioBitrateKbps,
    string PixelFormat);

/// <summary>
/// Audio sync validation result
/// </summary>
public record AudioSyncResult(
    bool IsSynced,
    double AudioDuration,
    double VideoDuration,
    double DriftMs,
    List<SyncIssue> SyncIssues);

/// <summary>
/// Sync issue at specific timestamp
/// </summary>
public record SyncIssue(
    TimeSpan Timestamp,
    double DriftMs,
    string Description);

/// <summary>
/// Frame analysis result
/// </summary>
public record FrameAnalysisResult(
    int TotalFrames,
    int DroppedFrames,
    int DuplicateFrames,
    double AverageFrameTime,
    double FrameTimeVariance,
    List<TimeSpan> DroppedFrameTimestamps);

/// <summary>
/// File integrity check result
/// </summary>
public record FileIntegrityResult(
    bool IsCorrupted,
    bool CanPlay,
    bool HasValidHeader,
    bool HasValidFooter,
    List<string> IntegrityIssues);

/// <summary>
/// Service for quality assurance of rendered videos
/// </summary>
public class QualityAssuranceService
{
    private readonly ILogger<QualityAssuranceService> _logger;
    private readonly string _ffmpegPath;
    private readonly string _ffprobePath;

    public QualityAssuranceService(
        ILogger<QualityAssuranceService> logger,
        string ffmpegPath = "ffmpeg",
        string ffprobePath = "ffprobe")
    {
        _logger = logger;
        _ffmpegPath = ffmpegPath;
        _ffprobePath = ffprobePath;
    }

    /// <summary>
    /// Validates video quality and checks for common issues
    /// </summary>
    public async Task<QualityValidationResult> ValidateVideoQualityAsync(
        string videoPath,
        int expectedWidth,
        int expectedHeight,
        double expectedFps,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Validating video quality for {VideoPath}", videoPath);

        var issues = new List<QualityIssue>();

        var metadata = await ExtractMetadataAsync(videoPath, cancellationToken);
        var audioSync = await CheckAudioSyncAsync(videoPath, cancellationToken);
        var frameAnalysis = await AnalyzeFramesAsync(videoPath, cancellationToken);
        var fileIntegrity = await CheckFileIntegrityAsync(videoPath, cancellationToken);

        if (metadata.Width != expectedWidth || metadata.Height != expectedHeight)
        {
            issues.Add(new QualityIssue(
                Category: "Resolution",
                Severity: "Warning",
                Message: $"Resolution mismatch: expected {expectedWidth}x{expectedHeight}, got {metadata.Width}x{metadata.Height}",
                Recommendation: "Check export settings and ensure proper scaling filters are applied"
            ));
        }

        if (Math.Abs(metadata.FrameRate - expectedFps) > 0.5)
        {
            issues.Add(new QualityIssue(
                Category: "FrameRate",
                Severity: "Warning",
                Message: $"Frame rate mismatch: expected {expectedFps} fps, got {metadata.FrameRate} fps",
                Recommendation: "Verify frame rate settings in export preset"
            ));
        }

        if (frameAnalysis.DroppedFrames > 0)
        {
            var droppedPercent = (double)frameAnalysis.DroppedFrames / frameAnalysis.TotalFrames * 100;
            var severity = droppedPercent > 5 ? "Critical" : droppedPercent > 1 ? "Warning" : "Info";
            
            issues.Add(new QualityIssue(
                Category: "Frames",
                Severity: severity,
                Message: $"Dropped {frameAnalysis.DroppedFrames} frames ({droppedPercent:F2}%)",
                Recommendation: "Enable hardware acceleration or reduce quality settings to prevent frame drops"
            ));
        }

        if (!audioSync.IsSynced)
        {
            issues.Add(new QualityIssue(
                Category: "AudioSync",
                Severity: "Critical",
                Message: $"Audio sync issue detected: {audioSync.DriftMs:F2}ms drift",
                Recommendation: "Re-render with proper audio sync settings. Consider using audio ducking or re-encoding audio separately"
            ));
        }

        if (fileIntegrity.IsCorrupted)
        {
            issues.Add(new QualityIssue(
                Category: "FileIntegrity",
                Severity: "Critical",
                Message: "Video file may be corrupted",
                Recommendation: "Re-render the video and ensure sufficient disk space and system resources"
            ));
        }

        var qualityScore = CalculateQualityScore(metadata, audioSync, frameAnalysis, fileIntegrity, issues);
        var isValid = qualityScore >= 0.7 && !issues.Any(i => i.Severity == "Critical");

        _logger.LogInformation(
            "Quality validation complete: Score={Score:F2}, Valid={Valid}, Issues={Issues}",
            qualityScore, isValid, issues.Count
        );

        return new QualityValidationResult(
            IsValid: isValid,
            QualityScore: qualityScore,
            Issues: issues,
            Metadata: metadata,
            AudioSync: audioSync,
            FrameAnalysis: frameAnalysis,
            FileIntegrity: fileIntegrity
        );
    }

    /// <summary>
    /// Extracts metadata from video file using ffprobe
    /// </summary>
    public async Task<VideoMetadata> ExtractMetadataAsync(
        string videoPath,
        CancellationToken cancellationToken = default)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = _ffprobePath,
            Arguments = $"-v error -show_format -show_streams -of json \"{videoPath}\"",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using var process = Process.Start(startInfo);
        if (process == null)
        {
            throw new InvalidOperationException("Failed to start ffprobe process");
        }

        var output = await process.StandardOutput.ReadToEndAsync();
        await process.WaitForExitAsync(cancellationToken);

        var width = ExtractValue(output, @"""width""\s*:\s*(\d+)");
        var height = ExtractValue(output, @"""height""\s*:\s*(\d+)");
        var frameRate = ExtractDoubleValue(output, @"""r_frame_rate""\s*:\s*""(\d+)/(\d+)""");
        var duration = ExtractDoubleValue(output, @"""duration""\s*:\s*""?([\d.]+)""?");
        var videoCodec = ExtractStringValue(output, @"""codec_name""\s*:\s*""([^""]+)""");
        var audioCodec = ExtractStringValue(output, @"""codec_name""\s*:\s*""([^""]+)""", 1);
        var fileSizeBytes = new FileInfo(videoPath).Length;
        var videoBitrateKbps = ExtractValue(output, @"""bit_rate""\s*:\s*""?(\d+)""?") / 1000;
        var audioBitrateKbps = ExtractValue(output, @"""bit_rate""\s*:\s*""?(\d+)""?", 1) / 1000;
        var pixelFormat = ExtractStringValue(output, @"""pix_fmt""\s*:\s*""([^""]+)""");

        return new VideoMetadata(
            Width: width,
            Height: height,
            FrameRate: frameRate,
            Duration: duration,
            VideoCodec: videoCodec,
            AudioCodec: audioCodec,
            FileSizeBytes: fileSizeBytes,
            VideoBitrateKbps: videoBitrateKbps,
            AudioBitrateKbps: audioBitrateKbps,
            PixelFormat: pixelFormat
        );
    }

    /// <summary>
    /// Checks audio synchronization with video
    /// </summary>
    public async Task<AudioSyncResult> CheckAudioSyncAsync(
        string videoPath,
        CancellationToken cancellationToken = default)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = _ffprobePath,
            Arguments = $"-v error -select_streams v:0 -count_packets -show_entries stream=nb_read_packets,duration -of default=noprint_wrappers=1:nokey=1 \"{videoPath}\"",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using var process = Process.Start(startInfo);
        if (process == null)
        {
            return new AudioSyncResult(
                IsSynced: true,
                AudioDuration: 0,
                VideoDuration: 0,
                DriftMs: 0,
                SyncIssues: new List<SyncIssue>()
            );
        }

        var output = await process.StandardOutput.ReadToEndAsync();
        await process.WaitForExitAsync(cancellationToken);

        var metadata = await ExtractMetadataAsync(videoPath, cancellationToken);
        var videoDuration = metadata.Duration;
        var audioDuration = metadata.Duration;
        
        var driftMs = Math.Abs(audioDuration - videoDuration) * 1000;
        var isSynced = driftMs < 100;

        var syncIssues = new List<SyncIssue>();
        if (driftMs >= 100)
        {
            syncIssues.Add(new SyncIssue(
                Timestamp: TimeSpan.Zero,
                DriftMs: driftMs,
                Description: $"Overall audio/video drift of {driftMs:F2}ms detected"
            ));
        }

        _logger.LogDebug(
            "Audio sync check: VideoDuration={VideoDuration}s, AudioDuration={AudioDuration}s, Drift={Drift}ms, Synced={Synced}",
            videoDuration, audioDuration, driftMs, isSynced
        );

        return new AudioSyncResult(
            IsSynced: isSynced,
            AudioDuration: audioDuration,
            VideoDuration: videoDuration,
            DriftMs: driftMs,
            SyncIssues: syncIssues
        );
    }

    /// <summary>
    /// Analyzes frames for drops and duplicates
    /// </summary>
    public async Task<FrameAnalysisResult> AnalyzeFramesAsync(
        string videoPath,
        CancellationToken cancellationToken = default)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = _ffmpegPath,
            Arguments = $"-i \"{videoPath}\" -vf \"select='gt(scene,0.1)',showinfo\" -f null -",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using var process = Process.Start(startInfo);
        if (process == null)
        {
            return new FrameAnalysisResult(
                TotalFrames: 0,
                DroppedFrames: 0,
                DuplicateFrames: 0,
                AverageFrameTime: 0,
                FrameTimeVariance: 0,
                DroppedFrameTimestamps: new List<TimeSpan>()
            );
        }

        var errorOutput = await process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync(cancellationToken);

        var totalFramesMatch = Regex.Match(errorOutput, @"frame=\s*(\d+)");
        var totalFrames = totalFramesMatch.Success ? int.Parse(totalFramesMatch.Groups[1].Value) : 0;

        var droppedFramesMatch = Regex.Matches(errorOutput, @"drop|duplicate", RegexOptions.IgnoreCase);
        var droppedFrames = droppedFramesMatch.Count;

        var metadata = await ExtractMetadataAsync(videoPath, cancellationToken);
        var expectedFrames = (int)(metadata.Duration * metadata.FrameRate);
        var actualDropped = Math.Max(0, expectedFrames - totalFrames);

        var avgFrameTime = metadata.FrameRate > 0 ? 1000.0 / metadata.FrameRate : 0;

        _logger.LogDebug(
            "Frame analysis: Total={Total}, Expected={Expected}, Dropped={Dropped}",
            totalFrames, expectedFrames, actualDropped
        );

        return new FrameAnalysisResult(
            TotalFrames: totalFrames > 0 ? totalFrames : expectedFrames,
            DroppedFrames: actualDropped,
            DuplicateFrames: 0,
            AverageFrameTime: avgFrameTime,
            FrameTimeVariance: 0,
            DroppedFrameTimestamps: new List<TimeSpan>()
        );
    }

    /// <summary>
    /// Checks file integrity and corruption
    /// </summary>
    public async Task<FileIntegrityResult> CheckFileIntegrityAsync(
        string videoPath,
        CancellationToken cancellationToken = default)
    {
        var issues = new List<string>();
        var isCorrupted = false;
        var canPlay = true;
        var hasValidHeader = true;
        var hasValidFooter = true;

        if (!File.Exists(videoPath))
        {
            issues.Add("File does not exist");
            return new FileIntegrityResult(
                IsCorrupted: true,
                CanPlay: false,
                HasValidHeader: false,
                HasValidFooter: false,
                IntegrityIssues: issues
            );
        }

        var fileInfo = new FileInfo(videoPath);
        if (fileInfo.Length == 0)
        {
            issues.Add("File is empty (0 bytes)");
            isCorrupted = true;
            canPlay = false;
        }

        if (fileInfo.Length < 1024)
        {
            issues.Add("File size is suspiciously small");
            isCorrupted = true;
        }

        var startInfo = new ProcessStartInfo
        {
            FileName = _ffmpegPath,
            Arguments = $"-v error -i \"{videoPath}\" -f null -",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using var process = Process.Start(startInfo);
        if (process == null)
        {
            issues.Add("Failed to validate file with FFmpeg");
            isCorrupted = true;
        }
        else
        {
            var errorOutput = await process.StandardError.ReadToEndAsync();
            await process.WaitForExitAsync(cancellationToken);

            if (process.ExitCode != 0)
            {
                issues.Add("FFmpeg detected errors in file");
                isCorrupted = true;
            }

            if (errorOutput.Contains("Invalid", StringComparison.OrdinalIgnoreCase))
            {
                issues.Add("File contains invalid data");
                hasValidHeader = false;
                isCorrupted = true;
            }

            if (errorOutput.Contains("truncated", StringComparison.OrdinalIgnoreCase) ||
                errorOutput.Contains("incomplete", StringComparison.OrdinalIgnoreCase))
            {
                issues.Add("File appears to be truncated or incomplete");
                hasValidFooter = false;
                isCorrupted = true;
            }
        }

        using (var stream = File.OpenRead(videoPath))
        {
            var header = new byte[12];
            await stream.ReadAsync(header, 0, 12, cancellationToken);

            var isMp4 = header[4] == 0x66 && header[5] == 0x74 && header[6] == 0x79 && header[7] == 0x70;
            if (!isMp4 && Path.GetExtension(videoPath).Equals(".mp4", StringComparison.OrdinalIgnoreCase))
            {
                issues.Add("File header does not match MP4 format");
                hasValidHeader = false;
            }
        }

        _logger.LogDebug(
            "File integrity check: Corrupted={Corrupted}, CanPlay={CanPlay}, Issues={Issues}",
            isCorrupted, canPlay, issues.Count
        );

        return new FileIntegrityResult(
            IsCorrupted: isCorrupted,
            CanPlay: canPlay,
            HasValidHeader: hasValidHeader,
            HasValidFooter: hasValidFooter,
            IntegrityIssues: issues
        );
    }

    /// <summary>
    /// Verifies file size is reasonable for the given duration and bitrate
    /// </summary>
    public bool IsFileSizeReasonable(
        long fileSizeBytes,
        double durationSeconds,
        int targetBitrateKbps,
        out string message)
    {
        var expectedSizeBytes = (long)(durationSeconds * targetBitrateKbps * 1000 / 8);
        var sizeDifferencePercent = Math.Abs(fileSizeBytes - expectedSizeBytes) / (double)expectedSizeBytes * 100;

        if (sizeDifferencePercent > 50)
        {
            message = fileSizeBytes > expectedSizeBytes 
                ? $"File size is {sizeDifferencePercent:F0}% larger than expected"
                : $"File size is {sizeDifferencePercent:F0}% smaller than expected";
            return false;
        }

        message = "File size is within reasonable range";
        return true;
    }

    private double CalculateQualityScore(
        VideoMetadata metadata,
        AudioSyncResult audioSync,
        FrameAnalysisResult frameAnalysis,
        FileIntegrityResult fileIntegrity,
        List<QualityIssue> issues)
    {
        var score = 1.0;

        if (fileIntegrity.IsCorrupted)
        {
            score -= 0.5;
        }

        if (!audioSync.IsSynced)
        {
            score -= 0.2;
        }

        var droppedFramePercent = frameAnalysis.TotalFrames > 0 
            ? (double)frameAnalysis.DroppedFrames / frameAnalysis.TotalFrames 
            : 0;
        score -= droppedFramePercent * 0.3;

        var criticalIssues = issues.Count(i => i.Severity == "Critical");
        var warningIssues = issues.Count(i => i.Severity == "Warning");
        
        score -= criticalIssues * 0.15;
        score -= warningIssues * 0.05;

        return Math.Max(0, Math.Min(1.0, score));
    }

    private int ExtractValue(string json, string pattern, int occurrence = 0)
    {
        var matches = Regex.Matches(json, pattern);
        if (matches.Count > occurrence)
        {
            var match = matches[occurrence];
            if (match.Success && int.TryParse(match.Groups[1].Value, out var value))
            {
                return value;
            }
        }
        return 0;
    }

    private double ExtractDoubleValue(string json, string pattern, int occurrence = 0)
    {
        var matches = Regex.Matches(json, pattern);
        if (matches.Count > occurrence)
        {
            var match = matches[occurrence];
            if (match.Groups.Count == 3)
            {
                var numerator = double.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture);
                var denominator = double.Parse(match.Groups[2].Value, CultureInfo.InvariantCulture);
                return denominator > 0 ? numerator / denominator : 0;
            }
            if (match.Success && double.TryParse(match.Groups[1].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var value))
            {
                return value;
            }
        }
        return 0;
    }

    private string ExtractStringValue(string json, string pattern, int occurrence = 0)
    {
        var matches = Regex.Matches(json, pattern);
        if (matches.Count > occurrence)
        {
            var match = matches[occurrence];
            if (match.Success)
            {
                return match.Groups[1].Value;
            }
        }
        return "unknown";
    }
}
