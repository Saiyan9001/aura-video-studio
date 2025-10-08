using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Aura.Core.Models;
using Aura.Providers.Planner;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Aura.Tests;

public class RecommendationEngineTests
{
    private readonly HeuristicRecommendationEngine _engine;
    private readonly Mock<ILogger<HeuristicRecommendationEngine>> _mockLogger;

    public RecommendationEngineTests()
    {
        _mockLogger = new Mock<ILogger<HeuristicRecommendationEngine>>();
        _engine = new HeuristicRecommendationEngine(_mockLogger.Object);
    }

    [Fact]
    public async Task GenerateRecommendationsAsync_ShouldReturnBoundedSceneCount()
    {
        // Arrange
        var brief = new Brief(
            Topic: "Test Topic",
            Audience: "General",
            Goal: "Educational",
            Tone: "Informative",
            Language: "en-US",
            Aspect: Aspect.Widescreen16x9
        );

        var planSpec = new PlanSpec(
            TargetDuration: TimeSpan.FromMinutes(5),
            Pacing: Pacing.Conversational,
            Density: Density.Balanced,
            Style: "Educational"
        );

        // Act
        var result = await _engine.GenerateRecommendationsAsync(brief, planSpec, null, null, CancellationToken.None);

        // Assert - Scene count should be between 3 and 20
        Assert.InRange(result.SceneCount, 3, 20);
    }

    [Fact]
    public async Task GenerateRecommendationsAsync_ShouldReturnBoundedShotsPerScene()
    {
        // Arrange
        var brief = new Brief(
            Topic: "Test Topic",
            Audience: "General",
            Goal: "Educational",
            Tone: "Informative",
            Language: "en-US",
            Aspect: Aspect.Widescreen16x9
        );

        var planSpec = new PlanSpec(
            TargetDuration: TimeSpan.FromMinutes(3),
            Pacing: Pacing.Fast,
            Density: Density.Dense,
            Style: "Standard"
        );

        // Act
        var result = await _engine.GenerateRecommendationsAsync(brief, planSpec, null, null, CancellationToken.None);

        // Assert - Shots per scene should be between 1 and 8
        Assert.InRange(result.ShotsPerScene, 1, 8);
    }

    [Fact]
    public async Task GenerateRecommendationsAsync_ShouldReturnBoundedBRollPercentage()
    {
        // Arrange
        var brief = new Brief(
            Topic: "Test Topic",
            Audience: "General",
            Goal: "Entertain",
            Tone: "Informative",
            Language: "en-US",
            Aspect: Aspect.Widescreen16x9
        );

        var planSpec = new PlanSpec(
            TargetDuration: TimeSpan.FromMinutes(4),
            Pacing: Pacing.Conversational,
            Density: Density.Balanced,
            Style: "Documentary"
        );

        // Act
        var result = await _engine.GenerateRecommendationsAsync(brief, planSpec, null, null, CancellationToken.None);

        // Assert - B-roll percentage should be between 0 and 100
        Assert.InRange(result.BRollPercentage, 0.0, 100.0);
    }

    [Fact]
    public async Task GenerateRecommendationsAsync_ShouldReturnBoundedOverlayDensity()
    {
        // Arrange
        var brief = new Brief(
            Topic: "Test Topic",
            Audience: "General",
            Goal: "Educational",
            Tone: "Informative",
            Language: "en-US",
            Aspect: Aspect.Widescreen16x9
        );

        var planSpec = new PlanSpec(
            TargetDuration: TimeSpan.FromMinutes(6),
            Pacing: Pacing.Conversational,
            Density: Density.Dense,
            Style: "Educational"
        );

        // Act
        var result = await _engine.GenerateRecommendationsAsync(brief, planSpec, null, null, CancellationToken.None);

        // Assert - Overlay density should be between 0.0 and 1.0
        Assert.InRange(result.OverlayDensity, 0.0, 1.0);
    }

    [Fact]
    public async Task GenerateRecommendationsAsync_ShouldReturnBoundedVoiceRate()
    {
        // Arrange
        var brief = new Brief(
            Topic: "Test Topic",
            Audience: "General",
            Goal: "Educational",
            Tone: "Informative",
            Language: "en-US",
            Aspect: Aspect.Widescreen16x9
        );

        var planSpec = new PlanSpec(
            TargetDuration: TimeSpan.FromMinutes(3),
            Pacing: Pacing.Fast,
            Density: Density.Balanced,
            Style: "Standard"
        );

        // Act
        var result = await _engine.GenerateRecommendationsAsync(brief, planSpec, null, null, CancellationToken.None);

        // Assert - Voice rate should be between 0.5 and 2.0 (practical range)
        Assert.InRange(result.VoiceRate, 0.5, 2.0);
    }

    [Fact]
    public async Task GenerateRecommendationsAsync_ShouldReturnBoundedVoicePitch()
    {
        // Arrange
        var brief = new Brief(
            Topic: "Test Topic",
            Audience: "General",
            Goal: "Educational",
            Tone: "Energetic",
            Language: "en-US",
            Aspect: Aspect.Widescreen16x9
        );

        var planSpec = new PlanSpec(
            TargetDuration: TimeSpan.FromMinutes(4),
            Pacing: Pacing.Conversational,
            Density: Density.Balanced,
            Style: "Standard"
        );

        // Act
        var result = await _engine.GenerateRecommendationsAsync(brief, planSpec, null, null, CancellationToken.None);

        // Assert - Voice pitch should be between -20 and +20
        Assert.InRange(result.VoicePitch, -20.0, 20.0);
    }

    [Fact]
    public async Task GenerateRecommendationsAsync_ShouldGenerateNonEmptyOutline()
    {
        // Arrange
        var brief = new Brief(
            Topic: "Machine Learning Basics",
            Audience: "Beginners",
            Goal: "Educational",
            Tone: "Informative",
            Language: "en-US",
            Aspect: Aspect.Widescreen16x9
        );

        var planSpec = new PlanSpec(
            TargetDuration: TimeSpan.FromMinutes(5),
            Pacing: Pacing.Conversational,
            Density: Density.Balanced,
            Style: "Educational"
        );

        // Act
        var result = await _engine.GenerateRecommendationsAsync(brief, planSpec, null, null, CancellationToken.None);

        // Assert
        Assert.NotNull(result.Outline);
        Assert.NotEmpty(result.Outline);
        Assert.Contains(brief.Topic, result.Outline);
    }

    [Fact]
    public async Task GenerateRecommendationsAsync_ShouldGenerateSeoFields()
    {
        // Arrange
        var brief = new Brief(
            Topic: "Introduction to Python Programming",
            Audience: "Beginners",
            Goal: "Educational",
            Tone: "Informative",
            Language: "en-US",
            Aspect: Aspect.Widescreen16x9
        );

        var planSpec = new PlanSpec(
            TargetDuration: TimeSpan.FromMinutes(10),
            Pacing: Pacing.Conversational,
            Density: Density.Balanced,
            Style: "Educational"
        );

        // Act
        var result = await _engine.GenerateRecommendationsAsync(brief, planSpec, null, null, CancellationToken.None);

        // Assert
        Assert.NotNull(result.SeoTitle);
        Assert.NotEmpty(result.SeoTitle);
        Assert.True(result.SeoTitle.Length <= 60, "SEO title should be 60 characters or less");

        Assert.NotNull(result.SeoDescription);
        Assert.NotEmpty(result.SeoDescription);
        Assert.True(result.SeoDescription.Length <= 155, "SEO description should be 155 characters or less");

        Assert.NotNull(result.SeoTags);
        Assert.NotEmpty(result.SeoTags);
        Assert.True(result.SeoTags.Count <= 15, "Should have at most 15 SEO tags");
    }

    [Fact]
    public async Task GenerateRecommendationsAsync_ShouldGenerateThumbnailPrompt()
    {
        // Arrange
        var brief = new Brief(
            Topic: "How to Cook Perfect Pasta",
            Audience: "Home Cooks",
            Goal: "Tutorial",
            Tone: "Friendly",
            Language: "en-US",
            Aspect: Aspect.Widescreen16x9
        );

        var planSpec = new PlanSpec(
            TargetDuration: TimeSpan.FromMinutes(8),
            Pacing: Pacing.Conversational,
            Density: Density.Balanced,
            Style: "Tutorial"
        );

        // Act
        var result = await _engine.GenerateRecommendationsAsync(brief, planSpec, null, null, CancellationToken.None);

        // Assert
        Assert.NotNull(result.ThumbnailPrompt);
        Assert.NotEmpty(result.ThumbnailPrompt);
        Assert.Contains("thumbnail", result.ThumbnailPrompt.ToLowerInvariant());
    }

    [Fact]
    public async Task GenerateRecommendationsAsync_ShouldAdjustForPacing()
    {
        // Arrange
        var brief = new Brief(
            Topic: "Test Topic",
            Audience: "General",
            Goal: "Educational",
            Tone: "Informative",
            Language: "en-US",
            Aspect: Aspect.Widescreen16x9
        );

        var chillPlan = new PlanSpec(
            TargetDuration: TimeSpan.FromMinutes(5),
            Pacing: Pacing.Chill,
            Density: Density.Balanced,
            Style: "Standard"
        );

        var fastPlan = new PlanSpec(
            TargetDuration: TimeSpan.FromMinutes(5),
            Pacing: Pacing.Fast,
            Density: Density.Balanced,
            Style: "Standard"
        );

        // Act
        var chillResult = await _engine.GenerateRecommendationsAsync(brief, chillPlan, null, null, CancellationToken.None);
        var fastResult = await _engine.GenerateRecommendationsAsync(brief, fastPlan, null, null, CancellationToken.None);

        // Assert - Fast pacing should have higher voice rate and more shots
        Assert.True(fastResult.VoiceRate > chillResult.VoiceRate);
        Assert.True(fastResult.ShotsPerScene >= chillResult.ShotsPerScene);
    }

    [Fact]
    public async Task GenerateRecommendationsAsync_ShouldGenerateMusicCurves()
    {
        // Arrange
        var brief = new Brief(
            Topic: "Test Topic",
            Audience: "General",
            Goal: "Educational",
            Tone: "Informative",
            Language: "en-US",
            Aspect: Aspect.Widescreen16x9
        );

        var planSpec = new PlanSpec(
            TargetDuration: TimeSpan.FromMinutes(5),
            Pacing: Pacing.Conversational,
            Density: Density.Balanced,
            Style: "Standard"
        );

        // Act
        var result = await _engine.GenerateRecommendationsAsync(brief, planSpec, null, null, CancellationToken.None);

        // Assert
        Assert.NotNull(result.MusicTempoCurve);
        Assert.NotEmpty(result.MusicTempoCurve);
        Assert.Contains("bpm", result.MusicTempoCurve.ToLowerInvariant());

        Assert.NotNull(result.MusicIntensityCurve);
        Assert.NotEmpty(result.MusicIntensityCurve);
    }

    [Fact]
    public async Task GenerateRecommendationsAsync_ShouldAdaptCaptionStyleToAspect()
    {
        // Arrange - Vertical format
        var verticalBrief = new Brief(
            Topic: "Test Topic",
            Audience: "Young Adults",
            Goal: "Entertain",
            Tone: "Energetic",
            Language: "en-US",
            Aspect: Aspect.Vertical9x16
        );

        var widescreenBrief = new Brief(
            Topic: "Test Topic",
            Audience: "Professionals",
            Goal: "Educational",
            Tone: "Professional",
            Language: "en-US",
            Aspect: Aspect.Widescreen16x9
        );

        var planSpec = new PlanSpec(
            TargetDuration: TimeSpan.FromMinutes(3),
            Pacing: Pacing.Fast,
            Density: Density.Balanced,
            Style: "Standard"
        );

        // Act
        var verticalResult = await _engine.GenerateRecommendationsAsync(verticalBrief, planSpec, null, null, CancellationToken.None);
        var widescreenResult = await _engine.GenerateRecommendationsAsync(widescreenBrief, planSpec, null, null, CancellationToken.None);

        // Assert - Different caption styles for different aspects
        Assert.NotNull(verticalResult.CaptionStyle);
        Assert.NotNull(widescreenResult.CaptionStyle);
        Assert.NotEqual(verticalResult.CaptionStyle, widescreenResult.CaptionStyle);
    }

    [Fact]
    public async Task GenerateRecommendationsAsync_ShouldConsiderAudiencePersona()
    {
        // Arrange
        var brief = new Brief(
            Topic: "Advanced Quantum Computing",
            Audience: "Experts",
            Goal: "Educational",
            Tone: "Professional",
            Language: "en-US",
            Aspect: Aspect.Widescreen16x9
        );

        var planSpec = new PlanSpec(
            TargetDuration: TimeSpan.FromMinutes(15),
            Pacing: Pacing.Conversational,
            Density: Density.Dense,
            Style: "Educational"
        );

        var expertPersona = new AudiencePersona(
            Name: "Dr. Expert",
            Demographics: "PhD researchers",
            Interests: "Quantum physics",
            ExpertiseLevel: "Expert"
        );

        // Act
        var result = await _engine.GenerateRecommendationsAsync(brief, planSpec, expertPersona, null, CancellationToken.None);

        // Assert
        Assert.NotNull(result.ReadingLevel);
        Assert.Contains("Advanced", result.ReadingLevel);
    }

    [Fact]
    public async Task GenerateRecommendationsAsync_ShortDuration_ShouldHaveFewerScenes()
    {
        // Arrange
        var brief = new Brief(
            Topic: "Quick Tip",
            Audience: "General",
            Goal: "Inform",
            Tone: "Informative",
            Language: "en-US",
            Aspect: Aspect.Vertical9x16
        );

        var shortPlan = new PlanSpec(
            TargetDuration: TimeSpan.FromSeconds(30),
            Pacing: Pacing.Fast,
            Density: Density.Balanced,
            Style: "Standard"
        );

        // Act
        var result = await _engine.GenerateRecommendationsAsync(brief, shortPlan, null, null, CancellationToken.None);

        // Assert - Should have minimum scenes (3)
        Assert.Equal(3, result.SceneCount);
    }

    [Fact]
    public async Task GenerateRecommendationsAsync_LongDuration_ShouldHaveMoreScenes()
    {
        // Arrange
        var brief = new Brief(
            Topic: "Comprehensive Guide",
            Audience: "General",
            Goal: "Educational",
            Tone: "Informative",
            Language: "en-US",
            Aspect: Aspect.Widescreen16x9
        );

        var longPlan = new PlanSpec(
            TargetDuration: TimeSpan.FromMinutes(30),
            Pacing: Pacing.Conversational,
            Density: Density.Balanced,
            Style: "Educational"
        );

        // Act
        var result = await _engine.GenerateRecommendationsAsync(brief, longPlan, null, null, CancellationToken.None);

        // Assert - Should be capped at maximum (20)
        Assert.True(result.SceneCount <= 20);
        Assert.True(result.SceneCount > 10); // Should be substantial for long video
    }
}
