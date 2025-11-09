using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace Aura.Providers.Rendering;

/// <summary>
/// Service for selecting the best available rendering provider based on system capabilities and user tier
/// </summary>
public class RenderingProviderSelector
{
    private readonly ILogger<RenderingProviderSelector> _logger;
    private readonly IEnumerable<IRenderingProvider> _providers;

    public RenderingProviderSelector(
        ILogger<RenderingProviderSelector> logger,
        IEnumerable<IRenderingProvider> providers)
    {
        _logger = logger;
        _providers = providers;
    }

    /// <summary>
    /// Selects the best available provider based on user tier and hardware capabilities
    /// </summary>
    /// <param name="isPremium">Whether the user is premium (gets hardware priority)</param>
    /// <param name="preferHardware">Whether to prefer hardware acceleration (default true for premium)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The best available provider</returns>
    public async Task<IRenderingProvider> SelectBestProviderAsync(
        bool isPremium = false,
        bool? preferHardware = null,
        CancellationToken cancellationToken = default)
    {
        var useHardware = preferHardware ?? isPremium;
        
        _logger.LogInformation(
            "Selecting rendering provider (IsPremium={IsPremium}, PreferHardware={PreferHardware})",
            isPremium, useHardware);

        var orderedProviders = _providers
            .OrderByDescending(p => p.Priority)
            .ToList();

        foreach (var provider in orderedProviders)
        {
            try
            {
                var isAvailable = await provider.IsAvailableAsync(cancellationToken);
                
                if (!isAvailable)
                {
                    _logger.LogDebug("Provider {Provider} is not available, skipping", provider.Name);
                    continue;
                }

                var capabilities = await provider.GetHardwareCapabilitiesAsync(cancellationToken);
                
                if (!useHardware && capabilities.IsHardwareAccelerated)
                {
                    _logger.LogDebug(
                        "Skipping hardware provider {Provider} (hardware not preferred for user tier)",
                        provider.Name);
                    continue;
                }

                _logger.LogInformation(
                    "Selected provider: {Provider} (Hardware={IsHardware}, Type={AccelType})",
                    provider.Name, capabilities.IsHardwareAccelerated, capabilities.AccelerationType);

                return provider;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error checking provider {Provider}, skipping", provider.Name);
                continue;
            }
        }

        var fallback = orderedProviders.LastOrDefault();
        if (fallback == null)
        {
            throw new InvalidOperationException("No rendering providers available");
        }

        _logger.LogWarning("No preferred provider available, using fallback: {Provider}", fallback.Name);
        return fallback;
    }

    /// <summary>
    /// Gets all available providers with their capabilities
    /// </summary>
    public async Task<IReadOnlyList<(IRenderingProvider Provider, RenderingCapabilities Capabilities)>> 
        GetAvailableProvidersAsync(CancellationToken cancellationToken = default)
    {
        var result = new List<(IRenderingProvider, RenderingCapabilities)>();

        foreach (var provider in _providers.OrderByDescending(p => p.Priority))
        {
            try
            {
                var isAvailable = await provider.IsAvailableAsync(cancellationToken);
                if (isAvailable)
                {
                    var capabilities = await provider.GetHardwareCapabilitiesAsync(cancellationToken);
                    result.Add((provider, capabilities));
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error getting capabilities for provider {Provider}", provider.Name);
            }
        }

        return result;
    }

    /// <summary>
    /// Attempts to render with automatic fallback on hardware failure
    /// </summary>
    public async Task<string> RenderWithFallbackAsync(
        Core.Providers.Timeline timeline,
        Core.Models.RenderSpec spec,
        IProgress<Core.Models.RenderProgress> progress,
        bool isPremium = false,
        CancellationToken cancellationToken = default)
    {
        var providers = await GetAvailableProvidersAsync(cancellationToken);
        
        if (providers.Count == 0)
        {
            throw new InvalidOperationException("No rendering providers available");
        }

        var preferHardware = isPremium;
        var orderedProviders = providers
            .OrderByDescending(p => preferHardware ? (p.Capabilities.IsHardwareAccelerated ? 1 : 0) : 0)
            .ThenByDescending(p => p.Provider.Priority)
            .ToList();

        Exception? lastException = null;

        foreach (var (provider, capabilities) in orderedProviders)
        {
            try
            {
                _logger.LogInformation(
                    "Attempting render with provider {Provider} (Hardware={IsHardware})",
                    provider.Name, capabilities.IsHardwareAccelerated);

                var result = await provider.RenderVideoAsync(timeline, spec, progress, cancellationToken);
                
                _logger.LogInformation(
                    "Render successful with provider {Provider}: {OutputPath}",
                    provider.Name, result);

                return result;
            }
            catch (Exception ex)
            {
                lastException = ex;
                _logger.LogWarning(ex, 
                    "Render failed with provider {Provider}, trying next provider", 
                    provider.Name);
                
                continue;
            }
        }

        _logger.LogError(lastException, "All rendering providers failed");
        throw new InvalidOperationException(
            "Rendering failed with all available providers. See inner exception for details.",
            lastException);
    }
}
