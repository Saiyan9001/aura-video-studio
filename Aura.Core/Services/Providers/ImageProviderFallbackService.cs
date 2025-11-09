using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Aura.Core.Models;
using Aura.Core.Providers;
using Microsoft.Extensions.Logging;

namespace Aura.Core.Services.Providers;

/// <summary>
/// Service that manages image provider fallback chain
/// Tries providers in priority order: Stable Diffusion → Replicate → Stock Images
/// </summary>
public class ImageProviderFallbackService
{
    private readonly ILogger<ImageProviderFallbackService> _logger;
    private readonly ProviderHealthMonitoringService _healthMonitoring;
    private readonly ProviderCircuitBreakerService _circuitBreaker;
    private readonly List<ImageProviderEntry> _providers = new();

    public ImageProviderFallbackService(
        ILogger<ImageProviderFallbackService> logger,
        ProviderHealthMonitoringService healthMonitoring,
        ProviderCircuitBreakerService circuitBreaker)
    {
        _logger = logger;
        _healthMonitoring = healthMonitoring;
        _circuitBreaker = circuitBreaker;
    }

    /// <summary>
    /// Register an image provider with priority (lower number = higher priority)
    /// </summary>
    public void RegisterProvider(string name, IImageProvider provider, int priority, Func<Task<bool>>? availabilityCheck = null)
    {
        _providers.Add(new ImageProviderEntry
        {
            Name = name,
            Provider = provider,
            Priority = priority,
            AvailabilityCheck = availabilityCheck
        });

        _providers.Sort((a, b) => a.Priority.CompareTo(b.Priority));
        
        _logger.LogInformation("Registered image provider {Name} with priority {Priority}", name, priority);
    }

    /// <summary>
    /// Fetch or generate images using the fallback chain
    /// </summary>
    public async Task<IReadOnlyList<Asset>> FetchOrGenerateAsync(
        Scene scene,
        VisualSpec spec,
        CancellationToken ct)
    {
        _logger.LogInformation("Starting image generation for scene {SceneIndex} with {ProviderCount} providers available",
            scene.Index, _providers.Count);

        Exception? lastException = null;

        foreach (var entry in _providers)
        {
            if (!_circuitBreaker.CanExecute(entry.Name))
            {
                _logger.LogWarning("Provider {Name} circuit breaker is open, skipping", entry.Name);
                continue;
            }

            if (entry.AvailabilityCheck != null)
            {
                try
                {
                    var isAvailable = await entry.AvailabilityCheck();
                    if (!isAvailable)
                    {
                        _logger.LogInformation("Provider {Name} availability check failed, skipping", entry.Name);
                        continue;
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Error checking availability for provider {Name}, skipping", entry.Name);
                    continue;
                }
            }

            try
            {
                _logger.LogInformation("Attempting image generation with provider {Name}", entry.Name);
                
                var startTime = DateTime.UtcNow;
                var assets = await entry.Provider.FetchOrGenerateAsync(scene, spec, ct);
                var duration = DateTime.UtcNow - startTime;

                if (assets != null && assets.Count > 0)
                {
                    _logger.LogInformation("Successfully generated {Count} images with provider {Name} in {Duration}s",
                        assets.Count, entry.Name, duration.TotalSeconds);
                    
                    _healthMonitoring.RecordSuccess(entry.Name, duration.TotalSeconds);
                    _circuitBreaker.RecordSuccess(entry.Name);
                    
                    return assets;
                }
                else
                {
                    _logger.LogWarning("Provider {Name} returned no assets, trying next provider", entry.Name);
                    lastException = new InvalidOperationException($"Provider {entry.Name} returned no assets");
                }
            }
            catch (Exception ex)
            {
                lastException = ex;
                _logger.LogError(ex, "Error generating images with provider {Name}, trying next provider", entry.Name);
                
                _healthMonitoring.RecordFailure(entry.Name, ex.Message);
                _circuitBreaker.RecordFailure(entry.Name, ex);
            }
        }

        _logger.LogError("All image providers failed for scene {SceneIndex}", scene.Index);
        
        if (lastException != null)
        {
            throw new InvalidOperationException(
                $"All image providers failed. Last error: {lastException.Message}", 
                lastException);
        }
        
        throw new InvalidOperationException("All image providers failed. No providers available or all returned empty results.");
    }

    /// <summary>
    /// Get the list of registered providers in priority order
    /// </summary>
    public IReadOnlyList<string> GetProviderNames()
    {
        return _providers.Select(p => p.Name).ToList();
    }

    /// <summary>
    /// Check if a specific provider is registered
    /// </summary>
    public bool HasProvider(string name)
    {
        return _providers.Any(p => p.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
    }

    private class ImageProviderEntry
    {
        public required string Name { get; init; }
        public required IImageProvider Provider { get; init; }
        public required int Priority { get; init; }
        public Func<Task<bool>>? AvailabilityCheck { get; init; }
    }
}
