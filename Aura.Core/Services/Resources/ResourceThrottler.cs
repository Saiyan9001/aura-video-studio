using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Aura.Core.Models;
using Microsoft.Extensions.Logging;

namespace Aura.Core.Services.Resources;

/// <summary>
/// Service for dynamically throttling resource allocation based on system capacity
/// </summary>
public class ResourceThrottler
{
    private readonly ILogger<ResourceThrottler> _logger;
    private readonly SystemResourceMonitor _resourceMonitor;
    private readonly ConcurrentDictionary<string, ProviderThrottleState> _providerStates = new();
    private readonly ConcurrentDictionary<string, ResourceReservation> _activeReservations = new();
    private readonly SemaphoreSlim _jobSemaphore;
    private readonly object _configLock = new();

    private const long BytesPerVideoJob = 2L * 1024 * 1024 * 1024; // 2 GB per video job
    private const double CpuThresholdPercent = 85.0;
    private const double MemoryThresholdPercent = 80.0;
    private int _maxConcurrentJobs;
    private int _reservedMemoryForUiMb = 500; // 500 MB reserved for UI

    public ResourceThrottler(
        ILogger<ResourceThrottler> logger,
        SystemResourceMonitor resourceMonitor)
    {
        _logger = logger;
        _resourceMonitor = resourceMonitor;
        
        _maxConcurrentJobs = CalculateMaxConcurrentJobs();
        _jobSemaphore = new SemaphoreSlim(_maxConcurrentJobs, _maxConcurrentJobs);
        
        _logger.LogInformation("ResourceThrottler initialized with max concurrent jobs: {MaxJobs}", _maxConcurrentJobs);
    }

    /// <summary>
    /// Attempts to acquire resources for a job
    /// </summary>
    public async Task<ResourceReservation?> TryAcquireJobResourcesAsync(
        string jobId,
        long estimatedMemoryBytes,
        bool requiresGpu,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var available = await _jobSemaphore.WaitAsync(0, cancellationToken).ConfigureAwait(false);
            if (!available)
            {
                _logger.LogWarning("Job {JobId} denied: Maximum concurrent jobs reached ({MaxJobs})", 
                    jobId, _maxConcurrentJobs);
                return null;
            }

            var metrics = await _resourceMonitor.CollectSystemMetricsAsync(cancellationToken).ConfigureAwait(false);
            
            if (metrics.Cpu.OverallUsagePercent > CpuThresholdPercent)
            {
                _jobSemaphore.Release();
                _logger.LogWarning("Job {JobId} denied: CPU usage too high ({Usage:F1}%)", 
                    jobId, metrics.Cpu.OverallUsagePercent);
                return null;
            }

            var availableMemory = metrics.Memory.AvailableBytes - (_reservedMemoryForUiMb * 1024L * 1024L);
            if (estimatedMemoryBytes > availableMemory)
            {
                _jobSemaphore.Release();
                _logger.LogWarning("Job {JobId} denied: Insufficient memory (need {NeedMB} MB, available {AvailMB} MB)",
                    jobId, estimatedMemoryBytes / (1024 * 1024), availableMemory / (1024 * 1024));
                return null;
            }

            if (requiresGpu && (metrics.Gpu == null || metrics.Gpu.UsagePercent > 90))
            {
                _jobSemaphore.Release();
                _logger.LogWarning("Job {JobId} denied: GPU not available or too busy", jobId);
                return null;
            }

            var reservation = new ResourceReservation
            {
                JobId = jobId,
                ReservedMemoryBytes = estimatedMemoryBytes,
                RequiresGpu = requiresGpu,
                AcquiredAt = DateTime.UtcNow
            };

            _activeReservations.TryAdd(jobId, reservation);
            
            _logger.LogInformation("Job {JobId} acquired resources: {MemoryMB} MB, GPU: {Gpu}",
                jobId, estimatedMemoryBytes / (1024 * 1024), requiresGpu);

            return reservation;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error acquiring resources for job {JobId}", jobId);
            return null;
        }
    }

    /// <summary>
    /// Releases resources held by a job
    /// </summary>
    public void ReleaseJobResources(string jobId)
    {
        try
        {
            if (_activeReservations.TryRemove(jobId, out var reservation))
            {
                _jobSemaphore.Release();
                
                var duration = DateTime.UtcNow - reservation.AcquiredAt;
                _logger.LogInformation("Job {JobId} released resources after {Duration:F1}s",
                    jobId, duration.TotalSeconds);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error releasing resources for job {JobId}", jobId);
        }
    }

    /// <summary>
    /// Gets or creates throttle state for a provider
    /// </summary>
    public ProviderThrottleState GetProviderThrottle(string providerName, int maxConcurrent = 5)
    {
        return _providerStates.GetOrAdd(providerName, name =>
        {
            _logger.LogInformation("Creating throttle state for provider {Provider} with max concurrent: {Max}",
                name, maxConcurrent);
            return new ProviderThrottleState(name, maxConcurrent);
        });
    }

    /// <summary>
    /// Attempts to acquire a slot for a provider operation
    /// </summary>
    public async Task<bool> TryAcquireProviderSlotAsync(
        string providerName,
        int maxConcurrent,
        CancellationToken cancellationToken = default)
    {
        var state = GetProviderThrottle(providerName, maxConcurrent);
        return await state.TryAcquireAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Releases a provider operation slot
    /// </summary>
    public void ReleaseProviderSlot(string providerName)
    {
        if (_providerStates.TryGetValue(providerName, out var state))
        {
            state.Release();
        }
    }

    /// <summary>
    /// Recalculates maximum concurrent jobs based on current system resources
    /// </summary>
    public async Task RecalculateResourceLimitsAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var newMax = CalculateMaxConcurrentJobs();
            
            lock (_configLock)
            {
                if (newMax != _maxConcurrentJobs)
                {
                    _logger.LogInformation("Adjusting max concurrent jobs from {Old} to {New}",
                        _maxConcurrentJobs, newMax);
                    _maxConcurrentJobs = newMax;
                }
            }

            await Task.CompletedTask;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error recalculating resource limits");
        }
    }

    /// <summary>
    /// Gets current resource utilization statistics
    /// </summary>
    public ResourceUtilizationStats GetUtilizationStats()
    {
        var systemMetrics = _resourceMonitor.GetLastSystemMetrics();
        
        return new ResourceUtilizationStats
        {
            MaxConcurrentJobs = _maxConcurrentJobs,
            ActiveJobs = _activeReservations.Count,
            AvailableJobSlots = _jobSemaphore.CurrentCount,
            TotalReservedMemoryBytes = _activeReservations.Values.Sum(r => r.ReservedMemoryBytes),
            ActiveProviders = _providerStates.Keys.ToArray(),
            CpuUsagePercent = systemMetrics?.Cpu.OverallUsagePercent ?? 0,
            MemoryUsagePercent = systemMetrics?.Memory.UsagePercent ?? 0,
            GpuUsagePercent = systemMetrics?.Gpu?.UsagePercent ?? 0
        };
    }

    /// <summary>
    /// Adjusts thread pool settings based on available cores and workload
    /// </summary>
    public void AdjustThreadPool()
    {
        try
        {
            var coreCount = Environment.ProcessorCount;
            var systemMetrics = _resourceMonitor.GetLastSystemMetrics();
            
            int workerThreads;
            int completionPortThreads;

            if (systemMetrics != null && systemMetrics.Cpu.OverallUsagePercent > 80)
            {
                workerThreads = Math.Max(coreCount, coreCount * 2);
                completionPortThreads = Math.Max(coreCount, coreCount * 2);
            }
            else
            {
                workerThreads = coreCount * 4;
                completionPortThreads = coreCount * 4;
            }

            ThreadPool.SetMinThreads(coreCount, coreCount);
            ThreadPool.SetMaxThreads(workerThreads, completionPortThreads);

            _logger.LogDebug("Thread pool adjusted: Min={MinThreads}, Max={MaxThreads}",
                coreCount, workerThreads);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error adjusting thread pool");
        }
    }

    private int CalculateMaxConcurrentJobs()
    {
        try
        {
            var systemMetrics = _resourceMonitor.GetLastSystemMetrics();
            if (systemMetrics == null)
            {
                return Environment.ProcessorCount;
            }

            var availableMemory = systemMetrics.Memory.AvailableBytes - (_reservedMemoryForUiMb * 1024L * 1024L);
            var maxByMemory = (int)(availableMemory / BytesPerVideoJob);

            var maxByCpu = Environment.ProcessorCount;

            var hasGpu = systemMetrics.Gpu != null;
            if (hasGpu)
            {
                maxByCpu = Math.Max(maxByCpu, maxByCpu * 2);
            }

            var calculated = Math.Min(maxByMemory, maxByCpu);
            return Math.Max(1, Math.Min(calculated, 10));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error calculating max concurrent jobs");
            return Environment.ProcessorCount;
        }
    }
}

/// <summary>
/// State for throttling provider operations
/// </summary>
public class ProviderThrottleState
{
    private readonly SemaphoreSlim _semaphore;
    public string ProviderName { get; }
    public int MaxConcurrent { get; }

    public ProviderThrottleState(string providerName, int maxConcurrent)
    {
        ProviderName = providerName;
        MaxConcurrent = maxConcurrent;
        _semaphore = new SemaphoreSlim(maxConcurrent, maxConcurrent);
    }

    public async Task<bool> TryAcquireAsync(CancellationToken cancellationToken = default)
    {
        return await _semaphore.WaitAsync(0, cancellationToken).ConfigureAwait(false);
    }

    public void Release()
    {
        try
        {
            _semaphore.Release();
        }
        catch (SemaphoreFullException)
        {
            // Ignore - already at capacity
        }
    }

    public int CurrentCount => _semaphore.CurrentCount;
}

/// <summary>
/// Represents a resource reservation for a job
/// </summary>
public class ResourceReservation
{
    public string JobId { get; set; } = string.Empty;
    public long ReservedMemoryBytes { get; set; }
    public bool RequiresGpu { get; set; }
    public DateTime AcquiredAt { get; set; }
}

/// <summary>
/// Statistics about resource utilization
/// </summary>
public class ResourceUtilizationStats
{
    public int MaxConcurrentJobs { get; set; }
    public int ActiveJobs { get; set; }
    public int AvailableJobSlots { get; set; }
    public long TotalReservedMemoryBytes { get; set; }
    public string[] ActiveProviders { get; set; } = Array.Empty<string>();
    public double CpuUsagePercent { get; set; }
    public double MemoryUsagePercent { get; set; }
    public double GpuUsagePercent { get; set; }
}
