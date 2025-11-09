using Aura.Core.Orchestrator;
using Aura.Core.Services.FFmpeg;
using Microsoft.AspNetCore.Mvc;
using Serilog;

namespace Aura.Api.Controllers;

/// <summary>
/// Controller for one-click safe video generation.
/// Provides guaranteed-success path for new users without configuration.
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class QuickController : ControllerBase
{
    private readonly QuickService _quickService;
    private readonly IFFmpegStatusService _ffmpegStatusService;

    public QuickController(QuickService quickService, IFFmpegStatusService ffmpegStatusService)
    {
        _quickService = quickService;
        _ffmpegStatusService = ffmpegStatusService;
    }

    /// <summary>
    /// Create a quick demo video with safe defaults.
    /// Forces Free-only providers: RuleBased LLM + Windows TTS + Stock visuals.
    /// Locks render to 1080p30 H.264 for maximum compatibility.
    /// Generates a 10-15 second video with captions.
    /// </summary>
    /// <param name="request">Optional topic override</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Job ID and status</returns>
    [HttpPost("demo")]
    public async Task<IActionResult> CreateQuickDemo(
        [FromBody] QuickDemoRequest? request = null,
        CancellationToken ct = default)
    {
        var correlationId = HttpContext.TraceIdentifier;
        
        try
        {
            Log.Information("[{CorrelationId}] POST /api/quick/demo endpoint called", correlationId);
            Log.Information("[{CorrelationId}] Quick Demo requested with topic: {Topic}", correlationId, request?.Topic ?? "(default)");

            // Check FFmpeg readiness before starting demo
            var ffmpegStatus = await _ffmpegStatusService.GetStatusAsync(ct);
            
            if (!ffmpegStatus.Installed || 
                !ffmpegStatus.Valid || 
                ffmpegStatus.Version == null || 
                !ffmpegStatus.VersionMeetsRequirement)
            {
                Log.Warning("[{CorrelationId}] FFmpeg not ready. Installed={Installed}, Valid={Valid}, Version={Version}, VersionMeets={VersionMeets}",
                    correlationId, ffmpegStatus.Installed, ffmpegStatus.Valid, ffmpegStatus.Version, ffmpegStatus.VersionMeetsRequirement);
                
                return Conflict(new Microsoft.AspNetCore.Mvc.ProblemDetails
                {
                    Type = "https://docs.aura.studio/errors/E302-FFMPEG_NOT_READY",
                    Title = "FFmpeg Not Ready",
                    Status = 409,
                    Detail = "FFmpeg is not properly installed or configured. Quick Demo requires a working FFmpeg installation to render videos.",
                    Extensions =
                    {
                        ["correlationId"] = correlationId,
                        ["ffmpegStatus"] = new
                        {
                            installed = ffmpegStatus.Installed,
                            valid = ffmpegStatus.Valid,
                            version = ffmpegStatus.Version,
                            versionMeetsRequirement = ffmpegStatus.VersionMeetsRequirement,
                            minimumVersion = ffmpegStatus.MinimumVersion,
                            path = ffmpegStatus.Path,
                            source = ffmpegStatus.Source,
                            error = ffmpegStatus.Error
                        },
                        ["suggestedActions"] = new[]
                        {
                            "Install Managed FFmpeg from the Downloads page",
                            "Verify FFmpeg is in your system PATH",
                            "Check FFmpeg status on the Dependencies page",
                            "Ensure FFmpeg version is 4.0 or higher"
                        }
                    }
                });
            }
            
            Log.Information("[{CorrelationId}] FFmpeg ready: Version={Version}, Source={Source}", 
                correlationId, ffmpegStatus.Version, ffmpegStatus.Source);

            var result = await _quickService.CreateQuickDemoAsync(request?.Topic, ct);

            if (result.Success)
            {
                Log.Information("[{CorrelationId}] Quick Demo job created successfully: {JobId}", correlationId, result.JobId);
                return Ok(new
                {
                    jobId = result.JobId,
                    status = "queued",
                    message = result.Message,
                    correlationId
                });
            }
            else
            {
                Log.Warning("[{CorrelationId}] Quick Demo creation failed: {Message}", correlationId, result.Message);
                return StatusCode(500, new
                {
                    type = "https://docs.aura.studio/errors/E200",
                    title = "Quick Demo Failed",
                    status = 500,
                    detail = result.Message,
                    correlationId,
                    guidance = "Quick Demo uses safe defaults and should always work. This error indicates a system issue. Ensure FFmpeg is installed and all required services are running. Check logs for details."
                });
            }
        }
        catch (ArgumentNullException ex)
        {
            Log.Error(ex, "[{CorrelationId}] Null argument error in quick demo", correlationId);
            
            return StatusCode(500, new
            {
                type = "https://docs.aura.studio/errors/E500",
                title = "Internal Service Error",
                status = 500,
                detail = $"Required service not initialized: {ex.ParamName}",
                correlationId,
                guidance = "This is an internal configuration error. Please restart the service or contact support."
            });
        }
        catch (Exception ex)
        {
            Log.Error(ex, "[{CorrelationId}] Error creating quick demo", correlationId);
            
            return StatusCode(500, new
            {
                type = "https://docs.aura.studio/errors/E200",
                title = "Quick Demo Error",
                status = 500,
                detail = $"Unexpected error creating quick demo: {ex.Message}",
                correlationId,
                guidance = "Quick Demo should never fail. This indicates a critical system issue. Verify: 1) FFmpeg is installed and accessible, 2) Sufficient disk space available, 3) All required dependencies are installed. Check logs for stack trace.",
                exceptionType = ex.GetType().Name
            });
        }
    }
}

/// <summary>
/// Request model for quick demo creation
/// </summary>
public record QuickDemoRequest(string? Topic);
