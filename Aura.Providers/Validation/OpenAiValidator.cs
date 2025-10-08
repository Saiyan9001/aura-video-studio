using System;
using System.Diagnostics;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace Aura.Providers.Validation;

/// <summary>
/// Validator for OpenAI API connectivity
/// </summary>
public class OpenAiValidator : IProviderValidator
{
    private readonly ILogger<OpenAiValidator> _logger;
    private readonly IKeyStore _keyStore;
    private readonly HttpClient _httpClient;

    public string ProviderName => "OpenAI";

    public OpenAiValidator(ILogger<OpenAiValidator> logger, IKeyStore keyStore, HttpClient httpClient)
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
            var apiKey = await _keyStore.GetKeyAsync("openai");
            if (string.IsNullOrEmpty(apiKey))
            {
                sw.Stop();
                return ValidationResult.Failure(ProviderName, "No API key configured", sw.ElapsedMilliseconds);
            }

            _logger.LogInformation("Validating OpenAI with key {MaskedKey}", _keyStore.MaskKey(apiKey));

            // Try to list models as a lightweight validation
            using var request = new HttpRequestMessage(HttpMethod.Get, "https://api.openai.com/v1/models");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
            
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(ct, cts.Token);
            
            var response = await _httpClient.SendAsync(request, linkedCts.Token);
            sw.Stop();

            if (response.IsSuccessStatusCode)
            {
                var content = await response.Content.ReadAsStringAsync(ct);
                var json = JsonDocument.Parse(content);
                var modelCount = json.RootElement.GetProperty("data").GetArrayLength();
                
                return ValidationResult.Success(
                    ProviderName, 
                    $"Connected successfully, {modelCount} models available", 
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
            _logger.LogError(ex, "Error validating OpenAI");
            return ValidationResult.Failure(ProviderName, $"Error: {ex.Message}", sw.ElapsedMilliseconds);
        }
    }
}
