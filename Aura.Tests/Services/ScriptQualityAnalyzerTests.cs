using System;
using System.Threading;
using System.Threading.Tasks;
using Aura.Core.Models;
using Aura.Core.Models.Generation;
using Aura.Core.Services.ScriptEnhancement;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Aura.Tests.Services;

/// <summary>
/// Tests for ScriptQualityAnalyzer
/// </summary>
public class ScriptQualityAnalyzerTests
{
    private readonly Mock<ILogger<ScriptQualityAnalyzer>> _loggerMock;
    private readonly ScriptQualityAnalyzer _analyzer;

    public ScriptQualityAnalyzerTests()
    {
        _loggerMock = new Mock<ILogger<ScriptQualityAnalyzer>>();
        _analyzer = new ScriptQualityAnalyzer(_loggerMock.Object);
    }

    [Fact]
    public async Task AnalyzeAsync_ValidScript_ReturnsMetrics()
    {
        var script = CreateTestScript();
        var brief = CreateTestBrief();
        var planSpec = CreateTestPlanSpec();

        var metrics = await _analyzer.AnalyzeAsync(script, brief, planSpec);

        Assert.NotNull(metrics);
        Assert.True(metrics.OverallScore > 0);
        Assert.True(metrics.OverallScore <= 100);
        Assert.NotNull(metrics.Issues);
        Assert.NotNull(metrics.Suggestions);
        Assert.NotNull(metrics.Strengths);
    }

    [Fact]
    public void ValidateReadingSpeed_OptimalSpeed_ReturnsValid()
    {
        // Create script with exactly 155 words per minute
        // For a 15-second (0.25 minute) duration: 155 * 0.25 = ~39 words
        var script = new Script
        {
            Title = "Test Script",
            Scenes = new()
            {
                new ScriptScene
                {
                    Number = 1,
                    Narration = string.Join(" ", System.Linq.Enumerable.Repeat("word", 39)),
                    VisualPrompt = "Visual prompt",
                    Duration = TimeSpan.FromSeconds(15)
                }
            },
            TotalDuration = TimeSpan.FromSeconds(15)
        };

        var validation = _analyzer.ValidateReadingSpeed(script);

        Assert.True(validation.IsWithinRange, $"Expected within range but got {validation.OverallReadingSpeed} WPM");
        Assert.Empty(validation.Issues);
        Assert.True(validation.OverallReadingSpeed >= 150);
        Assert.True(validation.OverallReadingSpeed <= 160);
    }

    [Fact]
    public void ValidateReadingSpeed_TooFast_ReturnsInvalid()
    {
        var script = CreateTestScript(wordsPerMinute: 200);

        var validation = _analyzer.ValidateReadingSpeed(script);

        Assert.False(validation.IsWithinRange);
        Assert.NotEmpty(validation.Issues);
    }

    [Fact]
    public void ValidateReadingSpeed_TooSlow_ReturnsInvalid()
    {
        var script = CreateTestScript(wordsPerMinute: 100);

        var validation = _analyzer.ValidateReadingSpeed(script);

        Assert.False(validation.IsWithinRange);
        Assert.NotEmpty(validation.Issues);
    }

    [Fact]
    public void ValidateSceneCount_OptimalCount_ReturnsValid()
    {
        var script = CreateTestScript(sceneCount: 5);
        var planSpec = CreateTestPlanSpec(durationSeconds: 30);

        var validation = _analyzer.ValidateSceneCount(script, planSpec);

        Assert.True(validation.IsOptimal);
        Assert.Null(validation.Issue);
    }

    [Fact]
    public void ValidateSceneCount_TooFewScenes_ReturnsInvalid()
    {
        var script = CreateTestScript(sceneCount: 1);
        var planSpec = CreateTestPlanSpec(durationSeconds: 60);

        var validation = _analyzer.ValidateSceneCount(script, planSpec);

        Assert.False(validation.IsOptimal);
        Assert.NotNull(validation.Issue);
        Assert.Contains("Too few", validation.Issue);
    }

    [Fact]
    public void ValidateSceneCount_TooManyScenes_ReturnsInvalid()
    {
        var script = CreateTestScript(sceneCount: 20);
        var planSpec = CreateTestPlanSpec(durationSeconds: 30);

        var validation = _analyzer.ValidateSceneCount(script, planSpec);

        Assert.False(validation.IsOptimal);
        Assert.NotNull(validation.Issue);
        Assert.Contains("Too many", validation.Issue);
    }

    [Fact]
    public void ValidateVisualPrompts_SpecificPrompts_ReturnsValid()
    {
        var script = new Script
        {
            Title = "Test Script",
            Scenes = new()
            {
                new ScriptScene
                {
                    Number = 1,
                    Narration = "Welcome to our tutorial",
                    VisualPrompt = "Professional office setting with modern desk, laptop, bright lighting, person typing on keyboard",
                    Duration = TimeSpan.FromSeconds(5)
                }
            },
            TotalDuration = TimeSpan.FromSeconds(5)
        };

        var validation = _analyzer.ValidateVisualPrompts(script);

        Assert.True(validation.AllPromptsSpecific);
        Assert.True(validation.AverageSpecificity >= 60);
        Assert.Empty(validation.VaguePrompts);
    }

    [Fact]
    public void ValidateVisualPrompts_VaguePrompts_ReturnsInvalid()
    {
        var script = new Script
        {
            Title = "Test Script",
            Scenes = new()
            {
                new ScriptScene
                {
                    Number = 1,
                    Narration = "Welcome",
                    VisualPrompt = "Scene",
                    Duration = TimeSpan.FromSeconds(5)
                }
            },
            TotalDuration = TimeSpan.FromSeconds(5)
        };

        var validation = _analyzer.ValidateVisualPrompts(script);

        // A very vague prompt should have low specificity
        Assert.False(validation.AllPromptsSpecific, $"Expected not specific but got {validation.AverageSpecificity}");
        Assert.True(validation.AverageSpecificity < 60, $"Expected specificity < 60 but got {validation.AverageSpecificity}");
    }

    [Fact]
    public void ValidateNarrativeFlow_GoodFlow_ReturnsValid()
    {
        var script = new Script
        {
            Title = "Test Script",
            Scenes = new()
            {
                new ScriptScene
                {
                    Number = 1,
                    Narration = "Ever wonder how video editing software works? Let me show you the basics today.",
                    VisualPrompt = "Person at computer editing video",
                    Duration = TimeSpan.FromSeconds(5)
                },
                new ScriptScene
                {
                    Number = 2,
                    Narration = "Video editing software combines visual and audio elements to tell a compelling story. You can create amazing editing projects with the right editing tools.",
                    VisualPrompt = "Timeline view showing editing process",
                    Duration = TimeSpan.FromSeconds(5)
                },
                new ScriptScene
                {
                    Number = 3,
                    Narration = "Remember, editing practice makes perfect. Start creating your own editing masterpieces today with these editing tools!",
                    VisualPrompt = "Person smiling at completed editing project",
                    Duration = TimeSpan.FromSeconds(5)
                }
            },
            TotalDuration = TimeSpan.FromSeconds(15)
        };

        var validation = _analyzer.ValidateNarrativeFlow(script);

        // Check that the key indicators are present
        Assert.True(validation.HasStrongOpening, "Expected strong opening");
        Assert.True(validation.HasStrongClosing, "Expected strong closing");
        // The coherence check might not be perfect, but we can check basic flow
        Assert.True(validation.CoherenceScore > 0, $"Coherence score should be > 0, got {validation.CoherenceScore}");
    }

    [Fact]
    public void ValidateContentAppropriateness_CleanContent_ReturnsAppropriate()
    {
        var script = CreateTestScript();

        var validation = _analyzer.ValidateContentAppropriateness(script);

        Assert.True(validation.IsAppropriate);
        Assert.Empty(validation.Warnings);
    }

    private Script CreateTestScript(int sceneCount = 3, double wordsPerMinute = 155)
    {
        var scenes = new System.Collections.Generic.List<ScriptScene>();
        var targetWordsPerScene = (int)(wordsPerMinute * 5.0 / 60.0); // 5 seconds per scene
        
        for (int i = 1; i <= sceneCount; i++)
        {
            var narration = string.Join(" ", System.Linq.Enumerable.Repeat("word", targetWordsPerScene));
            scenes.Add(new ScriptScene
            {
                Number = i,
                Narration = narration,
                VisualPrompt = $"Scene {i} visual",
                Duration = TimeSpan.FromSeconds(5)
            });
        }

        return new Script
        {
            Title = "Test Script",
            Scenes = scenes,
            TotalDuration = TimeSpan.FromSeconds(sceneCount * 5)
        };
    }

    private Brief CreateTestBrief()
    {
        return new Brief(
            Topic: "Video Editing Tutorial",
            Audience: "Beginners",
            Goal: "Teach basic concepts",
            Tone: "friendly",
            Language: "en",
            Aspect: Aspect.Widescreen16x9
        );
    }

    private PlanSpec CreateTestPlanSpec(double durationSeconds = 30)
    {
        return new PlanSpec(
            TargetDuration: TimeSpan.FromSeconds(durationSeconds),
            Pacing: Pacing.Conversational,
            Density: Density.Balanced,
            Style: "educational"
        );
    }
}
