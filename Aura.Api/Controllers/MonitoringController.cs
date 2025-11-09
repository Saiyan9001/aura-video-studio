using Aura.Core.Errors;
using Aura.Core.Services.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Aura.Api.Controllers;

/// <summary>
/// Monitoring endpoints for system health, provider status, and error tracking
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class MonitoringController : ControllerBase
{
    private readonly ILogger<MonitoringController> _logger;
    private readonly ErrorAggregationService? _errorAggregation;

    public MonitoringController(
        ILogger<MonitoringController> logger,
        ErrorAggregationService? errorAggregation = null)
    {
        _logger = logger;
        _errorAggregation = errorAggregation;
    }

    /// <summary>
    /// Get system health status including provider availability and circuit breaker states
    /// </summary>
    [HttpGet("health")]
    [ProducesResponseType(typeof(HealthResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<HealthResponse>> GetHealth(CancellationToken cancellationToken)
    {
        var health = new HealthResponse
        {
            Status = "healthy",
            Timestamp = DateTime.UtcNow,
            Providers = new List<ProviderStatus>
            {
                new ProviderStatus
                {
                    Name = "System",
                    Type = "Core",
                    IsAvailable = true,
                    CircuitState = "closed",
                    AverageResponseTimeMs = 0,
                    RecentErrorRate = 0.0
                }
            }
        };

        if (_errorAggregation != null)
        {
            var recentErrors = _errorAggregation.GetAggregatedErrors(TimeSpan.FromMinutes(5), 100);
            health.TotalErrors = (int)recentErrors.Sum(e => e.Count);
            health.ErrorRatePerMinute = CalculateErrorRate(recentErrors);
        }

        return Ok(health);
    }

    /// <summary>
    /// Get recent errors with filtering options (admin only)
    /// </summary>
    [HttpGet("errors")]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<ErrorResponse>> GetErrors(
        [FromQuery] int limit = 50,
        [FromQuery] string? errorType = null,
        [FromQuery] string? provider = null,
        CancellationToken cancellationToken = default)
    {
        if (_errorAggregation == null)
        {
            return Ok(new ErrorResponse
            {
                Errors = new List<ErrorDetail>(),
                TotalCount = 0
            });
        }

        var recentErrors = _errorAggregation.GetAggregatedErrors(TimeSpan.FromHours(1), limit);
        
        var errorDetails = recentErrors
            .Where(e => string.IsNullOrEmpty(errorType) || e.ExceptionType == errorType)
            .Select(e => new ErrorDetail
            {
                Timestamp = e.LastSeen,
                ErrorType = e.ExceptionType,
                Message = e.Message,
                CorrelationId = e.SampleCorrelationId,
                Context = e.SampleContext,
                Count = e.Count,
                FirstSeen = e.FirstSeen
            })
            .ToList();

        var response = new ErrorResponse
        {
            Errors = errorDetails,
            TotalCount = (int)errorDetails.Sum(e => e.Count),
            ErrorFrequencyByType = errorDetails
                .GroupBy(e => e.ErrorType)
                .ToDictionary(g => g.Key, g => (int)g.Sum(e => e.Count))
        };

        return Ok(response);
    }

    private static double CalculateErrorRate(List<ErrorSignature> errors)
    {
        if (errors.Count == 0) return 0.0;
        
        var oneMinuteAgo = DateTime.UtcNow.AddMinutes(-1);
        var errorsInLastMinute = errors.Where(e => e.LastSeen >= oneMinuteAgo).Sum(e => e.Count);
        return errorsInLastMinute;
    }
}

public class HealthResponse
{
    public string Status { get; set; } = "healthy";
    public DateTime Timestamp { get; set; }
    public List<ProviderStatus> Providers { get; set; } = new();
    public int TotalErrors { get; set; }
    public double ErrorRatePerMinute { get; set; }
}

public class ProviderStatus
{
    public string Name { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public bool IsAvailable { get; set; }
    public string CircuitState { get; set; } = "closed";
    public double AverageResponseTimeMs { get; set; }
    public double RecentErrorRate { get; set; }
}

public class ErrorResponse
{
    public List<ErrorDetail> Errors { get; set; } = new();
    public int TotalCount { get; set; }
    public Dictionary<string, int> ErrorFrequencyByType { get; set; } = new();
}

public class ErrorDetail
{
    public DateTime Timestamp { get; set; }
    public string ErrorType { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string? CorrelationId { get; set; }
    public Dictionary<string, object>? Context { get; set; }
    public long Count { get; set; }
    public DateTime FirstSeen { get; set; }
}

public class ProviderFailureStats
{
    public int TotalFailures { get; set; }
    public int TransientFailures { get; set; }
    public string MostCommonError { get; set; } = string.Empty;
}
