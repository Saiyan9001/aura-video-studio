using System.Collections.Generic;
using Aura.Core.Models;
using Aura.Core.Orchestrator;
using Aura.Core.Providers;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace Aura.Tests;

public class ProviderMixerTests
{
    [Fact]
    public void SelectLlmProvider_Should_PreferProWhenAvailable()
    {
        // Arrange
        var config = new ProviderMixingConfig { LogProviderSelection = false };
        var mixer = new ProviderMixer(NullLogger<ProviderMixer>.Instance, config);

        var providers = new Dictionary<string, ILlmProvider>
        {
            ["OpenAI"] = Mock.Of<ILlmProvider>(),
            ["RuleBased"] = Mock.Of<ILlmProvider>()
        };

        // Act
        var selection = mixer.SelectLlmProvider(providers, "Pro");

        // Assert
        Assert.Equal("OpenAI", selection.SelectedProvider);
        Assert.False(selection.IsFallback);
        Assert.Equal("Script", selection.Stage);
    }

    [Fact]
    public void SelectLlmProvider_Should_FallbackToFreeWhenProUnavailable()
    {
        // Arrange
        var config = new ProviderMixingConfig { LogProviderSelection = false };
        var mixer = new ProviderMixer(NullLogger<ProviderMixer>.Instance, config);

        var providers = new Dictionary<string, ILlmProvider>
        {
            ["RuleBased"] = Mock.Of<ILlmProvider>()
        };

        // Act
        var selection = mixer.SelectLlmProvider(providers, "Pro");

        // Assert
        Assert.Equal("RuleBased", selection.SelectedProvider);
        Assert.True(selection.IsFallback);
        Assert.NotNull(selection.FallbackFrom);
    }

    [Fact]
    public void SelectLlmProvider_Should_PreferOllamaOverRuleBased()
    {
        // Arrange
        var config = new ProviderMixingConfig { LogProviderSelection = false };
        var mixer = new ProviderMixer(NullLogger<ProviderMixer>.Instance, config);

        var providers = new Dictionary<string, ILlmProvider>
        {
            ["Ollama"] = Mock.Of<ILlmProvider>(),
            ["RuleBased"] = Mock.Of<ILlmProvider>()
        };

        // Act
        var selection = mixer.SelectLlmProvider(providers, "Free");

        // Assert
        Assert.Equal("Ollama", selection.SelectedProvider);
        Assert.False(selection.IsFallback);
    }

    [Fact]
    public void SelectLlmProvider_Should_UseProIfAvailableLogic()
    {
        // Arrange
        var config = new ProviderMixingConfig { LogProviderSelection = false };
        var mixer = new ProviderMixer(NullLogger<ProviderMixer>.Instance, config);

        // Pro available
        var providersWithPro = new Dictionary<string, ILlmProvider>
        {
            ["OpenAI"] = Mock.Of<ILlmProvider>(),
            ["Ollama"] = Mock.Of<ILlmProvider>()
        };

        var selection1 = mixer.SelectLlmProvider(providersWithPro, "ProIfAvailable");
        Assert.Equal("OpenAI", selection1.SelectedProvider);
        Assert.False(selection1.IsFallback);

        // Pro not available
        var providersWithoutPro = new Dictionary<string, ILlmProvider>
        {
            ["Ollama"] = Mock.Of<ILlmProvider>()
        };

        var selection2 = mixer.SelectLlmProvider(providersWithoutPro, "ProIfAvailable");
        Assert.Equal("Ollama", selection2.SelectedProvider);
        Assert.True(selection2.IsFallback);
    }

    [Fact]
    public void SelectTtsProvider_Should_PreferProWhenAvailable()
    {
        // Arrange
        var config = new ProviderMixingConfig { LogProviderSelection = false };
        var mixer = new ProviderMixer(NullLogger<ProviderMixer>.Instance, config);

        var providers = new Dictionary<string, ITtsProvider>
        {
            ["ElevenLabs"] = Mock.Of<ITtsProvider>(),
            ["Windows"] = Mock.Of<ITtsProvider>()
        };

        // Act
        var selection = mixer.SelectTtsProvider(providers, "Pro");

        // Assert
        Assert.Equal("ElevenLabs", selection.SelectedProvider);
        Assert.False(selection.IsFallback);
        Assert.Equal("TTS", selection.Stage);
    }

    [Fact]
    public void SelectTtsProvider_Should_FallbackToWindowsWhenProUnavailable()
    {
        // Arrange
        var config = new ProviderMixingConfig { LogProviderSelection = false };
        var mixer = new ProviderMixer(NullLogger<ProviderMixer>.Instance, config);

        var providers = new Dictionary<string, ITtsProvider>
        {
            ["Windows"] = Mock.Of<ITtsProvider>()
        };

        // Act
        var selection = mixer.SelectTtsProvider(providers, "Pro");

        // Assert
        Assert.Equal("Windows", selection.SelectedProvider);
        Assert.True(selection.IsFallback);
        Assert.Equal("Pro TTS", selection.FallbackFrom);
    }
    
    [Fact]
    public void SelectTtsProvider_Should_UseLinuxMockWhenWindowsUnavailable()
    {
        // Arrange
        var config = new ProviderMixingConfig { LogProviderSelection = false };
        var mixer = new ProviderMixer(NullLogger<ProviderMixer>.Instance, config);

        var providers = new Dictionary<string, ITtsProvider>
        {
            ["LinuxMock"] = Mock.Of<ITtsProvider>()
        };

        // Act
        var selection = mixer.SelectTtsProvider(providers, "Free");

        // Assert
        Assert.Equal("LinuxMock", selection.SelectedProvider);
        Assert.True(selection.IsFallback);
        Assert.Equal("Windows TTS", selection.FallbackFrom);
    }
    
    [Fact]
    public void SelectTtsProvider_Should_PreferWindowsOverLinuxMock()
    {
        // Arrange
        var config = new ProviderMixingConfig { LogProviderSelection = false };
        var mixer = new ProviderMixer(NullLogger<ProviderMixer>.Instance, config);

        var providers = new Dictionary<string, ITtsProvider>
        {
            ["Windows"] = Mock.Of<ITtsProvider>(),
            ["LinuxMock"] = Mock.Of<ITtsProvider>()
        };

        // Act
        var selection = mixer.SelectTtsProvider(providers, "Free");

        // Assert
        Assert.Equal("Windows", selection.SelectedProvider);
        Assert.False(selection.IsFallback);
    }

    [Fact]
    public void SelectVisualProvider_Should_RequireNvidiaForSD()
    {
        // Arrange
        var config = new ProviderMixingConfig { LogProviderSelection = false };
        var mixer = new ProviderMixer(NullLogger<ProviderMixer>.Instance, config);

        var providers = new Dictionary<string, object>
        {
            ["StableDiffusion"] = new object(),
            ["Stock"] = new object()
        };

        // NVIDIA GPU with sufficient VRAM - should use SD
        var selection1 = mixer.SelectVisualProvider(providers, "StockOrLocal", isNvidiaGpu: true, vramGB: 10);
        Assert.Equal("StableDiffusion", selection1.SelectedProvider);

        // AMD GPU with sufficient VRAM - should NOT use SD
        var selection2 = mixer.SelectVisualProvider(providers, "StockOrLocal", isNvidiaGpu: false, vramGB: 16);
        Assert.Equal("Stock", selection2.SelectedProvider);

        // NVIDIA GPU with insufficient VRAM - should NOT use SD
        var selection3 = mixer.SelectVisualProvider(providers, "StockOrLocal", isNvidiaGpu: true, vramGB: 4);
        Assert.Equal("Stock", selection3.SelectedProvider);
    }

    [Fact]
    public void SelectVisualProvider_Should_FallbackToSlideshow()
    {
        // Arrange
        var config = new ProviderMixingConfig { LogProviderSelection = false };
        var mixer = new ProviderMixer(NullLogger<ProviderMixer>.Instance, config);

        var providers = new Dictionary<string, object>(); // No providers

        // Act
        var selection = mixer.SelectVisualProvider(providers, "Pro", isNvidiaGpu: false, vramGB: 0);

        // Assert
        Assert.Equal("Slideshow", selection.SelectedProvider);
        Assert.True(selection.IsFallback);
    }

    [Fact]
    public void ProviderProfile_Should_HaveCorrectPresets()
    {
        // Assert
        var freeOnly = ProviderProfile.FreeOnly;
        Assert.Equal("Free-Only", freeOnly.Name);
        Assert.Equal("Free", freeOnly.Stages["Script"]);
        Assert.Equal("Windows", freeOnly.Stages["TTS"]);
        Assert.Equal("Stock", freeOnly.Stages["Visuals"]);

        var balanced = ProviderProfile.BalancedMix;
        Assert.Equal("Balanced Mix", balanced.Name);
        Assert.Equal("ProIfAvailable", balanced.Stages["Script"]);
        Assert.Equal("StockOrLocal", balanced.Stages["Visuals"]);

        var proMax = ProviderProfile.ProMax;
        Assert.Equal("Pro-Max", proMax.Name);
        Assert.Equal("Pro", proMax.Stages["Script"]);
        Assert.Equal("Pro", proMax.Stages["TTS"]);
        Assert.Equal("Pro", proMax.Stages["Visuals"]);
    }
}
