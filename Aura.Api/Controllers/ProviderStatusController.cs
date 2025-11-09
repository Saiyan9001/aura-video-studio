using System;
using System.Threading;
using System.Threading.Tasks;
using Aura.Core.Services.Providers;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace Aura.Api.Controllers;

/// <summary>
/// Controller for provider status and offline mode detection
/// </summary>
[ApiController]
[Route("api/provider-status")]
public class ProviderStatusController : ControllerBase
{
    private readonly ILogger<ProviderStatusController> _logger;
    private readonly ProviderStatusService _statusService;

    public ProviderStatusController(
        ILogger<ProviderStatusController> logger,
        ProviderStatusService statusService)
    {
        _logger = logger;
        _statusService = statusService;
    }

    /// <summary>
    /// Get comprehensive status of all providers
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<SystemProviderStatusDto>> GetStatus(CancellationToken ct)
    {
        try
        {
            _logger.LogInformation("Getting provider status, CorrelationId: {CorrelationId}",
                HttpContext.TraceIdentifier);

            var status = await _statusService.GetAllProviderStatusAsync(ct);

            var dto = new SystemProviderStatusDto
            {
                IsOfflineMode = status.IsOfflineMode,
                Providers = status.Providers.ConvertAll(p => new DetailedProviderStatusDto
                {
                    Name = p.Name,
                    Category = p.Category,
                    IsAvailable = p.IsAvailable,
                    IsOnline = p.IsOnline,
                    Tier = p.Tier,
                    Features = p.Features,
                    Message = p.Message
                }),
                OnlineProvidersCount = status.OnlineProvidersCount,
                OfflineProvidersCount = status.OfflineProvidersCount,
                AvailableFeatures = status.AvailableFeatures,
                DegradedFeatures = status.DegradedFeatures,
                LastUpdated = status.LastUpdated,
                Message = status.Message
            };

            return Ok(dto);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting provider status, CorrelationId: {CorrelationId}",
                HttpContext.TraceIdentifier);
            return Problem("Failed to get provider status", statusCode: 500);
        }
    }

    /// <summary>
    /// Check if system is in offline mode
    /// </summary>
    [HttpGet("offline-mode")]
    public async Task<ActionResult<OfflineModeDto>> GetOfflineMode(CancellationToken ct)
    {
        try
        {
            var isOffline = await _statusService.IsOfflineModeAsync(ct);
            var availableFeatures = await _statusService.GetAvailableFeaturesAsync(ct);
            var degradedFeatures = await _statusService.GetDegradedFeaturesAsync(ct);

            return Ok(new OfflineModeDto
            {
                IsOfflineMode = isOffline,
                AvailableFeatures = availableFeatures,
                DegradedFeatures = degradedFeatures,
                Message = isOffline
                    ? "Running in offline mode - using local providers and templates"
                    : "Online providers available"
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking offline mode, CorrelationId: {CorrelationId}",
                HttpContext.TraceIdentifier);
            return Problem("Failed to check offline mode", statusCode: 500);
        }
    }

    /// <summary>
    /// Refresh provider status cache
    /// </summary>
    [HttpPost("refresh")]
    public async Task<IActionResult> RefreshStatus(CancellationToken ct)
    {
        try
        {
            _logger.LogInformation("Refreshing provider status, CorrelationId: {CorrelationId}",
                HttpContext.TraceIdentifier);

            await _statusService.RefreshStatusAsync(ct);

            return Ok(new { message = "Provider status refreshed successfully" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error refreshing provider status, CorrelationId: {CorrelationId}",
                HttpContext.TraceIdentifier);
            return Problem("Failed to refresh provider status", statusCode: 500);
        }
    }

    /// <summary>
    /// Get available features in current configuration
    /// </summary>
    [HttpGet("features")]
    public async Task<ActionResult<FeaturesDto>> GetFeatures(CancellationToken ct)
    {
        try
        {
            var available = await _statusService.GetAvailableFeaturesAsync(ct);
            var degraded = await _statusService.GetDegradedFeaturesAsync(ct);

            return Ok(new FeaturesDto
            {
                Available = available,
                Degraded = degraded
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting features, CorrelationId: {CorrelationId}",
                HttpContext.TraceIdentifier);
            return Problem("Failed to get features", statusCode: 500);
        }
    }
}

/// <summary>
/// System provider status DTO
/// </summary>
public class SystemProviderStatusDto
{
    public bool IsOfflineMode { get; set; }
    public List<DetailedProviderStatusDto> Providers { get; set; } = new();
    public int OnlineProvidersCount { get; set; }
    public int OfflineProvidersCount { get; set; }
    public List<string> AvailableFeatures { get; set; } = new();
    public List<string> DegradedFeatures { get; set; } = new();
    public DateTime LastUpdated { get; set; }
    public string Message { get; set; } = string.Empty;
}

/// <summary>
/// Provider status DTO
/// </summary>
public class DetailedProviderStatusDto
{
    public string Name { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public bool IsAvailable { get; set; }
    public bool IsOnline { get; set; }
    public string Tier { get; set; } = string.Empty;
    public List<string> Features { get; set; } = new();
    public string Message { get; set; } = string.Empty;
}

/// <summary>
/// Offline mode DTO
/// </summary>
public class OfflineModeDto
{
    public bool IsOfflineMode { get; set; }
    public List<string> AvailableFeatures { get; set; } = new();
    public List<string> DegradedFeatures { get; set; } = new();
    public string Message { get; set; } = string.Empty;
}

/// <summary>
/// Features DTO
/// </summary>
public class FeaturesDto
{
    public List<string> Available { get; set; } = new();
    public List<string> Degraded { get; set; } = new();
}
