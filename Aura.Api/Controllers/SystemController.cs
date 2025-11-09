using System;
using System.Threading;
using System.Threading.Tasks;
using Aura.Core.Services.FFmpeg;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace Aura.Api.Controllers;

/// <summary>
/// System health and status information
/// </summary>
[ApiController]
[Route("api/system")]
public class SystemController : ControllerBase
{
    private readonly ILogger<SystemController> _logger;
    private readonly IFFmpegStatusService _ffmpegStatusService;

    public SystemController(
        ILogger<SystemController> logger,
        IFFmpegStatusService ffmpegStatusService)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _ffmpegStatusService = ffmpegStatusService ?? throw new ArgumentNullException(nameof(ffmpegStatusService));
    }

    /// <summary>
    /// Get comprehensive FFmpeg status including version and hardware acceleration support
    /// </summary>
    /// <remarks>
    /// Returns detailed information about FFmpeg installation:
    /// - Installation status and location
    /// - Version information and requirement compliance
    /// - Hardware acceleration support (NVENC, AMF, QuickSync, VideoToolbox)
    /// - Available hardware encoders
    /// </remarks>
    [HttpGet("ffmpeg/status")]
    public async Task<IActionResult> GetFFmpegStatus(CancellationToken ct)
    {
        var correlationId = HttpContext.TraceIdentifier;

        try
        {
            _logger.LogInformation("[{CorrelationId}] GET /api/system/ffmpeg/status", correlationId);

            var status = await _ffmpegStatusService.GetStatusAsync(ct);

            return Ok(new
            {
                installed = status.Installed,
                valid = status.Valid,
                version = status.Version,
                path = status.Path,
                source = status.Source,
                error = status.Error,
                versionMeetsRequirement = status.VersionMeetsRequirement,
                minimumVersion = status.MinimumVersion,
                hardwareAcceleration = new
                {
                    nvencSupported = status.HardwareAcceleration.NvencSupported,
                    amfSupported = status.HardwareAcceleration.AmfSupported,
                    quickSyncSupported = status.HardwareAcceleration.QuickSyncSupported,
                    videoToolboxSupported = status.HardwareAcceleration.VideoToolboxSupported,
                    availableEncoders = status.HardwareAcceleration.AvailableEncoders
                },
                correlationId
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[{CorrelationId}] Error getting FFmpeg status", correlationId);

            return StatusCode(500, new
            {
                type = "https://docs.aura.studio/errors/E500",
                title = "FFmpeg Status Error",
                status = 500,
                detail = $"Failed to get FFmpeg status: {ex.Message}",
                correlationId
            });
        }
    }
}
