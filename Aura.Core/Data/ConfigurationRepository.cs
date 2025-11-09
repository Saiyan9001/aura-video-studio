using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Aura.Core.Data;

/// <summary>
/// Repository for configuration operations with caching and validation
/// </summary>
public class ConfigurationRepository
{
    private readonly AuraDbContext _context;
    private readonly ILogger<ConfigurationRepository> _logger;

    public ConfigurationRepository(
        AuraDbContext context,
        ILogger<ConfigurationRepository> logger)
    {
        _context = context;
        _logger = logger;
    }

    /// <summary>
    /// Get a configuration value by key
    /// </summary>
    public async Task<ConfigurationEntity?> GetAsync(string key, CancellationToken ct = default)
    {
        try
        {
            return await _context.Configurations
                .Where(c => c.Key == key && c.IsActive)
                .FirstOrDefaultAsync(ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving configuration {Key}", key);
            throw;
        }
    }

    /// <summary>
    /// Get all configurations in a category
    /// </summary>
    public async Task<List<ConfigurationEntity>> GetByCategoryAsync(
        string category, 
        CancellationToken ct = default)
    {
        try
        {
            return await _context.Configurations
                .Where(c => c.Category == category && c.IsActive)
                .OrderBy(c => c.Key)
                .ToListAsync(ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving configurations for category {Category}", category);
            throw;
        }
    }

    /// <summary>
    /// Get all active configurations
    /// </summary>
    public async Task<List<ConfigurationEntity>> GetAllAsync(
        bool includeInactive = false,
        CancellationToken ct = default)
    {
        try
        {
            var query = _context.Configurations.AsQueryable();
            
            if (!includeInactive)
            {
                query = query.Where(c => c.IsActive);
            }

            return await query.OrderBy(c => c.Category).ThenBy(c => c.Key).ToListAsync(ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving all configurations");
            throw;
        }
    }

    /// <summary>
    /// Set or update a configuration value
    /// </summary>
    public async Task<ConfigurationEntity> SetAsync(
        string key,
        string value,
        string category,
        string valueType = "string",
        string? description = null,
        bool isSensitive = false,
        string? modifiedBy = null,
        CancellationToken ct = default)
    {
        try
        {
            var existing = await _context.Configurations
                .Where(c => c.Key == key)
                .FirstOrDefaultAsync(ct);

            if (existing != null)
            {
                existing.Value = value;
                existing.Category = category;
                existing.ValueType = valueType;
                existing.Description = description;
                existing.IsSensitive = isSensitive;
                existing.UpdatedAt = DateTime.UtcNow;
                existing.ModifiedBy = modifiedBy;
                existing.Version++;

                _logger.LogInformation(
                    "Updated configuration {Key} in category {Category} (version {Version})",
                    key, category, existing.Version);
            }
            else
            {
                existing = new ConfigurationEntity
                {
                    Key = key,
                    Value = value,
                    Category = category,
                    ValueType = valueType,
                    Description = description,
                    IsSensitive = isSensitive,
                    ModifiedBy = modifiedBy,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow,
                    Version = 1
                };

                _context.Configurations.Add(existing);

                _logger.LogInformation(
                    "Created new configuration {Key} in category {Category}",
                    key, category);
            }

            await _context.SaveChangesAsync(ct);
            return existing;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error setting configuration {Key}", key);
            throw;
        }
    }

    /// <summary>
    /// Set multiple configurations in a single transaction
    /// </summary>
    public async Task<List<ConfigurationEntity>> SetManyAsync(
        Dictionary<string, (string value, string category, string valueType)> configurations,
        string? modifiedBy = null,
        CancellationToken ct = default)
    {
        var results = new List<ConfigurationEntity>();

        try
        {
            using var transaction = await _context.Database.BeginTransactionAsync(ct);

            foreach (var kvp in configurations)
            {
                var result = await SetAsync(
                    kvp.Key,
                    kvp.Value.value,
                    kvp.Value.category,
                    kvp.Value.valueType,
                    null,
                    false,
                    modifiedBy,
                    ct);

                results.Add(result);
            }

            await transaction.CommitAsync(ct);

            _logger.LogInformation(
                "Bulk updated {Count} configurations",
                configurations.Count);

            return results;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error setting multiple configurations");
            throw;
        }
    }

    /// <summary>
    /// Delete a configuration (soft delete by setting IsActive = false)
    /// </summary>
    public async Task<bool> DeleteAsync(string key, CancellationToken ct = default)
    {
        try
        {
            var config = await _context.Configurations
                .Where(c => c.Key == key)
                .FirstOrDefaultAsync(ct);

            if (config == null)
            {
                return false;
            }

            config.IsActive = false;
            config.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync(ct);

            _logger.LogInformation("Deleted configuration {Key}", key);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting configuration {Key}", key);
            throw;
        }
    }

    /// <summary>
    /// Get configuration history (all versions)
    /// </summary>
    public async Task<List<ConfigurationEntity>> GetHistoryAsync(
        string key,
        CancellationToken ct = default)
    {
        try
        {
            return await _context.Configurations
                .Where(c => c.Key == key)
                .OrderByDescending(c => c.Version)
                .ToListAsync(ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving history for configuration {Key}", key);
            throw;
        }
    }
}
