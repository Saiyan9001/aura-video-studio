# System Resource Management Implementation Guide

## Overview

This implementation adds comprehensive system resource management and optimization to Aura Video Studio, enabling stable operation under load with intelligent resource allocation, monitoring, and cleanup.

## Architecture

### Components

#### 1. SystemResourceMonitor (`Aura.Core/Services/Resources/SystemResourceMonitor.cs`)

**Purpose**: Collects comprehensive system and process metrics

**Key Features**:
- **System Metrics**: CPU (per-core and overall), RAM, GPU (NVIDIA via nvidia-smi), disk I/O, network bandwidth
- **Process Metrics**: Thread pool usage, GC statistics, memory allocation by component
- **Cross-platform**: Works on Windows (with WMI) and Linux (via /proc and nvidia-smi)
- **Caching**: Stores last collected metrics to avoid redundant sampling

**Usage**:
```csharp
var monitor = serviceProvider.GetRequiredService<SystemResourceMonitor>();

// Collect current system metrics
var systemMetrics = await monitor.CollectSystemMetricsAsync(cancellationToken);
Console.WriteLine($"CPU: {systemMetrics.Cpu.OverallUsagePercent:F1}%");
Console.WriteLine($"RAM: {systemMetrics.Memory.UsagePercent:F1}%");

// Collect process metrics
var processMetrics = monitor.CollectProcessMetrics();
Console.WriteLine($"Active threads: {processMetrics.ThreadPool.BusyWorkerThreads}");

// Get cached metrics (fast, no new sampling)
var lastMetrics = monitor.GetLastSystemMetrics();
```

#### 2. ResourceThrottler (`Aura.Core/Services/Resources/ResourceThrottler.cs`)

**Purpose**: Dynamically throttles resource allocation based on system capacity

**Key Features**:
- **Job Throttling**: Limits concurrent jobs based on available RAM (2GB per video job)
- **Provider Throttling**: Rate limits API calls per provider with configurable max concurrent
- **Thread Pool Management**: Adjusts thread pool size based on CPU load
- **Resource Reservation**: Acquire/release pattern prevents overcommit
- **GPU Awareness**: Prefers GPU jobs when GPU is available and not overloaded

**Usage**:
```csharp
var throttler = serviceProvider.GetRequiredService<ResourceThrottler>();

// Acquire resources for a job
var reservation = await throttler.TryAcquireJobResourcesAsync(
    jobId: "video-123",
    estimatedMemoryBytes: 2L * 1024 * 1024 * 1024, // 2 GB
    requiresGpu: true,
    cancellationToken);

if (reservation != null)
{
    try
    {
        // Execute job
        await ExecuteVideoJobAsync(jobId, cancellationToken);
    }
    finally
    {
        // Always release when done
        throttler.ReleaseJobResources(jobId);
    }
}
else
{
    // Resources not available, queue or retry later
    await QueueJobForLaterAsync(jobId);
}

// Provider throttling
var acquired = await throttler.TryAcquireProviderSlotAsync("OpenAI", maxConcurrent: 5);
if (acquired)
{
    try
    {
        await CallOpenAIAsync();
    }
    finally
    {
        throttler.ReleaseProviderSlot("OpenAI");
    }
}

// Adjust thread pool dynamically
throttler.AdjustThreadPool();

// Get current utilization
var stats = throttler.GetUtilizationStats();
Console.WriteLine($"Active jobs: {stats.ActiveJobs}/{stats.MaxConcurrentJobs}");
Console.WriteLine($"Available slots: {stats.AvailableJobSlots}");
```

#### 3. EnhancedCleanupHostedService (`Aura.Api/HostedServices/EnhancedCleanupHostedService.cs`)

**Purpose**: Automated cleanup with scheduled maintenance tasks

**Schedules**:

**Hourly** (runs every hour):
- Temporary files older than 24 hours
- Orphaned upload files older than 48 hours
- Expired cache entries older than 24 hours

**Daily** (runs every 24 hours):
- Archive completed projects older than 7 days
- Delete failed projects older than 3 days
- Compress log files older than 7 days (gzip)
- Clean expired cache entries

**Weekly** (runs every 7 days):
- SQLite database vacuum (defragmentation)
- Clean old export history (30 days)
- Archive action logs (90 days)
- Delete old archived projects (30 days)

**Registration**:
```csharp
// In Program.cs
builder.Services.AddHostedService<EnhancedCleanupHostedService>();
```

## API Endpoints

### GET /api/metrics/system

Returns current system resource metrics in JSON format.

**Response**:
```json
{
  "timestamp": "2025-11-07T22:00:00Z",
  "cpu": {
    "overallUsagePercent": 45.2,
    "perCoreUsagePercent": [42.1, 48.3, 43.7, 47.2],
    "logicalCores": 8,
    "physicalCores": 4,
    "processUsagePercent": 12.5
  },
  "memory": {
    "totalBytes": 17179869184,
    "availableBytes": 8589934592,
    "usedBytes": 8589934592,
    "usagePercent": 50.0,
    "processUsageBytes": 536870912
  },
  "gpu": {
    "name": "NVIDIA GeForce RTX 3080",
    "vendor": "NVIDIA",
    "usagePercent": 75.0,
    "totalMemoryBytes": 10737418240,
    "usedMemoryBytes": 8053063680,
    "temperatureCelsius": 68.0
  }
}
```

### GET /api/metrics/process

Returns process-specific metrics.

**Response**:
```json
{
  "timestamp": "2025-11-07T22:00:00Z",
  "threadPool": {
    "availableWorkerThreads": 28,
    "maxWorkerThreads": 32,
    "busyWorkerThreads": 4
  },
  "cacheMemoryBytes": 104857600
}
```

### GET /api/metrics/utilization

Returns current resource utilization and throttling status.

**Response**:
```json
{
  "maxConcurrentJobs": 4,
  "activeJobs": 2,
  "availableJobSlots": 2,
  "totalReservedMemoryBytes": 4294967296,
  "activeProviders": ["OpenAI", "ElevenLabs"],
  "cpuUsagePercent": 45.2,
  "memoryUsagePercent": 50.0,
  "gpuUsagePercent": 75.0
}
```

### GET /api/metrics/prometheus

Returns metrics in Prometheus text format for monitoring integration.

**Response** (excerpt):
```
# HELP aura_cpu_usage_percent CPU usage percentage
# TYPE aura_cpu_usage_percent gauge
aura_cpu_usage_percent{type="overall"} 45.20
aura_cpu_usage_percent{type="process"} 12.50

# HELP aura_memory_bytes Memory usage in bytes
# TYPE aura_memory_bytes gauge
aura_memory_bytes{type="total"} 17179869184
aura_memory_bytes{type="available"} 8589934592
aura_memory_bytes{type="used"} 8589934592
aura_memory_bytes{type="process"} 536870912

# HELP aura_gpu_usage_percent GPU usage percentage
# TYPE aura_gpu_usage_percent gauge
aura_gpu_usage_percent{vendor="NVIDIA"} 75.00
```

## Integration Examples

### Job Execution with Resource Throttling

```csharp
public class VideoJobRunner
{
    private readonly ResourceThrottler _throttler;
    private readonly ILogger<VideoJobRunner> _logger;

    public VideoJobRunner(ResourceThrottler throttler, ILogger<VideoJobRunner> logger)
    {
        _throttler = throttler;
        _logger = logger;
    }

    public async Task<JobResult> RunVideoJobAsync(VideoJob job, CancellationToken ct)
    {
        // Estimate memory requirement (2GB base + video size)
        var estimatedMemory = 2L * 1024 * 1024 * 1024 + job.EstimatedOutputSize;
        
        // Try to acquire resources
        var reservation = await _throttler.TryAcquireJobResourcesAsync(
            job.Id,
            estimatedMemory,
            requiresGpu: job.RequiresHardwareAcceleration,
            ct);

        if (reservation == null)
        {
            _logger.LogWarning("Cannot start job {JobId}: insufficient resources", job.Id);
            return JobResult.Queued;
        }

        try
        {
            _logger.LogInformation("Starting job {JobId} with {MemoryMB} MB reserved",
                job.Id, estimatedMemory / (1024 * 1024));

            // Execute the actual video generation
            await job.ExecuteAsync(ct);

            return JobResult.Success;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Job {JobId} failed", job.Id);
            return JobResult.Failed;
        }
        finally
        {
            // Always release resources
            _throttler.ReleaseJobResources(job.Id);
            _logger.LogInformation("Released resources for job {JobId}", job.Id);
        }
    }
}
```

### Monitoring Dashboard Integration

```csharp
public class MonitoringService : BackgroundService
{
    private readonly SystemResourceMonitor _monitor;
    private readonly ILogger<MonitoringService> _logger;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var metrics = await _monitor.CollectSystemMetricsAsync(stoppingToken);
                
                // Check for resource pressure
                if (metrics.Cpu.OverallUsagePercent > 90)
                {
                    _logger.LogWarning("High CPU usage: {Usage:F1}%", 
                        metrics.Cpu.OverallUsagePercent);
                }

                if (metrics.Memory.UsagePercent > 85)
                {
                    _logger.LogWarning("High memory usage: {Usage:F1}%", 
                        metrics.Memory.UsagePercent);
                }

                if (metrics.Gpu != null && metrics.Gpu.TemperatureCelsius > 85)
                {
                    _logger.LogWarning("High GPU temperature: {Temp:F1}°C",
                        metrics.Gpu.TemperatureCelsius);
                }

                // Send to monitoring system (Prometheus, Grafana, etc.)
                await SendToMonitoringAsync(metrics, stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error collecting metrics");
            }

            await Task.Delay(TimeSpan.FromSeconds(15), stoppingToken);
        }
    }
}
```

## Configuration

### Resource Throttler Configuration

The `ResourceThrottler` can be configured by adjusting constants in the class:

```csharp
// In ResourceThrottler.cs
private const long BytesPerVideoJob = 2L * 1024 * 1024 * 1024; // 2 GB per job
private const double CpuThresholdPercent = 85.0; // CPU limit
private const double MemoryThresholdPercent = 80.0; // Memory limit
private int _reservedMemoryForUiMb = 500; // Reserved for UI
```

### Cleanup Schedule Configuration

The `EnhancedCleanupHostedService` schedules can be adjusted:

```csharp
// In EnhancedCleanupHostedService.cs
private readonly TimeSpan _hourlyInterval = TimeSpan.FromHours(1);
private readonly TimeSpan _dailyInterval = TimeSpan.FromDays(1);
private readonly TimeSpan _weeklyInterval = TimeSpan.FromDays(7);
```

Retention periods in cleanup methods:
- Temp files: 24 hours
- Upload files: 48 hours
- Completed projects: 7 days
- Failed projects: 3 days
- Log compression: 7 days
- Export history: 30 days
- Action logs: 90 days
- Archived projects: 30 days

## Testing

### Running Tests

```bash
# Run all resource management tests
dotnet test --filter "FullyQualifiedName~SystemResourceMonitorTests|FullyQualifiedName~ResourceThrottlerTests"

# Run specific test
dotnet test --filter "FullyQualifiedName~SystemResourceMonitorTests.CollectSystemMetricsAsync_ReturnsMetrics"
```

### Test Coverage

- **SystemResourceMonitorTests**: 10 tests
  - System metrics collection validation
  - CPU, memory, disk, network metrics
  - Process metrics and thread pool
  - Metric caching

- **ResourceThrottlerTests**: 12 tests
  - Resource acquisition and release
  - Memory-based throttling
  - Provider slot management
  - Utilization statistics
  - Thread pool adjustment

## Performance Considerations

### SystemResourceMonitor

- **CPU Impact**: Minimal (~0.1% CPU per collection)
- **Collection Time**: ~100-200ms per full collection
- **Recommendation**: Collect every 5-15 seconds for dashboards
- **Caching**: Use `GetLastSystemMetrics()` for frequent reads

### ResourceThrottler

- **Overhead**: Negligible (semaphore-based, microsecond operations)
- **Scalability**: Handles hundreds of concurrent job requests efficiently
- **Memory**: ~1KB per active reservation

### EnhancedCleanupHostedService

- **Impact**: Runs in background, low priority
- **Schedule**: Staggers operations to avoid resource spikes
- **Safety**: Checks for file locks and active usage before deletion

## Troubleshooting

### Issue: High CPU usage reported

**Possible causes**:
- PerformanceCounter initialization on Windows may briefly spike CPU
- Multiple concurrent collections

**Solution**:
- Increase collection interval
- Use cached metrics more frequently

### Issue: GPU metrics not available

**Possible causes**:
- nvidia-smi not in PATH
- Non-NVIDIA GPU
- Running on Linux without nvidia-utils

**Solution**:
- Install nvidia-utils: `apt-get install nvidia-utils`
- Check nvidia-smi availability: `which nvidia-smi`
- GPU metrics will be null if unavailable

### Issue: Resource reservations not released

**Possible causes**:
- Exception during job execution without finally block
- Job cancellation without cleanup

**Solution**:
- Always use try-finally pattern
- Ensure ReleaseJobResources is called in all code paths

### Issue: Cleanup service deleting active files

**Possible causes**:
- File age check not accounting for active jobs
- Clock skew

**Solution**:
- Increase retention periods
- Check file last access time in addition to creation time

## Best Practices

1. **Always release resources**: Use try-finally or using patterns
2. **Check before acquiring**: Verify resource availability before starting expensive operations
3. **Monitor regularly**: Set up dashboard with Prometheus metrics
4. **Adjust thresholds**: Tune for your specific hardware and workload
5. **Test under load**: Simulate peak usage to validate throttling
6. **Log resource decisions**: Include resource checks in operation logs
7. **Graceful degradation**: Queue jobs when resources unavailable rather than failing

## Future Enhancements

Potential improvements for follow-up work:

1. **Intelligent Caching**: Multi-tier cache with LRU eviction
2. **Preflight Checks**: Validate resources before accepting jobs
3. **Usage Analytics**: Per-user resource consumption tracking
4. **Cost Tracking**: Provider API usage and cost estimation
5. **Auto-scaling**: Adjust limits based on historical usage patterns
6. **Alerting**: Integration with notification systems for critical thresholds

## References

- [PR #9: System Resource Management and Optimization](https://github.com/Coffee285/aura-video-studio/pull/9)
- [Prometheus Metrics Format](https://prometheus.io/docs/instrumenting/exposition_formats/)
- [.NET Performance Counters](https://learn.microsoft.com/en-us/dotnet/api/system.diagnostics.performancecounter)
