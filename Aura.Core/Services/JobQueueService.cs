using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Aura.Core.Models;
using Microsoft.Extensions.Logging;

namespace Aura.Core.Services;

/// <summary>
/// Manages job queuing with priority support and retry logic
/// </summary>
public class JobQueueService
{
    private readonly ILogger<JobQueueService> _logger;
    private readonly PriorityQueue<QueuedJob, int> _jobQueue = new();
    private readonly ConcurrentDictionary<string, QueuedJob> _queuedJobs = new();
    private readonly ConcurrentDictionary<string, RetryState> _retryStates = new();
    private readonly SemaphoreSlim _queueLock = new(1, 1);

    public JobQueueService(ILogger<JobQueueService> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Enqueues a job with priority (lower number = higher priority)
    /// </summary>
    public async Task<bool> EnqueueJobAsync(string jobId, int priority = 5, CancellationToken ct = default)
    {
        await _queueLock.WaitAsync(ct);
        try
        {
            var queuedJob = new QueuedJob
            {
                JobId = jobId,
                Priority = priority,
                EnqueuedAt = DateTime.UtcNow,
                RetryCount = 0
            };

            _jobQueue.Enqueue(queuedJob, priority);
            _queuedJobs[jobId] = queuedJob;

            _logger.LogInformation("Job {JobId} enqueued with priority {Priority}", jobId, priority);
            return true;
        }
        finally
        {
            _queueLock.Release();
        }
    }

    /// <summary>
    /// Dequeues the next job to process
    /// </summary>
    public async Task<QueuedJob?> DequeueJobAsync(CancellationToken ct = default)
    {
        await _queueLock.WaitAsync(ct);
        try
        {
            if (_jobQueue.TryDequeue(out var queuedJob, out _))
            {
                _queuedJobs.TryRemove(queuedJob.JobId, out _);
                _logger.LogInformation("Job {JobId} dequeued for processing", queuedJob.JobId);
                return queuedJob;
            }

            return null;
        }
        finally
        {
            _queueLock.Release();
        }
    }

    /// <summary>
    /// Gets the current queue size
    /// </summary>
    public int GetQueueSize()
    {
        return _jobQueue.Count;
    }

    /// <summary>
    /// Checks if a job can be retried based on exponential backoff
    /// </summary>
    public bool CanRetryJob(string jobId, int maxRetries = 3)
    {
        if (!_retryStates.TryGetValue(jobId, out var retryState))
        {
            return true;
        }

        if (retryState.RetryCount >= maxRetries)
        {
            _logger.LogWarning("Job {JobId} has reached max retry count {MaxRetries}", 
                jobId, maxRetries);
            return false;
        }

        var backoffDelay = CalculateBackoffDelay(retryState.RetryCount);
        var nextRetryTime = retryState.LastRetryAt.Add(backoffDelay);

        if (DateTime.UtcNow < nextRetryTime)
        {
            _logger.LogDebug("Job {JobId} cannot retry yet. Next retry at {NextRetryTime}", 
                jobId, nextRetryTime);
            return false;
        }

        return true;
    }

    /// <summary>
    /// Records a retry attempt for a job
    /// </summary>
    public async Task<bool> RetryJobAsync(string jobId, int priority = 5, CancellationToken ct = default)
    {
        // Get or create retry state
        var isFirstRetry = !_retryStates.ContainsKey(jobId);
        
        var retryState = _retryStates.GetOrAdd(jobId, _ => new RetryState
        {
            JobId = jobId,
            RetryCount = 0,
            LastRetryAt = DateTime.UtcNow
        });

        // Check if retry is allowed (skip backoff check for first retry)
        if (!isFirstRetry && !CanRetryJob(jobId))
        {
            return false;
        }

        // Increment retry count
        retryState = retryState with
        {
            RetryCount = retryState.RetryCount + 1,
            LastRetryAt = DateTime.UtcNow
        };

        _retryStates[jobId] = retryState;

        _logger.LogInformation("Retrying job {JobId}, attempt {RetryCount}", 
            jobId, retryState.RetryCount);

        return await EnqueueJobAsync(jobId, priority, ct);
    }

    /// <summary>
    /// Clears retry state for a job (e.g., after successful completion)
    /// </summary>
    public void ClearRetryState(string jobId)
    {
        _retryStates.TryRemove(jobId, out _);
    }

    /// <summary>
    /// Gets retry state for a job
    /// </summary>
    public RetryState? GetRetryState(string jobId)
    {
        return _retryStates.TryGetValue(jobId, out var state) ? state : null;
    }

    /// <summary>
    /// Calculates exponential backoff delay
    /// </summary>
    private TimeSpan CalculateBackoffDelay(int retryCount)
    {
        var baseDelaySeconds = 5;
        var maxDelaySeconds = 300; // Cap at 5 minutes
        var delaySeconds = Math.Min(baseDelaySeconds * Math.Pow(2, retryCount), maxDelaySeconds);
        return TimeSpan.FromSeconds(delaySeconds);
    }

    /// <summary>
    /// Gets all queued job IDs
    /// </summary>
    public List<string> GetQueuedJobIds()
    {
        return _queuedJobs.Keys.ToList();
    }
}

/// <summary>
/// Represents a job in the queue
/// </summary>
public record QueuedJob
{
    public string JobId { get; init; } = string.Empty;
    public int Priority { get; init; }
    public DateTime EnqueuedAt { get; init; }
    public int RetryCount { get; init; }
}

/// <summary>
/// Tracks retry state for a job
/// </summary>
public record RetryState
{
    public string JobId { get; init; } = string.Empty;
    public int RetryCount { get; init; }
    public DateTime LastRetryAt { get; init; }
    public string? LastError { get; init; }
}
