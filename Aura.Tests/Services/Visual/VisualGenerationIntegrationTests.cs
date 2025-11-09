using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Aura.Core.Models;
using Aura.Core.Models.Visual;
using Aura.Core.Services.Visual;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Aura.Tests.Services.Visual;

/// <summary>
/// Integration tests for complete visual generation workflow with scene optimization
/// </summary>
public class VisualGenerationIntegrationTests
{
    private readonly SceneOptimizationService _optimizationService;
    private readonly VisualStyleCoherenceService _coherenceService;
    private readonly VisualEnhancementService _enhancementService;
    private readonly EnhancedFallbackService _fallbackService;
    private readonly ImageQualityChecker _qualityChecker;
    private readonly NsfwDetectionService _nsfwDetection;

    public VisualGenerationIntegrationTests()
    {
        var cinematography = new CinematographyKnowledgeBase();
        var continuity = new VisualContinuityEngine(NullLogger<VisualContinuityEngine>.Instance);
        var optimizer = new PromptOptimizer(NullLogger<PromptOptimizer>.Instance);
        
        var promptService = new VisualPromptGenerationService(
            NullLogger<VisualPromptGenerationService>.Instance,
            cinematography,
            continuity,
            optimizer);

        _optimizationService = new SceneOptimizationService(
            NullLogger<SceneOptimizationService>.Instance,
            promptService);

        _coherenceService = new VisualStyleCoherenceService(
            NullLogger<VisualStyleCoherenceService>.Instance);

        _enhancementService = new VisualEnhancementService(
            NullLogger<VisualEnhancementService>.Instance);

        _fallbackService = new EnhancedFallbackService(
            NullLogger<EnhancedFallbackService>.Instance);

        _qualityChecker = new ImageQualityChecker(
            NullLogger<ImageQualityChecker>.Instance);

        _nsfwDetection = new NsfwDetectionService(
            NullLogger<NsfwDetectionService>.Instance);
    }

    [Fact]
    public async Task CompleteWorkflow_WithMultipleScenes_ProducesOptimizedVisuals()
    {
        var scenes = CreateTestScenes(5);
        var brief = CreateTestBrief();
        
        var optimizationConfig = new SceneOptimizationConfig
        {
            AspectRatio = "16:9",
            ContentSafetyLevel = ContentSafetyLevel.Moderate,
            ContinuityStrength = 0.7,
            VariationsPerScene = 3,
            EnableQualityChecks = true
        };

        var optimizedPrompts = await _optimizationService.OptimizeScenePromptsAsync(
            scenes, brief, optimizationConfig, CancellationToken.None);

        Assert.NotNull(optimizedPrompts);
        Assert.Equal(5, optimizedPrompts.Count);

        foreach (var prompt in optimizedPrompts)
        {
            Assert.Equal("16:9", prompt.AspectRatio);
            Assert.NotEmpty(prompt.EnhancedNegativePrompts);
            Assert.True(prompt.CoherenceScore >= 0 && prompt.CoherenceScore <= 100);
        }

        var styleCoherenceConfig = new StyleCoherenceConfig
        {
            ExtractStyleFromFirstScene = true,
            CoherenceStrength = 0.7
        };

        var styledPrompts = await _coherenceService.ApplyCoherentStyleAsync(
            optimizedPrompts, styleCoherenceConfig, CancellationToken.None);

        Assert.NotNull(styledPrompts);
        Assert.Equal(5, styledPrompts.Count);
        Assert.True(styledPrompts[0].IsReferenceScene);
    }

    [Fact]
    public async Task VisualEnhancement_CalculatesKenBurnsEffect_Successfully()
    {
        var imageUrl = "https://example.com/image.jpg";
        var duration = 5.0;

        var config = new KenBurnsConfig
        {
            AutoScale = true,
            StartFromFocus = true,
            EndAtFocus = false
        };

        var effect = await _enhancementService.CalculateKenBurnsEffectAsync(
            imageUrl, duration, config, CancellationToken.None);

        Assert.NotNull(effect);
        Assert.Equal(duration, effect.Duration);
        Assert.True(effect.StartScale > 0);
        Assert.True(effect.EndScale > 0);
        Assert.NotNull(effect.StartPosition);
        Assert.NotNull(effect.EndPosition);
        Assert.NotEmpty(effect.MovementType);
    }

    [Fact]
    public async Task VisualEnhancement_CalculatesOptimalCrop_ForPortrait()
    {
        var imageUrl = "https://example.com/image.jpg";
        var targetAspectRatio = 9.0 / 16.0;

        var crop = await _enhancementService.CalculateOptimalCropAsync(
            imageUrl, targetAspectRatio, CancellationToken.None);

        Assert.NotNull(crop);
        Assert.True(crop.Width > 0);
        Assert.True(crop.Height > 0);
        Assert.Equal(targetAspectRatio, crop.AspectRatio);
    }

    [Fact]
    public async Task VisualEnhancement_CalculatesSmartZoom_DetectsEmptyAreas()
    {
        var imageUrl = "https://example.com/image.jpg";
        var minContentDensity = 40.0;

        var zoom = await _enhancementService.CalculateSmartZoomAsync(
            imageUrl, minContentDensity, CancellationToken.None);

        Assert.NotNull(zoom);
        Assert.True(zoom.ZoomLevel > 0);
        Assert.True(zoom.ContentDensity >= 0 && zoom.ContentDensity <= 100);
        Assert.NotEmpty(zoom.RecommendedAction);
    }

    [Fact]
    public void VisualEnhancement_CalculatesColorGrading_ForDifferentMoods()
    {
        var warmGrading = _enhancementService.CalculateColorGrading("warm");
        var coolGrading = _enhancementService.CalculateColorGrading("cool");
        var dramaticGrading = _enhancementService.CalculateColorGrading("dramatic");

        Assert.True(warmGrading.Temperature > 0);
        Assert.True(coolGrading.Temperature < 0);
        Assert.True(dramaticGrading.Contrast > 1.0);
    }

    [Fact]
    public void VisualEnhancement_CalculatesUpscaling_WhenNeeded()
    {
        var upscale1 = _enhancementService.CalculateUpscalingParameters(
            1280, 720, 1920, 1080);

        Assert.True(upscale1.NeedsUpscaling);
        Assert.Equal(1.5, upscale1.ScaleFactor);
        Assert.Equal("bilinear", upscale1.Method);

        var upscale2 = _enhancementService.CalculateUpscalingParameters(
            1920, 1080, 1920, 1080);

        Assert.False(upscale2.NeedsUpscaling);
    }

    [Fact]
    public async Task FallbackService_GeneratesStockPhotoFallback_Successfully()
    {
        var prompt = CreateOptimizedPrompt();

        var result = await _fallbackService.GenerateFallbackVisualAsync(
            prompt, FallbackTier.StockPhotos, CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal(FallbackTier.StockPhotos, result.FallbackTier);
        Assert.NotEmpty(result.ImageUrl);
        Assert.NotEmpty(result.Keywords);
        Assert.Equal("Unsplash", result.Source);
    }

    [Fact]
    public async Task FallbackService_GeneratesAbstractBackground_Successfully()
    {
        var prompt = CreateOptimizedPrompt();

        var result = await _fallbackService.GenerateFallbackVisualAsync(
            prompt, FallbackTier.AbstractBackground, CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal(FallbackTier.AbstractBackground, result.FallbackTier);
        Assert.NotNull(result.GradientConfig);
        Assert.NotNull(result.TextOverlay);
        Assert.NotEmpty(result.TextOverlay.Text);
    }

    [Fact]
    public async Task FallbackService_GeneratesSolidColorFallback_AsEmergency()
    {
        var prompt = CreateOptimizedPrompt();

        var result = await _fallbackService.GenerateFallbackVisualAsync(
            prompt, FallbackTier.SolidColor, CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal(FallbackTier.SolidColor, result.FallbackTier);
        Assert.NotNull(result.SolidColor);
        Assert.NotNull(result.TextOverlay);
        Assert.Contains("Scene", result.TextOverlay.Text);
    }

    [Fact]
    public async Task QualityChecker_EvaluatesImageQuality_Successfully()
    {
        var imageUrl = "https://example.com/test-image.jpg";

        var result = await _qualityChecker.CheckQualityAsync(imageUrl, CancellationToken.None);

        Assert.NotNull(result);
        Assert.True(result.OverallScore >= 0 && result.OverallScore <= 100);
        Assert.True(result.BlurScore >= 0 && result.BlurScore <= 100);
        Assert.True(result.ArtifactScore >= 0 && result.ArtifactScore <= 100);
        Assert.True(result.ResolutionScore >= 0 && result.ResolutionScore <= 100);
        Assert.NotNull(result.Issues);
    }

    [Fact]
    public async Task NsfwDetection_DetectsContentSafety_Successfully()
    {
        var imageUrl = "https://example.com/test-image.jpg";

        var result = await _nsfwDetection.DetectNsfwAsync(imageUrl, CancellationToken.None);

        Assert.NotNull(result);
        Assert.True(result.Confidence >= 0 && result.Confidence <= 100);
        Assert.NotNull(result.Categories);
    }

    [Fact]
    public async Task StyleCoherence_ExtractsStyleProfile_Successfully()
    {
        var referenceImageUrl = "https://example.com/reference.jpg";

        var profile = await _coherenceService.ExtractStyleProfileAsync(
            referenceImageUrl, CancellationToken.None);

        Assert.NotNull(profile);
        Assert.Equal(referenceImageUrl, profile.ReferenceImageUrl);
        Assert.NotEmpty(profile.ColorPalette);
        Assert.NotEmpty(profile.DominantColors);
        Assert.NotNull(profile.LightingCharacteristics);
        Assert.NotNull(profile.CompositionStyle);
    }

    private List<Scene> CreateTestScenes(int count)
    {
        var scenes = new List<Scene>();
        for (int i = 0; i < count; i++)
        {
            scenes.Add(new Scene(
                Index: i,
                Heading: $"Scene {i + 1}",
                Script: $"This is scene {i + 1} about {GetSceneTopic(i)}.",
                Start: TimeSpan.FromSeconds(i * 5),
                Duration: TimeSpan.FromSeconds(5)
            ));
        }
        return scenes;
    }

    private string GetSceneTopic(int index)
    {
        var topics = new[] { "nature", "technology", "people", "architecture", "abstract" };
        return topics[index % topics.Length];
    }

    private Brief CreateTestBrief()
    {
        return new Brief(
            Topic: "Visual Generation Workflow Test",
            Audience: "General Audience",
            Goal: "Demonstrate intelligent visual generation",
            Tone: "Professional",
            Language: "English",
            Aspect: Aspect.Widescreen16x9
        );
    }

    private OptimizedVisualPrompt CreateOptimizedPrompt()
    {
        var basePrompt = new VisualPrompt
        {
            SceneIndex = 0,
            DetailedDescription = "A professional office setting with modern furniture",
            Subject = "office",
            Framing = "medium shot",
            NarrativeKeywords = new[] { "office", "professional", "business" },
            Style = VisualStyle.Realistic,
            QualityTier = VisualQualityTier.Standard,
            ColorPalette = new[] { "#2C3E50", "#ECF0F1", "#3498DB" },
            StyleKeywords = new[] { "professional", "modern", "clean" },
            Lighting = new LightingSetup
            {
                Mood = "neutral",
                Direction = "front",
                Quality = "soft",
                TimeOfDay = "day"
            }
        };

        return new OptimizedVisualPrompt
        {
            BasePrompt = basePrompt,
            SceneIndex = 0,
            AspectRatio = "16:9",
            AspectRatioData = new AspectRatioOptimization
            {
                Ratio = "16:9",
                Width = 1920,
                Height = 1080,
                Orientation = "landscape",
                CompositionGuidance = "Horizontal rule of thirds",
                OptimalShotTypes = new[] { ShotType.WideShot },
                FramingAdjustments = "Standard widescreen"
            },
            EnhancedNegativePrompts = new[] { "blurry", "watermark", "low quality" },
            ContinuityHints = Array.Empty<string>(),
            StyleConsistencyTokens = Array.Empty<string>(),
            OptimizedDescription = "A professional office setting with modern furniture",
            ContentSafetyLevel = ContentSafetyLevel.Moderate,
            GenerationVariations = 3,
            QualityCheckEnabled = true,
            CoherenceScore = 100.0
        };
    }
}
