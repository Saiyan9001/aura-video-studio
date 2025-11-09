using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Aura.Core.Errors;
using Microsoft.Extensions.Logging;

namespace Aura.Core.Services.Resources;

/// <summary>
/// Service for checking disk space availability before performing operations
/// </summary>
public class DiskSpaceChecker
{
    private readonly ILogger<DiskSpaceChecker> _logger;
    private const long MinimumFreeSpaceBytes = 100 * 1024 * 1024; // 100 MB minimum
    private const long RecommendedFreeSpaceBytes = 1024 * 1024 * 1024; // 1 GB recommended

    public DiskSpaceChecker(ILogger<DiskSpaceChecker> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Checks if there is sufficient disk space for an operation
    /// </summary>
    /// <param name="path">Path to check (file or directory)</param>
    /// <param name="requiredBytes">Bytes required for the operation</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>True if sufficient space is available</returns>
    public async Task<bool> HasSufficientSpaceAsync(string path, long requiredBytes, CancellationToken cancellationToken = default)
    {
        try
        {
            var driveInfo = GetDriveInfo(path);
            if (driveInfo == null)
            {
                _logger.LogWarning("Could not determine drive for path: {Path}", path);
                return true; // Assume it's OK if we can't check
            }

            var availableSpace = driveInfo.AvailableFreeSpace;
            var hasSpace = availableSpace >= requiredBytes;

            _logger.LogDebug(
                "Disk space check for {Path}: {AvailableMB:F2} MB available, {RequiredMB:F2} MB required, Result: {Result}",
                path,
                availableSpace / (1024.0 * 1024.0),
                requiredBytes / (1024.0 * 1024.0),
                hasSpace ? "OK" : "INSUFFICIENT");

            return hasSpace;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking disk space for {Path}", path);
            return true; // Assume it's OK if we can't check
        }
        finally
        {
            await Task.Yield(); // Make method truly async
        }
    }

    /// <summary>
    /// Ensures sufficient disk space is available, throwing an exception if not
    /// </summary>
    /// <param name="path">Path to check</param>
    /// <param name="requiredBytes">Bytes required for the operation</param>
    /// <param name="correlationId">Correlation ID for error tracking</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <exception cref="ResourceException">Thrown if insufficient disk space</exception>
    public async Task EnsureSufficientSpaceAsync(
        string path,
        long requiredBytes,
        string? correlationId = null,
        CancellationToken cancellationToken = default)
    {
        var hasSpace = await HasSufficientSpaceAsync(path, requiredBytes, cancellationToken).ConfigureAwait(false);
        
        if (!hasSpace)
        {
            var driveInfo = GetDriveInfo(path);
            var availableBytes = driveInfo?.AvailableFreeSpace ?? 0;
            
            _logger.LogError(
                "Insufficient disk space: {AvailableMB:F2} MB available, {RequiredMB:F2} MB required",
                availableBytes / (1024.0 * 1024.0),
                requiredBytes / (1024.0 * 1024.0));

            throw ResourceException.InsufficientDiskSpace(path, requiredBytes, availableBytes, correlationId);
        }
    }

    /// <summary>
    /// Gets the available space on the drive containing the specified path
    /// </summary>
    /// <param name="path">Path to check</param>
    /// <returns>Available space in bytes, or null if unable to determine</returns>
    public long? GetAvailableSpace(string path)
    {
        try
        {
            var driveInfo = GetDriveInfo(path);
            return driveInfo?.AvailableFreeSpace;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting available space for {Path}", path);
            return null;
        }
    }

    /// <summary>
    /// Gets disk space information for the specified path
    /// </summary>
    public DiskSpaceInfo GetDiskSpaceInfo(string path)
    {
        try
        {
            var driveInfo = GetDriveInfo(path);
            if (driveInfo == null)
            {
                return new DiskSpaceInfo
                {
                    IsAvailable = false,
                    Path = path
                };
            }

            return new DiskSpaceInfo
            {
                IsAvailable = true,
                Path = path,
                DriveName = driveInfo.Name,
                TotalBytes = driveInfo.TotalSize,
                AvailableBytes = driveInfo.AvailableFreeSpace,
                UsedBytes = driveInfo.TotalSize - driveInfo.AvailableFreeSpace,
                PercentUsed = (double)(driveInfo.TotalSize - driveInfo.AvailableFreeSpace) / driveInfo.TotalSize * 100.0,
                HasMinimumSpace = driveInfo.AvailableFreeSpace >= MinimumFreeSpaceBytes,
                HasRecommendedSpace = driveInfo.AvailableFreeSpace >= RecommendedFreeSpaceBytes
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting disk space info for {Path}", path);
            return new DiskSpaceInfo
            {
                IsAvailable = false,
                Path = path
            };
        }
    }

    /// <summary>
    /// Gets the DriveInfo for the specified path
    /// </summary>
    private DriveInfo? GetDriveInfo(string path)
    {
        try
        {
            // Normalize the path
            var fullPath = Path.GetFullPath(path);
            
            // Get the root directory
            var rootPath = Path.GetPathRoot(fullPath);
            if (string.IsNullOrEmpty(rootPath))
            {
                return null;
            }

            // Get drive info
            var driveInfo = new DriveInfo(rootPath);
            
            // Verify the drive is ready
            if (!driveInfo.IsReady)
            {
                _logger.LogWarning("Drive {Drive} is not ready", rootPath);
                return null;
            }

            return driveInfo;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting drive info for {Path}", path);
            return null;
        }
    }

    /// <summary>
    /// Estimates the space required for video generation based on duration and quality
    /// </summary>
    /// <param name="durationSeconds">Duration of the video in seconds</param>
    /// <param name="quality">Quality setting (0-100, where 100 is highest)</param>
    /// <returns>Estimated bytes required</returns>
    public long EstimateVideoSpaceRequired(double durationSeconds, int quality = 50)
    {
        // Rough estimates based on typical bitrates
        // Low quality (0-33): ~500 KB/s
        // Medium quality (34-66): ~1 MB/s
        // High quality (67-100): ~2 MB/s
        
        double bytesPerSecond = quality switch
        {
            <= 33 => 500 * 1024, // 500 KB/s
            <= 66 => 1024 * 1024, // 1 MB/s
            _ => 2 * 1024 * 1024  // 2 MB/s
        };

        var estimatedBytes = (long)(durationSeconds * bytesPerSecond);
        
        // Add 20% buffer for temporary files and intermediate processing
        var totalRequired = (long)(estimatedBytes * 1.2);

        _logger.LogDebug(
            "Estimated space for {Duration:F1}s video at quality {Quality}: {SizeMB:F2} MB",
            durationSeconds, quality, totalRequired / (1024.0 * 1024.0));

        return totalRequired;
    }

    /// <summary>
    /// Checks if the system is running low on disk space
    /// </summary>
    public bool IsLowDiskSpace(string path)
    {
        try
        {
            var driveInfo = GetDriveInfo(path);
            if (driveInfo == null)
                return false;

            var availableSpace = driveInfo.AvailableFreeSpace;
            var totalSpace = driveInfo.TotalSize;
            var percentFree = (double)availableSpace / totalSpace * 100.0;

            // Consider low if less than 10% free or less than 500 MB
            return percentFree < 10.0 || availableSpace < 500 * 1024 * 1024;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking if low disk space for {Path}", path);
            return false;
        }
    }
}

/// <summary>
/// Information about disk space on a drive
/// </summary>
public class DiskSpaceInfo
{
    public bool IsAvailable { get; set; }
    public string Path { get; set; } = string.Empty;
    public string? DriveName { get; set; }
    public long TotalBytes { get; set; }
    public long AvailableBytes { get; set; }
    public long UsedBytes { get; set; }
    public double PercentUsed { get; set; }
    public bool HasMinimumSpace { get; set; }
    public bool HasRecommendedSpace { get; set; }
    
    public double TotalMegabytes => TotalBytes / (1024.0 * 1024.0);
    public double AvailableMegabytes => AvailableBytes / (1024.0 * 1024.0);
    public double UsedMegabytes => UsedBytes / (1024.0 * 1024.0);
    public double TotalGigabytes => TotalBytes / (1024.0 * 1024.0 * 1024.0);
    public double AvailableGigabytes => AvailableBytes / (1024.0 * 1024.0 * 1024.0);
}
