using System;
using System.Threading;
using System.Threading.Tasks;
using Aura.Core.Dependencies;
using Aura.Core.Models;
using Aura.Core.Providers;
using Microsoft.Extensions.Logging;

namespace Aura.Providers.Rendering;

/// <summary>
/// Base class for all rendering providers with common functionality
/// </summary>
public abstract class BaseRenderingProvider : IRenderingProvider
{
    protected readonly ILogger Logger;
    protected readonly IFfmpegLocator FfmpegLocator;
    protected readonly string? ConfiguredFfmpegPath;

    protected BaseRenderingProvider(
        ILogger logger,
        IFfmpegLocator ffmpegLocator,
        string? configuredFfmpegPath = null)
    {
        Logger = logger;
        FfmpegLocator = ffmpegLocator;
        ConfiguredFfmpegPath = configuredFfmpegPath;
    }

    /// <inheritdoc/>
    public abstract string Name { get; }

    /// <inheritdoc/>
    public abstract int Priority { get; }

    /// <inheritdoc/>
    public abstract Task<string> RenderVideoAsync(
        Timeline timeline,
        RenderSpec spec,
        IProgress<RenderProgress> progress,
        CancellationToken cancellationToken);

    /// <inheritdoc/>
    public abstract Task<RenderingCapabilities> GetHardwareCapabilitiesAsync(
        CancellationToken cancellationToken = default);

    /// <inheritdoc/>
    public virtual async Task<bool> IsAvailableAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var capabilities = await GetHardwareCapabilitiesAsync(cancellationToken);
            return capabilities.IsAvailable;
        }
        catch (Exception ex)
        {
            Logger.LogWarning(ex, "Failed to check availability for provider {ProviderName}", Name);
            return false;
        }
    }

    /// <summary>
    /// Resolves the FFmpeg path to use for rendering
    /// </summary>
    protected async Task<string> ResolveFfmpegPathAsync(CancellationToken cancellationToken)
    {
        try
        {
            return await FfmpegLocator.GetEffectiveFfmpegPathAsync(ConfiguredFfmpegPath, cancellationToken);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Failed to resolve FFmpeg path for provider {ProviderName}", Name);
            throw new InvalidOperationException(
                $"FFmpeg not available for {Name}. Please install FFmpeg or configure the path.", ex);
        }
    }
}
