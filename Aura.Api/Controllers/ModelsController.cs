using Aura.Core.Downloads;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

namespace Aura.Api.Controllers;

[ApiController]
[Route("api/models")]
public class ModelsController : ControllerBase
{
    private readonly ILogger<ModelsController> _logger;
    private readonly ModelInstaller _modelInstaller;

    public ModelsController(
        ILogger<ModelsController> logger,
        ModelInstaller modelInstaller)
    {
        _logger = logger;
        _modelInstaller = modelInstaller;
    }

    /// <summary>
    /// List all models for an engine
    /// </summary>
    [HttpGet("list")]
    public async Task<IActionResult> List([FromQuery] string? engineId = null, [FromQuery] string? kind = null, CancellationToken ct = default)
    {
        try
        {
            var models = new List<ManagedModel>();

            if (!string.IsNullOrEmpty(kind) && Enum.TryParse<ModelKind>(kind, true, out var modelKind))
            {
                models = await _modelInstaller.GetModelsAsync(modelKind, ct);
            }
            else if (!string.IsNullOrEmpty(engineId))
            {
                // Get models based on engine ID
                models = engineId.ToLowerInvariant() switch
                {
                    "stable-diffusion-webui" or "comfyui" => await GetSdModelsAsync(ct),
                    "piper" => await _modelInstaller.GetModelsAsync(ModelKind.PIPER_VOICE, ct),
                    "mimic3" => await _modelInstaller.GetModelsAsync(ModelKind.MIMIC3_VOICE, ct),
                    _ => new List<ManagedModel>()
                };
            }

            return Ok(new
            {
                models = models.Select(m => new
                {
                    m.Id,
                    m.Name,
                    kind = m.Kind.ToString(),
                    m.SizeBytes,
                    m.Sha256,
                    m.FilePath,
                    m.IsExternal,
                    m.Provenance,
                    m.VerificationStatus,
                    m.LastVerified,
                    sizeFormatted = FormatBytes(m.SizeBytes)
                })
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to list models");
            return StatusCode(500, new { error = ex.Message });
        }
    }

    /// <summary>
    /// Install a model
    /// </summary>
    [HttpPost("install")]
    public async Task<IActionResult> Install([FromBody] ModelInstallRequest request, CancellationToken ct = default)
    {
        try
        {
            if (!Enum.TryParse<ModelKind>(request.Kind, true, out var modelKind))
            {
                return BadRequest(new { error = $"Invalid model kind: {request.Kind}" });
            }

            var model = new ManagedModel
            {
                Id = request.Id,
                Name = request.Name,
                Kind = modelKind,
                SizeBytes = request.SizeBytes,
                Sha256 = request.Sha256,
                Mirrors = request.Mirrors ?? new List<string>()
            };

            var progress = new Progress<ModelInstallProgress>(p =>
            {
                _logger.LogInformation("Install progress for {ModelId}: {Phase} - {Percent}%", 
                    p.ModelId, p.Phase, p.PercentComplete);
            });

            await _modelInstaller.InstallAsync(model, request.DestinationPath, progress, ct);

            return Ok(new { success = true, message = "Model installed successfully" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to install model {ModelId}", request.Id);
            return StatusCode(500, new { error = ex.Message });
        }
    }

    /// <summary>
    /// Add an external folder to index models from
    /// </summary>
    [HttpPost("add-external")]
    public async Task<IActionResult> AddExternal([FromBody] ModelAddExternalRequest request, CancellationToken ct = default)
    {
        try
        {
            if (!Enum.TryParse<ModelKind>(request.Kind, true, out var modelKind))
            {
                return BadRequest(new { error = $"Invalid model kind: {request.Kind}" });
            }

            var discoveredModels = await _modelInstaller.AddExternalDirectoryAsync(
                modelKind,
                request.FolderPath,
                request.IsReadOnly ?? true,
                ct
            );

            return Ok(new
            {
                success = true,
                message = $"Added external directory with {discoveredModels.Count} models",
                models = discoveredModels.Select(m => new
                {
                    m.Id,
                    m.Name,
                    kind = m.Kind.ToString(),
                    m.SizeBytes,
                    m.FilePath,
                    sizeFormatted = FormatBytes(m.SizeBytes)
                })
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to add external directory {Path}", request.FolderPath);
            return StatusCode(500, new { error = ex.Message });
        }
    }

    /// <summary>
    /// Remove a model
    /// </summary>
    [HttpPost("remove")]
    public async Task<IActionResult> Remove([FromBody] ModelRemoveRequest request, CancellationToken ct = default)
    {
        try
        {
            if (string.IsNullOrEmpty(request.FilePath))
            {
                return BadRequest(new { error = "FilePath is required" });
            }

            await _modelInstaller.RemoveAsync(request.ModelId, request.FilePath, ct);

            return Ok(new { success = true, message = "Model removed successfully" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to remove model {ModelId}", request.ModelId);
            return StatusCode(500, new { error = ex.Message });
        }
    }

    /// <summary>
    /// Verify a model's checksum
    /// </summary>
    [HttpPost("verify")]
    public async Task<IActionResult> Verify([FromBody] ModelVerifyRequest request, CancellationToken ct = default)
    {
        try
        {
            var result = await _modelInstaller.VerifyAsync(request.FilePath, request.ExpectedSha256, ct);

            return Ok(new
            {
                result.ModelId,
                result.IsValid,
                result.Status,
                result.ExpectedSha256,
                result.ActualSha256,
                result.Issues
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to verify model at {Path}", request.FilePath);
            return StatusCode(500, new { error = ex.Message });
        }
    }

    /// <summary>
    /// Open the folder containing a model
    /// </summary>
    [HttpPost("open-folder")]
    public IActionResult OpenFolder([FromBody] ModelOpenFolderRequest request)
    {
        try
        {
            if (string.IsNullOrEmpty(request.FilePath) || !System.IO.File.Exists(request.FilePath))
            {
                return NotFound(new { error = "File not found" });
            }

            string? directory = System.IO.Path.GetDirectoryName(request.FilePath);
            if (string.IsNullOrEmpty(directory))
            {
                return BadRequest(new { error = "Invalid file path" });
            }

            // Open file explorer to the folder
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                Process.Start("explorer.exe", $"/select,\"{request.FilePath}\"");
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                Process.Start("xdg-open", directory);
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                Process.Start("open", directory);
            }

            return Ok(new { success = true, directory });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to open folder for {Path}", request.FilePath);
            return StatusCode(500, new { error = ex.Message });
        }
    }

    /// <summary>
    /// List configured external directories
    /// </summary>
    [HttpGet("external-directories")]
    public IActionResult GetExternalDirectories()
    {
        try
        {
            var directories = _modelInstaller.GetExternalDirectories();

            return Ok(new
            {
                directories = directories.Select(d => new
                {
                    d.Path,
                    kind = d.Kind.ToString(),
                    d.IsReadOnly,
                    d.AddedAt
                })
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get external directories");
            return StatusCode(500, new { error = ex.Message });
        }
    }

    /// <summary>
    /// Remove an external directory configuration
    /// </summary>
    [HttpPost("remove-external-directory")]
    public IActionResult RemoveExternalDirectory([FromBody] ModelRemoveExternalDirectoryRequest request)
    {
        try
        {
            _modelInstaller.RemoveExternalDirectory(request.Path);

            return Ok(new { success = true, message = "External directory removed" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to remove external directory {Path}", request.Path);
            return StatusCode(500, new { error = ex.Message });
        }
    }

    private async Task<List<ManagedModel>> GetSdModelsAsync(CancellationToken ct)
    {
        var models = new List<ManagedModel>();
        models.AddRange(await _modelInstaller.GetModelsAsync(ModelKind.SD_BASE, ct));
        models.AddRange(await _modelInstaller.GetModelsAsync(ModelKind.SD_REFINER, ct));
        models.AddRange(await _modelInstaller.GetModelsAsync(ModelKind.VAE, ct));
        models.AddRange(await _modelInstaller.GetModelsAsync(ModelKind.LORA, ct));
        return models;
    }

    private static string FormatBytes(long bytes)
    {
        string[] suffixes = { "B", "KB", "MB", "GB", "TB" };
        int suffixIndex = 0;
        double size = bytes;

        while (size >= 1024 && suffixIndex < suffixes.Length - 1)
        {
            size /= 1024;
            suffixIndex++;
        }

        return $"{size:F2} {suffixes[suffixIndex]}";
    }
}

// Request/Response models
public record ModelInstallRequest(
    string Id,
    string Name,
    string Kind,
    long SizeBytes,
    string? Sha256,
    List<string>? Mirrors,
    string? DestinationPath
);

public record ModelAddExternalRequest(
    string Kind,
    string FolderPath,
    bool? IsReadOnly
);

public record ModelRemoveRequest(
    string ModelId,
    string FilePath
);

public record ModelVerifyRequest(
    string FilePath,
    string? ExpectedSha256
);

public record ModelOpenFolderRequest(
    string FilePath
);

public record ModelRemoveExternalDirectoryRequest(
    string Path
);
