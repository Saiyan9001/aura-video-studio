using Aura.Api.Models.ApiModels.V1;
using Aura.Core.Services;
using Microsoft.AspNetCore.Mvc;
using Serilog;

namespace Aura.Api.Controllers;

/// <summary>
/// Controller for managing wizard-based video generation projects
/// </summary>
[ApiController]
[Route("api/wizard-projects")]
public class WizardProjectsController : ControllerBase
{
    private readonly WizardProjectService _projectService;

    public WizardProjectsController(WizardProjectService projectService)
    {
        _projectService = projectService ?? throw new ArgumentNullException(nameof(projectService));
    }

    /// <summary>
    /// Save or update a wizard project
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> SaveProject([FromBody] SaveWizardProjectRequest request, CancellationToken ct = default)
    {
        var correlationId = HttpContext.TraceIdentifier;

        try
        {
            if (string.IsNullOrWhiteSpace(request.Name))
            {
                return BadRequest(new
                {
                    type = "https://docs.aura.studio/errors/E400",
                    title = "Invalid Request",
                    status = 400,
                    detail = "Project name is required",
                    correlationId
                });
            }

            var project = await _projectService.SaveProjectAsync(
                request.Id,
                request.Name,
                request.Description,
                request.CurrentStep,
                request.BriefJson,
                request.PlanSpecJson,
                request.VoiceSpecJson,
                request.RenderSpecJson,
                ct);

            Log.Information("[{CorrelationId}] Saved wizard project {ProjectId}: {ProjectName}",
                correlationId, project.Id, project.Title);

            return Ok(new SaveWizardProjectResponse(
                project.Id,
                project.Title,
                project.UpdatedAt));
        }
        catch (Exception ex)
        {
            Log.Error(ex, "[{CorrelationId}] Failed to save wizard project", correlationId);
            return StatusCode(500, new
            {
                type = "https://docs.aura.studio/errors/E500",
                title = "Server Error",
                status = 500,
                detail = "Failed to save project",
                correlationId
            });
        }
    }

    /// <summary>
    /// Get a specific wizard project by ID
    /// </summary>
    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetProject(Guid id, CancellationToken ct = default)
    {
        var correlationId = HttpContext.TraceIdentifier;

        try
        {
            var project = await _projectService.GetProjectAsync(id, ct);
            if (project == null || project.IsDeleted)
            {
                return NotFound(new
                {
                    type = "https://docs.aura.studio/errors/E404",
                    title = "Project Not Found",
                    status = 404,
                    detail = $"Project {id} not found",
                    correlationId
                });
            }

            var assets = project.Assets.Select(a => new GeneratedAssetDto(
                a.AssetType,
                a.FilePath,
                a.FileSizeBytes,
                a.CreatedAt)).ToList();

            var response = new WizardProjectDetailsDto(
                project.Id,
                project.Title,
                project.Description,
                project.Status,
                project.ProgressPercent,
                project.CurrentWizardStep,
                project.CreatedAt,
                project.UpdatedAt,
                project.BriefJson,
                project.PlanSpecJson,
                project.VoiceSpecJson,
                project.RenderSpecJson,
                project.JobId,
                assets);

            return Ok(response);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "[{CorrelationId}] Failed to get wizard project {ProjectId}", correlationId, id);
            return StatusCode(500, new
            {
                type = "https://docs.aura.studio/errors/E500",
                title = "Server Error",
                status = 500,
                detail = "Failed to retrieve project",
                correlationId
            });
        }
    }

    /// <summary>
    /// Get all wizard projects for the current user
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetAllProjects(CancellationToken ct = default)
    {
        var correlationId = HttpContext.TraceIdentifier;

        try
        {
            var projects = await _projectService.GetAllProjectsAsync(ct);

            var response = projects.Select(p => new WizardProjectListItemDto(
                p.Id,
                p.Title,
                p.Description,
                p.Status,
                p.ProgressPercent,
                p.CurrentWizardStep,
                p.CreatedAt,
                p.UpdatedAt,
                p.JobId,
                p.Assets.Any())).ToList();

            return Ok(response);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "[{CorrelationId}] Failed to get wizard projects", correlationId);
            return StatusCode(500, new
            {
                type = "https://docs.aura.studio/errors/E500",
                title = "Server Error",
                status = 500,
                detail = "Failed to retrieve projects",
                correlationId
            });
        }
    }

    /// <summary>
    /// Get recent wizard projects
    /// </summary>
    [HttpGet("recent")]
    public async Task<IActionResult> GetRecentProjects([FromQuery] int count = 10, CancellationToken ct = default)
    {
        var correlationId = HttpContext.TraceIdentifier;

        try
        {
            var projects = await _projectService.GetRecentProjectsAsync(count, ct);

            var response = projects.Select(p => new WizardProjectListItemDto(
                p.Id,
                p.Title,
                p.Description,
                p.Status,
                p.ProgressPercent,
                p.CurrentWizardStep,
                p.CreatedAt,
                p.UpdatedAt,
                p.JobId,
                p.Assets.Any())).ToList();

            return Ok(response);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "[{CorrelationId}] Failed to get recent wizard projects", correlationId);
            return StatusCode(500, new
            {
                type = "https://docs.aura.studio/errors/E500",
                title = "Server Error",
                status = 500,
                detail = "Failed to retrieve recent projects",
                correlationId
            });
        }
    }

    /// <summary>
    /// Duplicate a wizard project
    /// </summary>
    [HttpPost("{id:guid}/duplicate")]
    public async Task<IActionResult> DuplicateProject(Guid id, [FromBody] DuplicateProjectRequest request, CancellationToken ct = default)
    {
        var correlationId = HttpContext.TraceIdentifier;

        try
        {
            if (string.IsNullOrWhiteSpace(request.NewName))
            {
                return BadRequest(new
                {
                    type = "https://docs.aura.studio/errors/E400",
                    title = "Invalid Request",
                    status = 400,
                    detail = "New project name is required",
                    correlationId
                });
            }

            var duplicate = await _projectService.DuplicateProjectAsync(id, request.NewName, ct);

            Log.Information("[{CorrelationId}] Duplicated project {SourceId} to {DuplicateId}",
                correlationId, id, duplicate.Id);

            return Ok(new SaveWizardProjectResponse(
                duplicate.Id,
                duplicate.Title,
                duplicate.UpdatedAt));
        }
        catch (InvalidOperationException ex)
        {
            return NotFound(new
            {
                type = "https://docs.aura.studio/errors/E404",
                title = "Project Not Found",
                status = 404,
                detail = ex.Message,
                correlationId
            });
        }
        catch (Exception ex)
        {
            Log.Error(ex, "[{CorrelationId}] Failed to duplicate wizard project {ProjectId}", correlationId, id);
            return StatusCode(500, new
            {
                type = "https://docs.aura.studio/errors/E500",
                title = "Server Error",
                status = 500,
                detail = "Failed to duplicate project",
                correlationId
            });
        }
    }

    /// <summary>
    /// Delete a wizard project
    /// </summary>
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> DeleteProject(Guid id, CancellationToken ct = default)
    {
        var correlationId = HttpContext.TraceIdentifier;

        try
        {
            await _projectService.DeleteProjectAsync(id, null, ct);

            Log.Information("[{CorrelationId}] Deleted wizard project {ProjectId}", correlationId, id);

            return NoContent();
        }
        catch (InvalidOperationException ex)
        {
            return NotFound(new
            {
                type = "https://docs.aura.studio/errors/E404",
                title = "Project Not Found",
                status = 404,
                detail = ex.Message,
                correlationId
            });
        }
        catch (Exception ex)
        {
            Log.Error(ex, "[{CorrelationId}] Failed to delete wizard project {ProjectId}", correlationId, id);
            return StatusCode(500, new
            {
                type = "https://docs.aura.studio/errors/E500",
                title = "Server Error",
                status = 500,
                detail = "Failed to delete project",
                correlationId
            });
        }
    }

    /// <summary>
    /// Export a wizard project as JSON
    /// </summary>
    [HttpGet("{id:guid}/export")]
    public async Task<IActionResult> ExportProject(Guid id, CancellationToken ct = default)
    {
        var correlationId = HttpContext.TraceIdentifier;

        try
        {
            var projectJson = await _projectService.ExportProjectAsync(id, ct);

            Log.Information("[{CorrelationId}] Exported wizard project {ProjectId}", correlationId, id);

            return Content(projectJson, "application/json");
        }
        catch (InvalidOperationException ex)
        {
            return NotFound(new
            {
                type = "https://docs.aura.studio/errors/E404",
                title = "Project Not Found",
                status = 404,
                detail = ex.Message,
                correlationId
            });
        }
        catch (Exception ex)
        {
            Log.Error(ex, "[{CorrelationId}] Failed to export wizard project {ProjectId}", correlationId, id);
            return StatusCode(500, new
            {
                type = "https://docs.aura.studio/errors/E500",
                title = "Server Error",
                status = 500,
                detail = "Failed to export project",
                correlationId
            });
        }
    }

    /// <summary>
    /// Import a wizard project from JSON
    /// </summary>
    [HttpPost("import")]
    public async Task<IActionResult> ImportProject([FromBody] ProjectImportRequest request, CancellationToken ct = default)
    {
        var correlationId = HttpContext.TraceIdentifier;

        try
        {
            if (string.IsNullOrWhiteSpace(request.ProjectJson))
            {
                return BadRequest(new
                {
                    type = "https://docs.aura.studio/errors/E400",
                    title = "Invalid Request",
                    status = 400,
                    detail = "Project JSON is required",
                    correlationId
                });
            }

            var project = await _projectService.ImportProjectAsync(request.ProjectJson, request.NewName, ct);

            Log.Information("[{CorrelationId}] Imported wizard project as {ProjectId}: {ProjectName}",
                correlationId, project.Id, project.Title);

            return Ok(new SaveWizardProjectResponse(
                project.Id,
                project.Title,
                project.UpdatedAt));
        }
        catch (Exception ex)
        {
            Log.Error(ex, "[{CorrelationId}] Failed to import wizard project", correlationId);
            return StatusCode(500, new
            {
                type = "https://docs.aura.studio/errors/E500",
                title = "Server Error",
                status = 500,
                detail = $"Failed to import project: {ex.Message}",
                correlationId
            });
        }
    }

    /// <summary>
    /// Clear generated content from a project but keep settings
    /// </summary>
    [HttpPost("{id:guid}/clear-content")]
    public async Task<IActionResult> ClearGeneratedContent(Guid id, [FromBody] ClearGeneratedContentRequest request, CancellationToken ct = default)
    {
        var correlationId = HttpContext.TraceIdentifier;

        try
        {
            await _projectService.ClearGeneratedContentAsync(
                id,
                request.KeepScript,
                request.KeepAudio,
                request.KeepImages,
                request.KeepVideo,
                ct);

            Log.Information("[{CorrelationId}] Cleared generated content for wizard project {ProjectId}", correlationId, id);

            return Ok(new
            {
                message = "Generated content cleared successfully",
                projectId = id
            });
        }
        catch (InvalidOperationException ex)
        {
            return NotFound(new
            {
                type = "https://docs.aura.studio/errors/E404",
                title = "Project Not Found",
                status = 404,
                detail = ex.Message,
                correlationId
            });
        }
        catch (Exception ex)
        {
            Log.Error(ex, "[{CorrelationId}] Failed to clear content for wizard project {ProjectId}", correlationId, id);
            return StatusCode(500, new
            {
                type = "https://docs.aura.studio/errors/E500",
                title = "Server Error",
                status = 500,
                detail = "Failed to clear generated content",
                correlationId
            });
        }
    }
}
