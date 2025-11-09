using System;
using System.Threading;
using System.Threading.Tasks;
using Aura.Core.Dependencies;
using Aura.Core.Models;
using Aura.Core.Providers;
using Aura.Providers.Video;
using Microsoft.Extensions.Logging;

namespace Aura.Providers.Rendering;

/// <summary>
/// Basic FFmpeg provider using software-only encoding (libx264/libx265)
/// This is the most compatible fallback that works on any system with FFmpeg
/// </summary>
public class BasicFFmpegProvider : BaseRenderingProvider
{
    private readonly FfmpegVideoComposer _videoComposer;

    public BasicFFmpegProvider(
        ILogger<BasicFFmpegProvider> logger,
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

    public override string Name => "BasicFFmpeg";

    public override int Priority => 10;

    public override async Task<string> RenderVideoAsync(
        Timeline timeline,
        RenderSpec spec,
        IProgress<RenderProgress> progress,
        CancellationToken cancellationToken)
    {
        Logger.LogInformation("Starting basic FFmpeg software render (Provider={Provider})", Name);
        
        try
        {
            var result = await _videoComposer.RenderAsync(timeline, spec, progress, cancellationToken);
            Logger.LogInformation("Basic FFmpeg render completed successfully: {OutputPath}", result);
            return result;
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Basic FFmpeg render failed");
            throw;
        }
    }

    public override async Task<RenderingCapabilities> GetHardwareCapabilitiesAsync(
        CancellationToken cancellationToken = default)
    {
        var isAvailable = false;
        
        try
        {
            var ffmpegPath = await ResolveFfmpegPathAsync(cancellationToken);
            isAvailable = !string.IsNullOrEmpty(ffmpegPath);
        }
        catch
        {
            isAvailable = false;
        }

        return new RenderingCapabilities(
            ProviderName: Name,
            IsHardwareAccelerated: false,
            AccelerationType: "Software",
            IsAvailable: isAvailable,
            SupportedCodecs: new[] { "h264", "h265", "vp9" },
            Description: "Software-only encoding (CPU). Compatible with all systems but slower than hardware acceleration."
        );
    }
}
