using System;
using System.Threading;
using System.Threading.Tasks;
using Aura.Core.Models;
using Aura.Core.Orchestrator;
using Aura.Core.Providers;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
using System.Collections.Generic;
using Moq;

namespace Aura.Tests;

public class LlmRoutingTests
{
    [Fact]
    public async Task LlmRouter_Should_UseFirstAvailableProvider()
    {
        // Arrange
        var mockProvider = new Mock<ILlmProvider>();
        mockProvider
            .Setup(p => p.DraftScriptAsync(It.IsAny<Brief>(), It.IsAny<PlanSpec>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("# Test Script\n## Introduction\nTest content");

        var providers = new Dictionary<string, ILlmProvider>
        {
            ["RuleBased"] = mockProvider.Object
        };

        var config = new ProviderMixingConfig { LogProviderSelection = true };
        var mixer = new ProviderMixer(NullLogger<ProviderMixer>.Instance, config);
        var router = new LlmRouter(NullLogger<LlmRouter>.Instance, mixer, config);

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
        var result = await router.GenerateScriptAsync(providers, brief, spec, "Free", CancellationToken.None);

        // Assert
        Assert.NotNull(result);
        Assert.Contains("Test Script", result);
        mockProvider.Verify(p => p.DraftScriptAsync(It.IsAny<Brief>(), It.IsAny<PlanSpec>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task LlmRouter_Should_FallbackWhenProviderFails()
    {
        // Arrange
        var failingProvider = new Mock<ILlmProvider>();
        failingProvider
            .Setup(p => p.DraftScriptAsync(It.IsAny<Brief>(), It.IsAny<PlanSpec>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("Provider failed"));

        var fallbackProvider = new Mock<ILlmProvider>();
        fallbackProvider
            .Setup(p => p.DraftScriptAsync(It.IsAny<Brief>(), It.IsAny<PlanSpec>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("# Fallback Script\n## Introduction\nFallback content");

        var providers = new Dictionary<string, ILlmProvider>
        {
            ["Ollama"] = failingProvider.Object,
            ["RuleBased"] = fallbackProvider.Object
        };

        var config = new ProviderMixingConfig { LogProviderSelection = true };
        var mixer = new ProviderMixer(NullLogger<ProviderMixer>.Instance, config);
        var router = new LlmRouter(NullLogger<LlmRouter>.Instance, mixer, config);

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
        var result = await router.GenerateScriptAsync(providers, brief, spec, "Free", CancellationToken.None);

        // Assert
        Assert.NotNull(result);
        Assert.Contains("Fallback Script", result);
        failingProvider.Verify(p => p.DraftScriptAsync(It.IsAny<Brief>(), It.IsAny<PlanSpec>(), It.IsAny<CancellationToken>()), Times.Once);
        fallbackProvider.Verify(p => p.DraftScriptAsync(It.IsAny<Brief>(), It.IsAny<PlanSpec>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task LlmRouter_Should_ThrowWhenAllProvidersFail()
    {
        // Arrange
        var failingProvider = new Mock<ILlmProvider>();
        failingProvider
            .Setup(p => p.DraftScriptAsync(It.IsAny<Brief>(), It.IsAny<PlanSpec>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("Provider failed"));

        var providers = new Dictionary<string, ILlmProvider>
        {
            ["RuleBased"] = failingProvider.Object
        };

        var config = new ProviderMixingConfig { LogProviderSelection = true };
        var mixer = new ProviderMixer(NullLogger<ProviderMixer>.Instance, config);
        var router = new LlmRouter(NullLogger<LlmRouter>.Instance, mixer, config);

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

        // Act & Assert
        await Assert.ThrowsAsync<Exception>(async () =>
            await router.GenerateScriptAsync(providers, brief, spec, "Free", CancellationToken.None));
    }

    [Fact]
    public async Task LlmRouter_Should_TryProProvidersFirstWhenRequested()
    {
        // Arrange
        var proProvider = new Mock<ILlmProvider>();
        proProvider
            .Setup(p => p.DraftScriptAsync(It.IsAny<Brief>(), It.IsAny<PlanSpec>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("# Pro Script\n## Introduction\nPro content");

        var freeProvider = new Mock<ILlmProvider>();
        freeProvider
            .Setup(p => p.DraftScriptAsync(It.IsAny<Brief>(), It.IsAny<PlanSpec>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("# Free Script\n## Introduction\nFree content");

        var providers = new Dictionary<string, ILlmProvider>
        {
            ["OpenAI"] = proProvider.Object,
            ["RuleBased"] = freeProvider.Object
        };

        var config = new ProviderMixingConfig { LogProviderSelection = true };
        var mixer = new ProviderMixer(NullLogger<ProviderMixer>.Instance, config);
        var router = new LlmRouter(NullLogger<LlmRouter>.Instance, mixer, config);

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
        var result = await router.GenerateScriptAsync(providers, brief, spec, "Pro", CancellationToken.None);

        // Assert
        Assert.NotNull(result);
        Assert.Contains("Pro Script", result);
        proProvider.Verify(p => p.DraftScriptAsync(It.IsAny<Brief>(), It.IsAny<PlanSpec>(), It.IsAny<CancellationToken>()), Times.Once);
        freeProvider.Verify(p => p.DraftScriptAsync(It.IsAny<Brief>(), It.IsAny<PlanSpec>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task LlmRouter_Should_SkipEmptyScripts()
    {
        // Arrange
        var emptyProvider = new Mock<ILlmProvider>();
        emptyProvider
            .Setup(p => p.DraftScriptAsync(It.IsAny<Brief>(), It.IsAny<PlanSpec>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("");

        var validProvider = new Mock<ILlmProvider>();
        validProvider
            .Setup(p => p.DraftScriptAsync(It.IsAny<Brief>(), It.IsAny<PlanSpec>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("# Valid Script\n## Introduction\nValid content");

        var providers = new Dictionary<string, ILlmProvider>
        {
            ["Ollama"] = emptyProvider.Object,
            ["RuleBased"] = validProvider.Object
        };

        var config = new ProviderMixingConfig { LogProviderSelection = true };
        var mixer = new ProviderMixer(NullLogger<ProviderMixer>.Instance, config);
        var router = new LlmRouter(NullLogger<LlmRouter>.Instance, mixer, config);

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
        var result = await router.GenerateScriptAsync(providers, brief, spec, "Free", CancellationToken.None);

        // Assert
        Assert.NotNull(result);
        Assert.Contains("Valid Script", result);
        emptyProvider.Verify(p => p.DraftScriptAsync(It.IsAny<Brief>(), It.IsAny<PlanSpec>(), It.IsAny<CancellationToken>()), Times.Once);
        validProvider.Verify(p => p.DraftScriptAsync(It.IsAny<Brief>(), It.IsAny<PlanSpec>(), It.IsAny<CancellationToken>()), Times.Once);
    }
}
