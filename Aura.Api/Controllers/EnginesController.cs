using Aura.Core.Downloads;
using Aura.Core.Runtime;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

namespace Aura.Api.Controllers;

[ApiController]
[Route("api/engines")]
public class EnginesController : ControllerBase
{
    private readonly ILogger<EnginesController> _logger;
    private readonly EngineManifestLoader _manifestLoader;
    private readonly EngineInstaller _installer;
    private readonly LocalEnginesRegistry _registry;
    private readonly ExternalProcessManager _processManager;
    private readonly EngineLifecycleManager _lifecycleManager;
    private readonly EngineDetector? _engineDetector;

    public EnginesController(
        ILogger<EnginesController> logger,
        EngineManifestLoader manifestLoader,
        EngineInstaller installer,
        LocalEnginesRegistry registry,
        ExternalProcessManager processManager,
        EngineLifecycleManager lifecycleManager,
        EngineDetector? engineDetector = null)
    {
        _logger = logger;
        _manifestLoader = manifestLoader;
        _installer = installer;
        _registry = registry;
        _processManager = processManager;
        _lifecycleManager = lifecycleManager;
        _engineDetector = engineDetector;
    }

    /// <summary>
    /// Get list of available engines from manifest
    /// </summary>
    [HttpGet("list")]
    public async Task<IActionResult> GetList()
    {
        try
        {
            var manifest = await _manifestLoader.LoadManifestAsync();
            
            // Detect GPU for gating info
            var hardwareDetector = new Aura.Core.Hardware.HardwareDetector(
                Microsoft.Extensions.Logging.Abstractions.NullLogger<Aura.Core.Hardware.HardwareDetector>.Instance);
            var systemProfile = await hardwareDetector.DetectSystemAsync();
            var gpuInfo = systemProfile.Gpu;
            bool hasNvidia = gpuInfo?.Vendor?.ToUpperInvariant() == "NVIDIA";
            bool hasEnoughVram = hasNvidia && (gpuInfo?.VramGB ?? 0) >= 6;
            
            var engines = manifest.Engines.Select(e => 
            {
                bool requiresNvidia = e.RequiredVRAMGB > 0;
                bool meetsRequirements = !requiresNvidia || (hasNvidia && (gpuInfo?.VramGB ?? 0) >= (e.RequiredVRAMGB ?? 0));
                
                string? gatingReason = null;
                if (requiresNvidia && !hasNvidia)
                {
                    gatingReason = "Requires NVIDIA GPU";
                }
                else if (requiresNvidia && hasNvidia && !meetsRequirements)
                {
                    gatingReason = $"Requires {e.RequiredVRAMGB}GB VRAM (detected: {gpuInfo?.VramGB ?? 0}GB)";
                }
                
                return new
                {
                    e.Id,
                    e.Name,
                    e.Version,
                    e.Description,
                    e.SizeBytes,
                    e.DefaultPort,
                    e.LicenseUrl,
                    e.RequiredVRAMGB,
                    IsInstalled = _installer.IsInstalled(e.Id),
                    InstallPath = _installer.GetInstallPath(e.Id),
                    // Gating information - now allows install anyway
                    IsGated = requiresNvidia,
                    CanInstall = true, // Always allow installation for future use
                    CanAutoStart = meetsRequirements, // Only auto-start if requirements are met
                    GatingReason = gatingReason,
                    VramTooltip = e.VramTooltip,
                    Icon = e.Icon,
                    Tags = e.Tags
                };
            }).ToList();

            return Ok(new { engines, hardwareInfo = new { hasNvidia, vramGB = gpuInfo?.VramGB ?? 0 } });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get engine list");
            return StatusCode(500, new { error = ex.Message });
        }
    }

    /// <summary>
    /// Get status of a specific engine
    /// </summary>
    [HttpGet("status")]
    public async Task<IActionResult> GetStatus([FromQuery] string engineId)
    {
        try
        {
            if (string.IsNullOrEmpty(engineId))
            {
                return BadRequest(new { error = "engineId is required" });
            }

            var manifest = await _manifestLoader.LoadManifestAsync();
            var engine = manifest.Engines.FirstOrDefault(e => e.Id == engineId);
            
            if (engine == null)
            {
                return NotFound(new { error = $"Engine {engineId} not found in manifest" });
            }

            bool isInstalled = _installer.IsInstalled(engineId);
            string? installedVersion = null;
            bool isRunning = false;
            int? port = null;
            bool isHealthy = false;
            var messages = new List<string>();
            int? processId = null;
            string? logsPath = null;

            var engineConfig = _registry.GetEngine(engineId);
            if (engineConfig != null)
            {
                installedVersion = engineConfig.Version;
                port = engineConfig.Port;
                
                var status = _processManager.GetStatus(engineId);
                isRunning = status.IsRunning;
                processId = status.ProcessId;
                isHealthy = status.HealthCheckPassed;

                if (isRunning)
                {
                    logsPath = _processManager.GetLogPath(engineId);
                }

                if (!string.IsNullOrEmpty(status.LastError))
                {
                    messages.Add(status.LastError);
                }
            }

            if (isInstalled && engineConfig == null)
            {
                messages.Add("Engine is installed but not registered. Please restart the application.");
            }

            return Ok(new
            {
                engineId,
                name = engine.Name,
                status = isRunning ? "running" : (isInstalled ? "installed" : "not_installed"),
                installedVersion,
                isRunning,
                port,
                health = isRunning ? (isHealthy ? "healthy" : "unreachable") : null,
                processId,
                logsPath,
                messages
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get engine status for {EngineId}", engineId);
            return StatusCode(500, new { error = ex.Message });
        }
    }

    /// <summary>
    /// Install an engine
    /// </summary>
    [HttpPost("install")]
    public async Task<IActionResult> Install([FromBody] InstallRequest request, CancellationToken ct)
    {
        try
        {
            if (string.IsNullOrEmpty(request.EngineId))
            {
                return BadRequest(new { error = "engineId is required" });
            }

            var manifest = await _manifestLoader.LoadManifestAsync();
            var engine = manifest.Engines.FirstOrDefault(e => e.Id == request.EngineId);
            
            if (engine == null)
            {
                return NotFound(new { error = $"Engine {request.EngineId} not found in manifest" });
            }

            if (_installer.IsInstalled(request.EngineId))
            {
                return BadRequest(new { error = $"Engine {request.EngineId} is already installed" });
            }

            // Install the engine
            string installPath = await _installer.InstallAsync(engine, null, ct, request.CustomUrl, request.LocalFilePath);

            // Register with the registry
            string executablePath = System.IO.Path.Combine(installPath, engine.Entrypoint);
            
            var engineConfig = new EngineConfig(
                engine.Id,
                engine.Name,
                request.Version ?? engine.Version,
                installPath,
                executablePath,
                engine.ArgsTemplate,
                request.Port ?? engine.DefaultPort,
                engine.HealthCheck != null ? $"http://localhost:{request.Port ?? engine.DefaultPort}{engine.HealthCheck.Url}" : null,
                StartOnAppLaunch: false,
                AutoRestart: false
            );

            await _registry.RegisterEngineAsync(engineConfig);

            return Ok(new
            {
                success = true,
                engineId = request.EngineId,
                installPath,
                message = $"Engine {engine.Name} installed successfully"
            });
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("Install operation cancelled for {EngineId}", request.EngineId);
            return StatusCode(499, new { error = "Installation cancelled by user" });
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "Network error while installing {EngineId}", request.EngineId);
            return StatusCode(500, new { 
                error = "Network error during download. Check your internet connection and try again.",
                details = ex.Message 
            });
        }
        catch (UnauthorizedAccessException ex)
        {
            _logger.LogError(ex, "Permission error while installing {EngineId}", request.EngineId);
            return StatusCode(500, new { 
                error = "Permission denied. Cannot write to installation directory. Check folder permissions.",
                details = ex.Message 
            });
        }
        catch (IOException ex)
        {
            _logger.LogError(ex, "File system error while installing {EngineId}", request.EngineId);
            string errorMsg = ex.Message.Contains("not enough space") || ex.Message.Contains("disk full")
                ? "Not enough disk space. Free up space and try again."
                : "File system error during installation.";
            return StatusCode(500, new { 
                error = errorMsg,
                details = ex.Message 
            });
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("checksum") || ex.Message.Contains("verification"))
        {
            _logger.LogError(ex, "Checksum verification failed for {EngineId}", request.EngineId);
            return StatusCode(500, new { 
                error = "Download verification failed. The file may be corrupted. Please try again.",
                details = ex.Message 
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to install engine {EngineId}", request.EngineId);
            return StatusCode(500, new { 
                error = $"Installation failed: {ex.Message}",
                details = ex.ToString()
            });
        }
    }

    /// <summary>
    /// Verify an engine installation
    /// </summary>
    [HttpPost("verify")]
    public async Task<IActionResult> Verify([FromBody] EngineActionRequest request)
    {
        try
        {
            if (string.IsNullOrEmpty(request.EngineId))
            {
                return BadRequest(new { error = "engineId is required" });
            }

            var manifest = await _manifestLoader.LoadManifestAsync();
            var engine = manifest.Engines.FirstOrDefault(e => e.Id == request.EngineId);
            
            if (engine == null)
            {
                return NotFound(new { error = $"Engine {request.EngineId} not found in manifest" });
            }

            var result = await _installer.VerifyAsync(engine);

            return Ok(new
            {
                engineId = result.EngineId,
                isValid = result.IsValid,
                status = result.Status,
                missingFiles = result.MissingFiles,
                issues = result.Issues
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to verify engine {EngineId}", request.EngineId);
            return StatusCode(500, new { error = ex.Message });
        }
    }

    /// <summary>
    /// Repair an engine installation
    /// </summary>
    [HttpPost("repair")]
    public async Task<IActionResult> Repair([FromBody] EngineActionRequest request, CancellationToken ct)
    {
        try
        {
            if (string.IsNullOrEmpty(request.EngineId))
            {
                return BadRequest(new { error = "engineId is required" });
            }

            var manifest = await _manifestLoader.LoadManifestAsync();
            var engine = manifest.Engines.FirstOrDefault(e => e.Id == request.EngineId);
            
            if (engine == null)
            {
                return NotFound(new { error = $"Engine {request.EngineId} not found in manifest" });
            }

            string installPath = await _installer.RepairAsync(engine, null, ct);

            return Ok(new
            {
                success = true,
                engineId = request.EngineId,
                installPath,
                message = $"Engine {engine.Name} repaired successfully"
            });
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("Repair operation cancelled for {EngineId}", request.EngineId);
            return StatusCode(499, new { error = "Repair cancelled by user" });
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "Network error while repairing {EngineId}", request.EngineId);
            return StatusCode(500, new { 
                error = "Network error during repair. Check your internet connection and try again.",
                details = ex.Message 
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to repair engine {EngineId}", request.EngineId);
            return StatusCode(500, new { 
                error = $"Repair failed: {ex.Message}",
                details = ex.ToString()
            });
        }
    }

    /// <summary>
    /// Remove an engine installation
    /// </summary>
    [HttpPost("remove")]
    public async Task<IActionResult> Remove([FromBody] EngineActionRequest request)
    {
        try
        {
            if (string.IsNullOrEmpty(request.EngineId))
            {
                return BadRequest(new { error = "engineId is required" });
            }

            // Stop if running
            await _registry.StopEngineAsync(request.EngineId);

            // Unregister
            await _registry.UnregisterEngineAsync(request.EngineId);

            // Remove files
            var manifest = await _manifestLoader.LoadManifestAsync();
            var engine = manifest.Engines.FirstOrDefault(e => e.Id == request.EngineId);
            
            if (engine != null)
            {
                await _installer.RemoveAsync(engine);
            }

            return Ok(new
            {
                success = true,
                engineId = request.EngineId,
                message = $"Engine removed successfully"
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to remove engine {EngineId}", request.EngineId);
            return StatusCode(500, new { error = ex.Message });
        }
    }

    /// <summary>
    /// Start an engine
    /// </summary>
    [HttpPost("start")]
    public async Task<IActionResult> Start([FromBody] StartRequest request, CancellationToken ct)
    {
        try
        {
            if (string.IsNullOrEmpty(request.EngineId))
            {
                return BadRequest(new { error = "engineId is required" });
            }

            var engineConfig = _registry.GetEngine(request.EngineId);
            if (engineConfig == null)
            {
                return NotFound(new { error = $"Engine {request.EngineId} not registered" });
            }

            // Update port if provided
            if (request.Port.HasValue && request.Port != engineConfig.Port)
            {
                engineConfig = engineConfig with { Port = request.Port };
                await _registry.RegisterEngineAsync(engineConfig);
            }

            // Update args if provided
            if (!string.IsNullOrEmpty(request.Args) && request.Args != engineConfig.Arguments)
            {
                engineConfig = engineConfig with { Arguments = request.Args };
                await _registry.RegisterEngineAsync(engineConfig);
            }

            bool started = await _registry.StartEngineAsync(request.EngineId, ct);

            if (started)
            {
                var status = _processManager.GetStatus(request.EngineId);
                return Ok(new
                {
                    success = true,
                    engineId = request.EngineId,
                    processId = status.ProcessId,
                    port = engineConfig.Port,
                    logsPath = _processManager.GetLogPath(request.EngineId),
                    message = $"Engine {engineConfig.Name} started successfully"
                });
            }
            else
            {
                return StatusCode(500, new { error = "Failed to start engine" });
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to start engine {EngineId}", request.EngineId);
            return StatusCode(500, new { error = ex.Message });
        }
    }

    /// <summary>
    /// Stop an engine
    /// </summary>
    [HttpPost("stop")]
    public async Task<IActionResult> Stop([FromBody] EngineActionRequest request)
    {
        try
        {
            if (string.IsNullOrEmpty(request.EngineId))
            {
                return BadRequest(new { error = "engineId is required" });
            }

            bool stopped = await _registry.StopEngineAsync(request.EngineId);

            if (stopped)
            {
                return Ok(new
                {
                    success = true,
                    engineId = request.EngineId,
                    message = "Engine stopped successfully"
                });
            }
            else
            {
                return NotFound(new { error = $"Engine {request.EngineId} is not running" });
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to stop engine {EngineId}", request.EngineId);
            return StatusCode(500, new { error = ex.Message });
        }
    }

    /// <summary>
    /// Save engine preferences (port, auto-start, etc.)
    /// </summary>
    [HttpPost("preferences")]
    public async Task<IActionResult> SavePreferences([FromBody] Dictionary<string, EnginePreferences> preferences)
    {
        try
        {
            foreach (var (engineId, prefs) in preferences)
            {
                var engineConfig = _registry.GetEngine(engineId);
                if (engineConfig != null)
                {
                    var updatedConfig = engineConfig with
                    {
                        Port = prefs.Port ?? engineConfig.Port,
                        StartOnAppLaunch = prefs.AutoStart
                    };
                    await _registry.RegisterEngineAsync(updatedConfig);
                }
            }

            return Ok(new { success = true, message = "Preferences saved successfully" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save engine preferences");
            return StatusCode(500, new { error = ex.Message });
        }
    }

    /// <summary>
    /// Get engine preferences
    /// </summary>
    [HttpGet("preferences")]
    public IActionResult GetPreferences()
    {
        try
        {
            var engines = _registry.GetAllEngines();
            var preferences = engines.ToDictionary(
                e => e.Id,
                e => new EnginePreferences
                {
                    Port = e.Port,
                    AutoStart = e.StartOnAppLaunch
                }
            );

            return Ok(preferences);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get engine preferences");
            return StatusCode(500, new { error = ex.Message });
        }
    }

    /// <summary>
    /// Get diagnostics for a specific engine
    /// </summary>
    [HttpGet("diagnostics/engine")]
    public async Task<IActionResult> GetEngineDiagnostics([FromQuery] string engineId)
    {
        try
        {
            if (string.IsNullOrEmpty(engineId))
            {
                return BadRequest(new { error = "engineId is required" });
            }

            var manifest = await _manifestLoader.LoadManifestAsync();
            var engine = manifest.Engines.FirstOrDefault(e => e.Id == engineId);
            
            if (engine == null)
            {
                return NotFound(new { error = $"Engine {engineId} not found in manifest" });
            }

            var diagnostics = await _installer.GetDiagnosticsAsync(engine);
            
            // Get last error from process manager if available
            var status = _processManager.GetStatus(engineId);
            var result = new
            {
                diagnostics.EngineId,
                diagnostics.InstallPath,
                diagnostics.IsInstalled,
                diagnostics.PathExists,
                diagnostics.PathWritable,
                diagnostics.AvailableDiskSpaceBytes,
                LastError = status.LastError ?? diagnostics.LastError,
                diagnostics.ChecksumStatus,
                diagnostics.ExpectedSha256,
                diagnostics.ActualSha256,
                diagnostics.FailedUrl,
                diagnostics.Issues
            };

            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get diagnostics for engine {EngineId}", engineId);
            return StatusCode(500, new { error = ex.Message });
        }
    }

    /// <summary>
    /// Get system diagnostics report
    /// </summary>
    [HttpGet("diagnostics")]
    public async Task<IActionResult> GetDiagnostics()
    {
        try
        {
            var report = await _lifecycleManager.GenerateDiagnosticsAsync();
            return Ok(report);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to generate diagnostics report");
            return StatusCode(500, new { error = ex.Message });
        }
    }

    /// <summary>
    /// Get logs for a specific engine
    /// </summary>
    [HttpGet("logs")]
    public async Task<IActionResult> GetLogs([FromQuery] string engineId, [FromQuery] int tailLines = 500)
    {
        try
        {
            if (string.IsNullOrEmpty(engineId))
            {
                return BadRequest(new { error = "engineId is required" });
            }

            var logs = await _registry.GetEngineLogsAsync(engineId, tailLines);
            return Ok(new { logs });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get logs for engine {EngineId}", engineId);
            return StatusCode(500, new { error = ex.Message });
        }
    }

    /// <summary>
    /// Get recent notifications
    /// </summary>
    [HttpGet("notifications")]
    public IActionResult GetNotifications([FromQuery] int count = 100)
    {
        try
        {
            var notifications = _lifecycleManager.GetRecentNotifications(count);
            return Ok(new { notifications });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get notifications");
            return StatusCode(500, new { error = ex.Message });
        }
    }

    /// <summary>
    /// Restart a specific engine
    /// </summary>
    [HttpPost("restart")]
    public async Task<IActionResult> RestartEngine([FromBody] EngineActionRequest request, CancellationToken ct)
    {
        try
        {
            if (string.IsNullOrEmpty(request?.EngineId))
            {
                return BadRequest(new { error = "engineId is required" });
            }

            var success = await _lifecycleManager.RestartEngineAsync(request.EngineId, ct);
            
            if (success)
            {
                return Ok(new { message = $"Engine {request.EngineId} restarted successfully" });
            }
            else
            {
                return StatusCode(500, new { error = $"Failed to restart engine {request.EngineId}" });
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to restart engine");
            return StatusCode(500, new { error = ex.Message });
        }
    }

    /// <summary>
    /// Detect all engines (installed, running, or available)
    /// </summary>
    [HttpGet("detect")]
    public async Task<IActionResult> DetectEngines(CancellationToken ct)
    {
        try
        {
            if (_engineDetector == null)
            {
                return StatusCode(500, new { error = "Engine detection not available" });
            }

            var detectionResults = await _engineDetector.DetectAllEnginesAsync(null, null, ct);
            
            return Ok(new { engines = detectionResults });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to detect engines");
            return StatusCode(500, new { error = ex.Message });
        }
    }

    /// <summary>
    /// Get installation provenance for an engine
    /// </summary>
    [HttpGet("provenance")]
    public async Task<IActionResult> GetProvenance([FromQuery] string engineId)
    {
        try
        {
            if (string.IsNullOrEmpty(engineId))
            {
                return BadRequest(new { error = "engineId is required" });
            }

            var provenance = await _installer.ReadProvenanceAsync(engineId);
            
            if (provenance == null)
            {
                return NotFound(new { error = $"No provenance information found for engine {engineId}" });
            }

            return Ok(provenance);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get provenance for engine {EngineId}", engineId);
            return StatusCode(500, new { error = ex.Message });
        }
    }

    /// <summary>
    /// Open the installation folder for an engine
    /// </summary>
    [HttpPost("open-folder")]
    public IActionResult OpenFolder([FromBody] EngineActionRequest request)
    {
        try
        {
            if (string.IsNullOrEmpty(request.EngineId))
            {
                return BadRequest(new { error = "engineId is required" });
            }

            string installPath = _installer.GetInstallPath(request.EngineId);
            
            if (!Directory.Exists(installPath))
            {
                return NotFound(new { error = $"Installation path does not exist: {installPath}" });
            }

            // Open folder in file explorer
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                System.Diagnostics.Process.Start("explorer.exe", installPath);
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                System.Diagnostics.Process.Start("xdg-open", installPath);
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                System.Diagnostics.Process.Start("open", installPath);
            }

            return Ok(new { success = true, path = installPath });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to open folder for engine {EngineId}", request.EngineId);
            return StatusCode(500, new { error = ex.Message });
        }
    }

    /// <summary>
    /// Detect FFmpeg specifically
    /// </summary>
    [HttpGet("detect/ffmpeg")]
    public async Task<IActionResult> DetectFFmpeg([FromQuery] string? configuredPath = null)
    {
        try
        {
            if (_engineDetector == null)
            {
                return StatusCode(500, new { error = "Engine detection not available" });
            }

            var result = await _engineDetector.DetectFFmpegAsync(configuredPath);
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to detect FFmpeg");
            return StatusCode(500, new { error = ex.Message });
        }
    }

    /// <summary>
    /// Detect Ollama specifically
    /// </summary>
    [HttpGet("detect/ollama")]
    public async Task<IActionResult> DetectOllama([FromQuery] string? url = null, CancellationToken ct = default)
    {
        try
        {
            if (_engineDetector == null)
            {
                return StatusCode(500, new { error = "Engine detection not available" });
            }

            var result = await _engineDetector.DetectOllamaAsync(url, ct);
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to detect Ollama");
            return StatusCode(500, new { error = ex.Message });
        }
    }
}

public record InstallRequest(
    string EngineId, 
    string? Version = null, 
    int? Port = null,
    string? CustomUrl = null,
    string? LocalFilePath = null
);
public record EngineActionRequest(string EngineId);
public record StartRequest(string EngineId, int? Port = null, string? Args = null);
public record EnginePreferences
{
    public int? Port { get; set; }
    public bool AutoStart { get; set; }
}
