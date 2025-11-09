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
/// FFmpeg provider using Intel QuickSync hardware acceleration
/// Requires Intel CPU with integrated graphics and QuickSync support (6th gen or newer recommended)
/// </summary>
public class FFmpegIntelProvider : BaseRenderingProvider
{
    private readonly FfmpegVideoComposer _videoComposer;

    public FFmpegIntelProvider(
        ILogger<FFmpegIntelProvider> logger,
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

    public override string Name => "FFmpegQSV";

    public override int Priority => 70;

    public override async Task<string> RenderVideoAsync(
        Timeline timeline,
        RenderSpec spec,
        IProgress<RenderProgress> progress,
        CancellationToken cancellationToken)
    {
        Logger.LogInformation("Starting FFmpeg QuickSync hardware-accelerated render (Provider={Provider})", Name);
        
        try
        {
            var result = await _videoComposer.RenderAsync(timeline, spec, progress, cancellationToken);
            Logger.LogInformation("FFmpeg QuickSync render completed successfully: {OutputPath}", result);
            return result;
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "FFmpeg QuickSync render failed");
            throw;
        }
    }

    public override async Task<RenderingCapabilities> GetHardwareCapabilitiesAsync(
        CancellationToken cancellationToken = default)
    {
        var isAvailable = false;
        var hasQSV = false;
        
        try
        {
            var ffmpegPath = await ResolveFfmpegPathAsync(cancellationToken);
            
            if (!string.IsNullOrEmpty(ffmpegPath))
            {
                var hwEncoder = new HardwareEncoder(
                    Microsoft.Extensions.Logging.Abstractions.NullLogger<HardwareEncoder>.Instance,
                    ffmpegPath);
                
                var capabilities = await hwEncoder.DetectHardwareCapabilitiesAsync();
                hasQSV = capabilities.HasQSV;
                isAvailable = hasQSV;
            }
        }
        catch (Exception ex)
        {
            Logger.LogWarning(ex, "Failed to detect QuickSync capabilities");
            isAvailable = false;
        }

        return new RenderingCapabilities(
            ProviderName: Name,
            IsHardwareAccelerated: true,
            AccelerationType: "QuickSync",
            IsAvailable: isAvailable,
            SupportedCodecs: new[] { "h264_qsv", "hevc_qsv" },
            Description: hasQSV
                ? "Intel QuickSync hardware encoding. 3-5x faster than CPU encoding."
                : "Intel QuickSync not available. Requires Intel CPU with integrated graphics."
        );
    }
}
