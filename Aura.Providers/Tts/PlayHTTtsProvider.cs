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
/// PlayHT TTS provider (Pro tier).
/// Provides high-quality neural voice synthesis.
/// </summary>
public class PlayHTTtsProvider : ITtsProvider
{
    private readonly ILogger<PlayHTTtsProvider> _logger;
    private readonly string _apiKey;
    private readonly string _userId;
    private readonly bool _offlineOnly;
    private readonly string _outputDirectory;
    private readonly HttpClient _httpClient;
    private const string ApiBaseUrl = "https://play.ht/api/v2";

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
        _outputDirectory = Path.Combine(Path.GetTempPath(), "AuraVideoStudio", "TTS");
        _httpClient = new HttpClient();
        
        // Only add headers if keys are not empty
        if (!string.IsNullOrWhiteSpace(_apiKey))
        {
            _httpClient.DefaultRequestHeaders.Add("AUTHORIZATION", _apiKey);
        }
        if (!string.IsNullOrWhiteSpace(_userId))
        {
            _httpClient.DefaultRequestHeaders.Add("X-USER-ID", _userId);
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
            _logger.LogWarning("OfflineOnly mode enabled, PlayHT voices unavailable");
            return Array.Empty<string>();
        }

        try
        {
            _logger.LogInformation("Fetching PlayHT voices");
            var response = await _httpClient.GetAsync($"{ApiBaseUrl}/voices");
            response.EnsureSuccessStatusCode();
            
            var json = await response.Content.ReadAsStringAsync();
            var voicesArray = JsonDocument.Parse(json);
            var voices = new List<string>();
            
            foreach (var voice in voicesArray.RootElement.EnumerateArray())
            {
                if (voice.TryGetProperty("name", out var name))
                {
                    voices.Add(name.GetString() ?? "Unknown");
                }
            }
            
            _logger.LogInformation("Retrieved {Count} PlayHT voices", voices.Count);
            return voices;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to fetch PlayHT voices");
            return Array.Empty<string>();
        }
    }

    public async Task<string> SynthesizeAsync(IEnumerable<ScriptLine> lines, VoiceSpec spec, CancellationToken ct)
    {
        if (_offlineOnly)
        {
            _logger.LogWarning("OfflineOnly mode enabled, cannot use PlayHT TTS");
            throw new InvalidOperationException("PlayHT TTS is not available in offline mode");
        }

        if (string.IsNullOrWhiteSpace(_apiKey) || string.IsNullOrWhiteSpace(_userId))
        {
            _logger.LogWarning("PlayHT API credentials not configured");
            throw new InvalidOperationException("PlayHT API key and user ID are required");
        }

        _logger.LogInformation("Synthesizing speech with PlayHT TTS");
        
        var linesList = lines.ToList();
        if (linesList.Count == 0)
        {
            throw new ArgumentException("No script lines provided for synthesis");
        }

        // Validate API key with a short smoke test
        await ValidateApiKeyAsync(ct);

        // Combine all text for synthesis
        string combinedText = string.Join(" ", linesList.Select(l => l.Text));
        
        // Use default PlayHT voice (can be made configurable)
        string voice = "en-US-JennyNeural";
        
        string outputFilePath = Path.Combine(_outputDirectory, $"narration_playht_{DateTime.Now:yyyyMMddHHmmss}.mp3");
        
        await SynthesizeTextAsync(combinedText, voice, outputFilePath, ct);
        
        _logger.LogInformation("PlayHT TTS audio generated at: {Path}", outputFilePath);
        return outputFilePath;
    }

    private async Task ValidateApiKeyAsync(CancellationToken ct)
    {
        try
        {
            _logger.LogInformation("Validating PlayHT API key with smoke test");
            
            // Short smoke test: synthesize a single word
            var testPayload = new
            {
                text = "Test",
                voice = "en-US-JennyNeural",
                quality = "draft",
                output_format = "mp3",
                speed = 1.0
            };
            
            var content = new StringContent(
                JsonSerializer.Serialize(testPayload),
                Encoding.UTF8,
                "application/json");
            
            var response = await _httpClient.PostAsync(
                $"{ApiBaseUrl}/tts",
                content,
                ct);
            
            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadAsStringAsync(ct);
                throw new InvalidOperationException($"PlayHT API key validation failed: {error}");
            }
            
            _logger.LogInformation("PlayHT API key validated successfully");
        }
        catch (Exception ex) when (ex is not InvalidOperationException)
        {
            _logger.LogError(ex, "PlayHT API key validation failed");
            throw new InvalidOperationException("Failed to validate PlayHT API key", ex);
        }
    }

    private async Task SynthesizeTextAsync(string text, string voice, string outputPath, CancellationToken ct)
    {
        var payload = new
        {
            text,
            voice,
            quality = "medium",
            output_format = "mp3",
            speed = 1.0,
            sample_rate = 44100
        };
        
        var content = new StringContent(
            JsonSerializer.Serialize(payload),
            Encoding.UTF8,
            "application/json");
        
        var response = await _httpClient.PostAsync(
            $"{ApiBaseUrl}/tts",
            content,
            ct);
        
        response.EnsureSuccessStatusCode();
        
        // PlayHT returns a job ID, we need to poll for completion
        var responseJson = await response.Content.ReadAsStringAsync(ct);
        var responseDoc = JsonDocument.Parse(responseJson);
        
        if (responseDoc.RootElement.TryGetProperty("id", out var jobId))
        {
            await PollForCompletionAsync(jobId.GetString()!, outputPath, ct);
        }
        else
        {
            throw new InvalidOperationException("PlayHT TTS job ID not returned");
        }
    }

    private async Task PollForCompletionAsync(string jobId, string outputPath, CancellationToken ct)
    {
        const int maxAttempts = 30;
        const int delayMs = 1000;
        
        for (int attempt = 0; attempt < maxAttempts; attempt++)
        {
            ct.ThrowIfCancellationRequested();
            
            var response = await _httpClient.GetAsync($"{ApiBaseUrl}/tts/{jobId}", ct);
            response.EnsureSuccessStatusCode();
            
            var json = await response.Content.ReadAsStringAsync(ct);
            var doc = JsonDocument.Parse(json);
            
            if (doc.RootElement.TryGetProperty("status", out var status))
            {
                var statusStr = status.GetString();
                
                if (statusStr == "completed")
                {
                    if (doc.RootElement.TryGetProperty("output", out var output))
                    {
                        if (output.TryGetProperty("url", out var url))
                        {
                            await DownloadAudioAsync(url.GetString()!, outputPath, ct);
                            return;
                        }
                    }
                }
                else if (statusStr == "failed" || statusStr == "error")
                {
                    throw new InvalidOperationException($"PlayHT TTS job failed with status: {statusStr}");
                }
            }
            
            await Task.Delay(delayMs, ct);
        }
        
        throw new TimeoutException("PlayHT TTS job did not complete within the expected time");
    }

    private async Task DownloadAudioAsync(string url, string outputPath, CancellationToken ct)
    {
        var response = await _httpClient.GetAsync(url, ct);
        response.EnsureSuccessStatusCode();
        
        var audioData = await response.Content.ReadAsByteArrayAsync(ct);
        await File.WriteAllBytesAsync(outputPath, audioData, ct);
        
        _logger.LogInformation("Downloaded {Length} bytes of audio", audioData.Length);
    }
}
