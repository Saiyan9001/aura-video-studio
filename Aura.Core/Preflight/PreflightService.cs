using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using Aura.Core.Configuration;
using Microsoft.Extensions.Logging;

namespace Aura.Core.Preflight;

/// <summary>
/// Service for running preflight configuration validation checks
/// </summary>
public class PreflightService
{
    private readonly ILogger<PreflightService> _logger;
    private readonly ProviderSettings _providerSettings;
    private readonly IHttpClientFactory? _httpClientFactory;

    public PreflightService(ILogger<PreflightService> logger, ProviderSettings providerSettings, IHttpClientFactory? httpClientFactory = null)
    {
        _logger = logger;
        _providerSettings = providerSettings;
        _httpClientFactory = httpClientFactory;
    }

    /// <summary>
    /// Run all preflight checks
    /// </summary>
    public async Task<PreflightResult> RunPreflightAsync()
    {
        var correlationId = Guid.NewGuid().ToString();
        _logger.LogInformation("Starting preflight checks. CorrelationId: {CorrelationId}", correlationId);

        var checks = new List<PreflightCheck>();

        // Run all checks in parallel for efficiency
        var checkTasks = new[]
        {
            CheckProviderSelectionAsync(),
            CheckApiKeysAsync(),
            CheckOllamaAsync(),
            CheckStableDiffusionAsync(),
            CheckFfmpegAsync(),
            CheckFfmpegHwaccelsAsync(),
            CheckNvencAsync(),
            CheckDiskSpaceAsync(),
            CheckOfflineConsistencyAsync()
        };

        var results = await Task.WhenAll(checkTasks);
        checks.AddRange(results);

        var allOk = checks.All(c => c.Ok);

        _logger.LogInformation("Preflight checks completed. CorrelationId: {CorrelationId}, Status: {Status}, Total: {Total}, Passed: {Passed}, Failed: {Failed}",
            correlationId, allOk ? "PASS" : "FAIL", checks.Count, checks.Count(c => c.Ok), checks.Count(c => !c.Ok));

        // Log each failed check
        foreach (var check in checks.Where(c => !c.Ok))
        {
            _logger.LogWarning("Preflight check FAILED - {CheckName}: {Message}. CorrelationId: {CorrelationId}",
                check.Name, check.Message, correlationId);
        }

        return new PreflightResult
        {
            Ok = allOk,
            Checks = checks,
            CorrelationId = correlationId
        };
    }

    private async Task<PreflightCheck> CheckProviderSelectionAsync()
    {
        var check = new PreflightCheck { Name = "Provider Selection Coherence" };
        
        try
        {
            // Load profile and per-stage provider settings
            var settingsPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Aura", "settings.json");
            
            if (!File.Exists(settingsPath))
            {
                check.Ok = true;
                check.Message = "No profile selected (using defaults)";
                return check;
            }

            var json = await File.ReadAllTextAsync(settingsPath);
            var settings = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(json);
            
            if (settings == null || !settings.ContainsKey("profile"))
            {
                check.Ok = true;
                check.Message = "No profile selected (using defaults)";
                return check;
            }

            var profile = settings["profile"].GetString();
            
            // Check coherence: Pro-Max requires API keys, Free-Only shouldn't use pro providers
            if (profile == "Pro-Max")
            {
                var hasKeys = await HasAnyApiKeysAsync();
                if (!hasKeys)
                {
                    check.Ok = false;
                    check.Message = "Pro-Max profile selected but no API keys configured";
                    check.FixHint = "Add API keys in Settings > API Keys or switch to Free-Only profile";
                    check.Link = "#/settings";
                    return check;
                }
            }

            check.Ok = true;
            check.Message = $"Profile '{profile}' is properly configured";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking provider selection coherence");
            check.Ok = false;
            check.Message = $"Error checking provider selection: {ex.Message}";
        }

        return check;
    }

    private async Task<PreflightCheck> CheckApiKeysAsync()
    {
        var check = new PreflightCheck { Name = "API Keys Validation" };
        
        try
        {
            var keysPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Aura", "apikeys.json");
            
            if (!File.Exists(keysPath))
            {
                check.Ok = true;
                check.Message = "No API keys configured (free providers will be used)";
                return check;
            }

            var json = await File.ReadAllTextAsync(keysPath);
            var keys = JsonSerializer.Deserialize<Dictionary<string, string>>(json);
            
            if (keys == null || keys.All(k => string.IsNullOrWhiteSpace(k.Value)))
            {
                check.Ok = true;
                check.Message = "No API keys configured (free providers will be used)";
                return check;
            }

            // Validate key format (basic check - they should be non-empty and have reasonable length)
            var validKeys = keys.Where(k => !string.IsNullOrWhiteSpace(k.Value) && k.Value.Length >= 8).ToList();
            
            if (validKeys.Any())
            {
                check.Ok = true;
                check.Message = $"{validKeys.Count} API key(s) configured: {string.Join(", ", validKeys.Select(k => k.Key))}";
            }
            else
            {
                check.Ok = true;
                check.Message = "API keys present but appear invalid (too short)";
                check.FixHint = "Review and update your API keys in Settings";
                check.Link = "#/settings";
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking API keys");
            check.Ok = false;
            check.Message = $"Error checking API keys: {ex.Message}";
        }

        return check;
    }

    private async Task<PreflightCheck> CheckOllamaAsync()
    {
        var check = new PreflightCheck { Name = "Ollama Availability" };
        
        try
        {
            var ollamaUrl = _providerSettings.GetOllamaUrl();
            var httpClient = _httpClientFactory?.CreateClient() ?? new HttpClient();
            httpClient.Timeout = TimeSpan.FromSeconds(3);

            var response = await httpClient.GetAsync($"{ollamaUrl}/api/tags");
            
            if (response.IsSuccessStatusCode)
            {
                check.Ok = true;
                check.Message = $"Ollama is reachable at {ollamaUrl}";
            }
            else
            {
                check.Ok = false;
                check.Message = $"Ollama returned status {response.StatusCode} at {ollamaUrl}";
                check.FixHint = "Start Ollama or configure the correct URL in Settings > Local Providers";
                check.Link = "https://ollama.ai/download";
            }
        }
        catch (Exception ex)
        {
            check.Ok = false;
            check.Message = $"Cannot reach Ollama: {ex.Message}";
            check.FixHint = "Install and start Ollama, or configure the correct URL";
            check.Link = "https://ollama.ai/download";
        }

        return check;
    }

    private async Task<PreflightCheck> CheckStableDiffusionAsync()
    {
        var check = new PreflightCheck { Name = "Stable Diffusion Availability" };
        
        try
        {
            var sdUrl = _providerSettings.GetStableDiffusionUrl();
            var httpClient = _httpClientFactory?.CreateClient() ?? new HttpClient();
            httpClient.Timeout = TimeSpan.FromSeconds(3);

            var response = await httpClient.GetAsync($"{sdUrl}/sdapi/v1/sd-models");
            
            if (response.IsSuccessStatusCode)
            {
                check.Ok = true;
                check.Message = $"Stable Diffusion WebUI is reachable at {sdUrl}";
            }
            else
            {
                check.Ok = false;
                check.Message = $"Stable Diffusion WebUI returned status {response.StatusCode} at {sdUrl}";
                check.FixHint = "Start Stable Diffusion WebUI with --api flag or configure the correct URL";
                check.Link = "https://github.com/AUTOMATIC1111/stable-diffusion-webui";
            }
        }
        catch (Exception ex)
        {
            check.Ok = false;
            check.Message = $"Cannot reach Stable Diffusion WebUI: {ex.Message}";
            check.FixHint = "Install and start Stable Diffusion WebUI with --api flag";
            check.Link = "https://github.com/AUTOMATIC1111/stable-diffusion-webui";
        }

        return check;
    }

    private async Task<PreflightCheck> CheckFfmpegAsync()
    {
        var check = new PreflightCheck { Name = "FFmpeg Availability" };
        
        try
        {
            var ffmpegPath = _providerSettings.GetFfmpegPath();
            
            var processInfo = new ProcessStartInfo
            {
                FileName = ffmpegPath,
                Arguments = "-version",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process = Process.Start(processInfo);
            if (process != null)
            {
                await process.WaitForExitAsync();
                if (process.ExitCode == 0)
                {
                    var output = await process.StandardOutput.ReadToEndAsync();
                    var versionLine = output.Split('\n').FirstOrDefault(l => l.Contains("ffmpeg version"));
                    check.Ok = true;
                    check.Message = versionLine ?? "FFmpeg is available";
                }
                else
                {
                    check.Ok = false;
                    check.Message = "FFmpeg returned non-zero exit code";
                    check.FixHint = "Install FFmpeg or configure the correct path in Settings > Local Providers";
                    check.Link = "https://ffmpeg.org/download.html";
                }
            }
            else
            {
                check.Ok = false;
                check.Message = "Failed to start FFmpeg process";
                check.FixHint = "Install FFmpeg or configure the correct path";
                check.Link = "https://ffmpeg.org/download.html";
            }
        }
        catch (Exception ex)
        {
            check.Ok = false;
            check.Message = $"FFmpeg not found: {ex.Message}";
            check.FixHint = "Install FFmpeg or configure the correct path in Settings > Local Providers";
            check.Link = "https://ffmpeg.org/download.html";
        }

        return check;
    }

    private async Task<PreflightCheck> CheckFfmpegHwaccelsAsync()
    {
        var check = new PreflightCheck { Name = "FFmpeg Hardware Acceleration" };
        
        try
        {
            var ffmpegPath = _providerSettings.GetFfmpegPath();
            
            var processInfo = new ProcessStartInfo
            {
                FileName = ffmpegPath,
                Arguments = "-hwaccels",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process = Process.Start(processInfo);
            if (process != null)
            {
                await process.WaitForExitAsync();
                if (process.ExitCode == 0)
                {
                    var output = await process.StandardOutput.ReadToEndAsync();
                    var hwaccels = output.Split('\n')
                        .Skip(1) // Skip "Hardware acceleration methods:" header
                        .Where(l => !string.IsNullOrWhiteSpace(l))
                        .Select(l => l.Trim())
                        .ToList();
                    
                    if (hwaccels.Any())
                    {
                        check.Ok = true;
                        check.Message = $"Hardware acceleration available: {string.Join(", ", hwaccels)}";
                    }
                    else
                    {
                        check.Ok = true;
                        check.Message = "No hardware acceleration detected (software encoding will be used)";
                    }
                }
                else
                {
                    check.Ok = false;
                    check.Message = "Failed to query hardware acceleration methods";
                }
            }
            else
            {
                check.Ok = false;
                check.Message = "Failed to start FFmpeg process";
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking FFmpeg hwaccels");
            check.Ok = false;
            check.Message = $"Error checking hardware acceleration: {ex.Message}";
        }

        return check;
    }

    private async Task<PreflightCheck> CheckNvencAsync()
    {
        var check = new PreflightCheck { Name = "NVENC Hardware Encoding" };
        
        try
        {
            var ffmpegPath = _providerSettings.GetFfmpegPath();
            
            var processInfo = new ProcessStartInfo
            {
                FileName = ffmpegPath,
                Arguments = "-hide_banner -encoders",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process = Process.Start(processInfo);
            if (process != null)
            {
                await process.WaitForExitAsync();
                var output = await process.StandardOutput.ReadToEndAsync();
                
                bool hasNvenc = output.Contains("h264_nvenc") || output.Contains("hevc_nvenc");
                
                if (hasNvenc)
                {
                    check.Ok = true;
                    check.Message = "NVENC hardware encoding is available";
                }
                else
                {
                    check.Ok = true; // Not a failure, just information
                    check.Message = "NVENC not available (software encoding will be used)";
                    check.FixHint = "Install NVIDIA drivers and FFmpeg with NVENC support for faster encoding";
                    check.Link = "https://www.nvidia.com/drivers";
                }
            }
            else
            {
                check.Ok = false;
                check.Message = "Failed to query encoders";
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking NVENC");
            check.Ok = false;
            check.Message = $"Error checking NVENC: {ex.Message}";
        }

        return check;
    }

    private async Task<PreflightCheck> CheckDiskSpaceAsync()
    {
        var check = new PreflightCheck { Name = "Disk Space" };
        
        try
        {
            var outputDirectory = _providerSettings.GetOutputDirectory();
            
            // Create directory if it doesn't exist
            Directory.CreateDirectory(outputDirectory);
            
            var driveInfo = new DriveInfo(Path.GetPathRoot(outputDirectory)!);
            var availableGB = driveInfo.AvailableFreeSpace / (1024.0 * 1024.0 * 1024.0);
            
            if (availableGB >= 10)
            {
                check.Ok = true;
                check.Message = $"{availableGB:F2} GB available on {driveInfo.Name}";
            }
            else
            {
                check.Ok = false;
                check.Message = $"Only {availableGB:F2} GB available on {driveInfo.Name} (minimum 10 GB required)";
                check.FixHint = "Free up disk space or change output directory in Settings > Local Providers";
                check.Link = "#/settings";
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking disk space");
            check.Ok = false;
            check.Message = $"Error checking disk space: {ex.Message}";
        }

        return await Task.FromResult(check);
    }

    private async Task<PreflightCheck> CheckOfflineConsistencyAsync()
    {
        var check = new PreflightCheck { Name = "Offline Mode Consistency" };
        
        try
        {
            var settingsPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Aura", "settings.json");
            
            if (!File.Exists(settingsPath))
            {
                check.Ok = true;
                check.Message = "Offline mode not configured (online providers allowed)";
                return check;
            }

            var json = await File.ReadAllTextAsync(settingsPath);
            var settings = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(json);
            
            if (settings == null || !settings.ContainsKey("offlineOnly"))
            {
                check.Ok = true;
                check.Message = "Offline mode not enabled";
                return check;
            }

            var offlineOnly = settings["offlineOnly"].GetBoolean();
            
            if (offlineOnly)
            {
                // Check if any API keys are configured - this would be inconsistent
                var hasKeys = await HasAnyApiKeysAsync();
                if (hasKeys)
                {
                    check.Ok = false;
                    check.Message = "Offline mode enabled but API keys are configured (they won't be used)";
                    check.FixHint = "Either disable offline mode or remove API keys to avoid confusion";
                    check.Link = "#/settings";
                }
                else
                {
                    check.Ok = true;
                    check.Message = "Offline mode enabled (only local providers will be used)";
                }
            }
            else
            {
                check.Ok = true;
                check.Message = "Offline mode not enabled";
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking offline mode consistency");
            check.Ok = false;
            check.Message = $"Error checking offline mode: {ex.Message}";
        }

        return check;
    }

    private async Task<bool> HasAnyApiKeysAsync()
    {
        try
        {
            var keysPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Aura", "apikeys.json");
            
            if (!File.Exists(keysPath))
                return false;

            var json = await File.ReadAllTextAsync(keysPath);
            var keys = JsonSerializer.Deserialize<Dictionary<string, string>>(json);
            
            return keys != null && keys.Any(k => !string.IsNullOrWhiteSpace(k.Value) && k.Value.Length >= 8);
        }
        catch
        {
            return false;
        }
    }
}
