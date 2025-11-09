using System;
using System.Threading;
using System.Threading.Tasks;
using Aura.Api.Models;
using Aura.Core.Dependencies;
using Aura.Core.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace Aura.Api.Controllers;

[ApiController]
[Route("api/dependencies")]
public class DependenciesController : ControllerBase
{
    private readonly ILogger<DependenciesController> _logger;
    private readonly DependencyRescanService _rescanService;
    private readonly FfmpegInstaller? _ffmpegInstaller;
    private readonly FfmpegLocator? _ffmpegLocator;

    public DependenciesController(
        ILogger<DependenciesController> logger,
        DependencyRescanService rescanService,
        FfmpegInstaller? ffmpegInstaller = null,
        FfmpegLocator? ffmpegLocator = null)
    {
        _logger = logger;
        _rescanService = rescanService;
        _ffmpegInstaller = ffmpegInstaller;
        _ffmpegLocator = ffmpegLocator;
    }

    /// <summary>
    /// Rescan all dependencies and return full report
    /// </summary>
    [HttpPost("rescan")]
    public async Task<IActionResult> RescanAll(CancellationToken ct)
    {
        try
        {
            _logger.LogInformation("Starting full dependency rescan via API");
            
            var report = await _rescanService.RescanAllAsync(ct);
            
            return Ok(new
            {
                success = true,
                scanTime = report.ScanTime,
                dependencies = report.Dependencies
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to rescan dependencies");
            return StatusCode(500, new
            {
                success = false,
                error = ex.Message,
                errorType = ex.GetType().Name,
                troubleshooting = new[]
                {
                    "Ensure all dependency services are properly configured",
                    "Check if FFmpegLocator and ComponentDownloader are registered in DI",
                    "Verify components.json file exists and is valid",
                    "Check application logs for detailed error information"
                }
            });
        }
    }

    /// <summary>
    /// Re-run only path checks (fast refresh)
    /// </summary>
    [HttpPost("refresh-candidate-paths")]
    public async Task<IActionResult> RefreshCandidatePaths(CancellationToken ct)
    {
        try
        {
            _logger.LogInformation("Refreshing candidate paths");
            
            // This is essentially the same as a full rescan for now
            // In the future, this could be optimized to only check file paths
            // without running validation commands
            var report = await _rescanService.RescanAllAsync(ct);
            
            return Ok(new
            {
                success = true,
                scanTime = report.ScanTime,
                dependencies = report.Dependencies
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to refresh candidate paths");
            return StatusCode(500, new
            {
                success = false,
                error = ex.Message
            });
        }
    }

    /// <summary>
    /// Get the last scan time
    /// </summary>
    [HttpGet("last-scan-time")]
    public async Task<IActionResult> GetLastScanTime()
    {
        try
        {
            var lastScanTime = await _rescanService.GetLastScanTimeAsync();
            
            return Ok(new
            {
                success = true,
                lastScanTime = lastScanTime
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get last scan time");
            return StatusCode(500, new
            {
                success = false,
                error = ex.Message
            });
        }
    }
    
    /// <summary>
    /// Verify FFmpeg installation and run smoke test
    /// </summary>
    [HttpPost("{componentId}/verify")]
    public async Task<IActionResult> VerifyComponent(string componentId, CancellationToken ct)
    {
        if (componentId != "ffmpeg" || _ffmpegInstaller == null)
        {
            return BadRequest(new
            {
                success = false,
                error = $"Verification not supported for component: {componentId}"
            });
        }
        
        try
        {
            _logger.LogInformation("Verifying FFmpeg installation via API");
            
            // Try to find FFmpeg via rescan
            var report = await _rescanService.RescanAllAsync(ct);
            var ffmpegDep = report.Dependencies.Find(d => d.Id == "ffmpeg");
            
            if (ffmpegDep == null || ffmpegDep.Status != DependencyStatus.Installed || string.IsNullOrEmpty(ffmpegDep.Path))
            {
                return Ok(new
                {
                    success = false,
                    available = false,
                    status = ffmpegDep?.Status.ToString() ?? "Missing",
                    error = ffmpegDep?.ErrorMessage ?? "FFmpeg not found. Install or attach FFmpeg first.",
                    troubleshooting = new[]
                    {
                        "Use the 'Install' button to download FFmpeg automatically",
                        "Use 'Attach Existing' if you already have FFmpeg installed",
                        "Click 'Manual Install Guide' for step-by-step installation instructions",
                        "Ensure FFmpeg is in your system PATH or Aura's dependencies folder"
                    }
                });
            }
            
            // Run smoke test
            var smokeTestResult = await _ffmpegInstaller.RunSmokeTestAsync(ffmpegDep.Path, ct);
            
            if (!smokeTestResult.success)
            {
                return Ok(new
                {
                    success = false,
                    available = true,
                    path = ffmpegDep.Path,
                    validationOutput = ffmpegDep.ValidationOutput,
                    smokeTestPassed = false,
                    error = smokeTestResult.error,
                    diagnostics = new
                    {
                        output = smokeTestResult.output,
                        suggestion = "FFmpeg binary may be corrupted. Try reinstalling or repairing."
                    }
                });
            }
            
            return Ok(new
            {
                success = true,
                available = true,
                path = ffmpegDep.Path,
                validationOutput = ffmpegDep.ValidationOutput,
                smokeTestPassed = true,
                output = smokeTestResult.output
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to verify {ComponentId}", componentId);
            return StatusCode(500, new
            {
                success = false,
                error = ex.Message
            });
        }
    }
    
    /// <summary>
    /// Repair or reinstall a component
    /// </summary>
    [HttpPost("{componentId}/repair")]
    public async Task<IActionResult> RepairComponent(string componentId, CancellationToken ct)
    {
        if (componentId != "ffmpeg")
        {
            return BadRequest(new
            {
                success = false,
                error = $"Repair not supported for component: {componentId}"
            });
        }
        
        try
        {
            _logger.LogInformation("Repairing {ComponentId} via API", componentId);
            
            // For now, repair = rescan to update paths
            // In the future, this could trigger reinstallation
            var report = await _rescanService.RescanAllAsync(ct);
            var ffmpegDep = report.Dependencies.Find(d => d.Id == "ffmpeg");
            
            if (ffmpegDep == null || ffmpegDep.Status != DependencyStatus.Installed)
            {
                return Ok(new
                {
                    success = false,
                    repaired = false,
                    status = ffmpegDep?.Status.ToString() ?? "Missing",
                    message = "FFmpeg not found. Use Download Center to install FFmpeg."
                });
            }
            
            return Ok(new
            {
                success = true,
                repaired = true,
                path = ffmpegDep.Path,
                validationOutput = ffmpegDep.ValidationOutput,
                message = "FFmpeg path refreshed successfully"
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to repair {ComponentId}", componentId);
            return StatusCode(500, new
            {
                success = false,
                error = ex.Message
            });
        }
    }
    
    /// <summary>
    /// Install a dependency component
    /// </summary>
    [HttpPost("{componentId}/install")]
    public async Task<IActionResult> InstallComponent(string componentId, CancellationToken ct)
    {
        if (componentId != "ffmpeg" || _ffmpegInstaller == null)
        {
            return BadRequest(new
            {
                success = false,
                error = $"Installation not supported for component: {componentId}"
            });
        }
        
        try
        {
            _logger.LogInformation("Installing {ComponentId} via API", componentId);
            
            // Use default mirrors for installation
            var mirrors = new[]
            {
                "https://github.com/GyanD/codexffmpeg/releases/latest/download/ffmpeg-release-essentials.zip",
                "https://www.gyan.dev/ffmpeg/builds/ffmpeg-release-essentials.zip"
            };
            
            var result = await _ffmpegInstaller.InstallFromMirrorsAsync(
                mirrors,
                "latest",
                null,
                null,
                ct);
            
            if (result.Success)
            {
                return Ok(new
                {
                    success = true,
                    installed = true,
                    path = result.FfmpegPath,
                    version = result.ValidationOutput != null ? ExtractVersionFromValidationOutput(result.ValidationOutput) : null,
                    installPath = result.InstallPath,
                    source = result.SourceType.ToString(),
                    logPath = (string?)null
                });
            }
            else
            {
                return BadRequest(new
                {
                    success = false,
                    installed = false,
                    error = result.ErrorMessage
                });
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to install {ComponentId}", componentId);
            return StatusCode(500, new
            {
                success = false,
                error = ex.Message
            });
        }
    }
    
    /// <summary>
    /// Attach existing dependency installation
    /// </summary>
    [HttpPost("{componentId}/attach")]
    public async Task<IActionResult> AttachComponent(
        string componentId, 
        [FromBody] AttachComponentRequest request,
        CancellationToken ct)
    {
        if (string.IsNullOrEmpty(request.Path))
        {
            return BadRequest(new
            {
                success = false,
                error = "Path is required"
            });
        }
        
        try
        {
            _logger.LogInformation("Attaching {ComponentId} from path: {Path}", componentId, request.Path);
            
            // Handle different component types
            switch (componentId.ToLowerInvariant())
            {
                case "ffmpeg":
                    return await AttachFFmpegAsync(request.Path, ct);
                
                case "ollama":
                    return await AttachOllamaAsync(request.Path, ct);
                
                case "stable-diffusion":
                case "stable-diffusion-webui":
                case "sd-webui":
                    return await AttachStableDiffusionAsync(request.Path, ct);
                
                default:
                    return BadRequest(new
                    {
                        success = false,
                        error = $"Attach not supported for component: {componentId}. Please use the Download Center for installation."
                    });
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to attach {ComponentId}", componentId);
            return StatusCode(500, new
            {
                success = false,
                error = $"An error occurred while attaching {componentId}: {ex.Message}"
            });
        }
    }
    
    private async Task<IActionResult> AttachFFmpegAsync(string path, CancellationToken ct)
    {
        if (_ffmpegInstaller == null || _ffmpegLocator == null)
        {
            return BadRequest(new
            {
                success = false,
                error = "FFmpeg installer not available"
            });
        }
        
        // Validate the path first
        var validation = await _ffmpegLocator.ValidatePathAsync(path, ct);
        
        if (!validation.Found || string.IsNullOrEmpty(validation.FfmpegPath))
        {
            return BadRequest(new
            {
                success = false,
                error = validation.Reason ?? "FFmpeg not found at specified path",
                attemptedPaths = validation.AttemptedPaths
            });
        }
        
        // Attach using installer
        var result = await _ffmpegInstaller.AttachExistingAsync(validation.FfmpegPath, ct);
        
        if (result.Success)
        {
            return Ok(new
            {
                success = true,
                installed = true,
                path = result.FfmpegPath,
                version = validation.VersionString,
                installPath = result.InstallPath,
                source = result.SourceType.ToString()
            });
        }
        else
        {
            return BadRequest(new
            {
                success = false,
                error = result.ErrorMessage
            });
        }
    }
    
    private Task<IActionResult> AttachOllamaAsync(string path, CancellationToken ct)
    {
        // Check if path is a URL (Ollama API endpoint)
        if (Uri.TryCreate(path, UriKind.Absolute, out var uri) && 
            (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps))
        {
            // For security, restrict to localhost addresses only
            if (uri.Host == "localhost" || uri.Host == "127.0.0.1" || uri.Host == "[::1]")
            {
                _logger.LogInformation("Ollama path is a localhost URL, treating as API endpoint: {Path}", path);
                
                return Task.FromResult<IActionResult>(Ok(new
                {
                    success = true,
                    installed = true,
                    path = path,
                    message = "Ollama API endpoint set. Ensure Ollama is running at this address."
                }));
            }
            else
            {
                _logger.LogWarning("Rejecting non-localhost Ollama URL: {Host}", uri.Host);
                return Task.FromResult<IActionResult>(BadRequest(new
                {
                    success = false,
                    error = "For security, only localhost URLs are allowed (localhost, 127.0.0.1)"
                }));
            }
        }
        
        // Check if path is a directory or executable
        if (System.IO.Directory.Exists(path))
        {
            // Look for ollama.exe in the directory
            var exePath = System.IO.Path.Combine(path, "ollama.exe");
            if (System.IO.File.Exists(exePath))
            {
                _logger.LogInformation("Found Ollama executable at: {Path}", exePath);
                return Task.FromResult<IActionResult>(Ok(new
                {
                    success = true,
                    installed = true,
                    path = exePath,
                    message = "Ollama installation found. You can start it from the Engines page."
                }));
            }
            else
            {
                return Task.FromResult<IActionResult>(BadRequest(new
                {
                    success = false,
                    error = "Ollama executable (ollama.exe) not found in the specified directory"
                }));
            }
        }
        else if (System.IO.File.Exists(path))
        {
            // Verify it's the ollama executable
            if (path.EndsWith("ollama.exe", StringComparison.OrdinalIgnoreCase) ||
                path.EndsWith("ollama", StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogInformation("Ollama executable found at: {Path}", path);
                return Task.FromResult<IActionResult>(Ok(new
                {
                    success = true,
                    installed = true,
                    path = path,
                    message = "Ollama installation found. You can start it from the Engines page."
                }));
            }
            else
            {
                return Task.FromResult<IActionResult>(BadRequest(new
                {
                    success = false,
                    error = "The specified file does not appear to be an Ollama executable"
                }));
            }
        }
        else
        {
            return Task.FromResult<IActionResult>(BadRequest(new
            {
                success = false,
                error = "The specified path does not exist. Please provide a valid directory or file path."
            }));
        }
    }
    
    private Task<IActionResult> AttachStableDiffusionAsync(string path, CancellationToken ct)
    {
        // Check if path is a URL (SD WebUI API endpoint)
        if (Uri.TryCreate(path, UriKind.Absolute, out var uri) && 
            (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps))
        {
            // For security, restrict to localhost addresses only
            if (uri.Host == "localhost" || uri.Host == "127.0.0.1" || uri.Host == "[::1]")
            {
                _logger.LogInformation("Stable Diffusion path is a localhost URL, treating as API endpoint: {Path}", path);
                
                return Task.FromResult<IActionResult>(Ok(new
                {
                    success = true,
                    installed = true,
                    path = path,
                    message = "Stable Diffusion WebUI endpoint set. Ensure the WebUI is running at this address."
                }));
            }
            else
            {
                _logger.LogWarning("Rejecting non-localhost SD WebUI URL: {Host}", uri.Host);
                return Task.FromResult<IActionResult>(BadRequest(new
                {
                    success = false,
                    error = "For security, only localhost URLs are allowed (localhost, 127.0.0.1)"
                }));
            }
        }
        
        // Check if path is a directory
        if (System.IO.Directory.Exists(path))
        {
            // Look for webui.py or webui-user.bat
            var webUiPy = System.IO.Path.Combine(path, "webui.py");
            var webUiBat = System.IO.Path.Combine(path, "webui-user.bat");
            
            if (System.IO.File.Exists(webUiPy) || System.IO.File.Exists(webUiBat))
            {
                _logger.LogInformation("Found Stable Diffusion WebUI at: {Path}", path);
                return Task.FromResult<IActionResult>(Ok(new
                {
                    success = true,
                    installed = true,
                    path = path,
                    message = "Stable Diffusion WebUI installation found. You can start it from the Engines page."
                }));
            }
            else
            {
                return Task.FromResult<IActionResult>(BadRequest(new
                {
                    success = false,
                    error = "Stable Diffusion WebUI files (webui.py or webui-user.bat) not found in the specified directory"
                }));
            }
        }
        else
        {
            return Task.FromResult<IActionResult>(BadRequest(new
            {
                success = false,
                error = "The specified path does not exist. Please provide a valid directory path to Stable Diffusion WebUI."
            }));
        }
    }
    
    private string? ExtractVersionFromValidationOutput(string? output)
    {
        if (string.IsNullOrEmpty(output))
            return null;

        try
        {
            var firstLine = output.Split('\n')[0];
            if (firstLine.Contains("ffmpeg version", StringComparison.OrdinalIgnoreCase))
            {
                var parts = firstLine.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length >= 3)
                {
                    return parts[2];
                }
            }
        }
        catch
        {
            // Ignore errors
        }

        return null;
    }

    /// <summary>
    /// Auto-detect installation path for a specific dependency component
    /// </summary>
    [HttpPost("{componentId}/detect")]
    public async Task<IActionResult> AutoDetect(string componentId, CancellationToken ct)
    {
        try
        {
            _logger.LogInformation("Auto-detecting path for {ComponentId}", componentId);

            string? detectedPath = null;

            switch (componentId.ToLowerInvariant())
            {
                case "ollama":
                    detectedPath = OllamaService.FindOllamaExecutable();
                    break;

                case "ffmpeg":
                    if (_ffmpegLocator != null)
                    {
                        var result = await _ffmpegLocator.CheckAllCandidatesAsync(null, ct).ConfigureAwait(false);
                        if (result.Found)
                        {
                            detectedPath = result.FfmpegPath;
                        }
                    }
                    break;
            }

            if (!string.IsNullOrEmpty(detectedPath))
            {
                return Ok(new
                {
                    success = true,
                    path = detectedPath
                });
            }
            else
            {
                return Ok(new
                {
                    success = false,
                    path = (string?)null
                });
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to auto-detect {ComponentId}", componentId);
            return Ok(new
            {
                success = false,
                path = (string?)null
            });
        }
    }

    /// <summary>
    /// Validate a path for a specific dependency component
    /// </summary>
    [HttpPost("{componentId}/validate-path")]
    public async Task<IActionResult> ValidatePath(
        string componentId,
        [FromBody] ValidatePathRequest request,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Path))
        {
            return BadRequest(new
            {
                isValid = false,
                message = "Path is required"
            });
        }

        try
        {
            _logger.LogInformation("Validating path for {ComponentId}: {Path}", componentId, request.Path);

            switch (componentId.ToLowerInvariant())
            {
                case "ollama":
                    var ollamaResult = await OllamaService.ValidateOllamaPathAsync(request.Path, ct);
                    return Ok(new
                    {
                        isValid = ollamaResult.IsValid,
                        message = ollamaResult.Message,
                        version = ollamaResult.Version
                    });

                case "ffmpeg":
                    if (_ffmpegLocator == null)
                    {
                        return BadRequest(new
                        {
                            isValid = false,
                            message = "FFmpeg locator not available"
                        });
                    }

                    var ffmpegValidation = await _ffmpegLocator.ValidatePathAsync(request.Path, ct);
                    return Ok(new
                    {
                        isValid = ffmpegValidation.Found,
                        message = ffmpegValidation.Found ? "Valid FFmpeg executable" : (ffmpegValidation.Reason ?? "Invalid path"),
                        version = ffmpegValidation.VersionString
                    });

                default:
                    // Generic file existence check for other components
                    var exists = System.IO.File.Exists(request.Path);
                    return Ok(new
                    {
                        isValid = exists,
                        message = exists ? "File exists" : "File not found"
                    });
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to validate path for {ComponentId}", componentId);
            return Ok(new
            {
                isValid = false,
                message = $"Validation error: {ex.Message}"
            });
        }
    }

    /// <summary>
    /// Get comprehensive dependency status including Node.js, .NET, Python
    /// </summary>
    [HttpGet("check")]
    public async Task<IActionResult> CheckDependencies(CancellationToken ct)
    {
        try
        {
            _logger.LogInformation("Checking comprehensive dependency status via API");
            
            var detector = new Core.Services.Setup.DependencyDetector(
                Microsoft.Extensions.Logging.Abstractions.NullLogger<Core.Services.Setup.DependencyDetector>.Instance,
                _ffmpegLocator,
                new System.Net.Http.HttpClient());
            
            var status = await detector.DetectAllDependenciesAsync(ct);
            
            return Ok(new
            {
                success = true,
                ffmpeg = new
                {
                    installed = status.FFmpegInstalled,
                    version = status.FFmpegVersion,
                    installationRequired = status.FFmpegInstallationRequired
                },
                nodejs = new
                {
                    installed = status.NodeJsInstalled,
                    version = status.NodeJsVersion
                },
                dotnet = new
                {
                    installed = status.DotNetInstalled,
                    version = status.DotNetVersion
                },
                python = new
                {
                    installed = status.PythonInstalled,
                    version = status.PythonVersion
                },
                ollama = new
                {
                    installed = status.OllamaInstalled,
                    version = status.OllamaVersion
                },
                piperTts = new
                {
                    installed = status.PiperTtsInstalled,
                    path = status.PiperTtsPath
                },
                nvidia = new
                {
                    installed = status.NvidiaDriversInstalled,
                    version = status.NvidiaDriverVersion
                },
                diskSpaceGB = status.DiskSpaceGB,
                internetConnected = status.InternetConnected
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to check dependencies");
            return StatusCode(500, new
            {
                success = false,
                error = ex.Message
            });
        }
    }
}

public class AttachComponentRequest
{
    public string Path { get; set; } = "";
    public bool AttachInPlace { get; set; } = false;
}
