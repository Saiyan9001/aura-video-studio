using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Aura.Core.Models;
using Aura.Core.Providers;
using Microsoft.Extensions.Logging;

namespace Aura.Providers.Tts;

/// <summary>
/// Mock TTS provider for Linux CI environments.
/// Generates deterministic beep/silence WAV files with correct duration.
/// </summary>
public class LinuxMockTtsProvider : ITtsProvider
{
    private readonly ILogger<LinuxMockTtsProvider> _logger;
    private readonly string _outputDirectory;

    public LinuxMockTtsProvider(ILogger<LinuxMockTtsProvider> logger)
    {
        _logger = logger;
        _outputDirectory = Path.Combine(Path.GetTempPath(), "AuraVideoStudio", "TTS");
        
        if (!Directory.Exists(_outputDirectory))
        {
            Directory.CreateDirectory(_outputDirectory);
        }
    }

    public Task<IReadOnlyList<string>> GetAvailableVoicesAsync()
    {
        _logger.LogInformation("LinuxMockTtsProvider returning mock voices");
        return Task.FromResult<IReadOnlyList<string>>(new List<string> 
        { 
            "Mock Voice (English)", 
            "Mock Voice (Spanish)" 
        });
    }

    public async Task<string> SynthesizeAsync(IEnumerable<ScriptLine> lines, VoiceSpec spec, CancellationToken ct)
    {
        _logger.LogInformation("LinuxMockTtsProvider generating mock TTS audio");
        
        var linesList = lines.ToList();
        if (linesList.Count == 0)
        {
            throw new ArgumentException("No script lines provided for synthesis");
        }

        // Calculate total duration
        var totalDuration = linesList.Sum(l => l.Duration.TotalSeconds);
        _logger.LogInformation("Mock TTS: Generating {Duration}s of audio for {Count} lines", 
            totalDuration, linesList.Count);

        // Generate deterministic WAV file
        string outputFilePath = Path.Combine(_outputDirectory, $"narration_mock_{DateTime.Now:yyyyMMddHHmmss}.wav");
        
        // Create a simple WAV file with silence
        // WAV format: RIFF header + fmt chunk + data chunk
        await GenerateWavFileAsync(outputFilePath, totalDuration, ct);
        
        _logger.LogInformation("Mock TTS audio generated at: {Path}", outputFilePath);
        return outputFilePath;
    }

    private async Task GenerateWavFileAsync(string filePath, double durationSeconds, CancellationToken ct)
    {
        // WAV file parameters
        const int sampleRate = 44100;
        const short channels = 2;
        const short bitsPerSample = 16;
        int bytesPerSample = bitsPerSample / 8;
        int blockAlign = channels * bytesPerSample;
        int byteRate = sampleRate * blockAlign;
        
        // Calculate number of samples needed
        int numSamples = (int)(sampleRate * durationSeconds);
        int dataSize = numSamples * blockAlign;
        
        using var fileStream = new FileStream(filePath, FileMode.Create, FileAccess.Write);
        using var writer = new BinaryWriter(fileStream);
        
        // Write RIFF header
        writer.Write(new[] { 'R', 'I', 'F', 'F' });
        writer.Write(36 + dataSize); // File size - 8
        writer.Write(new[] { 'W', 'A', 'V', 'E' });
        
        // Write fmt chunk
        writer.Write(new[] { 'f', 'm', 't', ' ' });
        writer.Write(16); // fmt chunk size
        writer.Write((short)1); // Audio format (1 = PCM)
        writer.Write(channels);
        writer.Write(sampleRate);
        writer.Write(byteRate);
        writer.Write((short)blockAlign);
        writer.Write(bitsPerSample);
        
        // Write data chunk header
        writer.Write(new[] { 'd', 'a', 't', 'a' });
        writer.Write(dataSize);
        
        // Write deterministic audio data (simple sine wave tone at 440Hz for first 100ms, then silence)
        const int toneMs = 100;
        int toneSamples = (sampleRate * toneMs) / 1000;
        double frequency = 440.0; // A4 note
        
        for (int i = 0; i < numSamples; i++)
        {
            ct.ThrowIfCancellationRequested();
            
            short sampleValue;
            if (i < toneSamples)
            {
                // Generate a brief tone (beep)
                double time = (double)i / sampleRate;
                double amplitude = 0.3 * Math.Sin(2 * Math.PI * frequency * time);
                sampleValue = (short)(amplitude * short.MaxValue);
            }
            else
            {
                // Silence
                sampleValue = 0;
            }
            
            // Write sample for both channels
            writer.Write(sampleValue);
            writer.Write(sampleValue);
        }
        
        writer.Flush();
        await fileStream.FlushAsync(ct);
        _logger.LogDebug("Generated {Size} byte WAV file with {Duration}s duration", 
            fileStream.Length, durationSeconds);
    }
}
