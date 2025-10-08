using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Aura.Core.Models;
using Aura.Core.Providers;
using Microsoft.Extensions.Logging;

#if WINDOWS10_0_19041_0_OR_GREATER
using Windows.Media.SpeechSynthesis;
using Windows.Storage;
#endif

namespace Aura.Providers.Tts;

public class WindowsTtsProvider : ITtsProvider
{
    private readonly ILogger<WindowsTtsProvider> _logger;
#if WINDOWS10_0_19041_0_OR_GREATER
    private readonly SpeechSynthesizer _synthesizer;
#endif
    private readonly string _outputDirectory;

    public WindowsTtsProvider(ILogger<WindowsTtsProvider> logger)
    {
        _logger = logger;
#if WINDOWS10_0_19041_0_OR_GREATER
        _synthesizer = new SpeechSynthesizer();
#endif
        _outputDirectory = Path.Combine(Path.GetTempPath(), "AuraVideoStudio", "TTS");
        
        // Ensure output directory exists
        if (!Directory.Exists(_outputDirectory))
        {
            Directory.CreateDirectory(_outputDirectory);
        }
    }

    public async Task<IReadOnlyList<string>> GetAvailableVoicesAsync()
    {
#if WINDOWS10_0_19041_0_OR_GREATER
        await Task.CompletedTask; // Ensure async behavior
        var voiceNames = new List<string>();
        
        foreach (var voice in SpeechSynthesizer.AllVoices)
        {
            voiceNames.Add(voice.DisplayName);
        }
        
        return voiceNames;
#else
        await Task.CompletedTask;
        return new List<string> { "Microsoft David Desktop", "Microsoft Zira Desktop" };
#endif
    }

    public async Task<string> SynthesizeAsync(IEnumerable<ScriptLine> lines, VoiceSpec spec, CancellationToken ct)
    {
#if WINDOWS10_0_19041_0_OR_GREATER
        _logger.LogInformation("Synthesizing speech with Windows TTS using voice {Voice}", spec.VoiceName);
        
        // Find the requested voice
        VoiceInformation? selectedVoice = null;
        foreach (var voice in SpeechSynthesizer.AllVoices)
        {
            if (voice.DisplayName == spec.VoiceName)
            {
                selectedVoice = voice;
                break;
            }
        }
        
        if (selectedVoice == null)
        {
            _logger.LogWarning("Voice {Voice} not found, using default voice", spec.VoiceName);
            selectedVoice = SpeechSynthesizer.DefaultVoice;
        }
        
        // Set the voice
        _synthesizer.Voice = selectedVoice;
        
        // Prepare the output file
        string outputFilePath = Path.Combine(_outputDirectory, $"narration_{DateTime.Now:yyyyMMddHHmmss}.wav");
        
        // Process each line
        var lineOutputs = new List<string>();
        
        foreach (var line in lines)
        {
            ct.ThrowIfCancellationRequested();
            
            // Create SSML with prosody adjustments
            string ssml = CreateSsml(line.Text, spec);
            
            // Synthesize the speech
            using var stream = await _synthesizer.SynthesizeSsmlToStreamAsync(ssml);
            
            // Save to temp file
            string tempFile = Path.Combine(_outputDirectory, $"line_{line.SceneIndex}.wav");
            lineOutputs.Add(tempFile);
            
            using (var fileStream = new FileStream(tempFile, FileMode.Create))
            {
                await stream.AsStreamForRead().CopyToAsync(fileStream, 81920, ct);
            }
            
            _logger.LogDebug("Synthesized line {Index}: {Text}", line.SceneIndex, 
                line.Text.Length > 30 ? line.Text.Substring(0, 30) + "..." : line.Text);
        }
        
        // Combine all audio files into one
        _logger.LogInformation("Synthesized {Count} lines, combining into final output", lineOutputs.Count);
        
        if (lineOutputs.Count > 0)
        {
            await MergeWavFilesAsync(lineOutputs, outputFilePath, ct);
        }
        
        // Clean up temp files
        foreach (var file in lineOutputs)
        {
            try
            {
                if (File.Exists(file))
                {
                    File.Delete(file);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to delete temporary file {File}", file);
            }
        }
        
        return outputFilePath;
#else
        await Task.CompletedTask;
        _logger.LogWarning("Windows TTS is not available on this platform. Returning stub file path.");
        string outputFilePath = Path.Combine(_outputDirectory, $"narration_{DateTime.Now:yyyyMMddHHmmss}.wav");
        // Create an empty file as a placeholder
        await File.WriteAllBytesAsync(outputFilePath, Array.Empty<byte>(), ct);
        return outputFilePath;
#endif
    }
    
#if WINDOWS10_0_19041_0_OR_GREATER
    private async Task MergeWavFilesAsync(List<string> inputFiles, string outputFile, CancellationToken ct)
    {
        if (inputFiles.Count == 0)
        {
            throw new ArgumentException("No input files to merge");
        }
        
        if (inputFiles.Count == 1)
        {
            File.Copy(inputFiles[0], outputFile, true);
            return;
        }
        
        _logger.LogInformation("Merging {Count} WAV files into master narration track", inputFiles.Count);
        
        // Read WAV headers from first file to get format info
        using var firstFile = new FileStream(inputFiles[0], FileMode.Open, FileAccess.Read);
        using var reader = new BinaryReader(firstFile);
        
        // Read RIFF header
        var riffId = new string(reader.ReadChars(4));
        if (riffId != "RIFF")
        {
            throw new InvalidDataException("Invalid WAV file format");
        }
        
        reader.ReadInt32(); // File size (will recalculate)
        var waveId = new string(reader.ReadChars(4));
        if (waveId != "WAVE")
        {
            throw new InvalidDataException("Invalid WAV file format");
        }
        
        // Read fmt chunk
        var fmtId = new string(reader.ReadChars(4));
        if (fmtId != "fmt ")
        {
            throw new InvalidDataException("Invalid WAV file format");
        }
        
        int fmtSize = reader.ReadInt32();
        var fmtData = reader.ReadBytes(fmtSize);
        
        // Calculate total data size from all files
        long totalDataSize = 0;
        foreach (var file in inputFiles)
        {
            ct.ThrowIfCancellationRequested();
            totalDataSize += GetWavDataSize(file);
        }
        
        // Write merged WAV file
        using var outputStream = new FileStream(outputFile, FileMode.Create, FileAccess.Write);
        using var writer = new BinaryWriter(outputStream);
        
        // Write RIFF header
        writer.Write(new[] { 'R', 'I', 'F', 'F' });
        writer.Write((int)(36 + totalDataSize)); // File size - 8
        writer.Write(new[] { 'W', 'A', 'V', 'E' });
        
        // Write fmt chunk
        writer.Write(new[] { 'f', 'm', 't', ' ' });
        writer.Write(fmtSize);
        writer.Write(fmtData);
        
        // Write data chunk header
        writer.Write(new[] { 'd', 'a', 't', 'a' });
        writer.Write((int)totalDataSize);
        
        // Copy audio data from all files
        foreach (var file in inputFiles)
        {
            ct.ThrowIfCancellationRequested();
            await CopyWavDataAsync(file, writer, ct);
        }
        
        _logger.LogInformation("Merged WAV file created: {Size} bytes", outputStream.Length);
    }
    
    private long GetWavDataSize(string wavFile)
    {
        using var file = new FileStream(wavFile, FileMode.Open, FileAccess.Read);
        using var reader = new BinaryReader(file);
        
        // Skip RIFF header (12 bytes)
        reader.ReadBytes(12);
        
        // Find data chunk
        while (file.Position < file.Length)
        {
            var chunkId = new string(reader.ReadChars(4));
            int chunkSize = reader.ReadInt32();
            
            if (chunkId == "data")
            {
                return chunkSize;
            }
            
            // Skip this chunk
            reader.ReadBytes(chunkSize);
        }
        
        throw new InvalidDataException("No data chunk found in WAV file");
    }
    
    private async Task CopyWavDataAsync(string wavFile, BinaryWriter writer, CancellationToken ct)
    {
        using var file = new FileStream(wavFile, FileMode.Open, FileAccess.Read);
        using var reader = new BinaryReader(file);
        
        // Skip RIFF header (12 bytes)
        reader.ReadBytes(12);
        
        // Find data chunk
        while (file.Position < file.Length)
        {
            var chunkId = new string(reader.ReadChars(4));
            int chunkSize = reader.ReadInt32();
            
            if (chunkId == "data")
            {
                // Copy audio data
                const int bufferSize = 81920;
                byte[] buffer = new byte[bufferSize];
                int remaining = chunkSize;
                
                while (remaining > 0)
                {
                    ct.ThrowIfCancellationRequested();
                    int toRead = Math.Min(remaining, bufferSize);
                    int read = await file.ReadAsync(buffer.AsMemory(0, toRead), ct);
                    if (read == 0) break;
                    
                    writer.Write(buffer, 0, read);
                    remaining -= read;
                }
                
                return;
            }
            
            // Skip this chunk
            reader.ReadBytes(chunkSize);
        }
    }
    
    private string CreateSsml(string text, VoiceSpec spec)
    {
        // Format text with SSML tags including prosody adjustments
        string rateAttribute = $"rate=\"{spec.Rate}\"";
        string pitchAttribute = $"pitch=\"{(spec.Pitch >= 0 ? "+" : "")}{spec.Pitch}st\"";
        
        // Add different pause styles
        string pauseStyle = spec.Pause switch
        {
            PauseStyle.Short => "<break strength=\"weak\"/>",
            PauseStyle.Long => "<break strength=\"strong\"/>",
            PauseStyle.Dramatic => "<break time=\"1s\"/>",
            _ => "<break strength=\"medium\"/>" // Default/Natural
        };
        
        // Replace periods with pause markers (simplified approach)
        text = text.Replace(". ", $". {pauseStyle} ");
        
        // Create final SSML
        return $@"
            <speak version=""1.0"" xmlns=""http://www.w3.org/2001/10/synthesis"" xml:lang=""en-US"">
                <voice name=""{spec.VoiceName}"">
                    <prosody {rateAttribute} {pitchAttribute}>
                        {text}
                    </prosody>
                </voice>
            </speak>";
    }
#endif
}