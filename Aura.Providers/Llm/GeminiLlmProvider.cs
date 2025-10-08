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
/// LLM provider that uses Google Gemini API for script generation (Pro feature).
/// </summary>
public class GeminiLlmProvider : ILlmProvider
{
    private readonly ILogger<GeminiLlmProvider> _logger;
    private readonly HttpClient _httpClient;
    private readonly string _apiKey;
    private readonly string _model;

    public GeminiLlmProvider(
        ILogger<GeminiLlmProvider> logger,
        HttpClient httpClient,
        string apiKey,
        string model = "gemini-1.5-flash")
    {
        _logger = logger;
        _httpClient = httpClient;
        _apiKey = apiKey;
        _model = model;

        if (string.IsNullOrEmpty(_apiKey))
        {
            throw new ArgumentException("Gemini API key is required", nameof(apiKey));
        }
    }

    public async Task<string> DraftScriptAsync(Brief brief, PlanSpec spec, CancellationToken ct)
    {
        _logger.LogInformation("Generating script with Gemini (model: {Model}) for topic: {Topic}", _model, brief.Topic);

        try
        {
            // Build the prompt
            string prompt = BuildPrompt(brief, spec);

            // Call Gemini API
            var requestBody = new
            {
                contents = new[]
                {
                    new
                    {
                        parts = new[]
                        {
                            new { text = prompt }
                        }
                    }
                },
                generationConfig = new
                {
                    temperature = 0.7,
                    maxOutputTokens = 2048,
                    topP = 0.9
                }
            };

            var json = JsonSerializer.Serialize(requestBody);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            // Gemini API endpoint format: https://generativelanguage.googleapis.com/v1beta/models/{model}:generateContent?key={apiKey}
            var url = $"https://generativelanguage.googleapis.com/v1beta/models/{_model}:generateContent?key={_apiKey}";
            var response = await _httpClient.PostAsync(url, content, ct);
            response.EnsureSuccessStatusCode();

            var responseJson = await response.Content.ReadAsStringAsync(ct);
            var responseDoc = JsonDocument.Parse(responseJson);

            if (responseDoc.RootElement.TryGetProperty("candidates", out var candidates) &&
                candidates.GetArrayLength() > 0)
            {
                var firstCandidate = candidates[0];
                if (firstCandidate.TryGetProperty("content", out var contentObj) &&
                    contentObj.TryGetProperty("parts", out var parts) &&
                    parts.GetArrayLength() > 0)
                {
                    var firstPart = parts[0];
                    if (firstPart.TryGetProperty("text", out var textProp))
                    {
                        string script = textProp.GetString() ?? string.Empty;
                        _logger.LogInformation("Script generated successfully ({Length} characters)", script.Length);
                        return script;
                    }
                }
            }

            _logger.LogWarning("Gemini response did not contain expected structure");
            throw new Exception("Invalid response from Gemini");
        }
        catch (HttpRequestException ex)
        {
            _logger.LogWarning(ex, "Failed to connect to Gemini API");
            throw new Exception("Failed to connect to Gemini API. Check your API key and internet connection.", ex);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating script with Gemini");
            throw;
        }
    }

    private string BuildPrompt(Brief brief, PlanSpec spec)
    {
        var sb = new StringBuilder();

        sb.AppendLine("You are an expert YouTube video script writer. Create a detailed, engaging script.");
        sb.AppendLine();
        sb.AppendLine($"Topic: {brief.Topic}");
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
        sb.AppendLine("- A strong introduction with a hook");
        sb.AppendLine("- A compelling conclusion with a call-to-action");
        sb.AppendLine();
        sb.AppendLine("Write the complete script now:");

        return sb.ToString();
    }
}
