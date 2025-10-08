using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace Aura.Core.Dependencies;

public class DependencyManager
{
    private readonly ILogger<DependencyManager> _logger;
    private readonly HttpClient _httpClient;
    private readonly string _manifestPath;
    private readonly string _downloadDirectory;

    public DependencyManager(
        ILogger<DependencyManager> logger,
        HttpClient httpClient,
        string manifestPath,
        string downloadDirectory)
    {
        _logger = logger;
        _httpClient = httpClient;
        _manifestPath = manifestPath;
        _downloadDirectory = downloadDirectory;
        
        // Ensure download directory exists
        if (!Directory.Exists(_downloadDirectory))
        {
            Directory.CreateDirectory(_downloadDirectory);
        }
    }
    
    public async Task<DependencyManifest> LoadManifestAsync()
    {
        if (File.Exists(_manifestPath))
        {
            try
            {
                string json = await File.ReadAllTextAsync(_manifestPath);
                var manifest = JsonSerializer.Deserialize<DependencyManifest>(json);
                return manifest ?? new DependencyManifest();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to load dependency manifest, creating new one");
            }
        }
        
        // Create a new manifest if it doesn't exist or couldn't be loaded
        var newManifest = new DependencyManifest
        {
            Components = new List<DependencyComponent>
            {
                new DependencyComponent
                {
                    Name = "FFmpeg",
                    Version = "6.0",
                    IsRequired = true,
                    Files = new List<DependencyFile>
                    {
                        new DependencyFile
                        {
                            Filename = "ffmpeg.exe",
                            Url = "https://github.com/BtbN/FFmpeg-Builds/releases/download/latest/ffmpeg-n6.0-latest-win64-gpl-6.0.zip",
                            Sha256 = "e25bfb9fc6986e5e42b0bcff64c20433171125243c5ebde1bbee29a4637434a9",
                            ExtractPath = "bin/ffmpeg.exe",
                            SizeBytes = 83558400
                        },
                        new DependencyFile
                        {
                            Filename = "ffprobe.exe",
                            Url = "https://github.com/BtbN/FFmpeg-Builds/releases/download/latest/ffmpeg-n6.0-latest-win64-gpl-6.0.zip",
                            Sha256 = "e25bfb9fc6986e5e42b0bcff64c20433171125243c5ebde1bbee29a4637434a9",
                            ExtractPath = "bin/ffprobe.exe",
                            SizeBytes = 83558400
                        }
                    }
                },
                new DependencyComponent
                {
                    Name = "Ollama",
                    Version = "0.1.19",
                    IsRequired = false,
                    Files = new List<DependencyFile>
                    {
                        new DependencyFile
                        {
                            Filename = "ollama-windows-amd64.zip",
                            Url = "https://github.com/ollama/ollama/releases/download/v0.1.19/ollama-windows-amd64.zip",
                            Sha256 = "f8e4078e510a4062186239fb8721b1a068a74fbe91e7bb7dff882191dff84e8a",
                            ExtractPath = "",
                            SizeBytes = 53620736
                        }
                    }
                }
            }
        };
        
        // Save the new manifest
        await SaveManifestAsync(newManifest);
        
        return newManifest;
    }
    
    private Task SaveManifestAsync(DependencyManifest manifest)
    {
        string json = JsonSerializer.Serialize(manifest, new JsonSerializerOptions
        {
            WriteIndented = true
        });
        
        return File.WriteAllTextAsync(_manifestPath, json);
    }
    
    public async Task<ComponentStatus> GetComponentStatusAsync(string componentName)
    {
        var manifest = await LoadManifestAsync();
        var component = manifest.Components.Find(c => c.Name == componentName);
        
        if (component == null)
        {
            return new ComponentStatus 
            { 
                Name = componentName, 
                IsInstalled = false, 
                NeedsRepair = false,
                ErrorMessage = "Component not found in manifest"
            };
        }
        
        bool allFilesExist = true;
        bool needsRepair = false;
        string? errorMessage = null;
        
        foreach (var file in component.Files)
        {
            string filePath = Path.Combine(_downloadDirectory, file.Filename);
            if (!File.Exists(filePath))
            {
                allFilesExist = false;
                break;
            }
            
            // Verify checksum
            if (!await VerifyChecksumAsync(filePath, file.Sha256))
            {
                needsRepair = true;
                errorMessage = "File checksum mismatch";
            }
        }
        
        // Run post-install probe if files exist
        if (allFilesExist && !needsRepair && !string.IsNullOrEmpty(component.PostInstallProbe))
        {
            var probeResult = await RunPostInstallProbeAsync(component);
            if (!probeResult.Success)
            {
                needsRepair = true;
                errorMessage = probeResult.ErrorMessage;
            }
        }
        
        return new ComponentStatus
        {
            Name = componentName,
            IsInstalled = allFilesExist && !needsRepair,
            NeedsRepair = allFilesExist && needsRepair,
            ErrorMessage = errorMessage
        };
    }
    
    public async Task<bool> IsComponentInstalledAsync(string componentName)
    {
        var status = await GetComponentStatusAsync(componentName);
        return status.IsInstalled;
    }
    
    public async Task DownloadComponentAsync(
        string componentName, 
        IProgress<DownloadProgress> progress, 
        CancellationToken ct)
    {
        var manifest = await LoadManifestAsync();
        var component = manifest.Components.Find(c => c.Name == componentName);
        
        if (component == null)
        {
            throw new ArgumentException($"Component {componentName} not found in manifest");
        }
        
        _logger.LogInformation("Downloading component: {Component} v{Version}", 
            component.Name, component.Version);
        
        foreach (var file in component.Files)
        {
            string filePath = Path.Combine(_downloadDirectory, file.Filename);
            
            // Check if file exists and has correct checksum
            if (File.Exists(filePath) && await VerifyChecksumAsync(filePath, file.Sha256))
            {
                _logger.LogInformation("File already exists with correct checksum: {File}", file.Filename);
                continue;
            }
            
            // Download the file
            await DownloadFileAsync(file.Url, filePath, file.SizeBytes, progress, ct);
            
            // Verify checksum
            if (!await VerifyChecksumAsync(filePath, file.Sha256))
            {
                _logger.LogError("Checksum verification failed for {File}", file.Filename);
                File.Delete(filePath);
                throw new Exception($"Checksum verification failed for {file.Filename}");
            }
            
            // Extract if needed
            if (file.Url.EndsWith(".zip"))
            {
                _logger.LogInformation("Extracting zip file: {File}", file.Filename);
                // Extract the file
                // In a real implementation, we would use System.IO.Compression.ZipFile
            }
        }
        
        _logger.LogInformation("Component download completed: {Component}", component.Name);
    }
    
    public async Task RepairComponentAsync(
        string componentName,
        IProgress<DownloadProgress> progress,
        CancellationToken ct)
    {
        _logger.LogInformation("Repairing component: {Component}", componentName);
        
        var manifest = await LoadManifestAsync();
        var component = manifest.Components.Find(c => c.Name == componentName);
        
        if (component == null)
        {
            throw new ArgumentException($"Component {componentName} not found in manifest");
        }
        
        foreach (var file in component.Files)
        {
            string filePath = Path.Combine(_downloadDirectory, file.Filename);
            
            // Check if file needs repair (missing or wrong checksum)
            bool needsRepair = !File.Exists(filePath) || 
                              !await VerifyChecksumAsync(filePath, file.Sha256);
            
            if (needsRepair)
            {
                _logger.LogInformation("Repairing file: {File}", file.Filename);
                
                // Delete corrupted file if it exists
                if (File.Exists(filePath))
                {
                    File.Delete(filePath);
                }
                
                // Re-download the file
                await DownloadFileAsync(file.Url, filePath, file.SizeBytes, progress, ct);
                
                // Verify checksum
                if (!await VerifyChecksumAsync(filePath, file.Sha256))
                {
                    _logger.LogError("Checksum verification failed after repair for {File}", file.Filename);
                    File.Delete(filePath);
                    throw new Exception($"Checksum verification failed after repair for {file.Filename}");
                }
            }
        }
        
        _logger.LogInformation("Component repair completed: {Component}", component.Name);
    }
    
    public async Task<bool> RemoveComponentAsync(string componentName)
    {
        _logger.LogInformation("Removing component: {Component}", componentName);
        
        var manifest = await LoadManifestAsync();
        var component = manifest.Components.Find(c => c.Name == componentName);
        
        if (component == null)
        {
            throw new ArgumentException($"Component {componentName} not found in manifest");
        }
        
        bool success = true;
        foreach (var file in component.Files)
        {
            string filePath = Path.Combine(_downloadDirectory, file.Filename);
            try
            {
                if (File.Exists(filePath))
                {
                    File.Delete(filePath);
                    _logger.LogInformation("Deleted file: {File}", filePath);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to delete file: {File}", filePath);
                success = false;
            }
        }
        
        return success;
    }
    
    public string GetComponentDirectory()
    {
        return _downloadDirectory;
    }
    
    public async Task<ManualInstallInstructions> GetManualInstallInstructionsAsync(string componentName)
    {
        var manifest = await LoadManifestAsync();
        var component = manifest.Components.Find(c => c.Name == componentName);
        
        if (component == null)
        {
            throw new ArgumentException($"Component {componentName} not found in manifest");
        }
        
        var instructions = new ManualInstallInstructions
        {
            ComponentName = component.Name,
            Version = component.Version,
            TargetDirectory = _downloadDirectory,
            Files = component.Files.Select(f => new ManualInstallFile
            {
                Filename = f.Filename,
                Url = f.Url,
                Sha256 = f.Sha256,
                SizeBytes = f.SizeBytes,
                InstallPath = f.InstallPath ?? _downloadDirectory
            }).ToList(),
            Instructions = $"Download the following files manually and place them in: {_downloadDirectory}\n\n" +
                          $"After placing files, verify checksums using the provided SHA-256 hashes."
        };
        
        return instructions;
    }
    
    public async Task<ChecksumVerificationResult> VerifyManualInstallAsync(string componentName)
    {
        var manifest = await LoadManifestAsync();
        var component = manifest.Components.Find(c => c.Name == componentName);
        
        if (component == null)
        {
            throw new ArgumentException($"Component {componentName} not found in manifest");
        }
        
        var result = new ChecksumVerificationResult
        {
            ComponentName = componentName,
            Files = new List<FileVerificationResult>()
        };
        
        foreach (var file in component.Files)
        {
            string filePath = Path.Combine(_downloadDirectory, file.Filename);
            var fileResult = new FileVerificationResult
            {
                Filename = file.Filename,
                ExpectedSha256 = file.Sha256,
                FilePath = filePath
            };
            
            if (!File.Exists(filePath))
            {
                fileResult.IsValid = false;
                fileResult.ErrorMessage = "File not found";
            }
            else
            {
                fileResult.IsValid = await VerifyChecksumAsync(filePath, file.Sha256);
                if (!fileResult.IsValid)
                {
                    using var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read);
                    using var sha256 = SHA256.Create();
                    byte[] hashBytes = await sha256.ComputeHashAsync(fs);
                    fileResult.ActualSha256 = BitConverter.ToString(hashBytes).Replace("-", "").ToLowerInvariant();
                    fileResult.ErrorMessage = "Checksum mismatch";
                }
            }
            
            result.Files.Add(fileResult);
        }
        
        result.IsValid = result.Files.All(f => f.IsValid);
        return result;
    }
    
    private async Task<ProbeResult> RunPostInstallProbeAsync(DependencyComponent component)
    {
        if (string.IsNullOrEmpty(component.PostInstallProbe))
        {
            return new ProbeResult { Success = true };
        }
        
        try
        {
            // Parse probe type from string (e.g., "ffmpeg:version", "http:http://localhost:11434/api/version", "file:models/sd-v1-5.safetensors")
            var probeParts = component.PostInstallProbe.Split(':', 2);
            var probeType = probeParts[0].ToLowerInvariant();
            var probeTarget = probeParts.Length > 1 ? probeParts[1] : "";
            
            switch (probeType)
            {
                case "ffmpeg":
                    return await ProbeFFmpegAsync();
                    
                case "http":
                    return await ProbeHttpEndpointAsync(probeTarget);
                    
                case "file":
                    return ProbeFile(probeTarget);
                    
                default:
                    _logger.LogWarning("Unknown probe type: {ProbeType}", probeType);
                    return new ProbeResult { Success = true }; // Don't fail on unknown probe types
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Post-install probe failed for {Component}", component.Name);
            return new ProbeResult 
            { 
                Success = false, 
                ErrorMessage = ex.Message 
            };
        }
    }
    
    private async Task<ProbeResult> ProbeFFmpegAsync()
    {
        try
        {
            var ffmpegPath = Path.Combine(_downloadDirectory, "ffmpeg.exe");
            if (!File.Exists(ffmpegPath))
            {
                return new ProbeResult 
                { 
                    Success = false, 
                    ErrorMessage = "FFmpeg executable not found" 
                };
            }
            
            // Try to run ffmpeg -version
            var processStartInfo = new System.Diagnostics.ProcessStartInfo
            {
                FileName = ffmpegPath,
                Arguments = "-version",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            
            using var process = System.Diagnostics.Process.Start(processStartInfo);
            if (process == null)
            {
                return new ProbeResult 
                { 
                    Success = false, 
                    ErrorMessage = "Failed to start FFmpeg process" 
                };
            }
            
            await process.WaitForExitAsync();
            
            if (process.ExitCode == 0)
            {
                return new ProbeResult { Success = true };
            }
            else
            {
                return new ProbeResult 
                { 
                    Success = false, 
                    ErrorMessage = $"FFmpeg exited with code {process.ExitCode}" 
                };
            }
        }
        catch (Exception ex)
        {
            return new ProbeResult 
            { 
                Success = false, 
                ErrorMessage = $"FFmpeg probe error: {ex.Message}" 
            };
        }
    }
    
    private async Task<ProbeResult> ProbeHttpEndpointAsync(string url)
    {
        try
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            var response = await _httpClient.GetAsync(url, cts.Token);
            
            if (response.IsSuccessStatusCode)
            {
                return new ProbeResult { Success = true };
            }
            else
            {
                return new ProbeResult 
                { 
                    Success = false, 
                    ErrorMessage = $"HTTP probe failed with status {response.StatusCode}" 
                };
            }
        }
        catch (Exception ex)
        {
            return new ProbeResult 
            { 
                Success = false, 
                ErrorMessage = $"HTTP probe error: {ex.Message}" 
            };
        }
    }
    
    private ProbeResult ProbeFile(string relativePath)
    {
        var fullPath = Path.Combine(_downloadDirectory, relativePath);
        if (File.Exists(fullPath))
        {
            return new ProbeResult { Success = true };
        }
        else
        {
            return new ProbeResult 
            { 
                Success = false, 
                ErrorMessage = $"File not found: {relativePath}" 
            };
        }
    }
    
    private async Task DownloadFileAsync(
        string url, 
        string filePath, 
        long expectedSize,
        IProgress<DownloadProgress> progress, 
        CancellationToken ct)
    {
        _logger.LogInformation("Downloading file: {Url} to {FilePath}", url, filePath);
        
        // Create directory if it doesn't exist
        Directory.CreateDirectory(Path.GetDirectoryName(filePath) ?? "");
        
        // Check if file partially exists (for resume)
        long startPosition = 0;
        if (File.Exists(filePath))
        {
            var fileInfo = new FileInfo(filePath);
            startPosition = fileInfo.Length;
            
            // If file is already complete size, verify and skip
            if (startPosition >= expectedSize && expectedSize > 0)
            {
                _logger.LogInformation("File already complete, skipping download: {FilePath}", filePath);
                progress.Report(new DownloadProgress(startPosition, expectedSize, 100, url));
                return;
            }
            
            _logger.LogInformation("Resuming download from {Position} bytes", startPosition);
        }
        
        // Create HTTP request with range header for resume
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        if (startPosition > 0)
        {
            request.Headers.Range = new System.Net.Http.Headers.RangeHeaderValue(startPosition, null);
        }
        
        using var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct);
        response.EnsureSuccessStatusCode();
        
        long totalBytes = response.Content.Headers.ContentLength ?? expectedSize;
        if (startPosition > 0 && response.StatusCode == System.Net.HttpStatusCode.PartialContent)
        {
            totalBytes = startPosition + (response.Content.Headers.ContentLength ?? 0);
        }
        else if (startPosition > 0 && response.StatusCode == System.Net.HttpStatusCode.OK)
        {
            // Server doesn't support resume, start from beginning
            _logger.LogWarning("Server doesn't support resume, starting from beginning");
            startPosition = 0;
            if (File.Exists(filePath))
            {
                File.Delete(filePath);
            }
        }
        
        long bytesRead = startPosition;
        
        using var contentStream = await response.Content.ReadAsStreamAsync(ct);
        using var fileStream = new FileStream(
            filePath, 
            startPosition > 0 ? FileMode.Append : FileMode.Create, 
            FileAccess.Write, 
            FileShare.None, 
            8192, 
            true);
        
        var buffer = new byte[8192];
        var lastProgressReport = DateTime.Now;
        
        while (true)
        {
            int read = await contentStream.ReadAsync(buffer, 0, buffer.Length, ct);
            if (read == 0) break;
            
            await fileStream.WriteAsync(buffer, 0, read, ct);
            
            bytesRead += read;
            
            // Report progress no more than once per 100ms
            var now = DateTime.Now;
            if ((now - lastProgressReport).TotalMilliseconds >= 100)
            {
                lastProgressReport = now;
                
                float percentComplete = totalBytes > 0 
                    ? (float)bytesRead / totalBytes * 100 
                    : 0;
                
                progress.Report(new DownloadProgress(
                    bytesRead, 
                    totalBytes, 
                    percentComplete,
                    url));
            }
        }
        
        // Final progress report
        progress.Report(new DownloadProgress(
            bytesRead, 
            totalBytes, 
            100,
            url));
        
        _logger.LogInformation("Download completed: {FilePath}, {Bytes} bytes", filePath, bytesRead);
    }
    
    private async Task<bool> VerifyChecksumAsync(string filePath, string expectedSha256)
    {
        if (string.IsNullOrEmpty(expectedSha256))
        {
            _logger.LogWarning("No checksum provided for {File}, skipping verification", filePath);
            return true;
        }
        
        _logger.LogInformation("Verifying checksum for {File}", filePath);
        
        using var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read);
        using var sha256 = SHA256.Create();
        
        byte[] hashBytes = await sha256.ComputeHashAsync(fs);
        string computedHash = BitConverter.ToString(hashBytes).Replace("-", "").ToLowerInvariant();
        
        bool isValid = computedHash.Equals(expectedSha256.ToLowerInvariant());
        
        if (!isValid)
        {
            _logger.LogWarning("Checksum verification failed for {File}. Expected: {Expected}, Actual: {Actual}", 
                filePath, expectedSha256, computedHash);
        }
        
        return isValid;
    }
}

public class DependencyManifest
{
    public List<DependencyComponent> Components { get; set; } = new List<DependencyComponent>();
}

public class DependencyComponent
{
    public string Name { get; set; } = "";
    public string Version { get; set; } = "";
    public bool IsRequired { get; set; }
    public List<DependencyFile> Files { get; set; } = new List<DependencyFile>();
    public string? PostInstallProbe { get; set; }
}

public class DependencyFile
{
    public string Filename { get; set; } = "";
    public string Url { get; set; } = "";
    public string Sha256 { get; set; } = "";
    public string ExtractPath { get; set; } = "";
    public long SizeBytes { get; set; }
    public string? InstallPath { get; set; }
}

public record DownloadProgress(
    long BytesDownloaded, 
    long TotalBytes, 
    float PercentComplete,
    string Url);

public class ComponentStatus
{
    public string Name { get; set; } = "";
    public bool IsInstalled { get; set; }
    public bool NeedsRepair { get; set; }
    public string? ErrorMessage { get; set; }
}

public class ManualInstallInstructions
{
    public string ComponentName { get; set; } = "";
    public string Version { get; set; } = "";
    public string TargetDirectory { get; set; } = "";
    public List<ManualInstallFile> Files { get; set; } = new();
    public string Instructions { get; set; } = "";
}

public class ManualInstallFile
{
    public string Filename { get; set; } = "";
    public string Url { get; set; } = "";
    public string Sha256 { get; set; } = "";
    public long SizeBytes { get; set; }
    public string InstallPath { get; set; } = "";
}

public class ChecksumVerificationResult
{
    public string ComponentName { get; set; } = "";
    public bool IsValid { get; set; }
    public List<FileVerificationResult> Files { get; set; } = new();
}

public class FileVerificationResult
{
    public string Filename { get; set; } = "";
    public string FilePath { get; set; } = "";
    public string ExpectedSha256 { get; set; } = "";
    public string? ActualSha256 { get; set; }
    public bool IsValid { get; set; }
    public string? ErrorMessage { get; set; }
}

public class ProbeResult
{
    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }
}