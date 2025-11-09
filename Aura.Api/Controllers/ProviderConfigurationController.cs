using Aura.Api.Models.ApiModels.V1;
using Aura.Core.Configuration;
using Aura.Core.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace Aura.Api.Controllers;

/// <summary>
/// Controller for provider and quality configuration management
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class ProviderConfigurationController : ControllerBase
{
    private readonly ILogger<ProviderConfigurationController> _logger;
    private readonly ProviderSettings _providerSettings;
    private readonly ISecureStorageService _secureStorage;
    private readonly string _configDir;

    public ProviderConfigurationController(
        ILogger<ProviderConfigurationController> logger,
        ProviderSettings providerSettings,
        ISecureStorageService secureStorage)
    {
        _logger = logger;
        _providerSettings = providerSettings;
        _secureStorage = secureStorage;
        
        var auraDataDir = _providerSettings.GetAuraDataDirectory();
        _configDir = Path.Combine(auraDataDir, "configurations");
        Directory.CreateDirectory(_configDir);
    }

    /// <summary>
    /// Get current provider configuration
    /// </summary>
    [HttpGet("providers")]
    public async Task<IActionResult> GetProviderConfiguration(CancellationToken ct)
    {
        try
        {
            var configPath = Path.Combine(_configDir, "provider-config.json");
            if (!System.IO.File.Exists(configPath))
            {
                return Ok(new SaveProviderConfigRequest(new List<ProviderConfigDto>
                {
                    CreateDefaultProvider("OpenAI", "LLM", 1),
                    CreateDefaultProvider("Anthropic", "LLM", 2),
                    CreateDefaultProvider("Google", "LLM", 3),
                    CreateDefaultProvider("Ollama", "LLM", 4),
                    CreateDefaultProvider("ElevenLabs", "TTS", 1),
                    CreateDefaultProvider("PlayHT", "TTS", 2),
                    CreateDefaultProvider("Windows", "TTS", 3),
                    CreateDefaultProvider("StabilityAI", "Image", 1),
                    CreateDefaultProvider("StableDiffusion", "Image", 2),
                    CreateDefaultProvider("Stock", "Image", 3)
                }));
            }

            var json = await System.IO.File.ReadAllTextAsync(configPath, ct);
            var config = JsonSerializer.Deserialize<SaveProviderConfigRequest>(json);
            return Ok(config);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading provider configuration");
            return StatusCode(500, new { error = "Failed to load provider configuration" });
        }
    }

    /// <summary>
    /// Save provider configuration
    /// </summary>
    [HttpPost("providers")]
    public async Task<IActionResult> SaveProviderConfiguration(
        [FromBody] SaveProviderConfigRequest request,
        CancellationToken ct)
    {
        try
        {
            if (request?.Providers == null)
            {
                return BadRequest(new { error = "Provider configuration is required" });
            }

            foreach (var provider in request.Providers)
            {
                if (!string.IsNullOrEmpty(provider.ApiKey))
                {
                    await _secureStorage.SaveApiKeyAsync(provider.Name, provider.ApiKey);
                }
            }

            var configPath = Path.Combine(_configDir, "provider-config.json");
            var providersWithoutKeys = new SaveProviderConfigRequest(
                request.Providers.Select(p => p with { ApiKey = null }).ToList()
            );
            
            var json = JsonSerializer.Serialize(providersWithoutKeys, new JsonSerializerOptions 
            { 
                WriteIndented = true 
            });
            await System.IO.File.WriteAllTextAsync(configPath, json, ct);

            _logger.LogInformation("Provider configuration saved successfully");
            return Ok(new { success = true, message = "Configuration saved" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error saving provider configuration");
            return StatusCode(500, new { error = "Failed to save provider configuration" });
        }
    }

    /// <summary>
    /// Get available models for a provider
    /// </summary>
    [HttpGet("models/{providerName}")]
    public async Task<IActionResult> GetAvailableModels(string providerName, CancellationToken ct)
    {
        try
        {
            var models = providerName.ToLowerInvariant() switch
            {
                "openai" => new List<AvailableModelDto>
                {
                    new("gpt-4", "GPT-4", "Most capable model", new List<string> { "text-generation", "reasoning" }, 0.03m, true, "OpenAI_ApiKey"),
                    new("gpt-4-turbo", "GPT-4 Turbo", "Faster GPT-4", new List<string> { "text-generation", "reasoning" }, 0.01m, true, "OpenAI_ApiKey"),
                    new("gpt-3.5-turbo", "GPT-3.5 Turbo", "Fast and cost-effective", new List<string> { "text-generation" }, 0.001m, true, "OpenAI_ApiKey")
                },
                "anthropic" => new List<AvailableModelDto>
                {
                    new("claude-3-opus", "Claude 3 Opus", "Most intelligent model", new List<string> { "text-generation", "reasoning", "analysis" }, 0.015m, true, "Anthropic_ApiKey"),
                    new("claude-3-sonnet", "Claude 3 Sonnet", "Balanced performance", new List<string> { "text-generation", "reasoning" }, 0.003m, true, "Anthropic_ApiKey"),
                    new("claude-3-haiku", "Claude 3 Haiku", "Fast and compact", new List<string> { "text-generation" }, 0.00025m, true, "Anthropic_ApiKey")
                },
                "google" => new List<AvailableModelDto>
                {
                    new("gemini-pro", "Gemini Pro", "Advanced reasoning", new List<string> { "text-generation", "reasoning", "multimodal" }, 0.00025m, true, "Google_ApiKey"),
                    new("gemini-pro-vision", "Gemini Pro Vision", "Multimodal capabilities", new List<string> { "text-generation", "vision", "multimodal" }, 0.00025m, true, "Google_ApiKey")
                },
                "ollama" => new List<AvailableModelDto>
                {
                    new("llama3.1:8b", "Llama 3.1 8B", "Efficient local model", new List<string> { "text-generation" }, 0, true, null),
                    new("mistral:7b", "Mistral 7B", "Fast local inference", new List<string> { "text-generation" }, 0, true, null),
                    new("codellama:13b", "CodeLlama 13B", "Code-focused model", new List<string> { "text-generation", "code" }, 0, true, null)
                },
                "elevenlabs" => new List<AvailableModelDto>
                {
                    new("eleven_multilingual_v2", "Multilingual v2", "Best quality, 29 languages", new List<string> { "tts", "multilingual" }, 0.30m, true, "ElevenLabs_ApiKey"),
                    new("eleven_turbo_v2", "Turbo v2", "Fast generation", new List<string> { "tts" }, 0.15m, true, "ElevenLabs_ApiKey")
                },
                "playht" => new List<AvailableModelDto>
                {
                    new("play3.0-mini", "Play 3.0 Mini", "Fast and efficient", new List<string> { "tts" }, 0.10m, true, "PlayHT_ApiKey"),
                    new("play3.0", "Play 3.0", "High quality", new List<string> { "tts", "voice-cloning" }, 0.20m, true, "PlayHT_ApiKey")
                },
                "stabilitya" => new List<AvailableModelDto>
                {
                    new("stable-diffusion-xl", "SDXL 1.0", "High quality images", new List<string> { "image-generation" }, 0.02m, true, "StabilityAI_ApiKey"),
                    new("stable-diffusion-3", "SD 3.0", "Latest model", new List<string> { "image-generation" }, 0.035m, true, "StabilityAI_ApiKey")
                },
                _ => new List<AvailableModelDto>()
            };

            await Task.CompletedTask;
            return Ok(new
            {
                providerName,
                providerType = GetProviderType(providerName),
                selectedModel = (string?)null,
                availableModels = models
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting models for provider {Provider}", providerName);
            return StatusCode(500, new { error = "Failed to get available models" });
        }
    }

    /// <summary>
    /// Get current quality configuration
    /// </summary>
    [HttpGet("quality")]
    public async Task<IActionResult> GetQualityConfiguration(CancellationToken ct)
    {
        try
        {
            var configPath = Path.Combine(_configDir, "quality-config.json");
            if (!System.IO.File.Exists(configPath))
            {
                return Ok(CreateDefaultQualityConfig());
            }

            var json = await System.IO.File.ReadAllTextAsync(configPath, ct);
            var config = JsonSerializer.Deserialize<QualityConfigDto>(json);
            return Ok(config);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading quality configuration");
            return StatusCode(500, new { error = "Failed to load quality configuration" });
        }
    }

    /// <summary>
    /// Save quality configuration
    /// </summary>
    [HttpPost("quality")]
    public async Task<IActionResult> SaveQualityConfiguration(
        [FromBody] QualityConfigDto request,
        CancellationToken ct)
    {
        try
        {
            if (request == null)
            {
                return BadRequest(new { error = "Quality configuration is required" });
            }

            var configPath = Path.Combine(_configDir, "quality-config.json");
            var json = JsonSerializer.Serialize(request, new JsonSerializerOptions 
            { 
                WriteIndented = true 
            });
            await System.IO.File.WriteAllTextAsync(configPath, json, ct);

            _logger.LogInformation("Quality configuration saved successfully");
            return Ok(new { success = true, message = "Configuration saved" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error saving quality configuration");
            return StatusCode(500, new { error = "Failed to save quality configuration" });
        }
    }

    /// <summary>
    /// Validate configuration
    /// </summary>
    [HttpPost("validate")]
    public async Task<IActionResult> ValidateConfiguration(
        [FromBody] SaveProviderConfigRequest request,
        CancellationToken ct)
    {
        try
        {
            var issues = new List<ValidationIssueDto>();
            var warnings = new List<ValidationIssueDto>();

            foreach (var provider in request.Providers)
            {
                if (provider.Enabled && string.IsNullOrEmpty(provider.ApiKey) && RequiresApiKey(provider.Name))
                {
                    issues.Add(new ValidationIssueDto(
                        "Error",
                        provider.Name,
                        $"{provider.Name} is enabled but API key is not configured",
                        "Configure API key in settings"));
                }

                if (provider.CostLimit.HasValue && provider.CostLimit <= 0)
                {
                    warnings.Add(new ValidationIssueDto(
                        "Warning",
                        provider.Name,
                        "Cost limit is set to zero or negative value",
                        "Set a positive cost limit or remove the limit"));
                }
            }

            await Task.CompletedTask;
            return Ok(new ConfigValidationResultDto(issues.Count == 0, issues, warnings));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error validating configuration");
            return StatusCode(500, new { error = "Failed to validate configuration" });
        }
    }

    /// <summary>
    /// Get configuration profiles
    /// </summary>
    [HttpGet("profiles")]
    public async Task<IActionResult> GetConfigurationProfiles(CancellationToken ct)
    {
        try
        {
            var profilesPath = Path.Combine(_configDir, "profiles");
            Directory.CreateDirectory(profilesPath);

            var profiles = new List<ConfigurationProfileDto>();
            foreach (var file in Directory.GetFiles(profilesPath, "*.json"))
            {
                var json = await System.IO.File.ReadAllTextAsync(file, ct);
                var profile = JsonSerializer.Deserialize<ConfigurationProfileDto>(json);
                if (profile != null)
                {
                    profiles.Add(profile);
                }
            }

            return Ok(profiles);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading configuration profiles");
            return StatusCode(500, new { error = "Failed to load profiles" });
        }
    }

    /// <summary>
    /// Save configuration profile
    /// </summary>
    [HttpPost("profiles")]
    public async Task<IActionResult> SaveConfigurationProfile(
        [FromBody] SaveConfigProfileRequest request,
        CancellationToken ct)
    {
        try
        {
            if (request == null)
            {
                return BadRequest(new { error = "Profile data is required" });
            }

            var profile = new ConfigurationProfileDto(
                Guid.NewGuid().ToString(),
                request.Name,
                request.Description,
                request.ProviderConfig,
                request.QualityConfig,
                DateTime.UtcNow,
                DateTime.UtcNow,
                false,
                "1.0");

            var profilesPath = Path.Combine(_configDir, "profiles");
            Directory.CreateDirectory(profilesPath);
            
            var filePath = Path.Combine(profilesPath, $"{profile.Id}.json");
            var json = JsonSerializer.Serialize(profile, new JsonSerializerOptions 
            { 
                WriteIndented = true 
            });
            await System.IO.File.WriteAllTextAsync(filePath, json, ct);

            _logger.LogInformation("Configuration profile {Name} saved successfully", request.Name);
            return Ok(profile);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error saving configuration profile");
            return StatusCode(500, new { error = "Failed to save profile" });
        }
    }

    /// <summary>
    /// Export configuration
    /// </summary>
    [HttpGet("export")]
    public async Task<IActionResult> ExportConfiguration(CancellationToken ct)
    {
        try
        {
            var profiles = await GetAllProfilesAsync(ct);
            var currentProvider = await LoadProviderConfigAsync(ct);
            var currentQuality = await LoadQualityConfigAsync(ct);
            
            var currentProfile = new ConfigurationProfileDto(
                "current",
                "Current Configuration",
                "Current active configuration",
                currentProvider,
                currentQuality,
                DateTime.UtcNow,
                DateTime.UtcNow,
                true,
                "1.0");

            var export = new ConfigurationExportDto(
                "1.0",
                DateTime.UtcNow,
                profiles,
                currentProfile);

            return Ok(export);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error exporting configuration");
            return StatusCode(500, new { error = "Failed to export configuration" });
        }
    }

    /// <summary>
    /// Import configuration
    /// </summary>
    [HttpPost("import")]
    public async Task<IActionResult> ImportConfiguration(
        [FromBody] ImportConfigurationRequest request,
        CancellationToken ct)
    {
        try
        {
            if (request?.Configuration == null)
            {
                return BadRequest(new { error = "Configuration data is required" });
            }

            if (request.OverwriteExisting)
            {
                await SaveProviderConfigAsync(request.Configuration.CurrentProfile.ProviderConfig, ct);
                await SaveQualityConfigAsync(request.Configuration.CurrentProfile.QualityConfig, ct);
            }

            foreach (var profile in request.Configuration.Profiles)
            {
                var profilesPath = Path.Combine(_configDir, "profiles");
                Directory.CreateDirectory(profilesPath);
                
                var filePath = Path.Combine(profilesPath, $"{profile.Id}.json");
                if (request.OverwriteExisting || !System.IO.File.Exists(filePath))
                {
                    var json = JsonSerializer.Serialize(profile, new JsonSerializerOptions 
                    { 
                        WriteIndented = true 
                    });
                    await System.IO.File.WriteAllTextAsync(filePath, json, ct);
                }
            }

            _logger.LogInformation("Configuration imported successfully");
            return Ok(new { success = true, message = "Configuration imported", profileCount = request.Configuration.Profiles.Count });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error importing configuration");
            return StatusCode(500, new { error = "Failed to import configuration" });
        }
    }

    /// <summary>
    /// Reset to default configuration
    /// </summary>
    [HttpPost("reset")]
    public async Task<IActionResult> ResetToDefaults(CancellationToken ct)
    {
        try
        {
            var defaultProviders = new SaveProviderConfigRequest(new List<ProviderConfigDto>
            {
                CreateDefaultProvider("OpenAI", "LLM", 1),
                CreateDefaultProvider("Anthropic", "LLM", 2),
                CreateDefaultProvider("Google", "LLM", 3),
                CreateDefaultProvider("Ollama", "LLM", 4),
                CreateDefaultProvider("ElevenLabs", "TTS", 1),
                CreateDefaultProvider("PlayHT", "TTS", 2),
                CreateDefaultProvider("Windows", "TTS", 3),
                CreateDefaultProvider("StabilityAI", "Image", 1),
                CreateDefaultProvider("StableDiffusion", "Image", 2),
                CreateDefaultProvider("Stock", "Image", 3)
            });

            var defaultQuality = CreateDefaultQualityConfig();

            await SaveProviderConfigAsync(defaultProviders, ct);
            await SaveQualityConfigAsync(defaultQuality, ct);

            _logger.LogInformation("Configuration reset to defaults");
            return Ok(new { success = true, message = "Configuration reset to defaults" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error resetting configuration");
            return StatusCode(500, new { error = "Failed to reset configuration" });
        }
    }

    private static ProviderConfigDto CreateDefaultProvider(string name, string type, int priority)
    {
        return new ProviderConfigDto(
            name,
            type,
            type == "TTS" && name == "Windows",
            priority,
            null,
            new Dictionary<string, string>(),
            null,
            "Not configured");
    }

    private static QualityConfigDto CreateDefaultQualityConfig()
    {
        return new QualityConfigDto(
            new VideoQualityDto("1080p", 1920, 1080, 30, "High", 5000, "h264", "mp4"),
            new AudioQualityDto(192, 48000, 2, "aac"),
            new SubtitleStyleDto("Arial", 24, "#FFFFFF", "#000000", 0.7, "Bottom", 2, "#000000"));
    }

    private static string GetProviderType(string providerName)
    {
        return providerName.ToLowerInvariant() switch
        {
            "openai" or "anthropic" or "google" or "ollama" => "LLM",
            "elevenlabs" or "playht" or "windows" => "TTS",
            "stabilitya" or "stablediffusion" or "stock" => "Image",
            _ => "Unknown"
        };
    }

    private static bool RequiresApiKey(string providerName)
    {
        return providerName.ToLowerInvariant() switch
        {
            "windows" or "ollama" or "stock" or "stablediffusion" => false,
            _ => true
        };
    }

    private async Task<List<ConfigurationProfileDto>> GetAllProfilesAsync(CancellationToken ct)
    {
        var profilesPath = Path.Combine(_configDir, "profiles");
        Directory.CreateDirectory(profilesPath);

        var profiles = new List<ConfigurationProfileDto>();
        foreach (var file in Directory.GetFiles(profilesPath, "*.json"))
        {
            var json = await System.IO.File.ReadAllTextAsync(file, ct);
            var profile = JsonSerializer.Deserialize<ConfigurationProfileDto>(json);
            if (profile != null)
            {
                profiles.Add(profile);
            }
        }
        return profiles;
    }

    private async Task<SaveProviderConfigRequest> LoadProviderConfigAsync(CancellationToken ct)
    {
        var configPath = Path.Combine(_configDir, "provider-config.json");
        if (!System.IO.File.Exists(configPath))
        {
            return new SaveProviderConfigRequest(new List<ProviderConfigDto>());
        }

        var json = await System.IO.File.ReadAllTextAsync(configPath, ct);
        return JsonSerializer.Deserialize<SaveProviderConfigRequest>(json) 
            ?? new SaveProviderConfigRequest(new List<ProviderConfigDto>());
    }

    private async Task<QualityConfigDto> LoadQualityConfigAsync(CancellationToken ct)
    {
        var configPath = Path.Combine(_configDir, "quality-config.json");
        if (!System.IO.File.Exists(configPath))
        {
            return CreateDefaultQualityConfig();
        }

        var json = await System.IO.File.ReadAllTextAsync(configPath, ct);
        return JsonSerializer.Deserialize<QualityConfigDto>(json) ?? CreateDefaultQualityConfig();
    }

    private async Task SaveProviderConfigAsync(SaveProviderConfigRequest config, CancellationToken ct)
    {
        var configPath = Path.Combine(_configDir, "provider-config.json");
        var json = JsonSerializer.Serialize(config, new JsonSerializerOptions { WriteIndented = true });
        await System.IO.File.WriteAllTextAsync(configPath, json, ct);
    }

    private async Task SaveQualityConfigAsync(QualityConfigDto config, CancellationToken ct)
    {
        var configPath = Path.Combine(_configDir, "quality-config.json");
        var json = JsonSerializer.Serialize(config, new JsonSerializerOptions { WriteIndented = true });
        await System.IO.File.WriteAllTextAsync(configPath, json, ct);
    }
}
