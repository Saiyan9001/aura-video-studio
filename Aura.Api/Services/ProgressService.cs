using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Aura.Api.Models.ApiModels.V1;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;

namespace Aura.Api.Services;

/// <summary>
/// Service for tracking and broadcasting video generation progress updates.
/// Stores progress in memory cache and broadcasts to connected SSE clients.
/// </summary>
public class ProgressService
{
    private readonly ILogger<ProgressService> _logger;
    private readonly IMemoryCache _cache;
    private readonly ConcurrentDictionary<string, List<Action<ProgressUpdate>>> _subscribers;
    private static readonly TimeSpan CacheExpiration = TimeSpan.FromHours(1);

    public ProgressService(ILogger<ProgressService> logger, IMemoryCache cache)
    {
        _logger = logger;
        _cache = cache;
        _subscribers = new ConcurrentDictionary<string, List<Action<ProgressUpdate>>>();
    }

    /// <summary>
    /// Create a progress reporter for a specific job
    /// </summary>
    public IProgress<string> CreateProgressReporter(string jobId, string correlationId)
    {
        return new Progress<string>(message =>
        {
            try
            {
                var update = ParseProgressMessage(message, jobId, correlationId);
                StoreProgress(jobId, update);
                BroadcastProgress(jobId, update);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "[{CorrelationId}] Failed to process progress update for job {JobId}", correlationId, jobId);
            }
        });
    }

    /// <summary>
    /// Subscribe to progress updates for a job
    /// </summary>
    public IDisposable Subscribe(string jobId, Action<ProgressUpdate> callback)
    {
        var subscribers = _subscribers.GetOrAdd(jobId, _ => new List<Action<ProgressUpdate>>());
        
        lock (subscribers)
        {
            subscribers.Add(callback);
        }

        _logger.LogDebug("Client subscribed to progress updates for job {JobId}", jobId);

        return new ProgressSubscription(() =>
        {
            lock (subscribers)
            {
                subscribers.Remove(callback);
            }
            _logger.LogDebug("Client unsubscribed from progress updates for job {JobId}", jobId);
        });
    }

    /// <summary>
    /// Get current progress for a job
    /// </summary>
    public ProgressUpdate? GetProgress(string jobId)
    {
        return _cache.Get<ProgressUpdate>($"progress_{jobId}");
    }

    /// <summary>
    /// Get all progress history for a job
    /// </summary>
    public List<ProgressUpdate> GetProgressHistory(string jobId)
    {
        var history = _cache.Get<List<ProgressUpdate>>($"progress_history_{jobId}");
        return history ?? new List<ProgressUpdate>();
    }

    /// <summary>
    /// Clear progress data for a job
    /// </summary>
    public void ClearProgress(string jobId)
    {
        _cache.Remove($"progress_{jobId}");
        _cache.Remove($"progress_history_{jobId}");
        _subscribers.TryRemove(jobId, out _);
        _logger.LogDebug("Cleared progress data for job {JobId}", jobId);
    }

    private void StoreProgress(string jobId, ProgressUpdate update)
    {
        // Store current progress
        _cache.Set($"progress_{jobId}", update, CacheExpiration);

        // Store in history
        var history = _cache.Get<List<ProgressUpdate>>($"progress_history_{jobId}") 
            ?? new List<ProgressUpdate>();
        
        history.Add(update);
        
        // Keep only last 100 updates
        if (history.Count > 100)
        {
            history.RemoveAt(0);
        }
        
        _cache.Set($"progress_history_{jobId}", history, CacheExpiration);
    }

    private void BroadcastProgress(string jobId, ProgressUpdate update)
    {
        if (!_subscribers.TryGetValue(jobId, out var subscribers))
        {
            return;
        }

        List<Action<ProgressUpdate>> subscribersCopy;
        lock (subscribers)
        {
            subscribersCopy = subscribers.ToList();
        }

        foreach (var callback in subscribersCopy)
        {
            try
            {
                callback(update);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to broadcast progress to subscriber for job {JobId}", jobId);
            }
        }
    }

    private ProgressUpdate ParseProgressMessage(string message, string jobId, string correlationId)
    {
        // Parse progress messages in format "Stage: percentage%" or just text
        var percentage = 0;
        var stage = "processing";
        var cleanMessage = message;

        // Try to extract percentage from message
        var percentMatch = System.Text.RegularExpressions.Regex.Match(message, @"(\d+)%");
        if (percentMatch.Success && int.TryParse(percentMatch.Groups[1].Value, out var parsedPercent))
        {
            percentage = Math.Clamp(parsedPercent, 0, 100);
        }

        // Try to extract stage from message format "Stage: message"
        var stageMatch = System.Text.RegularExpressions.Regex.Match(message, @"^([^:]+):");
        if (stageMatch.Success)
        {
            stage = stageMatch.Groups[1].Value.Trim().ToLowerInvariant();
            cleanMessage = message.Substring(stageMatch.Length).Trim();
        }

        return new ProgressUpdate(
            Percentage: percentage,
            Stage: stage,
            Message: cleanMessage,
            Timestamp: DateTime.UtcNow,
            CurrentTask: null,
            EstimatedTimeRemaining: null
        );
    }

    private class ProgressSubscription : IDisposable
    {
        private readonly Action _unsubscribe;
        private bool _disposed;

        public ProgressSubscription(Action unsubscribe)
        {
            _unsubscribe = unsubscribe;
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                _unsubscribe();
                _disposed = true;
            }
        }
    }
}
