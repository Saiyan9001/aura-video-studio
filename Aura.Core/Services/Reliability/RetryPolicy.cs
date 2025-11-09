using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Aura.Core.Errors;
using Microsoft.Extensions.Logging;

namespace Aura.Core.Services.Reliability;

/// <summary>
/// Implements retry logic with exponential backoff for transient failures
/// </summary>
public class RetryPolicy
{
    private readonly ILogger<RetryPolicy> _logger;
    private readonly int _maxRetries;
    private readonly TimeSpan _initialDelay;
    private readonly double _backoffMultiplier;
    private readonly TimeSpan _maxDelay;
    private readonly Func<Exception, bool> _shouldRetry;

    public RetryPolicy(
        ILogger<RetryPolicy> logger,
        int maxRetries = 3,
        TimeSpan? initialDelay = null,
        double backoffMultiplier = 2.0,
        TimeSpan? maxDelay = null,
        Func<Exception, bool>? shouldRetry = null)
    {
        _logger = logger;
        _maxRetries = maxRetries;
        _initialDelay = initialDelay ?? TimeSpan.FromSeconds(1);
        _backoffMultiplier = backoffMultiplier;
        _maxDelay = maxDelay ?? TimeSpan.FromMinutes(1);
        _shouldRetry = shouldRetry ?? DefaultShouldRetry;
    }

    /// <summary>
    /// Executes an operation with retry logic
    /// </summary>
    public async Task<T> ExecuteAsync<T>(
        Func<Task<T>> operation,
        string operationName,
        string? correlationId = null,
        CancellationToken cancellationToken = default)
    {
        Exception? lastException = null;
        var attempts = 0;

        while (attempts <= _maxRetries)
        {
            attempts++;

            try
            {
                _logger.LogDebug("Attempting operation {Operation} (attempt {Attempt}/{Max})",
                    operationName, attempts, _maxRetries + 1);

                var result = await operation().ConfigureAwait(false);

                if (attempts > 1)
                {
                    _logger.LogInformation("Operation {Operation} succeeded after {Attempts} attempts",
                        operationName, attempts);
                }

                return result;
            }
            catch (Exception ex)
            {
                lastException = ex;

                // Don't retry if cancelled
                if (cancellationToken.IsCancellationRequested)
                {
                    _logger.LogInformation("Operation {Operation} cancelled, not retrying", operationName);
                    throw;
                }

                // Check if we should retry this exception
                if (!_shouldRetry(ex))
                {
                    _logger.LogWarning(ex, 
                        "Operation {Operation} failed with non-retryable error: {ErrorType}",
                        operationName, ex.GetType().Name);
                    throw;
                }

                // Check if we've exhausted retries
                if (attempts > _maxRetries)
                {
                    _logger.LogError(ex,
                        "Operation {Operation} failed after {Attempts} attempts",
                        operationName, attempts);
                    break;
                }

                // Calculate delay with exponential backoff
                var delay = CalculateDelay(attempts - 1);
                
                _logger.LogWarning(ex,
                    "Operation {Operation} failed on attempt {Attempt}/{Max}, retrying in {Delay}ms",
                    operationName, attempts, _maxRetries + 1, delay.TotalMilliseconds);

                try
                {
                    await Task.Delay(delay, cancellationToken).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    _logger.LogInformation("Retry delay cancelled for operation {Operation}", operationName);
                    throw;
                }
            }
        }

        // All retries exhausted, throw the last exception
        throw CreateRetryExhaustedException(operationName, _maxRetries + 1, lastException!, correlationId);
    }

    /// <summary>
    /// Calculates the delay for a retry attempt using exponential backoff
    /// </summary>
    private TimeSpan CalculateDelay(int retryAttempt)
    {
        var delay = TimeSpan.FromMilliseconds(
            _initialDelay.TotalMilliseconds * Math.Pow(_backoffMultiplier, retryAttempt));

        // Cap at max delay
        if (delay > _maxDelay)
        {
            delay = _maxDelay;
        }

        // Add jitter to prevent thundering herd (±20%)
        var jitter = Random.Shared.NextDouble() * 0.4 - 0.2; // -0.2 to +0.2
        delay = TimeSpan.FromMilliseconds(delay.TotalMilliseconds * (1 + jitter));

        return delay;
    }

    /// <summary>
    /// Default logic for determining if an exception should be retried
    /// </summary>
    private static bool DefaultShouldRetry(Exception ex)
    {
        // Retry AuraException only if marked as transient
        if (ex is AuraException auraEx)
        {
            return auraEx.IsTransient;
        }

        // Retry common transient errors
        return ex is TimeoutException
            or System.Net.Http.HttpRequestException
            or System.Net.WebException
            or TaskCanceledException; // Can be transient timeout
    }

    /// <summary>
    /// Creates an exception for when retries are exhausted
    /// </summary>
    private static Exception CreateRetryExhaustedException(
        string operationName,
        int attempts,
        Exception lastException,
        string? correlationId)
    {
        var message = $"Operation '{operationName}' failed after {attempts} attempts";
        
        // If the last exception is already an AuraException, enhance it
        if (lastException is AuraException auraEx)
        {
            auraEx.WithContext("retryAttempts", attempts)
                  .WithContext("retriesExhausted", true);
            return auraEx;
        }

        // Otherwise, wrap in a generic ProviderException
        return new ProviderException(
            "RetryPolicy",
            ProviderType.LLM,
            message,
            userMessage: $"The operation failed {attempts} times. {lastException.Message}",
            correlationId: correlationId,
            isTransient: false,
            innerException: lastException)
            .WithContext("retryAttempts", attempts)
            .WithContext("retriesExhausted", true) as ProviderException;
    }

    /// <summary>
    /// Creates a retry policy for provider operations
    /// </summary>
    public static RetryPolicy ForProvider(ILogger<RetryPolicy> logger, int maxRetries = 3)
    {
        return new RetryPolicy(
            logger,
            maxRetries: maxRetries,
            initialDelay: TimeSpan.FromSeconds(1),
            backoffMultiplier: 2.0,
            maxDelay: TimeSpan.FromSeconds(30),
            shouldRetry: IsProviderRetryable);
    }

    /// <summary>
    /// Creates a retry policy for network operations
    /// </summary>
    public static RetryPolicy ForNetwork(ILogger<RetryPolicy> logger, int maxRetries = 5)
    {
        return new RetryPolicy(
            logger,
            maxRetries: maxRetries,
            initialDelay: TimeSpan.FromMilliseconds(500),
            backoffMultiplier: 2.0,
            maxDelay: TimeSpan.FromSeconds(10),
            shouldRetry: IsNetworkRetryable);
    }

    /// <summary>
    /// Determines if a provider exception should be retried
    /// </summary>
    private static bool IsProviderRetryable(Exception ex)
    {
        if (ex is ProviderException providerEx)
        {
            // Retry transient errors and rate limits
            return providerEx.IsTransient || providerEx.HttpStatusCode == 429;
        }

        return DefaultShouldRetry(ex);
    }

    /// <summary>
    /// Determines if a network exception should be retried
    /// </summary>
    private static bool IsNetworkRetryable(Exception ex)
    {
        // Network errors are usually retryable
        return ex is System.Net.Http.HttpRequestException
            or System.Net.WebException
            or TimeoutException
            or TaskCanceledException
            or System.Net.Sockets.SocketException;
    }
}

/// <summary>
/// Extension methods for retry policy
/// </summary>
public static class RetryPolicyExtensions
{
    /// <summary>
    /// Executes an action with retry logic
    /// </summary>
    public static async Task ExecuteAsync(
        this RetryPolicy policy,
        Func<Task> action,
        string operationName,
        string? correlationId = null,
        CancellationToken cancellationToken = default)
    {
        await policy.ExecuteAsync(async () =>
        {
            await action().ConfigureAwait(false);
            return true; // Dummy return value
        }, operationName, correlationId, cancellationToken).ConfigureAwait(false);
    }
}
