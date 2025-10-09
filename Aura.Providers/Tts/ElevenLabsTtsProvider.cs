using System;
using System.Collections.Generic;
using System.IO;
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
/// ElevenLabs TTS provider for pro-tier text-to-speech synthesis
/// </summary>
public class ElevenLabsTtsProvider : ITtsProvider
{
    private readonly ILogger<ElevenLabsTtsProvider> _logger;
    private readonly string _apiKey;
    private readonly string _outputDirectory;
    private readonly HttpClient _httpClient;
    private readonly bool _offlineOnly;
    private const string ApiBaseUrl = "https://api.elevenlabs.io/v1";

    public ElevenLabsTtsProvider(
        ILogger<ElevenLabsTtsProvider> logger, 
        string apiKey,
        bool offlineOnly = false)
    {
        _logger = logger;
        _apiKey = apiKey;
        _offlineOnly = offlineOnly;
        _httpClient = new HttpClient();
        _httpClient.DefaultRequestHeaders.Add("xi-api-key", _apiKey);
        _outputDirectory = Path.Combine(Path.GetTempPath(), "AuraVideoStudio", "TTS");
        
        if (!Directory.Exists(_outputDirectory))
        {
            Directory.CreateDirectory(_outputDirectory);
        }
    }

    public async Task<IReadOnlyList<string>> GetAvailableVoicesAsync()
    {
        if (_offlineOnly)
        {
            _logger.LogWarning("OfflineOnly mode enabled, cannot fetch ElevenLabs voices");
            return new List<string> { "Rachel", "Antoni", "Domi", "Bella" }; // Default voice list
        }

        try
        {
            var response = await _httpClient.GetAsync($"{ApiBaseUrl}/voices");
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Failed to fetch ElevenLabs voices: {Status}", response.StatusCode);
                return new List<string> { "Rachel", "Antoni", "Domi", "Bella" };
            }

            var json = await response.Content.ReadAsStringAsync();
            var doc = JsonDocument.Parse(json);
            var voices = new List<string>();

            if (doc.RootElement.TryGetProperty("voices", out var voicesArray))
            {
                foreach (var voice in voicesArray.EnumerateArray())
                {
                    if (voice.TryGetProperty("name", out var name))
                    {
                        voices.Add(name.GetString() ?? "Unknown");
                    }
                }
            }

            return voices;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching ElevenLabs voices");
            return new List<string> { "Rachel", "Antoni", "Domi", "Bella" };
        }
    }

    public async Task<string> SynthesizeAsync(IEnumerable<ScriptLine> lines, VoiceSpec spec, CancellationToken ct)
    {
        if (_offlineOnly)
        {
            _logger.LogWarning("OfflineOnly mode enabled, cannot use ElevenLabs");
            throw new InvalidOperationException("ElevenLabs requires online access. Please use Windows TTS for offline mode.");
        }

        // Validate API key first with a smoke test
        if (!await ValidateApiKeyAsync(ct))
        {
            throw new InvalidOperationException("Invalid ElevenLabs API key");
        }

        _logger.LogInformation("Synthesizing speech with ElevenLabs using voice {Voice}", spec.VoiceName);
        
        var lineOutputs = new List<string>();
        
        foreach (var line in lines)
        {
            ct.ThrowIfCancellationRequested();
            
            try
            {
                // Get voice ID (simplified - in production would cache this)
                var voiceId = await GetVoiceIdByName(spec.VoiceName, ct) ?? "21m00Tcm4TlvDq8ikWAM"; // Default to Rachel
                
                // Build request
                var requestBody = new
                {
                    text = line.Text,
                    model_id = "eleven_monolingual_v1",
                    voice_settings = new
                    {
                        stability = 0.5,
                        similarity_boost = 0.75,
                        style = 0.0,
                        use_speaker_boost = true
                    }
                };

                var json = JsonSerializer.Serialize(requestBody);
                var content = new StringContent(json, Encoding.UTF8, "application/json");
                
                var response = await _httpClient.PostAsync(
                    $"{ApiBaseUrl}/text-to-speech/{voiceId}", 
                    content, 
                    ct);

                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogWarning("Failed to synthesize line {Index}: {Status}", line.SceneIndex, response.StatusCode);
                    throw new HttpRequestException($"ElevenLabs API returned {response.StatusCode}");
                }

                // Save audio file
                string tempFile = Path.Combine(_outputDirectory, $"line_{line.SceneIndex}.mp3");
                using (var fileStream = new FileStream(tempFile, FileMode.Create))
                {
                    await response.Content.CopyToAsync(fileStream, ct);
                }
                
                lineOutputs.Add(tempFile);
                _logger.LogDebug("Synthesized line {Index}: {Text}", line.SceneIndex, 
                    line.Text.Length > 30 ? line.Text.Substring(0, 30) + "..." : line.Text);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to synthesize line {Index}", line.SceneIndex);
                throw;
            }
        }
        
        // Combine audio files
        string outputFilePath = Path.Combine(_outputDirectory, $"narration_elevenlabs_{DateTime.Now:yyyyMMddHHmmss}.mp3");
        
        if (lineOutputs.Count > 0)
        {
            // For now, just use the first file. In production, would use ffmpeg to concatenate
            File.Copy(lineOutputs[0], outputFilePath, true);
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
        
        _logger.LogInformation("ElevenLabs synthesis complete: {Path}", outputFilePath);
        return outputFilePath;
    }

    private async Task<bool> ValidateApiKeyAsync(CancellationToken ct)
    {
        try
        {
            // Perform a smoke test by fetching user info
            var response = await _httpClient.GetAsync($"{ApiBaseUrl}/user", ct);
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to validate ElevenLabs API key");
            return false;
        }
    }

    private async Task<string?> GetVoiceIdByName(string voiceName, CancellationToken ct)
    {
        try
        {
            var response = await _httpClient.GetAsync($"{ApiBaseUrl}/voices", ct);
            if (!response.IsSuccessStatusCode)
            {
                return null;
            }

            var json = await response.Content.ReadAsStringAsync(ct);
            var doc = JsonDocument.Parse(json);

            if (doc.RootElement.TryGetProperty("voices", out var voicesArray))
            {
                foreach (var voice in voicesArray.EnumerateArray())
                {
                    if (voice.TryGetProperty("name", out var name) && 
                        voice.TryGetProperty("voice_id", out var voiceId))
                    {
                        if (name.GetString()?.Equals(voiceName, StringComparison.OrdinalIgnoreCase) == true)
                        {
                            return voiceId.GetString();
                        }
                    }
                }
            }

            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get voice ID for {VoiceName}", voiceName);
            return null;
        }
    }

    public void Dispose()
    {
        _httpClient?.Dispose();
    }
}
