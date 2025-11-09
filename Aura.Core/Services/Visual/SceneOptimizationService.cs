using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Aura.Core.Models;
using Aura.Core.Models.Visual;
using Microsoft.Extensions.Logging;

namespace Aura.Core.Services.Visual;

/// <summary>
/// Service for optimizing visual generation with scene-level intelligence
/// Enhances prompt generation with aspect ratio optimization, continuity, and quality checks
/// </summary>
public class SceneOptimizationService
{
    private readonly ILogger<SceneOptimizationService> _logger;
    private readonly VisualPromptGenerationService _promptService;

    public SceneOptimizationService(
        ILogger<SceneOptimizationService> logger,
        VisualPromptGenerationService promptService)
    {
        _logger = logger;
        _promptService = promptService;
    }

    /// <summary>
    /// Optimize visual prompts for a set of scenes with aspect ratio and continuity enhancements
    /// </summary>
    public async Task<IReadOnlyList<OptimizedVisualPrompt>> OptimizeScenePromptsAsync(
        IReadOnlyList<Scene> scenes,
        Brief brief,
        SceneOptimizationConfig config,
        CancellationToken ct = default)
    {
        _logger.LogInformation("Optimizing visual prompts for {SceneCount} scenes with aspect ratio {AspectRatio}",
            scenes.Count, config.AspectRatio);

        var basePrompts = await _promptService.GenerateVisualPromptsAsync(
            scenes,
            brief,
            config.LlmProvider,
            config.PacingData,
            ct);

        var optimizedPrompts = new List<OptimizedVisualPrompt>();
        OptimizedVisualPrompt? previousPrompt = null;

        for (int i = 0; i < basePrompts.Count; i++)
        {
            var basePrompt = basePrompts[i];
            var scene = scenes[i];

            var optimized = await OptimizeSingleSceneAsync(
                basePrompt,
                scene,
                previousPrompt,
                config,
                ct);

            optimizedPrompts.Add(optimized);
            previousPrompt = optimized;
        }

        var coherenceScore = CalculateOverallCoherence(optimizedPrompts);
        _logger.LogInformation("Scene optimization complete. Overall coherence score: {Score:F2}", coherenceScore);

        return optimizedPrompts;
    }

    /// <summary>
    /// Optimize a single scene prompt with aspect ratio, negative prompts, and continuity
    /// </summary>
    private async Task<OptimizedVisualPrompt> OptimizeSingleSceneAsync(
        VisualPrompt basePrompt,
        Scene scene,
        OptimizedVisualPrompt? previousPrompt,
        SceneOptimizationConfig config,
        CancellationToken ct)
    {
        var aspectRatioData = GetAspectRatioOptimization(config.AspectRatio);
        
        var enhancedNegativePrompts = BuildEnhancedNegativePrompts(
            basePrompt.NegativeElements,
            aspectRatioData,
            config.ContentSafetyLevel);

        var continuityHints = BuildContinuityHints(
            basePrompt,
            previousPrompt,
            config.ContinuityStrength);

        var styleConsistencyTokens = previousPrompt != null
            ? ExtractStyleTokens(previousPrompt)
            : new List<string>();

        var optimizedDescription = EnhanceDescriptionForAspectRatio(
            basePrompt.DetailedDescription,
            aspectRatioData,
            basePrompt.Camera.ShotType);

        return new OptimizedVisualPrompt
        {
            BasePrompt = basePrompt,
            SceneIndex = basePrompt.SceneIndex,
            AspectRatio = config.AspectRatio,
            AspectRatioData = aspectRatioData,
            EnhancedNegativePrompts = enhancedNegativePrompts,
            ContinuityHints = continuityHints,
            StyleConsistencyTokens = styleConsistencyTokens,
            OptimizedDescription = optimizedDescription,
            ContentSafetyLevel = config.ContentSafetyLevel,
            GenerationVariations = config.VariationsPerScene,
            QualityCheckEnabled = config.EnableQualityChecks,
            CoherenceScore = CalculateSceneCoherence(basePrompt, previousPrompt?.BasePrompt)
        };
    }

    /// <summary>
    /// Get aspect ratio optimization data for video format
    /// </summary>
    private AspectRatioOptimization GetAspectRatioOptimization(string aspectRatio)
    {
        return aspectRatio.ToLowerInvariant() switch
        {
            "16:9" or "widescreen" => new AspectRatioOptimization
            {
                Ratio = "16:9",
                Width = 1920,
                Height = 1080,
                Orientation = "landscape",
                CompositionGuidance = "Use horizontal rule of thirds. Emphasize width for landscapes and environments. Place subjects in left or right thirds.",
                OptimalShotTypes = new[] { ShotType.WideShot, ShotType.ExtremeWideShot, ShotType.MediumShot },
                FramingAdjustments = "Allow more horizontal space. Avoid tall vertical subjects that don't utilize width."
            },
            "9:16" or "portrait" or "vertical" => new AspectRatioOptimization
            {
                Ratio = "9:16",
                Width = 1080,
                Height = 1920,
                Orientation = "portrait",
                CompositionGuidance = "Use vertical rule of thirds. Emphasize height. Ideal for full-body shots and portraits. Place subjects in upper or lower thirds.",
                OptimalShotTypes = new[] { ShotType.FullShot, ShotType.CloseUp, ShotType.MediumCloseUp },
                FramingAdjustments = "Allow more vertical space. Perfect for standing subjects and vertical architecture."
            },
            "1:1" or "square" => new AspectRatioOptimization
            {
                Ratio = "1:1",
                Width = 1080,
                Height = 1080,
                Orientation = "square",
                CompositionGuidance = "Use centered or symmetrical composition. Equal space in all directions. Central framing works well.",
                OptimalShotTypes = new[] { ShotType.MediumShot, ShotType.CloseUp, ShotType.MediumCloseUp },
                FramingAdjustments = "Balanced framing. Works well for centered subjects and symmetrical compositions."
            },
            "4:3" or "standard" => new AspectRatioOptimization
            {
                Ratio = "4:3",
                Width = 1024,
                Height = 768,
                Orientation = "landscape",
                CompositionGuidance = "Classic composition. Slightly wider than tall. Good balance for most subjects.",
                OptimalShotTypes = new[] { ShotType.MediumShot, ShotType.WideShot, ShotType.CloseUp },
                FramingAdjustments = "Traditional framing with moderate width advantage."
            },
            _ => new AspectRatioOptimization
            {
                Ratio = "16:9",
                Width = 1920,
                Height = 1080,
                Orientation = "landscape",
                CompositionGuidance = "Default widescreen composition.",
                OptimalShotTypes = new[] { ShotType.MediumShot },
                FramingAdjustments = "Standard widescreen framing."
            }
        };
    }

    /// <summary>
    /// Build enhanced negative prompts to avoid common issues
    /// </summary>
    private IReadOnlyList<string> BuildEnhancedNegativePrompts(
        IReadOnlyList<string> baseNegatives,
        AspectRatioOptimization aspectRatio,
        ContentSafetyLevel safetyLevel)
    {
        var negatives = new List<string>(baseNegatives);

        var commonIssues = new[]
        {
            "blurry", "out of focus", "low quality", "distorted", "deformed",
            "watermark", "text overlay", "logo", "signature", "username",
            "oversaturated", "undersaturated", "noisy", "grainy", "pixelated",
            "artifacts", "jpeg artifacts", "compression artifacts",
            "duplicated", "multiple", "cropped badly", "cut off",
            "malformed", "extra limbs", "missing limbs", "fused fingers",
            "bad anatomy", "bad proportions", "asymmetric", "unbalanced"
        };
        negatives.AddRange(commonIssues);

        if (aspectRatio.Orientation == "portrait")
        {
            negatives.Add("horizontal crop");
            negatives.Add("wide landscape");
            negatives.Add("letterboxed");
        }
        else if (aspectRatio.Orientation == "landscape")
        {
            negatives.Add("vertical crop");
            negatives.Add("tall portrait");
            negatives.Add("pillarboxed");
        }

        if (safetyLevel >= ContentSafetyLevel.Moderate)
        {
            var safetyNegatives = new[]
            {
                "nsfw", "explicit", "nudity", "violence", "gore", "blood",
                "weapons", "drugs", "alcohol", "smoking", "offensive"
            };
            negatives.AddRange(safetyNegatives);
        }

        if (safetyLevel >= ContentSafetyLevel.Strict)
        {
            var strictNegatives = new[]
            {
                "suggestive", "revealing", "inappropriate", "controversial",
                "political symbols", "religious symbols", "hate symbols"
            };
            negatives.AddRange(strictNegatives);
        }

        return negatives.Distinct().ToList();
    }

    /// <summary>
    /// Build continuity hints between consecutive scenes
    /// </summary>
    private IReadOnlyList<string> BuildContinuityHints(
        VisualPrompt currentPrompt,
        OptimizedVisualPrompt? previousPrompt,
        double continuityStrength)
    {
        if (previousPrompt == null || continuityStrength <= 0)
        {
            return Array.Empty<string>();
        }

        var hints = new List<string>();

        if (previousPrompt.BasePrompt.Continuity != null)
        {
            if (previousPrompt.BasePrompt.Continuity.CharacterAppearance.Count > 0 && continuityStrength >= 0.5)
            {
                hints.Add($"Maintain character appearance: {string.Join(", ", previousPrompt.BasePrompt.Continuity.CharacterAppearance)}");
            }

            if (previousPrompt.BasePrompt.Continuity.LocationDetails.Count > 0 && continuityStrength >= 0.5)
            {
                hints.Add($"Keep location consistent: {string.Join(", ", previousPrompt.BasePrompt.Continuity.LocationDetails)}");
            }

            if (previousPrompt.BasePrompt.Continuity.ColorGrading.Count > 0 && continuityStrength >= 0.7)
            {
                hints.Add($"Maintain color grading: {string.Join(", ", previousPrompt.BasePrompt.Continuity.ColorGrading)}");
            }

            if (!string.IsNullOrEmpty(previousPrompt.BasePrompt.Continuity.TimeProgression))
            {
                hints.Add($"Time continuity: {previousPrompt.BasePrompt.Continuity.TimeProgression}");
            }
        }

        if (previousPrompt.BasePrompt.Lighting != null && currentPrompt.Lighting != null)
        {
            if (previousPrompt.BasePrompt.Lighting.TimeOfDay == currentPrompt.Lighting.TimeOfDay && continuityStrength >= 0.6)
            {
                hints.Add($"Match lighting: {currentPrompt.Lighting.TimeOfDay}, {currentPrompt.Lighting.Quality}");
            }
        }

        if (previousPrompt.StyleConsistencyTokens.Count > 0 && continuityStrength >= 0.8)
        {
            hints.Add($"Style consistency: {string.Join(", ", previousPrompt.StyleConsistencyTokens.Take(3))}");
        }

        return hints;
    }

    /// <summary>
    /// Extract style consistency tokens from a previous prompt
    /// </summary>
    private IReadOnlyList<string> ExtractStyleTokens(OptimizedVisualPrompt previousPrompt)
    {
        var tokens = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        if (previousPrompt.BasePrompt.ColorPalette.Count > 0)
        {
            tokens.Add($"color palette: {string.Join(", ", previousPrompt.BasePrompt.ColorPalette.Take(3))}");
        }

        if (previousPrompt.BasePrompt.Lighting != null)
        {
            tokens.Add($"{previousPrompt.BasePrompt.Lighting.Mood} lighting");
            tokens.Add($"{previousPrompt.BasePrompt.Lighting.Quality} light quality");
        }

        if (previousPrompt.BasePrompt.StyleKeywords.Count > 0)
        {
            foreach (var keyword in previousPrompt.BasePrompt.StyleKeywords.Take(3))
            {
                if (!keyword.Contains("quality") && !keyword.Contains("detailed"))
                {
                    tokens.Add(keyword);
                }
            }
        }

        tokens.Add($"{previousPrompt.BasePrompt.Style.ToString().ToLowerInvariant()} style");

        return tokens.ToList();
    }

    /// <summary>
    /// Enhance description with aspect ratio-specific composition guidance
    /// </summary>
    private string EnhanceDescriptionForAspectRatio(
        string baseDescription,
        AspectRatioOptimization aspectRatio,
        ShotType shotType)
    {
        var isOptimalShot = aspectRatio.OptimalShotTypes.Contains(shotType);
        
        var enhanced = baseDescription;

        if (!isOptimalShot)
        {
            enhanced += $" {aspectRatio.FramingAdjustments}";
        }

        enhanced += $" {aspectRatio.CompositionGuidance}";

        return enhanced.Trim();
    }

    /// <summary>
    /// Calculate coherence between two consecutive scenes
    /// </summary>
    private double CalculateSceneCoherence(VisualPrompt current, VisualPrompt? previous)
    {
        if (previous == null)
        {
            return 100.0;
        }

        var score = 0.0;
        var factors = 0;

        if (current.Style == previous.Style)
        {
            score += 20.0;
        }
        factors++;

        if (current.Lighting?.TimeOfDay == previous.Lighting?.TimeOfDay)
        {
            score += 15.0;
        }
        factors++;

        var colorOverlap = current.ColorPalette.Intersect(previous.ColorPalette).Count();
        if (colorOverlap > 0)
        {
            score += Math.Min(15.0, colorOverlap * 5.0);
        }
        factors++;

        var keywordOverlap = current.StyleKeywords.Intersect(previous.StyleKeywords, StringComparer.OrdinalIgnoreCase).Count();
        if (keywordOverlap > 0)
        {
            score += Math.Min(15.0, keywordOverlap * 3.0);
        }
        factors++;

        var qualityDiff = Math.Abs((int)current.QualityTier - (int)previous.QualityTier);
        if (qualityDiff == 0)
        {
            score += 10.0;
        }
        else if (qualityDiff == 1)
        {
            score += 5.0;
        }
        factors++;

        if (current.Continuity != null && current.Continuity.SimilarityScore > 0)
        {
            score += current.Continuity.SimilarityScore * 0.25;
        }
        factors++;

        var normalizedScore = (score / (factors * 20.0)) * 100.0;
        return Math.Min(100.0, normalizedScore);
    }

    /// <summary>
    /// Calculate overall coherence across all scenes
    /// </summary>
    private double CalculateOverallCoherence(IReadOnlyList<OptimizedVisualPrompt> prompts)
    {
        if (prompts.Count <= 1)
        {
            return 100.0;
        }

        var totalCoherence = prompts.Sum(p => p.CoherenceScore);
        return totalCoherence / prompts.Count;
    }
}

/// <summary>
/// Configuration for scene optimization
/// </summary>
public record SceneOptimizationConfig
{
    /// <summary>
    /// Aspect ratio for video output (16:9, 9:16, 1:1, 4:3)
    /// </summary>
    public string AspectRatio { get; init; } = "16:9";

    /// <summary>
    /// Content safety level for filtering
    /// </summary>
    public ContentSafetyLevel ContentSafetyLevel { get; init; } = ContentSafetyLevel.Moderate;

    /// <summary>
    /// Strength of visual continuity between scenes (0.0 to 1.0)
    /// </summary>
    public double ContinuityStrength { get; init; } = 0.7;

    /// <summary>
    /// Number of variations to generate per scene
    /// </summary>
    public int VariationsPerScene { get; init; } = 3;

    /// <summary>
    /// Enable quality checks (blur, artifacts, NSFW detection)
    /// </summary>
    public bool EnableQualityChecks { get; init; } = true;

    /// <summary>
    /// LLM provider for enhanced prompt generation
    /// </summary>
    public Aura.Core.Providers.ILlmProvider? LlmProvider { get; init; }

    /// <summary>
    /// Optional pacing data for importance-based optimization
    /// </summary>
    public IReadOnlyList<Aura.Core.Models.PacingModels.SceneTimingSuggestion>? PacingData { get; init; }
}

/// <summary>
/// Optimized visual prompt with aspect ratio and continuity enhancements
/// </summary>
public record OptimizedVisualPrompt
{
    /// <summary>
    /// Base visual prompt from generation service
    /// </summary>
    public required VisualPrompt BasePrompt { get; init; }

    /// <summary>
    /// Scene index
    /// </summary>
    public int SceneIndex { get; init; }

    /// <summary>
    /// Aspect ratio used for optimization
    /// </summary>
    public string AspectRatio { get; init; } = "16:9";

    /// <summary>
    /// Aspect ratio optimization data
    /// </summary>
    public AspectRatioOptimization? AspectRatioData { get; init; }

    /// <summary>
    /// Enhanced negative prompts including common issues and safety filters
    /// </summary>
    public IReadOnlyList<string> EnhancedNegativePrompts { get; init; } = Array.Empty<string>();

    /// <summary>
    /// Continuity hints from previous scene
    /// </summary>
    public IReadOnlyList<string> ContinuityHints { get; init; } = Array.Empty<string>();

    /// <summary>
    /// Style consistency tokens to maintain visual coherence
    /// </summary>
    public IReadOnlyList<string> StyleConsistencyTokens { get; init; } = Array.Empty<string>();

    /// <summary>
    /// Optimized description with aspect ratio guidance
    /// </summary>
    public string OptimizedDescription { get; init; } = string.Empty;

    /// <summary>
    /// Content safety level applied
    /// </summary>
    public ContentSafetyLevel ContentSafetyLevel { get; init; }

    /// <summary>
    /// Number of variations to generate
    /// </summary>
    public int GenerationVariations { get; init; } = 3;

    /// <summary>
    /// Whether quality checks are enabled
    /// </summary>
    public bool QualityCheckEnabled { get; init; } = true;

    /// <summary>
    /// Coherence score with previous scene (0-100)
    /// </summary>
    public double CoherenceScore { get; init; }
}

/// <summary>
/// Aspect ratio optimization data
/// </summary>
public record AspectRatioOptimization
{
    /// <summary>
    /// Aspect ratio string (e.g., "16:9")
    /// </summary>
    public string Ratio { get; init; } = "16:9";

    /// <summary>
    /// Recommended width in pixels
    /// </summary>
    public int Width { get; init; } = 1920;

    /// <summary>
    /// Recommended height in pixels
    /// </summary>
    public int Height { get; init; } = 1080;

    /// <summary>
    /// Orientation (landscape, portrait, square)
    /// </summary>
    public string Orientation { get; init; } = "landscape";

    /// <summary>
    /// Composition guidance for this aspect ratio
    /// </summary>
    public string CompositionGuidance { get; init; } = string.Empty;

    /// <summary>
    /// Optimal shot types for this aspect ratio
    /// </summary>
    public IReadOnlyList<ShotType> OptimalShotTypes { get; init; } = Array.Empty<ShotType>();

    /// <summary>
    /// Framing adjustments needed
    /// </summary>
    public string FramingAdjustments { get; init; } = string.Empty;
}

/// <summary>
/// Content safety level for filtering
/// </summary>
public enum ContentSafetyLevel
{
    None = 0,
    Basic = 1,
    Moderate = 2,
    Strict = 3
}
