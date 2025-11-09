using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Aura.Core.Models;
using Aura.Core.Models.Visual;
using Aura.Core.Services.Visual;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Aura.Tests.Services.Visual;

/// <summary>
/// Tests for SceneOptimizationService
/// </summary>
public class SceneOptimizationServiceTests
{
    private readonly SceneOptimizationService _service;
    private readonly VisualPromptGenerationService _promptService;

    public SceneOptimizationServiceTests()
    {
        var cinematography = new CinematographyKnowledgeBase();
        var continuity = new VisualContinuityEngine(NullLogger<VisualContinuityEngine>.Instance);
        var optimizer = new PromptOptimizer(NullLogger<PromptOptimizer>.Instance);
        
        _promptService = new VisualPromptGenerationService(
            NullLogger<VisualPromptGenerationService>.Instance,
            cinematography,
            continuity,
            optimizer);

        _service = new SceneOptimizationService(
            NullLogger<SceneOptimizationService>.Instance,
            _promptService);
    }

    [Fact]
    public async Task OptimizeScenePromptsAsync_WithWidescreen_OptimizesForAspectRatio()
    {
        var scenes = CreateTestScenes(3);
        var brief = CreateTestBrief();
        var config = new SceneOptimizationConfig
        {
            AspectRatio = "16:9",
            ContinuityStrength = 0.7,
            VariationsPerScene = 3
        };

        var result = await _service.OptimizeScenePromptsAsync(scenes, brief, config, CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal(3, result.Count);
        
        var firstPrompt = result[0];
        Assert.Equal("16:9", firstPrompt.AspectRatio);
        Assert.NotNull(firstPrompt.AspectRatioData);
        Assert.Equal(1920, firstPrompt.AspectRatioData.Width);
        Assert.Equal(1080, firstPrompt.AspectRatioData.Height);
        Assert.Equal("landscape", firstPrompt.AspectRatioData.Orientation);
    }

    [Fact]
    public async Task OptimizeScenePromptsAsync_WithPortrait_OptimizesForVertical()
    {
        var scenes = CreateTestScenes(2);
        var brief = CreateTestBrief();
        var config = new SceneOptimizationConfig
        {
            AspectRatio = "9:16",
            ContentSafetyLevel = ContentSafetyLevel.Moderate
        };

        var result = await _service.OptimizeScenePromptsAsync(scenes, brief, config, CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal(2, result.Count);
        
        var firstPrompt = result[0];
        Assert.NotNull(firstPrompt.AspectRatioData);
        Assert.Equal(1080, firstPrompt.AspectRatioData.Width);
        Assert.Equal(1920, firstPrompt.AspectRatioData.Height);
        Assert.Equal("portrait", firstPrompt.AspectRatioData.Orientation);
    }

    [Fact]
    public async Task OptimizeScenePromptsAsync_AddsEnhancedNegativePrompts()
    {
        var scenes = CreateTestScenes(1);
        var brief = CreateTestBrief();
        var config = new SceneOptimizationConfig
        {
            ContentSafetyLevel = ContentSafetyLevel.Strict
        };

        var result = await _service.OptimizeScenePromptsAsync(scenes, brief, config, CancellationToken.None);

        Assert.NotNull(result);
        Assert.Single(result);
        
        var prompt = result[0];
        Assert.NotEmpty(prompt.EnhancedNegativePrompts);
        Assert.Contains(prompt.EnhancedNegativePrompts, p => p.Contains("blurry"));
        Assert.Contains(prompt.EnhancedNegativePrompts, p => p.Contains("watermark"));
        Assert.Contains(prompt.EnhancedNegativePrompts, p => p.Contains("nsfw"));
    }

    [Fact]
    public async Task OptimizeScenePromptsAsync_BuildsContinuityHints()
    {
        var scenes = CreateTestScenes(3);
        var brief = CreateTestBrief();
        var config = new SceneOptimizationConfig
        {
            ContinuityStrength = 0.8
        };

        var result = await _service.OptimizeScenePromptsAsync(scenes, brief, config, CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal(3, result.Count);
        
        var secondPrompt = result[1];
        Assert.NotEmpty(secondPrompt.ContinuityHints);
        
        var thirdPrompt = result[2];
        Assert.NotEmpty(thirdPrompt.ContinuityHints);
    }

    [Fact]
    public async Task OptimizeScenePromptsAsync_CalculatesCoherenceScores()
    {
        var scenes = CreateTestScenes(3);
        var brief = CreateTestBrief();
        var config = new SceneOptimizationConfig();

        var result = await _service.OptimizeScenePromptsAsync(scenes, brief, config, CancellationToken.None);

        Assert.NotNull(result);
        foreach (var prompt in result)
        {
            Assert.True(prompt.CoherenceScore >= 0 && prompt.CoherenceScore <= 100);
        }
        
        Assert.Equal(100.0, result[0].CoherenceScore);
    }

    [Fact]
    public async Task OptimizeScenePromptsAsync_WithSquareAspectRatio_UsesCorrectDimensions()
    {
        var scenes = CreateTestScenes(1);
        var brief = CreateTestBrief();
        var config = new SceneOptimizationConfig
        {
            AspectRatio = "1:1"
        };

        var result = await _service.OptimizeScenePromptsAsync(scenes, brief, config, CancellationToken.None);

        Assert.NotNull(result);
        var prompt = result[0];
        Assert.NotNull(prompt.AspectRatioData);
        Assert.Equal(1080, prompt.AspectRatioData.Width);
        Assert.Equal(1080, prompt.AspectRatioData.Height);
        Assert.Equal("square", prompt.AspectRatioData.Orientation);
    }

    [Fact]
    public async Task OptimizeScenePromptsAsync_ExtractsStyleConsistencyTokens()
    {
        var scenes = CreateTestScenes(2);
        var brief = CreateTestBrief();
        var config = new SceneOptimizationConfig
        {
            ContinuityStrength = 0.7
        };

        var result = await _service.OptimizeScenePromptsAsync(scenes, brief, config, CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal(2, result.Count);
        
        var secondPrompt = result[1];
        Assert.NotEmpty(secondPrompt.StyleConsistencyTokens);
    }

    [Fact]
    public async Task OptimizeScenePromptsAsync_WithBasicSafety_IncludesFewerNegatives()
    {
        var scenes = CreateTestScenes(1);
        var brief = CreateTestBrief();
        var configBasic = new SceneOptimizationConfig
        {
            ContentSafetyLevel = ContentSafetyLevel.Basic
        };
        var configStrict = new SceneOptimizationConfig
        {
            ContentSafetyLevel = ContentSafetyLevel.Strict
        };

        var resultBasic = await _service.OptimizeScenePromptsAsync(scenes, brief, configBasic, CancellationToken.None);
        var resultStrict = await _service.OptimizeScenePromptsAsync(scenes, brief, configStrict, CancellationToken.None);

        Assert.True(resultStrict[0].EnhancedNegativePrompts.Count > resultBasic[0].EnhancedNegativePrompts.Count);
    }

    [Fact]
    public async Task OptimizeScenePromptsAsync_HandlesEmptyScenes()
    {
        var scenes = new List<Scene>();
        var brief = CreateTestBrief();
        var config = new SceneOptimizationConfig();

        var result = await _service.OptimizeScenePromptsAsync(scenes, brief, config, CancellationToken.None);

        Assert.NotNull(result);
        Assert.Empty(result);
    }

    [Fact]
    public async Task OptimizeScenePromptsAsync_WithZeroContinuity_NoHints()
    {
        var scenes = CreateTestScenes(2);
        var brief = CreateTestBrief();
        var config = new SceneOptimizationConfig
        {
            ContinuityStrength = 0.0
        };

        var result = await _service.OptimizeScenePromptsAsync(scenes, brief, config, CancellationToken.None);

        Assert.NotNull(result);
        var secondPrompt = result[1];
        Assert.Empty(secondPrompt.ContinuityHints);
    }

    private List<Scene> CreateTestScenes(int count)
    {
        var scenes = new List<Scene>();
        for (int i = 0; i < count; i++)
        {
            scenes.Add(new Scene(
                Index: i,
                Heading: $"Scene {i + 1}",
                Script: $"This is scene {i + 1} with some interesting content.",
                Start: TimeSpan.FromSeconds(i * 5),
                Duration: TimeSpan.FromSeconds(5)
            ));
        }
        return scenes;
    }

    private Brief CreateTestBrief()
    {
        return new Brief(
            Topic: "Test Video Topic",
            Audience: "General Audience",
            Goal: "To inform and engage",
            Tone: "Professional",
            Language: "English",
            Aspect: Aspect.Widescreen16x9
        );
    }
}
