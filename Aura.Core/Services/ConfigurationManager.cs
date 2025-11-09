using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Aura.Core.Data;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Aura.Core.Services;

/// <summary>
/// Singleton configuration manager with caching and change notification
/// </summary>
public class ConfigurationManager
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<ConfigurationManager> _logger;
    private readonly IMemoryCache _cache;
    private readonly ConcurrentDictionary<string, SemaphoreSlim> _keyLocks;
    private readonly TimeSpan _cacheExpiration = TimeSpan.FromMinutes(10);

    public ConfigurationManager(
        IServiceScopeFactory scopeFactory,
        ILogger<ConfigurationManager> logger,
        IMemoryCache cache)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
        _cache = cache;
        _keyLocks = new ConcurrentDictionary<string, SemaphoreSlim>();
    }

    /// <summary>
    /// Get a configuration value with caching
    /// </summary>
    public async Task<T?> GetAsync<T>(
        string key,
        T? defaultValue = default,
        CancellationToken ct = default)
    {
        var cacheKey = $"config:{key}";

        if (_cache.TryGetValue<T>(cacheKey, out var cachedValue))
        {
            _logger.LogDebug("Configuration {Key} retrieved from cache", key);
            return cachedValue;
        }

        using var scope = _scopeFactory.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<ConfigurationRepository>();

        var config = await repository.GetAsync(key, ct);

        if (config == null)
        {
            _logger.LogDebug(
                "Configuration {Key} not found, returning default value",
                key);
            return defaultValue;
        }

        var value = DeserializeValue<T>(config.Value, config.ValueType);

        _cache.Set(cacheKey, value, _cacheExpiration);

        _logger.LogDebug("Configuration {Key} retrieved from database and cached", key);

        return value;
    }

    /// <summary>
    /// Get a string configuration value
    /// </summary>
    public async Task<string?> GetStringAsync(
        string key,
        string? defaultValue = null,
        CancellationToken ct = default)
    {
        return await GetAsync(key, defaultValue, ct);
    }

    /// <summary>
    /// Get an integer configuration value
    /// </summary>
    public async Task<int> GetIntAsync(
        string key,
        int defaultValue = 0,
        CancellationToken ct = default)
    {
        var value = await GetAsync<int?>(key, defaultValue, ct);
        return value ?? defaultValue;
    }

    /// <summary>
    /// Get a boolean configuration value
    /// </summary>
    public async Task<bool> GetBoolAsync(
        string key,
        bool defaultValue = false,
        CancellationToken ct = default)
    {
        var value = await GetAsync<bool?>(key, defaultValue, ct);
        return value ?? defaultValue;
    }

    /// <summary>
    /// Get all configurations in a category with caching
    /// </summary>
    public async Task<Dictionary<string, string>> GetCategoryAsync(
        string category,
        CancellationToken ct = default)
    {
        var cacheKey = $"config:category:{category}";

        if (_cache.TryGetValue<Dictionary<string, string>>(cacheKey, out var cachedDict))
        {
            _logger.LogDebug("Configuration category {Category} retrieved from cache", category);
            return cachedDict;
        }

        using var scope = _scopeFactory.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<ConfigurationRepository>();

        var configs = await repository.GetByCategoryAsync(category, ct);

        var dict = configs.ToDictionary(c => c.Key, c => c.Value);

        _cache.Set(cacheKey, dict, _cacheExpiration);

        _logger.LogDebug(
            "Configuration category {Category} retrieved from database and cached ({Count} items)",
            category, dict.Count);

        return dict;
    }

    /// <summary>
    /// Set a configuration value and invalidate cache
    /// </summary>
    public async Task SetAsync<T>(
        string key,
        T value,
        string category,
        string? description = null,
        bool isSensitive = false,
        CancellationToken ct = default)
    {
        var keyLock = _keyLocks.GetOrAdd(key, _ => new SemaphoreSlim(1, 1));
        await keyLock.WaitAsync(ct);

        try
        {
            using var scope = _scopeFactory.CreateScope();
            var repository = scope.ServiceProvider.GetRequiredService<ConfigurationRepository>();

            var (serializedValue, valueType) = SerializeValue(value);

            await repository.SetAsync(
                key,
                serializedValue,
                category,
                valueType,
                description,
                isSensitive,
                Environment.UserName,
                ct);

            InvalidateCache(key, category);

            _logger.LogInformation(
                "Configuration {Key} set to new value in category {Category}",
                key, category);
        }
        finally
        {
            keyLock.Release();
        }
    }

    /// <summary>
    /// Set multiple configurations in a single transaction
    /// </summary>
    public async Task SetManyAsync(
        Dictionary<string, (object value, string category, string? description)> configurations,
        CancellationToken ct = default)
    {
        using var scope = _scopeFactory.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<ConfigurationRepository>();

        var serializedConfigs = new Dictionary<string, (string value, string category, string valueType)>();

        foreach (var kvp in configurations)
        {
            var (serializedValue, valueType) = SerializeValue(kvp.Value.value);
            serializedConfigs[kvp.Key] = (serializedValue, kvp.Value.category, valueType);
        }

        await repository.SetManyAsync(serializedConfigs, Environment.UserName, ct);

        foreach (var kvp in configurations)
        {
            InvalidateCache(kvp.Key, kvp.Value.category);
        }

        _logger.LogInformation(
            "Bulk set {Count} configurations",
            configurations.Count);
    }

    /// <summary>
    /// Delete a configuration
    /// </summary>
    public async Task<bool> DeleteAsync(string key, CancellationToken ct = default)
    {
        using var scope = _scopeFactory.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<ConfigurationRepository>();

        var result = await repository.DeleteAsync(key, ct);

        if (result)
        {
            InvalidateCache(key, null);
            _logger.LogInformation("Configuration {Key} deleted", key);
        }

        return result;
    }

    /// <summary>
    /// Get all configurations (for debugging/export)
    /// </summary>
    public async Task<List<ConfigurationEntity>> GetAllConfigurationsAsync(
        bool includeInactive = false,
        CancellationToken ct = default)
    {
        using var scope = _scopeFactory.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<ConfigurationRepository>();

        return await repository.GetAllAsync(includeInactive, ct);
    }

    /// <summary>
    /// Clear all cached configurations
    /// </summary>
    public void ClearCache()
    {
        _logger.LogInformation("Clearing all configuration cache");
        
        // Memory cache doesn't support clearing all entries
        // Cache entries will expire naturally based on _cacheExpiration
        // For immediate effect, we rely on category/key invalidation
    }

    /// <summary>
    /// Load configuration on startup with defaults
    /// </summary>
    public async Task InitializeAsync(CancellationToken ct = default)
    {
        _logger.LogInformation("Initializing configuration system");

        try
        {
            var defaults = GetDefaultConfigurations();

            using var scope = _scopeFactory.CreateScope();
            var repository = scope.ServiceProvider.GetRequiredService<ConfigurationRepository>();

            var existing = await repository.GetAllAsync(false, ct);
            var existingKeys = existing.Select(c => c.Key).ToHashSet();

            var newConfigs = defaults
                .Where(kvp => !existingKeys.Contains(kvp.Key))
                .ToDictionary(
                    kvp => kvp.Key,
                    kvp => (kvp.Value.value, kvp.Value.category, kvp.Value.valueType));

            if (newConfigs.Count > 0)
            {
                await repository.SetManyAsync(newConfigs, "System", ct);

                _logger.LogInformation(
                    "Created {Count} default configurations",
                    newConfigs.Count);
            }
            else
            {
                _logger.LogInformation("All default configurations already exist");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error initializing configuration system");
            throw;
        }
    }

    private void InvalidateCache(string key, string? category)
    {
        var cacheKey = $"config:{key}";
        _cache.Remove(cacheKey);

        if (!string.IsNullOrEmpty(category))
        {
            var categoryCacheKey = $"config:category:{category}";
            _cache.Remove(categoryCacheKey);
        }

        _logger.LogDebug("Invalidated cache for configuration {Key}", key);
    }

    private (string serializedValue, string valueType) SerializeValue<T>(T value)
    {
        if (value == null)
        {
            return (string.Empty, "null");
        }

        var type = typeof(T);

        if (type == typeof(string))
        {
            return (value.ToString() ?? string.Empty, "string");
        }

        if (type == typeof(int) || type == typeof(long) || type == typeof(double) || type == typeof(decimal))
        {
            return (value.ToString() ?? "0", "number");
        }

        if (type == typeof(bool))
        {
            return (value.ToString()?.ToLowerInvariant() ?? "false", "boolean");
        }

        return (JsonSerializer.Serialize(value), "json");
    }

    private T? DeserializeValue<T>(string value, string valueType)
    {
        if (string.IsNullOrEmpty(value))
        {
            return default;
        }

        var targetType = typeof(T);

        if (targetType == typeof(string))
        {
            return (T)(object)value;
        }

        if (valueType == "boolean" && targetType == typeof(bool))
        {
            return (T)(object)bool.Parse(value);
        }

        if (valueType == "number")
        {
            if (targetType == typeof(int))
            {
                return (T)(object)int.Parse(value);
            }
            if (targetType == typeof(long))
            {
                return (T)(object)long.Parse(value);
            }
            if (targetType == typeof(double))
            {
                return (T)(object)double.Parse(value);
            }
            if (targetType == typeof(decimal))
            {
                return (T)(object)decimal.Parse(value);
            }
        }

        if (valueType == "json")
        {
            return JsonSerializer.Deserialize<T>(value);
        }

        return default;
    }

    private Dictionary<string, (string value, string category, string valueType)> GetDefaultConfigurations()
    {
        return new Dictionary<string, (string value, string category, string valueType)>
        {
            // General settings
            { "General.DefaultProjectSaveLocation", ("", "General", "string") },
            { "General.AutosaveIntervalSeconds", ("300", "General", "number") },
            { "General.AutosaveEnabled", ("true", "General", "boolean") },
            { "General.Language", ("en-US", "General", "string") },
            { "General.Theme", ("Auto", "General", "string") },
            { "General.CheckForUpdatesOnStartup", ("true", "General", "boolean") },

            // File locations
            { "FileLocations.OutputDirectory", ("", "FileLocations", "string") },
            { "FileLocations.TempDirectory", ("", "FileLocations", "string") },
            { "FileLocations.ProjectsDirectory", ("", "FileLocations", "string") },

            // Video defaults
            { "VideoDefaults.DefaultResolution", ("1920x1080", "VideoDefaults", "string") },
            { "VideoDefaults.DefaultFrameRate", ("30", "VideoDefaults", "number") },
            { "VideoDefaults.DefaultCodec", ("libx264", "VideoDefaults", "string") },
            { "VideoDefaults.DefaultBitrate", ("5M", "VideoDefaults", "string") },

            // Advanced settings
            { "Advanced.OfflineMode", ("false", "Advanced", "boolean") },
            { "Advanced.StableDiffusionUrl", ("http://127.0.0.1:7860", "Advanced", "string") },
            { "Advanced.OllamaUrl", ("http://127.0.0.1:11434", "Advanced", "string") },
            { "Advanced.EnableTelemetry", ("false", "Advanced", "boolean") },

            // System settings
            { "System.DatabaseVersion", ("1", "System", "number") },
            { "System.LastBackupDate", (DateTime.UtcNow.ToString("O"), "System", "string") },
        };
    }
}
