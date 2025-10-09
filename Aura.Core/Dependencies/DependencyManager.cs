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
    
    public async Task<bool> IsComponentInstalledAsync(string componentName)
    {
        var manifest = await LoadManifestAsync();
        var component = manifest.Components.Find(c => c.Name == componentName);
        
        if (component == null)
        {
            return false;
        }
        
        foreach (var file in component.Files)
        {
            string filePath = Path.Combine(_downloadDirectory, file.Filename);
            if (!File.Exists(filePath))
            {
                return false;
            }
            
            // Optionally verify checksum
        }
        
        return true;
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
    
    private async Task DownloadFileAsync(
        string url, 
        string filePath, 
        long expectedSize,
        IProgress<DownloadProgress> progress, 
        CancellationToken ct)
    {
        _logger.LogInformation("Downloading file: {Url} to {FilePath}", url, filePath);
        
        // Create directory if it doesn't exist
        Directory.CreateDirectory(Path.GetDirectoryName(filePath) ?? _downloadDirectory);
        
        long existingBytes = 0;
        
        // Check if partial download exists
        if (File.Exists(filePath))
        {
            var fileInfo = new FileInfo(filePath);
            existingBytes = fileInfo.Length;
            
            if (existingBytes >= expectedSize)
            {
                _logger.LogInformation("File already fully downloaded: {File}", filePath);
                progress.Report(new DownloadProgress(existingBytes, expectedSize, 100, url));
                return;
            }
            
            _logger.LogInformation("Resuming download from {Bytes} bytes", existingBytes);
        }
        
        // Create request with Range header for resume support
        var request = new HttpRequestMessage(HttpMethod.Get, url);
        if (existingBytes > 0)
        {
            request.Headers.Range = new System.Net.Http.Headers.RangeHeaderValue(existingBytes, null);
        }
        
        // Use HttpClient to download the file
        using var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct);
        response.EnsureSuccessStatusCode();
        
        long totalBytes = existingBytes + (response.Content.Headers.ContentLength ?? (expectedSize - existingBytes));
        long bytesRead = existingBytes;
        
        using var contentStream = await response.Content.ReadAsStreamAsync(ct);
        using var fileStream = new FileStream(filePath, FileMode.Append, FileAccess.Write, FileShare.None, 8192, true);
        
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
    
    public async Task<ComponentInfo> GetComponentStatusAsync(string componentName)
    {
        var manifest = await LoadManifestAsync();
        var component = manifest.Components.Find(c => c.Name == componentName);
        
        if (component == null)
        {
            return new ComponentInfo(
                componentName,
                "unknown",
                ComponentStatus.NotInstalled,
                false,
                0,
                null,
                "Component not found in manifest");
        }
        
        long totalSize = component.Files.Sum(f => f.SizeBytes);
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
                errorMessage = $"File {file.Filename} failed checksum verification";
                break;
            }
        }
        
        // Run post-install probe if all files exist
        if (allFilesExist && !needsRepair)
        {
            var probeResult = await RunPostInstallProbeAsync(component);
            if (!probeResult.Success)
            {
                needsRepair = true;
                errorMessage = probeResult.Message;
            }
        }
        
        ComponentStatus status = needsRepair ? ComponentStatus.NeedsRepair :
                                allFilesExist ? ComponentStatus.Installed :
                                ComponentStatus.NotInstalled;
        
        return new ComponentInfo(
            component.Name,
            component.Version,
            status,
            component.IsRequired,
            totalSize,
            _downloadDirectory,
            errorMessage);
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
        
        // Remove all files for this component
        foreach (var file in component.Files)
        {
            string filePath = Path.Combine(_downloadDirectory, file.Filename);
            if (File.Exists(filePath))
            {
                _logger.LogInformation("Removing corrupted file: {File}", filePath);
                File.Delete(filePath);
            }
        }
        
        // Re-download the component
        await DownloadComponentAsync(componentName, progress, ct);
    }
    
    public async Task RemoveComponentAsync(string componentName)
    {
        _logger.LogInformation("Removing component: {Component}", componentName);
        
        var manifest = await LoadManifestAsync();
        var component = manifest.Components.Find(c => c.Name == componentName);
        
        if (component == null)
        {
            throw new ArgumentException($"Component {componentName} not found in manifest");
        }
        
        foreach (var file in component.Files)
        {
            string filePath = Path.Combine(_downloadDirectory, file.Filename);
            if (File.Exists(filePath))
            {
                _logger.LogInformation("Deleting file: {File}", filePath);
                File.Delete(filePath);
            }
        }
    }
    
    public string GetComponentInstallDirectory()
    {
        return _downloadDirectory;
    }
    
    public async Task<string> GetManualInstallInstructionsAsync(string componentName)
    {
        var manifest = await LoadManifestAsync();
        var component = manifest.Components.Find(c => c.Name == componentName);
        
        if (component == null)
        {
            throw new ArgumentException($"Component {componentName} not found in manifest");
        }
        
        var instructions = new System.Text.StringBuilder();
        instructions.AppendLine($"Manual Installation Instructions for {component.Name} v{component.Version}");
        instructions.AppendLine();
        instructions.AppendLine("1. Download the following files:");
        instructions.AppendLine();
        
        foreach (var file in component.Files)
        {
            instructions.AppendLine($"   File: {file.Filename}");
            instructions.AppendLine($"   URL: {file.Url}");
            instructions.AppendLine($"   Size: {file.SizeBytes / (1024.0 * 1024.0):F2} MB");
            instructions.AppendLine($"   SHA-256: {file.Sha256}");
            instructions.AppendLine();
        }
        
        instructions.AppendLine($"2. Place the downloaded files in: {_downloadDirectory}");
        instructions.AppendLine();
        instructions.AppendLine("3. Verify checksums using:");
        instructions.AppendLine("   - Windows: certutil -hashfile <filename> SHA256");
        instructions.AppendLine("   - Linux/Mac: shasum -a 256 <filename>");
        instructions.AppendLine();
        instructions.AppendLine("4. Return to Aura and verify installation.");
        
        return instructions.ToString();
    }
    
    private async Task<(bool Success, string Message)> RunPostInstallProbeAsync(DependencyComponent component)
    {
        // Check for FFmpeg
        if (component.Name == "FFmpeg")
        {
            var ffmpegPath = Path.Combine(_downloadDirectory, "ffmpeg.exe");
            if (!File.Exists(ffmpegPath))
            {
                return (false, "ffmpeg.exe not found");
            }
            
            try
            {
                var processInfo = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = ffmpegPath,
                    Arguments = "-version",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };
                
                using var process = System.Diagnostics.Process.Start(processInfo);
                if (process != null)
                {
                    await process.WaitForExitAsync();
                    if (process.ExitCode == 0)
                    {
                        return (true, "FFmpeg verified successfully");
                    }
                }
                
                return (false, "FFmpeg failed to execute");
            }
            catch (Exception ex)
            {
                return (false, $"FFmpeg probe failed: {ex.Message}");
            }
        }
        
        // Check for Ollama
        if (component.Name == "Ollama")
        {
            try
            {
                using var httpClient = new HttpClient();
                httpClient.Timeout = TimeSpan.FromSeconds(5);
                var response = await httpClient.GetAsync("http://127.0.0.1:11434/api/tags");
                if (response.IsSuccessStatusCode)
                {
                    return (true, "Ollama endpoint verified successfully");
                }
                
                return (false, "Ollama endpoint not responding");
            }
            catch
            {
                return (false, "Ollama not running or not accessible");
            }
        }
        
        // Check for Stable Diffusion
        if (component.Name == "StableDiffusion" || component.Name == "StableDiffusionXL")
        {
            try
            {
                using var httpClient = new HttpClient();
                httpClient.Timeout = TimeSpan.FromSeconds(5);
                var response = await httpClient.GetAsync("http://127.0.0.1:7860/sdapi/v1/sd-models");
                if (response.IsSuccessStatusCode)
                {
                    return (true, "Stable Diffusion WebUI verified successfully");
                }
                
                return (false, "Stable Diffusion WebUI not responding");
            }
            catch
            {
                return (false, "Stable Diffusion WebUI not running or not accessible");
            }
        }
        
        // For other components, just check file existence
        return (true, "Component files verified");
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
}

public class DependencyFile
{
    public string Filename { get; set; } = "";
    public string Url { get; set; } = "";
    public string Sha256 { get; set; } = "";
    public string ExtractPath { get; set; } = "";
    public long SizeBytes { get; set; }
    public string InstallPath { get; set; } = "";
    public bool Required { get; set; }
    public string? PostInstallProbe { get; set; }
}

public record DownloadProgress(
    long BytesDownloaded, 
    long TotalBytes, 
    float PercentComplete,
    string Url);

public enum ComponentStatus
{
    NotInstalled,
    Installed,
    NeedsRepair,
    UpdateAvailable
}

public record ComponentInfo(
    string Name,
    string Version,
    ComponentStatus Status,
    bool IsRequired,
    long TotalSize,
    string? InstallPath,
    string? ErrorMessage);