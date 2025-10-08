using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Aura.Core.Models;
using Aura.Core.Orchestrator;
using Aura.Core.Providers;
using Aura.Providers.Llm;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Aura.Tests;

public class LlmIntegrationTests
{
    [Fact]
    public async Task RuleBasedProvider_Should_GenerateNonEmptyScript()
    {
        // Arrange
        var provider = new RuleBasedLlmProvider(NullLogger<RuleBasedLlmProvider>.Instance);
        var brief = new Brief(
            Topic: "Introduction to Quantum Computing",
            Audience: "Tech enthusiasts",
            Goal: "Educational",
            Tone: "Informative",
            Language: "en-US",
            Aspect: Aspect.Widescreen16x9
        );
        var spec = new PlanSpec(
            TargetDuration: TimeSpan.FromMinutes(3),
            Pacing: Pacing.Conversational,
            Density: Density.Balanced,
            Style: "Educational"
        );

        // Act
        var script = await provider.DraftScriptAsync(brief, spec, CancellationToken.None);

        // Assert
        Assert.NotNull(script);
        Assert.NotEmpty(script);
        Assert.Contains("Quantum Computing", script);
        Assert.Contains("##", script); // Has scene headers
        Assert.Contains("Introduction", script);
        Assert.Contains("Conclusion", script);
    }

    [Fact]
    public async Task RuleBasedProvider_Should_RespectDensitySettings()
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

        var sparseSpec = new PlanSpec(
            TargetDuration: TimeSpan.FromMinutes(2),
            Pacing: Pacing.Conversational,
            Density: Density.Sparse,
            Style: "Educational"
        );

        var denseSpec = new PlanSpec(
            TargetDuration: TimeSpan.FromMinutes(2),
            Pacing: Pacing.Conversational,
            Density: Density.Dense,
            Style: "Educational"
        );

        // Act
        var sparseScript = await provider.DraftScriptAsync(brief, sparseSpec, CancellationToken.None);
        var denseScript = await provider.DraftScriptAsync(brief, denseSpec, CancellationToken.None);

        // Assert - Dense should have more words than sparse
        var sparseWordCount = sparseScript.Split(new[] { ' ', '\t', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries).Length;
        var denseWordCount = denseScript.Split(new[] { ' ', '\t', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries).Length;
        
        Assert.True(denseWordCount > sparseWordCount, 
            $"Dense ({denseWordCount} words) should have more words than sparse ({sparseWordCount} words)");
    }

    [Fact]
    public async Task EndToEndScriptGeneration_Should_WorkWithFreeProviders()
    {
        // Arrange - Simulate a complete free-tier flow
        var providers = new Dictionary<string, ILlmProvider>
        {
            ["RuleBased"] = new RuleBasedLlmProvider(NullLogger<RuleBasedLlmProvider>.Instance)
        };

        var config = new ProviderMixingConfig { LogProviderSelection = true };
        var mixer = new ProviderMixer(NullLogger<ProviderMixer>.Instance, config);
        var router = new LlmRouter(NullLogger<LlmRouter>.Instance, mixer, config);

        var brief = new Brief(
            Topic: "How to Build a REST API",
            Audience: "Beginner developers",
            Goal: "Tutorial",
            Tone: "Friendly",
            Language: "en-US",
            Aspect: Aspect.Widescreen16x9
        );

        var spec = new PlanSpec(
            TargetDuration: TimeSpan.FromMinutes(5),
            Pacing: Pacing.Conversational,
            Density: Density.Balanced,
            Style: "Educational"
        );

        // Act
        var script = await router.GenerateScriptAsync(providers, brief, spec, "Free", CancellationToken.None);

        // Assert
        Assert.NotNull(script);
        Assert.NotEmpty(script);
        Assert.Contains("REST API", script);
        
        // Verify structure
        var lines = script.Split('\n');
        var hasTitleHeader = lines.Any(l => l.StartsWith("# "));
        var hasSceneHeaders = lines.Any(l => l.StartsWith("## "));
        
        Assert.True(hasTitleHeader, "Script should have a title header");
        Assert.True(hasSceneHeaders, "Script should have scene headers");
    }

    [Fact]
    public async Task EndToEndScriptGeneration_Should_HandleMultipleScenes()
    {
        // Arrange
        var providers = new Dictionary<string, ILlmProvider>
        {
            ["RuleBased"] = new RuleBasedLlmProvider(NullLogger<RuleBasedLlmProvider>.Instance)
        };

        var config = new ProviderMixingConfig { LogProviderSelection = true };
        var mixer = new ProviderMixer(NullLogger<ProviderMixer>.Instance, config);
        var router = new LlmRouter(NullLogger<LlmRouter>.Instance, mixer, config);

        var brief = new Brief(
            Topic: "History of Computing",
            Audience: null,
            Goal: null,
            Tone: "Informative",
            Language: "en-US",
            Aspect: Aspect.Widescreen16x9
        );

        var spec = new PlanSpec(
            TargetDuration: TimeSpan.FromMinutes(10),
            Pacing: Pacing.Conversational,
            Density: Density.Balanced,
            Style: "Educational"
        );

        // Act
        var script = await router.GenerateScriptAsync(providers, brief, spec, "Free", CancellationToken.None);

        // Assert
        Assert.NotNull(script);
        
        // Count scene headers (##)
        var sceneCount = script.Split("##", StringSplitOptions.RemoveEmptyEntries).Length;
        Assert.True(sceneCount >= 5, $"Expected at least 5 scenes for 10-minute video, got {sceneCount}");
    }

    [Fact]
    public async Task RuleBasedProvider_Should_BeDeterministic()
    {
        // Arrange - Same seed means same output
        var provider1 = new RuleBasedLlmProvider(NullLogger<RuleBasedLlmProvider>.Instance);
        var provider2 = new RuleBasedLlmProvider(NullLogger<RuleBasedLlmProvider>.Instance);
        
        var brief = new Brief(
            Topic: "Test Topic",
            Audience: null,
            Goal: null,
            Tone: "Informative",
            Language: "en-US",
            Aspect: Aspect.Widescreen16x9
        );

        var spec = new PlanSpec(
            TargetDuration: TimeSpan.FromMinutes(2),
            Pacing: Pacing.Conversational,
            Density: Density.Balanced,
            Style: "Educational"
        );

        // Act
        var script1 = await provider1.DraftScriptAsync(brief, spec, CancellationToken.None);
        var script2 = await provider2.DraftScriptAsync(brief, spec, CancellationToken.None);

        // Assert - Should be identical because of fixed random seed
        Assert.Equal(script1, script2);
    }

    [Fact]
    public async Task RuleBasedProvider_Should_ScaleWithDuration()
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

        var shortSpec = new PlanSpec(
            TargetDuration: TimeSpan.FromMinutes(1),
            Pacing: Pacing.Conversational,
            Density: Density.Balanced,
            Style: "Educational"
        );

        var longSpec = new PlanSpec(
            TargetDuration: TimeSpan.FromMinutes(10),
            Pacing: Pacing.Conversational,
            Density: Density.Balanced,
            Style: "Educational"
        );

        // Act
        var shortScript = await provider.DraftScriptAsync(brief, shortSpec, CancellationToken.None);
        var longScript = await provider.DraftScriptAsync(brief, longSpec, CancellationToken.None);

        // Assert
        var shortWordCount = shortScript.Split(new[] { ' ', '\t', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries).Length;
        var longWordCount = longScript.Split(new[] { ' ', '\t', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries).Length;
        
        Assert.True(longWordCount > shortWordCount * 5, 
            $"10-minute script ({longWordCount} words) should be significantly longer than 1-minute script ({shortWordCount} words)");
    }
}
