using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Aura.Core.Configuration;
using Aura.Core.Models;
using Microsoft.Extensions.Logging;

namespace Aura.Core.Services;

/// <summary>
/// Preflight check service that validates configuration before pipeline execution.
/// Checks provider coherence, keys, service availability, hardware capabilities, and disk space.
/// </summary>
public class PreflightService
{
    private readonly ILogger<PreflightService> _logger;
    private readonly ProviderSettings _providerSettings;

    public PreflightService(
        ILogger<PreflightService> logger,
        ProviderSettings providerSettings)
    {
        _logger = logger;
        _providerSettings = providerSettings;
    }

    /// <summary>
    /// Run all preflight checks and return comprehensive results
    /// </summary>
    public async Task<PreflightResult> RunPreflightChecksAsync(CancellationToken ct = default)
    {
        var correlationId = Guid.NewGuid().ToString("N")[..8];
        _logger.LogInformation("Starting preflight checks with correlation ID: {CorrelationId}", correlationId);

        var checks = new List<PreflightCheck>();

        // Run all checks
        checks.Add(await CheckProviderSelectionCoherenceAsync(ct));
        checks.Add(await CheckApiKeysPresenceAsync(ct));
        checks.Add(await CheckOllamaReachabilityAsync(ct));
        checks.Add(await CheckStableDiffusionReachabilityAsync(ct));
        checks.Add(await CheckFfmpegPresenceAsync(ct));
        checks.Add(await CheckFfmpegHwAccelsAsync(ct));
        checks.Add(await CheckNvencCapabilityAsync(ct));
        checks.Add(await CheckDiskSpaceAsync(ct));
        checks.Add(await CheckOfflineConsistencyAsync(ct));

        var allOk = checks.All(c => c.Ok);
        
        _logger.LogInformation(
            "Preflight checks complete. CorrelationId: {CorrelationId}, Result: {Result}, Checks: {CheckSummary}",
            correlationId,
            allOk ? "PASS" : "FAIL",
            string.Join(", ", checks.Select(c => $"{c.Name}:{(c.Ok ? "PASS" : "FAIL")}")));

        return new PreflightResult(
            Ok: allOk,
            CorrelationId: correlationId,
            Checks: checks
        );
    }

    private async Task<PreflightCheck> CheckProviderSelectionCoherenceAsync(CancellationToken ct)
    {
        try
        {
            // Load settings and profile
            var settingsPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "Aura",
                "settings.json");

            if (!File.Exists(settingsPath))
            {
                return new PreflightCheck(
                    Name: "Provider Selection",
                    Ok: true,
                    Message: "Using default provider profile"
                );
            }

            var json = await File.ReadAllTextAsync(settingsPath, ct);
            var settings = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(json);
            
            if (settings == null || !settings.ContainsKey("profile"))
            {
                return new PreflightCheck(
                    Name: "Provider Selection",
                    Ok: true,
                    Message: "Using default provider profile"
                );
            }

            var profileName = settings["profile"].GetString();
            
            // Validate profile coherence (basic check that it's a known profile)
            var validProfiles = new[] { "Free-Only", "Balanced Mix", "Pro-Max" };
            if (string.IsNullOrEmpty(profileName) || !validProfiles.Contains(profileName))
            {
                return new PreflightCheck(
                    Name: "Provider Selection",
                    Ok: false,
                    Message: $"Invalid provider profile: {profileName}",
                    FixHint: "Select a valid provider profile in Settings",
                    Link: "/settings"
                );
            }

            return new PreflightCheck(
                Name: "Provider Selection",
                Ok: true,
                Message: $"Provider profile '{profileName}' is valid"
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking provider selection coherence");
            return new PreflightCheck(
                Name: "Provider Selection",
                Ok: false,
                Message: $"Error checking provider selection: {ex.Message}",
                FixHint: "Check settings file integrity"
            );
        }
    }

    private async Task<PreflightCheck> CheckApiKeysPresenceAsync(CancellationToken ct)
    {
        try
        {
            var keysPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "Aura",
                "apikeys.json");

            if (!File.Exists(keysPath))
            {
                return new PreflightCheck(
                    Name: "API Keys",
                    Ok: true,
                    Message: "No API keys configured (using free providers only)",
                    FixHint: "Configure API keys in Settings to enable premium providers",
                    Link: "/settings"
                );
            }

            var json = await File.ReadAllTextAsync(keysPath, ct);
            var keys = JsonSerializer.Deserialize<Dictionary<string, string>>(json);

            if (keys == null || !keys.Any())
            {
                return new PreflightCheck(
                    Name: "API Keys",
                    Ok: true,
                    Message: "No API keys configured (using free providers only)"
                );
            }

            var configuredKeys = keys.Where(k => !string.IsNullOrWhiteSpace(k.Value)).Select(k => k.Key).ToList();
            
            return new PreflightCheck(
                Name: "API Keys",
                Ok: true,
                Message: $"API keys configured: {string.Join(", ", configuredKeys)}"
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking API keys");
            return new PreflightCheck(
                Name: "API Keys",
                Ok: true,
                Message: "Could not read API keys (proceeding with free providers)",
                FixHint: "Check API keys configuration in Settings"
            );
        }
    }

    private async Task<PreflightCheck> CheckOllamaReachabilityAsync(CancellationToken ct)
    {
        try
        {
            var ollamaUrl = _providerSettings.GetOllamaUrl();
            using var httpClient = new HttpClient();
            httpClient.Timeout = TimeSpan.FromSeconds(5);

            var response = await httpClient.GetAsync($"{ollamaUrl}/api/tags", ct);
            if (response.IsSuccessStatusCode)
            {
                return new PreflightCheck(
                    Name: "Ollama",
                    Ok: true,
                    Message: $"Ollama is reachable at {ollamaUrl}"
                );
            }

            return new PreflightCheck(
                Name: "Ollama",
                Ok: false,
                Message: $"Ollama returned status {response.StatusCode}",
                FixHint: "Install Ollama or switch to rule-based LLM provider",
                Link: "https://ollama.ai/download"
            );
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Ollama not reachable");
            return new PreflightCheck(
                Name: "Ollama",
                Ok: false,
                Message: $"Ollama is not reachable: {ex.Message}",
                FixHint: "Install and start Ollama, or switch to rule-based LLM provider",
                Link: "https://ollama.ai/download"
            );
        }
    }

    private async Task<PreflightCheck> CheckStableDiffusionReachabilityAsync(CancellationToken ct)
    {
        try
        {
            var sdUrl = _providerSettings.GetStableDiffusionUrl();
            using var httpClient = new HttpClient();
            httpClient.Timeout = TimeSpan.FromSeconds(5);

            var response = await httpClient.GetAsync($"{sdUrl}/sdapi/v1/sd-models", ct);
            if (response.IsSuccessStatusCode)
            {
                return new PreflightCheck(
                    Name: "Stable Diffusion",
                    Ok: true,
                    Message: $"Stable Diffusion WebUI is reachable at {sdUrl}"
                );
            }

            return new PreflightCheck(
                Name: "Stable Diffusion",
                Ok: false,
                Message: $"Stable Diffusion returned status {response.StatusCode}",
                FixHint: "Install SD WebUI or use stock visuals/slideshow",
                Link: "https://github.com/AUTOMATIC1111/stable-diffusion-webui"
            );
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Stable Diffusion not reachable");
            return new PreflightCheck(
                Name: "Stable Diffusion",
                Ok: false,
                Message: $"Stable Diffusion is not reachable: {ex.Message}",
                FixHint: "Install and start SD WebUI, or use stock visuals/slideshow",
                Link: "https://github.com/AUTOMATIC1111/stable-diffusion-webui"
            );
        }
    }

    private async Task<PreflightCheck> CheckFfmpegPresenceAsync(CancellationToken ct)
    {
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
                await process.WaitForExitAsync(ct);
                if (process.ExitCode == 0)
                {
                    var output = await process.StandardOutput.ReadToEndAsync(ct);
                    var versionLine = output.Split('\n').FirstOrDefault(l => l.Contains("ffmpeg version"));
                    return new PreflightCheck(
                        Name: "FFmpeg",
                        Ok: true,
                        Message: versionLine ?? "FFmpeg is available"
                    );
                }
            }

            return new PreflightCheck(
                Name: "FFmpeg",
                Ok: false,
                Message: "FFmpeg returned non-zero exit code",
                FixHint: "Download and install FFmpeg",
                Link: "https://ffmpeg.org/download.html"
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "FFmpeg not found");
            return new PreflightCheck(
                Name: "FFmpeg",
                Ok: false,
                Message: $"FFmpeg is not available: {ex.Message}",
                FixHint: "Download and install FFmpeg from the Downloads page",
                Link: "/downloads"
            );
        }
    }

    private async Task<PreflightCheck> CheckFfmpegHwAccelsAsync(CancellationToken ct)
    {
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
                await process.WaitForExitAsync(ct);
                var output = await process.StandardOutput.ReadToEndAsync(ct);
                
                var hwaccels = output.Split('\n')
                    .Where(l => !string.IsNullOrWhiteSpace(l) && l != "Hardware acceleration methods:")
                    .Select(l => l.Trim())
                    .Where(l => !string.IsNullOrEmpty(l))
                    .ToList();

                return new PreflightCheck(
                    Name: "Hardware Acceleration",
                    Ok: true,
                    Message: hwaccels.Any() 
                        ? $"Available: {string.Join(", ", hwaccels)}" 
                        : "Software encoding only (no hardware acceleration)"
                );
            }

            return new PreflightCheck(
                Name: "Hardware Acceleration",
                Ok: true,
                Message: "Could not detect hardware acceleration methods"
            );
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not check hardware acceleration");
            return new PreflightCheck(
                Name: "Hardware Acceleration",
                Ok: true,
                Message: "Could not check hardware acceleration (will use software encoding)"
            );
        }
    }

    private async Task<PreflightCheck> CheckNvencCapabilityAsync(CancellationToken ct)
    {
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
                await process.WaitForExitAsync(ct);
                var output = await process.StandardOutput.ReadToEndAsync(ct);

                var hasNvenc = output.Contains("h264_nvenc") || output.Contains("hevc_nvenc");
                
                if (hasNvenc)
                {
                    // Try a tiny probe encode
                    return await ProbeNvencEncodeAsync(ct);
                }

                return new PreflightCheck(
                    Name: "NVENC",
                    Ok: true,
                    Message: "NVENC not available (will use software encoder)",
                    FixHint: "Install NVIDIA drivers and FFmpeg with NVENC support for faster encoding",
                    Link: "https://www.nvidia.com/drivers"
                );
            }

            return new PreflightCheck(
                Name: "NVENC",
                Ok: true,
                Message: "Could not check NVENC availability"
            );
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not check NVENC");
            return new PreflightCheck(
                Name: "NVENC",
                Ok: true,
                Message: "Could not check NVENC (will use software encoder)"
            );
        }
    }

    private async Task<PreflightCheck> ProbeNvencEncodeAsync(CancellationToken ct)
    {
        try
        {
            var ffmpegPath = _providerSettings.GetFfmpegPath();
            var tempOutput = Path.Combine(Path.GetTempPath(), $"nvenc_probe_{Guid.NewGuid():N}.mp4");

            try
            {
                // Create a 2-second test video using NVENC
                var processInfo = new ProcessStartInfo
                {
                    FileName = ffmpegPath,
                    Arguments = $"-f lavfi -i color=c=black:s=320x240:d=1 -c:v h264_nvenc -t 1 -y \"{tempOutput}\"",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                using var process = Process.Start(processInfo);
                if (process != null)
                {
                    using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
                    cts.CancelAfter(TimeSpan.FromSeconds(10));
                    
                    await process.WaitForExitAsync(cts.Token);
                    
                    if (process.ExitCode == 0 && File.Exists(tempOutput))
                    {
                        return new PreflightCheck(
                            Name: "NVENC",
                            Ok: true,
                            Message: "NVENC hardware encoding is available and functional"
                        );
                    }
                }

                return new PreflightCheck(
                    Name: "NVENC",
                    Ok: false,
                    Message: "NVENC probe failed",
                    FixHint: "Update NVIDIA drivers or switch to software encoder",
                    Link: "https://www.nvidia.com/drivers"
                );
            }
            finally
            {
                if (File.Exists(tempOutput))
                {
                    try { File.Delete(tempOutput); } catch { /* ignore */ }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "NVENC probe failed");
            return new PreflightCheck(
                Name: "NVENC",
                Ok: false,
                Message: $"NVENC probe failed: {ex.Message}",
                FixHint: "Update NVIDIA drivers or switch to software encoder",
                Link: "https://www.nvidia.com/drivers"
            );
        }
    }

    private Task<PreflightCheck> CheckDiskSpaceAsync(CancellationToken ct)
    {
        try
        {
            var outputDir = _providerSettings.GetOutputDirectory();
            var driveInfo = new DriveInfo(Path.GetPathRoot(outputDir) ?? "C:\\");
            
            var freeGB = driveInfo.AvailableFreeSpace / (1024.0 * 1024.0 * 1024.0);
            
            if (freeGB >= 10)
            {
                return Task.FromResult(new PreflightCheck(
                    Name: "Disk Space",
                    Ok: true,
                    Message: $"{freeGB:F1} GB available"
                ));
            }

            return Task.FromResult(new PreflightCheck(
                Name: "Disk Space",
                Ok: false,
                Message: $"Only {freeGB:F1} GB available (minimum 10 GB required)",
                FixHint: "Free up disk space or change output directory",
                Link: "/settings"
            ));
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not check disk space");
            return Task.FromResult(new PreflightCheck(
                Name: "Disk Space",
                Ok: true,
                Message: "Could not check disk space (proceeding anyway)"
            ));
        }
    }

    private async Task<PreflightCheck> CheckOfflineConsistencyAsync(CancellationToken ct)
    {
        try
        {
            var settingsPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "Aura",
                "settings.json");

            if (!File.Exists(settingsPath))
            {
                return new PreflightCheck(
                    Name: "Offline Mode",
                    Ok: true,
                    Message: "Offline mode not enabled"
                );
            }

            var json = await File.ReadAllTextAsync(settingsPath, ct);
            var settings = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(json);

            if (settings == null || !settings.ContainsKey("offlineMode"))
            {
                return new PreflightCheck(
                    Name: "Offline Mode",
                    Ok: true,
                    Message: "Offline mode not enabled"
                );
            }

            var offlineMode = settings["offlineMode"].GetBoolean();
            
            if (!offlineMode)
            {
                return new PreflightCheck(
                    Name: "Offline Mode",
                    Ok: true,
                    Message: "Offline mode not enabled"
                );
            }

            // If offline mode is enabled, check that profile is compatible
            var hasProfile = settings.TryGetValue("profile", out var profileElement);
            var profileName = hasProfile ? profileElement.GetString() : null;

            if (profileName == "Pro-Max" || profileName == "Balanced Mix")
            {
                return new PreflightCheck(
                    Name: "Offline Mode",
                    Ok: false,
                    Message: $"Offline mode enabled but profile '{profileName}' requires network access",
                    FixHint: "Switch to 'Free-Only' profile or disable offline mode",
                    Link: "/settings"
                );
            }

            return new PreflightCheck(
                Name: "Offline Mode",
                Ok: true,
                Message: "Offline mode is enabled and configuration is consistent"
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking offline mode consistency");
            return new PreflightCheck(
                Name: "Offline Mode",
                Ok: true,
                Message: "Could not verify offline mode consistency (proceeding anyway)"
            );
        }
    }
}
