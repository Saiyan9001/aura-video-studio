using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Aura.Core.Configuration;
using Aura.Core.Orchestrator;
using Aura.Core.Providers;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;

namespace Aura.Api.HealthChecks;

/// <summary>
/// Health check that validates provider configuration and availability
/// Checks that at least one provider of each type (LLM, TTS, Video) is properly configured
/// </summary>
public class ProviderHealthCheck : IHealthCheck
{
    private readonly ILogger<ProviderHealthCheck> _logger;
    private readonly ProviderSettings _providerSettings;
    private readonly LlmProviderFactory _llmProviderFactory;
    private readonly ILoggerFactory _loggerFactory;
    private readonly IEnumerable<ITtsProvider> _ttsProviders;
    private readonly IVideoComposer _videoComposer;

    public ProviderHealthCheck(
        ILogger<ProviderHealthCheck> logger,
        ProviderSettings providerSettings,
        LlmProviderFactory llmProviderFactory,
        ILoggerFactory loggerFactory,
        IEnumerable<ITtsProvider> ttsProviders,
        IVideoComposer videoComposer)
    {
        _logger = logger;
        _providerSettings = providerSettings;
        _llmProviderFactory = llmProviderFactory;
        _loggerFactory = loggerFactory;
        _ttsProviders = ttsProviders;
        _videoComposer = videoComposer;
    }

    public Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {        
        try
        {
            var data = new Dictionary<string, object>();
            var warnings = new List<string>();

            var llmProviders = _llmProviderFactory.CreateAvailableProviders(_loggerFactory);
            var ttsProvidersFiltered = _ttsProviders.Where(p => p != null).ToList();

            data["llm_providers_available"] = llmProviders.Count;
            data["tts_providers_available"] = ttsProvidersFiltered.Count;
            data["video_composer_available"] = _videoComposer != null;

            if (llmProviders.Count > 0)
            {
                data["llm_providers"] = llmProviders.Keys.ToArray();
            }

            if (ttsProvidersFiltered.Count > 0)
            {
                data["tts_providers"] = ttsProvidersFiltered
                    .Select(p => p.GetType().Name.Replace("Provider", ""))
                    .ToArray();
            }

            CheckApiKeys(data, warnings);

            if (llmProviders.Count == 0)
            {
                warnings.Add("No LLM providers configured - script generation will not work");
            }

            if (ttsProvidersFiltered.Count == 0)
            {
                warnings.Add("No TTS providers configured - audio generation will not work");
            }

            if (_videoComposer == null)
            {
                return Task.FromResult(HealthCheckResult.Unhealthy(
                    "Video composer not available - critical for rendering",
                    data: data));
            }

            if (warnings.Count > 0)
            {
                data["warnings"] = warnings.ToArray();
                return Task.FromResult(HealthCheckResult.Degraded(
                    $"Provider configuration has {warnings.Count} warning(s)",
                    data: data));
            }

            return Task.FromResult(HealthCheckResult.Healthy(
                "All provider types are properly configured",
                data: data));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking provider health");
            return Task.FromResult(HealthCheckResult.Unhealthy(
                "Error checking provider configuration",
                exception: ex));
        }
    }

    private void CheckApiKeys(Dictionary<string, object> data, List<string> warnings)
    {
        var apiKeyStatus = new Dictionary<string, bool>();

        apiKeyStatus["openai"] = !string.IsNullOrWhiteSpace(_providerSettings.GetOpenAiApiKey());
        apiKeyStatus["azure_openai"] = !string.IsNullOrWhiteSpace(_providerSettings.GetAzureOpenAiApiKey());
        apiKeyStatus["gemini"] = !string.IsNullOrWhiteSpace(_providerSettings.GetGeminiApiKey());
        apiKeyStatus["elevenlabs"] = !string.IsNullOrWhiteSpace(_providerSettings.GetElevenLabsApiKey());
        apiKeyStatus["playht"] = !string.IsNullOrWhiteSpace(_providerSettings.GetPlayHTApiKey());
        apiKeyStatus["azure_speech"] = !string.IsNullOrWhiteSpace(_providerSettings.GetAzureSpeechKey());

        data["api_keys_configured"] = apiKeyStatus;

        var configuredCount = apiKeyStatus.Count(kv => kv.Value);
        data["api_keys_count"] = configuredCount;

        if (configuredCount == 0)
        {
            warnings.Add("No API keys configured - only offline providers will work");
        }
    }
}
