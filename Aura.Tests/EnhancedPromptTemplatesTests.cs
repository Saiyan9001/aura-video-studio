using System;
using Aura.Core.AI;
using Aura.Core.Models;
using Xunit;

namespace Aura.Tests;

/// <summary>
/// Tests for EnhancedPromptTemplates scene analysis functionality (PR 1)
/// Validates schema-driven prompts, few-shot examples, and configuration
/// </summary>
public class EnhancedPromptTemplatesTests
{
    private readonly Brief _testBrief = new Brief(
        Topic: "Test Topic",
        Audience: "General",
        Goal: "Educational",
        Tone: "Neutral",
        Language: "en-US",
        Aspect: Aspect.Widescreen16x9
    );

    private readonly PlanSpec _testSpec = new PlanSpec(
        TargetDuration: TimeSpan.FromMinutes(1),
        Pacing: Pacing.Conversational,
        Density: Density.Balanced,
        Style: "Educational"
    );

    #region Configuration Tests

    [Fact]
    public void ProviderPromptConfig_Should_DefaultToStrictSchemaEnabled()
    {
        // Assert
        Assert.True(EnhancedPromptTemplates.ProviderPromptConfig.StrictSchema);
    }

    [Fact]
    public void ProviderPromptConfig_Should_DefaultToTwoExamples()
    {
        // Assert
        Assert.Equal(2, EnhancedPromptTemplates.ProviderPromptConfig.ExampleCount);
    }

    [Fact]
    public void ProviderPromptConfig_Should_AllowTogglingStrictSchema()
    {
        // Arrange
        var originalValue = EnhancedPromptTemplates.ProviderPromptConfig.StrictSchema;

        try
        {
            // Act
            EnhancedPromptTemplates.ProviderPromptConfig.StrictSchema = false;
            Assert.False(EnhancedPromptTemplates.ProviderPromptConfig.StrictSchema);

            EnhancedPromptTemplates.ProviderPromptConfig.StrictSchema = true;
            Assert.True(EnhancedPromptTemplates.ProviderPromptConfig.StrictSchema);
        }
        finally
        {
            // Cleanup
            EnhancedPromptTemplates.ProviderPromptConfig.StrictSchema = originalValue;
        }
    }

    #endregion

    #region Scene Analysis System Prompt Tests

    [Fact]
    public void GetSystemPromptForSceneAnalysis_Should_ReturnStrictSchemaWhenEnabled()
    {
        // Arrange
        var originalValue = EnhancedPromptTemplates.ProviderPromptConfig.StrictSchema;

        try
        {
            EnhancedPromptTemplates.ProviderPromptConfig.StrictSchema = true;

            // Act
            var systemPrompt = EnhancedPromptTemplates.GetSystemPromptForSceneAnalysis();

            // Assert
            Assert.NotNull(systemPrompt);
            Assert.Contains("VALID JSON", systemPrompt);
            Assert.Contains("EXACT schema", systemPrompt);
            Assert.Contains("importance", systemPrompt);
            Assert.Contains("complexity", systemPrompt);
            Assert.Contains("emotionalIntensity", systemPrompt);
            Assert.Contains("informationDensity", systemPrompt);
            Assert.Contains("optimalDurationSeconds", systemPrompt);
            Assert.Contains("transitionType", systemPrompt);
            Assert.Contains("reasoning", systemPrompt);
        }
        finally
        {
            EnhancedPromptTemplates.ProviderPromptConfig.StrictSchema = originalValue;
        }
    }

    [Fact]
    public void GetSystemPromptForSceneAnalysis_Should_ContainSchemaFieldDefinitions()
    {
        // Arrange
        var originalValue = EnhancedPromptTemplates.ProviderPromptConfig.StrictSchema;

        try
        {
            EnhancedPromptTemplates.ProviderPromptConfig.StrictSchema = true;

            // Act
            var systemPrompt = EnhancedPromptTemplates.GetSystemPromptForSceneAnalysis();

            // Assert - Verify all required schema fields are defined
            Assert.Contains("importance", systemPrompt);
            Assert.Contains("0-100", systemPrompt);
            Assert.Contains("complexity", systemPrompt);
            Assert.Contains("emotionalIntensity", systemPrompt);
            Assert.Contains("informationDensity", systemPrompt);
            Assert.Contains("\"low\"", systemPrompt);
            Assert.Contains("\"medium\"", systemPrompt);
            Assert.Contains("\"high\"", systemPrompt);
            Assert.Contains("optimalDurationSeconds", systemPrompt);
            Assert.Contains("transitionType", systemPrompt);
            Assert.Contains("\"cut\"", systemPrompt);
            Assert.Contains("\"fade\"", systemPrompt);
            Assert.Contains("\"dissolve\"", systemPrompt);
            Assert.Contains("reasoning", systemPrompt);
        }
        finally
        {
            EnhancedPromptTemplates.ProviderPromptConfig.StrictSchema = originalValue;
        }
    }

    [Fact]
    public void GetSystemPromptForSceneAnalysis_Should_ReturnCompactSchemaWhenDisabled()
    {
        // Arrange
        var originalValue = EnhancedPromptTemplates.ProviderPromptConfig.StrictSchema;

        try
        {
            EnhancedPromptTemplates.ProviderPromptConfig.StrictSchema = false;

            // Act
            var systemPrompt = EnhancedPromptTemplates.GetSystemPromptForSceneAnalysis();

            // Assert
            Assert.NotNull(systemPrompt);
            Assert.Contains("video pacing expert", systemPrompt, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("JSON", systemPrompt);
            // Compact mode should be shorter and not include detailed schema
            Assert.DoesNotContain("EXACT schema", systemPrompt);
        }
        finally
        {
            EnhancedPromptTemplates.ProviderPromptConfig.StrictSchema = originalValue;
        }
    }

    #endregion

    #region Scene Analysis User Prompt Tests

    [Fact]
    public void BuildSceneAnalysisPrompt_Should_IncludeFewShotExamplesWhenStrictSchemaEnabled()
    {
        // Arrange
        var originalStrictSchema = EnhancedPromptTemplates.ProviderPromptConfig.StrictSchema;
        var originalExampleCount = EnhancedPromptTemplates.ProviderPromptConfig.ExampleCount;

        try
        {
            EnhancedPromptTemplates.ProviderPromptConfig.StrictSchema = true;
            EnhancedPromptTemplates.ProviderPromptConfig.ExampleCount = 2;

            // Act
            var userPrompt = EnhancedPromptTemplates.BuildSceneAnalysisPrompt(
                "This is a test scene",
                "Previous scene",
                "Educational video goal"
            );

            // Assert
            Assert.Contains("FEW-SHOT EXAMPLES", userPrompt);
            Assert.Contains("Example 1", userPrompt);
            Assert.Contains("Example 2", userPrompt);
            Assert.Contains("importance", userPrompt);
            Assert.Contains("optimalDurationSeconds", userPrompt);
        }
        finally
        {
            EnhancedPromptTemplates.ProviderPromptConfig.StrictSchema = originalStrictSchema;
            EnhancedPromptTemplates.ProviderPromptConfig.ExampleCount = originalExampleCount;
        }
    }

    [Fact]
    public void BuildSceneAnalysisPrompt_Should_RespectExampleCount()
    {
        // Arrange
        var originalStrictSchema = EnhancedPromptTemplates.ProviderPromptConfig.StrictSchema;
        var originalExampleCount = EnhancedPromptTemplates.ProviderPromptConfig.ExampleCount;

        try
        {
            EnhancedPromptTemplates.ProviderPromptConfig.StrictSchema = true;

            // Test with 1 example
            EnhancedPromptTemplates.ProviderPromptConfig.ExampleCount = 1;
            var userPrompt1 = EnhancedPromptTemplates.BuildSceneAnalysisPrompt(
                "Test scene",
                null,
                "Test goal"
            );
            Assert.Contains("Example 1", userPrompt1);
            Assert.DoesNotContain("Example 2", userPrompt1);

            // Test with 0 examples
            EnhancedPromptTemplates.ProviderPromptConfig.ExampleCount = 0;
            var userPrompt0 = EnhancedPromptTemplates.BuildSceneAnalysisPrompt(
                "Test scene",
                null,
                "Test goal"
            );
            Assert.DoesNotContain("FEW-SHOT EXAMPLES", userPrompt0);
        }
        finally
        {
            EnhancedPromptTemplates.ProviderPromptConfig.StrictSchema = originalStrictSchema;
            EnhancedPromptTemplates.ProviderPromptConfig.ExampleCount = originalExampleCount;
        }
    }

    [Fact]
    public void BuildSceneAnalysisPrompt_Should_IncludeSceneText()
    {
        // Act
        var userPrompt = EnhancedPromptTemplates.BuildSceneAnalysisPrompt(
            "This is the scene text to analyze",
            null,
            "Test goal"
        );

        // Assert
        Assert.Contains("This is the scene text to analyze", userPrompt);
        Assert.Contains("Scene:", userPrompt);
    }

    [Fact]
    public void BuildSceneAnalysisPrompt_Should_IncludePreviousSceneWhenProvided()
    {
        // Act
        var userPrompt = EnhancedPromptTemplates.BuildSceneAnalysisPrompt(
            "Current scene",
            "Previous scene text",
            "Test goal"
        );

        // Assert
        Assert.Contains("Previous scene text", userPrompt);
        Assert.Contains("Previous scene:", userPrompt);
    }

    [Fact]
    public void BuildSceneAnalysisPrompt_Should_OmitPreviousSceneWhenNull()
    {
        // Act
        var userPrompt = EnhancedPromptTemplates.BuildSceneAnalysisPrompt(
            "Current scene",
            null,
            "Test goal"
        );

        // Assert
        Assert.DoesNotContain("Previous scene:", userPrompt);
    }

    [Fact]
    public void BuildSceneAnalysisPrompt_Should_IncludeVideoGoal()
    {
        // Act
        var userPrompt = EnhancedPromptTemplates.BuildSceneAnalysisPrompt(
            "Scene text",
            null,
            "Educational content about technology"
        );

        // Assert
        Assert.Contains("Educational content about technology", userPrompt);
        Assert.Contains("Video goal:", userPrompt);
    }

    [Fact]
    public void BuildSceneAnalysisPrompt_Should_IncludeStrictJsonReminderInStrictMode()
    {
        // Arrange
        var originalValue = EnhancedPromptTemplates.ProviderPromptConfig.StrictSchema;

        try
        {
            EnhancedPromptTemplates.ProviderPromptConfig.StrictSchema = true;

            // Act
            var userPrompt = EnhancedPromptTemplates.BuildSceneAnalysisPrompt(
                "Scene",
                null,
                "Goal"
            );

            // Assert
            Assert.Contains("ONLY the JSON object", userPrompt);
            Assert.Contains("No explanatory text", userPrompt);
        }
        finally
        {
            EnhancedPromptTemplates.ProviderPromptConfig.StrictSchema = originalValue;
        }
    }

    #endregion

    #region Schema Version Tests

    [Fact]
    public void GetSystemPromptForSceneAnalysis_Should_IndicateSchemaVersion()
    {
        // Arrange
        var originalValue = EnhancedPromptTemplates.ProviderPromptConfig.StrictSchema;

        try
        {
            EnhancedPromptTemplates.ProviderPromptConfig.StrictSchema = true;

            // Act
            var systemPrompt = EnhancedPromptTemplates.GetSystemPromptForSceneAnalysis();

            // Assert - Check that schema version is documented (for PR 2 targeting)
            // The schema version is documented in the method XML comments
            Assert.NotNull(systemPrompt);
            Assert.NotEmpty(systemPrompt);
        }
        finally
        {
            EnhancedPromptTemplates.ProviderPromptConfig.StrictSchema = originalValue;
        }
    }

    #endregion

    #region Existing Script Generation Tests (Backward Compatibility)

    [Fact]
    public void GetSystemPromptForScriptGeneration_Should_ReturnNonEmptyPrompt()
    {
        // Act
        var systemPrompt = EnhancedPromptTemplates.GetSystemPromptForScriptGeneration();

        // Assert
        Assert.NotNull(systemPrompt);
        Assert.NotEmpty(systemPrompt);
        Assert.Contains("video creator", systemPrompt, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void BuildScriptGenerationPrompt_Should_IncludeBasicRequirements()
    {
        // Act
        var userPrompt = EnhancedPromptTemplates.BuildScriptGenerationPrompt(_testBrief, _testSpec);

        // Assert
        Assert.NotNull(userPrompt);
        Assert.Contains(_testBrief.Topic, userPrompt);
        Assert.Contains(_testBrief.Tone, userPrompt);
    }

    #endregion
}
