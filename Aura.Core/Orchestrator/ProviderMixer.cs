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
    /// Selects the best available LLM provider based on profile and availability
    /// </summary>
    public ProviderSelection SelectLlmProvider(
        Dictionary<string, ILlmProvider> availableProviders,
        string preferredTier)
    {
        var stage = "Script";
        _logger.LogInformation("Selecting LLM provider for {Stage} stage (preferred: {Tier})", stage, preferredTier);

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

        throw new Exception("No LLM providers available");
    }

    /// <summary>
    /// Selects the best available TTS provider based on profile and availability
    /// </summary>
    public ProviderSelection SelectTtsProvider(
        Dictionary<string, ITtsProvider> availableProviders,
        string preferredTier)
    {
        var stage = "TTS";
        _logger.LogInformation("Selecting TTS provider for {Stage} stage (preferred: {Tier})", stage, preferredTier);

        // Try Pro providers first if requested
        if (preferredTier == "Pro" || preferredTier == "ProIfAvailable")
        {
            if (availableProviders.ContainsKey("ElevenLabs"))
            {
                return new ProviderSelection
                {
                    Stage = stage,
                    SelectedProvider = "ElevenLabs",
                    Reason = "Pro provider available and preferred",
                    IsFallback = false
                };
            }

            if (availableProviders.ContainsKey("PlayHT"))
            {
                return new ProviderSelection
                {
                    Stage = stage,
                    SelectedProvider = "PlayHT",
                    Reason = "Pro provider available and preferred",
                    IsFallback = false
                };
            }

            // If ProIfAvailable and no Pro providers, fall back to free
            if (preferredTier == "ProIfAvailable")
            {
                _logger.LogInformation("No Pro TTS providers available, falling back to Windows TTS");
            }
            else
            {
                _logger.LogWarning("Pro TTS provider requested but none available");
            }
        }

        // Fall back to Windows TTS (always available)
        if (availableProviders.ContainsKey("Windows"))
        {
            return new ProviderSelection
            {
                Stage = stage,
                SelectedProvider = "Windows",
                Reason = "Windows TTS - free and always available",
                IsFallback = preferredTier == "Pro",
                FallbackFrom = preferredTier == "Pro" ? "Pro TTS" : null
            };
        }
        
        // Linux Mock TTS (for CI environments without Windows)
        if (availableProviders.ContainsKey("LinuxMock"))
        {
            return new ProviderSelection
            {
                Stage = stage,
                SelectedProvider = "LinuxMock",
                Reason = "Linux Mock TTS - fallback for non-Windows platforms",
                IsFallback = true,
                FallbackFrom = "Windows TTS"
            };
        }

        throw new Exception("No TTS providers available");
    }

    /// <summary>
    /// Selects the best available image/visual provider based on profile and availability
    /// </summary>
    public ProviderSelection SelectVisualProvider(
        Dictionary<string, object> availableProviders,
        string preferredTier,
        bool isNvidiaGpu,
        int vramGB)
    {
        var stage = "Visuals";
        _logger.LogInformation("Selecting visual provider for {Stage} stage (preferred: {Tier})", stage, preferredTier);

        // Try Pro providers first if requested
        if (preferredTier == "Pro" || preferredTier == "ProIfAvailable")
        {
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
                _logger.LogInformation("No Pro visual providers available, falling back to local/stock");
            }
        }

        // Try local SD if available (NVIDIA-only with sufficient VRAM)
        if (preferredTier == "StockOrLocal" || preferredTier == "ProIfAvailable")
        {
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

        // Fall back to stock images
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

        // Ultimate fallback: slideshow/solid colors
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
}
