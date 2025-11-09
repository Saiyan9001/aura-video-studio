using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Aura.Core.Data;

/// <summary>
/// Database entity for system-wide configuration and setup status
/// </summary>
[Table("system_configuration")]
public class SystemConfigurationEntity
{
    /// <summary>
    /// Primary key (always 1 - single record table)
    /// </summary>
    [Key]
    [Column("id")]
    public int Id { get; set; } = 1;

    /// <summary>
    /// Whether the initial setup wizard has been completed
    /// </summary>
    [Required]
    [Column("is_setup_complete")]
    public bool IsSetupComplete { get; set; } = false;

    /// <summary>
    /// Path to FFmpeg executable (null if not configured or using system PATH)
    /// </summary>
    [Column("ffmpeg_path")]
    [MaxLength(500)]
    public string? FFmpegPath { get; set; }

    /// <summary>
    /// Default output directory for generated videos
    /// </summary>
    [Column("output_directory")]
    [MaxLength(500)]
    public string OutputDirectory { get; set; } = GetDefaultOutputDirectory();

    /// <summary>
    /// When this configuration was created
    /// </summary>
    [Required]
    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// When this configuration was last updated
    /// </summary>
    [Required]
    [Column("updated_at")]
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Gets the default output directory path
    /// </summary>
    private static string GetDefaultOutputDirectory()
    {
        var homeDir = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        return Path.Combine(homeDir, "AuraVideoStudio", "Output");
    }
}
