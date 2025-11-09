using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Aura.Core.Models;
using Aura.Core.Orchestrator;
using Aura.Core.Providers;
using Aura.Providers.Tts;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace Aura.Tests;

/// <summary>
/// Tests for TTS Provider Selection Architecture
/// </summary>
public class TtsProviderSelectionTests
{
    [Fact]
    public void ProviderMixer_Should_SelectElevenLabsForProTier()
    {
        // Arrange
        var config = new ProviderMixingConfig { LogProviderSelection = false };
        var mixer = new ProviderMixer(NullLogger<ProviderMixer>.Instance, config);

        var providers = new Dictionary<string, ITtsProvider>
        {
            ["ElevenLabs"] = Mock.Of<ITtsProvider>(),
            ["OpenAI"] = Mock.Of<ITtsProvider>(),
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
    public void ProviderMixer_Should_FallbackToOpenAIWhenElevenLabsUnavailable()
    {
        // Arrange
        var config = new ProviderMixingConfig { LogProviderSelection = false };
        var mixer = new ProviderMixer(NullLogger<ProviderMixer>.Instance, config);

        var providers = new Dictionary<string, ITtsProvider>
        {
            ["OpenAI"] = Mock.Of<ITtsProvider>(),
            ["Azure"] = Mock.Of<ITtsProvider>(),
            ["Windows"] = Mock.Of<ITtsProvider>()
        };

        // Act
        var selection = mixer.SelectTtsProvider(providers, "Pro");

        // Assert
        Assert.Equal("OpenAI", selection.SelectedProvider);
        Assert.False(selection.IsFallback);
    }

    [Fact]
    public void ProviderMixer_Should_SelectEdgeTTSForFreeTier()
    {
        // Arrange
        var config = new ProviderMixingConfig { LogProviderSelection = false };
        var mixer = new ProviderMixer(NullLogger<ProviderMixer>.Instance, config);

        var providers = new Dictionary<string, ITtsProvider>
        {
            ["EdgeTTS"] = Mock.Of<ITtsProvider>(),
            ["Piper"] = Mock.Of<ITtsProvider>(),
            ["Windows"] = Mock.Of<ITtsProvider>()
        };

        // Act
        var selection = mixer.SelectTtsProvider(providers, "Free");

        // Assert
        Assert.Equal("EdgeTTS", selection.SelectedProvider);
        Assert.False(selection.IsFallback);
    }

    [Fact]
    public void ProviderMixer_Should_FallbackToWindowsWhenAllElseFails()
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
        Assert.NotNull(selection.FallbackFrom);
    }

    [Fact]
    public void ProviderMixer_Should_UseNullAsUltimateFallback()
    {
        // Arrange
        var config = new ProviderMixingConfig { LogProviderSelection = false };
        var mixer = new ProviderMixer(NullLogger<ProviderMixer>.Instance, config);

        var providers = new Dictionary<string, ITtsProvider>();

        // Act
        var selection = mixer.SelectTtsProvider(providers, "Pro");

        // Assert
        Assert.Equal("Null", selection.SelectedProvider);
        Assert.True(selection.IsFallback);
        Assert.Equal("All TTS providers", selection.FallbackFrom);
    }

    [Fact]
    public void ProviderMixer_Should_SelectProvidersInCorrectOrder()
    {
        // Arrange
        var config = new ProviderMixingConfig { LogProviderSelection = false };
        var mixer = new ProviderMixer(NullLogger<ProviderMixer>.Instance, config);

        // Test Pro tier cascade: ElevenLabs → OpenAI → PlayHT → Azure
        var allProviders = new Dictionary<string, ITtsProvider>
        {
            ["ElevenLabs"] = Mock.Of<ITtsProvider>(),
            ["OpenAI"] = Mock.Of<ITtsProvider>(),
            ["PlayHT"] = Mock.Of<ITtsProvider>(),
            ["Azure"] = Mock.Of<ITtsProvider>(),
            ["EdgeTTS"] = Mock.Of<ITtsProvider>(),
            ["Windows"] = Mock.Of<ITtsProvider>()
        };

        // Act & Assert - ElevenLabs should be first choice
        var selection1 = mixer.SelectTtsProvider(allProviders, "Pro");
        Assert.Equal("ElevenLabs", selection1.SelectedProvider);

        // Remove ElevenLabs - should get OpenAI
        var withoutElevenLabs = allProviders.Where(p => p.Key != "ElevenLabs").ToDictionary(p => p.Key, p => p.Value);
        var selection2 = mixer.SelectTtsProvider(withoutElevenLabs, "Pro");
        Assert.Equal("OpenAI", selection2.SelectedProvider);

        // Remove OpenAI - should get PlayHT
        var withoutOpenAI = withoutElevenLabs.Where(p => p.Key != "OpenAI").ToDictionary(p => p.Key, p => p.Value);
        var selection3 = mixer.SelectTtsProvider(withoutOpenAI, "Pro");
        Assert.Equal("PlayHT", selection3.SelectedProvider);

        // Remove PlayHT - should get Azure
        var withoutPlayHT = withoutOpenAI.Where(p => p.Key != "PlayHT").ToDictionary(p => p.Key, p => p.Value);
        var selection4 = mixer.SelectTtsProvider(withoutPlayHT, "Pro");
        Assert.Equal("Azure", selection4.SelectedProvider);

        // Remove Azure - should get EdgeTTS
        var withoutAzure = withoutPlayHT.Where(p => p.Key != "Azure").ToDictionary(p => p.Key, p => p.Value);
        var selection5 = mixer.SelectTtsProvider(withoutAzure, "Pro");
        Assert.Equal("EdgeTTS", selection5.SelectedProvider);
        Assert.True(selection5.IsFallback);
    }

    [Fact]
    public void ProviderMixer_Should_HandleProIfAvailableCorrectly()
    {
        // Arrange
        var config = new ProviderMixingConfig { LogProviderSelection = false };
        var mixer = new ProviderMixer(NullLogger<ProviderMixer>.Instance, config);

        // Test with Pro provider available
        var withPro = new Dictionary<string, ITtsProvider>
        {
            ["ElevenLabs"] = Mock.Of<ITtsProvider>(),
            ["EdgeTTS"] = Mock.Of<ITtsProvider>()
        };

        var selection1 = mixer.SelectTtsProvider(withPro, "ProIfAvailable");
        Assert.Equal("ElevenLabs", selection1.SelectedProvider);
        Assert.False(selection1.IsFallback);

        // Test without Pro provider - should gracefully downgrade
        var withoutPro = new Dictionary<string, ITtsProvider>
        {
            ["EdgeTTS"] = Mock.Of<ITtsProvider>(),
            ["Windows"] = Mock.Of<ITtsProvider>()
        };

        var selection2 = mixer.SelectTtsProvider(withoutPro, "ProIfAvailable");
        Assert.Equal("EdgeTTS", selection2.SelectedProvider);
        Assert.False(selection2.IsFallback);
    }

    [Fact]
    public void ProviderMixer_Should_NormalizeProviderNames()
    {
        // Arrange
        var config = new ProviderMixingConfig { LogProviderSelection = false };
        var mixer = new ProviderMixer(NullLogger<ProviderMixer>.Instance, config);

        var providers = new Dictionary<string, ITtsProvider>
        {
            ["EdgeTTS"] = Mock.Of<ITtsProvider>(),
            ["Windows"] = Mock.Of<ITtsProvider>()
        };

        // Act - Test various name formats
        var selection1 = mixer.SelectTtsProvider(providers, "edgetts");
        var selection2 = mixer.SelectTtsProvider(providers, "Edge");
        var selection3 = mixer.SelectTtsProvider(providers, "windows");
        var selection4 = mixer.SelectTtsProvider(providers, "System");

        // Assert
        Assert.Equal("EdgeTTS", selection1.SelectedProvider);
        Assert.Equal("EdgeTTS", selection2.SelectedProvider);
        Assert.Equal("Windows", selection3.SelectedProvider);
        Assert.Equal("Windows", selection4.SelectedProvider);
    }
}

/// <summary>
/// Tests for BaseTtsProvider behavior
/// </summary>
public class BaseTtsProviderTests
{
    private class TestTtsProvider : BaseTtsProvider
    {
        private readonly Func<IEnumerable<ScriptLine>, VoiceSpec, CancellationToken, Task<string>>? _generateFunc;
        private readonly Func<Task<IReadOnlyList<string>>>? _getVoicesFunc;
        private int _callCount;

        public int CallCount => _callCount;

        public TestTtsProvider(
            Func<IEnumerable<ScriptLine>, VoiceSpec, CancellationToken, Task<string>>? generateFunc = null,
            Func<Task<IReadOnlyList<string>>>? getVoicesFunc = null,
            int maxRetries = 3)
            : base(NullLogger<TestTtsProvider>.Instance, maxRetries, baseRetryDelayMs: 10)
        {
            _generateFunc = generateFunc;
            _getVoicesFunc = getVoicesFunc;
            _callCount = 0;
        }

        protected override string GetProviderName() => "TestProvider";

        protected override async Task<string> GenerateAudioCoreAsync(
            IEnumerable<ScriptLine> lines,
            VoiceSpec spec,
            CancellationToken ct)
        {
            Interlocked.Increment(ref _callCount);
            
            if (_generateFunc != null)
            {
                return await _generateFunc(lines, spec, ct);
            }

            return GenerateOutputPath("Test", spec.VoiceName);
        }

        protected override async Task<IReadOnlyList<string>> GetAvailableVoicesCoreAsync()
        {
            if (_getVoicesFunc != null)
            {
                return await _getVoicesFunc();
            }

            return new[] { "TestVoice1", "TestVoice2" };
        }
    }

    [Fact]
    public async Task BaseTtsProvider_Should_RetryOnFailure()
    {
        // Arrange
        int attemptCount = 0;
        var provider = new TestTtsProvider(
            generateFunc: async (lines, spec, ct) =>
            {
                attemptCount++;
                if (attemptCount < 3)
                {
                    throw new InvalidOperationException("Simulated failure");
                }
                await Task.CompletedTask;
                return "/tmp/test.wav";
            },
            maxRetries: 3
        );

        var lines = new List<ScriptLine>
        {
            new ScriptLine(0, "Test", TimeSpan.Zero, TimeSpan.FromSeconds(1))
        };
        var voiceSpec = new VoiceSpec("TestVoice", 1.0, 0.0, PauseStyle.Natural);

        // Act
        var result = await provider.SynthesizeAsync(lines, voiceSpec, CancellationToken.None);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(3, attemptCount);
        Assert.Equal(3, provider.CallCount);
    }

    [Fact]
    public async Task BaseTtsProvider_Should_FailAfterMaxRetries()
    {
        // Arrange
        var provider = new TestTtsProvider(
            generateFunc: (lines, spec, ct) => throw new InvalidOperationException("Persistent failure"),
            maxRetries: 3
        );

        var lines = new List<ScriptLine>
        {
            new ScriptLine(0, "Test", TimeSpan.Zero, TimeSpan.FromSeconds(1))
        };
        var voiceSpec = new VoiceSpec("TestVoice", 1.0, 0.0, PauseStyle.Natural);

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
        {
            await provider.SynthesizeAsync(lines, voiceSpec, CancellationToken.None);
        });

        Assert.Equal(3, provider.CallCount);
    }

    [Fact]
    public async Task BaseTtsProvider_Should_HandleCancellation()
    {
        // Arrange
        var cts = new CancellationTokenSource();
        var provider = new TestTtsProvider(
            generateFunc: async (lines, spec, ct) =>
            {
                ct.ThrowIfCancellationRequested();
                await Task.Delay(100, ct);
                return "/tmp/test.wav";
            }
        );

        var lines = new List<ScriptLine>
        {
            new ScriptLine(0, "Test", TimeSpan.Zero, TimeSpan.FromSeconds(1))
        };
        var voiceSpec = new VoiceSpec("TestVoice", 1.0, 0.0, PauseStyle.Natural);

        // Act
        cts.Cancel();

        // Assert - TaskCanceledException inherits from OperationCanceledException
        var exception = await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
        {
            await provider.SynthesizeAsync(lines, voiceSpec, cts.Token);
        });
        
        Assert.True(exception is OperationCanceledException);
    }

    [Fact]
    public async Task BaseTtsProvider_Should_GetVoicesWithRetry()
    {
        // Arrange
        int attemptCount = 0;
        var provider = new TestTtsProvider(
            getVoicesFunc: async () =>
            {
                attemptCount++;
                if (attemptCount < 2)
                {
                    throw new InvalidOperationException("Simulated failure");
                }
                await Task.CompletedTask;
                return new[] { "Voice1", "Voice2" } as IReadOnlyList<string>;
            },
            maxRetries: 3
        );

        // Act
        var voices = await provider.GetAvailableVoicesAsync();

        // Assert
        Assert.NotEmpty(voices);
        Assert.Equal(2, voices.Count);
        Assert.Equal(2, attemptCount);
    }

    [Fact]
    public async Task BaseTtsProvider_Should_ReturnEmptyVoicesOnPersistentFailure()
    {
        // Arrange
        var provider = new TestTtsProvider(
            getVoicesFunc: () => throw new InvalidOperationException("Persistent failure"),
            maxRetries: 3
        );

        // Act
        var voices = await provider.GetAvailableVoicesAsync();

        // Assert
        Assert.Empty(voices);
    }
}
