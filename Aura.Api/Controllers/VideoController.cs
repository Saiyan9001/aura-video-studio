using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Aura.Api.Models.ApiModels.V1;
using Aura.Api.Services;
using Aura.Core.Models;
using Aura.Core.Orchestrator;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace Aura.Api.Controllers;

/// <summary>
/// Controller for video generation operations with SSE support
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class VideoController : ControllerBase
{
    private readonly ILogger<VideoController> _logger;
    private readonly JobRunner _jobRunner;
    private readonly SseService _sseService;

    public VideoController(
        ILogger<VideoController> logger,
        JobRunner jobRunner,
        SseService sseService)
    {
        _logger = logger;
        _jobRunner = jobRunner;
        _sseService = sseService;
    }

    /// <summary>
    /// Generate a new video from brief and specifications
    /// </summary>
    /// <param name="request">Video generation request</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Job ID and initial status</returns>
    [HttpPost("generate")]
    [ProducesResponseType(typeof(VideoGenerationResponse), StatusCodes.Status202Accepted)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> GenerateVideo(
        [FromBody] VideoGenerationRequest request,
        CancellationToken ct = default)
    {
        var correlationId = HttpContext.TraceIdentifier;
        
        try
        {
            _logger.LogInformation(
                "[{CorrelationId}] POST /api/videos/generate - Brief: {Brief}, Duration: {Duration}m",
                correlationId, request.Brief.Substring(0, Math.Min(50, request.Brief.Length)), request.DurationMinutes);

            // Create brief from request
            var brief = new Brief(
                Topic: request.Brief,
                Audience: request.Options?.Audience ?? "general audience",
                Goal: request.Options?.Goal ?? "inform and engage",
                Tone: request.Options?.Tone ?? "professional",
                Language: request.Options?.Language ?? "English",
                Aspect: ParseAspect(request.Options?.Aspect),
                RagConfiguration: null
            );

            // Create plan spec
            var planSpec = new PlanSpec(
                TargetDuration: TimeSpan.FromMinutes(request.DurationMinutes),
                Pacing: ParsePacing(request.Options?.Pacing),
                Density: ParseDensity(request.Options?.Density),
                Style: request.Style ?? "informative"
            );

            // Create voice spec
            var voiceSpec = new VoiceSpec(
                VoiceName: request.VoiceId ?? "default",
                Rate: 1.0,
                Pitch: 0.0,
                Pause: Core.Models.PauseStyle.Natural
            );

            // Create render spec
            var renderSpec = new RenderSpec(
                Res: new Resolution(
                    Width: request.Options?.Width ?? 1920,
                    Height: request.Options?.Height ?? 1080
                ),
                Container: "mp4",
                VideoBitrateK: 5000,
                AudioBitrateK: 192,
                Fps: request.Options?.Fps ?? 30,
                Codec: request.Options?.Codec ?? "H264",
                QualityLevel: 75,
                EnableSceneCut: true
            );

            // Create and start job via JobRunner
            var job = await _jobRunner.CreateAndStartJobAsync(
                brief,
                planSpec,
                voiceSpec,
                renderSpec,
                correlationId,
                isQuickDemo: false,
                ct
            );

            _logger.LogInformation(
                "[{CorrelationId}] Video generation job created: {JobId}",
                correlationId, job.Id);

            // Create response
            var response = new VideoGenerationResponse(
                JobId: job.Id,
                Status: MapJobStatus(job.Status),
                VideoUrl: null,
                CreatedAt: job.CreatedUtc,
                CorrelationId: correlationId
            );

            // Set Location header for job status endpoint
            var locationUri = Url.Action(
                nameof(GetVideoStatus),
                new { id = job.Id }) ?? $"/api/videos/{job.Id}/status";

            return AcceptedAtAction(
                actionName: nameof(GetVideoStatus),
                routeValues: new { id = job.Id },
                value: response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[{CorrelationId}] Error generating video", correlationId);
            
            return StatusCode(
                StatusCodes.Status500InternalServerError,
                CreateProblemDetails(
                    "Video Generation Failed",
                    $"An error occurred while creating the video generation job: {ex.Message}",
                    StatusCodes.Status500InternalServerError,
                    correlationId));
        }
    }

    /// <summary>
    /// Get current status of a video generation job
    /// </summary>
    /// <param name="id">Job ID</param>
    /// <returns>Video status information</returns>
    [HttpGet("{id}/status")]
    [ProducesResponseType(typeof(VideoStatus), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public IActionResult GetVideoStatus(string id)
    {
        var correlationId = HttpContext.TraceIdentifier;
        
        try
        {
            _logger.LogInformation("[{CorrelationId}] GET /api/videos/{Id}/status", correlationId, id);

            var job = _jobRunner.GetJob(id);
            if (job == null)
            {
                return NotFound(CreateProblemDetails(
                    "Job Not Found",
                    $"Video generation job {id} was not found",
                    StatusCodes.Status404NotFound,
                    correlationId));
            }

            var status = MapJobStatus(job.Status);
            var videoUrl = job.Status == JobStatus.Done && !string.IsNullOrEmpty(job.OutputPath)
                ? $"/api/videos/{id}/download"
                : null;

            var response = new VideoStatus(
                JobId: job.Id,
                Status: status,
                ProgressPercentage: job.Percent,
                CurrentStage: job.Stage,
                CreatedAt: job.CreatedUtc,
                CompletedAt: job.CompletedUtc,
                VideoUrl: videoUrl,
                ErrorMessage: job.ErrorMessage,
                ProcessingSteps: GetProcessingSteps(job),
                CorrelationId: correlationId
            );

            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[{CorrelationId}] Error getting video status for {Id}", correlationId, id);
            
            return StatusCode(
                StatusCodes.Status500InternalServerError,
                CreateProblemDetails(
                    "Error Retrieving Status",
                    "An error occurred while retrieving the video status",
                    StatusCodes.Status500InternalServerError,
                    correlationId));
        }
    }

    /// <summary>
    /// Stream real-time progress updates via Server-Sent Events
    /// </summary>
    /// <param name="id">Job ID</param>
    /// <param name="ct">Cancellation token</param>
    [HttpGet("{id}/stream")]
    [Produces("text/event-stream")]
    public async Task StreamProgress(string id, CancellationToken ct)
    {
        var correlationId = HttpContext.TraceIdentifier;
        
        try
        {
            _logger.LogInformation("[{CorrelationId}] GET /api/videos/{Id}/stream - SSE connection requested", correlationId, id);

            // Verify job exists
            var job = _jobRunner.GetJob(id);
            if (job == null)
            {
                Response.StatusCode = StatusCodes.Status404NotFound;
                await Response.WriteAsync($"data: {{\"error\":\"Job {id} not found\"}}\n\n", ct);
                return;
            }

            // Set SSE headers
            Response.Headers.Append("Content-Type", "text/event-stream");
            Response.Headers.Append("Cache-Control", "no-cache");
            Response.Headers.Append("Connection", "keep-alive");
            Response.Headers.Append("X-Accel-Buffering", "no"); // Disable nginx buffering

            // Send initial status
            await SendSseEvent("progress", new
            {
                percentage = (int)job.Percent,
                stage = job.Stage,
                message = $"Job {job.Status}",
                timestamp = DateTime.UtcNow
            }, ct);

            // Track heartbeat
            var lastHeartbeat = DateTime.UtcNow;
            var heartbeatInterval = TimeSpan.FromSeconds(30);

            // Poll for updates
            while (!ct.IsCancellationRequested && 
                   job.Status != JobStatus.Done && 
                   job.Status != JobStatus.Failed && 
                   job.Status != JobStatus.Canceled)
            {
                await Task.Delay(500, ct); // Poll every 500ms

                // Send heartbeat
                if (DateTime.UtcNow - lastHeartbeat >= heartbeatInterval)
                {
                    await Response.WriteAsync(": keepalive\n\n", ct);
                    await Response.Body.FlushAsync(ct);
                    lastHeartbeat = DateTime.UtcNow;
                }

                // Get updated job
                var updatedJob = _jobRunner.GetJob(id);
                if (updatedJob == null) break;

                // Send progress update if changed
                if (updatedJob.Percent != job.Percent || updatedJob.Stage != job.Stage)
                {
                    await SendSseEvent("progress", new
                    {
                        percentage = (int)updatedJob.Percent,
                        stage = updatedJob.Stage,
                        message = $"Processing: {updatedJob.Stage}",
                        timestamp = DateTime.UtcNow
                    }, ct);
                }

                // Send stage completion
                if (updatedJob.Stage != job.Stage)
                {
                    await SendSseEvent("stage-complete", new
                    {
                        stage = job.Stage,
                        nextStage = updatedJob.Stage,
                        timestamp = DateTime.UtcNow
                    }, ct);
                }

                job = updatedJob;
            }

            // Send final event
            if (job.Status == JobStatus.Done)
            {
                await SendSseEvent("done", new
                {
                    jobId = job.Id,
                    videoUrl = $"/api/videos/{id}/download",
                    timestamp = DateTime.UtcNow
                }, ct);
            }
            else if (job.Status == JobStatus.Failed)
            {
                await SendSseEvent("error", new
                {
                    message = job.ErrorMessage ?? "Job failed",
                    timestamp = DateTime.UtcNow
                }, ct);
            }

            _logger.LogInformation("[{CorrelationId}] SSE stream completed for job {Id}", correlationId, id);
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("[{CorrelationId}] SSE stream cancelled for job {Id}", correlationId, id);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[{CorrelationId}] Error in SSE stream for job {Id}", correlationId, id);
            
            try
            {
                await SendSseEvent("error", new
                {
                    message = "Stream error occurred",
                    timestamp = DateTime.UtcNow
                }, ct);
            }
            catch
            {
                // Ignore errors when trying to send error event
            }
        }
    }

    /// <summary>
    /// Download the generated video file
    /// </summary>
    /// <param name="id">Job ID</param>
    /// <returns>Video file stream</returns>
    [HttpGet("{id}/download")]
    [ProducesResponseType(typeof(FileStreamResult), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public IActionResult DownloadVideo(string id)
    {
        var correlationId = HttpContext.TraceIdentifier;
        
        try
        {
            _logger.LogInformation("[{CorrelationId}] GET /api/videos/{Id}/download", correlationId, id);

            var job = _jobRunner.GetJob(id);
            if (job == null)
            {
                return NotFound(CreateProblemDetails(
                    "Job Not Found",
                    $"Video generation job {id} was not found",
                    StatusCodes.Status404NotFound,
                    correlationId));
            }

            if (job.Status != JobStatus.Done)
            {
                return BadRequest(CreateProblemDetails(
                    "Video Not Ready",
                    $"Video is not ready for download. Current status: {job.Status}",
                    StatusCodes.Status400BadRequest,
                    correlationId));
            }

            if (string.IsNullOrEmpty(job.OutputPath) || !System.IO.File.Exists(job.OutputPath))
            {
                return NotFound(CreateProblemDetails(
                    "Video File Not Found",
                    "The video file was not found on disk. It may have been cleaned up.",
                    StatusCodes.Status404NotFound,
                    correlationId));
            }

            var fileStream = System.IO.File.OpenRead(job.OutputPath);
            var fileName = $"video-{id}.mp4";
            
            _logger.LogInformation("[{CorrelationId}] Serving video file: {Path}", correlationId, job.OutputPath);
            
            return File(fileStream, "video/mp4", fileName, enableRangeProcessing: true);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[{CorrelationId}] Error downloading video for {Id}", correlationId, id);
            
            return StatusCode(
                StatusCodes.Status500InternalServerError,
                CreateProblemDetails(
                    "Download Failed",
                    "An error occurred while downloading the video",
                    StatusCodes.Status500InternalServerError,
                    correlationId));
        }
    }

    /// <summary>
    /// Get metadata about the generated video
    /// </summary>
    /// <param name="id">Job ID</param>
    /// <returns>Video metadata</returns>
    [HttpGet("{id}/metadata")]
    [ProducesResponseType(typeof(VideoMetadata), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public IActionResult GetVideoMetadata(string id)
    {
        var correlationId = HttpContext.TraceIdentifier;
        
        try
        {
            _logger.LogInformation("[{CorrelationId}] GET /api/videos/{Id}/metadata", correlationId, id);

            var job = _jobRunner.GetJob(id);
            if (job == null)
            {
                return NotFound(CreateProblemDetails(
                    "Job Not Found",
                    $"Video generation job {id} was not found",
                    StatusCodes.Status404NotFound,
                    correlationId));
            }

            if (job.Status != JobStatus.Done || string.IsNullOrEmpty(job.OutputPath))
            {
                return BadRequest(CreateProblemDetails(
                    "Video Not Ready",
                    $"Video metadata is not available. Current status: {job.Status}",
                    StatusCodes.Status400BadRequest,
                    correlationId));
            }

            long fileSize = 0;
            if (System.IO.File.Exists(job.OutputPath))
            {
                fileSize = new System.IO.FileInfo(job.OutputPath).Length;
            }

            var metadata = new VideoMetadata(
                JobId: job.Id,
                OutputPath: job.OutputPath,
                FileSizeBytes: fileSize,
                CreatedAt: job.CreatedUtc,
                CompletedAt: job.CompletedUtc ?? DateTime.UtcNow,
                Duration: job.PlanSpec?.TargetDuration ?? TimeSpan.Zero,
                Resolution: job.RenderSpec != null 
                    ? $"{job.RenderSpec.Res.Width}x{job.RenderSpec.Res.Height}"
                    : "Unknown",
                Codec: job.RenderSpec?.Codec ?? "Unknown",
                Fps: job.RenderSpec?.Fps ?? 0,
                Artifacts: job.Artifacts.Select(a => new ArtifactInfo(
                    a.Name,
                    a.Path,
                    a.Type,
                    a.SizeBytes
                )).ToList(),
                CorrelationId: correlationId
            );

            return Ok(metadata);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[{CorrelationId}] Error getting video metadata for {Id}", correlationId, id);
            
            return StatusCode(
                StatusCodes.Status500InternalServerError,
                CreateProblemDetails(
                    "Error Retrieving Metadata",
                    "An error occurred while retrieving video metadata",
                    StatusCodes.Status500InternalServerError,
                    correlationId));
        }
    }

    private async Task SendSseEvent(string eventType, object data, CancellationToken ct)
    {
        var json = System.Text.Json.JsonSerializer.Serialize(data);
        await Response.WriteAsync($"event: {eventType}\n", ct);
        await Response.WriteAsync($"data: {json}\n\n", ct);
        await Response.Body.FlushAsync(ct);
    }

    private ProblemDetails CreateProblemDetails(
        string title,
        string detail,
        int status,
        string correlationId,
        string? invalidField = null)
    {
        var problemDetails = new ProblemDetails
        {
            Type = $"https://docs.aura.studio/errors/E{status}",
            Title = title,
            Status = status,
            Detail = detail,
            Instance = HttpContext.Request.Path
        };

        problemDetails.Extensions["correlationId"] = correlationId;
        problemDetails.Extensions["timestamp"] = DateTime.UtcNow;
        
        if (invalidField != null)
        {
            problemDetails.Extensions["field"] = invalidField;
        }

        return problemDetails;
    }

    private static string MapJobStatus(JobStatus status)
    {
        return status switch
        {
            JobStatus.Queued => "pending",
            JobStatus.Running => "processing",
            JobStatus.Done => "completed",
            JobStatus.Failed => "failed",
            JobStatus.Canceled => "failed",
            _ => "unknown"
        };
    }

    private static List<string> GetProcessingSteps(Job job)
    {
        var steps = new List<string>();
        
        if (job.Percent >= 0) steps.Add("Initialized");
        if (job.Percent >= 15) steps.Add("Script Generated");
        if (job.Percent >= 35) steps.Add("Audio Synthesized");
        if (job.Percent >= 65) steps.Add("Visuals Created");
        if (job.Percent >= 85) steps.Add("Video Composed");
        if (job.Status == JobStatus.Done) steps.Add("Completed");
        
        return steps;
    }

    private static Core.Models.Aspect ParseAspect(string? aspect)
    {
        return aspect?.ToLowerInvariant() switch
        {
            "16:9" or "widescreen" => Core.Models.Aspect.Widescreen16x9,
            "9:16" or "portrait" => Core.Models.Aspect.Vertical9x16,
            "1:1" or "square" => Core.Models.Aspect.Square1x1,
            _ => Core.Models.Aspect.Widescreen16x9
        };
    }

    private static Core.Models.Pacing ParsePacing(string? pacing)
    {
        return pacing?.ToLowerInvariant() switch
        {
            "slow" or "chill" => Core.Models.Pacing.Chill,
            "fast" => Core.Models.Pacing.Fast,
            _ => Core.Models.Pacing.Conversational
        };
    }

    private static Core.Models.Density ParseDensity(string? density)
    {
        return density?.ToLowerInvariant() switch
        {
            "sparse" => Core.Models.Density.Sparse,
            "dense" => Core.Models.Density.Dense,
            _ => Core.Models.Density.Balanced
        };
    }
}
