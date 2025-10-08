using System;
using System.Diagnostics;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace Aura.Providers.Validation;

/// <summary>
/// Validator for ElevenLabs API connectivity
/// </summary>
public class ElevenLabsValidator : IProviderValidator
{
    private readonly ILogger<ElevenLabsValidator> _logger;
    private readonly IKeyStore _keyStore;
    private readonly HttpClient _httpClient;

    public string ProviderName => "ElevenLabs";

    public ElevenLabsValidator(ILogger<ElevenLabsValidator> logger, IKeyStore keyStore, HttpClient httpClient)
    {
        _logger = logger;
        _keyStore = keyStore;
        _httpClient = httpClient;
    }

    public async Task<ValidationResult> ValidateAsync(CancellationToken ct = default)
    {
        var sw = Stopwatch.StartNew();
        
        try
        {
            var apiKey = await _keyStore.GetKeyAsync("elevenlabs");
            if (string.IsNullOrEmpty(apiKey))
            {
                sw.Stop();
                return ValidationResult.Failure(ProviderName, "No API key configured", sw.ElapsedMilliseconds);
            }

            _logger.LogInformation("Validating ElevenLabs with key {MaskedKey}", _keyStore.MaskKey(apiKey));

            // Try to list voices as a lightweight validation
            using var request = new HttpRequestMessage(HttpMethod.Get, "https://api.elevenlabs.io/v1/voices");
            request.Headers.Add("xi-api-key", apiKey);
            
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(ct, cts.Token);
            
            var response = await _httpClient.SendAsync(request, linkedCts.Token);
            sw.Stop();

            if (response.IsSuccessStatusCode)
            {
                var content = await response.Content.ReadAsStringAsync(ct);
                var json = JsonDocument.Parse(content);
                var voiceCount = json.RootElement.GetProperty("voices").GetArrayLength();
                
                return ValidationResult.Success(
                    ProviderName, 
                    $"Connected successfully, {voiceCount} voices available", 
                    sw.ElapsedMilliseconds);
            }

            var errorContent = await response.Content.ReadAsStringAsync(ct);
            return ValidationResult.Failure(
                ProviderName, 
                $"API returned {response.StatusCode}: {errorContent}", 
                sw.ElapsedMilliseconds);
        }
        catch (OperationCanceledException)
        {
            sw.Stop();
            return ValidationResult.Failure(ProviderName, "Request timed out after 10 seconds", sw.ElapsedMilliseconds);
        }
        catch (Exception ex)
        {
            sw.Stop();
            _logger.LogError(ex, "Error validating ElevenLabs");
            return ValidationResult.Failure(ProviderName, $"Error: {ex.Message}", sw.ElapsedMilliseconds);
        }
    }
}
