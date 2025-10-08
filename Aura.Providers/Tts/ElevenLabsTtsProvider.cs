using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Aura.Core.Models;
using Aura.Core.Providers;
using Microsoft.Extensions.Logging;

namespace Aura.Providers.Tts;

/// <summary>
/// ElevenLabs TTS provider (Pro tier).
/// Provides high-quality neural voice synthesis.
/// </summary>
public class ElevenLabsTtsProvider : ITtsProvider
{
    private readonly ILogger<ElevenLabsTtsProvider> _logger;
    private readonly string _apiKey;
    private readonly bool _offlineOnly;
    private readonly string _outputDirectory;
    private readonly HttpClient _httpClient;
    private const string ApiBaseUrl = "https://api.elevenlabs.io/v1";

    public ElevenLabsTtsProvider(
        ILogger<ElevenLabsTtsProvider> logger,
        string apiKey,
        bool offlineOnly = false)
    {
        _logger = logger;
        _apiKey = apiKey;
        _offlineOnly = offlineOnly;
        _outputDirectory = Path.Combine(Path.GetTempPath(), "AuraVideoStudio", "TTS");
        _httpClient = new HttpClient();
        
        // Only add header if key is not empty
        if (!string.IsNullOrWhiteSpace(_apiKey))
        {
            _httpClient.DefaultRequestHeaders.Add("xi-api-key", _apiKey);
        }
        
        if (!Directory.Exists(_outputDirectory))
        {
            Directory.CreateDirectory(_outputDirectory);
        }
    }

    public async Task<IReadOnlyList<string>> GetAvailableVoicesAsync()
    {
        if (_offlineOnly)
        {
            _logger.LogWarning("OfflineOnly mode enabled, ElevenLabs voices unavailable");
            return Array.Empty<string>();
        }

        try
        {
            _logger.LogInformation("Fetching ElevenLabs voices");
            var response = await _httpClient.GetAsync($"{ApiBaseUrl}/voices");
            response.EnsureSuccessStatusCode();
            
            var json = await response.Content.ReadAsStringAsync();
            var voicesDoc = JsonDocument.Parse(json);
            var voices = new List<string>();
            
            if (voicesDoc.RootElement.TryGetProperty("voices", out var voicesArray))
            {
                foreach (var voice in voicesArray.EnumerateArray())
                {
                    if (voice.TryGetProperty("name", out var name))
                    {
                        voices.Add(name.GetString() ?? "Unknown");
                    }
                }
            }
            
            _logger.LogInformation("Retrieved {Count} ElevenLabs voices", voices.Count);
            return voices;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to fetch ElevenLabs voices");
            return Array.Empty<string>();
        }
    }

    public async Task<string> SynthesizeAsync(IEnumerable<ScriptLine> lines, VoiceSpec spec, CancellationToken ct)
    {
        if (_offlineOnly)
        {
            _logger.LogWarning("OfflineOnly mode enabled, cannot use ElevenLabs TTS");
            throw new InvalidOperationException("ElevenLabs TTS is not available in offline mode");
        }

        if (string.IsNullOrWhiteSpace(_apiKey))
        {
            _logger.LogWarning("ElevenLabs API key not configured");
            throw new InvalidOperationException("ElevenLabs API key is required");
        }

        _logger.LogInformation("Synthesizing speech with ElevenLabs TTS");
        
        var linesList = lines.ToList();
        if (linesList.Count == 0)
        {
            throw new ArgumentException("No script lines provided for synthesis");
        }

        // Validate API key with a short smoke test
        await ValidateApiKeyAsync(ct);

        // Combine all text for synthesis
        string combinedText = string.Join(" ", linesList.Select(l => l.Text));
        
        // Use default ElevenLabs voice (can be made configurable)
        string voiceId = await GetDefaultVoiceIdAsync(ct);
        
        string outputFilePath = Path.Combine(_outputDirectory, $"narration_elevenlabs_{DateTime.Now:yyyyMMddHHmmss}.mp3");
        
        await SynthesizeTextAsync(combinedText, voiceId, outputFilePath, ct);
        
        _logger.LogInformation("ElevenLabs TTS audio generated at: {Path}", outputFilePath);
        return outputFilePath;
    }

    private async Task ValidateApiKeyAsync(CancellationToken ct)
    {
        try
        {
            _logger.LogInformation("Validating ElevenLabs API key with smoke test");
            
            // Short smoke test: synthesize a single word
            var testPayload = new
            {
                text = "Test",
                model_id = "eleven_monolingual_v1"
            };
            
            string voiceId = "21m00Tcm4TlvDq8ikWAM"; // Default voice
            var content = new StringContent(
                JsonSerializer.Serialize(testPayload),
                Encoding.UTF8,
                "application/json");
            
            var response = await _httpClient.PostAsync(
                $"{ApiBaseUrl}/text-to-speech/{voiceId}",
                content,
                ct);
            
            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadAsStringAsync(ct);
                throw new InvalidOperationException($"ElevenLabs API key validation failed: {error}");
            }
            
            _logger.LogInformation("ElevenLabs API key validated successfully");
        }
        catch (Exception ex) when (ex is not InvalidOperationException)
        {
            _logger.LogError(ex, "ElevenLabs API key validation failed");
            throw new InvalidOperationException("Failed to validate ElevenLabs API key", ex);
        }
    }

    private async Task<string> GetDefaultVoiceIdAsync(CancellationToken ct)
    {
        try
        {
            var response = await _httpClient.GetAsync($"{ApiBaseUrl}/voices", ct);
            response.EnsureSuccessStatusCode();
            
            var json = await response.Content.ReadAsStringAsync(ct);
            var voicesDoc = JsonDocument.Parse(json);
            
            if (voicesDoc.RootElement.TryGetProperty("voices", out var voicesArray))
            {
                var firstVoice = voicesArray.EnumerateArray().FirstOrDefault();
                if (firstVoice.TryGetProperty("voice_id", out var voiceId))
                {
                    return voiceId.GetString() ?? "21m00Tcm4TlvDq8ikWAM";
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to get default voice, using fallback");
        }
        
        return "21m00Tcm4TlvDq8ikWAM"; // Fallback default voice
    }

    private async Task SynthesizeTextAsync(string text, string voiceId, string outputPath, CancellationToken ct)
    {
        var payload = new
        {
            text,
            model_id = "eleven_monolingual_v1",
            voice_settings = new
            {
                stability = 0.5,
                similarity_boost = 0.75
            }
        };
        
        var content = new StringContent(
            JsonSerializer.Serialize(payload),
            Encoding.UTF8,
            "application/json");
        
        var response = await _httpClient.PostAsync(
            $"{ApiBaseUrl}/text-to-speech/{voiceId}",
            content,
            ct);
        
        response.EnsureSuccessStatusCode();
        
        var audioData = await response.Content.ReadAsByteArrayAsync(ct);
        await File.WriteAllBytesAsync(outputPath, audioData, ct);
        
        _logger.LogInformation("Synthesized {Length} bytes of audio", audioData.Length);
    }
}
