using System.Text.Json;
using Aura.Api.Models.ApiModels.V1;
using Aura.Core.Configuration;
using Aura.Core.Services;
using Aura.Core.Services.Providers;
using Microsoft.AspNetCore.Mvc;
using Serilog;

namespace Aura.Api.Controllers;

/// <summary>
/// Controller for Ollama process control and status
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class OllamaController : ControllerBase
{
    private readonly OllamaService _ollamaService;
    private readonly ProviderSettings _settings;
    private readonly HttpClient _httpClient;
    private readonly OllamaDetectionService? _detectionService;

    public OllamaController(
        OllamaService ollamaService,
        ProviderSettings settings,
        IHttpClientFactory httpClientFactory,
        OllamaDetectionService? detectionService = null)
    {
        _ollamaService = ollamaService;
        _settings = settings;
        _httpClient = httpClientFactory.CreateClient();
        _detectionService = detectionService;
    }

    /// <summary>
    /// Get Ollama service status (running, PID, model info)
    /// </summary>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Status information</returns>
    [HttpGet("status")]
    public async Task<ActionResult<OllamaStatusResponse>> GetStatus(CancellationToken ct = default)
    {
        try
        {
            var baseUrl = _settings.GetOllamaUrl();
            var status = await _ollamaService.GetStatusAsync(baseUrl, ct);

            var response = new OllamaStatusResponse(
                Running: status.Running,
                Pid: status.Pid,
                ManagedByApp: status.ManagedByApp,
                Model: status.Model,
                Error: status.Error
            );

            Log.Information("Ollama status check: Running={Running}, PID={Pid}, ManagedByApp={Managed}, CorrelationId={CorrelationId}",
                status.Running, status.Pid, status.ManagedByApp, HttpContext.TraceIdentifier);

            return Ok(response);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error checking Ollama status, CorrelationId={CorrelationId}", HttpContext.TraceIdentifier);
            return Problem(
                title: "Error checking Ollama status",
                detail: ex.Message,
                statusCode: 500,
                instance: HttpContext.TraceIdentifier
            );
        }
    }

    /// <summary>
    /// Start Ollama server process (Windows only)
    /// </summary>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Start operation result</returns>
    [HttpPost("start")]
    public async Task<ActionResult<OllamaStartResponse>> Start(CancellationToken ct = default)
    {
        try
        {
            var executablePath = _settings.GetOllamaExecutablePath();
            
            if (string.IsNullOrWhiteSpace(executablePath))
            {
                var autoDetectPath = OllamaService.FindOllamaExecutable();
                
                if (string.IsNullOrWhiteSpace(autoDetectPath))
                {
                    Log.Warning("Ollama executable path not configured and auto-detection failed, CorrelationId={CorrelationId}",
                        HttpContext.TraceIdentifier);
                    
                    return BadRequest(new ProblemDetails
                    {
                        Title = "Ollama path not configured",
                        Detail = "Please configure the Ollama executable path in Settings → Providers → Ollama",
                        Status = 400,
                        Instance = HttpContext.TraceIdentifier
                    });
                }
                
                executablePath = autoDetectPath;
            }

            var baseUrl = _settings.GetOllamaUrl();
            
            Log.Information("Starting Ollama from {Path}, CorrelationId={CorrelationId}", 
                executablePath, HttpContext.TraceIdentifier);

            var result = await _ollamaService.StartAsync(executablePath, baseUrl, ct);

            var response = new OllamaStartResponse(
                Success: result.Success,
                Message: result.Message,
                Pid: result.Pid
            );

            if (result.Success)
            {
                Log.Information("Ollama started successfully (PID: {Pid}), CorrelationId={CorrelationId}",
                    result.Pid, HttpContext.TraceIdentifier);
                return Ok(response);
            }
            else
            {
                Log.Warning("Failed to start Ollama: {Message}, CorrelationId={CorrelationId}",
                    result.Message, HttpContext.TraceIdentifier);
                return BadRequest(response);
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error starting Ollama, CorrelationId={CorrelationId}", HttpContext.TraceIdentifier);
            return Problem(
                title: "Error starting Ollama",
                detail: ex.Message,
                statusCode: 500,
                instance: HttpContext.TraceIdentifier
            );
        }
    }

    /// <summary>
    /// Stop Ollama server process (only if started by this app)
    /// </summary>
    /// <returns>Stop operation result</returns>
    [HttpPost("stop")]
    public ActionResult<OllamaStopResponse> Stop()
    {
        try
        {
            Log.Information("Stopping Ollama, CorrelationId={CorrelationId}", HttpContext.TraceIdentifier);

            var result = _ollamaService.StopAsync().Result;

            var response = new OllamaStopResponse(
                Success: result.Success,
                Message: result.Message
            );

            if (result.Success)
            {
                Log.Information("Ollama stopped successfully, CorrelationId={CorrelationId}", HttpContext.TraceIdentifier);
                return Ok(response);
            }
            else
            {
                Log.Warning("Failed to stop Ollama: {Message}, CorrelationId={CorrelationId}",
                    result.Message, HttpContext.TraceIdentifier);
                return BadRequest(response);
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error stopping Ollama, CorrelationId={CorrelationId}", HttpContext.TraceIdentifier);
            return Problem(
                title: "Error stopping Ollama",
                detail: ex.Message,
                statusCode: 500,
                instance: HttpContext.TraceIdentifier
            );
        }
    }

    /// <summary>
    /// Get recent Ollama log entries
    /// </summary>
    /// <param name="maxLines">Maximum number of log lines to return (default: 200)</param>
    /// <returns>Log entries</returns>
    [HttpGet("logs")]
    public async Task<ActionResult<OllamaLogsResponse>> GetLogs([FromQuery] int maxLines = 200)
    {
        try
        {
            var logs = await _ollamaService.GetLogsAsync(maxLines);

            var response = new OllamaLogsResponse(
                Logs: logs,
                TotalLines: logs.Length
            );

            Log.Information("Retrieved {Count} Ollama log lines, CorrelationId={CorrelationId}",
                logs.Length, HttpContext.TraceIdentifier);

            return Ok(response);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error retrieving Ollama logs, CorrelationId={CorrelationId}", HttpContext.TraceIdentifier);
            return Problem(
                title: "Error retrieving logs",
                detail: ex.Message,
                statusCode: 500,
                instance: HttpContext.TraceIdentifier
            );
        }
    }

    /// <summary>
    /// List available Ollama models
    /// </summary>
    /// <param name="ct">Cancellation token</param>
    /// <returns>List of installed models</returns>
    [HttpGet("models")]
    public async Task<ActionResult<OllamaModelsListResponse>> GetModels(CancellationToken ct = default)
    {
        try
        {
            var baseUrl = _settings.GetOllamaUrl();

            using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            cts.CancelAfter(TimeSpan.FromSeconds(10));

            var response = await _httpClient.GetAsync($"{baseUrl}/api/tags", cts.Token);

            if (!response.IsSuccessStatusCode)
            {
                Log.Warning("Ollama models endpoint returned {StatusCode}, CorrelationId={CorrelationId}",
                    response.StatusCode, HttpContext.TraceIdentifier);
                
                return Problem(
                    title: "Failed to retrieve models",
                    detail: $"Ollama API returned {response.StatusCode}",
                    statusCode: (int)response.StatusCode,
                    instance: HttpContext.TraceIdentifier
                );
            }

            var json = await response.Content.ReadAsStringAsync(ct);
            var doc = JsonDocument.Parse(json);

            var models = new List<OllamaModelDto>();

            if (doc.RootElement.TryGetProperty("models", out var modelsArray))
            {
                foreach (var model in modelsArray.EnumerateArray())
                {
                    var name = model.TryGetProperty("name", out var nameEl) ? nameEl.GetString() : null;
                    var size = model.TryGetProperty("size", out var sizeEl) ? FormatSize(sizeEl.GetInt64()) : null;
                    var modifiedAt = model.TryGetProperty("modified_at", out var modEl) ? modEl.GetString() : null;

                    if (!string.IsNullOrEmpty(name))
                    {
                        models.Add(new OllamaModelDto(name, size, modifiedAt));
                    }
                }
            }

            Log.Information("Retrieved {Count} Ollama models, CorrelationId={CorrelationId}",
                models.Count, HttpContext.TraceIdentifier);

            return Ok(new OllamaModelsListResponse(models, models.Count));
        }
        catch (OperationCanceledException)
        {
            Log.Warning("Ollama models request timed out, CorrelationId={CorrelationId}", HttpContext.TraceIdentifier);
            return Problem(
                title: "Request timeout",
                detail: "Failed to retrieve models from Ollama (timeout)",
                statusCode: 504,
                instance: HttpContext.TraceIdentifier
            );
        }
        catch (HttpRequestException ex)
        {
            Log.Warning(ex, "Cannot connect to Ollama for models list, CorrelationId={CorrelationId}",
                HttpContext.TraceIdentifier);
            
            return Problem(
                title: "Cannot connect to Ollama",
                detail: "Please ensure Ollama is running",
                statusCode: 503,
                instance: HttpContext.TraceIdentifier
            );
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error retrieving Ollama models, CorrelationId={CorrelationId}", HttpContext.TraceIdentifier);
            return Problem(
                title: "Error retrieving models",
                detail: ex.Message,
                statusCode: 500,
                instance: HttpContext.TraceIdentifier
            );
        }
    }

    /// <summary>
    /// Get detailed information about a specific model
    /// </summary>
    [HttpGet("models/{modelName}/info")]
    public async Task<IActionResult> GetModelInfo(string modelName, CancellationToken ct)
    {
        if (_detectionService == null)
        {
            return Problem("Ollama detection service not available", statusCode: 503);
        }

        try
        {
            var info = await _detectionService.GetModelInfoAsync(modelName, ct);
            
            if (info == null)
            {
                return NotFound(new { message = $"Model '{modelName}' not found or info unavailable" });
            }

            return Ok(new OllamaModelInfoDto
            {
                Name = info.Name,
                Parameters = info.Parameters,
                Modelfile = info.Modelfile,
                ContextWindow = info.ContextWindow
            });
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error getting model info for {ModelName}, CorrelationId={CorrelationId}", 
                modelName, HttpContext.TraceIdentifier);
            return Problem($"Error getting model info for {modelName}", statusCode: 500);
        }
    }

    /// <summary>
    /// Check if a specific model is available locally
    /// </summary>
    [HttpGet("models/{modelName}/available")]
    public async Task<IActionResult> CheckModelAvailability(string modelName, CancellationToken ct)
    {
        if (_detectionService == null)
        {
            return Problem("Ollama detection service not available", statusCode: 503);
        }

        try
        {
            var isAvailable = await _detectionService.IsModelAvailableAsync(modelName, ct);
            
            return Ok(new { modelName, isAvailable });
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error checking model availability for {ModelName}, CorrelationId={CorrelationId}", 
                modelName, HttpContext.TraceIdentifier);
            return Problem($"Error checking model availability for {modelName}", statusCode: 500);
        }
    }

    /// <summary>
    /// Pull a model from the Ollama library (initiate async download)
    /// </summary>
    [HttpPost("models/{modelName}/pull")]
    public async Task<IActionResult> PullModel(string modelName, CancellationToken ct)
    {
        if (_detectionService == null)
        {
            return Problem("Ollama detection service not available", statusCode: 503);
        }

        try
        {
            Log.Information("Initiating pull for Ollama model: {ModelName}, CorrelationId={CorrelationId}", 
                modelName, HttpContext.TraceIdentifier);
            
            var pullProgress = new Progress<OllamaPullProgress>(p =>
            {
                Log.Debug("Pull progress for {ModelName}: {Status} - {Percent:F1}%",
                    modelName, p.Status, p.PercentComplete);
            });

            var success = await _detectionService.PullModelAsync(modelName, pullProgress, ct);

            if (success)
            {
                return Ok(new { 
                    message = $"Model '{modelName}' pulled successfully",
                    modelName,
                    success = true
                });
            }

            return BadRequest(new { 
                message = $"Failed to pull model '{modelName}'",
                modelName,
                success = false
            });
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error pulling Ollama model: {ModelName}, CorrelationId={CorrelationId}", 
                modelName, HttpContext.TraceIdentifier);
            return Problem($"Error pulling model {modelName}", statusCode: 500);
        }
    }

    private static string FormatSize(long bytes)
    {
        string[] sizes = { "B", "KB", "MB", "GB", "TB" };
        double len = bytes;
        int order = 0;
        
        while (len >= 1024 && order < sizes.Length - 1)
        {
            order++;
            len = len / 1024;
        }
        
        return $"{len:0.##} {sizes[order]}";
    }
}
