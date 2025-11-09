using Aura.Api.Telemetry;
using Aura.Core.Services.Resources;
using Microsoft.AspNetCore.Mvc;
using System.Text;

namespace Aura.Api.Controllers;

/// <summary>
/// Controller to expose performance metrics
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class MetricsController : ControllerBase
{
    private readonly PerformanceMetrics _metrics;
    private readonly SystemResourceMonitor? _resourceMonitor;
    private readonly ResourceThrottler? _resourceThrottler;
    private readonly ILogger<MetricsController> _logger;

    public MetricsController(
        PerformanceMetrics metrics, 
        ILogger<MetricsController> logger,
        SystemResourceMonitor? resourceMonitor = null,
        ResourceThrottler? resourceThrottler = null)
    {
        _metrics = metrics;
        _logger = logger;
        _resourceMonitor = resourceMonitor;
    }

    /// <summary>
    /// Get all performance metrics
    /// </summary>
    [HttpGet]
    public ActionResult<Dictionary<string, EndpointMetrics>> GetMetrics()
    {
        try
        {
            var metrics = _metrics.GetMetrics();
            return Ok(metrics);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving metrics");
            return StatusCode(500, new { error = "Failed to retrieve metrics" });
        }
    }

    /// <summary>
    /// Get metrics for a specific endpoint
    /// </summary>
    [HttpGet("{endpoint}")]
    public ActionResult<EndpointMetrics> GetEndpointMetrics(string endpoint)
    {
        try
        {
            // Decode the endpoint parameter (e.g., "GET:/api/jobs" might be URL encoded)
            var decodedEndpoint = Uri.UnescapeDataString(endpoint);
            
            var metrics = _metrics.GetEndpointMetrics(decodedEndpoint);
            
            if (metrics == null)
            {
                return NotFound(new { error = $"No metrics found for endpoint: {decodedEndpoint}" });
            }
            
            return Ok(metrics);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving metrics for endpoint: {Endpoint}", endpoint);
            return StatusCode(500, new { error = "Failed to retrieve endpoint metrics" });
        }
    }

    /// <summary>
    /// Reset all metrics (useful for testing)
    /// </summary>
    [HttpPost("reset")]
    public ActionResult ResetMetrics()
    {
        try
        {
            _metrics.Reset();
            _logger.LogInformation("Metrics reset via API request");
            return Ok(new { message = "All metrics have been reset" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error resetting metrics");
            return StatusCode(500, new { error = "Failed to reset metrics" });
        }
    }

    /// <summary>
    /// Reset metrics for a specific endpoint
    /// </summary>
    [HttpPost("reset/{endpoint}")]
    public ActionResult ResetEndpointMetrics(string endpoint)
    {
        try
        {
            var decodedEndpoint = Uri.UnescapeDataString(endpoint);
            _metrics.ResetEndpoint(decodedEndpoint);
            _logger.LogInformation("Metrics reset for endpoint: {Endpoint}", decodedEndpoint);
            return Ok(new { message = $"Metrics for {decodedEndpoint} have been reset" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error resetting metrics for endpoint: {Endpoint}", endpoint);
            return StatusCode(500, new { error = "Failed to reset endpoint metrics" });
        }
    }

    /// <summary>
    /// Get system resource metrics
    /// </summary>
    [HttpGet("system")]
    public async Task<ActionResult> GetSystemMetrics(CancellationToken cancellationToken)
    {
        try
        {
            if (_resourceMonitor == null)
            {
                return StatusCode(503, new { error = "Resource monitoring is not available" });
            }

            var metrics = await _resourceMonitor.CollectSystemMetricsAsync(cancellationToken);
            return Ok(metrics);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving system metrics");
            return StatusCode(500, new { error = "Failed to retrieve system metrics" });
        }
    }

    /// <summary>
    /// Get process-specific metrics
    /// </summary>
    [HttpGet("process")]
    public ActionResult GetProcessMetrics()
    {
        try
        {
            if (_resourceMonitor == null)
            {
                return StatusCode(503, new { error = "Resource monitoring is not available" });
            }

            var metrics = _resourceMonitor.CollectProcessMetrics();
            return Ok(metrics);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving process metrics");
            return StatusCode(500, new { error = "Failed to retrieve process metrics" });
        }
    }

    /// <summary>
    /// Get all metrics in Prometheus text format
    /// </summary>
    [HttpGet("prometheus")]
    [Produces("text/plain")]
    public async Task<ActionResult> GetPrometheusMetrics(CancellationToken cancellationToken)
    {
        try
        {
            var sb = new StringBuilder();

            if (_resourceMonitor != null)
            {
                var systemMetrics = await _resourceMonitor.CollectSystemMetricsAsync(cancellationToken);
                var processMetrics = _resourceMonitor.CollectProcessMetrics();

                sb.AppendLine("# HELP aura_cpu_usage_percent CPU usage percentage");
                sb.AppendLine("# TYPE aura_cpu_usage_percent gauge");
                sb.AppendLine($"aura_cpu_usage_percent {{type=\"overall\"}} {systemMetrics.Cpu.OverallUsagePercent:F2}");
                sb.AppendLine($"aura_cpu_usage_percent {{type=\"process\"}} {systemMetrics.Cpu.ProcessUsagePercent:F2}");

                sb.AppendLine();
                sb.AppendLine("# HELP aura_memory_bytes Memory usage in bytes");
                sb.AppendLine("# TYPE aura_memory_bytes gauge");
                sb.AppendLine($"aura_memory_bytes {{type=\"total\"}} {systemMetrics.Memory.TotalBytes}");
                sb.AppendLine($"aura_memory_bytes {{type=\"available\"}} {systemMetrics.Memory.AvailableBytes}");
                sb.AppendLine($"aura_memory_bytes {{type=\"used\"}} {systemMetrics.Memory.UsedBytes}");
                sb.AppendLine($"aura_memory_bytes {{type=\"process\"}} {systemMetrics.Memory.ProcessUsageBytes}");

                sb.AppendLine();
                sb.AppendLine("# HELP aura_memory_usage_percent Memory usage percentage");
                sb.AppendLine("# TYPE aura_memory_usage_percent gauge");
                sb.AppendLine($"aura_memory_usage_percent {systemMetrics.Memory.UsagePercent:F2}");

                if (systemMetrics.Gpu != null)
                {
                    sb.AppendLine();
                    sb.AppendLine("# HELP aura_gpu_usage_percent GPU usage percentage");
                    sb.AppendLine("# TYPE aura_gpu_usage_percent gauge");
                    sb.AppendLine($"aura_gpu_usage_percent {{vendor=\"{systemMetrics.Gpu.Vendor}\"}} {systemMetrics.Gpu.UsagePercent:F2}");

                    sb.AppendLine();
                    sb.AppendLine("# HELP aura_gpu_memory_bytes GPU memory in bytes");
                    sb.AppendLine("# TYPE aura_gpu_memory_bytes gauge");
                    sb.AppendLine($"aura_gpu_memory_bytes {{type=\"total\",vendor=\"{systemMetrics.Gpu.Vendor}\"}} {systemMetrics.Gpu.TotalMemoryBytes}");
                    sb.AppendLine($"aura_gpu_memory_bytes {{type=\"used\",vendor=\"{systemMetrics.Gpu.Vendor}\"}} {systemMetrics.Gpu.UsedMemoryBytes}");
                    sb.AppendLine($"aura_gpu_memory_bytes {{type=\"available\",vendor=\"{systemMetrics.Gpu.Vendor}\"}} {systemMetrics.Gpu.AvailableMemoryBytes}");

                    sb.AppendLine();
                    sb.AppendLine("# HELP aura_gpu_temperature_celsius GPU temperature in Celsius");
                    sb.AppendLine("# TYPE aura_gpu_temperature_celsius gauge");
                    sb.AppendLine($"aura_gpu_temperature_celsius {{vendor=\"{systemMetrics.Gpu.Vendor}\"}} {systemMetrics.Gpu.TemperatureCelsius:F1}");
                }

                foreach (var disk in systemMetrics.Disks)
                {
                    var driveName = disk.DriveName.Replace("\\", "").Replace(":", "").Replace("/", "");
                    sb.AppendLine();
                    sb.AppendLine($"# HELP aura_disk_bytes_{driveName} Disk space in bytes");
                    sb.AppendLine($"# TYPE aura_disk_bytes_{driveName} gauge");
                    sb.AppendLine($"aura_disk_bytes_{driveName} {{type=\"total\"}} {disk.TotalBytes}");
                    sb.AppendLine($"aura_disk_bytes_{driveName} {{type=\"available\"}} {disk.AvailableBytes}");
                    sb.AppendLine($"aura_disk_bytes_{driveName} {{type=\"used\"}} {disk.UsedBytes}");
                }

                sb.AppendLine();
                sb.AppendLine("# HELP aura_network_bytes_per_second Network bandwidth in bytes per second");
                sb.AppendLine("# TYPE aura_network_bytes_per_second gauge");
                sb.AppendLine($"aura_network_bytes_per_second {{direction=\"sent\"}} {systemMetrics.Network.BytesSentPerSecond}");
                sb.AppendLine($"aura_network_bytes_per_second {{direction=\"received\"}} {systemMetrics.Network.BytesReceivedPerSecond}");

                sb.AppendLine();
                sb.AppendLine("# HELP aura_threadpool_threads Thread pool thread counts");
                sb.AppendLine("# TYPE aura_threadpool_threads gauge");
                sb.AppendLine($"aura_threadpool_threads {{type=\"available_worker\"}} {processMetrics.ThreadPool.AvailableWorkerThreads}");
                sb.AppendLine($"aura_threadpool_threads {{type=\"max_worker\"}} {processMetrics.ThreadPool.MaxWorkerThreads}");
                sb.AppendLine($"aura_threadpool_threads {{type=\"busy_worker\"}} {processMetrics.ThreadPool.BusyWorkerThreads}");

                sb.AppendLine();
                sb.AppendLine("# HELP aura_cache_memory_bytes Cache memory usage in bytes");
                sb.AppendLine("# TYPE aura_cache_memory_bytes gauge");
                sb.AppendLine($"aura_cache_memory_bytes {processMetrics.CacheMemoryBytes}");
            }

            var endpointMetrics = _metrics.GetMetrics();
            sb.AppendLine();
            sb.AppendLine("# HELP aura_http_requests_total Total HTTP requests by endpoint");
            sb.AppendLine("# TYPE aura_http_requests_total counter");
            foreach (var endpoint in endpointMetrics)
            {
                var endpointLabel = endpoint.Key.Replace("\"", "\\\"");
                sb.AppendLine($"aura_http_requests_total {{endpoint=\"{endpointLabel}\"}} {endpoint.Value.TotalRequests}");
            }

            sb.AppendLine();
            sb.AppendLine("# HELP aura_http_request_duration_ms HTTP request duration in milliseconds");
            sb.AppendLine("# TYPE aura_http_request_duration_ms summary");
            foreach (var endpoint in endpointMetrics)
            {
                var endpointLabel = endpoint.Key.Replace("\"", "\\\"");
                sb.AppendLine($"aura_http_request_duration_ms {{endpoint=\"{endpointLabel}\",quantile=\"0.5\"}} {endpoint.Value.P50DurationMs}");
                sb.AppendLine($"aura_http_request_duration_ms {{endpoint=\"{endpointLabel}\",quantile=\"0.95\"}} {endpoint.Value.P95DurationMs}");
                sb.AppendLine($"aura_http_request_duration_ms {{endpoint=\"{endpointLabel}\",quantile=\"0.99\"}} {endpoint.Value.P99DurationMs}");
            }

            return Content(sb.ToString(), "text/plain; version=0.0.4");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating Prometheus metrics");
            return StatusCode(500, new { error = "Failed to generate Prometheus metrics" });
        }
    }

    /// <summary>
    /// Get resource utilization statistics
    /// </summary>
    [HttpGet("utilization")]
    public ActionResult GetUtilization()
    {
        try
        {
            if (_resourceThrottler == null)
            {
                return StatusCode(503, new { error = "Resource throttling is not available" });
            }

            var stats = _resourceThrottler.GetUtilizationStats();
            return Ok(stats);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving resource utilization");
            return StatusCode(500, new { error = "Failed to retrieve resource utilization" });
        }
    }

    /// <summary>
    /// Get cache performance metrics
    /// </summary>
    [HttpGet("cache")]
    public ActionResult GetCacheMetrics([FromServices] Aura.Core.Services.Caching.IDistributedCacheService? cacheService = null)
    {
        try
        {
            if (cacheService == null)
            {
                return Ok(new { message = "Caching is not enabled or not configured", enabled = false });
            }

            var stats = cacheService.GetStatistics();
            return Ok(new
            {
                enabled = true,
                hits = stats.Hits,
                misses = stats.Misses,
                errors = stats.Errors,
                hitRate = stats.HitRate,
                backendType = stats.BackendType,
                timestamp = DateTime.UtcNow
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving cache metrics");
            return StatusCode(500, new { error = "Failed to retrieve cache metrics" });
        }
    }
}
