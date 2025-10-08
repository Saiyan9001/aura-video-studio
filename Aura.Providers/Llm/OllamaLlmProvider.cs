using System;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Aura.Core.Models;
using Aura.Core.Providers;
using Microsoft.Extensions.Logging;

namespace Aura.Providers.Llm;

/// <summary>
/// LLM provider that uses a local Ollama instance for script generation.
/// </summary>
public class OllamaLlmProvider : ILlmProvider
{
    private readonly ILogger<OllamaLlmProvider> _logger;
    private readonly HttpClient _httpClient;
    private readonly string _baseUrl;
    private readonly string _model;

    public OllamaLlmProvider(
        ILogger<OllamaLlmProvider> logger,
        HttpClient httpClient,
        string baseUrl = "http://127.0.0.1:11434",
        string model = "llama3.1:8b-q4_k_m")
    {
        _logger = logger;
        _httpClient = httpClient;
        _baseUrl = baseUrl;
        _model = model;
        
        // Set reasonable timeout for Ollama
        _httpClient.Timeout = TimeSpan.FromMinutes(5);
    }

    public async Task<string> DraftScriptAsync(Brief brief, PlanSpec spec, CancellationToken ct)
    {
        _logger.LogInformation("Generating script with Ollama (model: {Model}) for topic: {Topic}", _model, brief.Topic);

        const int maxRetries = 2;
        int attempt = 0;
        Exception? lastException = null;

        while (attempt < maxRetries)
        {
            attempt++;
            
            try
            {
                // Build the prompt
                string prompt = BuildPrompt(brief, spec);

                // Call Ollama API
                var requestBody = new
                {
                    model = _model,
                    prompt = prompt,
                    stream = false,
                    options = new
                    {
                        temperature = 0.7,
                        top_p = 0.9,
                        num_predict = 2048
                    }
                };

                var json = JsonSerializer.Serialize(requestBody);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                _logger.LogDebug("Attempting Ollama request (attempt {Attempt}/{MaxRetries})", attempt, maxRetries);
                var response = await _httpClient.PostAsync($"{_baseUrl}/api/generate", content, ct);
                response.EnsureSuccessStatusCode();

                var responseJson = await response.Content.ReadAsStringAsync(ct);
                var responseDoc = JsonDocument.Parse(responseJson);

                if (responseDoc.RootElement.TryGetProperty("response", out var responseText))
                {
                    string script = responseText.GetString() ?? string.Empty;
                    _logger.LogInformation("Script generated successfully ({Length} characters)", script.Length);
                    return script;
                }

                _logger.LogWarning("Ollama response did not contain expected 'response' field");
                throw new Exception("Invalid response from Ollama");
            }
            catch (HttpRequestException ex) when (attempt < maxRetries)
            {
                lastException = ex;
                _logger.LogWarning(ex, "Failed to connect to Ollama (attempt {Attempt}/{MaxRetries}). Retrying...", attempt, maxRetries);
                await Task.Delay(TimeSpan.FromSeconds(2), ct);
            }
            catch (TaskCanceledException ex) when (attempt < maxRetries && !ct.IsCancellationRequested)
            {
                lastException = ex;
                _logger.LogWarning(ex, "Ollama request timed out (attempt {Attempt}/{MaxRetries}). Retrying...", attempt, maxRetries);
                await Task.Delay(TimeSpan.FromSeconds(2), ct);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating script with Ollama");
                throw;
            }
        }

        // All retries exhausted
        _logger.LogWarning(lastException, "Failed to connect to Ollama at {BaseUrl} after {MaxRetries} attempts. Ensure Ollama is running.", _baseUrl, maxRetries);
        throw new Exception($"Failed to connect to Ollama at {_baseUrl}. Ensure Ollama is running and the model '{_model}' is available.", lastException);
    }

    private string BuildPrompt(Brief brief, PlanSpec spec)
    {
        var sb = new StringBuilder();

        sb.AppendLine($"You are a YouTube video script writer. Create a detailed, engaging script for a video about: {brief.Topic}");
        sb.AppendLine();
        sb.AppendLine($"Requirements:");
        sb.AppendLine($"- Target duration: {spec.TargetDuration.TotalMinutes:F1} minutes");
        sb.AppendLine($"- Tone: {brief.Tone}");
        sb.AppendLine($"- Pacing: {spec.Pacing}");
        sb.AppendLine($"- Content density: {spec.Density}");
        sb.AppendLine($"- Language: {brief.Language}");

        if (!string.IsNullOrEmpty(brief.Audience))
        {
            sb.AppendLine($"- Target audience: {brief.Audience}");
        }

        if (!string.IsNullOrEmpty(brief.Goal))
        {
            sb.AppendLine($"- Goal: {brief.Goal}");
        }

        sb.AppendLine();
        sb.AppendLine("Format the script with:");
        sb.AppendLine("- A title starting with #");
        sb.AppendLine("- Multiple scenes, each with a heading starting with ##");
        sb.AppendLine("- Clear, engaging narration text for each scene");
        sb.AppendLine("- A strong introduction and conclusion");
        sb.AppendLine();
        sb.AppendLine("Write the complete script now:");

        return sb.ToString();
    }
}
