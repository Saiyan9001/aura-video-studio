using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Aura.Core.Data;
using Aura.Core.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Aura.Api.HostedServices;

/// <summary>
/// Enhanced cleanup service with job archival, log compression, and database maintenance
/// </summary>
public class EnhancedCleanupHostedService : BackgroundService
{
    private readonly ILogger<EnhancedCleanupHostedService> _logger;
    private readonly CleanupService _cleanupService;
    private readonly IServiceProvider _serviceProvider;
    
    private readonly TimeSpan _hourlyInterval = TimeSpan.FromHours(1);
    private readonly TimeSpan _dailyInterval = TimeSpan.FromDays(1);
    private readonly TimeSpan _weeklyInterval = TimeSpan.FromDays(7);
    
    private DateTime _lastHourlyRun = DateTime.MinValue;
    private DateTime _lastDailyRun = DateTime.MinValue;
    private DateTime _lastWeeklyRun = DateTime.MinValue;

    public EnhancedCleanupHostedService(
        ILogger<EnhancedCleanupHostedService> logger,
        CleanupService cleanupService,
        IServiceProvider serviceProvider)
    {
        _logger = logger;
        _cleanupService = cleanupService;
        _serviceProvider = serviceProvider;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Enhanced cleanup service started");

        await Task.Delay(TimeSpan.FromMinutes(5), stoppingToken).ConfigureAwait(false);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var now = DateTime.UtcNow;

                if (now - _lastHourlyRun >= _hourlyInterval)
                {
                    await RunHourlyCleanupAsync(stoppingToken).ConfigureAwait(false);
                    _lastHourlyRun = now;
                }

                if (now - _lastDailyRun >= _dailyInterval)
                {
                    await RunDailyCleanupAsync(stoppingToken).ConfigureAwait(false);
                    _lastDailyRun = now;
                }

                if (now - _lastWeeklyRun >= _weeklyInterval)
                {
                    await RunWeeklyCleanupAsync(stoppingToken).ConfigureAwait(false);
                    _lastWeeklyRun = now;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during cleanup cycle");
            }

            await Task.Delay(TimeSpan.FromMinutes(15), stoppingToken).ConfigureAwait(false);
        }

        _logger.LogInformation("Enhanced cleanup service stopped");
    }

    private async Task RunHourlyCleanupAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Running hourly cleanup");

        var cleanedCount = _cleanupService.SweepAllOrphaned();
        if (cleanedCount > 0)
        {
            _logger.LogInformation("Hourly cleanup: {Count} orphaned directories removed", cleanedCount);
        }

        await CleanupTemporaryFilesAsync(24, cancellationToken).ConfigureAwait(false);
        await CleanupUploadedFilesAsync(48, cancellationToken).ConfigureAwait(false);

        _logger.LogInformation("Hourly cleanup completed");
    }

    private async Task RunDailyCleanupAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Running daily cleanup");

        await ArchiveCompletedJobsAsync(7, cancellationToken).ConfigureAwait(false);
        await DeleteFailedJobsAsync(3, cancellationToken).ConfigureAwait(false);
        await CompressOldLogsAsync(7, cancellationToken).ConfigureAwait(false);
        await CleanExpiredCacheEntriesAsync(cancellationToken).ConfigureAwait(false);

        _logger.LogInformation("Daily cleanup completed");
    }

    private async Task RunWeeklyCleanupAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Running weekly cleanup (deep clean)");

        await VacuumDatabaseAsync(cancellationToken).ConfigureAwait(false);
        await CleanExpiredSessionsAsync(cancellationToken).ConfigureAwait(false);
        await ArchiveOldAnalyticsAsync(90, cancellationToken).ConfigureAwait(false);
        await CleanupOldArchivedJobsAsync(30, cancellationToken).ConfigureAwait(false);

        _logger.LogInformation("Weekly cleanup completed");
    }

    private async Task CleanupTemporaryFilesAsync(int maxAgeHours, CancellationToken cancellationToken)
    {
        try
        {
            var tempPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "Aura", "Temp");

            if (!Directory.Exists(tempPath))
                return;

            var cutoff = DateTime.UtcNow.AddHours(-maxAgeHours);
            var filesDeleted = 0;

            await Task.Run(() =>
            {
                var files = Directory.GetFiles(tempPath, "*.*", SearchOption.AllDirectories);
                foreach (var file in files)
                {
                    if (cancellationToken.IsCancellationRequested)
                        break;

                    try
                    {
                        var fileInfo = new FileInfo(file);
                        if (fileInfo.LastWriteTimeUtc < cutoff)
                        {
                            fileInfo.Delete();
                            filesDeleted++;
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to delete temp file: {File}", file);
                    }
                }
            }, cancellationToken).ConfigureAwait(false);

            if (filesDeleted > 0)
            {
                _logger.LogInformation("Cleaned {Count} temporary files older than {Hours} hours", 
                    filesDeleted, maxAgeHours);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error cleaning temporary files");
        }
    }

    private async Task CleanupUploadedFilesAsync(int maxAgeHours, CancellationToken cancellationToken)
    {
        try
        {
            var uploadsPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "Aura", "Uploads");

            if (!Directory.Exists(uploadsPath))
                return;

            var cutoff = DateTime.UtcNow.AddHours(-maxAgeHours);
            var filesDeleted = 0;

            await Task.Run(() =>
            {
                var files = Directory.GetFiles(uploadsPath, "*.*", SearchOption.AllDirectories);
                foreach (var file in files)
                {
                    if (cancellationToken.IsCancellationRequested)
                        break;

                    try
                    {
                        var fileInfo = new FileInfo(file);
                        if (fileInfo.LastWriteTimeUtc < cutoff)
                        {
                            fileInfo.Delete();
                            filesDeleted++;
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to delete uploaded file: {File}", file);
                    }
                }
            }, cancellationToken).ConfigureAwait(false);

            if (filesDeleted > 0)
            {
                _logger.LogInformation("Cleaned {Count} orphaned upload files older than {Hours} hours",
                    filesDeleted, maxAgeHours);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error cleaning uploaded files");
        }
    }

    private async Task ArchiveCompletedJobsAsync(int daysOld, CancellationToken cancellationToken)
    {
        try
        {
            using var scope = _serviceProvider.CreateScope();
            var dbContext = scope.ServiceProvider.GetService<AuraDbContext>();
            
            if (dbContext == null)
            {
                _logger.LogWarning("Database context not available for job archival");
                return;
            }

            var cutoff = DateTime.UtcNow.AddDays(-daysOld);
            
            var projectStatesToArchive = await dbContext.ProjectStates
                .Where(p => p.Status == "Completed" && p.UpdatedAt < cutoff)
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);

            if (projectStatesToArchive.Count > 0)
            {
                var archivePath = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "Aura", "Archive", "Projects");

                Directory.CreateDirectory(archivePath);

                foreach (var project in projectStatesToArchive)
                {
                    var archiveFile = Path.Combine(archivePath, $"{project.Id}_{DateTime.UtcNow:yyyyMMdd}.json");
                    await File.WriteAllTextAsync(archiveFile, 
                        System.Text.Json.JsonSerializer.Serialize(project), 
                        cancellationToken).ConfigureAwait(false);
                }

                dbContext.ProjectStates.RemoveRange(projectStatesToArchive);
                await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

                _logger.LogInformation("Archived {Count} completed projects older than {Days} days",
                    projectStatesToArchive.Count, daysOld);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error archiving completed projects");
        }
    }

    private async Task DeleteFailedJobsAsync(int daysOld, CancellationToken cancellationToken)
    {
        try
        {
            using var scope = _serviceProvider.CreateScope();
            var dbContext = scope.ServiceProvider.GetService<AuraDbContext>();

            if (dbContext == null)
                return;

            var cutoff = DateTime.UtcNow.AddDays(-daysOld);

            var failedProjects = await dbContext.ProjectStates
                .Where(p => p.Status == "Failed" && p.UpdatedAt < cutoff)
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);

            if (failedProjects.Count > 0)
            {
                dbContext.ProjectStates.RemoveRange(failedProjects);
                await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

                _logger.LogInformation("Deleted {Count} failed projects older than {Days} days",
                    failedProjects.Count, daysOld);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting failed projects");
        }
    }

    private async Task CompressOldLogsAsync(int daysOld, CancellationToken cancellationToken)
    {
        try
        {
            var logsPath = Path.Combine(AppContext.BaseDirectory, "logs");
            if (!Directory.Exists(logsPath))
                return;

            var cutoff = DateTime.UtcNow.AddDays(-daysOld);
            var filesCompressed = 0;

            await Task.Run(() =>
            {
                var logFiles = Directory.GetFiles(logsPath, "*.log", SearchOption.TopDirectoryOnly);
                foreach (var logFile in logFiles)
                {
                    if (cancellationToken.IsCancellationRequested)
                        break;

                    try
                    {
                        var fileInfo = new FileInfo(logFile);
                        if (fileInfo.LastWriteTimeUtc < cutoff && !logFile.EndsWith(".gz", StringComparison.OrdinalIgnoreCase))
                        {
                            var gzipPath = logFile + ".gz";
                            if (!File.Exists(gzipPath))
                            {
                                using var sourceStream = File.OpenRead(logFile);
                                using var destinationStream = File.Create(gzipPath);
                                using var gzipStream = new GZipStream(destinationStream, CompressionMode.Compress);
                                sourceStream.CopyTo(gzipStream);
                            }

                            File.Delete(logFile);
                            filesCompressed++;
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to compress log file: {File}", logFile);
                    }
                }
            }, cancellationToken).ConfigureAwait(false);

            if (filesCompressed > 0)
            {
                _logger.LogInformation("Compressed {Count} log files older than {Days} days",
                    filesCompressed, daysOld);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error compressing old logs");
        }
    }

    private async Task CleanExpiredCacheEntriesAsync(CancellationToken cancellationToken)
    {
        try
        {
            var cachePath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "Aura", "Cache");

            if (!Directory.Exists(cachePath))
                return;

            await Task.Run(() =>
            {
                var cacheFiles = Directory.GetFiles(cachePath, "*.*", SearchOption.AllDirectories);
                var now = DateTime.UtcNow;
                var deletedCount = 0;

                foreach (var file in cacheFiles)
                {
                    if (cancellationToken.IsCancellationRequested)
                        break;

                    try
                    {
                        var fileInfo = new FileInfo(file);
                        var age = now - fileInfo.LastAccessTimeUtc;

                        if (age.TotalHours > 24)
                        {
                            fileInfo.Delete();
                            deletedCount++;
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to delete cache file: {File}", file);
                    }
                }

                if (deletedCount > 0)
                {
                    _logger.LogInformation("Cleaned {Count} expired cache entries", deletedCount);
                }
            }, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error cleaning expired cache entries");
        }
    }

    private async Task VacuumDatabaseAsync(CancellationToken cancellationToken)
    {
        try
        {
            using var scope = _serviceProvider.CreateScope();
            var dbContext = scope.ServiceProvider.GetService<AuraDbContext>();

            if (dbContext == null)
                return;

            if (dbContext.Database.IsSqlite())
            {
                _logger.LogInformation("Vacuuming SQLite database");
                await dbContext.Database.ExecuteSqlRawAsync("VACUUM;", cancellationToken).ConfigureAwait(false);
                _logger.LogInformation("Database vacuum completed");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error vacuuming database");
        }
    }

    private async Task CleanExpiredSessionsAsync(CancellationToken cancellationToken)
    {
        try
        {
            using var scope = _serviceProvider.CreateScope();
            var dbContext = scope.ServiceProvider.GetService<AuraDbContext>();

            if (dbContext == null)
                return;

            var cutoff = DateTime.UtcNow.AddDays(-30);

            var oldExportHistory = await dbContext.ExportHistory
                .Where(e => e.CreatedAt < cutoff)
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);

            if (oldExportHistory.Count > 0)
            {
                dbContext.ExportHistory.RemoveRange(oldExportHistory);
                await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

                _logger.LogInformation("Cleaned {Count} old export history records", oldExportHistory.Count);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error cleaning old export history");
        }
    }

    private async Task ArchiveOldAnalyticsAsync(int daysOld, CancellationToken cancellationToken)
    {
        try
        {
            using var scope = _serviceProvider.CreateScope();
            var dbContext = scope.ServiceProvider.GetService<AuraDbContext>();

            if (dbContext == null)
                return;

            var cutoff = DateTime.UtcNow.AddDays(-daysOld);

            var oldActionLogs = await dbContext.ActionLogs
                .Where(a => a.Timestamp < cutoff)
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);

            if (oldActionLogs.Count > 0)
            {
                var archivePath = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "Aura", "Archive", "ActionLogs");

                Directory.CreateDirectory(archivePath);

                var archiveFile = Path.Combine(archivePath, $"actionlogs_{DateTime.UtcNow:yyyyMMdd}.json");
                await File.WriteAllTextAsync(archiveFile,
                    System.Text.Json.JsonSerializer.Serialize(oldActionLogs),
                    cancellationToken).ConfigureAwait(false);

                dbContext.ActionLogs.RemoveRange(oldActionLogs);
                await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

                _logger.LogInformation("Archived {Count} action logs older than {Days} days",
                    oldActionLogs.Count, daysOld);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error archiving old action logs");
        }
    }

    private async Task CleanupOldArchivedJobsAsync(int daysOld, CancellationToken cancellationToken)
    {
        try
        {
            var archivePath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "Aura", "Archive", "Projects");

            if (!Directory.Exists(archivePath))
                return;

            var cutoff = DateTime.UtcNow.AddDays(-daysOld);
            var deletedCount = 0;

            await Task.Run(() =>
            {
                var archiveFiles = Directory.GetFiles(archivePath, "*.json");
                foreach (var file in archiveFiles)
                {
                    if (cancellationToken.IsCancellationRequested)
                        break;

                    try
                    {
                        var fileInfo = new FileInfo(file);
                        if (fileInfo.CreationTimeUtc < cutoff)
                        {
                            fileInfo.Delete();
                            deletedCount++;
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to delete archived project: {File}", file);
                    }
                }
            }, cancellationToken).ConfigureAwait(false);

            if (deletedCount > 0)
            {
                _logger.LogInformation("Deleted {Count} archived projects older than {Days} days",
                    deletedCount, daysOld);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error cleaning old archived projects");
        }
    }
}
