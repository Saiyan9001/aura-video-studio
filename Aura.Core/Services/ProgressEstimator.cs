using System;
using System.Collections.Generic;
using System.Linq;
using Aura.Core.Models;

namespace Aura.Core.Services;

/// <summary>
/// Estimates time remaining for job completion based on progress history
/// </summary>
public class ProgressEstimator
{
    private readonly Dictionary<string, List<ProgressSample>> _progressHistory = new();
    private readonly TimeSpan _minEstimate = TimeSpan.FromSeconds(5);
    private readonly TimeSpan _maxEstimate = TimeSpan.FromHours(2);

    /// <summary>
    /// Records a progress sample for ETA calculation
    /// </summary>
    public void RecordProgress(string jobId, double percent, DateTime timestamp)
    {
        if (!_progressHistory.ContainsKey(jobId))
        {
            _progressHistory[jobId] = new List<ProgressSample>();
        }

        _progressHistory[jobId].Add(new ProgressSample
        {
            Percent = percent,
            Timestamp = timestamp
        });

        // Keep only last 20 samples to avoid memory growth
        if (_progressHistory[jobId].Count > 20)
        {
            _progressHistory[jobId].RemoveAt(0);
        }
    }

    /// <summary>
    /// Estimates time remaining based on progress velocity
    /// </summary>
    public TimeSpan? EstimateTimeRemaining(string jobId, double currentPercent)
    {
        // If job is complete, return zero immediately
        if (currentPercent >= 100)
        {
            return TimeSpan.Zero;
        }
        
        if (!_progressHistory.TryGetValue(jobId, out var samples) || samples.Count < 2)
        {
            return null;
        }

        // Calculate average velocity (percent per second) from recent samples
        var recentSamples = samples.TakeLast(Math.Min(5, samples.Count)).ToList();
        var velocities = new List<double>();

        for (int i = 1; i < recentSamples.Count; i++)
        {
            var deltaPercent = recentSamples[i].Percent - recentSamples[i - 1].Percent;
            var deltaTime = (recentSamples[i].Timestamp - recentSamples[i - 1].Timestamp).TotalSeconds;

            if (deltaTime > 0 && deltaPercent > 0)
            {
                velocities.Add(deltaPercent / deltaTime);
            }
        }

        if (velocities.Count == 0)
        {
            return null;
        }

        // Use median velocity for robustness
        var medianVelocity = velocities.OrderBy(v => v).ElementAt(velocities.Count / 2);

        if (medianVelocity <= 0)
        {
            return null;
        }

        var remainingPercent = 100 - currentPercent;
        var estimatedSeconds = remainingPercent / medianVelocity;

        var estimate = TimeSpan.FromSeconds(estimatedSeconds);

        // Clamp to reasonable bounds
        if (estimate < _minEstimate) return _minEstimate;
        if (estimate > _maxEstimate) return _maxEstimate;

        return estimate;
    }

    /// <summary>
    /// Calculates elapsed time since job start
    /// </summary>
    public TimeSpan? CalculateElapsedTime(string jobId)
    {
        if (!_progressHistory.TryGetValue(jobId, out var samples) || samples.Count == 0)
        {
            return null;
        }

        var firstSample = samples.First();
        return DateTime.UtcNow - firstSample.Timestamp;
    }

    /// <summary>
    /// Clears progress history for a job (after completion or cancellation)
    /// </summary>
    public void ClearHistory(string jobId)
    {
        _progressHistory.Remove(jobId);
    }

    /// <summary>
    /// Gets the average completion time for a specific stage across all jobs
    /// </summary>
    public TimeSpan? GetAverageStageTime(string stage)
    {
        // This would require persistent storage of historical data
        // For now, return stage-based estimates
        return stage.ToLowerInvariant() switch
        {
            "script" or "planning" => TimeSpan.FromSeconds(30),
            "tts" or "audio" or "voice" => TimeSpan.FromMinutes(2),
            "images" or "visuals" or "assets" => TimeSpan.FromMinutes(3),
            "rendering" or "render" or "encode" => TimeSpan.FromMinutes(2),
            _ => TimeSpan.FromMinutes(1)
        };
    }
}

/// <summary>
/// Represents a progress measurement at a point in time
/// </summary>
internal record ProgressSample
{
    public double Percent { get; init; }
    public DateTime Timestamp { get; init; }
}
