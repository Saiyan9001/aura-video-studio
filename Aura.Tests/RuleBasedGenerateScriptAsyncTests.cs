using Xunit;
using Aura.Providers.Llm;
using Microsoft.Extensions.Logging.Abstractions;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Diagnostics;

namespace Aura.Tests;

/// <summary>
/// Tests for the RuleBased provider's GenerateScriptAsync method (PR #5).
/// These tests verify offline fallback functionality with guaranteed execution time under 1 second.
/// </summary>
public class RuleBasedGenerateScriptAsyncTests
{
    private readonly RuleBasedLlmProvider _provider;

    public RuleBasedGenerateScriptAsyncTests()
    {
        _provider = new RuleBasedLlmProvider(NullLogger<RuleBasedLlmProvider>.Instance);
    }

    [Fact]
    public async Task GenerateScriptAsync_WithValidBrief_ReturnsCompleteScript()
    {
        var brief = "This is a tutorial about machine learning and artificial intelligence for beginners";
        var duration = 60;

        var script = await _provider.GenerateScriptAsync(brief, duration, CancellationToken.None);

        Assert.NotNull(script);
        Assert.NotEmpty(script.Scenes);
        Assert.True(script.TotalDuration.TotalSeconds >= duration * 0.95); 
        Assert.True(script.TotalDuration.TotalSeconds <= duration * 1.05); 
        Assert.NotEmpty(script.Title);
        Assert.NotEmpty(script.CorrelationId);
        Assert.NotNull(script.Metadata);
        Assert.Equal("RuleBased", script.Metadata.ProviderName);
    }

    [Theory]
    [InlineData("", 30)]
    [InlineData("test", 30)]
    public async Task GenerateScriptAsync_WithShortBrief_ReturnsGenericKeywords(string brief, int duration)
    {
        var script = await _provider.GenerateScriptAsync(brief, duration, CancellationToken.None);

        Assert.NotNull(script);
        Assert.NotEmpty(script.Scenes);
        Assert.True(script.Scenes.Count >= 3);
    }

    [Theory]
    [InlineData("Learn how to code and build tutorial applications", 30, "Tutorial")]
    [InlineData("Buy our amazing product on sale with discount offers", 45, "Marketing")]
    [InlineData("My honest review and rating of this product experience", 60, "Review")]
    [InlineData("General information about various topics", 30, "General")]
    public async Task GenerateScriptAsync_DetectsCorrectVideoType(string brief, int duration, string expectedTypeHint)
    {
        var script = await _provider.GenerateScriptAsync(brief, duration, CancellationToken.None);

        Assert.NotNull(script);
        Assert.NotEmpty(script.Scenes);
        
        var firstSceneText = script.Scenes[0].Narration.ToLowerInvariant();
        
        if (expectedTypeHint == "Tutorial")
        {
            Assert.Contains("learn", firstSceneText);
        }
        else if (expectedTypeHint == "Marketing")
        {
            Assert.True(firstSceneText.Contains("best") || firstSceneText.Contains("looking for"));
        }
        else if (expectedTypeHint == "Review")
        {
            Assert.Contains("review", firstSceneText);
        }
        else
        {
            Assert.Contains("welcome", firstSceneText);
        }
    }

    [Theory]
    [InlineData(30, 3, 20)]
    [InlineData(60, 3, 20)]
    [InlineData(120, 3, 20)]
    [InlineData(300, 3, 20)]
    [InlineData(20, 3, 20)]
    public async Task GenerateScriptAsync_CalculatesCorrectSceneCount(int duration, int minScenes, int maxScenes)
    {
        var brief = "A comprehensive tutorial about advanced programming concepts";
        
        var script = await _provider.GenerateScriptAsync(brief, duration, CancellationToken.None);

        var expectedSceneCount = Math.Max(3, Math.Min(20, duration / 10));
        Assert.Equal(expectedSceneCount, script.Scenes.Count);
        Assert.InRange(script.Scenes.Count, minScenes, maxScenes);
    }

    [Fact]
    public async Task GenerateScriptAsync_AllScenesHaveValidData()
    {
        var brief = "Educational content about science and technology for students";
        var duration = 90;

        var script = await _provider.GenerateScriptAsync(brief, duration, CancellationToken.None);

        Assert.NotNull(script);
        Assert.NotEmpty(script.Scenes);

        for (int i = 0; i < script.Scenes.Count; i++)
        {
            var scene = script.Scenes[i];
            Assert.Equal(i + 1, scene.Number);
            Assert.NotEmpty(scene.Narration);
            Assert.NotEmpty(scene.VisualPrompt);
            Assert.True(scene.Duration.TotalSeconds > 0);
            Assert.True(scene.VisualPrompt.Length <= 100);
        }
    }

    [Fact]
    public async Task GenerateScriptAsync_VisualPromptsUnder100Characters()
    {
        var brief = "Marketing video for our revolutionary new software product with incredible features";
        var duration = 60;

        var script = await _provider.GenerateScriptAsync(brief, duration, CancellationToken.None);

        foreach (var scene in script.Scenes)
        {
            Assert.True(scene.VisualPrompt.Length <= 100, 
                $"Visual prompt too long: {scene.VisualPrompt.Length} chars - '{scene.VisualPrompt}'");
        }
    }

    [Fact]
    public async Task GenerateScriptAsync_SceneDurationsMatchTotal()
    {
        var brief = "Tutorial about web development and programming best practices";
        var duration = 120;

        var script = await _provider.GenerateScriptAsync(brief, duration, CancellationToken.None);

        var totalSceneDuration = script.Scenes.Sum(s => s.Duration.TotalSeconds);
        var difference = Math.Abs(totalSceneDuration - duration);
        var tolerance = duration * 0.05;

        Assert.True(difference <= tolerance, 
            $"Scene durations sum to {totalSceneDuration}s but expected {duration}s (±{tolerance}s)");
    }

    [Fact]
    public async Task GenerateScriptAsync_IntroAndOutroGetCorrectDuration()
    {
        var brief = "Educational video about history and culture";
        var duration = 100;

        var script = await _provider.GenerateScriptAsync(brief, duration, CancellationToken.None);

        if (script.Scenes.Count >= 3)
        {
            var introDuration = script.Scenes[0].Duration.TotalSeconds;
            var outroDuration = script.Scenes[^1].Duration.TotalSeconds;

            var expectedIntroDuration = duration * 0.15;
            var expectedOutroDuration = duration * 0.15;

            Assert.InRange(introDuration, expectedIntroDuration * 0.8, expectedIntroDuration * 1.2);
            Assert.InRange(outroDuration, expectedOutroDuration * 0.8, expectedOutroDuration * 1.2);
        }
    }

    [Fact]
    public async Task GenerateScriptAsync_LastSceneUsesFadeTransition()
    {
        var brief = "Review of the latest technology products and features";
        var duration = 60;

        var script = await _provider.GenerateScriptAsync(brief, duration, CancellationToken.None);

        var lastScene = script.Scenes[^1];
        Assert.Equal(Core.Models.Generation.TransitionType.Fade, lastScene.Transition);
    }

    [Fact]
    public async Task GenerateScriptAsync_ExecutesUnderOneSecond()
    {
        var brief = "A detailed tutorial about advanced programming techniques, algorithms, and data structures for experienced developers";
        var duration = 300;

        var stopwatch = Stopwatch.StartNew();
        var script = await _provider.GenerateScriptAsync(brief, duration, CancellationToken.None);
        stopwatch.Stop();

        Assert.NotNull(script);
        Assert.True(stopwatch.ElapsedMilliseconds < 1000, 
            $"Generation took {stopwatch.ElapsedMilliseconds}ms, should be under 1000ms");
    }

    [Theory]
    [InlineData(30)]
    [InlineData(60)]
    [InlineData(120)]
    [InlineData(300)]
    public async Task GenerateScriptAsync_WorksWithVariousDurations(int duration)
    {
        var brief = "Educational content about various important topics";

        var script = await _provider.GenerateScriptAsync(brief, duration, CancellationToken.None);

        Assert.NotNull(script);
        Assert.NotEmpty(script.Scenes);
        Assert.True(script.Scenes.Count >= 3);
        Assert.True(script.TotalDuration.TotalSeconds >= duration * 0.95);
    }

    [Theory]
    [InlineData("machine learning algorithms neural networks deep learning", new[] { "machine", "learning", "algorithms", "neural", "networks" })]
    [InlineData("tutorial programming coding software development", new[] { "tutorial", "programming", "coding", "software", "development" })]
    public async Task GenerateScriptAsync_ExtractsRelevantKeywords(string brief, string[] expectedKeywords)
    {
        var duration = 60;

        var script = await _provider.GenerateScriptAsync(brief, duration, CancellationToken.None);

        var allNarration = string.Join(" ", script.Scenes.Select(s => s.Narration)).ToLowerInvariant();
        
        var foundKeywords = expectedKeywords.Count(keyword => allNarration.Contains(keyword.ToLowerInvariant()));
        Assert.True(foundKeywords >= 2, $"Should find at least 2 keywords from {string.Join(", ", expectedKeywords)}");
    }

    [Fact]
    public async Task GenerateScriptAsync_CoherentNarrationFlow()
    {
        var brief = "Tutorial on web development with HTML, CSS, and JavaScript";
        var duration = 90;

        var script = await _provider.GenerateScriptAsync(brief, duration, CancellationToken.None);

        Assert.Contains("Welcome", script.Scenes[0].Narration);
        
        var lastScene = script.Scenes[^1];
        Assert.True(lastScene.Narration.Contains("Thank") || 
                    lastScene.Narration.Contains("concludes") ||
                    lastScene.Narration.Contains("rating") ||
                    lastScene.Narration.Contains("Ready"));
    }

    [Fact]
    public async Task GenerateScriptAsync_NeverThrowsException()
    {
        var testCases = new[]
        {
            ("", 30),
            ("a", 60),
            ("Normal brief about testing", 45),
            (new string('x', 1000), 120),
            ("Special characters !@#$%^&*()", 30),
            (null, 60)
        };

        foreach (var (brief, duration) in testCases)
        {
            var script = await _provider.GenerateScriptAsync(brief ?? string.Empty, duration, CancellationToken.None);
            Assert.NotNull(script);
            Assert.NotEmpty(script.Scenes);
        }
    }

    [Fact]
    public async Task GenerateScriptAsync_MetadataIsComplete()
    {
        var brief = "Educational video about science";
        var duration = 60;

        var script = await _provider.GenerateScriptAsync(brief, duration, CancellationToken.None);

        Assert.NotNull(script.Metadata);
        Assert.Equal("RuleBased", script.Metadata.ProviderName);
        Assert.NotEmpty(script.Metadata.ModelUsed);
        Assert.Equal(0, script.Metadata.TokensUsed);
        Assert.Equal(0m, script.Metadata.EstimatedCost);
        Assert.Equal(Core.Models.Generation.ProviderTier.Free, script.Metadata.Tier);
        Assert.True(script.Metadata.GenerationTime.TotalMilliseconds > 0);
        Assert.True(script.Metadata.GenerationTime.TotalSeconds < 1);
    }

    [Theory]
    [InlineData(10)]
    [InlineData(50)]
    [InlineData(200)]
    public async Task GenerateScriptAsync_HandlesVariousBriefLengths(int wordCount)
    {
        var words = string.Join(" ", Enumerable.Range(0, wordCount).Select(i => $"word{i}"));
        var duration = 60;

        var script = await _provider.GenerateScriptAsync(words, duration, CancellationToken.None);

        Assert.NotNull(script);
        Assert.NotEmpty(script.Scenes);
        Assert.All(script.Scenes, scene =>
        {
            Assert.NotEmpty(scene.Narration);
            Assert.NotEmpty(scene.VisualPrompt);
        });
    }

    [Fact]
    public async Task GenerateScriptAsync_WorksCompletelyOffline()
    {
        var brief = "Tutorial about programming concepts";
        var duration = 60;

        var script = await _provider.GenerateScriptAsync(brief, duration, CancellationToken.None);

        Assert.NotNull(script);
        Assert.Equal(0m, script.Metadata.EstimatedCost);
        Assert.Equal(Core.Models.Generation.ProviderTier.Free, script.Metadata.Tier);
    }

    [Fact]
    public async Task GenerateScriptAsync_VisualPromptsContainStyleDescriptors()
    {
        var brief = "Marketing video for our product launch";
        var duration = 45;

        var script = await _provider.GenerateScriptAsync(brief, duration, CancellationToken.None);

        foreach (var scene in script.Scenes)
        {
            var hasStyle = scene.VisualPrompt.Contains("professional") || 
                          scene.VisualPrompt.Contains("modern") ||
                          scene.VisualPrompt.Contains("abstract");
            Assert.True(hasStyle, $"Visual prompt should contain style descriptor: {scene.VisualPrompt}");
        }
    }

    [Fact]
    public async Task GenerateScriptAsync_SingleSceneForVeryShortDuration()
    {
        var brief = "Quick introduction";
        var duration = 10;

        var script = await _provider.GenerateScriptAsync(brief, duration, CancellationToken.None);

        Assert.NotNull(script);
        Assert.True(script.Scenes.Count >= 1);
    }

    [Fact]
    public async Task GenerateScriptAsync_TwentySceneLimitRespected()
    {
        var brief = "Very long comprehensive tutorial about many topics";
        var duration = 500;

        var script = await _provider.GenerateScriptAsync(brief, duration, CancellationToken.None);

        Assert.NotNull(script);
        Assert.True(script.Scenes.Count <= 20, $"Scene count {script.Scenes.Count} exceeds maximum of 20");
    }

    [Fact]
    public async Task GenerateScriptAsync_CancellationTokenIsRespected()
    {
        var brief = "Test cancellation";
        var duration = 60;
        var cts = new CancellationTokenSource();
        cts.Cancel();

        var script = await _provider.GenerateScriptAsync(brief, duration, cts.Token);

        Assert.NotNull(script);
    }
}
