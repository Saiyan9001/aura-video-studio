using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Aura.Core.Models;
using Microsoft.Extensions.Logging;

namespace Aura.Providers.Tts;

/// <summary>
/// Free Microsoft Edge TTS provider using the Edge Read Aloud API.
/// This is a free tier provider that works without API keys.
/// Supports multiple languages and voices.
/// </summary>
public class EdgeTtsProvider : BaseTtsProvider
{
    private readonly HttpClient _httpClient;
    private readonly bool _offlineOnly;
    
    private const string VoicesEndpoint = "https://speech.platform.bing.com/consumer/speech/synthesize/readaloud/voices/list?trustedclienttoken=6A5AA1D4EAFF4E9FB37E23D68491D6F4";
    private const string SynthesisEndpoint = "https://speech.platform.bing.com/consumer/speech/synthesize/readaloud/edge/v1";

    public EdgeTtsProvider(
        ILogger<EdgeTtsProvider> logger,
        HttpClient httpClient,
        bool offlineOnly = false)
        : base(logger, maxRetries: 3, baseRetryDelayMs: 1000)
    {
        _httpClient = httpClient;
        _offlineOnly = offlineOnly;
        
        if (_offlineOnly)
        {
            _logger.LogInformation("EdgeTTS provider initialized in offline-only mode (not functional)");
        }
        else
        {
            _logger.LogInformation("EdgeTTS provider initialized (free, no API key required)");
        }
    }

    protected override string GetProviderName() => "EdgeTTS";

    protected override async Task<IReadOnlyList<string>> GetAvailableVoicesCoreAsync()
    {
        if (_offlineOnly)
        {
            _logger.LogWarning("EdgeTTS not available in offline mode");
            return Array.Empty<string>();
        }

        try
        {
            var response = await _httpClient.GetAsync(VoicesEndpoint);
            response.EnsureSuccessStatusCode();
            
            var voices = await response.Content.ReadFromJsonAsync<List<EdgeVoiceInfo>>();
            
            if (voices == null || voices.Count == 0)
            {
                _logger.LogWarning("No voices returned from EdgeTTS API");
                return Array.Empty<string>();
            }
            
            return voices
                .Select(v => $"{v.FriendlyName} ({v.Locale})")
                .ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to fetch EdgeTTS voices");
            throw;
        }
    }

    protected override async Task<string> GenerateAudioCoreAsync(
        IEnumerable<ScriptLine> lines,
        VoiceSpec spec,
        CancellationToken ct)
    {
        if (_offlineOnly)
        {
            throw new InvalidOperationException("EdgeTTS provider is not available in offline mode");
        }

        var scriptLines = lines.ToList();
        if (scriptLines.Count == 0)
        {
            throw new ArgumentException("No script lines provided for synthesis");
        }

        var combinedText = string.Join(" ", scriptLines.Select(l => l.Text));
        
        var outputPath = GenerateOutputPath("EdgeTTS", spec.VoiceName);

        var voiceName = await ResolveVoiceNameAsync(spec.VoiceName, ct);
        
        var ssml = BuildSsml(combinedText, voiceName, spec);
        
        var request = new HttpRequestMessage(HttpMethod.Post, SynthesisEndpoint);
        request.Headers.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36");
        request.Content = new StringContent(ssml, Encoding.UTF8, "application/ssml+xml");
        
        var response = await _httpClient.SendAsync(request, ct);
        response.EnsureSuccessStatusCode();
        
        var audioData = await response.Content.ReadAsByteArrayAsync(ct);
        
        await File.WriteAllBytesAsync(outputPath, audioData, ct);
        
        if (!ValidateAudioFile(outputPath, scriptLines.Sum(l => l.Duration.TotalSeconds)))
        {
            throw new InvalidOperationException($"Generated audio file is invalid: {outputPath}");
        }
        
        _logger.LogInformation("EdgeTTS synthesis completed: {OutputPath} ({Size} bytes)", 
            outputPath, audioData.Length);
        
        return outputPath;
    }

    private async Task<string> ResolveVoiceNameAsync(string requestedVoice, CancellationToken ct)
    {
        try
        {
            var voices = await GetAvailableVoicesCoreAsync();
            
            var match = voices.FirstOrDefault(v => 
                v.Contains(requestedVoice, StringComparison.OrdinalIgnoreCase));
            
            if (match != null)
            {
                var parts = match.Split('(', ')');
                if (parts.Length >= 2)
                {
                    return parts[0].Trim();
                }
                return match;
            }
            
            _logger.LogWarning("Voice {Voice} not found in EdgeTTS, using default en-US-AriaNeural", requestedVoice);
            return "en-US-AriaNeural";
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to resolve voice name, using default");
            return "en-US-AriaNeural";
        }
    }

    private string BuildSsml(string text, string voiceName, VoiceSpec spec)
    {
        var rate = spec.Rate switch
        {
            > 1.2 => "+20%",
            > 1.0 => "+10%",
            < 0.8 => "-20%",
            < 1.0 => "-10%",
            _ => "+0%"
        };
        
        var pitch = spec.Pitch switch
        {
            > 0.5 => "+10Hz",
            > 0.0 => "+5Hz",
            < -0.5 => "-10Hz",
            < 0.0 => "-5Hz",
            _ => "+0Hz"
        };

        var ssml = $@"<speak version='1.0' xmlns='http://www.w3.org/2001/10/synthesis' xml:lang='en-US'>
    <voice name='{voiceName}'>
        <prosody rate='{rate}' pitch='{pitch}'>
            {System.Security.SecurityElement.Escape(text)}
        </prosody>
    </voice>
</speak>";

        return ssml;
    }
}

/// <summary>
/// Edge TTS voice information model
/// </summary>
internal record EdgeVoiceInfo
{
    public string Name { get; init; } = string.Empty;
    public string FriendlyName { get; init; } = string.Empty;
    public string Locale { get; init; } = string.Empty;
    public string Gender { get; init; } = string.Empty;
}
