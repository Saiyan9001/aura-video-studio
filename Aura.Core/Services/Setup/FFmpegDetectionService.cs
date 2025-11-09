using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;

namespace Aura.Core.Services.Setup;

/// <summary>
/// Information about detected FFmpeg installation
/// </summary>
public record FFmpegInfo
{
    public bool IsInstalled { get; init; }
    public string? Path { get; init; }
    public string? Version { get; init; }
    public string? Error { get; init; }
}

/// <summary>
/// Service for detecting FFmpeg installation on the system
/// </summary>
public interface IFFmpegDetectionService
{
    /// <summary>
    /// Detects FFmpeg installation and returns information about it
    /// </summary>
    Task<FFmpegInfo> DetectFFmpegAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets cached FFmpeg detection status
    /// </summary>
    Task<FFmpegInfo> GetCachedStatusAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// Implementation of FFmpeg detection service with caching
/// </summary>
public class FFmpegDetectionService : IFFmpegDetectionService
{
    private readonly ILogger<FFmpegDetectionService> _logger;
    private readonly IMemoryCache _cache;
    private const string CacheKey = "ffmpeg:detection";
    private static readonly TimeSpan CacheDuration = TimeSpan.FromMinutes(10);

    public FFmpegDetectionService(
        ILogger<FFmpegDetectionService> logger,
        IMemoryCache cache)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _cache = cache ?? throw new ArgumentNullException(nameof(cache));
    }

    public async Task<FFmpegInfo> DetectFFmpegAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Starting FFmpeg detection");

        try
        {
            // Try to find FFmpeg in various locations
            var ffmpegPath = await FindFFmpegPathAsync(cancellationToken).ConfigureAwait(false);

            if (string.IsNullOrEmpty(ffmpegPath))
            {
                _logger.LogWarning("FFmpeg not found on system");
                var notFoundInfo = new FFmpegInfo
                {
                    IsInstalled = false,
                    Path = null,
                    Version = null,
                    Error = "FFmpeg not found in system PATH or common installation directories"
                };

                // Cache the result
                _cache.Set(CacheKey, notFoundInfo, CacheDuration);
                return notFoundInfo;
            }

            // Get FFmpeg version
            var version = await GetFFmpegVersionAsync(ffmpegPath, cancellationToken).ConfigureAwait(false);

            var info = new FFmpegInfo
            {
                IsInstalled = true,
                Path = ffmpegPath,
                Version = version,
                Error = null
            };

            _logger.LogInformation(
                "FFmpeg detected successfully at {Path}, version: {Version}",
                ffmpegPath,
                version ?? "unknown"
            );

            // Cache the result
            _cache.Set(CacheKey, info, CacheDuration);
            return info;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error detecting FFmpeg");
            var errorInfo = new FFmpegInfo
            {
                IsInstalled = false,
                Path = null,
                Version = null,
                Error = ex.Message
            };

            // Cache the error result for a shorter duration
            _cache.Set(CacheKey, errorInfo, TimeSpan.FromMinutes(2));
            return errorInfo;
        }
    }

    public async Task<FFmpegInfo> GetCachedStatusAsync(CancellationToken cancellationToken = default)
    {
        // Try to get from cache first
        if (_cache.TryGetValue(CacheKey, out FFmpegInfo? cachedInfo) && cachedInfo != null)
        {
            _logger.LogDebug("Returning cached FFmpeg detection result");
            return cachedInfo;
        }

        // If not in cache, detect it
        _logger.LogDebug("Cache miss, performing FFmpeg detection");
        return await DetectFFmpegAsync(cancellationToken).ConfigureAwait(false);
    }

    private async Task<string?> FindFFmpegPathAsync(CancellationToken cancellationToken)
    {
        // 1. Check application directory first
        var appDir = AppDomain.CurrentDomain.BaseDirectory;
        var appDirFFmpeg = Path.Combine(appDir, "ffmpeg");
        if (File.Exists(appDirFFmpeg))
        {
            _logger.LogDebug("Found FFmpeg in application directory: {Path}", appDirFFmpeg);
            return appDirFFmpeg;
        }

        // 2. Check system PATH using 'which' command (Unix) or 'where' (Windows)
        var pathResult = await FindInSystemPathAsync(cancellationToken).ConfigureAwait(false);
        if (!string.IsNullOrEmpty(pathResult))
        {
            _logger.LogDebug("Found FFmpeg in system PATH: {Path}", pathResult);
            return pathResult;
        }

        // 3. Check common Linux installation paths
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            var commonPaths = new[]
            {
                "/usr/bin/ffmpeg",
                "/usr/local/bin/ffmpeg",
                "/snap/bin/ffmpeg",
                "/opt/ffmpeg/bin/ffmpeg"
            };

            foreach (var path in commonPaths)
            {
                if (File.Exists(path))
                {
                    _logger.LogDebug("Found FFmpeg at common Linux path: {Path}", path);
                    return path;
                }
            }
        }

        // 4. Check common Windows installation paths
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            var commonPaths = new[]
            {
                @"C:\Program Files\ffmpeg\bin\ffmpeg.exe",
                @"C:\Program Files (x86)\ffmpeg\bin\ffmpeg.exe",
                @"C:\ffmpeg\bin\ffmpeg.exe"
            };

            foreach (var path in commonPaths)
            {
                if (File.Exists(path))
                {
                    _logger.LogDebug("Found FFmpeg at common Windows path: {Path}", path);
                    return path;
                }
            }
        }

        return null;
    }

    private async Task<string?> FindInSystemPathAsync(CancellationToken cancellationToken)
    {
        try
        {
            var command = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "where" : "which";
            var argument = "ffmpeg";

            using var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = command,
                    Arguments = argument,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                }
            };

            process.Start();
            var output = await process.StandardOutput.ReadToEndAsync(cancellationToken).ConfigureAwait(false);
            await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);

            if (process.ExitCode == 0 && !string.IsNullOrWhiteSpace(output))
            {
                // 'which' or 'where' returns the path on first line
                var path = output.Split('\n', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault()?.Trim();
                if (!string.IsNullOrEmpty(path) && File.Exists(path))
                {
                    return path;
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Error checking system PATH for FFmpeg");
        }

        return null;
    }

    private async Task<string?> GetFFmpegVersionAsync(string ffmpegPath, CancellationToken cancellationToken)
    {
        try
        {
            using var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = ffmpegPath,
                    Arguments = "-version",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                }
            };

            process.Start();
            var output = await process.StandardOutput.ReadToEndAsync(cancellationToken).ConfigureAwait(false);
            await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);

            if (process.ExitCode == 0 && !string.IsNullOrWhiteSpace(output))
            {
                // Parse version from output (usually first line: "ffmpeg version X.Y.Z ...")
                var firstLine = output.Split('\n', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();
                if (!string.IsNullOrEmpty(firstLine))
                {
                    // Extract version number using regex
                    var match = Regex.Match(firstLine, @"version\s+([^\s]+)");
                    if (match.Success)
                    {
                        return match.Groups[1].Value;
                    }
                }

                // Fallback: return first line if we can't parse version
                return firstLine?.Trim();
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error getting FFmpeg version from {Path}", ffmpegPath);
        }

        return "unknown";
    }
}
