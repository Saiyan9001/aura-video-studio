using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Aura.Core.Models;
using Aura.Core.Providers;
using Microsoft.Extensions.Logging;

namespace Aura.Core.Orchestrator;

/// <summary>
/// Manages provider selection and automatic fallback based on profiles and availability
/// </summary>
public class ProviderMixer
{
    private readonly ILogger<ProviderMixer> _logger;
    private readonly ProviderMixingConfig _config;

    public ProviderMixer(ILogger<ProviderMixer> logger, ProviderMixingConfig config)
    {
        _logger = logger;
        _config = config;
    }

    /// <summary>
    /// Resolves the best available LLM provider based on tier, availability, and offline mode.
    /// This method is deterministic and never throws - always returns a valid ProviderDecision.
    /// 
    /// Fallback chain:
    /// - Pro tier (online): OpenAI → Azure → Gemini → Ollama → RuleBased (guaranteed)
    /// - ProIfAvailable (online): OpenAI → Azure → Gemini → Ollama → RuleBased (guaranteed)
    /// - Pro tier (offline): Error - Pro requires internet
    /// - ProIfAvailable (offline): Ollama → RuleBased (guaranteed)
    /// - Free tier: Ollama → RuleBased (guaranteed)
    /// - Empty providers: RuleBased (guaranteed - never throws)
    /// </summary>
    public ProviderDecision ResolveLlm(
        Dictionary<string, ILlmProvider> availableProviders,
        string preferredTier,
        bool offlineOnly = false)
    {
        var stage = "Script";
        _logger.LogInformation("Resolving LLM provider for {Stage} stage (tier: {Tier}, offlineOnly: {OfflineOnly})", 
            stage, preferredTier, offlineOnly);

        // Build the complete downgrade chain based on tier and offline mode
        var downgradeChain = BuildLlmDowngradeChain(preferredTier, offlineOnly);

        // If offline mode is enabled and Pro tier is requested, we need to handle this specially
        if (offlineOnly && preferredTier == "Pro")
        {
            // Pro tier in offline mode is not allowed - but we still return a decision with empty chain
            return new ProviderDecision
            {
                Stage = stage,
                ProviderName = "None",
                PriorityRank = 0,
                DowngradeChain = downgradeChain,
                Reason = "Pro providers require internet connection but system is in offline-only mode",
                IsFallback = false,
                FallbackFrom = null
            };
        }

        // Find the first available provider in the downgrade chain
        for (int i = 0; i < downgradeChain.Length; i++)
        {
            var providerName = downgradeChain[i];
            
            // Check if provider is available
            if (availableProviders.ContainsKey(providerName))
            {
                var isFallback = i > 0; // It's a fallback if not the first in chain
                var fallbackFrom = isFallback ? string.Join(" → ", downgradeChain[0..i]) : null;
                
                return new ProviderDecision
                {
                    Stage = stage,
                    ProviderName = providerName,
                    PriorityRank = i + 1, // 1-based ranking
                    DowngradeChain = downgradeChain,
                    Reason = isFallback 
                        ? $"Fallback to {providerName} (higher-tier providers unavailable)"
                        : $"{providerName} available and preferred",
                    IsFallback = isFallback,
                    FallbackFrom = fallbackFrom
                };
            }
        }

        // No providers available in dictionary - return RuleBased as guaranteed fallback
        // RuleBased is always available even if not registered
        var guaranteedFallbackRank = downgradeChain.Length;
        return new ProviderDecision
        {
            Stage = stage,
            ProviderName = "RuleBased",
            PriorityRank = guaranteedFallbackRank,
            DowngradeChain = downgradeChain,
            Reason = "RuleBased fallback - guaranteed always-available provider",
            IsFallback = true,
            FallbackFrom = "All providers"
        };
    }

    /// <summary>
    /// Builds the deterministic downgrade chain for LLM providers based on tier and offline mode
    /// </summary>
    private static string[] BuildLlmDowngradeChain(string preferredTier, bool offlineOnly)
    {
        // Normalize tier name
        var tier = (preferredTier ?? "Free").Trim();

        if (offlineOnly)
        {
            // In offline mode, only local providers are allowed
            if (tier == "Pro")
            {
                // Pro tier in offline mode - empty chain since Pro requires internet
                return System.Array.Empty<string>();
            }
            else if (tier == "ProIfAvailable")
            {
                // ProIfAvailable downgrades to Free in offline mode
                return new[] { "Ollama", "RuleBased" };
            }
            else
            {
                // Free tier always uses local providers
                return new[] { "Ollama", "RuleBased" };
            }
        }
        else
        {
            // Online mode - full chain
            if (tier == "Pro" || tier == "ProIfAvailable")
            {
                return new[] { "OpenAI", "Azure", "Gemini", "Ollama", "RuleBased" };
            }
            else if (tier == "Free")
            {
                return new[] { "Ollama", "RuleBased" };
            }
            else
            {
                // Unknown tier or specific provider - use Free tier chain
                return new[] { "Ollama", "RuleBased" };
            }
        }
    }

    /// <summary>
    /// Selects the best available LLM provider based on profile and availability
    /// 
    /// Fallback chain:
    /// - Pro tier: OpenAI → Azure → Gemini → Ollama → RuleBased (guaranteed)
    /// - ProIfAvailable: OpenAI → Azure → Gemini → Ollama → RuleBased (guaranteed)
    /// - Free tier: Ollama → RuleBased (guaranteed)
    /// - Empty providers: RuleBased (guaranteed - never throws)
    /// </summary>
    public ProviderSelection SelectLlmProvider(
        Dictionary<string, ILlmProvider> availableProviders,
        string preferredTier)
    {
        var stage = "Script";
        _logger.LogInformation("Selecting LLM provider for {Stage} stage (preferred: {Tier})", stage, preferredTier);

        // If specific provider is requested (not a tier), try to use it directly
        if (!string.IsNullOrWhiteSpace(preferredTier) && 
            preferredTier != "Pro" && 
            preferredTier != "ProIfAvailable" && 
            preferredTier != "Free")
        {
            // Normalize provider name
            var normalizedName = NormalizeProviderName(preferredTier);
            if (availableProviders.ContainsKey(normalizedName))
            {
                return new ProviderSelection
                {
                    Stage = stage,
                    SelectedProvider = normalizedName,
                    Reason = $"User-selected provider: {normalizedName}",
                    IsFallback = false
                };
            }
            
            _logger.LogWarning("Requested provider {Provider} not available, falling back to tier logic", preferredTier);
        }

        // Try Pro providers first if requested
        if (preferredTier == "Pro" || preferredTier == "ProIfAvailable")
        {
            if (availableProviders.ContainsKey("OpenAI"))
            {
                return new ProviderSelection
                {
                    Stage = stage,
                    SelectedProvider = "OpenAI",
                    Reason = "Pro provider available and preferred",
                    IsFallback = false
                };
            }

            if (availableProviders.ContainsKey("Azure"))
            {
                return new ProviderSelection
                {
                    Stage = stage,
                    SelectedProvider = "Azure",
                    Reason = "Pro provider available and preferred",
                    IsFallback = false
                };
            }

            if (availableProviders.ContainsKey("Gemini"))
            {
                return new ProviderSelection
                {
                    Stage = stage,
                    SelectedProvider = "Gemini",
                    Reason = "Pro provider available and preferred",
                    IsFallback = false
                };
            }

            // If ProIfAvailable and no Pro providers, fall back to free
            if (preferredTier == "ProIfAvailable")
            {
                _logger.LogInformation("No Pro LLM providers available, falling back to free");
            }
            else
            {
                _logger.LogWarning("Pro LLM provider requested but none available");
            }
        }

        // Try free providers
        if (availableProviders.ContainsKey("Ollama"))
        {
            bool isFallback = preferredTier == "Pro" || preferredTier == "ProIfAvailable";
            return new ProviderSelection
            {
                Stage = stage,
                SelectedProvider = "Ollama",
                Reason = "Local Ollama available",
                IsFallback = isFallback,
                FallbackFrom = isFallback ? "Pro LLM" : null
            };
        }

        if (availableProviders.ContainsKey("RuleBased"))
        {
            bool isFallback = preferredTier == "Pro" || preferredTier == "ProIfAvailable";
            return new ProviderSelection
            {
                Stage = stage,
                SelectedProvider = "RuleBased",
                Reason = "Free fallback - always available",
                IsFallback = isFallback,
                FallbackFrom = isFallback ? "Pro/Local LLM" : null
            };
        }

        // RuleBased provider is ALWAYS available as last resort - never throw
        _logger.LogWarning("No LLM providers in registry, returning RuleBased as guaranteed fallback");
        return new ProviderSelection
        {
            Stage = stage,
            SelectedProvider = "RuleBased",
            Reason = "RuleBased fallback - guaranteed always-available provider",
            IsFallback = true,
            FallbackFrom = "All providers"
        };
    }

    /// <summary>
    /// Selects the best available TTS provider based on profile and availability
    /// 
    /// Fallback chain:
    /// - Pro tier: ElevenLabs → OpenAI → PlayHT → Azure → EdgeTTS → Mimic3 → Piper → Windows (guaranteed)
    /// - ProIfAvailable: ElevenLabs → OpenAI → PlayHT → Azure → EdgeTTS → Mimic3 → Piper → Windows (guaranteed)
    /// - Free tier: EdgeTTS → Mimic3 → Piper → Windows (guaranteed)
    /// - Empty providers: Windows (guaranteed - never throws)
    /// </summary>
    public ProviderSelection SelectTtsProvider(
        Dictionary<string, ITtsProvider> availableProviders,
        string preferredTier)
    {
        var stage = "TTS";
        _logger.LogInformation("Selecting TTS provider for {Stage} stage (preferred: {Tier})", stage, preferredTier);

        // If specific provider is requested (not a tier), try to use it directly
        if (!string.IsNullOrWhiteSpace(preferredTier) && 
            preferredTier != "Pro" && 
            preferredTier != "ProIfAvailable" && 
            preferredTier != "Free")
        {
            var normalizedName = NormalizeProviderName(preferredTier);
            if (availableProviders.ContainsKey(normalizedName))
            {
                return new ProviderSelection
                {
                    Stage = stage,
                    SelectedProvider = normalizedName,
                    Reason = $"User-selected provider: {normalizedName}",
                    IsFallback = false
                };
            }
            
            _logger.LogWarning("Requested TTS provider {Provider} not available, falling back to tier logic", preferredTier);
        }

        // Try Pro providers first if requested
        if (preferredTier == "Pro" || preferredTier == "ProIfAvailable")
        {
            if (availableProviders.ContainsKey("ElevenLabs"))
            {
                return new ProviderSelection
                {
                    Stage = stage,
                    SelectedProvider = "ElevenLabs",
                    Reason = "Pro provider available and preferred (premium quality)",
                    IsFallback = false
                };
            }

            if (availableProviders.ContainsKey("OpenAI"))
            {
                return new ProviderSelection
                {
                    Stage = stage,
                    SelectedProvider = "OpenAI",
                    Reason = "Pro provider available (high quality, streaming support)",
                    IsFallback = false
                };
            }

            if (availableProviders.ContainsKey("PlayHT"))
            {
                return new ProviderSelection
                {
                    Stage = stage,
                    SelectedProvider = "PlayHT",
                    Reason = "Pro provider available (voice cloning support)",
                    IsFallback = false
                };
            }

            if (availableProviders.ContainsKey("Azure"))
            {
                return new ProviderSelection
                {
                    Stage = stage,
                    SelectedProvider = "Azure",
                    Reason = "Pro provider available (enterprise grade)",
                    IsFallback = false
                };
            }

            // If ProIfAvailable and no Pro providers, fall back to free/local
            if (preferredTier == "ProIfAvailable")
            {
                _logger.LogInformation("No Pro TTS providers available, falling back to free/local TTS");
            }
            else
            {
                _logger.LogWarning("Pro TTS provider requested but none available");
            }
        }

        // Try free online TTS providers
        if (availableProviders.ContainsKey("EdgeTTS"))
        {
            bool isFallback = preferredTier == "Pro";
            return new ProviderSelection
            {
                Stage = stage,
                SelectedProvider = "EdgeTTS",
                Reason = "Free EdgeTTS available (no API key required, good quality)",
                IsFallback = isFallback,
                FallbackFrom = isFallback ? "Pro TTS" : null
            };
        }

        // Try local TTS providers (offline, high quality)
        if (availableProviders.ContainsKey("Mimic3"))
        {
            bool isFallback = preferredTier == "Pro" || preferredTier == "ProIfAvailable";
            return new ProviderSelection
            {
                Stage = stage,
                SelectedProvider = "Mimic3",
                Reason = "Local Mimic3 TTS available (offline, neural TTS)",
                IsFallback = isFallback,
                FallbackFrom = isFallback ? "Pro/Free TTS" : null
            };
        }

        if (availableProviders.ContainsKey("Piper"))
        {
            bool isFallback = preferredTier == "Pro" || preferredTier == "ProIfAvailable";
            return new ProviderSelection
            {
                Stage = stage,
                SelectedProvider = "Piper",
                Reason = "Local Piper TTS available (offline, fast)",
                IsFallback = isFallback,
                FallbackFrom = isFallback ? "Pro/Free TTS" : null
            };
        }

        // Fall back to Windows TTS (always available on Windows)
        if (availableProviders.ContainsKey("Windows"))
        {
            return new ProviderSelection
            {
                Stage = stage,
                SelectedProvider = "Windows",
                Reason = "Windows TTS - free and always available (system fallback)",
                IsFallback = preferredTier == "Pro" || preferredTier == "ProIfAvailable",
                FallbackFrom = (preferredTier == "Pro" || preferredTier == "ProIfAvailable") ? "Pro/Free/Local TTS" : null
            };
        }

        // Null TTS is ALWAYS available as last resort - never throw (generates silence)
        _logger.LogWarning("No TTS providers in registry, returning Null as guaranteed fallback");
        return new ProviderSelection
        {
            Stage = stage,
            SelectedProvider = "Null",
            Reason = "Null TTS fallback - guaranteed always-available provider (generates silence)",
            IsFallback = true,
            FallbackFrom = "All TTS providers"
        };
    }

    /// <summary>
    /// Selects the best available image/visual provider based on profile and availability
    /// 
    /// Fallback chain:
    /// - Pro tier: DALL-E 3 → StabilityAI → Midjourney → LocalSD (if NVIDIA 6GB+) → Unsplash → Placeholder (guaranteed)
    /// - ProIfAvailable: DALL-E 3 → StabilityAI → Midjourney → LocalSD (if NVIDIA 6GB+) → Unsplash → Placeholder (guaranteed)
    /// - StockOrLocal: LocalSD (if NVIDIA 6GB+) → Unsplash → Placeholder (guaranteed)
    /// - Free tier: LocalSD (if NVIDIA 6GB+) → Unsplash → Placeholder (guaranteed)
    /// - Empty providers: Placeholder (guaranteed - never throws)
    /// </summary>
    public ProviderSelection SelectVisualProvider(
        Dictionary<string, object> availableProviders,
        string preferredTier,
        bool isNvidiaGpu,
        int vramGB)
    {
        var stage = "Visuals";
        _logger.LogInformation("Selecting visual provider for {Stage} stage (preferred: {Tier})", stage, preferredTier);

        // If specific provider is requested (not a tier), try to use it directly
        if (!string.IsNullOrWhiteSpace(preferredTier) && 
            preferredTier != "Pro" && 
            preferredTier != "ProIfAvailable" && 
            preferredTier != "Free" &&
            preferredTier != "StockOrLocal")
        {
            var normalizedName = NormalizeProviderName(preferredTier);
            if (availableProviders.ContainsKey(normalizedName))
            {
                return new ProviderSelection
                {
                    Stage = stage,
                    SelectedProvider = normalizedName,
                    Reason = $"User-selected provider: {normalizedName}",
                    IsFallback = false
                };
            }
            
            _logger.LogWarning("Requested visual provider {Provider} not available, falling back to tier logic", preferredTier);
        }

        // Try Pro providers first if requested
        if (preferredTier == "Pro" || preferredTier == "ProIfAvailable" || preferredTier == "CloudPro")
        {
            if (availableProviders.ContainsKey("DALL-E3"))
            {
                return new ProviderSelection
                {
                    Stage = stage,
                    SelectedProvider = "DALL-E3",
                    Reason = "Pro provider available (OpenAI DALL-E 3, highest quality)",
                    IsFallback = false
                };
            }

            if (availableProviders.ContainsKey("StabilityAI"))
            {
                return new ProviderSelection
                {
                    Stage = stage,
                    SelectedProvider = "StabilityAI",
                    Reason = "Pro provider available (Stability AI, excellent quality)",
                    IsFallback = false
                };
            }

            if (availableProviders.ContainsKey("Midjourney"))
            {
                return new ProviderSelection
                {
                    Stage = stage,
                    SelectedProvider = "Midjourney",
                    Reason = "Pro provider available (Midjourney, artistic quality)",
                    IsFallback = false
                };
            }

            if (availableProviders.ContainsKey("Stability"))
            {
                return new ProviderSelection
                {
                    Stage = stage,
                    SelectedProvider = "Stability",
                    Reason = "Pro provider available and preferred",
                    IsFallback = false
                };
            }

            if (availableProviders.ContainsKey("Runway"))
            {
                return new ProviderSelection
                {
                    Stage = stage,
                    SelectedProvider = "Runway",
                    Reason = "Pro provider available and preferred",
                    IsFallback = false
                };
            }

            if (preferredTier == "ProIfAvailable")
            {
                _logger.LogInformation("No Pro visual providers available, falling back to local/free");
            }
        }

        // Try local SD if available (NVIDIA-only with sufficient VRAM)
        if (preferredTier == "StockOrLocal" || preferredTier == "ProIfAvailable" || preferredTier == "Free")
        {
            if (isNvidiaGpu && vramGB >= 6 && availableProviders.ContainsKey("LocalSD"))
            {
                return new ProviderSelection
                {
                    Stage = stage,
                    SelectedProvider = "LocalSD",
                    Reason = $"Local Stable Diffusion available (NVIDIA GPU with {vramGB}GB VRAM, free)",
                    IsFallback = preferredTier == "Pro",
                    FallbackFrom = preferredTier == "Pro" ? "Pro Visual" : null
                };
            }

            if (isNvidiaGpu && vramGB >= 6 && availableProviders.ContainsKey("StableDiffusion"))
            {
                return new ProviderSelection
                {
                    Stage = stage,
                    SelectedProvider = "StableDiffusion",
                    Reason = $"Local SD available (NVIDIA GPU with {vramGB}GB VRAM)",
                    IsFallback = preferredTier == "Pro",
                    FallbackFrom = preferredTier == "Pro" ? "Pro Visual" : null
                };
            }
        }

        // Fall back to stock images (Unsplash)
        if (availableProviders.ContainsKey("Unsplash"))
        {
            return new ProviderSelection
            {
                Stage = stage,
                SelectedProvider = "Unsplash",
                Reason = "Free Unsplash stock photos",
                IsFallback = preferredTier == "Pro" || preferredTier == "StockOrLocal",
                FallbackFrom = preferredTier != "Free" ? "Pro/Local Visual" : null
            };
        }

        if (availableProviders.ContainsKey("Stock"))
        {
            return new ProviderSelection
            {
                Stage = stage,
                SelectedProvider = "Stock",
                Reason = "Free stock images",
                IsFallback = preferredTier == "Pro" || preferredTier == "StockOrLocal",
                FallbackFrom = preferredTier != "Stock" ? "Pro/Local Visual" : null
            };
        }

        // Ultimate fallback: Placeholder (guaranteed to always succeed)
        if (availableProviders.ContainsKey("Placeholder"))
        {
            return new ProviderSelection
            {
                Stage = stage,
                SelectedProvider = "Placeholder",
                Reason = "Placeholder provider - guaranteed fallback (always available)",
                IsFallback = true,
                FallbackFrom = "All visual providers"
            };
        }

        // Slideshow fallback for backward compatibility
        return new ProviderSelection
        {
            Stage = stage,
            SelectedProvider = "Slideshow",
            Reason = "Fallback to slideshow (no other providers available)",
            IsFallback = true,
            FallbackFrom = "All visual providers"
        };
    }

    /// <summary>
    /// Logs a provider selection decision
    /// </summary>
    public void LogSelection(ProviderSelection selection)
    {
        if (!_config.LogProviderSelection)
            return;

        if (selection.IsFallback)
        {
            _logger.LogWarning(
                "[{Stage}] Provider: {Provider} (FALLBACK from {FallbackFrom}) - {Reason}",
                selection.Stage,
                selection.SelectedProvider,
                selection.FallbackFrom,
                selection.Reason
            );
        }
        else
        {
            _logger.LogInformation(
                "[{Stage}] Provider: {Provider} - {Reason}",
                selection.Stage,
                selection.SelectedProvider,
                selection.Reason
            );
        }
    }

    /// <summary>
    /// Logs a provider decision
    /// </summary>
    public void LogDecision(ProviderDecision decision)
    {
        if (!_config.LogProviderSelection)
            return;

        if (decision.IsFallback)
        {
            _logger.LogWarning(
                "[{Stage}] Provider: {Provider} (Rank {Rank}, FALLBACK from {FallbackFrom}) - {Reason}. Chain: [{Chain}]",
                decision.Stage,
                decision.ProviderName,
                decision.PriorityRank,
                decision.FallbackFrom,
                decision.Reason,
                string.Join(" → ", decision.DowngradeChain)
            );
        }
        else
        {
            _logger.LogInformation(
                "[{Stage}] Provider: {Provider} (Rank {Rank}) - {Reason}. Chain: [{Chain}]",
                decision.Stage,
                decision.ProviderName,
                decision.PriorityRank,
                decision.Reason,
                string.Join(" → ", decision.DowngradeChain)
            );
        }
    }

    /// <summary>
    /// Normalizes provider names to match DI registration keys
    /// </summary>
    private static string NormalizeProviderName(string name)
    {
        return name switch
        {
            // LLM providers
            "RuleBased" or "rulebased" or "Rule-Based" or "rule-based" => "RuleBased",
            "Ollama" or "ollama" => "Ollama",
            "OpenAI" or "openai" or "OpenAi" => "OpenAI",
            "AzureOpenAI" or "Azure" or "azure" or "AzureOpenAi" or "azureopenai" => "Azure",
            "Gemini" or "gemini" => "Gemini",
            
            // TTS providers
            "Windows" or "windows" or "Windows SAPI" or "WindowsSAPI" or "SAPI" or "System" or "system" => "Windows",
            "ElevenLabs" or "elevenlabs" or "Eleven" or "eleven" => "ElevenLabs",
            "OpenAI" or "openai" or "OpenAI-TTS" or "OpenAiTts" => "OpenAI",
            "PlayHT" or "playht" or "Play.ht" or "PlayHt" => "PlayHT",
            "EdgeTTS" or "edgetts" or "Edge" or "edge" or "EdgeTts" => "EdgeTTS",
            "Piper" or "piper" => "Piper",
            "Mimic3" or "mimic3" or "Mimic" or "mimic" => "Mimic3",
            
            // Visual providers
            "Stock" or "stock" => "Stock",
            "LocalSD" or "localsd" or "StableDiffusion" or "stablediffusion" or "SD" => "LocalSD",
            "CloudPro" or "cloudpro" or "Cloud" or "cloud" => "CloudPro",
            "Stability" or "stability" or "StabilityAI" or "StabilityAi" => "StabilityAI",
            "Runway" or "runway" or "RunwayML" or "RunwayMl" => "Runway",
            "Slideshow" or "slideshow" => "Slideshow",
            "DALL-E3" or "DALL-E 3" or "dalle3" or "dalle-3" or "DallE3" => "DALL-E3",
            "Midjourney" or "midjourney" or "MJ" or "mj" => "Midjourney",
            "Unsplash" or "unsplash" => "Unsplash",
            "Placeholder" or "placeholder" => "Placeholder",
            
            // Default: return as-is
            _ => name
        };
    }
}
