using System;

namespace Aura.Core.Data;

/// <summary>
/// Database entity for storing application configuration
/// </summary>
public class ConfigurationEntity
{
    /// <summary>
    /// Configuration key (unique identifier)
    /// </summary>
    public string Key { get; set; } = string.Empty;

    /// <summary>
    /// Configuration value (stored as JSON)
    /// </summary>
    public string Value { get; set; } = string.Empty;

    /// <summary>
    /// Configuration category for grouping
    /// </summary>
    public string Category { get; set; } = string.Empty;

    /// <summary>
    /// Data type of the value (string, json, boolean, number)
    /// </summary>
    public string ValueType { get; set; } = "string";

    /// <summary>
    /// Description of what this configuration does
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// Whether this configuration is sensitive (API keys, etc.)
    /// </summary>
    public bool IsSensitive { get; set; } = false;

    /// <summary>
    /// Version of the configuration schema
    /// </summary>
    public int Version { get; set; } = 1;

    /// <summary>
    /// When the configuration was created
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// When the configuration was last updated
    /// </summary>
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// User or system that last modified this configuration
    /// </summary>
    public string? ModifiedBy { get; set; }

    /// <summary>
    /// Whether this configuration is active
    /// </summary>
    public bool IsActive { get; set; } = true;
}
