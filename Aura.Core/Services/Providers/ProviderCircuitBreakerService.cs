using System;
using System.Collections.Generic;
using Microsoft.Extensions.Logging;

namespace Aura.Core.Services.Providers;

/// <summary>
/// Circuit breaker service for providers to prevent cascading failures
/// Implements the circuit breaker pattern with automatic recovery
/// </summary>
public class ProviderCircuitBreakerService
{
    private readonly ILogger<ProviderCircuitBreakerService> _logger;
    private readonly Dictionary<string, CircuitBreaker> _breakers = new();
    private readonly object _lock = new();

    private const int DefaultFailureThreshold = 5;
    private const int DefaultSuccessThreshold = 2;
    private static readonly TimeSpan DefaultTimeout = TimeSpan.FromMinutes(1);

    public ProviderCircuitBreakerService(ILogger<ProviderCircuitBreakerService> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Check if a provider can be used (circuit is closed or half-open)
    /// </summary>
    public bool CanExecute(string providerName)
    {
        lock (_lock)
        {
            var breaker = GetOrCreateBreaker(providerName);
            return breaker.State != CircuitState.Open;
        }
    }

    /// <summary>
    /// Record a successful provider execution
    /// </summary>
    public void RecordSuccess(string providerName)
    {
        lock (_lock)
        {
            var breaker = GetOrCreateBreaker(providerName);
            var previousState = breaker.State;
            breaker.RecordSuccess();

            if (previousState != breaker.State)
            {
                _logger.LogInformation(
                    "Circuit breaker state changed for {ProviderName}: {OldState} → {NewState}",
                    providerName, previousState, breaker.State);
            }
        }
    }

    /// <summary>
    /// Record a failed provider execution
    /// </summary>
    public void RecordFailure(string providerName, Exception? exception = null)
    {
        lock (_lock)
        {
            var breaker = GetOrCreateBreaker(providerName);
            var previousState = breaker.State;
            breaker.RecordFailure();

            if (previousState != breaker.State)
            {
                _logger.LogWarning(
                    exception,
                    "Circuit breaker OPENED for {ProviderName} after {Failures} consecutive failures. Will retry after {Timeout}",
                    providerName, breaker.ConsecutiveFailures, breaker.Timeout);
            }
        }
    }

    /// <summary>
    /// Get the current state of a provider's circuit breaker
    /// </summary>
    public CircuitState GetState(string providerName)
    {
        lock (_lock)
        {
            var breaker = GetOrCreateBreaker(providerName);
            return breaker.State;
        }
    }

    /// <summary>
    /// Get circuit breaker status for a provider
    /// </summary>
    public CircuitBreakerStatus GetStatus(string providerName)
    {
        lock (_lock)
        {
            var breaker = GetOrCreateBreaker(providerName);
            return new CircuitBreakerStatus
            {
                ProviderName = providerName,
                State = breaker.State,
                ConsecutiveFailures = breaker.ConsecutiveFailures,
                ConsecutiveSuccesses = breaker.ConsecutiveSuccesses,
                LastFailureTime = breaker.LastFailureTime,
                NextRetryTime = breaker.GetNextRetryTime()
            };
        }
    }

    /// <summary>
    /// Get circuit breaker status for all providers
    /// </summary>
    public List<CircuitBreakerStatus> GetAllStatus()
    {
        lock (_lock)
        {
            var statuses = new List<CircuitBreakerStatus>();
            foreach (var providerName in _breakers.Keys)
            {
                statuses.Add(GetStatus(providerName));
            }
            return statuses;
        }
    }

    /// <summary>
    /// Manually reset a circuit breaker to closed state
    /// </summary>
    public void Reset(string providerName)
    {
        lock (_lock)
        {
            var breaker = GetOrCreateBreaker(providerName);
            breaker.Reset();
            _logger.LogInformation("Manually reset circuit breaker for {ProviderName}", providerName);
        }
    }

    private CircuitBreaker GetOrCreateBreaker(string providerName)
    {
        if (!_breakers.TryGetValue(providerName, out var breaker))
        {
            breaker = new CircuitBreaker(
                DefaultFailureThreshold,
                DefaultSuccessThreshold,
                DefaultTimeout);
            _breakers[providerName] = breaker;
        }
        return breaker;
    }
}

/// <summary>
/// Circuit breaker states
/// </summary>
public enum CircuitState
{
    Closed,   // Normal operation, requests allowed
    Open,     // Too many failures, requests blocked
    HalfOpen  // Testing if service recovered, limited requests allowed
}

/// <summary>
/// Status information for a circuit breaker
/// </summary>
public class CircuitBreakerStatus
{
    public string ProviderName { get; init; } = "";
    public CircuitState State { get; init; }
    public int ConsecutiveFailures { get; init; }
    public int ConsecutiveSuccesses { get; init; }
    public DateTime? LastFailureTime { get; init; }
    public DateTime? NextRetryTime { get; init; }
}

/// <summary>
/// Internal circuit breaker implementation
/// </summary>
internal class CircuitBreaker
{
    private readonly int _failureThreshold;
    private readonly int _successThreshold;
    private readonly TimeSpan _timeout;

    private CircuitState _state = CircuitState.Closed;
    private int _consecutiveFailures;
    private int _consecutiveSuccesses;
    private DateTime? _lastFailureTime;

    public CircuitBreaker(int failureThreshold, int successThreshold, TimeSpan timeout)
    {
        _failureThreshold = failureThreshold;
        _successThreshold = successThreshold;
        _timeout = timeout;
    }

    public CircuitState State
    {
        get
        {
            if (_state == CircuitState.Open && CanAttemptReset())
            {
                _state = CircuitState.HalfOpen;
                _consecutiveSuccesses = 0;
            }
            return _state;
        }
    }

    public int ConsecutiveFailures => _consecutiveFailures;
    public int ConsecutiveSuccesses => _consecutiveSuccesses;
    public DateTime? LastFailureTime => _lastFailureTime;
    public TimeSpan Timeout => _timeout;

    public void RecordSuccess()
    {
        _consecutiveFailures = 0;

        if (_state == CircuitState.HalfOpen)
        {
            _consecutiveSuccesses++;
            if (_consecutiveSuccesses >= _successThreshold)
            {
                _state = CircuitState.Closed;
                _consecutiveSuccesses = 0;
            }
        }
    }

    public void RecordFailure()
    {
        _lastFailureTime = DateTime.UtcNow;
        _consecutiveFailures++;
        _consecutiveSuccesses = 0;

        if (_state == CircuitState.HalfOpen)
        {
            _state = CircuitState.Open;
        }
        else if (_consecutiveFailures >= _failureThreshold)
        {
            _state = CircuitState.Open;
        }
    }

    public void Reset()
    {
        _state = CircuitState.Closed;
        _consecutiveFailures = 0;
        _consecutiveSuccesses = 0;
        _lastFailureTime = null;
    }

    public DateTime? GetNextRetryTime()
    {
        if (_state == CircuitState.Open && _lastFailureTime.HasValue)
        {
            return _lastFailureTime.Value.Add(_timeout);
        }
        return null;
    }

    private bool CanAttemptReset()
    {
        if (!_lastFailureTime.HasValue)
        {
            return false;
        }

        return DateTime.UtcNow >= _lastFailureTime.Value.Add(_timeout);
    }
}
