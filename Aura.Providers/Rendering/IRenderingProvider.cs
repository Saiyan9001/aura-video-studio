using System;
using System.Threading;
using System.Threading.Tasks;
using Aura.Core.Models;
using Aura.Core.Providers;

namespace Aura.Providers.Rendering;

/// <summary>
/// Hardware capabilities for a rendering provider
/// </summary>
public record RenderingCapabilities(
    string ProviderName,
    bool IsHardwareAccelerated,
    string AccelerationType,
    bool IsAvailable,
    string[] SupportedCodecs,
    string Description);

/// <summary>
/// Options for video rendering
/// </summary>
public record RenderOptions(
    bool PreferHardware = true,
    int MaxRetries = 2,
    TimeSpan? Timeout = null);

/// <summary>
/// Interface for video rendering providers
/// </summary>
public interface IRenderingProvider
{
    /// <summary>
    /// Gets the name of this rendering provider
    /// </summary>
    string Name { get; }
    
    /// <summary>
    /// Gets the priority of this provider (higher is preferred)
    /// </summary>
    int Priority { get; }
    
    /// <summary>
    /// Renders a video from a timeline
    /// </summary>
    Task<string> RenderVideoAsync(
        Timeline timeline, 
        RenderSpec spec, 
        IProgress<RenderProgress> progress, 
        CancellationToken cancellationToken);
    
    /// <summary>
    /// Gets the hardware capabilities of this provider
    /// </summary>
    Task<RenderingCapabilities> GetHardwareCapabilitiesAsync(CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Checks if this provider is available for use
    /// </summary>
    Task<bool> IsAvailableAsync(CancellationToken cancellationToken = default);
}
