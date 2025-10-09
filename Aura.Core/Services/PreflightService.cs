using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using Aura.Core.Configuration;
using Aura.Core.Hardware;
using Aura.Core.Models;
using Microsoft.Extensions.Logging;

namespace Aura.Core.Services;

/// <summary>
/// Service for running preflight checks before video generation
/// Validates configuration, provider availability, and system requirements
/// </summary>
public class PreflightService
{
    private readonly ILogger<PreflightService> _logger;
    private readonly HardwareDetector _hardwareDetector;
    private readonly ProviderSettings _providerSettings;
    private readonly IHttpClientFactory? _httpClientFactory;

    public PreflightService(
        ILogger<PreflightService> logger,
        HardwareDetector hardwareDetector,
        ProviderSettings providerSettings,
        IHttpClientFactory? httpClientFactory = null)
    {
        _logger = logger;
        _hardwareDetector = hardwareDetector;
        _providerSettings = providerSettings;
        _httpClientFactory = httpClientFactory;
    }

    /// <summary>
    /// Run all preflight checks
    /// </summary>
    public async Task<PreflightResult> RunPreflightChecksAsync()
    {
        var correlationId = Guid.NewGuid().ToString();
        _logger.LogInformation("Starting preflight checks with correlationId: {CorrelationId}", correlationId);

        var checks = new List<PreflightCheckResult>();

        // Run all checks
        checks.Add(await CheckProviderSelectionCoherenceAsync());
        checks.Add(await CheckApiKeysAsync());
        checks.Add(await CheckOllamaReachabilityAsync());
        checks.Add(await CheckStableDiffusionReachabilityAsync());
        checks.Add(await CheckFfmpegPresenceAsync());
        checks.Add(await CheckNvencSupportAsync());
        checks.Add(await CheckDiskSpaceAsync());
        checks.Add(await CheckOfflineConsistencyAsync());

        var allOk = checks.All(c => c.Ok || c.Severity == "warning");
        var canAutoSwitchToFree = checks.Any(c => !c.Ok && c.Severity == "error" && 
            (c.Name.Contains("Ollama") || c.Name.Contains("Stable Diffusion") || c.Name.Contains("API")));

        var result = new PreflightResult
        {
            Ok = allOk,
            CorrelationId = correlationId,
            Timestamp = DateTime.UtcNow,
            Checks = checks,
            CanAutoSwitchToFree = canAutoSwitchToFree
        };

        _logger.LogInformation(
            "Preflight checks completed. CorrelationId: {CorrelationId}, Result: {Result}, Failed: {Failed}",
            correlationId,
            allOk ? "PASS" : "FAIL",
            checks.Count(c => !c.Ok));

        foreach (var check in checks.Where(c => !c.Ok))
        {
            _logger.LogWarning(
                "Preflight check failed - CorrelationId: {CorrelationId}, Check: {CheckName}, Message: {Message}",
                correlationId,
                check.Name,
                check.Message);
        }

        return result;
    }

    private async Task<PreflightCheckResult> CheckProviderSelectionCoherenceAsync()
    {
        try
        {
            // Load provider profile configuration
            var configPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "Aura",
                "provider-profile.json");

            if (!File.Exists(configPath))
            {
                return new PreflightCheckResult
                {
                    Name = "Provider Selection Coherence",
                    Ok = true,
                    Message = "Using default provider profile (Free-Only)",
                    Severity = "info"
                };
            }

            var json = await File.ReadAllTextAsync(configPath);
            var config = JsonSerializer.Deserialize<ProviderMixingConfig>(json);

            if (config == null)
            {
                return new PreflightCheckResult
                {
                    Name = "Provider Selection Coherence",
                    Ok = false,
                    Message = "Invalid provider profile configuration",
                    FixHint = "Reset to default Free-Only profile",
                    Link = "/settings#providers"
                };
            }

            // Check if active profile exists in saved profiles
            var activeProfile = config.SavedProfiles.FirstOrDefault(p => p.Name == config.ActiveProfile);
            if (activeProfile == null)
            {
                return new PreflightCheckResult
                {
                    Name = "Provider Selection Coherence",
                    Ok = false,
                    Message = $"Active profile '{config.ActiveProfile}' not found in saved profiles",
                    FixHint = "Select a valid provider profile in Settings",
                    Link = "/settings#providers"
                };
            }

            // Check if profile has required stages
            var requiredStages = new[] { "Script", "TTS", "Visuals" };
            var missingStages = requiredStages.Where(s => !activeProfile.Stages.ContainsKey(s)).ToList();
            
            if (missingStages.Any())
            {
                return new PreflightCheckResult
                {
                    Name = "Provider Selection Coherence",
                    Ok = false,
                    Message = $"Profile missing required stages: {string.Join(", ", missingStages)}",
                    FixHint = "Use a complete profile or reset to defaults",
                    Link = "/settings#providers"
                };
            }

            return new PreflightCheckResult
            {
                Name = "Provider Selection Coherence",
                Ok = true,
                Message = $"Provider profile '{config.ActiveProfile}' is valid"
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking provider selection coherence");
            return new PreflightCheckResult
            {
                Name = "Provider Selection Coherence",
                Ok = false,
                Message = $"Error validating provider configuration: {ex.Message}",
                FixHint = "Check provider configuration in Settings",
                Link = "/settings#providers"
            };
        }
    }

    private async Task<PreflightCheckResult> CheckApiKeysAsync()
    {
        try
        {
            var keysPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "Aura",
                "apikeys.json");

            if (!File.Exists(keysPath))
            {
                return new PreflightCheckResult
                {
                    Name = "API Keys",
                    Ok = true,
                    Message = "No API keys configured (using free providers)",
                    Severity = "info"
                };
            }

            var json = await File.ReadAllTextAsync(keysPath);
            var keys = JsonSerializer.Deserialize<Dictionary<string, string>>(json);

            if (keys == null || keys.All(k => string.IsNullOrWhiteSpace(k.Value)))
            {
                return new PreflightCheckResult
                {
                    Name = "API Keys",
                    Ok = true,
                    Message = "No API keys configured (using free providers)",
                    Severity = "info"
                };
            }

            // Basic validation - check if keys look valid (not empty, reasonable length)
            var invalidKeys = keys.Where(k => 
                !string.IsNullOrWhiteSpace(k.Value) && 
                (k.Value.Length < 8 || k.Value.Length > 200))
                .Select(k => k.Key)
                .ToList();

            if (invalidKeys.Any())
            {
                return new PreflightCheckResult
                {
                    Name = "API Keys",
                    Ok = false,
                    Message = $"Invalid API keys detected: {string.Join(", ", invalidKeys)}",
                    FixHint = "Check API keys in Settings - they should be between 8 and 200 characters",
                    Link = "/settings#api-keys",
                    Severity = "warning"
                };
            }

            var configuredKeys = keys.Where(k => !string.IsNullOrWhiteSpace(k.Value)).Select(k => k.Key).ToList();
            return new PreflightCheckResult
            {
                Name = "API Keys",
                Ok = true,
                Message = $"Valid API keys configured: {string.Join(", ", configuredKeys)}"
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking API keys");
            return new PreflightCheckResult
            {
                Name = "API Keys",
                Ok = false,
                Message = $"Error validating API keys: {ex.Message}",
                FixHint = "Check API keys configuration in Settings",
                Link = "/settings#api-keys",
                Severity = "warning"
            };
        }
    }

    private async Task<PreflightCheckResult> CheckOllamaReachabilityAsync()
    {
        try
        {
            var ollamaUrl = _providerSettings.GetOllamaUrl();
            
            if (_httpClientFactory == null)
            {
                return new PreflightCheckResult
                {
                    Name = "Ollama Reachability",
                    Ok = true,
                    Message = "Ollama check skipped (HTTP client not available)",
                    Severity = "warning"
                };
            }

            var httpClient = _httpClientFactory.CreateClient();
            httpClient.Timeout = TimeSpan.FromSeconds(5);

            var response = await httpClient.GetAsync($"{ollamaUrl}/api/tags");
            
            if (response.IsSuccessStatusCode)
            {
                return new PreflightCheckResult
                {
                    Name = "Ollama Reachability",
                    Ok = true,
                    Message = "Ollama is running and accessible"
                };
            }

            return new PreflightCheckResult
            {
                Name = "Ollama Reachability",
                Ok = false,
                Message = $"Ollama returned status {response.StatusCode}",
                FixHint = "Start Ollama or switch to Free providers",
                Link = "/downloads",
                Severity = "warning"
            };
        }
        catch (TaskCanceledException)
        {
            return new PreflightCheckResult
            {
                Name = "Ollama Reachability",
                Ok = false,
                Message = "Ollama connection timeout - service may not be running",
                FixHint = "Start Ollama or switch to Free providers",
                Link = "/downloads",
                Severity = "warning"
            };
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error checking Ollama reachability");
            return new PreflightCheckResult
            {
                Name = "Ollama Reachability",
                Ok = false,
                Message = "Ollama is not accessible",
                FixHint = "Install and start Ollama or switch to Free providers",
                Link = "/downloads",
                Severity = "warning"
            };
        }
    }

    private async Task<PreflightCheckResult> CheckStableDiffusionReachabilityAsync()
    {
        try
        {
            var sdUrl = _providerSettings.GetStableDiffusionUrl();
            
            if (_httpClientFactory == null)
            {
                return new PreflightCheckResult
                {
                    Name = "Stable Diffusion Reachability",
                    Ok = true,
                    Message = "SD check skipped (HTTP client not available)",
                    Severity = "warning"
                };
            }

            var httpClient = _httpClientFactory.CreateClient();
            httpClient.Timeout = TimeSpan.FromSeconds(5);

            var response = await httpClient.GetAsync($"{sdUrl}/internal/ping");
            
            if (response.IsSuccessStatusCode)
            {
                return new PreflightCheckResult
                {
                    Name = "Stable Diffusion Reachability",
                    Ok = true,
                    Message = "Stable Diffusion WebUI is running and accessible"
                };
            }

            return new PreflightCheckResult
            {
                Name = "Stable Diffusion Reachability",
                Ok = false,
                Message = $"Stable Diffusion returned status {response.StatusCode}",
                FixHint = "Start Stable Diffusion WebUI or switch to Free providers",
                Link = "/downloads",
                Severity = "warning"
            };
        }
        catch (TaskCanceledException)
        {
            return new PreflightCheckResult
            {
                Name = "Stable Diffusion Reachability",
                Ok = false,
                Message = "Stable Diffusion connection timeout - service may not be running",
                FixHint = "Start Stable Diffusion WebUI or switch to Free providers",
                Link = "/downloads",
                Severity = "warning"
            };
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error checking Stable Diffusion reachability");
            return new PreflightCheckResult
            {
                Name = "Stable Diffusion Reachability",
                Ok = false,
                Message = "Stable Diffusion WebUI is not accessible",
                FixHint = "Install and start SD WebUI or switch to Free providers",
                Link = "/downloads",
                Severity = "warning"
            };
        }
    }

    private async Task<PreflightCheckResult> CheckFfmpegPresenceAsync()
    {
        try
        {
            var ffmpegPath = _providerSettings.GetFfmpegPath();
            var ffmpegExe = string.IsNullOrEmpty(ffmpegPath) ? "ffmpeg" : ffmpegPath;

            var startInfo = new ProcessStartInfo
            {
                FileName = ffmpegExe,
                Arguments = "-version",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process = Process.Start(startInfo);
            if (process == null)
            {
                return new PreflightCheckResult
                {
                    Name = "FFmpeg Presence",
                    Ok = false,
                    Message = "Failed to start FFmpeg process",
                    FixHint = "Install FFmpeg or configure path in Settings",
                    Link = "/downloads"
                };
            }

            await process.WaitForExitAsync();
            var output = await process.StandardOutput.ReadToEndAsync();

            if (process.ExitCode == 0 && output.Contains("ffmpeg version"))
            {
                // Extract version info
                var versionLine = output.Split('\n')[0];
                return new PreflightCheckResult
                {
                    Name = "FFmpeg Presence",
                    Ok = true,
                    Message = $"FFmpeg is available: {versionLine.Substring(0, Math.Min(80, versionLine.Length))}"
                };
            }

            return new PreflightCheckResult
            {
                Name = "FFmpeg Presence",
                Ok = false,
                Message = "FFmpeg check failed",
                FixHint = "Install FFmpeg or configure path in Settings",
                Link = "/downloads"
            };
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error checking FFmpeg presence");
            return new PreflightCheckResult
            {
                Name = "FFmpeg Presence",
                Ok = false,
                Message = "FFmpeg not found or not accessible",
                FixHint = "Install FFmpeg or configure path in Settings",
                Link = "/downloads"
            };
        }
    }

    private async Task<PreflightCheckResult> CheckNvencSupportAsync()
    {
        try
        {
            var ffmpegPath = _providerSettings.GetFfmpegPath();
            var ffmpegExe = string.IsNullOrEmpty(ffmpegPath) ? "ffmpeg" : ffmpegPath;

            // Check hardware accelerators
            var startInfo = new ProcessStartInfo
            {
                FileName = ffmpegExe,
                Arguments = "-hwaccels",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process = Process.Start(startInfo);
            if (process == null)
            {
                return new PreflightCheckResult
                {
                    Name = "NVENC Support",
                    Ok = true,
                    Message = "NVENC check skipped (FFmpeg not accessible)",
                    Severity = "warning"
                };
            }

            await process.WaitForExitAsync();
            var output = await process.StandardOutput.ReadToEndAsync();

            var hasNvenc = output.Contains("cuda") || output.Contains("nvenc");

            if (hasNvenc)
            {
                // TODO: Could add a tiny 2-second probe here to verify NVENC actually works
                return new PreflightCheckResult
                {
                    Name = "NVENC Support",
                    Ok = true,
                    Message = "NVENC hardware acceleration is available"
                };
            }

            return new PreflightCheckResult
            {
                Name = "NVENC Support",
                Ok = true,
                Message = "NVENC not available - will use software encoding",
                Severity = "info"
            };
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error checking NVENC support");
            return new PreflightCheckResult
            {
                Name = "NVENC Support",
                Ok = true,
                Message = "NVENC check failed - will use software encoding",
                Severity = "info"
            };
        }
    }

    private async Task<PreflightCheckResult> CheckDiskSpaceAsync()
    {
        try
        {
            var outputDir = _providerSettings.GetOutputDirectory();
            var dirToCheck = string.IsNullOrEmpty(outputDir) 
                ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "AuraVideoStudio")
                : outputDir;

            // Get drive info
            var driveInfo = new DriveInfo(Path.GetPathRoot(dirToCheck) ?? "C:\\");
            var availableGB = driveInfo.AvailableFreeSpace / (1024.0 * 1024.0 * 1024.0);

            if (availableGB >= 10)
            {
                return new PreflightCheckResult
                {
                    Name = "Disk Space",
                    Ok = true,
                    Message = $"Sufficient disk space available: {availableGB:F1} GB free"
                };
            }

            return new PreflightCheckResult
            {
                Name = "Disk Space",
                Ok = false,
                Message = $"Low disk space: only {availableGB:F1} GB available (minimum 10 GB required)",
                FixHint = "Free up disk space or change output directory",
                Link = "/settings#paths"
            };
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error checking disk space");
            return new PreflightCheckResult
            {
                Name = "Disk Space",
                Ok = true,
                Message = "Disk space check failed - proceeding with caution",
                Severity = "warning"
            };
        }
    }

    private async Task<PreflightCheckResult> CheckOfflineConsistencyAsync()
    {
        try
        {
            // Check if offline mode is enabled
            var settingsPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "Aura",
                "settings.json");

            bool offlineMode = false;
            if (File.Exists(settingsPath))
            {
                var json = await File.ReadAllTextAsync(settingsPath);
                var settings = JsonSerializer.Deserialize<Dictionary<string, object>>(json);
                if (settings != null && settings.TryGetValue("offlineMode", out var value))
                {
                    if (value is JsonElement element && element.ValueKind == JsonValueKind.True)
                    {
                        offlineMode = true;
                    }
                }
            }

            if (!offlineMode)
            {
                return new PreflightCheckResult
                {
                    Name = "Offline Mode Consistency",
                    Ok = true,
                    Message = "Online mode - all providers available"
                };
            }

            // If offline mode is enabled, check that profile uses only offline providers
            var configPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "Aura",
                "provider-profile.json");

            if (!File.Exists(configPath))
            {
                // Default profile is Free-Only which is offline-compatible
                return new PreflightCheckResult
                {
                    Name = "Offline Mode Consistency",
                    Ok = true,
                    Message = "Offline mode enabled with default profile"
                };
            }

            var profileJson = await File.ReadAllTextAsync(configPath);
            var config = JsonSerializer.Deserialize<ProviderMixingConfig>(profileJson);
            var activeProfile = config?.SavedProfiles.FirstOrDefault(p => p.Name == config.ActiveProfile);

            if (activeProfile == null)
            {
                return new PreflightCheckResult
                {
                    Name = "Offline Mode Consistency",
                    Ok = false,
                    Message = "Offline mode enabled but no valid profile configured",
                    FixHint = "Configure an offline-compatible profile or disable offline mode",
                    Link = "/settings#system"
                };
            }

            // Check if profile uses only offline providers
            var onlineProviders = activeProfile.Stages.Values
                .Where(v => v.Contains("Pro") && !v.Contains("IfAvailable"))
                .ToList();

            if (onlineProviders.Any())
            {
                return new PreflightCheckResult
                {
                    Name = "Offline Mode Consistency",
                    Ok = false,
                    Message = "Offline mode enabled but profile requires online providers",
                    FixHint = "Switch to Free-Only profile or disable offline mode",
                    Link = "/settings#providers"
                };
            }

            return new PreflightCheckResult
            {
                Name = "Offline Mode Consistency",
                Ok = true,
                Message = "Offline mode configured correctly"
            };
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error checking offline consistency");
            return new PreflightCheckResult
            {
                Name = "Offline Mode Consistency",
                Ok = true,
                Message = "Offline mode check completed with warnings",
                Severity = "warning"
            };
        }
    }
}
