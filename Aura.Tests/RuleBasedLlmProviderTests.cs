using Xunit;
using Aura.Core.Models;
using Aura.Providers.Llm;
using Microsoft.Extensions.Logging.Abstractions;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Aura.Tests;

public class RuleBasedLlmProviderTests
{
    [Fact]
    public async Task DraftScriptAsync_Should_GenerateScript()
    {
        // Arrange
        var provider = new RuleBasedLlmProvider(NullLogger<RuleBasedLlmProvider>.Instance);
        var brief = new Brief(
            Topic: "Machine Learning Basics",
            Audience: "Students",
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
        var script = await provider.DraftScriptAsync(brief, planSpec, CancellationToken.None);

        // Assert
        Assert.NotNull(script);
        Assert.NotEmpty(script);
        Assert.Contains("Machine Learning Basics", script);
        Assert.Contains("##", script); // Should contain section headers
    }

    [Fact]
    public async Task DraftScriptAsync_Should_GenerateMultipleScenes()
    {
        // Arrange
        var provider = new RuleBasedLlmProvider(NullLogger<RuleBasedLlmProvider>.Instance);
        var brief = new Brief(
            Topic: "Introduction to Programming",
            Audience: null,
            Goal: null,
            Tone: "Educational",
            Language: "en-US",
            Aspect: Aspect.Widescreen16x9
        );
        var planSpec = new PlanSpec(
            TargetDuration: TimeSpan.FromMinutes(8),
            Pacing: Pacing.Conversational,
            Density: Density.Balanced,
            Style: "Educational"
        );

        // Act
        var script = await provider.DraftScriptAsync(brief, planSpec, CancellationToken.None);

        // Assert
        Assert.NotNull(script);
        // Should have multiple scenes (Introduction + Body sections + Conclusion)
        var sectionCount = script.Split("##", StringSplitOptions.RemoveEmptyEntries).Length;
        Assert.True(sectionCount >= 3, $"Expected at least 3 sections, got {sectionCount}");
    }

    [Theory]
    [InlineData(Pacing.Chill, 2, 150)]  // Slower pacing = fewer words
    [InlineData(Pacing.Fast, 2, 200)]   // Faster pacing = more words (adjusted for templates)
    public async Task DraftScriptAsync_Should_AdjustLengthByPacing(Pacing pacing, int minutes, int minWords)
    {
        // Arrange
        var provider = new RuleBasedLlmProvider(NullLogger<RuleBasedLlmProvider>.Instance);
        var brief = new Brief(
            Topic: "Test Topic",
            Audience: null,
            Goal: null,
            Tone: "Informative",
            Language: "en-US",
            Aspect: Aspect.Widescreen16x9
        );
        var planSpec = new PlanSpec(
            TargetDuration: TimeSpan.FromMinutes(minutes),
            Pacing: pacing,
            Density: Density.Balanced,
            Style: "Educational"
        );

        // Act
        var script = await provider.DraftScriptAsync(brief, planSpec, CancellationToken.None);

        // Assert
        var wordCount = script.Split(new[] { ' ', '\t', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries).Length;
        Assert.True(wordCount >= minWords, $"Expected at least {minWords} words, got {wordCount}");
    }

    [Theory]
    [InlineData("Product Demo Tutorial", "product")]
    [InlineData("How to Guide for Beginners", "tutorial")]
    [InlineData("Marketing Launch Event", "marketing")]
    [InlineData("Educational Content Series", "educational")]
    [InlineData("Welcome to Our Platform", "welcome")]
    public async Task DraftScriptAsync_Should_UseAppropriateTemplate(string topic, string expectedKeyword)
    {
        // Arrange
        var provider = new RuleBasedLlmProvider(NullLogger<RuleBasedLlmProvider>.Instance);
        var brief = new Brief(
            Topic: topic,
            Audience: "General",
            Goal: "Engage",
            Tone: "Informative",
            Language: "en-US",
            Aspect: Aspect.Widescreen16x9
        );
        var planSpec = new PlanSpec(
            TargetDuration: TimeSpan.FromSeconds(15),
            Pacing: Pacing.Fast,
            Density: Density.Sparse,
            Style: "Demo"
        );

        // Act
        var script = await provider.DraftScriptAsync(brief, planSpec, CancellationToken.None);

        // Assert
        Assert.NotNull(script);
        Assert.NotEmpty(script);
        Assert.Contains(topic, script);
    }

    [Fact]
    public async Task DraftScriptAsync_WelcomeTopic_Should_MentionAuraVideoStudio()
    {
        // Arrange
        var provider = new RuleBasedLlmProvider(NullLogger<RuleBasedLlmProvider>.Instance);
        var brief = new Brief(
            Topic: "Welcome to Aura Video Studio",
            Audience: "New Users",
            Goal: "Onboard",
            Tone: "Friendly",
            Language: "en-US",
            Aspect: Aspect.Widescreen16x9
        );
        var planSpec = new PlanSpec(
            TargetDuration: TimeSpan.FromSeconds(12),
            Pacing: Pacing.Fast,
            Density: Density.Sparse,
            Style: "Demo"
        );

        // Act
        var script = await provider.DraftScriptAsync(brief, planSpec, CancellationToken.None);

        // Assert
        Assert.Contains("Aura Video Studio", script);
        Assert.Contains("AI", script);
        Assert.Contains("Welcome", script);
    }

    [Fact]
    public async Task DraftScriptAsync_Should_GenerateProfessionalContent()
    {
        // Arrange
        var provider = new RuleBasedLlmProvider(NullLogger<RuleBasedLlmProvider>.Instance);
        var brief = new Brief(
            Topic: "Professional Video Production",
            Audience: "Business Users",
            Goal: "Inform",
            Tone: "Professional",
            Language: "en-US",
            Aspect: Aspect.Widescreen16x9
        );
        var planSpec = new PlanSpec(
            TargetDuration: TimeSpan.FromMinutes(3),
            Pacing: Pacing.Conversational,
            Density: Density.Balanced,
            Style: "Professional"
        );

        // Act
        var script = await provider.DraftScriptAsync(brief, planSpec, CancellationToken.None);

        // Assert
        Assert.NotNull(script);
        Assert.NotEmpty(script);
        
        // Should have structured content
        Assert.Contains("## Introduction", script);
        Assert.Contains("## ", script);
        
        // Should not contain obvious placeholder text
        Assert.DoesNotContain("Lorem ipsum", script);
        Assert.DoesNotContain("placeholder", script.ToLowerInvariant());
    }

    [Fact]
    public async Task AnalyzeSceneImportanceAsync_Should_ReturnValidAnalysis()
    {
        // Arrange
        var provider = new RuleBasedLlmProvider(NullLogger<RuleBasedLlmProvider>.Instance);
        var sceneText = "This is an important introduction to our topic. It's critical to understand the key concepts.";

        // Act
        var result = await provider.AnalyzeSceneImportanceAsync(sceneText, null, "Educational video", CancellationToken.None);

        // Assert
        Assert.NotNull(result);
        Assert.InRange(result.Importance, 0, 100);
        Assert.InRange(result.Complexity, 0, 100);
        Assert.InRange(result.EmotionalIntensity, 0, 100);
        Assert.NotNull(result.InformationDensity);
        Assert.True(result.OptimalDurationSeconds > 0);
        Assert.NotNull(result.TransitionType);
        Assert.NotEmpty(result.Reasoning);
        Assert.Contains("Rule-based heuristic", result.Reasoning);
    }

    [Fact]
    public async Task AnalyzeSceneImportanceAsync_Should_DetectHighComplexity_ForLongText()
    {
        // Arrange
        var provider = new RuleBasedLlmProvider(NullLogger<RuleBasedLlmProvider>.Instance);
        var longText = string.Join(" ", System.Linq.Enumerable.Repeat("word", 150));

        // Act
        var result = await provider.AnalyzeSceneImportanceAsync(longText, null, "test", CancellationToken.None);

        // Assert
        Assert.NotNull(result);
        Assert.True(result.Complexity >= 70, $"Expected high complexity (>=70) for long text, got {result.Complexity}");
        Assert.Equal("high", result.InformationDensity);
    }

    [Fact]
    public async Task AnalyzeSceneImportanceAsync_Should_DetectLowComplexity_ForShortText()
    {
        // Arrange
        var provider = new RuleBasedLlmProvider(NullLogger<RuleBasedLlmProvider>.Instance);
        var shortText = "Short text.";

        // Act
        var result = await provider.AnalyzeSceneImportanceAsync(shortText, null, "test", CancellationToken.None);

        // Assert
        Assert.NotNull(result);
        Assert.True(result.Complexity <= 50, $"Expected low complexity (<=50) for short text, got {result.Complexity}");
        Assert.Equal("low", result.InformationDensity);
    }

    [Fact]
    public async Task AnalyzeSceneImportanceAsync_Should_DetectImportantKeywords()
    {
        // Arrange
        var provider = new RuleBasedLlmProvider(NullLogger<RuleBasedLlmProvider>.Instance);
        var textWithKeywords = "This is an important and critical introduction to key concepts.";
        var textWithoutKeywords = "This is some regular content about the topic.";

        // Act
        var resultWithKeywords = await provider.AnalyzeSceneImportanceAsync(textWithKeywords, null, "test", CancellationToken.None);
        var resultWithoutKeywords = await provider.AnalyzeSceneImportanceAsync(textWithoutKeywords, null, "test", CancellationToken.None);

        // Assert
        Assert.NotNull(resultWithKeywords);
        Assert.NotNull(resultWithoutKeywords);
        Assert.True(resultWithKeywords.Importance > resultWithoutKeywords.Importance,
            $"Text with keywords should have higher importance. With: {resultWithKeywords.Importance}, Without: {resultWithoutKeywords.Importance}");
    }

    [Fact]
    public async Task AnalyzeSceneImportanceAsync_Should_DetectEmotionalWords()
    {
        // Arrange
        var provider = new RuleBasedLlmProvider(NullLogger<RuleBasedLlmProvider>.Instance);
        var emotionalText = "This is amazing and incredible! It's fantastic and exciting!";
        var neutralText = "This is some content about the topic.";

        // Act
        var emotionalResult = await provider.AnalyzeSceneImportanceAsync(emotionalText, null, "test", CancellationToken.None);
        var neutralResult = await provider.AnalyzeSceneImportanceAsync(neutralText, null, "test", CancellationToken.None);

        // Assert
        Assert.NotNull(emotionalResult);
        Assert.NotNull(neutralResult);
        Assert.True(emotionalResult.EmotionalIntensity > neutralResult.EmotionalIntensity,
            $"Emotional text should have higher intensity. Emotional: {emotionalResult.EmotionalIntensity}, Neutral: {neutralResult.EmotionalIntensity}");
    }

    [Fact]
    public async Task AnalyzeSceneImportanceAsync_Should_CalculateOptimalDuration()
    {
        // Arrange
        var provider = new RuleBasedLlmProvider(NullLogger<RuleBasedLlmProvider>.Instance);
        var shortText = "Short.";
        var longText = string.Join(" ", System.Linq.Enumerable.Repeat("word", 100));

        // Act
        var shortResult = await provider.AnalyzeSceneImportanceAsync(shortText, null, "test", CancellationToken.None);
        var longResult = await provider.AnalyzeSceneImportanceAsync(longText, null, "test", CancellationToken.None);

        // Assert
        Assert.NotNull(shortResult);
        Assert.NotNull(longResult);
        Assert.True(shortResult.OptimalDurationSeconds >= 5, "Should have minimum duration of 5 seconds");
        Assert.True(longResult.OptimalDurationSeconds > shortResult.OptimalDurationSeconds,
            $"Longer text should have longer duration. Long: {longResult.OptimalDurationSeconds}s, Short: {shortResult.OptimalDurationSeconds}s");
    }

    [Theory]
    [InlineData("meanwhile we continue", "fade")]
    [InlineData("later that day", "fade")]
    [InlineData("gradually the scene changes", "dissolve")]
    [InlineData("slowly it transforms", "dissolve")]
    [InlineData("standard content", "cut")]
    public async Task AnalyzeSceneImportanceAsync_Should_DetectTransitionType(string text, string expectedTransition)
    {
        // Arrange
        var provider = new RuleBasedLlmProvider(NullLogger<RuleBasedLlmProvider>.Instance);

        // Act
        var result = await provider.AnalyzeSceneImportanceAsync(text, null, "test", CancellationToken.None);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(expectedTransition, result.TransitionType);
    }
}
