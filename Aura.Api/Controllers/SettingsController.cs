using System;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Aura.Api.Models;
using Aura.Core.Configuration;
using Aura.Core.Models;
using Aura.Core.Models.Settings;
using Aura.Core.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace Aura.Api.Controllers;

/// <summary>
/// Controller for application settings including AI optimization and user settings
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class SettingsController : ControllerBase
{
    private readonly ILogger<SettingsController> _logger;
    private readonly ProviderSettings _providerSettings;
    private readonly ISecureStorageService _secureStorage;
    private readonly string _settingsFilePath;
    private readonly string _firstRunFilePath;
    private readonly string _userSettingsFilePath;

    public SettingsController(
        ILogger<SettingsController> logger,
        ProviderSettings providerSettings,
        ISecureStorageService secureStorage)
    {
        _logger = logger;
        _providerSettings = providerSettings;
        _secureStorage = secureStorage;
        
        // Store settings in AuraData directory
        var auraDataDir = _providerSettings.GetAuraDataDirectory();
        _settingsFilePath = Path.Combine(auraDataDir, "ai-optimization-settings.json");
        _firstRunFilePath = Path.Combine(auraDataDir, "first-run-status.json");
        _userSettingsFilePath = Path.Combine(auraDataDir, "user-settings.json");
    }

    /// <summary>
    /// Get AI optimization settings
    /// </summary>
    [HttpGet("ai-optimization")]
    public async Task<IActionResult> GetAIOptimizationSettings(CancellationToken ct)
    {
        try
        {
            var settings = await LoadSettingsAsync(ct);
            
            return Ok(new
            {
                success = true,
                settings
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading AI optimization settings");
            return StatusCode(500, new { error = "Failed to load settings" });
        }
    }

    /// <summary>
    /// Update AI optimization settings
    /// </summary>
    [HttpPost("ai-optimization")]
    public async Task<IActionResult> UpdateAIOptimizationSettings(
        [FromBody] AIOptimizationSettings settings,
        CancellationToken ct)
    {
        try
        {
            if (settings == null)
            {
                return BadRequest(new { error = "Settings are required" });
            }

            // Validate settings
            if (settings.MinimumQualityThreshold < 0 || settings.MinimumQualityThreshold > 100)
            {
                return BadRequest(new { error = "Quality threshold must be between 0 and 100" });
            }

            await SaveSettingsAsync(settings, ct);

            _logger.LogInformation(
                "AI optimization settings updated: Enabled={Enabled}, Level={Level}",
                settings.Enabled, settings.Level);

            return Ok(new
            {
                success = true,
                message = "Settings updated successfully",
                settings
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating AI optimization settings");
            return StatusCode(500, new { error = "Failed to update settings" });
        }
    }

    /// <summary>
    /// Reset AI optimization settings to defaults
    /// </summary>
    [HttpPost("ai-optimization/reset")]
    public async Task<IActionResult> ResetAIOptimizationSettings(CancellationToken ct)
    {
        try
        {
            var defaultSettings = AIOptimizationSettings.Default;
            await SaveSettingsAsync(defaultSettings, ct);

            _logger.LogInformation("AI optimization settings reset to defaults");

            return Ok(new
            {
                success = true,
                message = "Settings reset to defaults",
                settings = defaultSettings
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error resetting AI optimization settings");
            return StatusCode(500, new { error = "Failed to reset settings" });
        }
    }

    /// <summary>
    /// Get first-run status
    /// </summary>
    [HttpGet("first-run")]
    public async Task<IActionResult> GetFirstRunStatus(CancellationToken ct)
    {
        try
        {
            var status = await LoadFirstRunStatusAsync(ct);
            return Ok(status);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading first-run status");
            return StatusCode(500, new { error = "Failed to load first-run status" });
        }
    }

    /// <summary>
    /// Set first-run status
    /// </summary>
    [HttpPost("first-run")]
    public async Task<IActionResult> SetFirstRunStatus(
        [FromBody] FirstRunStatus status,
        CancellationToken ct)
    {
        try
        {
            if (status == null)
            {
                return BadRequest(new { error = "Status is required" });
            }

            await SaveFirstRunStatusAsync(status, ct);

            _logger.LogInformation(
                "First-run status updated: Completed={Completed}, Version={Version}",
                status.HasCompletedFirstRun, status.Version);

            return Ok(new
            {
                success = true,
                message = "First-run status updated successfully"
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating first-run status");
            return StatusCode(500, new { error = "Failed to update first-run status" });
        }
    }

    /// <summary>
    /// Reset first-run status (for testing/re-running wizard)
    /// </summary>
    [HttpPost("first-run/reset")]
    public async Task<IActionResult> ResetFirstRunStatus(CancellationToken ct)
    {
        try
        {
            var resetStatus = new FirstRunStatus
            {
                HasCompletedFirstRun = false,
                CompletedAt = null,
                Version = null
            };

            await SaveFirstRunStatusAsync(resetStatus, ct);

            _logger.LogInformation("First-run status reset");

            return Ok(new
            {
                success = true,
                message = "First-run status reset successfully"
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error resetting first-run status");
            return StatusCode(500, new { error = "Failed to reset first-run status" });
        }
    }

    /// <summary>
    /// Load settings from file or return defaults
    /// </summary>
    private async Task<AIOptimizationSettings> LoadSettingsAsync(CancellationToken ct)
    {
        if (!System.IO.File.Exists(_settingsFilePath))
        {
            _logger.LogDebug("Settings file not found, using defaults");
            return AIOptimizationSettings.Default;
        }

        try
        {
            var json = await System.IO.File.ReadAllTextAsync(_settingsFilePath, ct);
            var settings = JsonSerializer.Deserialize<AIOptimizationSettings>(json);
            return settings ?? AIOptimizationSettings.Default;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error reading settings file, using defaults");
            return AIOptimizationSettings.Default;
        }
    }

    /// <summary>
    /// Save settings to file
    /// </summary>
    private async Task SaveSettingsAsync(AIOptimizationSettings settings, CancellationToken ct)
    {
        var directory = Path.GetDirectoryName(_settingsFilePath);
        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var options = new JsonSerializerOptions
        {
            WriteIndented = true
        };

        var json = JsonSerializer.Serialize(settings, options);
        await System.IO.File.WriteAllTextAsync(_settingsFilePath, json, ct);
    }

    /// <summary>
    /// Load first-run status from file or return default
    /// </summary>
    private async Task<FirstRunStatus> LoadFirstRunStatusAsync(CancellationToken ct)
    {
        if (!System.IO.File.Exists(_firstRunFilePath))
        {
            _logger.LogDebug("First-run status file not found, returning default");
            return new FirstRunStatus
            {
                HasCompletedFirstRun = false,
                CompletedAt = null,
                Version = null
            };
        }

        try
        {
            var json = await System.IO.File.ReadAllTextAsync(_firstRunFilePath, ct);
            var status = JsonSerializer.Deserialize<FirstRunStatus>(json);
            return status ?? new FirstRunStatus
            {
                HasCompletedFirstRun = false,
                CompletedAt = null,
                Version = null
            };
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error reading first-run status file, using default");
            return new FirstRunStatus
            {
                HasCompletedFirstRun = false,
                CompletedAt = null,
                Version = null
            };
        }
    }

    /// <summary>
    /// Save first-run status to file
    /// </summary>
    private async Task SaveFirstRunStatusAsync(FirstRunStatus status, CancellationToken ct)
    {
        var directory = Path.GetDirectoryName(_firstRunFilePath);
        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var options = new JsonSerializerOptions
        {
            WriteIndented = true
        };

        var json = JsonSerializer.Serialize(status, options);
        await System.IO.File.WriteAllTextAsync(_firstRunFilePath, json, ct);
    }

    /// <summary>
    /// Get comprehensive user settings
    /// </summary>
    [HttpGet("user")]
    public async Task<IActionResult> GetUserSettings(CancellationToken ct)
    {
        try
        {
            var settings = await LoadUserSettingsAsync(ct);
            return Ok(settings);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading user settings");
            return StatusCode(500, new { error = "Failed to load user settings" });
        }
    }

    /// <summary>
    /// Save comprehensive user settings
    /// API keys are extracted and saved via secure encrypted storage separately
    /// </summary>
    [HttpPost("user")]
    public async Task<IActionResult> SaveUserSettings(
        [FromBody] UserSettings settings,
        CancellationToken ct)
    {
        try
        {
            if (settings == null)
            {
                return BadRequest(new { error = "Settings are required" });
            }

            // Update timestamp
            settings.LastUpdated = DateTime.UtcNow;

            // Extract and save API keys securely (encrypted storage)
            if (settings.ApiKeys != null)
            {
                await SaveApiKeysSecurelyAsync(settings.ApiKeys, ct);
            }

            // Save settings WITHOUT API keys (they're stored securely in encrypted storage)
            await SaveUserSettingsAsync(settings, ct);

            _logger.LogInformation("User settings saved successfully");

            return Ok(new
            {
                success = true,
                message = "Settings saved successfully"
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error saving user settings");
            return StatusCode(500, new { error = "Failed to save user settings" });
        }
    }

    /// <summary>
    /// Reset user settings to defaults and clear API keys from secure storage
    /// </summary>
    [HttpPost("user/reset")]
    public async Task<IActionResult> ResetUserSettings(CancellationToken ct)
    {
        try
        {
            var defaults = new UserSettings();
            
            // Clear all API keys from secure storage when resetting
            var configuredProviders = await _secureStorage.GetConfiguredProvidersAsync();
            foreach (var provider in configuredProviders)
            {
                await _secureStorage.DeleteApiKeyAsync(provider);
            }
            
            // Save default settings (without API keys)
            await SaveUserSettingsAsync(defaults, ct);

            _logger.LogInformation("User settings reset to defaults and API keys cleared");

            return Ok(new
            {
                success = true,
                message = "Settings reset to defaults",
                settings = defaults
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error resetting user settings");
            return StatusCode(500, new { error = "Failed to reset user settings" });
        }
    }

    /// <summary>
    /// Test API key for a specific provider (basic format validation only)
    /// </summary>
    [HttpPost("test-api-key/{provider}")]
    public async Task<IActionResult> TestApiKey(
        string provider,
        [FromBody] TestApiKeyRequest request,
        CancellationToken ct)
    {
        try
        {
            var apiKey = request.ApiKey ?? "";
            
            // Basic validation
            if (string.IsNullOrWhiteSpace(apiKey))
            {
                return Ok(new
                {
                    success = false,
                    message = "API key is required"
                });
            }

            // Provider-specific validation
            var normalizedProvider = provider.Trim().ToLowerInvariant();

            var result = normalizedProvider switch
            {
                "openai" => ValidateOpenAIKey(apiKey),
                "anthropic" => ValidateAnthropicKey(apiKey),
                "stabilityai" => ValidateStabilityAIKey(apiKey),
                "elevenlabs" => ValidateElevenLabsKey(apiKey),
                "pexels" => ValidatePexelsKey(apiKey),
                "pixabay" => ValidatePixabayKey(apiKey),
                "unsplash" => ValidateUnsplashKey(apiKey),
                "google" => ValidateGoogleKey(apiKey),
                "azure" => ValidateAzureKey(apiKey),
                _ => new
                {
                    success = false,
                    message = $"Unknown provider '{provider}'."
                }
            };

            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error testing API key for {Provider}", provider);
            return Ok(new
            {
                success = false,
                message = $"Error testing API key: {ex.Message}"
            });
        }
    }

    /// <summary>
    /// Validate file or directory path
    /// </summary>
    [HttpPost("validate-path")]
    public IActionResult ValidatePath([FromBody] ValidatePathRequest request)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(request.Path))
            {
                return Ok(new
                {
                    valid = false,
                    message = "Path is required"
                });
            }

            // Check if it's a file
            if (System.IO.File.Exists(request.Path))
            {
                return Ok(new
                {
                    valid = true,
                    message = "File exists"
                });
            }

            // Check if it's a directory
            if (Directory.Exists(request.Path))
            {
                return Ok(new
                {
                    valid = true,
                    message = "Directory exists"
                });
            }

            return Ok(new
            {
                valid = false,
                message = "Path does not exist"
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error validating path");
            return Ok(new
            {
                valid = false,
                message = $"Error validating path: {ex.Message}"
            });
        }
    }

    /// <summary>
    /// Load user settings from file and API keys from secure encrypted storage
    /// </summary>
    private async Task<UserSettings> LoadUserSettingsAsync(CancellationToken ct)
    {
        UserSettings settings;
        
        if (!System.IO.File.Exists(_userSettingsFilePath))
        {
            _logger.LogDebug("User settings file not found, using defaults");
            settings = new UserSettings();
            
            if (string.IsNullOrWhiteSpace(settings.General.DefaultProjectSaveLocation))
            {
                settings.General.DefaultProjectSaveLocation = Aura.Core.Services.DefaultPathProvider.GetDefaultSaveLocation();
                _logger.LogInformation("Initialized default save location: {Location}", settings.General.DefaultProjectSaveLocation);
            }
        }
        else
        {
            try
            {
                var json = await System.IO.File.ReadAllTextAsync(_userSettingsFilePath, ct);
                settings = JsonSerializer.Deserialize<UserSettings>(json) ?? new UserSettings();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error reading user settings file, using defaults");
                settings = new UserSettings();
            }
        }

        if (string.IsNullOrWhiteSpace(settings.General.DefaultProjectSaveLocation))
        {
            settings.General.DefaultProjectSaveLocation = Aura.Core.Services.DefaultPathProvider.GetDefaultSaveLocation();
            _logger.LogInformation("Default save location not configured, initialized to: {Location}", settings.General.DefaultProjectSaveLocation);
        }

        // Load API keys from secure encrypted storage (never from settings file)
        // Initialize ApiKeys if null to ensure keys are loaded
        if (settings.ApiKeys == null)
        {
            settings.ApiKeys = new ApiKeysSettings();
        }
        await LoadApiKeysSecurelyAsync(settings.ApiKeys, ct);

        return settings;
    }

    /// <summary>
    /// Save user settings to file WITHOUT API keys (they're stored securely separately)
    /// </summary>
    private async Task SaveUserSettingsAsync(UserSettings settings, CancellationToken ct)
    {
        var directory = Path.GetDirectoryName(_userSettingsFilePath);
        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }

        // Create a copy of settings with API keys cleared (they're stored securely)
        var settingsToSave = new UserSettings
        {
            General = settings.General,
            ApiKeys = new ApiKeysSettings(), // Empty - keys stored in secure storage
            FileLocations = settings.FileLocations,
            VideoDefaults = settings.VideoDefaults,
            EditorPreferences = settings.EditorPreferences,
            UI = settings.UI,
            Advanced = settings.Advanced,
            Version = settings.Version,
            LastUpdated = settings.LastUpdated
        };

        var options = new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };

        var json = JsonSerializer.Serialize(settingsToSave, options);
        await System.IO.File.WriteAllTextAsync(_userSettingsFilePath, json, ct);
    }

    /// <summary>
    /// Save API keys to secure encrypted storage (DPAPI on Windows, AES-256 on Linux/macOS)
    /// </summary>
    private async Task SaveApiKeysSecurelyAsync(ApiKeysSettings apiKeys, CancellationToken ct)
    {
        var keyMap = new System.Collections.Generic.Dictionary<string, string>
        {
            { "openai", apiKeys.OpenAI ?? string.Empty },
            { "anthropic", apiKeys.Anthropic ?? string.Empty },
            { "stabilityai", apiKeys.StabilityAI ?? string.Empty },
            { "elevenlabs", apiKeys.ElevenLabs ?? string.Empty },
            { "pexels", apiKeys.Pexels ?? string.Empty },
            { "pixabay", apiKeys.Pixabay ?? string.Empty },
            { "unsplash", apiKeys.Unsplash ?? string.Empty },
            { "google", apiKeys.Google ?? string.Empty },
            { "azure", apiKeys.Azure ?? string.Empty }
        };

        foreach (var kvp in keyMap)
        {
            if (!string.IsNullOrWhiteSpace(kvp.Value))
            {
                await _secureStorage.SaveApiKeyAsync(kvp.Key, kvp.Value);
            }
            else
            {
                // Delete key if it's empty (user cleared it)
                if (await _secureStorage.HasApiKeyAsync(kvp.Key))
                {
                    await _secureStorage.DeleteApiKeyAsync(kvp.Key);
                }
            }
        }
    }

    /// <summary>
    /// Load API keys from secure encrypted storage into ApiKeysSettings
    /// </summary>
    private async Task LoadApiKeysSecurelyAsync(ApiKeysSettings apiKeys, CancellationToken ct)
    {
        apiKeys.OpenAI = await _secureStorage.GetApiKeyAsync("openai") ?? string.Empty;
        apiKeys.Anthropic = await _secureStorage.GetApiKeyAsync("anthropic") ?? string.Empty;
        apiKeys.StabilityAI = await _secureStorage.GetApiKeyAsync("stabilityai") ?? string.Empty;
        apiKeys.ElevenLabs = await _secureStorage.GetApiKeyAsync("elevenlabs") ?? string.Empty;
        apiKeys.Pexels = await _secureStorage.GetApiKeyAsync("pexels") ?? string.Empty;
        apiKeys.Pixabay = await _secureStorage.GetApiKeyAsync("pixabay") ?? string.Empty;
        apiKeys.Unsplash = await _secureStorage.GetApiKeyAsync("unsplash") ?? string.Empty;
        apiKeys.Google = await _secureStorage.GetApiKeyAsync("google") ?? string.Empty;
        apiKeys.Azure = await _secureStorage.GetApiKeyAsync("azure") ?? string.Empty;
    }

    /// <summary>
    /// Validate OpenAI API key format
    /// </summary>
    private object ValidateOpenAIKey(string apiKey)
    {
        if (!apiKey.StartsWith("sk-") || apiKey.Length < 20)
        {
            return new
            {
                success = false,
                message = "Invalid OpenAI API key format. Must start with 'sk-' and be at least 20 characters."
            };
        }

        return new
        {
            success = true,
            message = "API key format is valid"
        };
    }

    /// <summary>
    /// Validate Anthropic API key format
    /// </summary>
    private object ValidateAnthropicKey(string apiKey)
    {
        if (!apiKey.StartsWith("sk-ant-") || apiKey.Length < 20)
        {
            return new
            {
                success = false,
                message = "Invalid Anthropic API key format. Must start with 'sk-ant-' and be at least 20 characters."
            };
        }

        return new
        {
            success = true,
            message = "API key format is valid"
        };
    }

    /// <summary>
    /// Validate Stability AI API key format
    /// </summary>
    private object ValidateStabilityAIKey(string apiKey)
    {
        if (!apiKey.StartsWith("sk-") || apiKey.Length < 20)
        {
            return new
            {
                success = false,
                message = "Invalid Stability AI API key format. Must start with 'sk-' and be at least 20 characters."
            };
        }

        return new
        {
            success = true,
            message = "API key format is valid"
        };
    }

    /// <summary>
    /// Validate ElevenLabs API key format
    /// </summary>
    private object ValidateElevenLabsKey(string apiKey)
    {
        if (apiKey.Length < 32)
        {
            return new
            {
                success = false,
                message = "Invalid ElevenLabs API key format. Must be at least 32 characters."
            };
        }

        return new
        {
            success = true,
            message = "API key format is valid"
        };
    }

    /// <summary>
    /// Validate Pexels API key format
    /// </summary>
    private object ValidatePexelsKey(string apiKey)
    {
        if (apiKey.Length < 20)
        {
            return new
            {
                success = false,
                message = "Pexels API keys should be at least 20 characters long."
            };
        }

        return new
        {
            success = true,
            message = "API key format is valid"
        };
    }

    /// <summary>
    /// Validate Pixabay API key format
    /// </summary>
    private object ValidatePixabayKey(string apiKey)
    {
        if (apiKey.Length < 10)
        {
            return new
            {
                success = false,
                message = "Pixabay API keys should be at least 10 characters long."
            };
        }

        return new
        {
            success = true,
            message = "API key format is valid"
        };
    }

    /// <summary>
    /// Validate Unsplash API key format
    /// </summary>
    private object ValidateUnsplashKey(string apiKey)
    {
        if (apiKey.Length < 16)
        {
            return new
            {
                success = false,
                message = "Unsplash API keys should be at least 16 characters long."
            };
        }

        return new
        {
            success = true,
            message = "API key format is valid"
        };
    }

    /// <summary>
    /// Validate Google API key format
    /// </summary>
    private object ValidateGoogleKey(string apiKey)
    {
        if (!apiKey.StartsWith("AIza", StringComparison.OrdinalIgnoreCase) || apiKey.Length < 20)
        {
            return new
            {
                success = false,
                message = "Google API keys typically start with 'AIza' and should be at least 20 characters."
            };
        }

        return new
        {
            success = true,
            message = "API key format is valid"
        };
    }

    /// <summary>
    /// Validate Azure API key format
    /// </summary>
    private object ValidateAzureKey(string apiKey)
    {
        if (apiKey.Length < 32)
        {
            return new
            {
                success = false,
                message = "Azure API keys should be at least 32 characters long."
            };
        }

        return new
        {
            success = true,
            message = "API key format is valid"
        };
    }

    /// <summary>
    /// Get current Ollama model setting
    /// </summary>
    [HttpGet("ollama/model")]
    public IActionResult GetOllamaModel()
    {
        try
        {
            var model = _providerSettings.GetOllamaModel();
            _logger.LogInformation("Retrieved Ollama model setting: {Model}", model);
            
            return Ok(new
            {
                success = true,
                model
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting Ollama model setting");
            return StatusCode(500, new { error = "Failed to get Ollama model setting" });
        }
    }

    /// <summary>
    /// Set Ollama model
    /// </summary>
    [HttpPost("ollama/model")]
    public IActionResult SetOllamaModel([FromBody] SetOllamaModelRequest request)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(request.Model))
            {
                return BadRequest(new { error = "Model name is required" });
            }

            _providerSettings.SetOllamaModel(request.Model);
            _logger.LogInformation("Ollama model setting updated to: {Model}", request.Model);
            
            return Ok(new
            {
                success = true,
                message = "Ollama model updated successfully",
                model = request.Model
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error setting Ollama model");
            return StatusCode(500, new { error = "Failed to set Ollama model" });
        }
    }
}



