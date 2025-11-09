using Aura.Core.Services.Providers;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Aura.Api.Controllers;

/// <summary>
/// Controller for provider health monitoring and circuit breaker management
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class ProviderHealthController : ControllerBase
{
    private readonly ILogger<ProviderHealthController> _logger;
    private readonly ProviderHealthMonitoringService _healthMonitoring;
    private readonly ProviderCircuitBreakerService _circuitBreaker;

    public ProviderHealthController(
        ILogger<ProviderHealthController> logger,
        ProviderHealthMonitoringService healthMonitoring,
        ProviderCircuitBreakerService circuitBreaker)
    {
        _logger = logger;
        _healthMonitoring = healthMonitoring;
        _circuitBreaker = circuitBreaker;
    }

    /// <summary>
    /// Get health status for all tracked providers
    /// </summary>
    [HttpGet]
    public ActionResult<List<ProviderHealthStatusDto>> GetAllProviderHealth()
    {
        try
        {
            var healthMetrics = _healthMonitoring.GetAllProviderHealth();
            var circuitStatuses = _circuitBreaker.GetAllStatus();

            var results = healthMetrics.Select(h =>
            {
                var circuitStatus = circuitStatuses.FirstOrDefault(c => c.ProviderName == h.ProviderName);
                return new ProviderHealthStatusDto
                {
                    ProviderName = h.ProviderName,
                    Status = h.Status.ToString(),
                    SuccessRatePercent = h.SuccessRatePercent,
                    AverageLatencySeconds = h.AverageLatencySeconds,
                    TotalRequests = h.TotalRequests,
                    ConsecutiveFailures = h.ConsecutiveFailures,
                    CircuitState = circuitStatus?.State.ToString() ?? "Closed",
                    LastUpdated = h.LastUpdated
                };
            }).ToList();

            return Ok(results);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving provider health status");
            return Problem("Error retrieving provider health status", statusCode: 500);
        }
    }

    /// <summary>
    /// Get health status for a specific provider
    /// </summary>
    [HttpGet("{providerName}")]
    public ActionResult<ProviderHealthStatusDto> GetProviderHealth(string providerName)
    {
        try
        {
            var health = _healthMonitoring.GetProviderHealth(providerName);
            if (health == null)
            {
                return NotFound(new { message = $"No health data available for provider '{providerName}'" });
            }

            var circuitStatus = _circuitBreaker.GetStatus(providerName);

            var result = new ProviderHealthStatusDto
            {
                ProviderName = health.ProviderName,
                Status = health.Status.ToString(),
                SuccessRatePercent = health.SuccessRatePercent,
                AverageLatencySeconds = health.AverageLatencySeconds,
                TotalRequests = health.TotalRequests,
                ConsecutiveFailures = health.ConsecutiveFailures,
                CircuitState = circuitStatus.State.ToString(),
                LastUpdated = health.LastUpdated,
                NextRetryTime = circuitStatus.NextRetryTime
            };

            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving health status for provider {ProviderName}", providerName);
            return Problem($"Error retrieving health status for provider {providerName}", statusCode: 500);
        }
    }

    /// <summary>
    /// Reset health metrics for a specific provider
    /// </summary>
    [HttpPost("{providerName}/reset")]
    public IActionResult ResetProviderHealth(string providerName)
    {
        try
        {
            _healthMonitoring.ResetProviderHealth(providerName);
            _circuitBreaker.Reset(providerName);

            _logger.LogInformation("Reset health metrics and circuit breaker for provider {ProviderName}", providerName);
            
            return Ok(new { message = $"Health metrics reset for provider '{providerName}'" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error resetting health for provider {ProviderName}", providerName);
            return Problem($"Error resetting health for provider {providerName}", statusCode: 500);
        }
    }

    /// <summary>
    /// Get circuit breaker status for all providers
    /// </summary>
    [HttpGet("circuit-breakers")]
    public ActionResult<List<CircuitBreakerStatusDto>> GetAllCircuitBreakers()
    {
        try
        {
            var statuses = _circuitBreaker.GetAllStatus();
            
            var results = statuses.Select(s => new CircuitBreakerStatusDto
            {
                ProviderName = s.ProviderName,
                State = s.State.ToString(),
                ConsecutiveFailures = s.ConsecutiveFailures,
                ConsecutiveSuccesses = s.ConsecutiveSuccesses,
                LastFailureTime = s.LastFailureTime,
                NextRetryTime = s.NextRetryTime
            }).ToList();

            return Ok(results);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving circuit breaker statuses");
            return Problem("Error retrieving circuit breaker statuses", statusCode: 500);
        }
    }

    /// <summary>
    /// Get circuit breaker status for a specific provider
    /// </summary>
    [HttpGet("{providerName}/circuit-breaker")]
    public ActionResult<CircuitBreakerStatusDto> GetCircuitBreaker(string providerName)
    {
        try
        {
            var status = _circuitBreaker.GetStatus(providerName);

            var result = new CircuitBreakerStatusDto
            {
                ProviderName = status.ProviderName,
                State = status.State.ToString(),
                ConsecutiveFailures = status.ConsecutiveFailures,
                ConsecutiveSuccesses = status.ConsecutiveSuccesses,
                LastFailureTime = status.LastFailureTime,
                NextRetryTime = status.NextRetryTime
            };

            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving circuit breaker status for {ProviderName}", providerName);
            return Problem($"Error retrieving circuit breaker status for {providerName}", statusCode: 500);
        }
    }

    /// <summary>
    /// Manually reset a circuit breaker
    /// </summary>
    [HttpPost("{providerName}/circuit-breaker/reset")]
    public IActionResult ResetCircuitBreaker(string providerName)
    {
        try
        {
            _circuitBreaker.Reset(providerName);
            
            _logger.LogInformation("Manually reset circuit breaker for provider {ProviderName}", providerName);
            
            return Ok(new { message = $"Circuit breaker reset for provider '{providerName}'" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error resetting circuit breaker for {ProviderName}", providerName);
            return Problem($"Error resetting circuit breaker for {providerName}", statusCode: 500);
        }
    }
}

/// <summary>
/// Provider health status DTO
/// </summary>
public class ProviderHealthStatusDto
{
    public string ProviderName { get; init; } = "";
    public string Status { get; init; } = "";
    public double SuccessRatePercent { get; init; }
    public double AverageLatencySeconds { get; init; }
    public int TotalRequests { get; init; }
    public int ConsecutiveFailures { get; init; }
    public string CircuitState { get; init; } = "";
    public DateTime LastUpdated { get; init; }
    public DateTime? NextRetryTime { get; init; }
}

/// <summary>
/// Circuit breaker status DTO
/// </summary>
public class CircuitBreakerStatusDto
{
    public string ProviderName { get; init; } = "";
    public string State { get; init; } = "";
    public int ConsecutiveFailures { get; init; }
    public int ConsecutiveSuccesses { get; init; }
    public DateTime? LastFailureTime { get; init; }
    public DateTime? NextRetryTime { get; init; }
}
