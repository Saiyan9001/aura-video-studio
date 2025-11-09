using System;
using System.Threading;
using System.Threading.Tasks;
using Aura.Core.Dependencies;
using Aura.Core.Services.Render;
using Microsoft.Extensions.Logging;

namespace Aura.Core.Services.FFmpeg;

/// <summary>
/// Comprehensive FFmpeg status information
/// </summary>
public record FFmpegStatusInfo
{
    public bool Installed { get; init; }
    public bool Valid { get; init; }
    public string? Version { get; init; }
    public string? Path { get; init; }
    public string Source { get; init; } = "None";
    public string? Error { get; init; }
    public bool VersionMeetsRequirement { get; init; }
    public string? MinimumVersion { get; init; }
    public HardwareAcceleration HardwareAcceleration { get; init; } = new();
}

/// <summary>
/// Hardware acceleration support information
/// </summary>
public record HardwareAcceleration
{
    public bool NvencSupported { get; init; }
    public bool AmfSupported { get; init; }
    public bool QuickSyncSupported { get; init; }
    public bool VideoToolboxSupported { get; init; }
    public string[] AvailableEncoders { get; init; } = Array.Empty<string>();
}

/// <summary>
/// Service for comprehensive FFmpeg status checking
/// </summary>
public interface IFFmpegStatusService
{
    /// <summary>
    /// Get comprehensive FFmpeg status including version and hardware acceleration
    /// </summary>
    Task<FFmpegStatusInfo> GetStatusAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// Implementation of FFmpeg status service
/// </summary>
public class FFmpegStatusService : IFFmpegStatusService
{
    private readonly FFmpegResolver _resolver;
    private readonly ILogger<FFmpegStatusService> _logger;
    private readonly ILoggerFactory _loggerFactory;
    private const string MinimumRequiredVersion = "4.0";

    public FFmpegStatusService(
        FFmpegResolver resolver,
        ILoggerFactory loggerFactory,
        ILogger<FFmpegStatusService> logger)
    {
        _resolver = resolver ?? throw new ArgumentNullException(nameof(resolver));
        _loggerFactory = loggerFactory ?? throw new ArgumentNullException(nameof(loggerFactory));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<FFmpegStatusInfo> GetStatusAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Getting comprehensive FFmpeg status");

        var resolution = await _resolver.ResolveAsync(null, forceRefresh: true, cancellationToken);
        
        var hardwareAccel = new HardwareAcceleration();
        
        if (resolution.Found && resolution.IsValid && !string.IsNullOrEmpty(resolution.Path))
        {
            try
            {
                var hardwareLogger = _loggerFactory.CreateLogger<HardwareEncoder>();
                var hardwareEncoder = new HardwareEncoder(hardwareLogger, resolution.Path);
                
                var capabilities = await hardwareEncoder.DetectHardwareCapabilitiesAsync();
                hardwareAccel = new HardwareAcceleration
                {
                    NvencSupported = capabilities.HasNVENC,
                    AmfSupported = capabilities.HasAMF,
                    QuickSyncSupported = capabilities.HasQSV,
                    VideoToolboxSupported = capabilities.HasVideoToolbox,
                    AvailableEncoders = capabilities.AvailableEncoders.ToArray()
                };
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to detect hardware acceleration capabilities");
            }
        }

        var versionMeets = CheckVersionRequirement(resolution.Version);

        var status = new FFmpegStatusInfo
        {
            Installed = resolution.Found && resolution.IsValid,
            Valid = resolution.IsValid,
            Version = resolution.Version,
            Path = resolution.Path,
            Source = resolution.Source,
            Error = resolution.Error,
            VersionMeetsRequirement = versionMeets,
            MinimumVersion = MinimumRequiredVersion,
            HardwareAcceleration = hardwareAccel
        };

        _logger.LogInformation(
            "FFmpeg status: Installed={Installed}, Version={Version}, HW Accel: NVENC={NVENC}, AMF={AMF}, QSV={QSV}",
            status.Installed, status.Version, hardwareAccel.NvencSupported, hardwareAccel.AmfSupported, hardwareAccel.QuickSyncSupported);

        return status;
    }

    private bool CheckVersionRequirement(string? versionString)
    {
        if (string.IsNullOrEmpty(versionString))
        {
            return false;
        }

        try
        {
            var cleanVersion = ExtractVersionNumber(versionString);
            var version = Version.Parse(cleanVersion);
            var minVersion = Version.Parse(MinimumRequiredVersion);
            
            return version >= minVersion;
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Failed to parse version string: {Version}", versionString);
            return false;
        }
    }

    private string ExtractVersionNumber(string versionString)
    {
        var parts = versionString.Split(new[] { ' ', '-', '_' }, StringSplitOptions.RemoveEmptyEntries);
        
        foreach (var part in parts)
        {
            if (char.IsDigit(part[0]) && part.Contains('.'))
            {
                var versionPart = part.Split('-')[0];
                var numbers = string.Concat(versionPart.TakeWhile(c => char.IsDigit(c) || c == '.'));
                
                if (!string.IsNullOrEmpty(numbers))
                {
                    return numbers;
                }
            }
        }
        
        return "0.0.0";
    }
}
