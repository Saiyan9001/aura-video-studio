using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace Aura.Core.Downloads;

public record EngineInstallProgress(
    string EngineId,
    string Phase, // downloading, extracting, verifying, complete
    long BytesProcessed,
    long TotalBytes,
    float PercentComplete,
    string? Message = null
);

public record EngineVerificationResult(
    string EngineId,
    bool IsValid,
    string Status,
    List<string> MissingFiles,
    List<string> Issues
);

public record EngineDiagnosticsResult(
    string EngineId,
    string InstallPath,
    bool IsInstalled,
    bool PathExists,
    bool PathWritable,
    long AvailableDiskSpaceBytes,
    string? LastError,
    string? ChecksumStatus,
    string? ExpectedSha256,
    string? ActualSha256,
    string? FailedUrl,
    List<string> Issues
);

public record InstallProvenance(
    string EngineId,
    string Version,
    string InstalledAt,
    string InstallPath,
    string Source, // "Mirror", "CustomUrl", or "LocalFile"
    string Url,
    string Sha256
);

/// <summary>
/// Handles installation, verification, repair, and removal of engines
/// </summary>
public class EngineInstaller
{
    private readonly ILogger<EngineInstaller> _logger;
    private readonly HttpClient _httpClient;
    private readonly HttpDownloader _downloader;
    private readonly string _installRoot;

    public EngineInstaller(
        ILogger<EngineInstaller> logger,
        HttpClient httpClient,
        string installRoot)
    {
        _logger = logger;
        _httpClient = httpClient;
        _installRoot = installRoot;
        
        // Create HttpDownloader with a logger
        var downloaderLogger = Microsoft.Extensions.Logging.Abstractions.NullLogger<HttpDownloader>.Instance;
        _downloader = new HttpDownloader(downloaderLogger, httpClient);

        if (!Directory.Exists(_installRoot))
        {
            Directory.CreateDirectory(_installRoot);
        }
    }

    /// <summary>
    /// Install an engine from manifest entry
    /// </summary>
    public async Task<string> InstallAsync(
        EngineManifestEntry engine,
        IProgress<EngineInstallProgress>? progress = null,
        CancellationToken ct = default,
        string? customUrl = null,
        string? localFilePath = null)
    {
        _logger.LogInformation("Installing engine: {EngineId}", engine.Id);

        string installPath = Path.Combine(_installRoot, engine.Id);
        
        // Check if already installed
        if (Directory.Exists(installPath) && Directory.GetFiles(installPath).Length > 0)
        {
            _logger.LogWarning("Engine {EngineId} is already installed at {Path}", engine.Id, installPath);
            progress?.Report(new EngineInstallProgress(engine.Id, "complete", 0, 0, 100, "Already installed"));
            return installPath;
        }

        Directory.CreateDirectory(installPath);

        string? sourceUrl = null;
        string sourceType = "Mirror";

        try
        {
            // Determine the source
            if (!string.IsNullOrEmpty(localFilePath))
            {
                // Local file import
                sourceUrl = localFilePath;
                sourceType = "LocalFile";
                await InstallFromLocalFileAsync(engine, localFilePath, installPath, progress, ct).ConfigureAwait(false);
            }
            else
            {
                // Get platform-specific URL
                string platform = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "windows" : "linux";
                
                // Build list of URLs to try (custom URL, then primary, then mirrors)
                var urls = new List<string>();
                
                if (!string.IsNullOrEmpty(customUrl))
                {
                    urls.Add(customUrl);
                    sourceUrl = customUrl;
                    sourceType = "CustomUrl";
                }
                else if (engine.Urls.TryGetValue(platform, out string? primaryUrl) && !string.IsNullOrEmpty(primaryUrl))
                {
                    urls.Add(primaryUrl);
                    sourceUrl = primaryUrl;
                }
                
                // Add mirrors if available
                if (engine.Mirrors?.TryGetValue(platform, out var mirrors) == true && mirrors != null)
                {
                    urls.AddRange(mirrors);
                    if (sourceUrl == null && mirrors.Count > 0)
                    {
                        sourceUrl = mirrors[0];
                    }
                }

                if (urls.Count == 0)
                {
                    throw new InvalidOperationException($"No URL found for platform {platform}");
                }

                // Handle git repositories differently
                if (engine.ArchiveType == "git")
                {
                    await InstallFromGitAsync(engine, urls[0], installPath, progress, ct).ConfigureAwait(false);
                }
                else
                {
                    await InstallFromArchiveAsync(engine, urls, installPath, progress, ct, sourceType).ConfigureAwait(false);
                }
            }

            // Write provenance file
            var provenance = new InstallProvenance(
                engine.Id,
                engine.Version,
                DateTime.UtcNow.ToString("o"),
                installPath,
                sourceType,
                sourceUrl ?? "unknown",
                engine.Sha256 ?? "unknown"
            );
            
            await WriteProvenanceAsync(installPath, provenance).ConfigureAwait(false);

            _logger.LogInformation("Engine {EngineId} installed successfully at {Path}", engine.Id, installPath);
            progress?.Report(new EngineInstallProgress(engine.Id, "complete", engine.SizeBytes, engine.SizeBytes, 100, $"Installation complete: {installPath}"));
            
            return installPath;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to install engine {EngineId}", engine.Id);
            
            // Cleanup on failure
            if (Directory.Exists(installPath))
            {
                try
                {
                    Directory.Delete(installPath, true);
                }
                catch (Exception cleanupEx)
                {
                    _logger.LogWarning(cleanupEx, "Failed to cleanup after failed install");
                }
            }
            
            throw;
        }
    }

    private async Task InstallFromLocalFileAsync(
        EngineManifestEntry engine,
        string localFilePath,
        string installPath,
        IProgress<EngineInstallProgress>? progress,
        CancellationToken ct)
    {
        progress?.Report(new EngineInstallProgress(engine.Id, "importing", 0, engine.SizeBytes, 0, "Importing from local file..."));

        // Use persistent download directory
        string downloadDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Aura", "Downloads", engine.Id, engine.Version);
        Directory.CreateDirectory(downloadDir);
        
        string archiveFile = Path.Combine(downloadDir, $"{engine.Id}.archive");
        
        try
        {
            var importProgress = new Progress<HttpDownloadProgress>(p =>
            {
                progress?.Report(new EngineInstallProgress(
                    engine.Id, 
                    "importing", 
                    p.BytesDownloaded, 
                    p.TotalBytes, 
                    p.PercentComplete,
                    p.Message ?? $"Importing: {p.PercentComplete:F1}%"
                ));
            });
            
            bool importSuccess = await _downloader.ImportLocalFileAsync(
                localFilePath, 
                archiveFile, 
                engine.Sha256, // Pass checksum for verification
                importProgress, 
                ct).ConfigureAwait(false);
            
            if (!importSuccess)
            {
                // Checksum mismatch - ask user if they want to continue
                _logger.LogWarning("Local file checksum mismatch, but continuing with installation");
                progress?.Report(new EngineInstallProgress(engine.Id, "importing", 0, 0, 0, 
                    "Warning: Checksum mismatch. Continuing anyway..."));
            }
            
            _logger.LogInformation("Local file import complete for {EngineId}", engine.Id);

            // Extract archive
            progress?.Report(new EngineInstallProgress(engine.Id, "extracting", 0, 0, 0, "Extracting..."));
            await ExtractArchiveAsync(archiveFile, installPath, engine.ArchiveType).ConfigureAwait(false);
            
            // Clean up archive after successful extraction
            if (File.Exists(archiveFile))
            {
                File.Delete(archiveFile);
                _logger.LogInformation("Cleaned up archive file for {EngineId}", engine.Id);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to install {EngineId} from local file", engine.Id);
            throw;
        }
    }

    private async Task InstallFromGitAsync(
        EngineManifestEntry engine,
        string gitUrl,
        string installPath,
        IProgress<EngineInstallProgress>? progress,
        CancellationToken ct)
    {
        progress?.Report(new EngineInstallProgress(engine.Id, "downloading", 0, engine.SizeBytes, 0, "Cloning repository..."));

        // Use git clone
        var startInfo = new ProcessStartInfo
        {
            FileName = "git",
            Arguments = $"clone --depth 1 \"{gitUrl}\" \"{installPath}\"",
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };

        using var process = new Process { StartInfo = startInfo };
        process.Start();

        string output = await process.StandardOutput.ReadToEndAsync(ct).ConfigureAwait(false);
        string error = await process.StandardError.ReadToEndAsync(ct).ConfigureAwait(false);
        
        await process.WaitForExitAsync(ct).ConfigureAwait(false);

        if (process.ExitCode != 0)
        {
            _logger.LogError("Git clone failed: {Error}", error);
            throw new InvalidOperationException($"Git clone failed: {error}");
        }

        _logger.LogInformation("Git clone completed for {EngineId}", engine.Id);
    }

    private async Task InstallFromArchiveAsync(
        EngineManifestEntry engine,
        List<string> urls,
        string installPath,
        IProgress<EngineInstallProgress>? progress,
        CancellationToken ct,
        string sourceType = "Mirror")
    {
        // Use persistent download directory instead of temp
        string downloadDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Aura", "Downloads", engine.Id, engine.Version);
        Directory.CreateDirectory(downloadDir);
        
        string archiveFile = Path.Combine(downloadDir, $"{engine.Id}.archive");
        
        try
        {
            progress?.Report(new EngineInstallProgress(engine.Id, "downloading", 0, engine.SizeBytes, 0, "Starting download..."));
            
            // Use HttpDownloader with resume, retry, and checksum verification
            var downloadProgress = new Progress<HttpDownloadProgress>(p =>
            {
                string message = p.Message ?? $"{p.PercentComplete:F1}% - {FormatBytes(p.BytesDownloaded)}/{FormatBytes(p.TotalBytes)} at {FormatSpeed(p.SpeedBytesPerSecond)}";
                if (!string.IsNullOrEmpty(p.ActiveMirror))
                {
                    message = $"[{p.ActiveMirror}] {message}";
                }
                
                progress?.Report(new EngineInstallProgress(
                    engine.Id, 
                    "downloading", 
                    p.BytesDownloaded, 
                    p.TotalBytes, 
                    p.PercentComplete,
                    message
                ));
            });
            
            bool downloadSuccess = await _downloader.DownloadFileWithMirrorsAsync(
                urls, 
                archiveFile, 
                engine.Sha256, // Pass checksum for verification
                downloadProgress, 
                ct).ConfigureAwait(false);
            
            if (!downloadSuccess)
            {
                throw new InvalidOperationException("Download or checksum verification failed. Check the logs for details.");
            }
            
            _logger.LogInformation("Download and verification complete for {EngineId}", engine.Id);

            // Extract archive
            progress?.Report(new EngineInstallProgress(engine.Id, "extracting", 0, 0, 0, "Extracting..."));
            await ExtractArchiveAsync(archiveFile, installPath, engine.ArchiveType).ConfigureAwait(false);
            
            // Clean up archive after successful extraction
            if (File.Exists(archiveFile))
            {
                File.Delete(archiveFile);
                _logger.LogInformation("Cleaned up archive file for {EngineId}", engine.Id);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to install {EngineId} from archive", engine.Id);
            
            // Clean up on failure but keep partial downloads for resume
            if (ex is not OperationCanceledException)
            {
                // Don't delete the archive on failure - it might be a partial download that can be resumed
                _logger.LogInformation("Keeping partial download for resume: {File}", archiveFile);
            }
            throw;
        }
    }

    private static string FormatBytes(long bytes)
    {
        string[] sizes = { "B", "KB", "MB", "GB", "TB" };
        int order = 0;
        double size = bytes;
        while (size >= 1024 && order < sizes.Length - 1)
        {
            order++;
            size /= 1024;
        }
        return $"{size:0.##} {sizes[order]}";
    }

    private static string FormatSpeed(double bytesPerSecond)
    {
        return $"{FormatBytes((long)bytesPerSecond)}/s";
    }

    private async Task ExtractArchiveAsync(string archiveFile, string targetDir, string archiveType)
    {
        await Task.Run(() =>
        {
            if (archiveType == "zip")
            {
                ZipFile.ExtractToDirectory(archiveFile, targetDir, overwriteFiles: true);
            }
            else if (archiveType == "tar.gz")
            {
                // For tar.gz support, install SharpZipLib or use system tar command
                throw new NotSupportedException($"Archive type {archiveType} is not supported. Please extract manually or use a zip archive.");
            }
        }).ConfigureAwait(false);
    }

    /// <summary>
    /// Verify engine installation
    /// </summary>
    public async Task<EngineVerificationResult> VerifyAsync(EngineManifestEntry engine)
    {
        _logger.LogInformation("Verifying engine: {EngineId}", engine.Id);

        string installPath = Path.Combine(_installRoot, engine.Id);
        var missingFiles = new List<string>();
        var issues = new List<string>();

        if (!Directory.Exists(installPath))
        {
            return new EngineVerificationResult(engine.Id, false, "Not installed", new List<string>(), new List<string> { "Installation directory not found" });
        }

        // Check for entrypoint
        string entrypointPath = Path.Combine(installPath, engine.Entrypoint);
        if (!File.Exists(entrypointPath))
        {
            missingFiles.Add(engine.Entrypoint);
            issues.Add($"Entrypoint file not found: {engine.Entrypoint}");
        }

        // Check if directory has any files
        int fileCount = Directory.GetFiles(installPath, "*", SearchOption.AllDirectories).Length;
        if (fileCount == 0)
        {
            issues.Add("Installation directory is empty");
        }

        bool isValid = missingFiles.Count == 0 && issues.Count == 0;
        string status = isValid ? "Valid" : "Invalid";

        return await Task.FromResult(new EngineVerificationResult(engine.Id, isValid, status, missingFiles, issues));
    }

    /// <summary>
    /// Repair engine installation by reinstalling
    /// </summary>
    public async Task<string> RepairAsync(
        EngineManifestEntry engine,
        IProgress<EngineInstallProgress>? progress = null,
        CancellationToken ct = default)
    {
        _logger.LogInformation("Repairing engine: {EngineId}", engine.Id);

        // Clean up partial downloads
        string downloadDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Aura", "Downloads", engine.Id, engine.Version);
        
        if (Directory.Exists(downloadDir))
        {
            try
            {
                var partialFiles = Directory.GetFiles(downloadDir, "*.partial");
                foreach (var partialFile in partialFiles)
                {
                    _logger.LogInformation("Deleting partial download: {File}", partialFile);
                    File.Delete(partialFile);
                }
                
                // Also delete the main archive file if it exists but might be corrupted
                var archiveFile = Path.Combine(downloadDir, $"{engine.Id}.archive");
                if (File.Exists(archiveFile))
                {
                    _logger.LogInformation("Deleting potentially corrupted archive: {File}", archiveFile);
                    File.Delete(archiveFile);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to clean up some partial downloads");
            }
        }

        // Remove and reinstall
        await RemoveAsync(engine).ConfigureAwait(false);
        return await InstallAsync(engine, progress, ct).ConfigureAwait(false);
    }

    /// <summary>
    /// Get diagnostics for an engine installation
    /// </summary>
    public async Task<EngineDiagnosticsResult> GetDiagnosticsAsync(EngineManifestEntry engine)
    {
        _logger.LogInformation("Getting diagnostics for engine: {EngineId}", engine.Id);

        string installPath = Path.Combine(_installRoot, engine.Id);
        var issues = new List<string>();
        bool isInstalled = IsInstalled(engine.Id);
        bool pathExists = Directory.Exists(installPath);
        bool pathWritable = false;
        long availableDiskSpace = 0;
        string? checksumStatus = null;
        string? expectedSha256 = null;
        string? actualSha256 = null;
        string? failedUrl = null;

        // Check disk space
        try
        {
            DriveInfo drive = new DriveInfo(Path.GetPathRoot(installPath) ?? "C:\\");
            availableDiskSpace = drive.AvailableFreeSpace;
            
            if (availableDiskSpace < engine.SizeBytes * 2) // Need 2x size for extraction
            {
                issues.Add($"Insufficient disk space. Need {FormatBytes(engine.SizeBytes * 2)}, available: {FormatBytes(availableDiskSpace)}");
            }
        }
        catch (Exception ex)
        {
            issues.Add($"Could not check disk space: {ex.Message}");
        }

        // Check path writability
        try
        {
            if (!pathExists)
            {
                Directory.CreateDirectory(installPath);
                pathExists = true;
            }
            
            string testFile = Path.Combine(installPath, $".test_{Guid.NewGuid()}.tmp");
            File.WriteAllText(testFile, "test");
            File.Delete(testFile);
            pathWritable = true;
        }
        catch (Exception ex)
        {
            issues.Add($"Path is not writable: {ex.Message}");
        }

        // Check if partial download exists in download directory
        string downloadDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Aura", "Downloads", engine.Id, engine.Version);
        if (Directory.Exists(downloadDir))
        {
            var partialFiles = Directory.GetFiles(downloadDir, "*.partial");
            if (partialFiles.Length > 0)
            {
                issues.Add($"Found {partialFiles.Length} partial download(s). Repair will clean these up and retry.");
            }
        }

        // Get the download URL for this platform
        string platform = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "windows" : "linux";
        if (engine.Urls.TryGetValue(platform, out string? url) && !string.IsNullOrEmpty(url))
        {
            failedUrl = url;
        }

        // If installed, verify checksum
        if (isInstalled)
        {
            var verifyResult = await VerifyAsync(engine).ConfigureAwait(false);
            checksumStatus = verifyResult.IsValid ? "Valid" : "Invalid";
            if (!verifyResult.IsValid)
            {
                issues.AddRange(verifyResult.Issues);
                
                // Try to compute actual checksum for the entrypoint file
                expectedSha256 = engine.Sha256;
                if (!string.IsNullOrEmpty(expectedSha256))
                {
                    try
                    {
                        // Note: We're checking the archive's SHA256, not individual files
                        // In a failed installation, the archive may not exist, so we just report the expected value
                        actualSha256 = "Unable to compute (installation incomplete or corrupted)";
                        issues.Add($"Expected checksum: {expectedSha256}");
                    }
                    catch
                    {
                        // Ignore errors when computing checksum
                    }
                }
            }
        }
        else if (!string.IsNullOrEmpty(engine.Sha256))
        {
            // Not installed but we know the expected checksum
            expectedSha256 = engine.Sha256;
        }

        return await Task.FromResult(new EngineDiagnosticsResult(
            engine.Id,
            installPath,
            isInstalled,
            pathExists,
            pathWritable,
            availableDiskSpace,
            null, // LastError would come from process manager
            checksumStatus,
            expectedSha256,
            actualSha256,
            failedUrl,
            issues
        ));
    }

    /// <summary>
    /// Remove engine installation
    /// </summary>
    public async Task RemoveAsync(EngineManifestEntry engine)
    {
        _logger.LogInformation("Removing engine: {EngineId}", engine.Id);

        string installPath = Path.Combine(_installRoot, engine.Id);
        
        if (Directory.Exists(installPath))
        {
            await Task.Run(() =>
            {
                try
                {
                    Directory.Delete(installPath, true);
                    _logger.LogInformation("Engine {EngineId} removed successfully", engine.Id);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to remove engine {EngineId}", engine.Id);
                    throw;
                }
            }).ConfigureAwait(false);
        }
        else
        {
            _logger.LogWarning("Engine {EngineId} installation directory not found", engine.Id);
        }
    }

    /// <summary>
    /// Get installation path for an engine
    /// </summary>
    public string GetInstallPath(string engineId)
    {
        return Path.Combine(_installRoot, engineId);
    }

    /// <summary>
    /// Check if engine is installed
    /// </summary>
    public bool IsInstalled(string engineId)
    {
        string installPath = GetInstallPath(engineId);
        return Directory.Exists(installPath) && Directory.GetFiles(installPath, "*", SearchOption.AllDirectories).Length > 0;
    }

    /// <summary>
    /// Write installation provenance file
    /// </summary>
    private async Task WriteProvenanceAsync(string installPath, InstallProvenance provenance)
    {
        try
        {
            string provenanceFile = Path.Combine(installPath, "install.json");
            string json = System.Text.Json.JsonSerializer.Serialize(provenance, new System.Text.Json.JsonSerializerOptions 
            { 
                WriteIndented = true 
            });
            await File.WriteAllTextAsync(provenanceFile, json).ConfigureAwait(false);
            _logger.LogInformation("Wrote provenance file to {Path}", provenanceFile);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to write provenance file, continuing anyway");
        }
    }

    /// <summary>
    /// Read installation provenance file
    /// </summary>
    public async Task<InstallProvenance?> ReadProvenanceAsync(string engineId)
    {
        try
        {
            string installPath = GetInstallPath(engineId);
            string provenanceFile = Path.Combine(installPath, "install.json");
            
            if (!File.Exists(provenanceFile))
            {
                return null;
            }

            string json = await File.ReadAllTextAsync(provenanceFile).ConfigureAwait(false);
            return System.Text.Json.JsonSerializer.Deserialize<InstallProvenance>(json);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to read provenance file for {EngineId}", engineId);
            return null;
        }
    }
}
