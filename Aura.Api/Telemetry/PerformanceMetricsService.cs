using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using Microsoft.Extensions.Logging;

namespace Aura.Api.Telemetry;

/// <summary>
/// Service for tracking and reporting performance metrics including cache statistics
/// </summary>
public class PerformanceMetricsService
{
    private readonly ILogger<PerformanceMetricsService> _logger;
    private readonly ConcurrentDictionary<string, ApiEndpointMetrics> _endpointMetrics = new();
    private long _totalRequests;
    private long _totalErrors;

    public PerformanceMetricsService(ILogger<PerformanceMetricsService> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Records metrics for an API request
    /// </summary>
    public void RecordRequest(string endpoint, long durationMs, bool isError = false, bool isCached = false)
    {
        Interlocked.Increment(ref _totalRequests);
        
        if (isError)
        {
            Interlocked.Increment(ref _totalErrors);
        }

        var metrics = _endpointMetrics.GetOrAdd(endpoint, _ => new ApiEndpointMetrics());
        metrics.RecordRequest(durationMs, isError, isCached);

        if (durationMs > 1000)
        {
            _logger.LogWarning("Slow request detected: {Endpoint} took {Duration}ms", endpoint, durationMs);
        }
    }

    /// <summary>
    /// Gets metrics for a specific endpoint
    /// </summary>
    public ApiEndpointMetrics? GetEndpointMetrics(string endpoint)
    {
        return _endpointMetrics.TryGetValue(endpoint, out var metrics) ? metrics : null;
    }

    /// <summary>
    /// Gets summary of all performance metrics
    /// </summary>
    public PerformanceSummary GetSummary()
    {
        return new PerformanceSummary
        {
            TotalRequests = Interlocked.Read(ref _totalRequests),
            TotalErrors = Interlocked.Read(ref _totalErrors),
            ErrorRate = _totalRequests > 0 ? (double)_totalErrors / _totalRequests : 0,
            EndpointMetrics = _endpointMetrics.ToDictionary(
                kvp => kvp.Key,
                kvp => new
                {
                    kvp.Value.RequestCount,
                    kvp.Value.ErrorCount,
                    kvp.Value.CachedCount,
                    AverageDurationMs = kvp.Value.AverageDuration,
                    CacheHitRate = kvp.Value.CacheHitRate,
                    P95DurationMs = kvp.Value.P95Duration,
                    P99DurationMs = kvp.Value.P99Duration
                })
        };
    }

    /// <summary>
    /// Resets all metrics (useful for testing)
    /// </summary>
    public void Reset()
    {
        _endpointMetrics.Clear();
        Interlocked.Exchange(ref _totalRequests, 0);
        Interlocked.Exchange(ref _totalErrors, 0);
    }
}

/// <summary>
/// Metrics for a specific API endpoint
/// </summary>
public class ApiEndpointMetrics
{
    private long _requestCount;
    private long _errorCount;
    private long _cachedCount;
    private long _totalDurationMs;
    private readonly ConcurrentBag<long> _durations = new();

    public long RequestCount => Interlocked.Read(ref _requestCount);
    public long ErrorCount => Interlocked.Read(ref _errorCount);
    public long CachedCount => Interlocked.Read(ref _cachedCount);
    
    public double AverageDuration => RequestCount > 0 
        ? (double)Interlocked.Read(ref _totalDurationMs) / RequestCount 
        : 0;
    
    public double CacheHitRate => RequestCount > 0 
        ? (double)CachedCount / RequestCount 
        : 0;

    public long P95Duration => CalculatePercentile(0.95);
    public long P99Duration => CalculatePercentile(0.99);

    public void RecordRequest(long durationMs, bool isError, bool isCached)
    {
        Interlocked.Increment(ref _requestCount);
        Interlocked.Add(ref _totalDurationMs, durationMs);
        
        if (isError)
        {
            Interlocked.Increment(ref _errorCount);
        }
        
        if (isCached)
        {
            Interlocked.Increment(ref _cachedCount);
        }

        _durations.Add(durationMs);
        
        if (_durations.Count > 1000)
        {
            var oldest = _durations.Take(100).ToList();
            foreach (var item in oldest)
            {
                _durations.TryTake(out long _);
            }
        }
    }

    private long CalculatePercentile(double percentile)
    {
        var durations = _durations.OrderBy(x => x).ToList();
        if (durations.Count == 0) return 0;
        
        var index = (int)Math.Ceiling(durations.Count * percentile) - 1;
        return durations[Math.Max(0, Math.Min(index, durations.Count - 1))];
    }
}

/// <summary>
/// Summary of all performance metrics
/// </summary>
public class PerformanceSummary
{
    public long TotalRequests { get; set; }
    public long TotalErrors { get; set; }
    public double ErrorRate { get; set; }
    public object EndpointMetrics { get; set; } = new();
}
