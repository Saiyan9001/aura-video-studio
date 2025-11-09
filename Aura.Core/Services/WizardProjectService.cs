using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Aura.Core.Data;
using Microsoft.Extensions.Logging;

namespace Aura.Core.Services;

/// <summary>
/// Service for managing wizard-based project saving and loading
/// </summary>
public class WizardProjectService
{
    private readonly ProjectStateRepository _repository;
    private readonly ILogger<WizardProjectService> _logger;

    public WizardProjectService(
        ProjectStateRepository repository,
        ILogger<WizardProjectService> logger)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Save or update a wizard project
    /// </summary>
    public async Task<ProjectStateEntity> SaveProjectAsync(
        Guid? id,
        string name,
        string? description,
        int currentStep,
        string? briefJson,
        string? planSpecJson,
        string? voiceSpecJson,
        string? renderSpecJson,
        CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        ProjectStateEntity project;

        if (id.HasValue)
        {
            project = await _repository.GetByIdAsync(id.Value, ct);
            if (project == null)
            {
                throw new InvalidOperationException($"Project {id.Value} not found");
            }

            project.Title = name;
            project.Description = description;
            project.CurrentWizardStep = currentStep;
            project.BriefJson = briefJson;
            project.PlanSpecJson = planSpecJson;
            project.VoiceSpecJson = voiceSpecJson;
            project.RenderSpecJson = renderSpecJson;
            project.UpdatedAt = DateTime.UtcNow;

            await _repository.UpdateAsync(project, ct);
            _logger.LogInformation("Updated wizard project {ProjectId}: {ProjectName}", project.Id, project.Title);
        }
        else
        {
            project = new ProjectStateEntity
            {
                Id = Guid.NewGuid(),
                Title = name,
                Description = description,
                CurrentWizardStep = currentStep,
                Status = "Draft",
                BriefJson = briefJson,
                PlanSpecJson = planSpecJson,
                VoiceSpecJson = voiceSpecJson,
                RenderSpecJson = renderSpecJson,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            await _repository.CreateAsync(project, ct);
            _logger.LogInformation("Created new wizard project {ProjectId}: {ProjectName}", project.Id, project.Title);
        }

        return project;
    }

    /// <summary>
    /// Get a wizard project by ID
    /// </summary>
    public async Task<ProjectStateEntity?> GetProjectAsync(Guid id, CancellationToken ct = default)
    {
        return await _repository.GetByIdAsync(id, ct);
    }

    /// <summary>
    /// Get all non-deleted wizard projects for the user
    /// </summary>
    public async Task<List<ProjectStateEntity>> GetAllProjectsAsync(CancellationToken ct = default)
    {
        var allProjects = await _repository.GetIncompleteProjectsAsync(ct);
        
        return allProjects
            .Where(p => !p.IsDeleted)
            .OrderByDescending(p => p.UpdatedAt)
            .ToList();
    }

    /// <summary>
    /// Get recent wizard projects (non-deleted, ordered by update time)
    /// </summary>
    public async Task<List<ProjectStateEntity>> GetRecentProjectsAsync(int count = 10, CancellationToken ct = default)
    {
        var allProjects = await _repository.GetIncompleteProjectsAsync(ct);
        
        return allProjects
            .Where(p => !p.IsDeleted)
            .OrderByDescending(p => p.UpdatedAt)
            .Take(count)
            .ToList();
    }

    /// <summary>
    /// Duplicate a wizard project with a new name
    /// </summary>
    public async Task<ProjectStateEntity> DuplicateProjectAsync(
        Guid sourceId,
        string newName,
        CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(newName);

        var source = await _repository.GetByIdAsync(sourceId, ct);
        if (source == null)
        {
            throw new InvalidOperationException($"Source project {sourceId} not found");
        }

        var duplicate = new ProjectStateEntity
        {
            Id = Guid.NewGuid(),
            Title = newName,
            Description = source.Description,
            CurrentWizardStep = source.CurrentWizardStep,
            Status = "Draft",
            BriefJson = source.BriefJson,
            PlanSpecJson = source.PlanSpecJson,
            VoiceSpecJson = source.VoiceSpecJson,
            RenderSpecJson = source.RenderSpecJson,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        await _repository.CreateAsync(duplicate, ct);
        _logger.LogInformation("Duplicated project {SourceId} to {DuplicateId}: {NewName}",
            sourceId, duplicate.Id, newName);

        return duplicate;
    }

    /// <summary>
    /// Soft delete a wizard project
    /// </summary>
    public async Task DeleteProjectAsync(Guid id, string? userId = null, CancellationToken ct = default)
    {
        var project = await _repository.GetByIdAsync(id, ct);
        if (project == null)
        {
            throw new InvalidOperationException($"Project {id} not found");
        }

        project.IsDeleted = true;
        project.DeletedAt = DateTime.UtcNow;
        project.DeletedByUserId = userId;
        project.Status = "Deleted";

        await _repository.UpdateAsync(project, ct);
        _logger.LogInformation("Soft deleted project {ProjectId}: {ProjectName}", id, project.Title);
    }

    /// <summary>
    /// Clear generated content but keep project settings
    /// </summary>
    public async Task ClearGeneratedContentAsync(
        Guid id,
        bool keepScript,
        bool keepAudio,
        bool keepImages,
        bool keepVideo,
        CancellationToken ct = default)
    {
        var project = await _repository.GetByIdAsync(id, ct);
        if (project == null)
        {
            throw new InvalidOperationException($"Project {id} not found");
        }

        if (!keepScript)
        {
            project.PlanSpecJson = null;
        }

        var assetsToRemove = project.Assets.Where(a =>
        {
            if (a.AssetType == "Script" && !keepScript) return true;
            if (a.AssetType == "Audio" && !keepAudio) return true;
            if (a.AssetType == "Image" && !keepImages) return true;
            if (a.AssetType == "Video" && !keepVideo) return true;
            return false;
        }).ToList();

        foreach (var asset in assetsToRemove)
        {
            project.Assets.Remove(asset);
        }

        project.Status = "Draft";
        project.ProgressPercent = 0;
        project.JobId = null;
        project.UpdatedAt = DateTime.UtcNow;

        await _repository.UpdateAsync(project, ct);
        _logger.LogInformation("Cleared generated content for project {ProjectId}", id);
    }

    /// <summary>
    /// Export a wizard project to JSON
    /// </summary>
    public async Task<string> ExportProjectAsync(Guid id, CancellationToken ct = default)
    {
        var project = await _repository.GetByIdAsync(id, ct);
        if (project == null)
        {
            throw new InvalidOperationException($"Project {id} not found");
        }

        var exportData = new
        {
            version = "1.0.0",
            exportedAt = DateTime.UtcNow,
            project = new
            {
                id = project.Id,
                name = project.Title,
                description = project.Description,
                currentStep = project.CurrentWizardStep,
                briefJson = project.BriefJson,
                planSpecJson = project.PlanSpecJson,
                voiceSpecJson = project.VoiceSpecJson,
                renderSpecJson = project.RenderSpecJson
            }
        };

        return JsonSerializer.Serialize(exportData, new JsonSerializerOptions
        {
            WriteIndented = true
        });
    }

    /// <summary>
    /// Import a wizard project from JSON
    /// </summary>
    public async Task<ProjectStateEntity> ImportProjectAsync(
        string projectJson,
        string? newName,
        CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(projectJson);

        var importData = JsonSerializer.Deserialize<JsonElement>(projectJson);
        var projectData = importData.GetProperty("project");

        var name = newName ?? projectData.GetProperty("name").GetString() ?? "Imported Project";
        var description = projectData.TryGetProperty("description", out var descProp) && descProp.ValueKind != JsonValueKind.Null
            ? descProp.GetString()
            : null;
        var currentStep = projectData.TryGetProperty("currentStep", out var stepProp)
            ? stepProp.GetInt32()
            : 0;

        var project = new ProjectStateEntity
        {
            Id = Guid.NewGuid(),
            Title = name,
            Description = description,
            CurrentWizardStep = currentStep,
            Status = "Draft",
            BriefJson = projectData.TryGetProperty("briefJson", out var briefProp) && briefProp.ValueKind != JsonValueKind.Null
                ? briefProp.GetString()
                : null,
            PlanSpecJson = projectData.TryGetProperty("planSpecJson", out var planProp) && planProp.ValueKind != JsonValueKind.Null
                ? planProp.GetString()
                : null,
            VoiceSpecJson = projectData.TryGetProperty("voiceSpecJson", out var voiceProp) && voiceProp.ValueKind != JsonValueKind.Null
                ? voiceProp.GetString()
                : null,
            RenderSpecJson = projectData.TryGetProperty("renderSpecJson", out var renderProp) && renderProp.ValueKind != JsonValueKind.Null
                ? renderProp.GetString()
                : null,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        await _repository.CreateAsync(project, ct);
        _logger.LogInformation("Imported wizard project as {ProjectId}: {ProjectName}", project.Id, project.Title);

        return project;
    }

    /// <summary>
    /// Generate a default project name based on timestamp
    /// </summary>
    public static string GenerateDefaultName()
    {
        return $"Project {DateTime.Now:yyyy-MM-dd HH:mm}";
    }
}
