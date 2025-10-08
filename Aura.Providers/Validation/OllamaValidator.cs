using System;
using System.Diagnostics;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Aura.Core.Configuration;
using Microsoft.Extensions.Logging;

namespace Aura.Providers.Validation;

/// <summary>
/// Validator for Ollama local LLM connectivity
/// </summary>
public class OllamaValidator : IProviderValidator
{
    private readonly ILogger<OllamaValidator> _logger;
    private readonly ProviderSettings _providerSettings;
    private readonly HttpClient _httpClient;

    public string ProviderName => "Ollama";

    public OllamaValidator(ILogger<OllamaValidator> logger, ProviderSettings providerSettings, HttpClient httpClient)
    {
        _logger = logger;
        _providerSettings = providerSettings;
        _httpClient = httpClient;
    }

    public async Task<ValidationResult> ValidateAsync(CancellationToken ct = default)
    {
        var sw = Stopwatch.StartNew();
        
        try
        {
            var ollamaUrl = _providerSettings.GetOllamaUrl();
            _logger.LogInformation("Validating Ollama at {Url}", ollamaUrl);

            // First, try to list available models
            using var listRequest = new HttpRequestMessage(HttpMethod.Get, $"{ollamaUrl}/api/tags");
            
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(ct, cts.Token);
            
            var listResponse = await _httpClient.SendAsync(listRequest, linkedCts.Token);
            
            if (!listResponse.IsSuccessStatusCode)
            {
                sw.Stop();
                return ValidationResult.Failure(
                    ProviderName, 
                    $"Failed to list models: {listResponse.StatusCode}", 
                    sw.ElapsedMilliseconds);
            }

            var listContent = await listResponse.Content.ReadAsStringAsync(ct);
            var listJson = JsonDocument.Parse(listContent);
            var models = listJson.RootElement.GetProperty("models");
            var modelCount = models.GetArrayLength();

            if (modelCount == 0)
            {
                sw.Stop();
                return ValidationResult.Failure(
                    ProviderName, 
                    "Connected but no models installed", 
                    sw.ElapsedMilliseconds);
            }

            // Get the first model name for testing
            var firstModel = models[0].GetProperty("name").GetString();
            
            // Try a minimal 2-token completion
            var generateRequest = new
            {
                model = firstModel,
                prompt = "Say hello",
                stream = false,
                options = new
                {
                    num_predict = 2,
                    temperature = 0
                }
            };

            var generateContent = new StringContent(
                JsonSerializer.Serialize(generateRequest),
                Encoding.UTF8,
                "application/json");

            using var generateHttpRequest = new HttpRequestMessage(HttpMethod.Post, $"{ollamaUrl}/api/generate");
            generateHttpRequest.Content = generateContent;
            
            var generateResponse = await _httpClient.SendAsync(generateHttpRequest, linkedCts.Token);
            sw.Stop();

            if (generateResponse.IsSuccessStatusCode)
            {
                return ValidationResult.Success(
                    ProviderName, 
                    $"Connected successfully, {modelCount} model(s) available, generation test passed", 
                    sw.ElapsedMilliseconds);
            }

            var errorContent = await generateResponse.Content.ReadAsStringAsync(ct);
            return ValidationResult.Failure(
                ProviderName, 
                $"Generation test failed: {generateResponse.StatusCode}", 
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
            _logger.LogError(ex, "Error validating Ollama");
            return ValidationResult.Failure(ProviderName, $"Error: {ex.Message}", sw.ElapsedMilliseconds);
        }
    }
}
