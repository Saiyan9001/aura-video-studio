using Microsoft.Extensions.Caching.Memory;

namespace Aura.Api.Services.Caching;

/// <summary>
/// Caching service for expensive operations
/// - Media metadata: 1 hour
/// - Project lists: 5 minutes
/// - Provider health: 2 minutes
/// </summary>
public class CachingService
{
    private readonly IMemoryCache _cache;
    private readonly ILogger<CachingService> _logger;

    // Cache key prefixes
    private const string MediaMetadataPrefix = "media:metadata:";
    private const string ProjectListPrefix = "project:list:";
    private const string ProviderHealthPrefix = "provider:health:";
    private const string AssetLibraryPrefix = "asset:library:";

    // Cache durations
    private static readonly TimeSpan MediaMetadataDuration = TimeSpan.FromHours(1);
    private static readonly TimeSpan ProjectListDuration = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan ProviderHealthDuration = TimeSpan.FromMinutes(2);
    private static readonly TimeSpan AssetLibraryDuration = TimeSpan.FromMinutes(10);

    public CachingService(IMemoryCache cache, ILogger<CachingService> logger)
    {
        _cache = cache;
        _logger = logger;
    }

    #region Media Metadata Caching

    /// <summary>
    /// Gets media metadata from cache or executes the factory function
    /// </summary>
    public async Task<T> GetOrCreateMediaMetadataAsync<T>(
        string mediaPath,
        Func<Task<T>> factory,
        CancellationToken ct = default)
    {
        var cacheKey = $"{MediaMetadataPrefix}{mediaPath}";

        if (_cache.TryGetValue(cacheKey, out T? cached) && cached != null)
        {
            _logger.LogDebug("Cache hit for media metadata: {Path}", mediaPath);
            return cached;
        }

        _logger.LogDebug("Cache miss for media metadata: {Path}", mediaPath);
        var result = await factory();

        var options = new MemoryCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = MediaMetadataDuration,
            Size = 1 // Assign size for eviction policy
        };

        _cache.Set(cacheKey, result, options);
        return result;
    }

    /// <summary>
    /// Invalidates media metadata cache for a specific file
    /// </summary>
    public void InvalidateMediaMetadata(string mediaPath)
    {
        var cacheKey = $"{MediaMetadataPrefix}{mediaPath}";
        _cache.Remove(cacheKey);
        _logger.LogDebug("Invalidated media metadata cache: {Path}", mediaPath);
    }

    #endregion

    #region Project List Caching

    /// <summary>
    /// Gets project list from cache or executes the factory function
    /// </summary>
    public async Task<T> GetOrCreateProjectListAsync<T>(
        string userId,
        Func<Task<T>> factory,
        CancellationToken ct = default)
    {
        var cacheKey = $"{ProjectListPrefix}{userId}";

        if (_cache.TryGetValue(cacheKey, out T? cached) && cached != null)
        {
            _logger.LogDebug("Cache hit for project list: {UserId}", userId);
            return cached;
        }

        _logger.LogDebug("Cache miss for project list: {UserId}", userId);
        var result = await factory();

        var options = new MemoryCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = ProjectListDuration,
            Size = 1
        };

        _cache.Set(cacheKey, result, options);
        return result;
    }

    /// <summary>
    /// Invalidates project list cache for a specific user
    /// Call this when projects are created, updated, or deleted
    /// </summary>
    public void InvalidateProjectList(string userId)
    {
        var cacheKey = $"{ProjectListPrefix}{userId}";
        _cache.Remove(cacheKey);
        _logger.LogDebug("Invalidated project list cache: {UserId}", userId);
    }

    #endregion

    #region Provider Health Caching

    /// <summary>
    /// Gets provider health status from cache or executes the factory function
    /// </summary>
    public async Task<T> GetOrCreateProviderHealthAsync<T>(
        string providerName,
        Func<Task<T>> factory,
        CancellationToken ct = default)
    {
        var cacheKey = $"{ProviderHealthPrefix}{providerName}";

        if (_cache.TryGetValue(cacheKey, out T? cached) && cached != null)
        {
            _logger.LogDebug("Cache hit for provider health: {Provider}", providerName);
            return cached;
        }

        _logger.LogDebug("Cache miss for provider health: {Provider}", providerName);
        var result = await factory();

        var options = new MemoryCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = ProviderHealthDuration,
            Size = 1
        };

        _cache.Set(cacheKey, result, options);
        return result;
    }

    /// <summary>
    /// Invalidates provider health cache
    /// </summary>
    public void InvalidateProviderHealth(string providerName)
    {
        var cacheKey = $"{ProviderHealthPrefix}{providerName}";
        _cache.Remove(cacheKey);
        _logger.LogDebug("Invalidated provider health cache: {Provider}", providerName);
    }

    #endregion

    #region Asset Library Caching

    /// <summary>
    /// Gets asset library data from cache or executes the factory function
    /// </summary>
    public async Task<T> GetOrCreateAssetLibraryAsync<T>(
        string libraryKey,
        Func<Task<T>> factory,
        CancellationToken ct = default)
    {
        var cacheKey = $"{AssetLibraryPrefix}{libraryKey}";

        if (_cache.TryGetValue(cacheKey, out T? cached) && cached != null)
        {
            _logger.LogDebug("Cache hit for asset library: {Key}", libraryKey);
            return cached;
        }

        _logger.LogDebug("Cache miss for asset library: {Key}", libraryKey);
        var result = await factory();

        var options = new MemoryCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = AssetLibraryDuration,
            Size = 1
        };

        _cache.Set(cacheKey, result, options);
        return result;
    }

    /// <summary>
    /// Invalidates asset library cache
    /// </summary>
    public void InvalidateAssetLibrary(string libraryKey)
    {
        var cacheKey = $"{AssetLibraryPrefix}{libraryKey}";
        _cache.Remove(cacheKey);
        _logger.LogDebug("Invalidated asset library cache: {Key}", libraryKey);
    }

    #endregion

    #region Generic Caching

    /// <summary>
    /// Generic caching method with custom key and duration
    /// </summary>
    public async Task<T> GetOrCreateAsync<T>(
        string cacheKey,
        Func<Task<T>> factory,
        TimeSpan duration,
        CancellationToken ct = default)
    {
        if (_cache.TryGetValue(cacheKey, out T? cached) && cached != null)
        {
            _logger.LogDebug("Cache hit: {Key}", cacheKey);
            return cached;
        }

        _logger.LogDebug("Cache miss: {Key}", cacheKey);
        var result = await factory();

        var options = new MemoryCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = duration,
            Size = 1
        };

        _cache.Set(cacheKey, result, options);
        return result;
    }

    /// <summary>
    /// Removes an item from cache
    /// </summary>
    public void Invalidate(string cacheKey)
    {
        _cache.Remove(cacheKey);
        _logger.LogDebug("Invalidated cache: {Key}", cacheKey);
    }

    /// <summary>
    /// Clears all cached items (use sparingly)
    /// </summary>
    public void ClearAll()
    {
        if (_cache is MemoryCache memCache)
        {
            memCache.Compact(1.0); // Compact 100% = clear all
            _logger.LogWarning("Cleared all cache entries");
        }
        else
        {
            _logger.LogWarning("Cache clear requested but not supported by current implementation");
        }
    }

    #endregion

    #region Cache Statistics

    /// <summary>
    /// Gets cache statistics (if available)
    /// </summary>
    public object GetStatistics()
    {
        // MemoryCache doesn't expose statistics directly
        // This would require custom tracking if detailed stats are needed
        return new
        {
            message = "Cache statistics not available with current MemoryCache implementation",
            recommendation = "Consider implementing IDistributedCache with Redis for production statistics"
        };
    }

    #endregion
}
