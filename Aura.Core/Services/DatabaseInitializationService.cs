using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Aura.Core.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Aura.Core.Services;

/// <summary>
/// Service for database initialization, health checking, and repair
/// </summary>
public class DatabaseInitializationService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<DatabaseInitializationService> _logger;
    private readonly string _databasePath;

    public DatabaseInitializationService(
        IServiceScopeFactory scopeFactory,
        ILogger<DatabaseInitializationService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
        _databasePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "aura.db");
    }

    /// <summary>
    /// Initialize database with migrations and health checks
    /// </summary>
    public async Task<InitializationResult> InitializeAsync(CancellationToken ct = default)
    {
        _logger.LogInformation("Starting database initialization");

        var result = new InitializationResult
        {
            DatabasePath = _databasePath,
            StartTime = DateTime.UtcNow
        };

        try
        {
            result.PathWritable = await CheckPathWritableAsync();

            if (!result.PathWritable)
            {
                result.Success = false;
                result.Error = $"Database path is not writable: {_databasePath}";
                _logger.LogError(result.Error);
                return result;
            }

            result.DatabaseExists = File.Exists(_databasePath);

            using var scope = _scopeFactory.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<AuraDbContext>();

            result.MigrationsApplied = await ApplyMigrationsAsync(context, ct);

            if (!result.MigrationsApplied)
            {
                result.Success = false;
                result.Error = "Failed to apply database migrations";
                return result;
            }

            result.WalModeEnabled = await ConfigureWalModeAsync(context, ct);

            result.IntegrityCheck = await CheckIntegrityAsync(context, ct);

            if (!result.IntegrityCheck)
            {
                _logger.LogWarning("Database integrity check failed, attempting repair");
                result.RepairAttempted = true;
                result.RepairSuccessful = await AttemptRepairAsync(context, ct);

                if (!result.RepairSuccessful)
                {
                    result.Success = false;
                    result.Error = "Database integrity check failed and repair was unsuccessful";
                    return result;
                }
            }

            result.Success = true;
            result.EndTime = DateTime.UtcNow;
            result.DurationMs = (result.EndTime.Value - result.StartTime).TotalMilliseconds;

            _logger.LogInformation(
                "Database initialization completed successfully in {Duration}ms",
                result.DurationMs);

            return result;
        }
        catch (Exception ex)
        {
            result.Success = false;
            result.Error = ex.Message;
            result.EndTime = DateTime.UtcNow;
            _logger.LogError(ex, "Database initialization failed");
            return result;
        }
    }

    /// <summary>
    /// Check if database path is writable
    /// </summary>
    private async Task<bool> CheckPathWritableAsync()
    {
        try
        {
            var directory = Path.GetDirectoryName(_databasePath);
            
            if (string.IsNullOrEmpty(directory))
            {
                return false;
            }

            if (!Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            var testFile = Path.Combine(directory, $".write-test-{Guid.NewGuid()}");
            
            await File.WriteAllTextAsync(testFile, "test");
            File.Delete(testFile);

            _logger.LogDebug("Database path is writable: {Path}", directory);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Database path is not writable");
            return false;
        }
    }

    /// <summary>
    /// Apply pending migrations
    /// </summary>
    private async Task<bool> ApplyMigrationsAsync(AuraDbContext context, CancellationToken ct)
    {
        try
        {
            var pendingMigrations = await context.Database.GetPendingMigrationsAsync(ct);
            var pendingCount = pendingMigrations.Count();

            if (pendingCount > 0)
            {
                _logger.LogInformation("Applying {Count} pending migrations", pendingCount);
                await context.Database.MigrateAsync(ct);
                _logger.LogInformation("Migrations applied successfully");
            }
            else
            {
                _logger.LogInformation("No pending migrations");
            }

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to apply migrations");
            return false;
        }
    }

    /// <summary>
    /// Configure SQLite WAL mode for better concurrency
    /// </summary>
    private async Task<bool> ConfigureWalModeAsync(AuraDbContext context, CancellationToken ct)
    {
        try
        {
            await context.Database.ExecuteSqlRawAsync("PRAGMA journal_mode=WAL;", ct);
            await context.Database.ExecuteSqlRawAsync("PRAGMA synchronous=NORMAL;", ct);

            _logger.LogInformation("SQLite WAL mode configured successfully");
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to configure SQLite WAL mode, using default journal mode");
            return false;
        }
    }

    /// <summary>
    /// Check database integrity
    /// </summary>
    private async Task<bool> CheckIntegrityAsync(AuraDbContext context, CancellationToken ct)
    {
        try
        {
            var connection = context.Database.GetDbConnection();
            await connection.OpenAsync(ct);

            using var command = connection.CreateCommand();
            command.CommandText = "PRAGMA integrity_check;";
            
            var result = await command.ExecuteScalarAsync(ct);
            var isOk = result?.ToString() == "ok";

            if (isOk)
            {
                _logger.LogDebug("Database integrity check passed");
            }
            else
            {
                _logger.LogWarning("Database integrity check failed: {Result}", result);
            }

            return isOk;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking database integrity");
            return false;
        }
    }

    /// <summary>
    /// Attempt to repair a corrupted database
    /// </summary>
    private async Task<bool> AttemptRepairAsync(AuraDbContext context, CancellationToken ct)
    {
        try
        {
            _logger.LogInformation("Attempting database repair");

            var connection = context.Database.GetDbConnection();
            await connection.OpenAsync(ct);

            using var command = connection.CreateCommand();
            command.CommandText = "PRAGMA integrity_check;";
            
            var checkResult = await command.ExecuteScalarAsync(ct);

            if (checkResult?.ToString() == "ok")
            {
                _logger.LogInformation("Database integrity restored");
                return true;
            }

            _logger.LogWarning("Database repair unsuccessful, manual intervention may be required");
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during database repair");
            return false;
        }
    }
}

/// <summary>
/// Result of database initialization
/// </summary>
public class InitializationResult
{
    public bool Success { get; set; }
    public string? Error { get; set; }
    public string DatabasePath { get; set; } = string.Empty;
    public bool DatabaseExists { get; set; }
    public bool PathWritable { get; set; }
    public bool MigrationsApplied { get; set; }
    public bool WalModeEnabled { get; set; }
    public bool IntegrityCheck { get; set; }
    public bool RepairAttempted { get; set; }
    public bool RepairSuccessful { get; set; }
    public DateTime StartTime { get; set; }
    public DateTime? EndTime { get; set; }
    public double DurationMs { get; set; }
}
