using System;
using System.Collections.Generic;

namespace Aura.Core.Models;

public record Brief(string Topic, string? Audience, string? Goal, string Tone, string Language, Aspect Aspect);

public record PlanSpec(TimeSpan TargetDuration, Pacing Pacing, Density Density, string Style);

public record VoiceSpec(string VoiceName, double Rate, double Pitch, PauseStyle Pause);

public record Scene(int Index, string Heading, string Script, TimeSpan Start, TimeSpan Duration);

public record ScriptLine(int SceneIndex, string Text, TimeSpan Start, TimeSpan Duration);

public record Asset(string Kind, string PathOrUrl, string? License, string? Attribution);

public record Resolution(int Width, int Height);

public record RenderSpec(Resolution Res, string Container, int VideoBitrateK, int AudioBitrateK);

public record RenderProgress(float Percentage, TimeSpan Elapsed, TimeSpan Remaining, string CurrentStage);

public record SystemProfile
{
    public bool AutoDetect { get; init; } = true;
    public int LogicalCores { get; init; }
    public int PhysicalCores { get; init; }
    public int RamGB { get; init; }
    public GpuInfo? Gpu { get; init; }
    public HardwareTier Tier { get; init; }
    public bool EnableNVENC { get; init; }
    public bool EnableSD { get; init; }
    public bool OfflineOnly { get; init; }
    
    // Manual overrides (per spec: RAM 8-256 GB, cores 2-32+, GPU presets)
    public HardwareOverrides? Overrides { get; init; }
}

/// <summary>
/// Manual hardware overrides for users who want to customize detection results
/// Spec: RAM (8-256 GB), cores (2-32+), GPU presets (NVIDIA 50/40/30/20/16/10 series, AMD RX, Intel Arc)
/// </summary>
public record HardwareOverrides
{
    public int? ManualRamGB { get; init; }  // 8-256 GB
    public int? ManualLogicalCores { get; init; }  // 2-32+
    public int? ManualPhysicalCores { get; init; }  // 2-32+
    public string? ManualGpuPreset { get; init; }  // e.g., "NVIDIA RTX 3080", "AMD RX 6800", "Intel Arc A770"
    public bool? ForceEnableNVENC { get; init; }
    public bool? ForceEnableSD { get; init; }
    public bool? ForceOfflineMode { get; init; }
}

public record GpuInfo(string Vendor, string Model, int VramGB, string? Series);

/// <summary>
/// Persona of the target audience for content recommendations
/// </summary>
public record AudiencePersona(string? Name, string? Demographics, string? Interests, string? ExpertiseLevel);

/// <summary>
/// Constraints for plan recommendations (e.g., time limits, resource limits)
/// </summary>
public record PlanConstraints(
    TimeSpan? MaxDuration,
    TimeSpan? MinDuration,
    bool? MustBeOffline,
    string? PreferredLanguage
);

/// <summary>
/// Comprehensive recommendations for video production
/// </summary>
public record PlanRecommendations
{
    public string Outline { get; init; } = "";
    public int SceneCount { get; init; }
    public int ShotsPerScene { get; init; }
    public double BRollPercentage { get; init; }  // 0-100
    public double OverlayDensity { get; init; }  // 0-1
    public string ReadingLevel { get; init; } = "";
    public double VoiceRate { get; init; }  // 0.5-2.0
    public double VoicePitch { get; init; }  // -20 to +20
    public string MusicTempoCurve { get; init; } = "";
    public string MusicIntensityCurve { get; init; } = "";
    public string CaptionStyle { get; init; } = "";
    public string ThumbnailPrompt { get; init; } = "";
    public string SeoTitle { get; init; } = "";
    public string SeoDescription { get; init; } = "";
    public List<string> SeoTags { get; init; } = new();
}

/// <summary>
/// Input for generating recommendations
/// </summary>
public record RecommendationRequest(
    Brief Brief,
    PlanSpec PlanSpec,
    AudiencePersona? Persona,
    PlanConstraints? Constraints
);