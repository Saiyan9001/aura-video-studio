using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Channels;
using Aura.Core.Models;

namespace Aura.Core.Orchestrator;

/// <summary>
/// Pipeline context that passes data and state between orchestration stages.
/// Implements a memory-efficient streaming architecture using System.Threading.Channels.
/// </summary>
public sealed class PipelineContext : IDisposable
{
    private bool _disposed;
    private readonly Dictionary<string, object> _stageOutputs = new();
    private readonly Dictionary<string, PipelineStageMetrics> _metrics = new();
    private readonly object _lock = new();

    public PipelineContext(
        string correlationId,
        Brief brief,
        PlanSpec planSpec,
        VoiceSpec voiceSpec,
        RenderSpec renderSpec,
        SystemProfile systemProfile)
    {
        CorrelationId = correlationId ?? throw new ArgumentNullException(nameof(correlationId));
        Brief = brief ?? throw new ArgumentNullException(nameof(brief));
        PlanSpec = planSpec ?? throw new ArgumentNullException(nameof(planSpec));
        VoiceSpec = voiceSpec ?? throw new ArgumentNullException(nameof(voiceSpec));
        RenderSpec = renderSpec ?? throw new ArgumentNullException(nameof(renderSpec));
        SystemProfile = systemProfile ?? throw new ArgumentNullException(nameof(systemProfile));
        
        StartedAt = DateTime.UtcNow;
        State = PipelineState.Initialized;
        
        // Create channels for inter-stage communication
        ScriptChannel = Channel.CreateUnbounded<string>(new UnboundedChannelOptions
        {
            SingleReader = true,
            SingleWriter = true
        });
        
        SceneChannel = Channel.CreateUnbounded<Scene>(new UnboundedChannelOptions
        {
            SingleReader = false,
            SingleWriter = false
        });
        
        AssetChannel = Channel.CreateUnbounded<AssetBatch>(new UnboundedChannelOptions
        {
            SingleReader = true,
            SingleWriter = false
        });
    }

    // Input specifications
    public string CorrelationId { get; }
    public Brief Brief { get; }
    public PlanSpec PlanSpec { get; }
    public VoiceSpec VoiceSpec { get; }
    public RenderSpec RenderSpec { get; }
    public SystemProfile SystemProfile { get; }

    // Pipeline state
    public PipelineState State { get; set; }
    public string CurrentStage { get; set; } = "Initialization";
    public DateTime StartedAt { get; }
    public DateTime? CompletedAt { get; private set; }

    // Channels for stage communication
    public Channel<string> ScriptChannel { get; }
    public Channel<Scene> SceneChannel { get; }
    public Channel<AssetBatch> AssetChannel { get; }

    // Stage outputs (for stages that don't use channels)
    public string? GeneratedScript { get; set; }
    public List<Scene>? ParsedScenes { get; set; }
    public string? NarrationPath { get; set; }
    public string? MusicPath { get; set; }
    public string? SubtitlesPath { get; set; }
    public Dictionary<int, IReadOnlyList<Asset>> SceneAssets { get; set; } = new();
    public string? FinalVideoPath { get; set; }

    // Checkpoint data
    public Guid? CheckpointProjectId { get; set; }
    public string? LastCheckpointStage { get; set; }

    // Error tracking
    public List<PipelineError> Errors { get; } = new();
    public int RetryCount { get; set; }

    /// <summary>
    /// Stores output from a specific stage
    /// </summary>
    public void SetStageOutput<T>(string stageName, T output)
    {
        lock (_lock)
        {
            _stageOutputs[stageName] = output!;
        }
    }

    /// <summary>
    /// Retrieves output from a specific stage
    /// </summary>
    public T? GetStageOutput<T>(string stageName)
    {
        lock (_lock)
        {
            if (_stageOutputs.TryGetValue(stageName, out var output) && output is T typed)
            {
                return typed;
            }
            return default;
        }
    }

    /// <summary>
    /// Records metrics for a completed stage
    /// </summary>
    public void RecordStageMetrics(string stageName, PipelineStageMetrics metrics)
    {
        lock (_lock)
        {
            _metrics[stageName] = metrics;
        }
    }

    /// <summary>
    /// Gets metrics for a specific stage
    /// </summary>
    public PipelineStageMetrics? GetStageMetrics(string stageName)
    {
        lock (_lock)
        {
            return _metrics.TryGetValue(stageName, out var metrics) ? metrics : null;
        }
    }

    /// <summary>
    /// Gets all recorded stage metrics
    /// </summary>
    public IReadOnlyDictionary<string, PipelineStageMetrics> GetAllMetrics()
    {
        lock (_lock)
        {
            return new Dictionary<string, PipelineStageMetrics>(_metrics);
        }
    }

    /// <summary>
    /// Records an error that occurred during pipeline execution
    /// </summary>
    public void RecordError(string stageName, Exception exception, bool isRecoverable)
    {
        lock (_lock)
        {
            Errors.Add(new PipelineError
            {
                StageName = stageName,
                Exception = exception,
                Message = exception.Message,
                Timestamp = DateTime.UtcNow,
                IsRecoverable = isRecoverable
            });
        }
    }

    /// <summary>
    /// Marks the pipeline as completed
    /// </summary>
    public void MarkCompleted()
    {
        CompletedAt = DateTime.UtcNow;
        State = PipelineState.Completed;
    }

    /// <summary>
    /// Marks the pipeline as failed
    /// </summary>
    public void MarkFailed()
    {
        CompletedAt = DateTime.UtcNow;
        State = PipelineState.Failed;
    }

    /// <summary>
    /// Marks the pipeline as cancelled
    /// </summary>
    public void MarkCancelled()
    {
        CompletedAt = DateTime.UtcNow;
        State = PipelineState.Cancelled;
    }

    /// <summary>
    /// Gets the total elapsed time
    /// </summary>
    public TimeSpan GetElapsedTime()
    {
        var endTime = CompletedAt ?? DateTime.UtcNow;
        return endTime - StartedAt;
    }

    public void Dispose()
    {
        if (_disposed) return;
        
        // Complete all channels to prevent deadlocks
        ScriptChannel.Writer.TryComplete();
        SceneChannel.Writer.TryComplete();
        AssetChannel.Writer.TryComplete();
        
        _disposed = true;
    }
}

/// <summary>
/// State of the pipeline execution
/// </summary>
public enum PipelineState
{
    Initialized,
    Running,
    Completed,
    Failed,
    Cancelled,
    Paused
}

/// <summary>
/// Batch of assets for efficient channel communication
/// </summary>
public record AssetBatch
{
    public int SceneIndex { get; init; }
    public IReadOnlyList<Asset> Assets { get; init; } = Array.Empty<Asset>();
    public DateTime GeneratedAt { get; init; } = DateTime.UtcNow;
}

/// <summary>
/// Performance metrics for a pipeline stage
/// </summary>
public record PipelineStageMetrics
{
    public string StageName { get; init; } = string.Empty;
    public DateTime StartTime { get; init; }
    public DateTime EndTime { get; init; }
    public TimeSpan Duration => EndTime - StartTime;
    
    // Performance metrics
    public long MemoryUsedBytes { get; init; }
    public double CpuPercent { get; init; }
    
    // Stage-specific metrics
    public int ItemsProcessed { get; init; }
    public int ItemsFailed { get; init; }
    public int RetryCount { get; init; }
    
    // Provider information
    public string? ProviderUsed { get; init; }
    public string? ProviderModel { get; init; }
    
    // Cost tracking
    public decimal? EstimatedCost { get; init; }
    
    // Quality metrics
    public Dictionary<string, object> CustomMetrics { get; init; } = new();
}

/// <summary>
/// Error information for pipeline stage failures
/// </summary>
public record PipelineError
{
    public string StageName { get; init; } = string.Empty;
    public Exception Exception { get; init; } = null!;
    public string Message { get; init; } = string.Empty;
    public DateTime Timestamp { get; init; }
    public bool IsRecoverable { get; init; }
    public int AttemptNumber { get; init; }
}

/// <summary>
/// Configuration for pipeline execution behavior
/// </summary>
public record PipelineConfiguration
{
    /// <summary>
    /// Enable checkpoint/resume functionality
    /// </summary>
    public bool EnableCheckpoints { get; init; } = true;

    /// <summary>
    /// Checkpoint frequency (number of stages between checkpoints)
    /// </summary>
    public int CheckpointFrequency { get; init; } = 1;

    /// <summary>
    /// Enable performance metrics collection
    /// </summary>
    public bool EnableMetrics { get; init; } = true;

    /// <summary>
    /// Maximum retry attempts per stage
    /// </summary>
    public int MaxRetryAttempts { get; init; } = 3;

    /// <summary>
    /// Enable circuit breaker pattern for providers
    /// </summary>
    public bool EnableCircuitBreaker { get; init; } = true;

    /// <summary>
    /// Maximum concurrent operations for parallel stages
    /// </summary>
    public int MaxConcurrency { get; init; } = 3;

    /// <summary>
    /// Enable memory-efficient streaming for large assets
    /// </summary>
    public bool EnableStreaming { get; init; } = true;

    /// <summary>
    /// Buffer size for channel operations (in items)
    /// </summary>
    public int ChannelBufferSize { get; init; } = 10;

    /// <summary>
    /// Timeout for individual stage execution
    /// </summary>
    public TimeSpan StageTimeout { get; init; } = TimeSpan.FromMinutes(10);

    /// <summary>
    /// Timeout for the entire pipeline
    /// </summary>
    public TimeSpan PipelineTimeout { get; init; } = TimeSpan.FromHours(1);
}
