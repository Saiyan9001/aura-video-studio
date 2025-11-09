using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Aura.Core.Models;
using Microsoft.Extensions.Logging;

namespace Aura.Providers.Tts;

/// <summary>
/// OpenAI TTS provider using the OpenAI Text-to-Speech API.
/// Supports multiple voices and models (tts-1, tts-1-hd).
/// </summary>
public class OpenAiTtsProvider : BaseTtsProvider
{
    private readonly HttpClient _httpClient;
    private readonly string? _apiKey;
    private readonly bool _offlineOnly;
    private readonly string _model;
    
    private const string BaseUrl = "https://api.openai.com/v1/audio/speech";
    
    private static readonly string[] AvailableVoices = new[]
    {
        "alloy", "echo", "fable", "onyx", "nova", "shimmer"
    };

    public OpenAiTtsProvider(
        ILogger<OpenAiTtsProvider> logger,
        HttpClient httpClient,
        string? apiKey,
        bool offlineOnly = false,
        string model = "tts-1")
        : base(logger, maxRetries: 3, baseRetryDelayMs: 1000)
    {
        _httpClient = httpClient;
        _apiKey = apiKey;
        _offlineOnly = offlineOnly;
        _model = model;
        
        if (!_offlineOnly)
        {
            if (string.IsNullOrWhiteSpace(_apiKey))
            {
                _logger.LogWarning("OpenAI API key is missing. Provider will not be functional. Please configure your API key in settings.");
            }
            else
            {
                _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _apiKey);
                _logger.LogInformation("OpenAI TTS provider initialized with API key and model {Model}", _model);
            }
        }
        else
        {
            _logger.LogInformation("OpenAI TTS provider initialized in offline-only mode (not functional)");
        }
    }

    protected override string GetProviderName() => "OpenAI-TTS";

    protected override Task<IReadOnlyList<string>> GetAvailableVoicesCoreAsync()
    {
        if (_offlineOnly || string.IsNullOrEmpty(_apiKey))
        {
            _logger.LogWarning("OpenAI TTS not available in offline mode or without API key");
            return Task.FromResult<IReadOnlyList<string>>(Array.Empty<string>());
        }

        return Task.FromResult<IReadOnlyList<string>>(AvailableVoices);
    }

    protected override async Task<string> GenerateAudioCoreAsync(
        IEnumerable<ScriptLine> lines,
        VoiceSpec spec,
        CancellationToken ct)
    {
        if (_offlineOnly || string.IsNullOrEmpty(_apiKey))
        {
            throw new InvalidOperationException("OpenAI TTS provider is not available in offline mode or without API key");
        }

        var scriptLines = lines.ToList();
        if (scriptLines.Count == 0)
        {
            throw new ArgumentException("No script lines provided for synthesis");
        }

        var combinedText = string.Join(" ", scriptLines.Select(l => l.Text));
        
        if (combinedText.Length > 4096)
        {
            _logger.LogWarning("Text length ({Length}) exceeds OpenAI TTS limit (4096), truncating", combinedText.Length);
            combinedText = combinedText[..4096];
        }

        var outputPath = GenerateOutputPath("OpenAI", spec.VoiceName);

        var voice = ResolveVoiceName(spec.VoiceName);
        
        var speed = Math.Clamp(spec.Rate, 0.25, 4.0);

        var requestBody = new
        {
            model = _model,
            input = combinedText,
            voice = voice,
            response_format = "mp3",
            speed = speed
        };

        var request = new HttpRequestMessage(HttpMethod.Post, BaseUrl)
        {
            Content = new StringContent(JsonSerializer.Serialize(requestBody), Encoding.UTF8, "application/json")
        };

        var response = await _httpClient.SendAsync(request, ct);
        
        if (!response.IsSuccessStatusCode)
        {
            var errorContent = await response.Content.ReadAsStringAsync(ct);
            throw new HttpRequestException(
                $"OpenAI TTS request failed with status {response.StatusCode}: {errorContent}");
        }

        var audioData = await response.Content.ReadAsByteArrayAsync(ct);
        
        var mp3Path = Path.ChangeExtension(outputPath, ".mp3");
        await File.WriteAllBytesAsync(mp3Path, audioData, ct);
        
        _logger.LogInformation("OpenAI TTS synthesis completed: {OutputPath} ({Size} bytes)", 
            mp3Path, audioData.Length);
        
        return mp3Path;
    }

    private string ResolveVoiceName(string requestedVoice)
    {
        var normalizedRequest = requestedVoice.ToLowerInvariant().Trim();
        
        if (AvailableVoices.Contains(normalizedRequest))
        {
            return normalizedRequest;
        }
        
        var match = AvailableVoices.FirstOrDefault(v => 
            v.Contains(normalizedRequest, StringComparison.OrdinalIgnoreCase) ||
            normalizedRequest.Contains(v, StringComparison.OrdinalIgnoreCase));
        
        if (match != null)
        {
            _logger.LogInformation("Mapped voice {Requested} to {Resolved}", requestedVoice, match);
            return match;
        }
        
        _logger.LogWarning("Voice {Voice} not found in OpenAI TTS, using default 'alloy'", requestedVoice);
        return "alloy";
    }

    public override async IAsyncEnumerable<AudioChunk> StreamAudioAsync(
        IEnumerable<ScriptLine> lines,
        VoiceSpec spec,
        CancellationToken ct)
    {
        if (_offlineOnly || string.IsNullOrEmpty(_apiKey))
        {
            throw new InvalidOperationException("OpenAI TTS provider is not available in offline mode or without API key");
        }

        var scriptLines = lines.ToList();
        if (scriptLines.Count == 0)
        {
            throw new ArgumentException("No script lines provided for synthesis");
        }

        var chunkIndex = 0;
        
        foreach (var line in scriptLines)
        {
            var voice = ResolveVoiceName(spec.VoiceName);
            var speed = Math.Clamp(spec.Rate, 0.25, 4.0);

            var requestBody = new
            {
                model = _model,
                input = line.Text,
                voice = voice,
                response_format = "mp3",
                speed = speed
            };

            var request = new HttpRequestMessage(HttpMethod.Post, BaseUrl)
            {
                Content = new StringContent(JsonSerializer.Serialize(requestBody), Encoding.UTF8, "application/json")
            };

            var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct);
            response.EnsureSuccessStatusCode();

            var audioData = await response.Content.ReadAsByteArrayAsync(ct);
            
            yield return new AudioChunk
            {
                Data = audioData,
                Index = chunkIndex++,
                IsComplete = chunkIndex == scriptLines.Count
            };
        }
    }
}
