using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Aura.Core.Models.Visual;
using Microsoft.Extensions.Logging;

namespace Aura.Core.Services.Visual;

/// <summary>
/// Interface for image generation providers used by variation service
/// </summary>
public interface IImageGenerationProvider
{
    Task<string?> GenerateImageAsync(string prompt, ImageGenerationOptions options, CancellationToken ct);
    string ProviderName { get; }
}

/// <summary>
/// Options for image generation
/// </summary>
public record ImageGenerationOptions
{
    public int Width { get; init; } = 1920;
    public int Height { get; init; } = 1080;
    public string AspectRatio { get; init; } = "16:9";
    public string Style { get; init; } = "photorealistic";
    public int Quality { get; init; } = 80;
    public string[]? NegativePrompts { get; init; }
    public Dictionary<string, object>? ProviderSpecificOptions { get; init; }
}

/// <summary>
/// Service for generating multiple image variations with intelligent selection
/// Supports CLIP-based scoring, NSFW detection, and quality checks
/// </summary>
public class ImageVariationService
{
    private readonly ILogger<ImageVariationService> _logger;
    private readonly ImageQualityChecker _qualityChecker;
    private readonly ClipScoringService? _clipScoring;
    private readonly NsfwDetectionService _nsfwDetection;

    public ImageVariationService(
        ILogger<ImageVariationService> logger,
        ImageQualityChecker qualityChecker,
        NsfwDetectionService nsfwDetection,
        ClipScoringService? clipScoring = null)
    {
        _logger = logger;
        _qualityChecker = qualityChecker;
        _nsfwDetection = nsfwDetection;
        _clipScoring = clipScoring;
    }

    /// <summary>
    /// Generate multiple variations for a scene and select the best
    /// </summary>
    public async Task<ImageVariationResult> GenerateAndSelectBestAsync(
        OptimizedVisualPrompt prompt,
        IImageGenerationProvider provider,
        ImageVariationConfig config,
        CancellationToken ct = default)
    {
        _logger.LogInformation(
            "Generating {Count} variations for scene {SceneIndex}",
            config.VariationCount, prompt.SceneIndex);

        var variations = await GenerateVariationsAsync(prompt, provider, config, ct);

        var scoredVariations = await ScoreVariationsAsync(variations, prompt, config, ct);

        var selectedVariation = SelectBestVariation(scoredVariations, config);

        var result = new ImageVariationResult
        {
            SceneIndex = prompt.SceneIndex,
            GeneratedVariations = scoredVariations,
            SelectedVariation = selectedVariation,
            SelectionMode = config.SelectionMode,
            TotalVariationsGenerated = variations.Count,
            VariationsPassedQuality = scoredVariations.Count(v => v.PassedQualityChecks),
            AverageGenerationTimeMs = variations.Average(v => v.GenerationLatencyMs)
        };

        _logger.LogInformation(
            "Scene {SceneIndex}: Generated {Total} variations, {Passed} passed quality, selected variation with score {Score:F2}",
            prompt.SceneIndex, result.TotalVariationsGenerated, result.VariationsPassedQuality,
            selectedVariation?.OverallScore ?? 0);

        return result;
    }

    /// <summary>
    /// Generate multiple image variations using provider
    /// </summary>
    private async Task<List<ImageCandidate>> GenerateVariationsAsync(
        OptimizedVisualPrompt prompt,
        IImageGenerationProvider provider,
        ImageVariationConfig config,
        CancellationToken ct)
    {
        var variations = new List<ImageCandidate>();
        var options = BuildGenerationOptions(prompt, config);

        var promptText = BuildPromptText(prompt);
        var startTime = DateTime.UtcNow;

        for (int i = 0; i < config.VariationCount; i++)
        {
            if (ct.IsCancellationRequested)
            {
                break;
            }

            try
            {
                _logger.LogDebug("Generating variation {Index}/{Total} for scene {SceneIndex}",
                    i + 1, config.VariationCount, prompt.SceneIndex);

                var variedPrompt = config.VaryPrompts ? AddPromptVariation(promptText, i) : promptText;
                
                var imageUrl = await provider.GenerateImageAsync(variedPrompt, options, ct);

                if (!string.IsNullOrEmpty(imageUrl))
                {
                    var generationTime = (DateTime.UtcNow - startTime).TotalMilliseconds;
                    
                    var candidate = new ImageCandidate
                    {
                        ImageUrl = imageUrl,
                        Source = provider.ProviderName,
                        Width = options.Width,
                        Height = options.Height,
                        GenerationLatencyMs = generationTime,
                        Reasoning = $"Variation {i + 1} from {provider.ProviderName}"
                    };

                    variations.Add(candidate);
                    startTime = DateTime.UtcNow;
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to generate variation {Index} for scene {SceneIndex}",
                    i + 1, prompt.SceneIndex);
            }
        }

        _logger.LogInformation("Generated {Count} variations for scene {SceneIndex}",
            variations.Count, prompt.SceneIndex);

        return variations;
    }

    /// <summary>
    /// Score all variations using quality checks and CLIP
    /// </summary>
    private async Task<List<ScoredImageVariation>> ScoreVariationsAsync(
        List<ImageCandidate> variations,
        OptimizedVisualPrompt prompt,
        ImageVariationConfig config,
        CancellationToken ct)
    {
        var scoredVariations = new List<ScoredImageVariation>();

        foreach (var variation in variations)
        {
            if (ct.IsCancellationRequested)
            {
                break;
            }

            var qualityScore = await _qualityChecker.CheckQualityAsync(variation.ImageUrl, ct);
            
            NsfwDetectionResult nsfwCheck;
            if (config.EnableNsfwDetection)
            {
                nsfwCheck = await _nsfwDetection.DetectNsfwAsync(variation.ImageUrl, ct);
            }
            else
            {
                nsfwCheck = new NsfwDetectionResult
                {
                    IsNsfw = false,
                    Confidence = 0.0,
                    Categories = Array.Empty<string>()
                };
            }

            var clipScore = 0.0;
            if (_clipScoring != null && config.UseClipScoring)
            {
                try
                {
                    clipScore = await _clipScoring.ScorePromptAdherenceAsync(
                        variation.ImageUrl,
                        prompt.OptimizedDescription,
                        ct);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "CLIP scoring failed for variation, using fallback score");
                    clipScore = 70.0;
                }
            }
            else
            {
                clipScore = 70.0;
            }

            var aestheticScore = CalculateAestheticScore(qualityScore, clipScore);

            var overallScore = CalculateOverallScore(
                qualityScore,
                clipScore,
                aestheticScore,
                nsfwCheck,
                config);

            var passedChecks = qualityScore.IsAcceptable && !nsfwCheck.IsNsfw;

            var rejectionReasons = new List<string>();
            if (!qualityScore.IsAcceptable)
            {
                rejectionReasons.AddRange(qualityScore.Issues);
            }
            if (nsfwCheck.IsNsfw && config.EnableNsfwDetection)
            {
                rejectionReasons.Add($"NSFW content detected (confidence: {nsfwCheck.Confidence:F2})");
            }

            var scoredVariation = new ScoredImageVariation
            {
                ImageUrl = variation.ImageUrl,
                Source = variation.Source,
                Width = variation.Width,
                Height = variation.Height,
                GenerationLatencyMs = variation.GenerationLatencyMs,
                QualityScore = qualityScore.OverallScore,
                ClipScore = clipScore,
                AestheticScore = aestheticScore,
                OverallScore = overallScore,
                PassedQualityChecks = passedChecks,
                NsfwDetected = nsfwCheck.IsNsfw,
                QualityIssues = qualityScore.Issues.ToList(),
                RejectionReasons = rejectionReasons,
                Reasoning = variation.Reasoning
            };

            scoredVariations.Add(scoredVariation);
        }

        scoredVariations.Sort((a, b) => b.OverallScore.CompareTo(a.OverallScore));

        return scoredVariations;
    }

    /// <summary>
    /// Select best variation based on configuration
    /// </summary>
    private ScoredImageVariation? SelectBestVariation(
        List<ScoredImageVariation> variations,
        ImageVariationConfig config)
    {
        if (variations.Count == 0)
        {
            return null;
        }

        if (config.SelectionMode == VariationSelectionMode.Automatic)
        {
            var passed = variations.Where(v => v.PassedQualityChecks).ToList();
            if (passed.Count > 0)
            {
                return passed.OrderByDescending(v => v.OverallScore).First();
            }
            return variations.OrderByDescending(v => v.OverallScore).First();
        }
        else if (config.SelectionMode == VariationSelectionMode.Manual)
        {
            return null;
        }
        else if (config.SelectionMode == VariationSelectionMode.BestQuality)
        {
            return variations.OrderByDescending(v => v.QualityScore).First();
        }
        else if (config.SelectionMode == VariationSelectionMode.BestClipScore)
        {
            return variations.OrderByDescending(v => v.ClipScore).First();
        }

        return variations.First();
    }

    /// <summary>
    /// Build generation options from optimized prompt
    /// </summary>
    private ImageGenerationOptions BuildGenerationOptions(
        OptimizedVisualPrompt prompt,
        ImageVariationConfig config)
    {
        var aspectRatio = prompt.AspectRatioData ?? new AspectRatioOptimization();

        return new ImageGenerationOptions
        {
            Width = aspectRatio.Width,
            Height = aspectRatio.Height,
            AspectRatio = aspectRatio.Ratio,
            Style = prompt.BasePrompt.Style.ToString().ToLowerInvariant(),
            Quality = MapQualityTier(prompt.BasePrompt.QualityTier),
            NegativePrompts = prompt.EnhancedNegativePrompts.ToArray(),
            ProviderSpecificOptions = new Dictionary<string, object>
            {
                ["guidance_scale"] = 7.5,
                ["num_inference_steps"] = config.InferenceSteps,
                ["seed"] = config.UseDifferentSeeds ? Random.Shared.Next() : config.BaseSeed
            }
        };
    }

    /// <summary>
    /// Build full prompt text from optimized prompt
    /// </summary>
    private string BuildPromptText(OptimizedVisualPrompt prompt)
    {
        var parts = new List<string>();

        parts.Add(prompt.OptimizedDescription);

        if (prompt.ContinuityHints.Count > 0)
        {
            parts.Add(string.Join(". ", prompt.ContinuityHints));
        }

        if (prompt.StyleConsistencyTokens.Count > 0)
        {
            parts.Add(string.Join(", ", prompt.StyleConsistencyTokens));
        }

        if (prompt.BasePrompt.StyleKeywords.Count > 0)
        {
            parts.Add(string.Join(", ", prompt.BasePrompt.StyleKeywords));
        }

        return string.Join(". ", parts);
    }

    /// <summary>
    /// Add variation to prompt text
    /// </summary>
    private string AddPromptVariation(string basePrompt, int variationIndex)
    {
        var variations = new[]
        {
            "",
            ", slightly different angle",
            ", alternative composition",
            ", varied lighting",
            ", different perspective"
        };

        var index = variationIndex % variations.Length;
        return basePrompt + variations[index];
    }

    /// <summary>
    /// Map quality tier to quality percentage
    /// </summary>
    private int MapQualityTier(VisualQualityTier tier)
    {
        return tier switch
        {
            VisualQualityTier.Premium => 95,
            VisualQualityTier.Enhanced => 85,
            VisualQualityTier.Standard => 75,
            VisualQualityTier.Basic => 60,
            _ => 75
        };
    }

    /// <summary>
    /// Calculate aesthetic score from quality and CLIP scores
    /// </summary>
    private double CalculateAestheticScore(ImageQualityResult qualityResult, double clipScore)
    {
        var hasBlur = qualityResult.BlurScore < 50;
        var hasArtifacts = qualityResult.ArtifactScore < 50;

        var baseScore = (qualityResult.OverallScore + clipScore) / 2.0;

        if (hasBlur)
        {
            baseScore -= 10.0;
        }
        if (hasArtifacts)
        {
            baseScore -= 5.0;
        }

        return Math.Max(0, Math.Min(100, baseScore));
    }

    /// <summary>
    /// Calculate overall score with weighting
    /// </summary>
    private double CalculateOverallScore(
        ImageQualityResult qualityResult,
        double clipScore,
        double aestheticScore,
        NsfwDetectionResult nsfwResult,
        ImageVariationConfig config)
    {
        if (nsfwResult.IsNsfw)
        {
            return 0.0;
        }

        var score = (qualityResult.OverallScore * config.QualityWeight) +
                   (clipScore * config.ClipWeight) +
                   (aestheticScore * config.AestheticWeight);

        if (!qualityResult.IsAcceptable)
        {
            score *= 0.5;
        }

        return Math.Max(0, Math.Min(100, score));
    }
}

/// <summary>
/// Configuration for image variation generation
/// </summary>
public record ImageVariationConfig
{
    /// <summary>
    /// Number of variations to generate per scene
    /// </summary>
    public int VariationCount { get; init; } = 3;

    /// <summary>
    /// Selection mode for choosing the best variation
    /// </summary>
    public VariationSelectionMode SelectionMode { get; init; } = VariationSelectionMode.Automatic;

    /// <summary>
    /// Whether to use CLIP scoring for prompt adherence
    /// </summary>
    public bool UseClipScoring { get; init; } = true;

    /// <summary>
    /// Whether to enable NSFW content detection and filtering
    /// </summary>
    public bool EnableNsfwDetection { get; init; } = true;

    /// <summary>
    /// Number of inference steps for generation
    /// </summary>
    public int InferenceSteps { get; init; } = 30;

    /// <summary>
    /// Whether to use different seeds for variations
    /// </summary>
    public bool UseDifferentSeeds { get; init; } = true;

    /// <summary>
    /// Base seed for deterministic generation
    /// </summary>
    public int BaseSeed { get; init; } = -1;

    /// <summary>
    /// Whether to vary prompts slightly between variations
    /// </summary>
    public bool VaryPrompts { get; init; } = true;

    /// <summary>
    /// Weight for quality score in overall ranking (0-1)
    /// </summary>
    public double QualityWeight { get; init; } = 0.35;

    /// <summary>
    /// Weight for CLIP score in overall ranking (0-1)
    /// </summary>
    public double ClipWeight { get; init; } = 0.40;

    /// <summary>
    /// Weight for aesthetic score in overall ranking (0-1)
    /// </summary>
    public double AestheticWeight { get; init; } = 0.25;
}

/// <summary>
/// Result of image variation generation and selection
/// </summary>
public record ImageVariationResult
{
    /// <summary>
    /// Scene index
    /// </summary>
    public int SceneIndex { get; init; }

    /// <summary>
    /// All generated and scored variations
    /// </summary>
    public IReadOnlyList<ScoredImageVariation> GeneratedVariations { get; init; } = Array.Empty<ScoredImageVariation>();

    /// <summary>
    /// Selected best variation (null if manual selection)
    /// </summary>
    public ScoredImageVariation? SelectedVariation { get; init; }

    /// <summary>
    /// Selection mode used
    /// </summary>
    public VariationSelectionMode SelectionMode { get; init; }

    /// <summary>
    /// Total variations generated
    /// </summary>
    public int TotalVariationsGenerated { get; init; }

    /// <summary>
    /// Number of variations that passed quality checks
    /// </summary>
    public int VariationsPassedQuality { get; init; }

    /// <summary>
    /// Average generation time per variation
    /// </summary>
    public double AverageGenerationTimeMs { get; init; }
}

/// <summary>
/// Scored image variation with quality and CLIP metrics
/// </summary>
public record ScoredImageVariation : ImageCandidate
{
    /// <summary>
    /// CLIP similarity score (0-100) measuring prompt adherence
    /// </summary>
    public double ClipScore { get; init; }

    /// <summary>
    /// Whether this variation passed all quality checks
    /// </summary>
    public bool PassedQualityChecks { get; init; }

    /// <summary>
    /// Whether NSFW content was detected
    /// </summary>
    public bool NsfwDetected { get; init; }

    /// <summary>
    /// List of quality issues found
    /// </summary>
    public IReadOnlyList<string> QualityIssues { get; init; } = Array.Empty<string>();
}

/// <summary>
/// Mode for selecting best variation
/// </summary>
public enum VariationSelectionMode
{
    /// <summary>
    /// Automatically select best based on combined scores
    /// </summary>
    Automatic,

    /// <summary>
    /// Return all variations for manual selection
    /// </summary>
    Manual,

    /// <summary>
    /// Select based on highest quality score
    /// </summary>
    BestQuality,

    /// <summary>
    /// Select based on highest CLIP score
    /// </summary>
    BestClipScore
}
