using System;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Aura.Core.AI;
using Aura.Core.Models;
using Aura.Core.Providers;
using Microsoft.Extensions.Logging;

namespace Aura.Providers.Llm;

/// <summary>
/// LLM provider that uses a local Ollama instance for script generation.
/// Supports optional ML-driven enhancements via callbacks.
/// </summary>
public class OllamaLlmProvider : ILlmProvider
{
    private readonly ILogger<OllamaLlmProvider> _logger;
    private readonly HttpClient _httpClient;
    private readonly string _baseUrl;
    private readonly string _model;
    private readonly int _maxRetries;
    private readonly TimeSpan _timeout;

    /// <summary>
    /// Optional callback to enhance prompts before generation
    /// </summary>
    public Func<string, Brief, PlanSpec, Task<string>>? PromptEnhancementCallback { get; set; }

    /// <summary>
    /// Optional callback to track generation performance
    /// </summary>
    public Action<double, TimeSpan, bool>? PerformanceTrackingCallback { get; set; }

    public OllamaLlmProvider(
        ILogger<OllamaLlmProvider> logger,
        HttpClient httpClient,
        string baseUrl = "http://127.0.0.1:11434",
        string model = "llama3.1:8b-q4_k_m",
        int maxRetries = 2,
        int timeoutSeconds = 120)
    {
        _logger = logger;
        _httpClient = httpClient;
        _baseUrl = baseUrl;
        _model = model;
        _maxRetries = maxRetries;
        _timeout = TimeSpan.FromSeconds(timeoutSeconds);

        // Note: Connection test is skipped during initialization to avoid blocking startup.
        // Connection will be tested on first use with helpful error messages.
    }

    public async Task<string> DraftScriptAsync(Brief brief, PlanSpec spec, CancellationToken ct)
    {
        _logger.LogInformation("Generating script with Ollama (model: {Model}) at {BaseUrl} for topic: {Topic}", _model, _baseUrl, brief.Topic);

        var startTime = DateTime.UtcNow;
        Exception? lastException = null;
        
        for (int attempt = 0; attempt <= _maxRetries; attempt++)
        {
            try
            {
                if (attempt > 0)
                {
                    _logger.LogInformation("Retry attempt {Attempt}/{MaxRetries} for Ollama", attempt, _maxRetries);
                    await Task.Delay(TimeSpan.FromSeconds(2 * attempt), ct); // Exponential backoff
                }

                // Build enhanced prompt for quality content
                string systemPrompt = EnhancedPromptTemplates.GetSystemPromptForScriptGeneration();
                string userPrompt = EnhancedPromptTemplates.BuildScriptGenerationPrompt(brief, spec);
                
                // Apply enhancement callback if configured
                if (PromptEnhancementCallback != null)
                {
                    userPrompt = await PromptEnhancementCallback(userPrompt, brief, spec);
                }
                
                string prompt = $"{systemPrompt}\n\n{userPrompt}";

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

                using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
                cts.CancelAfter(_timeout);

                var response = await _httpClient.PostAsync($"{_baseUrl}/api/generate", content, cts.Token);
                response.EnsureSuccessStatusCode();

                var responseJson = await response.Content.ReadAsStringAsync(ct);
                var responseDoc = JsonDocument.Parse(responseJson);

                if (responseDoc.RootElement.TryGetProperty("response", out var responseText))
                {
                    string script = responseText.GetString() ?? string.Empty;
                    var duration = DateTime.UtcNow - startTime;
                    
                    _logger.LogInformation("Script generated successfully with Ollama ({Length} characters)", script.Length);
                    
                    // Track performance if callback configured
                    PerformanceTrackingCallback?.Invoke(75.0, duration, true);
                    
                    return script;
                }

                _logger.LogWarning("Ollama response did not contain expected 'response' field");
                throw new Exception("Invalid response from Ollama");
            }
            catch (TaskCanceledException ex)
            {
                lastException = ex;
                _logger.LogWarning(ex, "Ollama request timed out (attempt {Attempt}/{MaxRetries})", attempt + 1, _maxRetries + 1);
                if (attempt >= _maxRetries)
                {
                    var duration = DateTime.UtcNow - startTime;
                    PerformanceTrackingCallback?.Invoke(0, duration, false);
                    throw new InvalidOperationException(
                        $"Ollama request timed out after {_timeout.TotalSeconds}s. The model '{_model}' may be loading or Ollama may be overloaded.", ex);
                }
            }
            catch (HttpRequestException ex)
            {
                lastException = ex;
                _logger.LogWarning(ex, "Failed to connect to Ollama at {BaseUrl} (attempt {Attempt}/{MaxRetries})", _baseUrl, attempt + 1, _maxRetries + 1);
                if (attempt >= _maxRetries)
                {
                    var duration = DateTime.UtcNow - startTime;
                    PerformanceTrackingCallback?.Invoke(0, duration, false);
                    throw new InvalidOperationException(
                        $"Cannot connect to Ollama at {_baseUrl}. Please ensure Ollama is running: 'ollama serve'", ex);
                }
            }
            catch (Exception ex) when (attempt < _maxRetries)
            {
                lastException = ex;
                _logger.LogWarning(ex, "Error generating script with Ollama (attempt {Attempt}/{MaxRetries})", attempt + 1, _maxRetries + 1);
            }
            catch (Exception ex)
            {
                var duration = DateTime.UtcNow - startTime;
                PerformanceTrackingCallback?.Invoke(0, duration, false);
                _logger.LogError(ex, "Error generating script with Ollama after all retries");
                throw;
            }
        }

        // Should not reach here, but just in case
        var finalDuration = DateTime.UtcNow - startTime;
        PerformanceTrackingCallback?.Invoke(0, finalDuration, false);
        throw new InvalidOperationException(
            $"Failed to generate script with Ollama at {_baseUrl} after {_maxRetries + 1} attempts. Please verify Ollama is running and model '{_model}' is available.", 
            lastException);
    }

    public async Task<SceneAnalysisResult?> AnalyzeSceneImportanceAsync(
        string sceneText,
        string? previousSceneText,
        string videoGoal,
        CancellationToken ct)
    {
        _logger.LogInformation("Analyzing scene importance with Ollama");

        try
        {
            // Use centralized scene analysis prompt templates
            var systemPrompt = EnhancedPromptTemplates.GetSystemPromptForSceneAnalysis();
            var userPrompt = EnhancedPromptTemplates.BuildSceneAnalysisPrompt(sceneText, previousSceneText, videoGoal);

            var prompt = $"{systemPrompt}\n\n{userPrompt}";

            var requestBody = new
            {
                model = _model,
                prompt = prompt,
                stream = false,
                options = new
                {
                    temperature = 0.3, // Lower temperature for more consistent JSON
                    top_p = 0.9,
                    num_predict = 512
                }
            };

            var json = JsonSerializer.Serialize(requestBody);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            cts.CancelAfter(TimeSpan.FromSeconds(30)); // Shorter timeout for analysis

            var response = await _httpClient.PostAsync($"{_baseUrl}/api/generate", content, cts.Token);
            response.EnsureSuccessStatusCode();

            var responseJson = await response.Content.ReadAsStringAsync(ct);
            var responseDoc = JsonDocument.Parse(responseJson);

            if (responseDoc.RootElement.TryGetProperty("response", out var responseText))
            {
                var analysisText = responseText.GetString() ?? string.Empty;
                
                // Try to parse the JSON response
                try
                {
                    var analysisDoc = JsonDocument.Parse(analysisText);
                    var root = analysisDoc.RootElement;

                    var result = new SceneAnalysisResult(
                        Importance: root.TryGetProperty("importance", out var imp) ? imp.GetDouble() : 50.0,
                        Complexity: root.TryGetProperty("complexity", out var comp) ? comp.GetDouble() : 50.0,
                        EmotionalIntensity: root.TryGetProperty("emotionalIntensity", out var emo) ? emo.GetDouble() : 50.0,
                        InformationDensity: root.TryGetProperty("informationDensity", out var info) ? info.GetString() ?? "medium" : "medium",
                        OptimalDurationSeconds: root.TryGetProperty("optimalDurationSeconds", out var dur) ? dur.GetDouble() : 10.0,
                        TransitionType: root.TryGetProperty("transitionType", out var trans) ? trans.GetString() ?? "cut" : "cut",
                        Reasoning: root.TryGetProperty("reasoning", out var reas) ? reas.GetString() ?? "" : ""
                    );

                    _logger.LogInformation("Scene analysis complete. Importance: {Importance}, Complexity: {Complexity}", 
                        result.Importance, result.Complexity);
                    
                    return result;
                }
                catch (JsonException ex)
                {
                    _logger.LogWarning(ex, "Failed to parse scene analysis JSON from Ollama: {Response}", analysisText);
                    return null;
                }
            }

            _logger.LogWarning("Ollama scene analysis response did not contain expected 'response' field");
            return null;
        }
        catch (TaskCanceledException ex)
        {
            _logger.LogWarning(ex, "Ollama scene analysis request timed out");
            return null;
        }
        catch (HttpRequestException ex)
        {
            _logger.LogWarning(ex, "Failed to connect to Ollama for scene analysis");
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error analyzing scene with Ollama");
            return null;
        }
    }

    // Removed legacy prompt building method - now using EnhancedPromptTemplates
}
