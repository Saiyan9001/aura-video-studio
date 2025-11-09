using System;
using System.Collections.Generic;

namespace Aura.Core.Models.Generation;

/// <summary>
/// Represents a generated video script with scenes, metadata, and timing information
/// </summary>
public record Script
{
    /// <summary>
    /// Title of the script
    /// </summary>
    public string Title { get; init; } = string.Empty;

    /// <summary>
    /// List of scenes that compose the script
    /// </summary>
    public List<ScriptScene> Scenes { get; init; } = new();

    /// <summary>
    /// Total duration of the script
    /// </summary>
    public TimeSpan TotalDuration { get; init; }

    /// <summary>
    /// Metadata about script generation
    /// </summary>
    public ScriptMetadata Metadata { get; init; } = new();

    /// <summary>
    /// Correlation ID for tracking this script through the system
    /// </summary>
    public string CorrelationId { get; init; } = string.Empty;
}
