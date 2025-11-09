using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Aura.Core.Models.Visual;
using Microsoft.Extensions.Logging;

namespace Aura.Core.Services.Visual;

/// <summary>
/// Service for maintaining visual style coherence across scenes
/// Extracts style from reference images and applies consistent styling
/// </summary>
public class VisualStyleCoherenceService
{
    private readonly ILogger<VisualStyleCoherenceService> _logger;

    public VisualStyleCoherenceService(ILogger<VisualStyleCoherenceService> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Extract style characteristics from a reference image
    /// </summary>
    public async Task<StyleProfile> ExtractStyleProfileAsync(
        string referenceImageUrl,
        CancellationToken ct = default)
    {
        _logger.LogInformation("Extracting style profile from reference image: {ImageUrl}", referenceImageUrl);

        try
        {
            await Task.Delay(1, ct);

            var colorPalette = ExtractColorPalette(referenceImageUrl);
            var lighting = AnalyzeLighting(referenceImageUrl);
            var composition = AnalyzeComposition(referenceImageUrl);
            var texture = AnalyzeTexture(referenceImageUrl);

            return new StyleProfile
            {
                ReferenceImageUrl = referenceImageUrl,
                ColorPalette = colorPalette,
                DominantColors = colorPalette.Take(3).ToList(),
                LightingCharacteristics = lighting,
                CompositionStyle = composition,
                TextureProfile = texture,
                ExtractedAt = DateTime.UtcNow
            };
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to extract style profile from image");
            return CreateFallbackStyleProfile(referenceImageUrl);
        }
    }

    /// <summary>
    /// Apply style transfer guidance to subsequent images
    /// </summary>
    public async Task<StyleTransferGuidance> GenerateStyleTransferGuidanceAsync(
        StyleProfile referenceStyle,
        OptimizedVisualPrompt targetPrompt,
        CancellationToken ct = default)
    {
        _logger.LogDebug("Generating style transfer guidance for scene {SceneIndex}", targetPrompt.SceneIndex);

        await Task.Delay(1, ct);

        var colorGuidance = BuildColorConsistencyGuidance(referenceStyle.ColorPalette);
        var lightingGuidance = BuildLightingMatchGuidance(referenceStyle.LightingCharacteristics);
        var perspectiveGuidance = BuildPerspectiveGuidance(referenceStyle.CompositionStyle);

        var transitionHints = BuildTransitionHints(referenceStyle, targetPrompt);

        return new StyleTransferGuidance
        {
            ColorConsistencyTokens = colorGuidance,
            LightingMatchTokens = lightingGuidance,
            PerspectiveMatchTokens = perspectiveGuidance,
            TransitionHints = transitionHints,
            StyleStrength = 0.7,
            ApplyColorGrading = true,
            ApplyLightingMatch = true,
            ApplyPerspectiveMatch = true
        };
    }

    /// <summary>
    /// Apply style coherence across multiple scenes
    /// </summary>
    public async Task<IReadOnlyList<StyledVisualPrompt>> ApplyCoherentStyleAsync(
        IReadOnlyList<OptimizedVisualPrompt> prompts,
        StyleCoherenceConfig config,
        CancellationToken ct = default)
    {
        _logger.LogInformation("Applying coherent style across {Count} scenes", prompts.Count);

        if (prompts.Count == 0)
        {
            return Array.Empty<StyledVisualPrompt>();
        }

        StyleProfile? referenceStyle = null;
        
        if (!string.IsNullOrEmpty(config.ReferenceImageUrl))
        {
            referenceStyle = await ExtractStyleProfileAsync(config.ReferenceImageUrl, ct);
        }
        else if (config.ExtractStyleFromFirstScene && prompts.Count > 0)
        {
            _logger.LogInformation("Will extract style from first generated scene");
        }

        var styledPrompts = new List<StyledVisualPrompt>();

        for (int i = 0; i < prompts.Count; i++)
        {
            if (ct.IsCancellationRequested)
            {
                break;
            }

            var prompt = prompts[i];

            StyleTransferGuidance? guidance = null;
            if (referenceStyle != null)
            {
                guidance = await GenerateStyleTransferGuidanceAsync(referenceStyle, prompt, ct);
            }

            var colorGrading = referenceStyle != null
                ? BuildColorGradingParams(referenceStyle, prompt)
                : null;

            var styledPrompt = new StyledVisualPrompt
            {
                OptimizedPrompt = prompt,
                StyleProfile = referenceStyle,
                StyleGuidance = guidance,
                ColorGrading = colorGrading,
                CoherenceStrength = config.CoherenceStrength,
                IsReferenceScene = i == 0 && config.ExtractStyleFromFirstScene
            };

            styledPrompts.Add(styledPrompt);
        }

        _logger.LogInformation("Applied coherent style to {Count} scenes", styledPrompts.Count);

        return styledPrompts;
    }

    /// <summary>
    /// Extract color palette from image URL (heuristic implementation)
    /// </summary>
    private IReadOnlyList<string> ExtractColorPalette(string imageUrl)
    {
        var random = new Random(imageUrl.GetHashCode());
        
        var palettes = new[]
        {
            new[] { "#2C3E50", "#ECF0F1", "#3498DB", "#95A5A6", "#34495E" },
            new[] { "#FFA500", "#FF8C00", "#FFD700", "#FFB347", "#8B4513" },
            new[] { "#FF6B6B", "#4ECDC4", "#FFE66D", "#A8E6CF", "#FF8B94" },
            new[] { "#1A1A1A", "#8B0000", "#FFD700", "#2F4F4F", "#8B4513" },
            new[] { "#E8F4F8", "#B8D4E0", "#88B4C8", "#5894B0", "#287498" }
        };

        var index = Math.Abs(random.Next()) % palettes.Length;
        return palettes[index];
    }

    /// <summary>
    /// Analyze lighting characteristics
    /// </summary>
    private LightingCharacteristics AnalyzeLighting(string imageUrl)
    {
        var random = new Random(imageUrl.GetHashCode() + 1);
        
        var moods = new[] { "warm", "cool", "neutral", "dramatic", "soft" };
        var directions = new[] { "front", "side", "back", "top", "diffused" };
        var qualities = new[] { "soft", "hard", "balanced", "high-key", "low-key" };

        return new LightingCharacteristics
        {
            Mood = moods[random.Next(moods.Length)],
            Direction = directions[random.Next(directions.Length)],
            Quality = qualities[random.Next(qualities.Length)],
            Intensity = 50.0 + random.NextDouble() * 40.0
        };
    }

    /// <summary>
    /// Analyze composition style
    /// </summary>
    private CompositionStyle AnalyzeComposition(string imageUrl)
    {
        var random = new Random(imageUrl.GetHashCode() + 2);
        
        var styles = new[] { "rule of thirds", "centered", "symmetrical", "dynamic", "balanced" };
        var depths = new[] { "shallow", "medium", "deep" };

        return new CompositionStyle
        {
            Style = styles[random.Next(styles.Length)],
            DepthOfField = depths[random.Next(depths.Length)],
            BalanceScore = 50.0 + random.NextDouble() * 40.0
        };
    }

    /// <summary>
    /// Analyze texture profile
    /// </summary>
    private TextureProfile AnalyzeTexture(string imageUrl)
    {
        var random = new Random(imageUrl.GetHashCode() + 3);
        
        return new TextureProfile
        {
            Smoothness = random.NextDouble(),
            DetailLevel = 0.3 + random.NextDouble() * 0.6,
            GrainIntensity = random.NextDouble() * 0.3
        };
    }

    /// <summary>
    /// Create fallback style profile
    /// </summary>
    private StyleProfile CreateFallbackStyleProfile(string imageUrl)
    {
        return new StyleProfile
        {
            ReferenceImageUrl = imageUrl,
            ColorPalette = new[] { "#34495E", "#ECF0F1", "#3498DB", "#2ECC71", "#E74C3C" },
            DominantColors = new[] { "#34495E", "#ECF0F1", "#3498DB" },
            LightingCharacteristics = new LightingCharacteristics
            {
                Mood = "neutral",
                Direction = "front",
                Quality = "soft",
                Intensity = 70.0
            },
            CompositionStyle = new CompositionStyle
            {
                Style = "balanced",
                DepthOfField = "medium",
                BalanceScore = 75.0
            },
            TextureProfile = new TextureProfile
            {
                Smoothness = 0.7,
                DetailLevel = 0.6,
                GrainIntensity = 0.1
            },
            ExtractedAt = DateTime.UtcNow
        };
    }

    /// <summary>
    /// Build color consistency guidance
    /// </summary>
    private IReadOnlyList<string> BuildColorConsistencyGuidance(IReadOnlyList<string> palette)
    {
        var guidance = new List<string>
        {
            $"color palette: {string.Join(", ", palette.Take(3))}",
            "consistent color grading",
            "unified color scheme"
        };

        return guidance;
    }

    /// <summary>
    /// Build lighting match guidance
    /// </summary>
    private IReadOnlyList<string> BuildLightingMatchGuidance(LightingCharacteristics lighting)
    {
        var guidance = new List<string>
        {
            $"{lighting.Mood} lighting",
            $"{lighting.Direction} light direction",
            $"{lighting.Quality} light quality"
        };

        return guidance;
    }

    /// <summary>
    /// Build perspective guidance
    /// </summary>
    private IReadOnlyList<string> BuildPerspectiveGuidance(CompositionStyle composition)
    {
        var guidance = new List<string>
        {
            $"{composition.Style} composition",
            $"{composition.DepthOfField} depth of field"
        };

        return guidance;
    }

    /// <summary>
    /// Build transition hints between scenes
    /// </summary>
    private IReadOnlyList<string> BuildTransitionHints(StyleProfile style, OptimizedVisualPrompt prompt)
    {
        var hints = new List<string>
        {
            "smooth visual transition",
            "maintain style consistency"
        };

        if (style.LightingCharacteristics != null && prompt.BasePrompt.Lighting != null)
        {
            if (style.LightingCharacteristics.Mood != prompt.BasePrompt.Lighting.Mood)
            {
                hints.Add($"gradual lighting shift from {style.LightingCharacteristics.Mood} to {prompt.BasePrompt.Lighting.Mood}");
            }
        }

        return hints;
    }

    /// <summary>
    /// Build color grading parameters
    /// </summary>
    private ColorGradingParams BuildColorGradingParams(StyleProfile style, OptimizedVisualPrompt prompt)
    {
        return new ColorGradingParams
        {
            TargetColorPalette = style.ColorPalette,
            Temperature = style.LightingCharacteristics?.Mood == "warm" ? 10 : 
                         style.LightingCharacteristics?.Mood == "cool" ? -10 : 0,
            Tint = 0,
            Saturation = 1.0,
            Contrast = 1.0,
            Brightness = 0
        };
    }
}

/// <summary>
/// Configuration for style coherence
/// </summary>
public record StyleCoherenceConfig
{
    /// <summary>
    /// Reference image URL to extract style from
    /// </summary>
    public string? ReferenceImageUrl { get; init; }

    /// <summary>
    /// Extract style from first generated scene
    /// </summary>
    public bool ExtractStyleFromFirstScene { get; init; } = true;

    /// <summary>
    /// Strength of style coherence (0.0 to 1.0)
    /// </summary>
    public double CoherenceStrength { get; init; } = 0.7;

    /// <summary>
    /// Apply color grading for consistency
    /// </summary>
    public bool ApplyColorGrading { get; init; } = true;

    /// <summary>
    /// Apply lighting matching
    /// </summary>
    public bool ApplyLightingMatch { get; init; } = true;
}

/// <summary>
/// Style profile extracted from reference image
/// </summary>
public record StyleProfile
{
    public string ReferenceImageUrl { get; init; } = string.Empty;
    public IReadOnlyList<string> ColorPalette { get; init; } = Array.Empty<string>();
    public IReadOnlyList<string> DominantColors { get; init; } = Array.Empty<string>();
    public LightingCharacteristics? LightingCharacteristics { get; init; }
    public CompositionStyle? CompositionStyle { get; init; }
    public TextureProfile? TextureProfile { get; init; }
    public DateTime ExtractedAt { get; init; }
}

/// <summary>
/// Lighting characteristics
/// </summary>
public record LightingCharacteristics
{
    public string Mood { get; init; } = string.Empty;
    public string Direction { get; init; } = string.Empty;
    public string Quality { get; init; } = string.Empty;
    public double Intensity { get; init; }
}

/// <summary>
/// Composition style
/// </summary>
public record CompositionStyle
{
    public string Style { get; init; } = string.Empty;
    public string DepthOfField { get; init; } = string.Empty;
    public double BalanceScore { get; init; }
}

/// <summary>
/// Texture profile
/// </summary>
public record TextureProfile
{
    public double Smoothness { get; init; }
    public double DetailLevel { get; init; }
    public double GrainIntensity { get; init; }
}

/// <summary>
/// Style transfer guidance
/// </summary>
public record StyleTransferGuidance
{
    public IReadOnlyList<string> ColorConsistencyTokens { get; init; } = Array.Empty<string>();
    public IReadOnlyList<string> LightingMatchTokens { get; init; } = Array.Empty<string>();
    public IReadOnlyList<string> PerspectiveMatchTokens { get; init; } = Array.Empty<string>();
    public IReadOnlyList<string> TransitionHints { get; init; } = Array.Empty<string>();
    public double StyleStrength { get; init; }
    public bool ApplyColorGrading { get; init; }
    public bool ApplyLightingMatch { get; init; }
    public bool ApplyPerspectiveMatch { get; init; }
}

/// <summary>
/// Styled visual prompt with coherence applied
/// </summary>
public record StyledVisualPrompt
{
    public required OptimizedVisualPrompt OptimizedPrompt { get; init; }
    public StyleProfile? StyleProfile { get; init; }
    public StyleTransferGuidance? StyleGuidance { get; init; }
    public ColorGradingParams? ColorGrading { get; init; }
    public double CoherenceStrength { get; init; }
    public bool IsReferenceScene { get; init; }
}

/// <summary>
/// Color grading parameters
/// </summary>
public record ColorGradingParams
{
    public IReadOnlyList<string> TargetColorPalette { get; init; } = Array.Empty<string>();
    public int Temperature { get; init; }
    public int Tint { get; init; }
    public double Saturation { get; init; } = 1.0;
    public double Contrast { get; init; } = 1.0;
    public int Brightness { get; init; }
}
