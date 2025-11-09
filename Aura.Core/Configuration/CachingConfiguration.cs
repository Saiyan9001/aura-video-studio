using System;

namespace Aura.Core.Configuration;

/// <summary>
/// Cache configuration settings
/// </summary>
public class CachingConfiguration
{
    /// <summary>
    /// Whether caching is enabled globally
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Redis connection string (if using Redis)
    /// </summary>
    public string? RedisConnection { get; set; }

    /// <summary>
    /// Whether to use Redis (true) or in-memory only (false)
    /// </summary>
    public bool UseRedis { get; set; }

    /// <summary>
    /// Default expiration for cache entries in seconds
    /// </summary>
    public int DefaultExpirationSeconds { get; set; } = 300;

    /// <summary>
    /// Cache expiration strategies for different data types
    /// </summary>
    public CacheExpirationStrategies Strategies { get; set; } = new();

    /// <summary>
    /// Whether to enable cache warming on startup
    /// </summary>
    public bool EnableCacheWarming { get; set; } = true;

    /// <summary>
    /// Whether to enable stampede protection
    /// </summary>
    public bool EnableStampedeProtection { get; set; } = true;
}

/// <summary>
/// Cache expiration strategies for different data types
/// </summary>
public class CacheExpirationStrategies
{
    /// <summary>
    /// Provider responses (API rate limit optimization) - 5 minutes
    /// </summary>
    public int ProviderResponsesSeconds { get; set; } = 300;

    /// <summary>
    /// Generated scripts (reuse for variations) - 1 hour
    /// </summary>
    public int GeneratedScriptsSeconds { get; set; } = 3600;

    /// <summary>
    /// Audio files (expensive to generate) - 24 hours
    /// </summary>
    public int AudioFilesSeconds { get; set; } = 86400;

    /// <summary>
    /// Images (highest storage, longest TTL) - 7 days
    /// </summary>
    public int ImagesSeconds { get; set; } = 604800;

    /// <summary>
    /// User sessions - 30 minutes sliding expiration
    /// </summary>
    public int UserSessionsSeconds { get; set; } = 1800;

    /// <summary>
    /// Query results - 1 minute
    /// </summary>
    public int QueryResultsSeconds { get; set; } = 60;
}
