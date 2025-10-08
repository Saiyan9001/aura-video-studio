using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Aura.Core.Models;
using Aura.Providers.Tts;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Aura.Tests;

public class TtsProviderTests
{
    [Fact]
    public async Task LinuxMockTtsProvider_Should_GenerateValidWavFile()
    {
        // Arrange
        var provider = new LinuxMockTtsProvider(NullLogger<LinuxMockTtsProvider>.Instance);
        var lines = new List<ScriptLine>
        {
            new ScriptLine(0, "Hello world", TimeSpan.FromSeconds(0), TimeSpan.FromSeconds(2)),
            new ScriptLine(1, "This is a test", TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(3))
        };
        var spec = new VoiceSpec("Mock Voice (English)", 1.0, 0, PauseStyle.Natural);

        // Act
        string outputPath = await provider.SynthesizeAsync(lines, spec, CancellationToken.None);

        // Assert
        Assert.True(File.Exists(outputPath), "WAV file should be created");
        
        var fileInfo = new FileInfo(outputPath);
        Assert.True(fileInfo.Length > 0, "WAV file should not be empty");
        
        // Verify WAV header
        using var stream = File.OpenRead(outputPath);
        using var reader = new BinaryReader(stream);
        
        var riffId = new string(reader.ReadChars(4));
        Assert.Equal("RIFF", riffId);
        
        reader.ReadInt32(); // File size
        
        var waveId = new string(reader.ReadChars(4));
        Assert.Equal("WAVE", waveId);
        
        // Cleanup
        if (File.Exists(outputPath))
        {
            File.Delete(outputPath);
        }
    }

    [Fact]
    public async Task LinuxMockTtsProvider_Should_CalculateCorrectDuration()
    {
        // Arrange
        var provider = new LinuxMockTtsProvider(NullLogger<LinuxMockTtsProvider>.Instance);
        var lines = new List<ScriptLine>
        {
            new ScriptLine(0, "Line one", TimeSpan.FromSeconds(0), TimeSpan.FromSeconds(3)),
            new ScriptLine(1, "Line two", TimeSpan.FromSeconds(3), TimeSpan.FromSeconds(2))
        };
        var spec = new VoiceSpec("Mock Voice (English)", 1.0, 0, PauseStyle.Natural);

        // Act
        string outputPath = await provider.SynthesizeAsync(lines, spec, CancellationToken.None);

        // Assert
        Assert.True(File.Exists(outputPath));
        
        // Read WAV file to verify duration matches expected (5 seconds)
        // For 44100 Hz, stereo, 16-bit: 5 seconds = 44100 * 5 * 2 * 2 = 882000 bytes
        var fileInfo = new FileInfo(outputPath);
        const int wavHeaderSize = 44;
        long expectedDataSize = 44100 * 5 * 2 * 2; // sample_rate * duration * channels * bytes_per_sample
        long actualDataSize = fileInfo.Length - wavHeaderSize;
        
        // Allow small tolerance for rounding
        Assert.InRange(actualDataSize, expectedDataSize - 1000, expectedDataSize + 1000);
        
        // Cleanup
        if (File.Exists(outputPath))
        {
            File.Delete(outputPath);
        }
    }

    [Fact]
    public async Task LinuxMockTtsProvider_Should_GetAvailableVoices()
    {
        // Arrange
        var provider = new LinuxMockTtsProvider(NullLogger<LinuxMockTtsProvider>.Instance);

        // Act
        var voices = await provider.GetAvailableVoicesAsync();

        // Assert
        Assert.NotEmpty(voices);
        Assert.Contains("Mock Voice (English)", voices);
    }

    [Fact]
    public void LinuxMockTtsProvider_Should_ThrowOnEmptyLines()
    {
        // Arrange
        var provider = new LinuxMockTtsProvider(NullLogger<LinuxMockTtsProvider>.Instance);
        var lines = new List<ScriptLine>();
        var spec = new VoiceSpec("Mock Voice (English)", 1.0, 0, PauseStyle.Natural);

        // Act & Assert
        Assert.ThrowsAsync<ArgumentException>(async () =>
            await provider.SynthesizeAsync(lines, spec, CancellationToken.None));
    }

    [Fact]
    public void ElevenLabsTtsProvider_Should_ThrowInOfflineMode()
    {
        // Arrange
        var provider = new ElevenLabsTtsProvider(
            NullLogger<ElevenLabsTtsProvider>.Instance,
            "fake-api-key",
            offlineOnly: true);
        
        var lines = new List<ScriptLine>
        {
            new ScriptLine(0, "Test", TimeSpan.FromSeconds(0), TimeSpan.FromSeconds(2))
        };
        var spec = new VoiceSpec("Test Voice", 1.0, 0, PauseStyle.Natural);

        // Act & Assert
        Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await provider.SynthesizeAsync(lines, spec, CancellationToken.None));
    }

    [Fact]
    public async Task ElevenLabsTtsProvider_Should_ReturnEmptyVoicesInOfflineMode()
    {
        // Arrange
        var provider = new ElevenLabsTtsProvider(
            NullLogger<ElevenLabsTtsProvider>.Instance,
            "fake-api-key",
            offlineOnly: true);

        // Act
        var voices = await provider.GetAvailableVoicesAsync();

        // Assert
        Assert.Empty(voices);
    }

    [Fact]
    public void PlayHTTtsProvider_Should_ThrowInOfflineMode()
    {
        // Arrange
        var provider = new PlayHTTtsProvider(
            NullLogger<PlayHTTtsProvider>.Instance,
            "fake-api-key",
            "fake-user-id",
            offlineOnly: true);
        
        var lines = new List<ScriptLine>
        {
            new ScriptLine(0, "Test", TimeSpan.FromSeconds(0), TimeSpan.FromSeconds(2))
        };
        var spec = new VoiceSpec("Test Voice", 1.0, 0, PauseStyle.Natural);

        // Act & Assert
        Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await provider.SynthesizeAsync(lines, spec, CancellationToken.None));
    }

    [Fact]
    public async Task PlayHTTtsProvider_Should_ReturnEmptyVoicesInOfflineMode()
    {
        // Arrange
        var provider = new PlayHTTtsProvider(
            NullLogger<PlayHTTtsProvider>.Instance,
            "fake-api-key",
            "fake-user-id",
            offlineOnly: true);

        // Act
        var voices = await provider.GetAvailableVoicesAsync();

        // Assert
        Assert.Empty(voices);
    }

    [Fact]
    public void ElevenLabsTtsProvider_Should_RequireApiKey()
    {
        // Arrange
        var provider = new ElevenLabsTtsProvider(
            NullLogger<ElevenLabsTtsProvider>.Instance,
            "",
            offlineOnly: false);
        
        var lines = new List<ScriptLine>
        {
            new ScriptLine(0, "Test", TimeSpan.FromSeconds(0), TimeSpan.FromSeconds(2))
        };
        var spec = new VoiceSpec("Test Voice", 1.0, 0, PauseStyle.Natural);

        // Act & Assert
        Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await provider.SynthesizeAsync(lines, spec, CancellationToken.None));
    }

    [Fact]
    public void PlayHTTtsProvider_Should_RequireApiKey()
    {
        // Arrange
        var provider = new PlayHTTtsProvider(
            NullLogger<PlayHTTtsProvider>.Instance,
            "",
            "",
            offlineOnly: false);
        
        var lines = new List<ScriptLine>
        {
            new ScriptLine(0, "Test", TimeSpan.FromSeconds(0), TimeSpan.FromSeconds(2))
        };
        var spec = new VoiceSpec("Test Voice", 1.0, 0, PauseStyle.Natural);

        // Act & Assert
        Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await provider.SynthesizeAsync(lines, spec, CancellationToken.None));
    }
}
