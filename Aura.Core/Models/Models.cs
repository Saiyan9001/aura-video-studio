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
/// Input for requesting planner recommendations
/// </summary>
public record RecommendationRequest(
    Brief Brief,
    PlanSpec PlanSpec,
    string? AudiencePersona,
    PlannerConstraints? Constraints
);

/// <summary>
/// Constraints for recommendation generation
/// </summary>
public record PlannerConstraints(
    int? MaxSceneCount,
    int? MinSceneCount,
    double? MaxBRollPercentage,
    double? MinBRollPercentage,
    bool? PreferHighQuality
);

/// <summary>
/// Comprehensive recommendations from the planner
/// </summary>
public record PlannerRecommendations(
    string Outline,
    int SceneCount,
    int ShotsPerScene,
    double BRollPercentage,
    double OverlayDensity,
    string ReadingLevel,
    VoiceRecommendations Voice,
    MusicRecommendations Music,
    CaptionRecommendations Captions,
    string ThumbnailPrompt,
    SeoRecommendations Seo
);

/// <summary>
/// Voice rate and pitch recommendations
/// </summary>
public record VoiceRecommendations(
    double Rate,
    double Pitch
);

/// <summary>
/// Music tempo and intensity curve recommendations
/// </summary>
public record MusicRecommendations(
    int Tempo,
    string IntensityCurve
);

/// <summary>
/// Caption style recommendations
/// </summary>
public record CaptionRecommendations(
    string Style,
    string Position,
    int FontSize,
    string Color
);

/// <summary>
/// SEO recommendations including title, description, and tags
/// </summary>
public record SeoRecommendations(
    string Title,
    string Description,
    string[] Tags
);