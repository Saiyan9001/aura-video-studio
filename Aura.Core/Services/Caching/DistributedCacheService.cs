using System;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;

namespace Aura.Core.Services.Caching;

/// <summary>
/// Distributed cache service with Redis/In-Memory fallback and stampede protection
/// </summary>
public class DistributedCacheService : IDistributedCacheService
{
    private readonly IDistributedCache _distributedCache;
    private readonly IMemoryCache _memoryCache;
    private readonly ILogger<DistributedCacheService> _logger;
    private readonly SemaphoreSlim _stampedeProtection;
    private long _hits;
    private long _misses;
    private long _errors;

    public DistributedCacheService(
        IDistributedCache distributedCache,
        IMemoryCache memoryCache,
        ILogger<DistributedCacheService> logger)
    {
        _distributedCache = distributedCache;
        _memoryCache = memoryCache;
        _logger = logger;
        _stampedeProtection = new SemaphoreSlim(1, 1);
    }

    public async Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default) where T : class
    {
        try
        {
            if (_memoryCache.TryGetValue(key, out T? cachedValue))
            {
                Interlocked.Increment(ref _hits);
                _logger.LogDebug("Memory cache hit for key: {Key}", key);
                return cachedValue;
            }

            var bytes = await _distributedCache.GetAsync(key, cancellationToken);
            if (bytes != null)
            {
                var value = JsonSerializer.Deserialize<T>(bytes);
                if (value != null)
                {
                    _memoryCache.Set(key, value, TimeSpan.FromMinutes(5));
                    Interlocked.Increment(ref _hits);
                    _logger.LogDebug("Distributed cache hit for key: {Key}", key);
                    return value;
                }
            }

            Interlocked.Increment(ref _misses);
            _logger.LogDebug("Cache miss for key: {Key}", key);
            return null;
        }
        catch (Exception ex)
        {
            Interlocked.Increment(ref _errors);
            _logger.LogWarning(ex, "Cache get failed for key: {Key}, returning null", key);
            return null;
        }
    }

    public async Task SetAsync<T>(string key, T value, TimeSpan expiration, CancellationToken cancellationToken = default) where T : class
    {
        try
        {
            _memoryCache.Set(key, value, expiration > TimeSpan.FromMinutes(5) ? TimeSpan.FromMinutes(5) : expiration);

            var bytes = JsonSerializer.SerializeToUtf8Bytes(value);
            var options = new DistributedCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = expiration
            };

            await _distributedCache.SetAsync(key, bytes, options, cancellationToken);
            _logger.LogDebug("Cache set for key: {Key} with expiration: {Expiration}", key, expiration);
        }
        catch (Exception ex)
        {
            Interlocked.Increment(ref _errors);
            _logger.LogWarning(ex, "Cache set failed for key: {Key}", key);
        }
    }

    public async Task RemoveAsync(string key, CancellationToken cancellationToken = default)
    {
        try
        {
            _memoryCache.Remove(key);
            await _distributedCache.RemoveAsync(key, cancellationToken);
            _logger.LogDebug("Cache entry removed for key: {Key}", key);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Cache remove failed for key: {Key}", key);
        }
    }

    public async Task<T> GetOrCreateAsync<T>(
        string key,
        Func<CancellationToken, Task<T>> factory,
        TimeSpan expiration,
        CancellationToken cancellationToken = default) where T : class
    {
        var cached = await GetAsync<T>(key, cancellationToken);
        if (cached != null)
        {
            return cached;
        }

        await _stampedeProtection.WaitAsync(cancellationToken);
        try
        {
            cached = await GetAsync<T>(key, cancellationToken);
            if (cached != null)
            {
                return cached;
            }

            _logger.LogDebug("Creating cache entry for key: {Key}", key);
            var value = await factory(cancellationToken);
            await SetAsync(key, value, expiration, cancellationToken);
            return value;
        }
        finally
        {
            _stampedeProtection.Release();
        }
    }

    public async Task<bool> ExistsAsync(string key, CancellationToken cancellationToken = default)
    {
        try
        {
            if (_memoryCache.TryGetValue(key, out _))
            {
                return true;
            }

            var bytes = await _distributedCache.GetAsync(key, cancellationToken);
            return bytes != null;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Cache exists check failed for key: {Key}", key);
            return false;
        }
    }

    public CacheStatistics GetStatistics()
    {
        return new CacheStatistics
        {
            Hits = Interlocked.Read(ref _hits),
            Misses = Interlocked.Read(ref _misses),
            Errors = Interlocked.Read(ref _errors),
            BackendType = "Hybrid (Redis + Memory)"
        };
    }

    public async Task ClearAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogWarning("Clearing all cache entries");
        
        if (_memoryCache is MemoryCache memCache)
        {
            memCache.Compact(1.0);
        }

        await Task.CompletedTask;
    }
}
