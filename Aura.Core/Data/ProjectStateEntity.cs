using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Aura.Core.Data;

/// <summary>
/// Entity representing a video generation project state for persistence and recovery
/// </summary>
public class ProjectStateEntity
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Required]
    [MaxLength(500)]
    public string Title { get; set; } = string.Empty;

    [MaxLength(2000)]
    public string? Description { get; set; }

    /// <summary>
    /// Current wizard step (0-based index, e.g., 0=Brief, 1=Plan, 2=Voice, 3=Generate)
    /// </summary>
    public int CurrentWizardStep { get; set; } = 0;

    [Required]
    public string Status { get; set; } = "InProgress"; // InProgress, Completed, Failed, Cancelled

    [Required]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [Required]
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? CompletedAt { get; set; }

    /// <summary>
    /// Soft-delete support: indicates if this project has been deleted
    /// </summary>
    public bool IsDeleted { get; set; } = false;

    /// <summary>
    /// Soft-delete support: when the project was deleted (null if not deleted)
    /// </summary>
    public DateTime? DeletedAt { get; set; }

    /// <summary>
    /// Soft-delete support: user who deleted the project
    /// </summary>
    [MaxLength(200)]
    public string? DeletedByUserId { get; set; }

    /// <summary>
    /// Current stage in pipeline (Script, TTS, Images, Composition, Render)
    /// </summary>
    [MaxLength(100)]
    public string? CurrentStage { get; set; }

    /// <summary>
    /// Overall progress percentage (0-100)
    /// </summary>
    public int ProgressPercent { get; set; } = 0;

    /// <summary>
    /// Associated job ID if running
    /// </summary>
    [MaxLength(100)]
    public string? JobId { get; set; }

    /// <summary>
    /// JSON serialized Brief
    /// </summary>
    [Column(TypeName = "TEXT")]
    public string? BriefJson { get; set; }

    /// <summary>
    /// JSON serialized PlanSpec
    /// </summary>
    [Column(TypeName = "TEXT")]
    public string? PlanSpecJson { get; set; }

    /// <summary>
    /// JSON serialized VoiceSpec
    /// </summary>
    [Column(TypeName = "TEXT")]
    public string? VoiceSpecJson { get; set; }

    /// <summary>
    /// JSON serialized RenderSpec
    /// </summary>
    [Column(TypeName = "TEXT")]
    public string? RenderSpecJson { get; set; }

    /// <summary>
    /// Last error message if failed
    /// </summary>
    [Column(TypeName = "TEXT")]
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// Navigation property for scenes
    /// </summary>
    public ICollection<SceneStateEntity> Scenes { get; set; } = new List<SceneStateEntity>();

    /// <summary>
    /// Navigation property for assets
    /// </summary>
    public ICollection<AssetStateEntity> Assets { get; set; } = new List<AssetStateEntity>();

    /// <summary>
    /// Navigation property for checkpoints
    /// </summary>
    public ICollection<RenderCheckpointEntity> Checkpoints { get; set; } = new List<RenderCheckpointEntity>();
}

/// <summary>
/// Entity representing a scene within a project
/// </summary>
public class SceneStateEntity
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Required]
    public Guid ProjectId { get; set; }

    [ForeignKey(nameof(ProjectId))]
    public ProjectStateEntity Project { get; set; } = null!;

    [Required]
    public int SceneIndex { get; set; }

    [Required]
    [Column(TypeName = "TEXT")]
    public string ScriptText { get; set; } = string.Empty;

    [MaxLength(500)]
    public string? AudioFilePath { get; set; }

    [MaxLength(500)]
    public string? ImageFilePath { get; set; }

    public double DurationSeconds { get; set; }

    public bool IsCompleted { get; set; } = false;

    [Required]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Entity representing an asset (audio, image, video file) in a project
/// </summary>
public class AssetStateEntity
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Required]
    public Guid ProjectId { get; set; }

    [ForeignKey(nameof(ProjectId))]
    public ProjectStateEntity Project { get; set; } = null!;

    [Required]
    [MaxLength(100)]
    public string AssetType { get; set; } = string.Empty; // Audio, Image, Video, Subtitle

    [Required]
    [MaxLength(500)]
    public string FilePath { get; set; } = string.Empty;

    public long FileSizeBytes { get; set; }

    [MaxLength(200)]
    public string? MimeType { get; set; }

    public bool IsTemporary { get; set; } = true;

    [Required]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Entity representing a checkpoint during video rendering or generation
/// </summary>
public class RenderCheckpointEntity
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Required]
    public Guid ProjectId { get; set; }

    [ForeignKey(nameof(ProjectId))]
    public ProjectStateEntity Project { get; set; } = null!;

    [Required]
    [MaxLength(100)]
    public string StageName { get; set; } = string.Empty; // Script, TTS, Images, Composition, Render

    [Required]
    public DateTime CheckpointTime { get; set; } = DateTime.UtcNow;

    public int CompletedScenes { get; set; } = 0;

    public int TotalScenes { get; set; } = 0;

    /// <summary>
    /// JSON serialized checkpoint data (file paths, progress details, etc.)
    /// </summary>
    [Column(TypeName = "TEXT")]
    public string? CheckpointData { get; set; }

    [MaxLength(500)]
    public string? OutputFilePath { get; set; }

    public bool IsValid { get; set; } = true;
}
