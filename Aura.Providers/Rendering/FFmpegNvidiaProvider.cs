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
/// FFmpeg provider using NVIDIA NVENC hardware acceleration
/// Requires NVIDIA GPU with NVENC support (GTX 10 series or newer recommended)
/// </summary>
public class FFmpegNvidiaProvider : BaseRenderingProvider
{
    private readonly FfmpegVideoComposer _videoComposer;

    public FFmpegNvidiaProvider(
        ILogger<FFmpegNvidiaProvider> logger,
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
    }

    public override string Name => "FFmpegNVENC";

    public override int Priority => 90;

    public override async Task<string> RenderVideoAsync(
        Timeline timeline,
        RenderSpec spec,
        IProgress<RenderProgress> progress,
        CancellationToken cancellationToken)
    {
        Logger.LogInformation("Starting FFmpeg NVENC hardware-accelerated render (Provider={Provider})", Name);
        
        try
        {
            var result = await _videoComposer.RenderAsync(timeline, spec, progress, cancellationToken);
            Logger.LogInformation("FFmpeg NVENC render completed successfully: {OutputPath}", result);
            return result;
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "FFmpeg NVENC render failed");
            throw;
        }
    }

    public override async Task<RenderingCapabilities> GetHardwareCapabilitiesAsync(
        CancellationToken cancellationToken = default)
    {
        var isAvailable = false;
        var hasNVENC = false;
        
        try
        {
            var ffmpegPath = await ResolveFfmpegPathAsync(cancellationToken);
            
            if (!string.IsNullOrEmpty(ffmpegPath))
            {
                var hwEncoder = new HardwareEncoder(
                    Microsoft.Extensions.Logging.Abstractions.NullLogger<HardwareEncoder>.Instance,
                    ffmpegPath);
                
                var capabilities = await hwEncoder.DetectHardwareCapabilitiesAsync();
                hasNVENC = capabilities.HasNVENC;
                isAvailable = hasNVENC;
            }
        }
        catch (Exception ex)
        {
            Logger.LogWarning(ex, "Failed to detect NVENC capabilities");
            isAvailable = false;
        }

        return new RenderingCapabilities(
            ProviderName: Name,
            IsHardwareAccelerated: true,
            AccelerationType: "NVENC",
            IsAvailable: isAvailable,
            SupportedCodecs: new[] { "h264_nvenc", "hevc_nvenc" },
            Description: hasNVENC
                ? "NVIDIA GPU hardware encoding (NVENC). 5-10x faster than CPU encoding."
                : "NVIDIA NVENC not available. Requires NVIDIA GPU with driver support."
        );
    }
}
