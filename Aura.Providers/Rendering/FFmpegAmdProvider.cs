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
/// FFmpeg provider using AMD VCE (Video Coding Engine) hardware acceleration
/// Requires AMD GPU with VCE support (RX 400 series or newer recommended)
/// </summary>
public class FFmpegAmdProvider : BaseRenderingProvider
{
    private readonly FfmpegVideoComposer _videoComposer;

    public FFmpegAmdProvider(
        ILogger<FFmpegAmdProvider> logger,
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

    public override string Name => "FFmpegAMF";

    public override int Priority => 80;

    public override async Task<string> RenderVideoAsync(
        Timeline timeline,
        RenderSpec spec,
        IProgress<RenderProgress> progress,
        CancellationToken cancellationToken)
    {
        Logger.LogInformation("Starting FFmpeg AMF hardware-accelerated render (Provider={Provider})", Name);
        
        try
        {
            var result = await _videoComposer.RenderAsync(timeline, spec, progress, cancellationToken);
            Logger.LogInformation("FFmpeg AMF render completed successfully: {OutputPath}", result);
            return result;
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "FFmpeg AMF render failed");
            throw;
        }
    }

    public override async Task<RenderingCapabilities> GetHardwareCapabilitiesAsync(
        CancellationToken cancellationToken = default)
    {
        var isAvailable = false;
        var hasAMF = false;
        
        try
        {
            var ffmpegPath = await ResolveFfmpegPathAsync(cancellationToken);
            
            if (!string.IsNullOrEmpty(ffmpegPath))
            {
                var hwEncoder = new HardwareEncoder(
                    Microsoft.Extensions.Logging.Abstractions.NullLogger<HardwareEncoder>.Instance,
                    ffmpegPath);
                
                var capabilities = await hwEncoder.DetectHardwareCapabilitiesAsync();
                hasAMF = capabilities.HasAMF;
                isAvailable = hasAMF;
            }
        }
        catch (Exception ex)
        {
            Logger.LogWarning(ex, "Failed to detect AMF capabilities");
            isAvailable = false;
        }

        return new RenderingCapabilities(
            ProviderName: Name,
            IsHardwareAccelerated: true,
            AccelerationType: "AMF",
            IsAvailable: isAvailable,
            SupportedCodecs: new[] { "h264_amf", "hevc_amf" },
            Description: hasAMF
                ? "AMD GPU hardware encoding (VCE/AMF). 5-10x faster than CPU encoding."
                : "AMD AMF not available. Requires AMD GPU with driver support."
        );
    }
}
