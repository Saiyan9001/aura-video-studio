using System;
using System.Threading.Tasks;
using Aura.Core.Models;
using Aura.Core.Models.Generation;
using Aura.Core.Services.Generation;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Aura.Tests;

public class ScriptProcessorTests
{
    private readonly ScriptProcessor _processor;

    public ScriptProcessorTests()
    {
        _processor = new ScriptProcessor(NullLogger<ScriptProcessor>.Instance);
    }

    [Fact]
    public void ValidateSceneTiming_AdjustsToTargetDuration()
    {
        var script = new Script
        {
            Title = "Test Script",
            Scenes = new()
            {
                new ScriptScene { Number = 1, Narration = "Scene 1", Duration = TimeSpan.FromSeconds(10), Transition = TransitionType.Cut },
                new ScriptScene { Number = 2, Narration = "Scene 2", Duration = TimeSpan.FromSeconds(10), Transition = TransitionType.Cut }
            },
            TotalDuration = TimeSpan.FromSeconds(20)
        };

        var targetDuration = TimeSpan.FromSeconds(30);
        var result = _processor.ValidateSceneTiming(script, targetDuration);

        Assert.Equal(2, result.Scenes.Count);
        Assert.True(result.Scenes[0].Duration.TotalSeconds > 10);
        Assert.True(result.Scenes[1].Duration.TotalSeconds > 10);
        Assert.InRange(result.TotalDuration.TotalSeconds, 28, 32);
    }

    [Fact]
    public void OptimizeNarrationFlow_CleansUpWhitespace()
    {
        var script = new Script
        {
            Title = "Test Script",
            Scenes = new()
            {
                new ScriptScene 
                { 
                    Number = 1, 
                    Narration = "This  has   extra    spaces.", 
                    Duration = TimeSpan.FromSeconds(5),
                    Transition = TransitionType.Cut
                }
            }
        };

        var result = _processor.OptimizeNarrationFlow(script);

        Assert.Equal("This has extra spaces.", result.Scenes[0].Narration);
    }

    [Fact]
    public void ApplyTransitions_SetsLastSceneToFade()
    {
        var script = new Script
        {
            Title = "Test Script",
            Scenes = new()
            {
                new ScriptScene { Number = 1, Narration = "Scene 1", Duration = TimeSpan.FromSeconds(5), Transition = TransitionType.Cut },
                new ScriptScene { Number = 2, Narration = "Scene 2", Duration = TimeSpan.FromSeconds(5), Transition = TransitionType.Cut },
                new ScriptScene { Number = 3, Narration = "Scene 3", Duration = TimeSpan.FromSeconds(5), Transition = TransitionType.Cut }
            },
            TotalDuration = TimeSpan.FromSeconds(15)
        };

        var result = _processor.ApplyTransitions(script, "professional");

        Assert.Equal(TransitionType.Fade, result.Scenes[2].Transition);
    }

    [Fact]
    public void CalculateReadingSpeed_ReturnsCorrectWPM()
    {
        var narration = "This is a test sentence with exactly ten words total.";
        var duration = TimeSpan.FromSeconds(30);

        var wpm = _processor.CalculateReadingSpeed(narration, duration);

        Assert.InRange(wpm, 15, 25);
    }

    [Fact]
    public void EnsureSceneBalance_DoesNotCrashWithSingleScene()
    {
        var script = new Script
        {
            Title = "Test Script",
            Scenes = new()
            {
                new ScriptScene { Number = 1, Narration = "Only scene", Duration = TimeSpan.FromSeconds(10), Transition = TransitionType.Cut }
            },
            TotalDuration = TimeSpan.FromSeconds(10)
        };

        var result = _processor.EnsureSceneBalance(script);

        Assert.Single(result.Scenes);
        Assert.Equal("Only scene", result.Scenes[0].Narration);
    }

    [Fact]
    public void EnhanceVisualPrompts_AddsQualityDescriptorsForStability()
    {
        var script = new Script
        {
            Title = "Test Script",
            Scenes = new()
            {
                new ScriptScene 
                { 
                    Number = 1, 
                    Narration = "Scene 1", 
                    VisualPrompt = "A sunset",
                    Duration = TimeSpan.FromSeconds(5),
                    Transition = TransitionType.Cut
                }
            },
            TotalDuration = TimeSpan.FromSeconds(5)
        };

        var result = _processor.EnhanceVisualPrompts(script, "Stability");

        Assert.Contains("high quality", result.Scenes[0].VisualPrompt);
    }
}
