using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace Aura.Core.Services.Render;

/// <summary>
/// Real-time rendering statistics
/// </summary>
public record RenderStats(
    double CurrentFps,
    double AverageFps,
    double CurrentBitrate,
    double Speed,
    TimeSpan Elapsed,
    TimeSpan Estimated,
    int FramesProcessed,
    int TotalFrames,
    double ProgressPercent,
    double CpuUsagePercent,
    double MemoryUsageMb,
    GpuUtilization? GpuStats);

/// <summary>
/// Rendering error information
/// </summary>
public record RenderError(
    DateTime Timestamp,
    string Message,
    string? Details,
    bool IsRecoverable);

/// <summary>
/// Preview frame information
/// </summary>
public record PreviewFrame(
    string FilePath,
    TimeSpan Timestamp,
    int Width,
    int Height);

/// <summary>
/// Monitors FFmpeg rendering process and provides real-time statistics
/// </summary>
public class RenderMonitor
{
    private readonly ILogger<RenderMonitor> _logger;
    private readonly HardwareEncoder _hardwareEncoder;
    private readonly Stopwatch _stopwatch;
    private readonly List<RenderError> _errors;
    private readonly List<double> _fpsHistory;
    private Process? _currentProcess;
    private RenderStats? _currentStats;
    private CancellationTokenSource? _monitoringCts;
    private Task? _monitoringTask;
    private int _processId;

    public RenderMonitor(ILogger<RenderMonitor> logger, HardwareEncoder hardwareEncoder)
    {
        _logger = logger;
        _hardwareEncoder = hardwareEncoder;
        _stopwatch = new Stopwatch();
        _errors = new List<RenderError>();
        _fpsHistory = new List<double>(100);
    }

    /// <summary>
    /// Current rendering statistics
    /// </summary>
    public RenderStats? CurrentStats => _currentStats;

    /// <summary>
    /// List of errors encountered during rendering
    /// </summary>
    public IReadOnlyList<RenderError> Errors => _errors.AsReadOnly();

    /// <summary>
    /// Start monitoring an FFmpeg process
    /// </summary>
    public void StartMonitoring(Process process, int totalFrames)
    {
        _currentProcess = process;
        _processId = process.Id;
        _stopwatch.Restart();
        _errors.Clear();
        _fpsHistory.Clear();

        _monitoringCts = new CancellationTokenSource();
        _monitoringTask = Task.Run(() => MonitorProcessAsync(totalFrames, _monitoringCts.Token));
    }

    /// <summary>
    /// Stop monitoring
    /// </summary>
    public async Task StopMonitoringAsync()
    {
        _monitoringCts?.Cancel();
        if (_monitoringTask != null)
        {
            try
            {
                await _monitoringTask;
            }
            catch (OperationCanceledException)
            {
            }
        }
        _stopwatch.Stop();
    }

    /// <summary>
    /// Parse FFmpeg progress output
    /// </summary>
    public void ParseProgressLine(string line, int totalFrames)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(line))
            {
                return;
            }

            var frameMatch = Regex.Match(line, @"frame=\s*(\d+)");
            var fpsMatch = Regex.Match(line, @"fps=\s*([\d.]+)");
            var bitrateMatch = Regex.Match(line, @"bitrate=\s*([\d.]+)");
            var speedMatch = Regex.Match(line, @"speed=\s*([\d.]+)x");
            var timeMatch = Regex.Match(line, @"time=(\d{2}):(\d{2}):(\d{2})\.(\d{2})");

            if (!frameMatch.Success)
            {
                return;
            }

            var framesProcessed = int.Parse(frameMatch.Groups[1].Value);
            var fps = fpsMatch.Success ? double.Parse(fpsMatch.Groups[1].Value, CultureInfo.InvariantCulture) : 0;
            var bitrate = bitrateMatch.Success ? double.Parse(bitrateMatch.Groups[1].Value, CultureInfo.InvariantCulture) : 0;
            var speed = speedMatch.Success ? double.Parse(speedMatch.Groups[1].Value, CultureInfo.InvariantCulture) : 1.0;

            _fpsHistory.Add(fps);
            if (_fpsHistory.Count > 100)
            {
                _fpsHistory.RemoveAt(0);
            }

            var avgFps = _fpsHistory.Count > 0 ? _fpsHistory.Average() : fps;
            var elapsed = _stopwatch.Elapsed;
            var progressPercent = totalFrames > 0 ? (double)framesProcessed / totalFrames * 100 : 0;
            
            var framesRemaining = totalFrames - framesProcessed;
            var estimatedTimeRemaining = avgFps > 0 
                ? TimeSpan.FromSeconds(framesRemaining / avgFps) 
                : TimeSpan.Zero;

            _currentStats = new RenderStats(
                CurrentFps: fps,
                AverageFps: avgFps,
                CurrentBitrate: bitrate,
                Speed: speed,
                Elapsed: elapsed,
                Estimated: estimatedTimeRemaining,
                FramesProcessed: framesProcessed,
                TotalFrames: totalFrames,
                ProgressPercent: progressPercent,
                CpuUsagePercent: 0,
                MemoryUsageMb: 0,
                GpuStats: null
            );
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Failed to parse progress line: {Line}", line);
        }
    }

    /// <summary>
    /// Detect errors in FFmpeg output
    /// </summary>
    public void ParseErrorLine(string line)
    {
        if (string.IsNullOrWhiteSpace(line))
        {
            return;
        }

        var errorPatterns = new[]
        {
            (@"Error", true),
            (@"Invalid", true),
            (@"not found", true),
            (@"Cannot", true),
            (@"Failed", true),
            (@"No such file", false),
            (@"Permission denied", false),
            (@"Out of memory", false),
            (@"Conversion failed", false)
        };

        foreach (var (pattern, isRecoverable) in errorPatterns)
        {
            if (Regex.IsMatch(line, pattern, RegexOptions.IgnoreCase))
            {
                var error = new RenderError(
                    Timestamp: DateTime.UtcNow,
                    Message: line.Length > 200 ? line.Substring(0, 200) + "..." : line,
                    Details: line,
                    IsRecoverable: isRecoverable
                );

                _errors.Add(error);
                _logger.LogWarning("Rendering error detected: {Message}", error.Message);
                break;
            }
        }
    }

    /// <summary>
    /// Background task to monitor system resources
    /// </summary>
    private async Task MonitorProcessAsync(int totalFrames, CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(1000, cancellationToken);

                var cpuUsage = await GetProcessCpuUsageAsync();
                var memoryUsage = await GetProcessMemoryUsageAsync();
                var gpuStats = await _hardwareEncoder.GetGpuUtilizationAsync();

                if (_currentStats != null)
                {
                    _currentStats = _currentStats with
                    {
                        CpuUsagePercent = cpuUsage,
                        MemoryUsageMb = memoryUsage,
                        GpuStats = gpuStats
                    };
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogDebug(ex, "Error monitoring process resources");
            }
        }
    }

    /// <summary>
    /// Get CPU usage of the rendering process
    /// </summary>
    private async Task<double> GetProcessCpuUsageAsync()
    {
        try
        {
            var process = Process.GetProcessById(_processId);
            var startTime = DateTime.UtcNow;
            var startCpuTime = process.TotalProcessorTime;

            await Task.Delay(500);

            var endTime = DateTime.UtcNow;
            var endCpuTime = process.TotalProcessorTime;

            var cpuUsedTime = (endCpuTime - startCpuTime).TotalMilliseconds;
            var totalTime = (endTime - startTime).TotalMilliseconds;
            var cpuUsagePercent = cpuUsedTime / (Environment.ProcessorCount * totalTime) * 100;

            return Math.Min(100, cpuUsagePercent);
        }
        catch
        {
            return 0;
        }
    }

    /// <summary>
    /// Get memory usage of the rendering process in MB
    /// </summary>
    private async Task<double> GetProcessMemoryUsageAsync()
    {
        try
        {
            var process = Process.GetProcessById(_processId);
            await Task.CompletedTask;
            return process.WorkingSet64 / 1024.0 / 1024.0;
        }
        catch
        {
            return 0;
        }
    }

    /// <summary>
    /// Generate a preview frame at the specified timestamp
    /// </summary>
    public async Task<PreviewFrame?> GeneratePreviewFrameAsync(
        string inputVideo,
        TimeSpan timestamp,
        string outputPath,
        int width = 640,
        int height = 360,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var timestampStr = $"{timestamp.Hours:D2}:{timestamp.Minutes:D2}:{timestamp.Seconds:D2}.{timestamp.Milliseconds:D3}";
            
            var startInfo = new ProcessStartInfo
            {
                FileName = "ffmpeg",
                Arguments = $"-y -ss {timestampStr} -i \"{inputVideo}\" -vframes 1 -vf scale={width}:{height} \"{outputPath}\"",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process = Process.Start(startInfo);
            if (process == null)
            {
                return null;
            }

            await process.WaitForExitAsync(cancellationToken);

            if (process.ExitCode == 0 && System.IO.File.Exists(outputPath))
            {
                return new PreviewFrame(
                    FilePath: outputPath,
                    Timestamp: timestamp,
                    Width: width,
                    Height: height
                );
            }

            return null;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to generate preview frame at {Timestamp}", timestamp);
            return null;
        }
    }

    /// <summary>
    /// Check if errors indicate rendering should be aborted
    /// </summary>
    public bool HasCriticalErrors()
    {
        return _errors.Any(e => !e.IsRecoverable);
    }

    /// <summary>
    /// Get rendering health status
    /// </summary>
    public string GetHealthStatus()
    {
        if (_currentStats == null)
        {
            return "Unknown";
        }

        if (HasCriticalErrors())
        {
            return "Critical";
        }

        if (_errors.Count > 0)
        {
            return "Warning";
        }

        if (_currentStats.CurrentFps < 1)
        {
            return "Slow";
        }

        if (_currentStats.Speed < 0.5)
        {
            return "Degraded";
        }

        return "Healthy";
    }
}
