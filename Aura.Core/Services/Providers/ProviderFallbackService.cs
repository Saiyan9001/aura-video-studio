using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Aura.Core.Models;
using Aura.Core.Providers;
using Microsoft.Extensions.Logging;

namespace Aura.Core.Services.Providers;

/// <summary>
/// Service that orchestrates automatic fallback between providers when primary providers fail.
/// Ensures seamless degradation to offline mode when online providers are unavailable.
/// </summary>
public class ProviderFallbackService
{
    private readonly ILogger<ProviderFallbackService> _logger;
    private readonly ProviderStatusService _statusService;
    private readonly ProviderCircuitBreakerService? _circuitBreakerService;

    public ProviderFallbackService(
        ILogger<ProviderFallbackService> logger,
        ProviderStatusService statusService,
        ProviderCircuitBreakerService? circuitBreakerService = null)
    {
        _logger = logger;
        _statusService = statusService;
        _circuitBreakerService = circuitBreakerService;
    }

    /// <summary>
    /// Execute an LLM operation with automatic fallback through provider chain
    /// </summary>
    public async Task<T> ExecuteWithLlmFallbackAsync<T>(
        Func<ILlmProvider, Task<T>> operation,
        IEnumerable<ILlmProvider> providers,
        CancellationToken ct = default)
    {
        _logger.LogInformation("Executing LLM operation with fallback");

        var providerList = providers.ToList();
        var exceptions = new List<Exception>();

        foreach (var provider in providerList)
        {
            var providerName = provider.GetType().Name;

            if (_circuitBreakerService != null)
            {
                var status = _circuitBreakerService.GetStatus(providerName);
                if (status.State == CircuitState.Open)
                {
                    _logger.LogWarning("Skipping {Provider} - circuit breaker is open", providerName);
                    continue;
                }
            }

            try
            {
                _logger.LogInformation("Attempting operation with {Provider}", providerName);
                var result = await operation(provider);
                
                _logger.LogInformation("Operation succeeded with {Provider}", providerName);
                return result;
            }
            catch (OperationCanceledException)
            {
                _logger.LogWarning("Operation cancelled");
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Operation failed with {Provider}: {Message}", providerName, ex.Message);
                exceptions.Add(ex);

                if (_circuitBreakerService != null)
                {
                    _circuitBreakerService.RecordFailure(providerName);
                }
            }
        }

        _logger.LogError("All {Count} LLM providers failed", providerList.Count);
        throw new AggregateException(
            $"All {providerList.Count} LLM providers failed. Enable offline mode or configure additional providers.",
            exceptions);
    }

    /// <summary>
    /// Get recommended provider chain for LLM operations based on current status
    /// </summary>
    public async Task<List<string>> GetLlmProviderChainAsync(CancellationToken ct = default)
    {
        var status = await _statusService.GetAllProviderStatusAsync(ct);
        var chain = new List<string>();

        var llmProviders = status.Providers
            .Where(p => p.Category == "LLM" && p.IsAvailable)
            .OrderByDescending(p => p.IsOnline ? 1 : 0)
            .ThenBy(p => GetProviderPriority(p.Name))
            .ToList();

        chain.AddRange(llmProviders.Select(p => p.Name));

        if (!chain.Contains("RuleBased"))
        {
            chain.Add("RuleBased");
        }

        _logger.LogInformation("LLM provider chain: {Chain}", string.Join(" → ", chain));
        return chain;
    }

    /// <summary>
    /// Get recommended provider chain for TTS operations based on current status
    /// </summary>
    public async Task<List<string>> GetTtsProviderChainAsync(CancellationToken ct = default)
    {
        var status = await _statusService.GetAllProviderStatusAsync(ct);
        var chain = new List<string>();

        var ttsProviders = status.Providers
            .Where(p => p.Category == "TTS" && p.IsAvailable)
            .OrderByDescending(p => p.IsOnline ? 1 : 0)
            .ThenBy(p => GetProviderPriority(p.Name))
            .ToList();

        chain.AddRange(ttsProviders.Select(p => p.Name));

        _logger.LogInformation("TTS provider chain: {Chain}", string.Join(" → ", chain));
        return chain;
    }

    /// <summary>
    /// Get recommended provider chain for image operations based on current status
    /// </summary>
    public async Task<List<string>> GetImageProviderChainAsync(CancellationToken ct = default)
    {
        var status = await _statusService.GetAllProviderStatusAsync(ct);
        var chain = new List<string>();

        var imageProviders = status.Providers
            .Where(p => p.Category == "Images" && p.IsAvailable)
            .OrderByDescending(p => p.IsOnline ? 1 : 0)
            .ThenBy(p => GetProviderPriority(p.Name))
            .ToList();

        chain.AddRange(imageProviders.Select(p => p.Name));

        if (!chain.Contains("PlaceholderImages"))
        {
            chain.Add("PlaceholderImages");
        }

        _logger.LogInformation("Image provider chain: {Chain}", string.Join(" → ", chain));
        return chain;
    }

    /// <summary>
    /// Check if system should operate in offline mode
    /// </summary>
    public async Task<OfflineModeInfo> GetOfflineModeInfoAsync(CancellationToken ct = default)
    {
        var status = await _statusService.GetAllProviderStatusAsync(ct);
        
        return new OfflineModeInfo
        {
            IsOfflineMode = status.IsOfflineMode,
            HasOnlineProviders = status.OnlineProvidersCount > 0,
            HasOfflineProviders = status.OfflineProvidersCount > 0,
            AvailableFeatures = status.AvailableFeatures,
            DegradedFeatures = status.DegradedFeatures,
            Message = status.Message,
            Recommendation = GetRecommendation(status)
        };
    }

    private string GetRecommendation(SystemProviderStatus status)
    {
        if (status.IsOfflineMode)
        {
            return "Configure online providers (OpenAI, ElevenLabs, etc.) for enhanced capabilities, or continue using offline mode with template-based generation.";
        }

        if (status.DegradedFeatures.Any())
        {
            return "Some features are running in degraded mode. Configure additional providers for full functionality.";
        }

        return "All systems operational. Online and offline providers available.";
    }

    private int GetProviderPriority(string providerName)
    {
        return providerName switch
        {
            "OpenAI" => 1,
            "Anthropic Claude" => 2,
            "Google Gemini" => 3,
            "Ollama (Local)" => 4,
            "ElevenLabs" => 1,
            "PlayHT" => 2,
            "Piper TTS" => 3,
            "Mimic3" => 4,
            "Windows SAPI" => 5,
            "Stable Diffusion" => 1,
            "PlaceholderImages" => 10,
            "RuleBased" => 10,
            _ => 99
        };
    }
}

/// <summary>
/// Information about offline mode status
/// </summary>
public class OfflineModeInfo
{
    public bool IsOfflineMode { get; set; }
    public bool HasOnlineProviders { get; set; }
    public bool HasOfflineProviders { get; set; }
    public List<string> AvailableFeatures { get; set; } = new();
    public List<string> DegradedFeatures { get; set; } = new();
    public string Message { get; set; } = string.Empty;
    public string Recommendation { get; set; } = string.Empty;
}
