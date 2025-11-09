using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Aura.Core.Artifacts;
using Aura.Core.Hardware;
using Aura.Core.Models;
using Aura.Core.Models.Events;
using Aura.Core.Providers;
using Aura.Core.Telemetry;
using Aura.Core.Validation;
using Microsoft.Extensions.Logging;

namespace Aura.Core.Orchestrator;

/// <summary>
/// Manages video generation jobs with background execution and progress tracking.
/// </summary>
public class JobRunner
{
    private readonly ILogger<JobRunner> _logger;
    private readonly ArtifactManager _artifactManager;
    private readonly VideoOrchestrator _orchestrator;
    private readonly IHardwareDetector _hardwareDetector;
    private readonly Services.CheckpointManager? _checkpointManager;
    private readonly Services.CleanupService? _cleanupService;
    private readonly RunTelemetryCollector _telemetryCollector;
    private readonly Services.JobQueueService? _jobQueueService;
    private readonly Services.ProgressEstimator _progressEstimator;
    private readonly Dictionary<string, Job> _activeJobs = new();
    private readonly Dictionary<string, CancellationTokenSource> _jobCancellationTokens = new();
    private readonly Dictionary<string, Guid> _jobProjectIds = new();

    public event EventHandler<JobProgressEventArgs>? JobProgress;

    public JobRunner(
        ILogger<JobRunner> logger,
        ArtifactManager artifactManager,
        VideoOrchestrator orchestrator,
        IHardwareDetector hardwareDetector,
        RunTelemetryCollector telemetryCollector,
        Services.CheckpointManager? checkpointManager = null,
        Services.CleanupService? cleanupService = null,
        Services.JobQueueService? jobQueueService = null,
        Services.ProgressEstimator? progressEstimator = null)
    {
        ArgumentNullException.ThrowIfNull(logger);
        ArgumentNullException.ThrowIfNull(artifactManager);
        ArgumentNullException.ThrowIfNull(orchestrator);
        ArgumentNullException.ThrowIfNull(hardwareDetector);
        ArgumentNullException.ThrowIfNull(telemetryCollector);
        
        _logger = logger;
        _artifactManager = artifactManager;
        _orchestrator = orchestrator;
        _hardwareDetector = hardwareDetector;
        _telemetryCollector = telemetryCollector;
        _checkpointManager = checkpointManager;
        _cleanupService = cleanupService;
        _jobQueueService = jobQueueService;
        _progressEstimator = progressEstimator ?? new Services.ProgressEstimator();
    }

    /// <summary>
    /// Creates a new job and starts execution.
    /// </summary>
    public async Task<Job> CreateAndStartJobAsync(
        Brief brief,
        PlanSpec planSpec,
        VoiceSpec voiceSpec,
        RenderSpec renderSpec,
        string? correlationId = null,
        bool isQuickDemo = false,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(brief);
        ArgumentNullException.ThrowIfNull(planSpec);
        ArgumentNullException.ThrowIfNull(voiceSpec);
        ArgumentNullException.ThrowIfNull(renderSpec);
        
        var jobId = Guid.NewGuid().ToString();
        _logger.LogInformation("Creating new job with ID: {JobId}, Topic: {Topic}, IsQuickDemo: {IsQuickDemo}", 
            jobId, brief.Topic, isQuickDemo);
        
        var nowUtc = DateTime.UtcNow;
        var job = new Job
        {
            Id = jobId,
            Stage = "Script",
            Status = JobStatus.Queued,
            CorrelationId = correlationId ?? Guid.NewGuid().ToString(),
            Brief = brief,
            PlanSpec = planSpec,
            VoiceSpec = voiceSpec,
            RenderSpec = renderSpec,
            CreatedUtc = nowUtc,
            QueuedUtc = nowUtc,
            IsQuickDemo = isQuickDemo
        };

        _activeJobs[job.Id] = job;
        _artifactManager.SaveJob(job);
        
        _logger.LogInformation("Job {JobId} saved to active jobs and artifact storage", jobId);

        // Create a linked cancellation token source that responds to both the provided token and manual cancellation
        var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        _jobCancellationTokens[job.Id] = linkedCts;

        _logger.LogInformation("Starting background execution for job {JobId}", jobId);
        
        // Start execution in background
        _ = Task.Run(async () => await ExecuteJobAsync(job.Id, linkedCts.Token), linkedCts.Token);

        return job;
    }

    /// <summary>
    /// Gets a job by ID.
    /// </summary>
    public Job? GetJob(string jobId)
    {
        if (_activeJobs.TryGetValue(jobId, out var job))
        {
            return job;
        }

        return _artifactManager.LoadJob(jobId);
    }

    /// <summary>
    /// Lists all recent jobs.
    /// </summary>
    public List<Job> ListJobs(int limit = 50)
    {
        return _artifactManager.ListJobs(limit);
    }

    /// <summary>
    /// Cancels a running job.
    /// </summary>
    /// <param name="jobId">The ID of the job to cancel</param>
    /// <returns>True if the job was found and cancellation was requested, false otherwise</returns>
    public bool CancelJob(string jobId)
    {
        _logger.LogInformation("Cancellation requested for job {JobId}", jobId);
        
        // Check if job is active and has a cancellation token
        if (_jobCancellationTokens.TryGetValue(jobId, out var cts))
        {
            try
            {
                cts.Cancel();
                _logger.LogInformation("Cancellation token triggered for job {JobId}", jobId);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error cancelling job {JobId}", jobId);
                return false;
            }
        }
        
        // Job not found or not active
        var job = GetJob(jobId);
        if (job == null)
        {
            _logger.LogWarning("Cannot cancel job {JobId}: job not found", jobId);
            return false;
        }
        
        // Job exists but is not running
        if (job.Status != JobStatus.Running && job.Status != JobStatus.Queued)
        {
            _logger.LogWarning("Cannot cancel job {JobId}: job is in status {Status}", jobId, job.Status);
            return false;
        }
        
        _logger.LogWarning("Job {JobId} is marked as active but has no cancellation token", jobId);
        return false;
    }

    /// <summary>
    /// Retries a failed job with exponential backoff
    /// </summary>
    public async Task<bool> RetryJobAsync(string jobId, CancellationToken ct = default)
    {
        var job = GetJob(jobId);
        if (job == null)
        {
            _logger.LogWarning("Cannot retry job {JobId}: job not found", jobId);
            return false;
        }

        if (job.Status != JobStatus.Failed)
        {
            _logger.LogWarning("Cannot retry job {JobId}: job has not failed (status: {Status})", 
                jobId, job.Status);
            return false;
        }

        // Check if retry is allowed
        if (_jobQueueService != null && !_jobQueueService.CanRetryJob(jobId))
        {
            _logger.LogWarning("Cannot retry job {JobId}: max retry count reached or backoff not elapsed", 
                jobId);
            return false;
        }

        _logger.LogInformation("Retrying job {JobId}", jobId);

        // Create a new job with same parameters
        if (job.Brief != null && job.PlanSpec != null && job.VoiceSpec != null && job.RenderSpec != null)
        {
            var retriedJob = await CreateAndStartJobAsync(
                job.Brief,
                job.PlanSpec,
                job.VoiceSpec,
                job.RenderSpec,
                job.CorrelationId,
                job.IsQuickDemo,
                ct
            );

            // Record retry in queue service
            if (_jobQueueService != null)
            {
                await _jobQueueService.RetryJobAsync(jobId, priority: 3, ct);
            }

            return true;
        }

        _logger.LogWarning("Cannot retry job {JobId}: missing job parameters", jobId);
        return false;
    }

    /// <summary>
    /// Executes a job through all stages.
    /// </summary>
    private async Task ExecuteJobAsync(string jobId, CancellationToken ct)
    {
        try
        {
            var job = GetJob(jobId);
            if (job == null)
            {
                _logger.LogError("Job {JobId} not found", jobId);
                return;
            }

            _logger.LogInformation("Starting job {JobId}", jobId);
            
            // Start telemetry collection for this job
            _telemetryCollector.StartCollection(jobId, job.CorrelationId ?? Guid.NewGuid().ToString());
            _logger.LogInformation("Started telemetry collection for job {JobId}", jobId);
            
            // Create project state for checkpointing if checkpoint manager is available
            Guid? projectId = null;
            if (_checkpointManager != null && job.Brief != null && job.PlanSpec != null && 
                job.VoiceSpec != null && job.RenderSpec != null)
            {
                try
                {
                    projectId = await _checkpointManager.CreateProjectStateAsync(
                        job.Brief.Topic ?? "Untitled Project",
                        jobId,
                        job.Brief,
                        job.PlanSpec,
                        job.VoiceSpec,
                        job.RenderSpec,
                        ct).ConfigureAwait(false);
                    
                    _jobProjectIds[jobId] = projectId.Value;
                    _logger.LogInformation("Created project state {ProjectId} for job {JobId}", projectId, jobId);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to create project state for job {JobId}, continuing without checkpoints", jobId);
                }
            }
            
            // Update to running status with initialization message
            job = UpdateJob(job, 
                status: JobStatus.Running, 
                percent: 0, 
                stage: "Initialization",
                progressMessage: "Initializing job execution",
                startedUtc: DateTime.UtcNow);

            // Detect system profile for orchestration
            _logger.LogInformation("[Job {JobId}] Detecting system hardware...", jobId);
            var systemProfile = await _hardwareDetector.DetectSystemAsync().ConfigureAwait(false);
            _logger.LogInformation("[Job {JobId}] System Profile - Tier: {Tier}, CPU: {Cores} cores, RAM: {Ram}GB, GPU: {Gpu}", 
                jobId, systemProfile.Tier, systemProfile.LogicalCores, systemProfile.RamGB, 
                systemProfile.Gpu?.Model ?? "None");

            // Create progress reporter with detailed stage tracking and ETA estimation
            var progress = new Progress<string>(message =>
            {
                _logger.LogInformation("[Job {JobId}] {Message}", jobId, message);
                
                // Determine stage and progress from message
                var (stage, percent, progressMsg) = ParseProgressMessage(message, job.Stage, job.Percent);
                
                // Record progress for ETA calculation
                _progressEstimator.RecordProgress(jobId, percent, DateTime.UtcNow);
                
                // Calculate ETA and elapsed time
                var eta = _progressEstimator.EstimateTimeRemaining(jobId, percent);
                var elapsed = _progressEstimator.CalculateElapsedTime(jobId);
                
                job = UpdateJob(job, 
                    stage: stage,
                    percent: percent,
                    logs: new List<string>(job.Logs) { $"[{DateTime.UtcNow:yyyy-MM-dd HH:mm:ss}] {message}" },
                    progressMessage: progressMsg,
                    eta: eta);
            });

            // Create detailed progress reporter
            var detailedProgress = new Progress<GenerationProgress>(generationProgress =>
            {
                _logger.LogInformation("[Job {JobId}] Stage: {Stage}, Overall: {Overall}%, {Message}", 
                    jobId, generationProgress.Stage, generationProgress.OverallPercent, generationProgress.Message);
                
                // Update job with detailed progress
                job = UpdateJobWithProgress(job, generationProgress);
                
                // Raise event for SSE streaming
                JobProgress?.Invoke(this, new JobProgressEventArgs 
                { 
                    JobId = jobId,
                    Stage = generationProgress.Stage,
                    Progress = (int)Math.Round(generationProgress.OverallPercent),
                    Status = job.Status,
                    Message = generationProgress.Message,
                    CorrelationId = job.CorrelationId ?? string.Empty
                });
            });

            // Execute orchestrator with system profile and detailed progress
            var outputPath = await _orchestrator.GenerateVideoAsync(
                job.Brief!,
                job.PlanSpec!,
                job.VoiceSpec!,
                job.RenderSpec!,
                systemProfile,
                progress,
                detailedProgress,
                ct,
                jobId,
                job.CorrelationId,
                job.IsQuickDemo
            ).ConfigureAwait(false);

            // Add final artifact
            var artifact = _artifactManager.CreateArtifact(jobId, "video.mp4", outputPath, "video/mp4");
            var artifacts = new List<JobArtifact>(job.Artifacts) { artifact };

            // Mark project as completed if checkpoint manager is available
            if (_checkpointManager != null && _jobProjectIds.TryGetValue(jobId, out var completedProjectId))
            {
                try
                {
                    await _checkpointManager.CompleteProjectAsync(completedProjectId, ct).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to mark project {ProjectId} as completed", completedProjectId);
                }
            }

            // End telemetry collection and persist to disk
            var telemetryPath = _telemetryCollector.EndCollection();
            if (telemetryPath != null)
            {
                _logger.LogInformation("Telemetry data persisted to {Path}", telemetryPath);
            }
            
            // Clear progress estimation history
            _progressEstimator.ClearHistory(jobId);
            
            // Clear retry state if job queue service is available
            _jobQueueService?.ClearRetryState(jobId);

            // Mark as done with output path
            job = UpdateJob(job, 
                status: JobStatus.Done, 
                percent: 100, 
                stage: "Complete",
                artifacts: artifacts,
                outputPath: outputPath,
                finishedAt: DateTime.UtcNow,
                completedUtc: DateTime.UtcNow);

            _logger.LogInformation("Job {JobId} completed successfully. Output: {OutputPath}", jobId, outputPath);
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("Job {JobId} was cancelled", jobId);
            var job = GetJob(jobId);
            if (job != null)
            {
                _logger.LogInformation("Beginning cleanup for cancelled job {JobId} (Stage: {Stage}, Percent: {Percent}%)", 
                    jobId, job.Stage, job.Percent);
                
                // Clean up temporary files and proxies
                if (_cleanupService != null)
                {
                    _logger.LogInformation("Cleaning up temporary files and proxies for cancelled job {JobId}", jobId);
                    _cleanupService.CleanupJob(jobId);
                    _logger.LogInformation("Cleanup completed for job {JobId}", jobId);
                }
                else
                {
                    _logger.LogWarning("CleanupService not available, temporary files may remain for job {JobId}", jobId);
                }
                
                // Mark project as cancelled if checkpoint manager is available
                if (_checkpointManager != null && _jobProjectIds.TryGetValue(jobId, out var cancelledProjectId))
                {
                    try
                    {
                        await _checkpointManager.CancelProjectAsync(cancelledProjectId, default).ConfigureAwait(false);
                        _logger.LogInformation("Project {ProjectId} marked as cancelled", cancelledProjectId);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to mark project {ProjectId} as cancelled", cancelledProjectId);
                    }
                }
                
                // End telemetry collection for cancelled job
                var telemetryPath = _telemetryCollector.EndCollection();
                if (telemetryPath != null)
                {
                    _logger.LogInformation("Telemetry data persisted for cancelled job {JobId} to {Path}", jobId, telemetryPath);
                }
                
                // Clear progress estimation history
                _progressEstimator.ClearHistory(jobId);
                
                // Add cancellation message to logs so it's visible in UI
                var cancelLog = $"[{DateTime.UtcNow:yyyy-MM-dd HH:mm:ss}] Job was cancelled by user - cleanup completed";
                var updatedLogs = new List<string>(job.Logs) { cancelLog };
                
                UpdateJob(job, 
                    status: JobStatus.Canceled, 
                    errorMessage: "Job was cancelled by user",
                    logs: updatedLogs,
                    finishedAt: DateTime.UtcNow,
                    canceledUtc: DateTime.UtcNow);
                
                _logger.LogInformation("Job {JobId} marked as Canceled with cleanup complete", jobId);
            }
        }
        catch (ValidationException vex)
        {
            _logger.LogError("Job {JobId} failed validation: {Message}", jobId, vex.Message);
            var job = GetJob(jobId);
            if (job != null)
            {
                // Mark project as failed if checkpoint manager is available
                if (_checkpointManager != null && _jobProjectIds.TryGetValue(jobId, out var failedProjectId))
                {
                    try
                    {
                        await _checkpointManager.FailProjectAsync(failedProjectId, vex.Message, default).ConfigureAwait(false);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to mark project {ProjectId} as failed", failedProjectId);
                    }
                }
                
                var failureDetails = new JobFailure
                {
                    Stage = job.Stage,
                    Message = vex.Message,
                    CorrelationId = job.CorrelationId ?? string.Empty,
                    FailedAt = DateTime.UtcNow,
                    ErrorCode = "E400-VALIDATION",
                    SuggestedActions = new[]
                    {
                        "Review the validation errors and adjust your input",
                        "Try simplifying your brief or reducing the duration",
                        "Check that all required providers are configured",
                        "Verify system resources are sufficient"
                    }
                };
                
                // End telemetry collection for failed validation
                var telemetryPath = _telemetryCollector.EndCollection();
                if (telemetryPath != null)
                {
                    _logger.LogInformation("Telemetry data persisted for failed job {JobId} to {Path}", jobId, telemetryPath);
                }
                
                // Clear progress estimation history
                _progressEstimator.ClearHistory(jobId);
                
                // Add detailed validation errors to logs
                var errorLog = $"[{DateTime.UtcNow:yyyy-MM-dd HH:mm:ss}] VALIDATION ERROR: {vex.Message}";
                var updatedLogs = new List<string>(job.Logs) { errorLog };
                
                if (vex.Issues.Any())
                {
                    foreach (var issue in vex.Issues)
                    {
                        updatedLogs.Add($"[{DateTime.UtcNow:yyyy-MM-dd HH:mm:ss}]   - {issue}");
                    }
                }
                
                UpdateJob(job, 
                    status: JobStatus.Failed, 
                    errorMessage: vex.Message, 
                    failureDetails: failureDetails,
                    logs: updatedLogs,
                    finishedAt: DateTime.UtcNow);
                
                _artifactManager.SaveJob(job);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Job {JobId} failed: {Message}\nStack Trace: {StackTrace}", 
                jobId, ex.Message, ex.StackTrace);
            var job = GetJob(jobId);
            if (job != null)
            {
                // Mark project as failed if checkpoint manager is available
                if (_checkpointManager != null && _jobProjectIds.TryGetValue(jobId, out var failedProjectId))
                {
                    try
                    {
                        await _checkpointManager.FailProjectAsync(failedProjectId, ex.Message, default).ConfigureAwait(false);
                    }
                    catch (Exception checkpointEx)
                    {
                        _logger.LogWarning(checkpointEx, "Failed to mark project {ProjectId} as failed", failedProjectId);
                    }
                }
                
                // End telemetry collection for failed job
                var telemetryPath = _telemetryCollector.EndCollection();
                if (telemetryPath != null)
                {
                    _logger.LogInformation("Telemetry data persisted for failed job {JobId} to {Path}", jobId, telemetryPath);
                }
                
                // Clear progress estimation history
                _progressEstimator.ClearHistory(jobId);
                
                var failureDetails = CreateFailureDetails(job, ex);
                
                // Add error to logs so it's visible in UI
                var errorLog = $"[{DateTime.UtcNow:yyyy-MM-dd HH:mm:ss}] ERROR: {GetFriendlyErrorMessage(ex)}";
                var updatedLogs = new List<string>(job.Logs) { errorLog };
                
                // Add inner exception details if available
                if (ex.InnerException != null)
                {
                    updatedLogs.Add($"[{DateTime.UtcNow:yyyy-MM-dd HH:mm:ss}] Inner Exception: {ex.InnerException.Message}");
                }
                
                // Add stack trace for debugging (truncated)
                var stackLines = ex.StackTrace?.Split('\n').Take(5) ?? Array.Empty<string>();
                foreach (var line in stackLines)
                {
                    updatedLogs.Add($"[{DateTime.UtcNow:yyyy-MM-dd HH:mm:ss}]   {line.Trim()}");
                }
                
                UpdateJob(job, 
                    status: JobStatus.Failed, 
                    errorMessage: GetFriendlyErrorMessage(ex), 
                    failureDetails: failureDetails,
                    logs: updatedLogs,
                    finishedAt: DateTime.UtcNow);
                
                // Save job state to artifact manager
                _artifactManager.SaveJob(job);
            }
        }
        finally
        {
            _activeJobs.Remove(jobId);
            
            // Clean up cancellation token
            if (_jobCancellationTokens.TryGetValue(jobId, out var cts))
            {
                cts.Dispose();
                _jobCancellationTokens.Remove(jobId);
            }
        }
    }

    /// <summary>
    /// Updates a job with detailed progress information and persists changes.
    /// </summary>
    private Job UpdateJobWithProgress(
        Job job,
        GenerationProgress generationProgress)
    {
        // Add to progress history
        var updatedHistory = new List<GenerationProgress>(job.ProgressHistory) { generationProgress };
        
        // Calculate percent from overall progress
        var percent = (int)Math.Round(generationProgress.OverallPercent);
        
        // Build log message from progress
        var logMessage = generationProgress.SubstageDetail != null 
            ? $"{generationProgress.Message} - {generationProgress.SubstageDetail}"
            : generationProgress.Message;
        var logs = new List<string>(job.Logs) 
        { 
            $"[{DateTime.UtcNow:yyyy-MM-dd HH:mm:ss}] {logMessage}" 
        };
        
        // Update using existing method
        var updatedJob = UpdateJob(
            job,
            stage: generationProgress.Stage,
            percent: percent,
            logs: logs,
            progressMessage: generationProgress.Message,
            eta: generationProgress.EstimatedTimeRemaining
        );
        
        // Add progress history and current progress
        var finalJob = updatedJob with
        {
            ProgressHistory = updatedHistory,
            CurrentProgress = generationProgress
        };
        
        // Update active jobs and persist
        _activeJobs[finalJob.Id] = finalJob;
        _artifactManager.SaveJob(finalJob);
        
        return finalJob;
    }

    /// <summary>
    /// Updates a job and persists changes with state validation and monotonic progress.
    /// </summary>
    private Job UpdateJob(
        Job job,
        string? stage = null,
        JobStatus? status = null,
        int? percent = null,
        TimeSpan? eta = null,
        List<JobArtifact>? artifacts = null,
        List<string>? logs = null,
        DateTime? finishedAt = null,
        string? errorMessage = null,
        JobFailure? failureDetails = null,
        string? progressMessage = null,
        DateTime? startedUtc = null,
        DateTime? completedUtc = null,
        DateTime? canceledUtc = null,
        string? outputPath = null)
    {
        // Validate state transition if status is changing
        var newStatus = status ?? job.Status;
        if (newStatus != job.Status && !job.CanTransitionTo(newStatus))
        {
            _logger.LogWarning("[Job {JobId}] Invalid state transition from {OldStatus} to {NewStatus}, transition rejected",
                job.Id, job.Status, newStatus);
            newStatus = job.Status; // Keep old status
        }
        
        // Enforce monotonic progress (never decrease)
        var newPercent = percent ?? job.Percent;
        if (newPercent < job.Percent)
        {
            _logger.LogDebug("[Job {JobId}] Progress would decrease from {OldPercent}% to {NewPercent}%, keeping old value",
                job.Id, job.Percent, newPercent);
            newPercent = job.Percent;
        }
        
        // Ensure EndedUtc is set for terminal states
        DateTime? endedUtc = job.EndedUtc;
        if (IsTerminalStatus(newStatus) && endedUtc == null)
        {
            endedUtc = completedUtc ?? canceledUtc ?? DateTime.UtcNow;
        }
        
        var updated = job with
        {
            Stage = stage ?? job.Stage,
            Status = newStatus,
            Percent = Math.Clamp(newPercent, 0, 100),
            Eta = eta ?? job.Eta,
            Artifacts = artifacts ?? job.Artifacts,
            Logs = logs ?? job.Logs,
            FinishedAt = finishedAt ?? job.FinishedAt,
            ErrorMessage = errorMessage ?? job.ErrorMessage,
            FailureDetails = failureDetails ?? job.FailureDetails,
            StartedUtc = startedUtc ?? job.StartedUtc,
            CompletedUtc = completedUtc ?? job.CompletedUtc,
            CanceledUtc = canceledUtc ?? job.CanceledUtc,
            EndedUtc = endedUtc,
            OutputPath = outputPath ?? job.OutputPath
        };

        _activeJobs[job.Id] = updated;
        _artifactManager.SaveJob(updated);

        // Log job state changes for debugging
        _logger.LogInformation("[Job {JobId}] Updated: Status={Status}, Stage={Stage}, Percent={Percent}%", 
            updated.Id, updated.Status, updated.Stage, updated.Percent);

        // Raise progress event with detailed information
        var eventArgs = new JobProgressEventArgs(
            jobId: updated.Id,
            progress: updated.Percent,
            status: updated.Status,
            stage: updated.Stage,
            message: progressMessage ?? GetProgressMessage(updated),
            correlationId: updated.CorrelationId ?? string.Empty,
            eta: updated.Eta
        );
        
        JobProgress?.Invoke(this, eventArgs);

        return updated;
    }
    
    /// <summary>
    /// Checks if a status is terminal (no further transitions allowed)
    /// </summary>
    private static bool IsTerminalStatus(JobStatus status)
    {
        return status is JobStatus.Done or JobStatus.Succeeded or JobStatus.Failed or JobStatus.Canceled;
    }

    /// <summary>
    /// Gets a human-readable progress message for a job
    /// </summary>
    private string GetProgressMessage(Job job)
    {
        if (!string.IsNullOrEmpty(job.ErrorMessage) && job.Status == JobStatus.Failed)
        {
            return job.ErrorMessage;
        }

        return job.Status switch
        {
            JobStatus.Queued => "Job is queued for execution",
            JobStatus.Running => $"Processing: {job.Stage}",
            JobStatus.Done => "Job completed successfully",
            JobStatus.Failed => "Job failed",
            JobStatus.Canceled => "Job was canceled",
            _ => $"Status: {job.Status}"
        };
    }

    /// <summary>
    /// Creates detailed failure information from an exception
    /// </summary>
    private JobFailure CreateFailureDetails(Job job, Exception ex)
    {
        var failureDetails = new JobFailure
        {
            Stage = job.Stage,
            Message = GetFriendlyErrorMessage(ex),
            CorrelationId = job.CorrelationId ?? string.Empty,
            FailedAt = DateTime.UtcNow
        };

        // Check if this is an FFmpeg-related error
        if (ex.Message.Contains("FFmpeg") || ex.Message.Contains("render failed"))
        {
            failureDetails = failureDetails with
            {
                ErrorCode = "E304-FFMPEG_RUNTIME",
                StderrSnippet = TryReadFfmpegLog(job.Id),
                LogPath = GetFfmpegLogPath(job.Id),
                SuggestedActions = new[]
                {
                    "Try using software encoder (x264) instead of hardware acceleration",
                    "Verify FFmpeg is properly installed using Dependencies page",
                    "Check the full log for detailed error information",
                    "Retry the render with different settings"
                }
            };
        }
        else
        {
            failureDetails = failureDetails with
            {
                SuggestedActions = new[]
                {
                    "Check the logs for more details",
                    "Verify all dependencies are installed",
                    "Retry the operation",
                    "Contact support if the issue persists"
                }
            };
        }

        return failureDetails;
    }

    /// <summary>
    /// Attempts to read the last 16KB of FFmpeg log for a job
    /// </summary>
    private string? TryReadFfmpegLog(string jobId)
    {
        try
        {
            var logPath = GetFfmpegLogPath(jobId);
            if (!File.Exists(logPath))
            {
                return null;
            }

            // Read last 16KB of the log file
            var fileInfo = new FileInfo(logPath);
            var maxBytes = 16 * 1024; // 16KB

            using var fileStream = new FileStream(logPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            
            if (fileInfo.Length <= maxBytes)
            {
                // Read entire file if smaller than max
                using var reader = new StreamReader(fileStream);
                return reader.ReadToEnd();
            }
            else
            {
                // Read last 16KB
                fileStream.Seek(-maxBytes, SeekOrigin.End);
                using var reader = new StreamReader(fileStream);
                var snippet = reader.ReadToEnd();
                return $"... (showing last 16KB)\n{snippet}";
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to read FFmpeg log for job {JobId}", jobId);
            return null;
        }
    }

    /// <summary>
    /// Gets the path to the FFmpeg log file for a job
    /// </summary>
    private string GetFfmpegLogPath(string jobId)
    {
        var logsDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Aura", "Logs", "ffmpeg");
        return Path.Combine(logsDir, $"{jobId}.log");
    }

    /// <summary>
    /// Extracts a friendly error message from an exception
    /// </summary>
    private string GetFriendlyErrorMessage(Exception ex)
    {
        // Extract the first line of the exception message for a cleaner display
        var message = ex.Message.Split('\n')[0];
        
        // Try to extract a user-friendly portion
        if (message.Contains("FFmpeg render failed"))
        {
            return "Render failed due to FFmpeg error";
        }
        
        return message.Length > 200 ? message.Substring(0, 197) + "..." : message;
    }

    /// <summary>
    /// Parses a progress message to extract stage, progress percentage, and formatted message
    /// </summary>
    private (string stage, int percent, string message) ParseProgressMessage(string message, string currentStage, int currentPercent)
    {
        var stage = currentStage;
        var percent = currentPercent;
        var formattedMessage = message;

        // Parse stage transitions from orchestrator messages with more granular progress
        if (message.Contains("Validating system", StringComparison.OrdinalIgnoreCase))
        {
            stage = "Initialization";
            percent = 2;
            formattedMessage = "Validating system readiness";
        }
        else if (message.Contains("Stage 1/5", StringComparison.OrdinalIgnoreCase) ||
                 message.Contains("Generating script", StringComparison.OrdinalIgnoreCase))
        {
            stage = "Script";
            percent = 15;
            formattedMessage = "Generating video script with AI";
        }
        else if (message.Contains("Stage 2/5", StringComparison.OrdinalIgnoreCase) ||
                 message.Contains("Parsing scenes", StringComparison.OrdinalIgnoreCase))
        {
            stage = "Script";
            percent = 25;
            formattedMessage = "Parsing script into scenes";
        }
        else if (message.Contains("Stage 3/5", StringComparison.OrdinalIgnoreCase) ||
                 message.Contains("Generating narration", StringComparison.OrdinalIgnoreCase) || 
                 message.Contains("voice", StringComparison.OrdinalIgnoreCase))
        {
            stage = "Voice";
            percent = 40;
            formattedMessage = "Generating voice narration";
        }
        else if (message.Contains("Stage 4/5", StringComparison.OrdinalIgnoreCase) ||
                 message.Contains("Building timeline", StringComparison.OrdinalIgnoreCase))
        {
            stage = "Visuals";
            percent = 55;
            formattedMessage = "Preparing visual timeline";
        }
        else if (message.Contains("visual", StringComparison.OrdinalIgnoreCase) ||
                 message.Contains("image", StringComparison.OrdinalIgnoreCase) ||
                 message.Contains("asset", StringComparison.OrdinalIgnoreCase))
        {
            stage = "Visuals";
            percent = 65;
            formattedMessage = "Generating visual assets";
        }
        else if (message.Contains("Stage 5/5", StringComparison.OrdinalIgnoreCase) ||
                 message.Contains("render", StringComparison.OrdinalIgnoreCase) ||
                 message.Contains("composing video", StringComparison.OrdinalIgnoreCase))
        {
            stage = "Rendering";
            percent = 80;
            formattedMessage = "Rendering final video";
        }
        else if (message.Contains("Rendering:", StringComparison.OrdinalIgnoreCase))
        {
            // Extract percentage from render progress if available
            stage = "Rendering";
            // Try to parse "Rendering: X%" from message
            var percentMatch = System.Text.RegularExpressions.Regex.Match(message, @"(\d+(?:\.\d+)?)\s*%");
            if (percentMatch.Success && double.TryParse(percentMatch.Groups[1].Value, out double renderPercent))
            {
                // Map render progress (0-100%) to overall progress (80-95%)
                percent = 80 + (int)(renderPercent * 0.15);
            }
            else
            {
                percent = 85;
            }
            formattedMessage = message;
        }
        else if (message.Contains("complete", StringComparison.OrdinalIgnoreCase))
        {
            stage = "Complete";
            percent = 100;
            formattedMessage = "Video generation complete";
        }

        // Ensure progress never goes backwards
        if (percent < currentPercent)
        {
            percent = currentPercent;
        }

        return (stage, percent, formattedMessage);
    }
}
