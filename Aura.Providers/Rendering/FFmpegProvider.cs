using System;
using System.Threading;
using System.Threading.Tasks;
using Aura.Core.Dependencies;
using Aura.Core.Models;
using Aura.Core.Providers;
using Aura.Core.Services.Render;
using Aura.Providers.Video;
using Microsoft.Extensions.Logging;

namespace Aura.Providers.Rendering;

/// <summary>
/// Primary FFmpeg provider that automatically detects and uses best available encoder
/// (hardware or software based on system capabilities)
/// </summary>
public class FFmpegProvider : BaseRenderingProvider
{
    private readonly FfmpegVideoComposer _videoComposer;
    private readonly HardwareEncoder _hardwareEncoder;

    public FFmpegProvider(
        ILogger<FFmpegProvider> logger,
        IFfmpegLocator ffmpegLocator,
        string? configuredFfmpegPath = null,
        string? outputDirectory = null)
        : base(logger, ffmpegLocator, configuredFfmpegPath)
    {
        _videoComposer = new FfmpegVideoComposer(
            logger as ILogger<FfmpegVideoComposer> ?? 
                Microsoft.Extensions.Logging.Abstractions.NullLogger<FfmpegVideoComposer>.Instance,
            ffmpegLocator,
            configuredFfmpegPath,
            outputDirectory);

        _hardwareEncoder = new HardwareEncoder(
            Microsoft.Extensions.Logging.Abstractions.NullLogger<HardwareEncoder>.Instance,
            "ffmpeg");
    }

    public override string Name => "FFmpeg";

    public override int Priority => 100;

    public override async Task<string> RenderVideoAsync(
        Timeline timeline,
        RenderSpec spec,
        IProgress<RenderProgress> progress,
        CancellationToken cancellationToken)
    {
        Logger.LogInformation("Starting FFmpeg render with auto-detection (Provider={Provider})", Name);
        
        try
        {
            var result = await _videoComposer.RenderAsync(timeline, spec, progress, cancellationToken);
            Logger.LogInformation("FFmpeg render completed successfully: {OutputPath}", result);
            return result;
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "FFmpeg render failed");
            throw;
        }
    }

    public override async Task<RenderingCapabilities> GetHardwareCapabilitiesAsync(
        CancellationToken cancellationToken = default)
    {
        var isAvailable = false;
        var isHardware = false;
        var accelerationType = "Software";
        var supportedCodecs = new[] { "h264", "h265", "vp9" };
        
        try
        {
            var ffmpegPath = await ResolveFfmpegPathAsync(cancellationToken);
            isAvailable = !string.IsNullOrEmpty(ffmpegPath);

            if (isAvailable)
            {
                var hwEncoder = new HardwareEncoder(
                    Microsoft.Extensions.Logging.Abstractions.NullLogger<HardwareEncoder>.Instance,
                    ffmpegPath);
                
                var capabilities = await hwEncoder.DetectHardwareCapabilitiesAsync();
                
                if (capabilities.HasNVENC)
                {
                    isHardware = true;
                    accelerationType = "NVENC";
                }
                else if (capabilities.HasAMF)
                {
                    isHardware = true;
                    accelerationType = "AMF";
                }
                else if (capabilities.HasQSV)
                {
                    isHardware = true;
                    accelerationType = "QuickSync";
                }
                else if (capabilities.HasVideoToolbox)
                {
                    isHardware = true;
                    accelerationType = "VideoToolbox";
                }
            }
        }
        catch (Exception ex)
        {
            Logger.LogWarning(ex, "Failed to detect hardware capabilities for FFmpegProvider");
            isAvailable = false;
        }

        var description = isHardware
            ? $"Hardware-accelerated encoding using {accelerationType}. 5-10x faster than software encoding."
            : "Software encoding (CPU). Compatible with all systems.";

        return new RenderingCapabilities(
            ProviderName: Name,
            IsHardwareAccelerated: isHardware,
            AccelerationType: accelerationType,
            IsAvailable: isAvailable,
            SupportedCodecs: supportedCodecs,
            Description: description
        );
    }
}
