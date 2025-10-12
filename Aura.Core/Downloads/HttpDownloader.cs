using System;
using System.IO;
using System.Net.Http;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace Aura.Core.Downloads;

public record HttpDownloadProgress(
    long BytesDownloaded,
    long TotalBytes,
    float PercentComplete,
    double SpeedBytesPerSecond,
    string? Message = null,
    string? ErrorCode = null, // E-DL-404, E-DL-TIMEOUT, E-DL-CHECKSUM
    string? ActiveMirror = null
);

/// <summary>
/// Robust HTTP downloader with resume support, retry logic, and checksum verification
/// </summary>
public class HttpDownloader
{
    private readonly ILogger<HttpDownloader> _logger;
    private readonly HttpClient _httpClient;
    private const int BufferSize = 8192;
    private const int MaxRetries = 3;

    public HttpDownloader(ILogger<HttpDownloader> logger, HttpClient httpClient)
    {
        _logger = logger;
        _httpClient = httpClient;
    }

    /// <summary>
    /// Download a file with resume support and optional checksum verification
    /// </summary>
    public async Task<bool> DownloadFileAsync(
        string url,
        string outputPath,
        string? expectedSha256 = null,
        IProgress<HttpDownloadProgress>? progress = null,
        CancellationToken ct = default)
    {
        var urls = new List<string> { url };
        return await DownloadFileWithMirrorsAsync(urls, outputPath, expectedSha256, progress, ct);
    }

    /// <summary>
    /// Download a file with mirror fallback support
    /// </summary>
    public async Task<bool> DownloadFileWithMirrorsAsync(
        List<string> urls,
        string outputPath,
        string? expectedSha256 = null,
        IProgress<HttpDownloadProgress>? progress = null,
        CancellationToken ct = default)
    {
        if (urls == null || urls.Count == 0)
        {
            throw new ArgumentException("At least one URL must be provided", nameof(urls));
        }

        Exception? lastException = null;
        
        // Try each mirror
        for (int mirrorIndex = 0; mirrorIndex < urls.Count; mirrorIndex++)
        {
            string currentUrl = urls[mirrorIndex];
            _logger.LogInformation("Trying URL {Index} of {Total}: {Url}", mirrorIndex + 1, urls.Count, currentUrl);
            
            // Try with retries for each mirror
            for (int attempt = 0; attempt < MaxRetries; attempt++)
            {
                try
                {
                    if (attempt > 0)
                    {
                        _logger.LogInformation("Retry attempt {Attempt} of {MaxRetries} for mirror {MirrorIndex}", 
                            attempt, MaxRetries, mirrorIndex + 1);
                        await Task.Delay(TimeSpan.FromSeconds(Math.Pow(2, attempt)), ct); // Exponential backoff
                    }

                    progress?.Report(new HttpDownloadProgress(0, 0, 0, 0, 
                        $"Trying mirror {mirrorIndex + 1}/{urls.Count}...", null, currentUrl));

                    var result = await DownloadFileInternalAsync(currentUrl, outputPath, expectedSha256, progress, ct, currentUrl);
                    
                    if (result)
                    {
                        _logger.LogInformation("Successfully downloaded from mirror {Index}", mirrorIndex + 1);
                        return true;
                    }
                    
                    // Checksum failed - try next mirror
                    _logger.LogWarning("Download succeeded but checksum verification failed for mirror {Index}", mirrorIndex + 1);
                    break; // Don't retry this mirror, move to next one
                }
                catch (HttpRequestException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
                {
                    _logger.LogWarning("Mirror {Index} returned 404, trying next mirror", mirrorIndex + 1);
                    progress?.Report(new HttpDownloadProgress(0, 0, 0, 0, 
                        $"Mirror {mirrorIndex + 1} returned 404", "E-DL-404", currentUrl));
                    lastException = ex;
                    break; // Don't retry 404s, move to next mirror
                }
                catch (TaskCanceledException ex)
                {
                    _logger.LogWarning("Download from mirror {Index} timed out", mirrorIndex + 1);
                    progress?.Report(new HttpDownloadProgress(0, 0, 0, 0, 
                        $"Mirror {mirrorIndex + 1} timed out", "E-DL-TIMEOUT", currentUrl));
                    lastException = ex;
                    
                    if (attempt < MaxRetries - 1)
                    {
                        continue; // Retry timeouts
                    }
                    break; // Max retries reached, try next mirror
                }
                catch (HttpRequestException ex) when (attempt < MaxRetries - 1)
                {
                    _logger.LogWarning(ex, "Download attempt {Attempt} failed for mirror {Index}, will retry", 
                        attempt + 1, mirrorIndex + 1);
                    lastException = ex;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Download failed for mirror {Index}: {Url}", mirrorIndex + 1, currentUrl);
                    lastException = ex;
                    break; // Don't retry unexpected errors, try next mirror
                }
            }
        }

        // All mirrors exhausted
        _logger.LogError("All {Count} mirror(s) failed", urls.Count);
        if (lastException != null)
        {
            throw lastException;
        }
        
        return false;
    }

    /// <summary>
    /// Import a file from local path with checksum verification
    /// </summary>
    public async Task<bool> ImportLocalFileAsync(
        string localPath,
        string outputPath,
        string? expectedSha256 = null,
        IProgress<HttpDownloadProgress>? progress = null,
        CancellationToken ct = default)
    {
        if (!File.Exists(localPath))
        {
            throw new FileNotFoundException($"Local file not found: {localPath}", localPath);
        }

        _logger.LogInformation("Importing local file: {LocalPath}", localPath);
        
        progress?.Report(new HttpDownloadProgress(0, 0, 0, 0, "Copying local file...", null, "LocalFile"));

        // Copy file
        long fileSize = new FileInfo(localPath).Length;
        await using (var sourceStream = File.OpenRead(localPath))
        await using (var destStream = File.Create(outputPath))
        {
            var buffer = new byte[81920]; // 80KB buffer
            long totalRead = 0;
            int bytesRead;
            
            while ((bytesRead = await sourceStream.ReadAsync(buffer, 0, buffer.Length, ct)) > 0)
            {
                await destStream.WriteAsync(buffer, 0, bytesRead, ct);
                totalRead += bytesRead;
                
                float percent = fileSize > 0 ? (float)(totalRead * 100.0 / fileSize) : 0;
                progress?.Report(new HttpDownloadProgress(totalRead, fileSize, percent, 0, 
                    $"Copying: {percent:F1}%", null, "LocalFile"));
            }
        }

        _logger.LogInformation("Local file copied successfully");

        // Verify checksum if provided
        if (!string.IsNullOrEmpty(expectedSha256))
        {
            _logger.LogInformation("Verifying checksum...");
            progress?.Report(new HttpDownloadProgress(fileSize, fileSize, 100, 0, "Verifying checksum...", null, "LocalFile"));
            
            var actualSha256 = await ComputeSha256Async(outputPath, ct);
            
            if (!string.Equals(actualSha256, expectedSha256, StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogWarning("Checksum mismatch! Expected: {Expected}, Actual: {Actual}", 
                    expectedSha256, actualSha256);
                progress?.Report(new HttpDownloadProgress(fileSize, fileSize, 100, 0, 
                    "Checksum mismatch - accept anyway?", "E-DL-CHECKSUM", "LocalFile"));
                
                // Don't delete the file, let caller decide
                return false;
            }
            
            _logger.LogInformation("Checksum verified successfully");
        }
        else
        {
            _logger.LogWarning("No checksum provided for local file, skipping verification");
        }

        progress?.Report(new HttpDownloadProgress(fileSize, fileSize, 100, 0, "Import complete", null, "LocalFile"));
        return true;
    }

    private async Task<bool> DownloadFileInternalAsync(
        string url,
        string outputPath,
        string? expectedSha256,
        IProgress<HttpDownloadProgress>? progress,
        CancellationToken ct,
        string? activeMirror = null)
    {
        var partialPath = outputPath + ".partial";
        var startTime = DateTime.UtcNow;
        long totalBytesRead = 0;

        try
        {
            // Check if partial download exists
            long existingBytes = 0;
            if (File.Exists(partialPath))
            {
                existingBytes = new FileInfo(partialPath).Length;
                _logger.LogInformation("Found partial download: {Bytes} bytes", existingBytes);
                totalBytesRead = existingBytes;
            }

            // Make HTTP request with range header for resume
            using var request = new HttpRequestMessage(HttpMethod.Get, url);
            if (existingBytes > 0)
            {
                request.Headers.Range = new System.Net.Http.Headers.RangeHeaderValue(existingBytes, null);
            }

            using var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct);
            response.EnsureSuccessStatusCode();

            var totalBytes = existingBytes + (response.Content.Headers.ContentLength ?? 0);
            _logger.LogInformation("Downloading {Url} ({TotalBytes} bytes)", url, totalBytes);

            // Download to partial file
            {
                // Open file for writing (append if resuming)
                await using var fileStream = new FileStream(
                    partialPath,
                    existingBytes > 0 ? FileMode.Append : FileMode.Create,
                    FileAccess.Write,
                    FileShare.None,
                    BufferSize,
                    useAsync: true);

                await using var httpStream = await response.Content.ReadAsStreamAsync(ct);
                
                var buffer = new byte[BufferSize];
                int bytesRead;
                var lastProgressReport = DateTime.UtcNow;

                while ((bytesRead = await httpStream.ReadAsync(buffer, 0, buffer.Length, ct)) > 0)
                {
                    await fileStream.WriteAsync(buffer, 0, bytesRead, ct);
                    totalBytesRead += bytesRead;

                    // Report progress every 500ms
                    if ((DateTime.UtcNow - lastProgressReport).TotalMilliseconds >= 500)
                    {
                        var elapsed = (DateTime.UtcNow - startTime).TotalSeconds;
                        var speed = elapsed > 0 ? totalBytesRead / elapsed : 0;
                        var percentComplete = totalBytes > 0 ? (float)(totalBytesRead * 100.0 / totalBytes) : 0;

                        progress?.Report(new HttpDownloadProgress(
                            totalBytesRead,
                            totalBytes,
                            percentComplete,
                            speed,
                            null,
                            null,
                            activeMirror ?? url
                        ));

                        lastProgressReport = DateTime.UtcNow;
                    }
                }

                // Ensure stream is flushed before closing
                await fileStream.FlushAsync(ct);
            } // File stream is now closed

            // Final progress report
            progress?.Report(new HttpDownloadProgress(totalBytesRead, totalBytes, 100, 0, "Download complete", null, activeMirror ?? url));

            // Move partial file to final location
            if (File.Exists(outputPath))
            {
                File.Delete(outputPath);
            }
            File.Move(partialPath, outputPath);

            _logger.LogInformation("Download complete: {OutputPath}", outputPath);

            // Verify checksum if provided
            if (!string.IsNullOrEmpty(expectedSha256))
            {
                _logger.LogInformation("Verifying checksum...");
                var actualSha256 = await ComputeSha256Async(outputPath, ct);
                
                if (!string.Equals(actualSha256, expectedSha256, StringComparison.OrdinalIgnoreCase))
                {
                    _logger.LogError("Checksum mismatch! Expected: {Expected}, Actual: {Actual}", 
                        expectedSha256, actualSha256);
                    
                    // Delete the downloaded file on checksum failure
                    if (File.Exists(outputPath))
                    {
                        File.Delete(outputPath);
                        _logger.LogInformation("Deleted file with mismatched checksum");
                    }
                    
                    return false;
                }
                
                _logger.LogInformation("Checksum verified successfully");
            }

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Download failed: {Url}", url);
            throw;
        }
    }

    private async Task<string> ComputeSha256Async(string filePath, CancellationToken ct = default)
    {
        using var sha256 = SHA256.Create();
        await using var stream = File.OpenRead(filePath);
        var hashBytes = await sha256.ComputeHashAsync(stream, ct);
        return Convert.ToHexString(hashBytes).ToLowerInvariant();
    }
}
