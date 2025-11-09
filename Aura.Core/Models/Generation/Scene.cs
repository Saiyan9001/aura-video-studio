using System;
using System.Collections.Generic;

namespace Aura.Core.Models.Generation;

/// <summary>
/// Represents a single scene within a generated script
/// </summary>
public record ScriptScene
{
    /// <summary>
    /// Scene number (1-based)
    /// </summary>
    public int Number { get; init; }

    /// <summary>
    /// Narration text for the scene
    /// </summary>
    public string Narration { get; init; } = string.Empty;

    /// <summary>
    /// Visual prompt for generating or selecting visuals
    /// </summary>
    public string VisualPrompt { get; init; } = string.Empty;

    /// <summary>
    /// Duration of this scene
    /// </summary>
    public TimeSpan Duration { get; init; }

    /// <summary>
    /// Transition type to next scene
    /// </summary>
    public TransitionType Transition { get; init; }

    /// <summary>
    /// Extended data for scene customization and provider-specific information
    /// </summary>
    public Dictionary<string, object> ExtendedData { get; init; } = new();
}

/// <summary>
/// Types of transitions between scenes
/// </summary>
public enum TransitionType
{
    /// <summary>
    /// Direct cut between scenes
    /// </summary>
    Cut,

    /// <summary>
    /// Fade to black then to next scene
    /// </summary>
    Fade,

    /// <summary>
    /// Dissolve from one scene to another
    /// </summary>
    Dissolve,

    /// <summary>
    /// Wipe transition
    /// </summary>
    Wipe,

    /// <summary>
    /// Slide transition
    /// </summary>
    Slide,

    /// <summary>
    /// Zoom transition
    /// </summary>
    Zoom
}
