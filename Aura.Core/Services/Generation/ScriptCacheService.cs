using System;
using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Aura.Core.Models;
using Aura.Core.Models.Generation;
using Microsoft.Extensions.Logging;

namespace Aura.Core.Services.Generation;

/// <summary>
/// Caches generated scripts to reduce API calls and improve performance
/// </summary>
public class ScriptCacheService : IDisposable
{
    private readonly ILogger<ScriptCacheService> _logger;
    private readonly ConcurrentDictionary<string, CachedScript> _cache;
    private readonly ConcurrentDictionary<string, int> _cacheHits;
    private Timer? _cleanupTimer;
    private bool _disposed;

    public ScriptCacheService(ILogger<ScriptCacheService> logger)
    {
        _logger = logger;
        _cache = new ConcurrentDictionary<string, CachedScript>();
        _cacheHits = new ConcurrentDictionary<string, int>();
        
        _cleanupTimer = new Timer(CleanupExpiredEntries, null, TimeSpan.FromMinutes(5), TimeSpan.FromMinutes(5));
    }

    /// <summary>
    /// Get a cached script if available and not expired
    /// </summary>
    public Script? GetCachedScript(Brief brief, PlanSpec planSpec, string provider, string model)
    {
        var cacheKey = GenerateCacheKey(brief, planSpec, provider, model);
        
        if (_cache.TryGetValue(cacheKey, out var cachedScript))
        {
            if (DateTime.UtcNow < cachedScript.ExpiresAt)
            {
                _cacheHits.AddOrUpdate(provider, 1, (_, count) => count + 1);
                _logger.LogInformation("Cache HIT for provider {Provider}, key: {Key}", provider, cacheKey);
                return cachedScript.Script;
            }
            else
            {
                _cache.TryRemove(cacheKey, out _);
                _logger.LogDebug("Cache entry expired for key: {Key}", cacheKey);
            }
        }

        _logger.LogDebug("Cache MISS for provider {Provider}, key: {Key}", provider, cacheKey);
        return null;
    }

    /// <summary>
    /// Cache a generated script with appropriate TTL
    /// </summary>
    public void CacheScript(Brief brief, PlanSpec planSpec, string provider, string model, Script script)
    {
        var cacheKey = GenerateCacheKey(brief, planSpec, provider, model);
        var ttl = GetTtlForProvider(provider);
        
        var cachedScript = new CachedScript
        {
            Script = script,
            CachedAt = DateTime.UtcNow,
            ExpiresAt = DateTime.UtcNow.Add(ttl),
            Provider = provider,
            Model = model
        };

        _cache[cacheKey] = cachedScript;
        
        _logger.LogInformation("Cached script for provider {Provider} with TTL {TTL} minutes, key: {Key}",
            provider, ttl.TotalMinutes, cacheKey);
    }

    /// <summary>
    /// Clear all cached scripts
    /// </summary>
    public void ClearCache()
    {
        var count = _cache.Count;
        _cache.Clear();
        _cacheHits.Clear();
        
        _logger.LogInformation("Cleared cache ({Count} entries)", count);
    }

    /// <summary>
    /// Clear cache for a specific provider
    /// </summary>
    public void ClearProviderCache(string provider)
    {
        var removed = 0;
        
        foreach (var kvp in _cache)
        {
            if (kvp.Value.Provider.Equals(provider, StringComparison.OrdinalIgnoreCase))
            {
                if (_cache.TryRemove(kvp.Key, out _))
                {
                    removed++;
                }
            }
        }
        
        _logger.LogInformation("Cleared cache for provider {Provider} ({Count} entries)", provider, removed);
    }

    /// <summary>
    /// Get cache statistics
    /// </summary>
    public CacheStatistics GetStatistics()
    {
        var now = DateTime.UtcNow;
        var validEntries = 0;
        var expiredEntries = 0;

        foreach (var entry in _cache.Values)
        {
            if (now < entry.ExpiresAt)
            {
                validEntries++;
            }
            else
            {
                expiredEntries++;
            }
        }

        return new CacheStatistics
        {
            TotalEntries = _cache.Count,
            ValidEntries = validEntries,
            ExpiredEntries = expiredEntries,
            HitsByProvider = new Dictionary<string, int>(_cacheHits)
        };
    }

    /// <summary>
    /// Warm cache with common templates (for popular topics/configurations)
    /// </summary>
    public Task WarmCacheAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Cache warming not yet implemented");
        return Task.CompletedTask;
    }

    /// <summary>
    /// Generate a cache key from request parameters
    /// </summary>
    private string GenerateCacheKey(Brief brief, PlanSpec planSpec, string provider, string model)
    {
        var keyData = new
        {
            brief.Topic,
            brief.Audience,
            brief.Goal,
            brief.Tone,
            brief.Language,
            brief.Aspect,
            Duration = planSpec.TargetDuration.TotalSeconds,
            planSpec.Pacing,
            planSpec.Density,
            planSpec.Style,
            Provider = provider,
            Model = model
        };

        var json = JsonSerializer.Serialize(keyData);
        var hashBytes = SHA256.HashData(Encoding.UTF8.GetBytes(json));
        return Convert.ToHexString(hashBytes);
    }

    /// <summary>
    /// Get TTL for a provider
    /// - Paid providers: 1 hour (expensive API calls)
    /// - RuleBased: 24 hours (deterministic, no cost)
    /// - Ollama: 6 hours (local, no cost but may change with model updates)
    /// </summary>
    private TimeSpan GetTtlForProvider(string provider)
    {
        return provider.ToLowerInvariant() switch
        {
            "rulebased" => TimeSpan.FromHours(24),
            "ollama" => TimeSpan.FromHours(6),
            "openai" or "azure" or "gemini" or "anthropic" => TimeSpan.FromHours(1),
            _ => TimeSpan.FromHours(1)
        };
    }

    /// <summary>
    /// Cleanup expired entries periodically
    /// </summary>
    private void CleanupExpiredEntries(object? state)
    {
        try
        {
            var now = DateTime.UtcNow;
            var removed = 0;

            foreach (var kvp in _cache)
            {
                if (now >= kvp.Value.ExpiresAt)
                {
                    if (_cache.TryRemove(kvp.Key, out _))
                    {
                        removed++;
                    }
                }
            }

            if (removed > 0)
            {
                _logger.LogInformation("Cleaned up {Count} expired cache entries", removed);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during cache cleanup");
        }
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (!_disposed)
        {
            if (disposing)
            {
                _cleanupTimer?.Dispose();
            }
            _disposed = true;
        }
    }
}

/// <summary>
/// Cached script entry
/// </summary>
internal class CachedScript
{
    public Script Script { get; init; } = null!;
    public DateTime CachedAt { get; init; }
    public DateTime ExpiresAt { get; init; }
    public string Provider { get; init; } = string.Empty;
    public string Model { get; init; } = string.Empty;
}

/// <summary>
/// Cache statistics
/// </summary>
public class CacheStatistics
{
    public int TotalEntries { get; init; }
    public int ValidEntries { get; init; }
    public int ExpiredEntries { get; init; }
    public Dictionary<string, int> HitsByProvider { get; init; } = new();
}
