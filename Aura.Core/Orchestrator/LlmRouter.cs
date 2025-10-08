using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Aura.Core.Models;
using Aura.Core.Providers;
using Microsoft.Extensions.Logging;

namespace Aura.Core.Orchestrator;

/// <summary>
/// Routes LLM requests through available providers with automatic fallback
/// </summary>
public class LlmRouter
{
    private readonly ILogger<LlmRouter> _logger;
    private readonly ProviderMixer _mixer;
    private readonly ProviderMixingConfig _config;

    public LlmRouter(ILogger<LlmRouter> logger, ProviderMixer mixer, ProviderMixingConfig config)
    {
        _logger = logger;
        _mixer = mixer;
        _config = config;
    }

    /// <summary>
    /// Generates a script using the best available provider with automatic fallback
    /// </summary>
    public async Task<string> GenerateScriptAsync(
        Dictionary<string, ILlmProvider> availableProviders,
        Brief brief,
        PlanSpec spec,
        string preferredTier,
        CancellationToken ct)
    {
        if (availableProviders.Count == 0)
        {
            throw new Exception("No LLM providers available");
        }

        var attemptedProviders = new List<string>();
        Exception? lastException = null;

        // Create an ordered list of providers to try
        var providerOrder = GetProviderOrder(availableProviders, preferredTier);

        foreach (var providerName in providerOrder)
        {
            if (!availableProviders.TryGetValue(providerName, out var provider))
                continue;

            attemptedProviders.Add(providerName);
            var isFallback = attemptedProviders.Count > 1;

            try
            {
                if (isFallback)
                {
                    _logger.LogWarning(
                        "Falling back to {Provider} after failures: {AttemptedProviders}",
                        providerName,
                        string.Join(", ", attemptedProviders.Take(attemptedProviders.Count - 1)));
                }
                else
                {
                    _logger.LogInformation("Using {Provider} for script generation", providerName);
                }

                var script = await provider.DraftScriptAsync(brief, spec, ct);

                if (!string.IsNullOrWhiteSpace(script))
                {
                    _logger.LogInformation(
                        "Script generated successfully using {Provider} ({Length} characters)",
                        providerName,
                        script.Length);
                    return script;
                }

                _logger.LogWarning("{Provider} returned empty script, trying next provider", providerName);
                lastException = new Exception($"{providerName} returned empty script");
            }
            catch (Exception ex)
            {
                lastException = ex;
                _logger.LogWarning(
                    ex,
                    "{Provider} failed: {Message}. Will try next provider if available.",
                    providerName,
                    ex.Message);
            }
        }

        // All providers failed
        _logger.LogError(
            lastException,
            "All LLM providers failed. Attempted: {AttemptedProviders}",
            string.Join(", ", attemptedProviders));

        throw new Exception(
            $"Script generation failed. Attempted providers: {string.Join(", ", attemptedProviders)}. Last error: {lastException?.Message}",
            lastException);
    }

    private List<string> GetProviderOrder(Dictionary<string, ILlmProvider> availableProviders, string preferredTier)
    {
        var order = new List<string>();

        // Determine priority based on preferred tier
        if (preferredTier == "Pro" || preferredTier == "ProIfAvailable")
        {
            // Try Pro providers first
            if (availableProviders.ContainsKey("OpenAI"))
                order.Add("OpenAI");
            if (availableProviders.ContainsKey("Azure"))
                order.Add("Azure");
            if (availableProviders.ContainsKey("Gemini"))
                order.Add("Gemini");
        }

        // Then try free/local providers
        if (availableProviders.ContainsKey("Ollama"))
            order.Add("Ollama");

        // RuleBased is always the final fallback
        if (availableProviders.ContainsKey("RuleBased"))
            order.Add("RuleBased");

        return order;
    }
}
