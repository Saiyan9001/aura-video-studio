using System;
using System.Collections.Generic;

namespace Aura.Core.Models;

/// <summary>
/// Represents a video generation job with stage-by-stage progress tracking.
/// 
/// State Machine Invariants:
/// - Queued → Running → (Done | Failed | Canceled)
/// - Terminal states (Done, Failed, Canceled) cannot transition to any other state
/// - Progress (Percent) must be monotonically increasing (never decreases)
/// - EndedUtc must be set when entering any terminal state
/// 
/// Timestamp Invariants:
/// - CreatedUtc: Set on job creation, never changes
/// - QueuedUtc: Set when job enters Queued state (same as CreatedUtc for new jobs)
/// - StartedUtc: Set when job transitions from Queued to Running
/// - CompletedUtc: Set when job transitions to Done/Succeeded
/// - CanceledUtc: Set when job transitions to Canceled
/// - EndedUtc: Set to CompletedUtc, CanceledUtc, or FailedAt when entering terminal state
/// 
/// Resumability Rules:
/// - Jobs can only be resumed if CanResume is true
/// - Resume is supported for jobs that failed at specific checkpoints
/// - LastCompletedStep indicates the last successful checkpoint
/// - Not all stages support resumption (e.g., rendering is atomic)
/// </summary>
public record Job
{
    /// <summary>Unique identifier for the job</summary>
    public string Id { get; init; } = Guid.NewGuid().ToString();
    
    /// <summary>Current stage of execution (Script, Voice, Visuals, Rendering, Complete)</summary>
    public string Stage { get; init; } = "Plan";
    
    /// <summary>Current status following state machine rules</summary>
    public JobStatus Status { get; init; } = JobStatus.Queued;
    
    /// <summary>Progress percentage (0-100), monotonically increasing</summary>
    public int Percent { get; init; } = 0;
    
    /// <summary>Estimated time remaining for completion</summary>
    public TimeSpan? Eta { get; init; }
    
    /// <summary>Artifacts produced during job execution (video, subtitles, etc.)</summary>
    public List<JobArtifact> Artifacts { get; init; } = new();
    
    /// <summary>Execution logs for debugging and progress tracking</summary>
    public List<string> Logs { get; init; } = new();
    
    /// <summary>Legacy: When job started execution (use StartedUtc instead)</summary>
    public DateTime StartedAt { get; init; } = DateTime.UtcNow;
    
    /// <summary>Legacy: When job finished (use EndedUtc instead)</summary>
    public DateTime? FinishedAt { get; init; }
    
    /// <summary>Correlation ID for request tracing</summary>
    public string? CorrelationId { get; init; }
    
    /// <summary>User-friendly error message if job failed</summary>
    public string? ErrorMessage { get; init; }
    
    /// <summary>Detailed failure information if job failed</summary>
    public JobFailure? FailureDetails { get; init; }
    
    /// <summary>Original brief for the job</summary>
    public Brief? Brief { get; init; }
    
    /// <summary>Plan specification for the job</summary>
    public PlanSpec? PlanSpec { get; init; }
    
    /// <summary>Voice specification for the job</summary>
    public VoiceSpec? VoiceSpec { get; init; }
    
    /// <summary>Render specification for the job</summary>
    public RenderSpec? RenderSpec { get; init; }
    
    /// <summary>UTC timestamp when job was created (immutable)</summary>
    public DateTime CreatedUtc { get; init; } = DateTime.UtcNow;
    
    /// <summary>UTC timestamp when job was added to queue (typically same as CreatedUtc)</summary>
    public DateTime QueuedUtc { get; init; } = DateTime.UtcNow;
    
    /// <summary>UTC timestamp when job started running (Queued → Running transition)</summary>
    public DateTime? StartedUtc { get; init; }
    
    /// <summary>UTC timestamp when job completed successfully (Running → Done transition)</summary>
    public DateTime? CompletedUtc { get; init; }
    
    /// <summary>UTC timestamp when job was canceled (Running → Canceled transition)</summary>
    public DateTime? CanceledUtc { get; init; }
    
    /// <summary>UTC timestamp when job reached terminal state (Done, Failed, or Canceled)</summary>
    public DateTime? EndedUtc { get; init; }
    
    /// <summary>Individual steps within the job for granular tracking</summary>
    public List<JobStep> Steps { get; init; } = new();
    
    /// <summary>Final output information for completed jobs</summary>
    public JobOutput? Output { get; init; }
    
    /// <summary>Non-fatal warnings collected during execution</summary>
    public List<string> Warnings { get; init; } = new();
    
    /// <summary>Errors collected during execution (may include recoverable errors)</summary>
    public List<JobStepError> Errors { get; init; } = new();
    
    /// <summary>Last successfully completed step for resume support</summary>
    public string? LastCompletedStep { get; init; }
    
    /// <summary>Whether this job can be resumed from LastCompletedStep</summary>
    public bool CanResume { get; init; } = false;
    
    /// <summary>Whether this job is a Quick Demo with resilient fallback behavior</summary>
    public bool IsQuickDemo { get; init; } = false;
    
    /// <summary>Final output path for the rendered video (populated after successful render)</summary>
    public string? OutputPath { get; init; }
    
    /// <summary>Progress history for recovery and replay</summary>
    public List<GenerationProgress> ProgressHistory { get; init; } = new();
    
    /// <summary>Current detailed progress information</summary>
    public GenerationProgress? CurrentProgress { get; init; }
    
    /// <summary>
    /// Creates a new Job with updated progress, ensuring monotonic invariant (progress never decreases)
    /// </summary>
    public Job WithMonotonicProgress(int newPercent)
    {
        var safePercent = Math.Max(this.Percent, Math.Clamp(newPercent, 0, 100));
        return this with { Percent = safePercent };
    }
    
    /// <summary>
    /// Validates job state transition is legal according to state machine rules.
    /// 
    /// Valid State Transitions:
    /// - Queued → Running (job starts execution)
    /// - Queued → Canceled (job canceled before starting)
    /// - Running → Done/Succeeded (job completed successfully)
    /// - Running → Failed (job encountered an error)
    /// - Running → Canceled (job canceled during execution)
    /// 
    /// Terminal States (no outbound transitions):
    /// - Done/Succeeded
    /// - Failed
    /// - Canceled
    /// 
    /// Note: Same state transition is always allowed (idempotent updates)
    /// </summary>
    /// <param name="newStatus">The target status to transition to</param>
    /// <returns>True if transition is valid, false otherwise</returns>
    public bool CanTransitionTo(JobStatus newStatus)
    {
        return (this.Status, newStatus) switch
        {
            // Queued can transition to Running or Canceled
            (JobStatus.Queued, JobStatus.Running) => true,
            (JobStatus.Queued, JobStatus.Canceled) => true,
            
            // Running can transition to Done, Failed, or Canceled
            (JobStatus.Running, JobStatus.Done) => true,
            (JobStatus.Running, JobStatus.Succeeded) => true,
            (JobStatus.Running, JobStatus.Failed) => true,
            (JobStatus.Running, JobStatus.Canceled) => true,
            
            // Terminal states cannot transition
            (JobStatus.Done, _) => false,
            (JobStatus.Succeeded, _) => false,
            (JobStatus.Failed, _) => false,
            (JobStatus.Canceled, _) => false,
            
            // Same state is always valid (no-op)
            _ when this.Status == newStatus => true,
            
            // All other transitions are invalid
            _ => false
        };
    }
    
    /// <summary>
    /// Checks if the current job status is a terminal state
    /// </summary>
    /// <returns>True if the job is in a terminal state (Done, Failed, or Canceled)</returns>
    public bool IsTerminal()
    {
        return Status is JobStatus.Done or JobStatus.Succeeded or JobStatus.Failed or JobStatus.Canceled;
    }
}

/// <summary>
/// Status of a job.
/// </summary>
public enum JobStatus
{
    Queued,
    Running,
    Done,
    Failed,
    Skipped,
    Canceled,
    Succeeded  // Alias for Done to match SSE contract
}

/// <summary>
/// Represents an artifact produced during job execution.
/// </summary>
public record JobArtifact(
    string Name,
    string Path,
    string Type,
    long SizeBytes,
    DateTime CreatedAt);
