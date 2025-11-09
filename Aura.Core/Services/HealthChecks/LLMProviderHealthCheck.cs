using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Aura.Core.Models;
using Aura.Core.Orchestrator;
using Aura.Core.Providers;
using Microsoft.Extensions.Logging;

namespace Aura.Core.Services.HealthChecks;

/// <summary>
/// Health check for LLM provider connectivity and functionality
/// </summary>
public class LLMProviderHealthCheck : IHealthCheck
{
    private readonly ILogger<LLMProviderHealthCheck> _logger;
    private readonly LlmProviderFactory _providerFactory;
    private readonly ILoggerFactory _loggerFactory;

    public string Name => "LLM Providers";

    public LLMProviderHealthCheck(
        ILogger<LLMProviderHealthCheck> logger,
        LlmProviderFactory providerFactory,
        ILoggerFactory loggerFactory)
    {
        _logger = logger;
        _providerFactory = providerFactory;
        _loggerFactory = loggerFactory;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(CancellationToken ct = default)
    {
        var sw = Stopwatch.StartNew();
        var result = new HealthCheckResult
        {
            Name = Name,
            Status = HealthStatus.Healthy
        };

        var providerStatuses = new Dictionary<string, object>();
        var healthyCount = 0;
        var totalCount = 0;

        var providers = _providerFactory.CreateAvailableProviders(_loggerFactory);

        if (providers.Count == 0)
        {
            result.Status = HealthStatus.Degraded;
            result.Message = "No LLM providers are registered";
            sw.Stop();
            result.Duration = sw.Elapsed;
            result.Data["ProviderStatuses"] = providerStatuses;
            result.Data["HealthyProviders"] = healthyCount;
            result.Data["TotalProviders"] = totalCount;
            return result;
        }

        foreach (var (providerKey, provider) in providers)
        {
            totalCount++;
            var providerName = providerKey;

            try
            {
                // Test with a simple brief
                var testBrief = new Brief(
                    Topic: "Health Check Test",
                    Audience: null,
                    Goal: null,
                    Tone: "informative",
                    Language: "en",
                    Aspect: Aspect.Widescreen16x9
                );

                var testSpec = new PlanSpec(
                    TargetDuration: TimeSpan.FromSeconds(10),
                    Pacing: Models.Pacing.Conversational,
                    Density: Models.Density.Balanced,
                    Style: "test"
                );

                // Use short timeout for health check
                using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
                cts.CancelAfter(TimeSpan.FromSeconds(10));

                var response = await provider.DraftScriptAsync(testBrief, testSpec, cts.Token);

                if (!string.IsNullOrWhiteSpace(response))
                {
                    providerStatuses[providerName] = "Healthy";
                    healthyCount++;
                    _logger.LogDebug("{Provider} health check passed", providerName);
                }
                else
                {
                    providerStatuses[providerName] = "Degraded - Empty response";
                    result.Status = HealthStatus.Degraded;
                    _logger.LogWarning("{Provider} returned empty response", providerName);
                }
            }
            catch (OperationCanceledException)
            {
                providerStatuses[providerName] = "Degraded - Timeout";
                result.Status = HealthStatus.Degraded;
                _logger.LogWarning("{Provider} health check timed out", providerName);
            }
            catch (Exception ex)
            {
                providerStatuses[providerName] = $"Unhealthy - {ex.Message}";
                if (result.Status != HealthStatus.Unhealthy)
                {
                    result.Status = HealthStatus.Degraded;
                }
                _logger.LogWarning(ex, "{Provider} health check failed", providerName);
            }
        }

        sw.Stop();

        result.Data["ProviderStatuses"] = providerStatuses;
        result.Data["HealthyProviders"] = healthyCount;
        result.Data["TotalProviders"] = totalCount;
        result.Duration = sw.Elapsed;

        if (healthyCount == 0 && totalCount > 0)
        {
            result.Status = HealthStatus.Unhealthy;
            result.Message = "No LLM providers are healthy";
        }
        else if (healthyCount < totalCount)
        {
            result.Message = $"{healthyCount}/{totalCount} LLM providers are healthy";
        }
        else
        {
            result.Message = $"All {totalCount} LLM providers are healthy";
        }

        return result;
    }
}
