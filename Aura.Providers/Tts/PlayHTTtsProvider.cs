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
/// PlayHT TTS provider for pro-tier text-to-speech synthesis
/// </summary>
public class PlayHTTtsProvider : ITtsProvider
{
    private readonly ILogger<PlayHTTtsProvider> _logger;
    private readonly string _apiKey;
    private readonly string _userId;
    private readonly string _outputDirectory;
    private readonly HttpClient _httpClient;
    private readonly bool _offlineOnly;
    private const string ApiBaseUrl = "https://api.play.ht/api/v2";

    public PlayHTTtsProvider(
        ILogger<PlayHTTtsProvider> logger, 
        string apiKey,
        string userId,
        bool offlineOnly = false)
    {
        _logger = logger;
        _apiKey = apiKey;
        _userId = userId;
        _offlineOnly = offlineOnly;
        _httpClient = new HttpClient();
        _httpClient.DefaultRequestHeaders.Add("AUTHORIZATION", _apiKey);
        _httpClient.DefaultRequestHeaders.Add("X-USER-ID", _userId);
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
            _logger.LogWarning("OfflineOnly mode enabled, cannot fetch PlayHT voices");
            return new List<string> { "Matthew", "Jennifer", "Joey", "Joanna" }; // Default voice list
        }

        try
        {
            var response = await _httpClient.GetAsync($"{ApiBaseUrl}/voices");
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Failed to fetch PlayHT voices: {Status}", response.StatusCode);
                return new List<string> { "Matthew", "Jennifer", "Joey", "Joanna" };
            }

            var json = await response.Content.ReadAsStringAsync();
            var voices = JsonSerializer.Deserialize<List<PlayHTVoice>>(json);
            var voiceNames = new List<string>();

            if (voices != null)
            {
                foreach (var voice in voices)
                {
                    voiceNames.Add(voice.Name ?? "Unknown");
                }
            }

            return voiceNames;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching PlayHT voices");
            return new List<string> { "Matthew", "Jennifer", "Joey", "Joanna" };
        }
    }

    public async Task<string> SynthesizeAsync(IEnumerable<ScriptLine> lines, VoiceSpec spec, CancellationToken ct)
    {
        if (_offlineOnly)
        {
            _logger.LogWarning("OfflineOnly mode enabled, cannot use PlayHT");
            throw new InvalidOperationException("PlayHT requires online access. Please use Windows TTS for offline mode.");
        }

        // Validate API key first with a smoke test
        if (!await ValidateApiKeyAsync(ct))
        {
            throw new InvalidOperationException("Invalid PlayHT API key or User ID");
        }

        _logger.LogInformation("Synthesizing speech with PlayHT using voice {Voice}", spec.VoiceName);
        
        var lineOutputs = new List<string>();
        
        foreach (var line in lines)
        {
            ct.ThrowIfCancellationRequested();
            
            try
            {
                // Build request
                var requestBody = new
                {
                    text = line.Text,
                    voice = spec.VoiceName,
                    quality = "premium",
                    output_format = "mp3",
                    speed = spec.Rate,
                    sample_rate = 24000
                };

                var json = JsonSerializer.Serialize(requestBody);
                var content = new StringContent(json, Encoding.UTF8, "application/json");
                
                var response = await _httpClient.PostAsync(
                    $"{ApiBaseUrl}/tts", 
                    content, 
                    ct);

                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogWarning("Failed to synthesize line {Index}: {Status}", line.SceneIndex, response.StatusCode);
                    throw new HttpRequestException($"PlayHT API returned {response.StatusCode}");
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
        string outputFilePath = Path.Combine(_outputDirectory, $"narration_playht_{DateTime.Now:yyyyMMddHHmmss}.mp3");
        
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
        
        _logger.LogInformation("PlayHT synthesis complete: {Path}", outputFilePath);
        return outputFilePath;
    }

    private async Task<bool> ValidateApiKeyAsync(CancellationToken ct)
    {
        try
        {
            // Perform a smoke test by fetching voices
            var response = await _httpClient.GetAsync($"{ApiBaseUrl}/voices", ct);
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to validate PlayHT API key");
            return false;
        }
    }

    public void Dispose()
    {
        _httpClient?.Dispose();
    }

    private class PlayHTVoice
    {
        public string? Id { get; set; }
        public string? Name { get; set; }
    }
}
