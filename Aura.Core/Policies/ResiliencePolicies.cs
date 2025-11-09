using System;
using System.Net.Http;
using System.Threading.Tasks;
using Aura.Core.Errors;
using Microsoft.Extensions.Logging;
using Polly;
using Polly.CircuitBreaker;
using Polly.Retry;
using Polly.Timeout;

namespace Aura.Core.Policies;

/// <summary>
/// Resilience policies for handling transient failures, rate limits, and service outages.
/// Provides retry, circuit breaker, bulkhead, and timeout policies for different provider types.
/// </summary>
public static class ResiliencePolicies
{
    /// <summary>
    /// Creates a retry policy for OpenAI with exponential backoff for rate limits
    /// </summary>
    public static ResiliencePipeline<T> CreateOpenAiRetryPolicy<T>(ILogger logger)
    {
        return new ResiliencePipelineBuilder<T>()
            .AddRetry(new RetryStrategyOptions<T>
            {
                ShouldHandle = new PredicateBuilder<T>()
                    .HandleResult(result => ShouldRetryOpenAi(result))
                    .Handle<HttpRequestException>()
                    .Handle<TimeoutException>()
                    .Handle<ProviderException>(ex => ex.SpecificErrorCode == ProviderErrorCode.RateLimit),
                MaxRetryAttempts = 4,
                Delay = TimeSpan.FromSeconds(2),
                BackoffType = DelayBackoffType.Exponential,
                UseJitter = true,
                OnRetry = args =>
                {
                    logger.LogWarning(
                        "OpenAI retry attempt {AttemptNumber} after {RetryDelay}ms. Reason: {Exception}",
                        args.AttemptNumber,
                        args.RetryDelay.TotalMilliseconds,
                        args.Outcome.Exception?.Message ?? "Rate limit or transient error");
                    return ValueTask.CompletedTask;
                }
            })
            .AddTimeout(TimeSpan.FromSeconds(30))
            .Build();
    }

    /// <summary>
    /// Creates a retry policy for Ollama with immediate retry for connection issues
    /// </summary>
    public static ResiliencePipeline<T> CreateOllamaRetryPolicy<T>(ILogger logger)
    {
        return new ResiliencePipelineBuilder<T>()
            .AddRetry(new RetryStrategyOptions<T>
            {
                ShouldHandle = new PredicateBuilder<T>()
                    .Handle<HttpRequestException>()
                    .Handle<ProviderException>(ex => 
                        ex.SpecificErrorCode == ProviderErrorCode.NetworkError ||
                        ex.SpecificErrorCode == ProviderErrorCode.ServiceUnavailable),
                MaxRetryAttempts = 3,
                Delay = TimeSpan.FromMilliseconds(100),
                BackoffType = DelayBackoffType.Constant,
                OnRetry = args =>
                {
                    logger.LogWarning(
                        "Ollama retry attempt {AttemptNumber}. Reason: {Exception}",
                        args.AttemptNumber,
                        args.Outcome.Exception?.Message ?? "Connection issue");
                    return ValueTask.CompletedTask;
                }
            })
            .AddTimeout(TimeSpan.FromSeconds(30))
            .Build();
    }

    /// <summary>
    /// Creates a retry policy for Anthropic (Claude) with exponential backoff
    /// </summary>
    public static ResiliencePipeline<T> CreateAnthropicRetryPolicy<T>(ILogger logger)
    {
        return new ResiliencePipelineBuilder<T>()
            .AddRetry(new RetryStrategyOptions<T>
            {
                ShouldHandle = new PredicateBuilder<T>()
                    .Handle<HttpRequestException>()
                    .Handle<TimeoutException>()
                    .Handle<ProviderException>(ex => 
                        ex.SpecificErrorCode == ProviderErrorCode.RateLimit ||
                        ex.IsTransient),
                MaxRetryAttempts = 4,
                Delay = TimeSpan.FromSeconds(2),
                BackoffType = DelayBackoffType.Exponential,
                UseJitter = true,
                OnRetry = args =>
                {
                    logger.LogWarning(
                        "Anthropic retry attempt {AttemptNumber} after {RetryDelay}ms",
                        args.AttemptNumber,
                        args.RetryDelay.TotalMilliseconds);
                    return ValueTask.CompletedTask;
                }
            })
            .AddTimeout(TimeSpan.FromSeconds(30))
            .Build();
    }

    /// <summary>
    /// Creates a no-retry policy for local providers (fail fast)
    /// </summary>
    public static ResiliencePipeline<T> CreateLocalProviderPolicy<T>(ILogger logger)
    {
        return new ResiliencePipelineBuilder<T>()
            .AddTimeout(TimeSpan.FromSeconds(30))
            .Build();
    }

    /// <summary>
    /// Creates a circuit breaker policy for a provider
    /// </summary>
    public static ResiliencePipeline<T> CreateCircuitBreakerPolicy<T>(
        string providerName,
        ILogger logger,
        int failureThreshold = 3,
        TimeSpan durationOfBreak = default)
    {
        if (durationOfBreak == default)
        {
            durationOfBreak = TimeSpan.FromSeconds(30);
        }

        return new ResiliencePipelineBuilder<T>()
            .AddCircuitBreaker(new CircuitBreakerStrategyOptions<T>
            {
                FailureRatio = 0.5,
                MinimumThroughput = failureThreshold,
                SamplingDuration = TimeSpan.FromSeconds(30),
                BreakDuration = durationOfBreak,
                ShouldHandle = new PredicateBuilder<T>()
                    .Handle<HttpRequestException>()
                    .Handle<TimeoutException>()
                    .Handle<ProviderException>(ex => !ex.IsTransient),
                OnOpened = args =>
                {
                    logger.LogError(
                        "Circuit breaker opened for provider {ProviderName} after {FailureCount} failures. Will retry after {BreakDuration}s",
                        providerName,
                        failureThreshold,
                        durationOfBreak.TotalSeconds);
                    return ValueTask.CompletedTask;
                },
                OnClosed = args =>
                {
                    logger.LogInformation(
                        "Circuit breaker closed for provider {ProviderName} - service recovered",
                        providerName);
                    return ValueTask.CompletedTask;
                },
                OnHalfOpened = args =>
                {
                    logger.LogInformation(
                        "Circuit breaker half-open for provider {ProviderName} - testing if service recovered",
                        providerName);
                    return ValueTask.CompletedTask;
                }
            })
            .Build();
    }

    /// <summary>
    /// Creates a timeout policy for LLM calls (30 seconds)
    /// </summary>
    public static ResiliencePipeline<T> CreateLlmTimeoutPolicy<T>()
    {
        return new ResiliencePipelineBuilder<T>()
            .AddTimeout(TimeSpan.FromSeconds(30))
            .Build();
    }

    /// <summary>
    /// Creates a timeout policy for image generation (60 seconds)
    /// </summary>
    public static ResiliencePipeline<T> CreateImageGenerationTimeoutPolicy<T>()
    {
        return new ResiliencePipelineBuilder<T>()
            .AddTimeout(TimeSpan.FromSeconds(60))
            .Build();
    }

    /// <summary>
    /// Creates a timeout policy for video rendering (5 minutes)
    /// </summary>
    public static ResiliencePipeline<T> CreateVideoRenderingTimeoutPolicy<T>()
    {
        return new ResiliencePipelineBuilder<T>()
            .AddTimeout(TimeSpan.FromMinutes(5))
            .Build();
    }

    /// <summary>
    /// Creates a combined resilience pipeline with retry, circuit breaker, and timeout
    /// </summary>
    public static ResiliencePipeline<T> CreateComprehensivePolicy<T>(
        string providerName,
        ProviderType providerType,
        ILogger logger)
    {
        var builder = new ResiliencePipelineBuilder<T>();

        // Add retry policy based on provider type
        if (providerName.Contains("OpenAI", StringComparison.OrdinalIgnoreCase))
        {
            builder.AddRetry(new RetryStrategyOptions<T>
            {
                ShouldHandle = new PredicateBuilder<T>()
                    .Handle<HttpRequestException>()
                    .Handle<TimeoutException>()
                    .Handle<ProviderException>(ex => ex.IsTransient),
                MaxRetryAttempts = 4,
                Delay = TimeSpan.FromSeconds(2),
                BackoffType = DelayBackoffType.Exponential,
                UseJitter = true
            });
        }
        else if (providerName.Contains("Ollama", StringComparison.OrdinalIgnoreCase))
        {
            builder.AddRetry(new RetryStrategyOptions<T>
            {
                ShouldHandle = new PredicateBuilder<T>()
                    .Handle<HttpRequestException>()
                    .Handle<ProviderException>(ex => ex.IsTransient),
                MaxRetryAttempts = 3,
                Delay = TimeSpan.FromMilliseconds(100)
            });
        }

        // Add circuit breaker
        builder.AddCircuitBreaker(new CircuitBreakerStrategyOptions<T>
        {
            FailureRatio = 0.5,
            MinimumThroughput = 3,
            SamplingDuration = TimeSpan.FromSeconds(30),
            BreakDuration = TimeSpan.FromSeconds(30),
            ShouldHandle = new PredicateBuilder<T>()
                .Handle<HttpRequestException>()
                .Handle<TimeoutException>()
                .Handle<ProviderException>()
        });

        // Add timeout based on provider type
        var timeout = providerType switch
        {
            ProviderType.LLM => TimeSpan.FromSeconds(30),
            ProviderType.TTS => TimeSpan.FromSeconds(60),
            ProviderType.Visual => TimeSpan.FromSeconds(60),
            ProviderType.Rendering => TimeSpan.FromMinutes(5),
            _ => TimeSpan.FromSeconds(30)
        };
        builder.AddTimeout(timeout);

        return builder.Build();
    }

    /// <summary>
    /// Determines if a result should trigger a retry for OpenAI
    /// </summary>
    private static bool ShouldRetryOpenAi<T>(T result)
    {
        if (result is HttpResponseMessage response)
        {
            return (int)response.StatusCode == 429 || // Rate limit
                   (int)response.StatusCode >= 500;   // Server errors
        }
        return false;
    }
}
