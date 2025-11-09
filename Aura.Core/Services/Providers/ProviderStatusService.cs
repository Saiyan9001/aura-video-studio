using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Aura.Core.Services;
using Microsoft.Extensions.Logging;

namespace Aura.Core.Services.Providers;

/// <summary>
/// Service to track real-time status of all providers and determine system capabilities.
/// Detects offline mode and provides comprehensive provider availability information.
/// </summary>
public class ProviderStatusService
{
    private readonly ILogger<ProviderStatusService> _logger;
    private readonly OfflineProviderAvailabilityService _offlineAvailability;
    private readonly ProviderHealthMonitoringService _healthMonitoring;
    private readonly ConcurrentDictionary<string, ProviderStatus> _statusCache;
    private DateTime _lastUpdate;
    private readonly TimeSpan _cacheExpiration = TimeSpan.FromSeconds(30);

    public ProviderStatusService(
        ILogger<ProviderStatusService> logger,
        OfflineProviderAvailabilityService offlineAvailability,
        ProviderHealthMonitoringService healthMonitoring)
    {
        _logger = logger;
        _offlineAvailability = offlineAvailability;
        _healthMonitoring = healthMonitoring;
        _statusCache = new ConcurrentDictionary<string, ProviderStatus>();
        _lastUpdate = DateTime.MinValue;
    }

    /// <summary>
    /// Get comprehensive status of all providers in the system
    /// </summary>
    public async Task<SystemProviderStatus> GetAllProviderStatusAsync(CancellationToken ct = default)
    {
        if (DateTime.UtcNow - _lastUpdate < _cacheExpiration && _statusCache.Any())
        {
            return BuildSystemStatus();
        }

        await RefreshStatusAsync(ct);
        return BuildSystemStatus();
    }

    /// <summary>
    /// Refresh provider status cache
    /// </summary>
    public async Task RefreshStatusAsync(CancellationToken ct = default)
    {
        _logger.LogInformation("Refreshing provider status");

        try
        {
            var offlineStatus = await _offlineAvailability.CheckAllProvidersAsync(ct);
            var healthMetrics = _healthMonitoring.GetAllProviderHealth();

            _statusCache.Clear();

            AddLlmProviderStatus();
            AddTtsProviderStatus(offlineStatus);
            AddImageProviderStatus(offlineStatus);
            AddMusicProviderStatus();

            _lastUpdate = DateTime.UtcNow;

            _logger.LogInformation("Provider status refreshed: {Count} providers tracked", _statusCache.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error refreshing provider status");
        }
    }

    /// <summary>
    /// Check if system is in offline mode (no online providers available)
    /// </summary>
    public async Task<bool> IsOfflineModeAsync(CancellationToken ct = default)
    {
        var status = await GetAllProviderStatusAsync(ct);
        return status.IsOfflineMode;
    }

    /// <summary>
    /// Get features that are available in current provider configuration
    /// </summary>
    public async Task<List<string>> GetAvailableFeaturesAsync(CancellationToken ct = default)
    {
        var status = await GetAllProviderStatusAsync(ct);
        return status.AvailableFeatures;
    }

    /// <summary>
    /// Get features that are degraded or unavailable
    /// </summary>
    public async Task<List<string>> GetDegradedFeaturesAsync(CancellationToken ct = default)
    {
        var status = await GetAllProviderStatusAsync(ct);
        return status.DegradedFeatures;
    }

    private void AddLlmProviderStatus()
    {
        var hasOpenAI = !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("OPENAI_API_KEY"));
        var hasAnthropic = !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("ANTHROPIC_API_KEY"));
        var hasGemini = !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("GEMINI_API_KEY"));
        var hasAzureOpenAI = !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("AZURE_OPENAI_API_KEY"));

        _statusCache.TryAdd("OpenAI", new ProviderStatus
        {
            Name = "OpenAI",
            Category = "LLM",
            IsAvailable = hasOpenAI,
            IsOnline = hasOpenAI,
            Tier = "Premium",
            Features = new List<string> { "Script Generation", "Scene Analysis", "Visual Prompts" },
            Message = hasOpenAI ? "Available" : "API key not configured"
        });

        _statusCache.TryAdd("Anthropic", new ProviderStatus
        {
            Name = "Anthropic Claude",
            Category = "LLM",
            IsAvailable = hasAnthropic,
            IsOnline = hasAnthropic,
            Tier = "Premium",
            Features = new List<string> { "Script Generation", "Scene Analysis", "Visual Prompts" },
            Message = hasAnthropic ? "Available" : "API key not configured"
        });

        _statusCache.TryAdd("Gemini", new ProviderStatus
        {
            Name = "Google Gemini",
            Category = "LLM",
            IsAvailable = hasGemini,
            IsOnline = hasGemini,
            Tier = "Free",
            Features = new List<string> { "Script Generation", "Scene Analysis" },
            Message = hasGemini ? "Available" : "API key not configured"
        });

        _statusCache.TryAdd("AzureOpenAI", new ProviderStatus
        {
            Name = "Azure OpenAI",
            Category = "LLM",
            IsAvailable = hasAzureOpenAI,
            IsOnline = hasAzureOpenAI,
            Tier = "Premium",
            Features = new List<string> { "Script Generation", "Scene Analysis", "Visual Prompts" },
            Message = hasAzureOpenAI ? "Available" : "API key not configured"
        });

        _statusCache.TryAdd("Ollama", new ProviderStatus
        {
            Name = "Ollama (Local)",
            Category = "LLM",
            IsAvailable = true,
            IsOnline = false,
            Tier = "Free",
            Features = new List<string> { "Script Generation", "Scene Analysis" },
            Message = "Local models - always available when installed"
        });

        _statusCache.TryAdd("RuleBased", new ProviderStatus
        {
            Name = "Rule-Based (Offline)",
            Category = "LLM",
            IsAvailable = true,
            IsOnline = false,
            Tier = "Free",
            Features = new List<string> { "Basic Script Generation", "Template-Based Content" },
            Message = "Always available - no dependencies"
        });
    }

    private void AddTtsProviderStatus(OfflineProvidersStatus offlineStatus)
    {
        var hasElevenLabs = !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("ELEVENLABS_API_KEY"));
        var hasPlayHT = !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("PLAYHT_API_KEY"));

        _statusCache.TryAdd("ElevenLabs", new ProviderStatus
        {
            Name = "ElevenLabs",
            Category = "TTS",
            IsAvailable = hasElevenLabs,
            IsOnline = hasElevenLabs,
            Tier = "Premium",
            Features = new List<string> { "High-Quality Voices", "Voice Cloning", "Emotional Control" },
            Message = hasElevenLabs ? "Available" : "API key not configured"
        });

        _statusCache.TryAdd("PlayHT", new ProviderStatus
        {
            Name = "PlayHT",
            Category = "TTS",
            IsAvailable = hasPlayHT,
            IsOnline = hasPlayHT,
            Tier = "Premium",
            Features = new List<string> { "High-Quality Voices", "Voice Cloning" },
            Message = hasPlayHT ? "Available" : "API key not configured"
        });

        _statusCache.TryAdd("Piper", new ProviderStatus
        {
            Name = "Piper TTS",
            Category = "TTS",
            IsAvailable = offlineStatus.Piper.IsAvailable,
            IsOnline = false,
            Tier = "Free",
            Features = new List<string> { "Neural TTS", "Multiple Languages", "Offline" },
            Message = offlineStatus.Piper.Message
        });

        _statusCache.TryAdd("Mimic3", new ProviderStatus
        {
            Name = "Mimic3",
            Category = "TTS",
            IsAvailable = offlineStatus.Mimic3.IsAvailable,
            IsOnline = false,
            Tier = "Free",
            Features = new List<string> { "Neural TTS", "Offline" },
            Message = offlineStatus.Mimic3.Message
        });

        _statusCache.TryAdd("WindowsTTS", new ProviderStatus
        {
            Name = "Windows SAPI",
            Category = "TTS",
            IsAvailable = offlineStatus.WindowsTts.IsAvailable,
            IsOnline = false,
            Tier = "Free",
            Features = new List<string> { "System Voices", "Always Available" },
            Message = offlineStatus.WindowsTts.Message
        });
    }

    private void AddImageProviderStatus(OfflineProvidersStatus offlineStatus)
    {
        var hasStableDiffusion = offlineStatus.StableDiffusion.IsAvailable;

        _statusCache.TryAdd("StableDiffusion", new ProviderStatus
        {
            Name = "Stable Diffusion",
            Category = "Images",
            IsAvailable = hasStableDiffusion,
            IsOnline = false,
            Tier = "Free",
            Features = new List<string> { "AI Image Generation", "Custom Models", "Offline" },
            Message = offlineStatus.StableDiffusion.Message
        });

        _statusCache.TryAdd("PlaceholderImages", new ProviderStatus
        {
            Name = "Placeholder Images",
            Category = "Images",
            IsAvailable = true,
            IsOnline = false,
            Tier = "Free",
            Features = new List<string> { "Colored Cards", "Text Overlays", "Always Available" },
            Message = "Fallback image generation - always available"
        });
    }

    private void AddMusicProviderStatus()
    {
        _statusCache.TryAdd("StockMusic", new ProviderStatus
        {
            Name = "Stock Music Library",
            Category = "Music",
            IsAvailable = true,
            IsOnline = false,
            Tier = "Free",
            Features = new List<string> { "Background Music", "Sound Effects" },
            Message = "Built-in library available"
        });
    }

    private SystemProviderStatus BuildSystemStatus()
    {
        var providers = _statusCache.Values.ToList();
        var onlineProviders = providers.Where(p => p.IsOnline && p.IsAvailable).ToList();
        var offlineProviders = providers.Where(p => !p.IsOnline && p.IsAvailable).ToList();

        var isOfflineMode = !onlineProviders.Any();

        var availableFeatures = new List<string>();
        var degradedFeatures = new List<string>();

        var hasAnyLlm = providers.Any(p => p.Category == "LLM" && p.IsAvailable);
        var hasOnlineLlm = providers.Any(p => p.Category == "LLM" && p.IsOnline && p.IsAvailable);
        var hasAnyTts = providers.Any(p => p.Category == "TTS" && p.IsAvailable);
        var hasOnlineTts = providers.Any(p => p.Category == "TTS" && p.IsOnline && p.IsAvailable);
        var hasAnyImages = providers.Any(p => p.Category == "Images" && p.IsAvailable);

        if (hasAnyLlm)
        {
            availableFeatures.Add("Script Generation");
            if (!hasOnlineLlm)
            {
                degradedFeatures.Add("Script Generation (template-based only)");
            }
        }

        if (hasAnyTts)
        {
            availableFeatures.Add("Text-to-Speech");
            if (!hasOnlineTts)
            {
                degradedFeatures.Add("Text-to-Speech (local voices only)");
            }
        }

        if (hasAnyImages)
        {
            availableFeatures.Add("Visual Content");
        }

        availableFeatures.Add("Video Rendering");
        availableFeatures.Add("Timeline Editing");
        availableFeatures.Add("Subtitle Generation");

        return new SystemProviderStatus
        {
            IsOfflineMode = isOfflineMode,
            Providers = providers,
            OnlineProvidersCount = onlineProviders.Count,
            OfflineProvidersCount = offlineProviders.Count,
            AvailableFeatures = availableFeatures,
            DegradedFeatures = degradedFeatures,
            LastUpdated = _lastUpdate,
            Message = isOfflineMode
                ? "Running in offline mode - using local providers and templates"
                : $"{onlineProviders.Count} online providers available"
        };
    }
}

/// <summary>
/// Status information for a single provider
/// </summary>
public class ProviderStatus
{
    public string Name { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public bool IsAvailable { get; set; }
    public bool IsOnline { get; set; }
    public string Tier { get; set; } = string.Empty;
    public List<string> Features { get; set; } = new();
    public string Message { get; set; } = string.Empty;
}

/// <summary>
/// Overall system provider status
/// </summary>
public class SystemProviderStatus
{
    public bool IsOfflineMode { get; set; }
    public List<ProviderStatus> Providers { get; set; } = new();
    public int OnlineProvidersCount { get; set; }
    public int OfflineProvidersCount { get; set; }
    public List<string> AvailableFeatures { get; set; } = new();
    public List<string> DegradedFeatures { get; set; } = new();
    public DateTime LastUpdated { get; set; }
    public string Message { get; set; } = string.Empty;
}
