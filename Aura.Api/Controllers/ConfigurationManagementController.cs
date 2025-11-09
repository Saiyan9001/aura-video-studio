using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Aura.Core.Data;
using Aura.Core.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace Aura.Api.Controllers;

/// <summary>
/// Enhanced configuration management controller with persistence and debugging
/// </summary>
[ApiController]
[Route("api/configuration")]
public class ConfigurationManagementController : ControllerBase
{
    private readonly ILogger<ConfigurationManagementController> _logger;
    private readonly Aura.Core.Services.ConfigurationManager _configManager;
    private readonly DatabaseInitializationService _dbInitService;

    public ConfigurationManagementController(
        ILogger<ConfigurationManagementController> logger,
        Aura.Core.Services.ConfigurationManager configManager,
        DatabaseInitializationService dbInitService)
    {
        _logger = logger;
        _configManager = configManager;
        _dbInitService = dbInitService;
    }

    /// <summary>
    /// Get a configuration value
    /// </summary>
    [HttpGet("{key}")]
    public async Task<IActionResult> GetConfiguration(
        string key,
        CancellationToken ct)
    {
        try
        {
            var value = await _configManager.GetStringAsync(key, null, ct);

            if (value == null)
            {
                return NotFound(new
                {
                    error = "Configuration not found",
                    key
                });
            }

            return Ok(new
            {
                key,
                value,
                source = "database"
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving configuration {Key}", key);
            return StatusCode(500, new { error = "Failed to retrieve configuration" });
        }
    }

    /// <summary>
    /// Get all configurations in a category
    /// </summary>
    [HttpGet("category/{category}")]
    public async Task<IActionResult> GetCategory(
        string category,
        CancellationToken ct)
    {
        try
        {
            var configs = await _configManager.GetCategoryAsync(category, ct);

            return Ok(new
            {
                category,
                count = configs.Count,
                configurations = configs,
                source = "database"
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving category {Category}", category);
            return StatusCode(500, new { error = "Failed to retrieve category" });
        }
    }

    /// <summary>
    /// Set a configuration value with immediate persistence
    /// </summary>
    [HttpPost("{key}")]
    public async Task<IActionResult> SetConfiguration(
        string key,
        [FromBody] SetConfigurationRequest request,
        CancellationToken ct)
    {
        try
        {
            if (string.IsNullOrEmpty(request.Value))
            {
                return BadRequest(new { error = "Value is required" });
            }

            if (string.IsNullOrEmpty(request.Category))
            {
                return BadRequest(new { error = "Category is required" });
            }

            await _configManager.SetAsync(
                key,
                request.Value,
                request.Category,
                request.Description,
                request.IsSensitive,
                ct);

            _logger.LogInformation(
                "Configuration {Key} set in category {Category} by {User}",
                key, request.Category, User?.Identity?.Name ?? "Anonymous");

            return Ok(new
            {
                success = true,
                message = "Configuration saved successfully",
                key,
                category = request.Category,
                persisted = true
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error setting configuration {Key}", key);
            return StatusCode(500, new { error = "Failed to set configuration" });
        }
    }

    /// <summary>
    /// Set multiple configurations at once
    /// </summary>
    [HttpPost("bulk")]
    public async Task<IActionResult> SetBulkConfigurations(
        [FromBody] BulkSetConfigurationRequest request,
        CancellationToken ct)
    {
        try
        {
            if (request.Configurations == null || request.Configurations.Count == 0)
            {
                return BadRequest(new { error = "Configurations array is required" });
            }

            var configs = request.Configurations.ToDictionary(
                c => c.Key,
                c => ((object)c.Value, c.Category, c.Description));

            await _configManager.SetManyAsync(configs, ct);

            _logger.LogInformation(
                "Bulk updated {Count} configurations",
                configs.Count);

            return Ok(new
            {
                success = true,
                message = $"Successfully updated {configs.Count} configurations",
                count = configs.Count,
                persisted = true
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error setting bulk configurations");
            return StatusCode(500, new { error = "Failed to set configurations" });
        }
    }

    /// <summary>
    /// Delete a configuration
    /// </summary>
    [HttpDelete("{key}")]
    public async Task<IActionResult> DeleteConfiguration(
        string key,
        CancellationToken ct)
    {
        try
        {
            var result = await _configManager.DeleteAsync(key, ct);

            if (!result)
            {
                return NotFound(new { error = "Configuration not found", key });
            }

            _logger.LogInformation("Configuration {Key} deleted", key);

            return Ok(new
            {
                success = true,
                message = "Configuration deleted successfully",
                key
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting configuration {Key}", key);
            return StatusCode(500, new { error = "Failed to delete configuration" });
        }
    }

    /// <summary>
    /// Dump all configurations for debugging
    /// </summary>
    [HttpGet("debug/dump")]
    public async Task<IActionResult> DumpConfigurations(
        [FromQuery] bool includeInactive = false,
        CancellationToken ct = default)
    {
        try
        {
            var configs = await _configManager.GetAllConfigurationsAsync(includeInactive, ct);

            var dump = configs.Select(c => new
            {
                c.Key,
                c.Value,
                c.Category,
                c.ValueType,
                c.Description,
                c.IsSensitive,
                c.Version,
                c.CreatedAt,
                c.UpdatedAt,
                c.ModifiedBy,
                c.IsActive
            }).ToList();

            return Ok(new
            {
                totalCount = configs.Count,
                activeCount = configs.Count(c => c.IsActive),
                inactiveCount = configs.Count(c => !c.IsActive),
                configurations = dump,
                dumpedAt = DateTime.UtcNow
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error dumping configurations");
            return StatusCode(500, new { error = "Failed to dump configurations" });
        }
    }

    /// <summary>
    /// Reset all configurations to defaults
    /// </summary>
    [HttpPost("reset")]
    public async Task<IActionResult> ResetConfigurations(CancellationToken ct)
    {
        try
        {
            _logger.LogWarning("Resetting all configurations to defaults");

            await _configManager.InitializeAsync(ct);

            return Ok(new
            {
                success = true,
                message = "Configurations reset to defaults successfully"
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error resetting configurations");
            return StatusCode(500, new { error = "Failed to reset configurations" });
        }
    }

    /// <summary>
    /// Check database health
    /// </summary>
    [HttpGet("health/database")]
    public async Task<IActionResult> CheckDatabaseHealth(CancellationToken ct)
    {
        try
        {
            var initResult = await _dbInitService.InitializeAsync(ct);

            return Ok(new
            {
                healthy = initResult.Success,
                databaseExists = initResult.DatabaseExists,
                pathWritable = initResult.PathWritable,
                migrationsApplied = initResult.MigrationsApplied,
                walModeEnabled = initResult.WalModeEnabled,
                integrityCheck = initResult.IntegrityCheck,
                error = initResult.Error
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking database health");
            return StatusCode(500, new { error = "Failed to check database health" });
        }
    }

    /// <summary>
    /// Clear configuration cache
    /// </summary>
    [HttpPost("cache/clear")]
    public IActionResult ClearCache()
    {
        try
        {
            _configManager.ClearCache();

            _logger.LogInformation("Configuration cache cleared");

            return Ok(new
            {
                success = true,
                message = "Cache cleared successfully"
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error clearing cache");
            return StatusCode(500, new { error = "Failed to clear cache" });
        }
    }
}

public class SetConfigurationRequest
{
    public string Value { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string? Description { get; set; }
    public bool IsSensitive { get; set; }
}

public class BulkSetConfigurationRequest
{
    public List<BulkConfigItem> Configurations { get; set; } = new();
}

public class BulkConfigItem
{
    public string Key { get; set; } = string.Empty;
    public string Value { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string? Description { get; set; }
}
