using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace Aura.Providers.Visuals;

/// <summary>
/// Midjourney visual provider via third-party API integration
/// Note: Uses unofficial API endpoints as Midjourney doesn't have official API yet
/// </summary>
public class MidjourneyProvider : BaseVisualProvider
{
    private readonly HttpClient _httpClient;
    private readonly string? _apiKey;
    private readonly string _baseUrl;
    private const decimal CostPerImage = 0.040m;

    public MidjourneyProvider(
        ILogger<MidjourneyProvider> logger,
        HttpClient httpClient,
        string? apiKey,
        string? baseUrl = null) : base(logger)
    {
        _httpClient = httpClient;
        _apiKey = apiKey;
        _baseUrl = baseUrl ?? "https://api.midjourney.com/v1";
    }

    public override string ProviderName => "Midjourney";

    public override bool RequiresApiKey => true;

    public override async Task<string?> GenerateImageAsync(
        string prompt,
        VisualGenerationOptions options,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(_apiKey))
        {
            Logger.LogWarning("Midjourney API key not configured");
            return null;
        }

        try
        {
            Logger.LogInformation("Generating image with Midjourney for prompt: {Prompt}", prompt);

            var adaptedPrompt = AdaptPrompt(prompt, options);

            var request = new HttpRequestMessage(HttpMethod.Post, $"{_baseUrl}/imagine");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _apiKey);

            var requestBody = new
            {
                prompt = adaptedPrompt,
                aspect_ratio = options.AspectRatio,
                quality = options.Quality > 75 ? "high" : "standard"
            };

            request.Content = new StringContent(
                JsonSerializer.Serialize(requestBody),
                Encoding.UTF8,
                "application/json");

            var response = await _httpClient.SendAsync(request, ct).ConfigureAwait(false);
            var content = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);

            if (!response.IsSuccessStatusCode)
            {
                Logger.LogWarning("Midjourney request failed: {Status} - {Content}", response.StatusCode, content);
                return null;
            }

            var result = JsonSerializer.Deserialize<MidjourneyResponse>(content);
            if (result?.TaskId != null)
            {
                var imagePath = await PollForCompletion(result.TaskId, ct).ConfigureAwait(false);
                if (imagePath != null)
                {
                    Logger.LogInformation("Midjourney image generated successfully: {Path}", imagePath);
                    return imagePath;
                }
            }

            Logger.LogWarning("Midjourney generation failed or timed out");
            return null;
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error generating image with Midjourney");
            return null;
        }
    }

    public override VisualProviderCapabilities GetProviderCapabilities()
    {
        return new VisualProviderCapabilities
        {
            ProviderName = ProviderName,
            SupportsNegativePrompts = true,
            SupportsBatchGeneration = true,
            SupportsStylePresets = true,
            SupportedAspectRatios = new() { "16:9", "9:16", "1:1", "4:3", "3:2", "2:3", "21:9" },
            SupportedStyles = new()
            {
                "artistic", "photorealistic", "anime", "digital-art",
                "fantasy", "cinematic", "realistic", "stylized"
            },
            MaxWidth = 2048,
            MaxHeight = 2048,
            IsLocal = false,
            IsFree = false,
            CostPerImage = CostPerImage,
            Tier = "Pro"
        };
    }

    public override async Task<bool> IsAvailableAsync(CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(_apiKey))
        {
            return false;
        }

        try
        {
            var request = new HttpRequestMessage(HttpMethod.Get, $"{_baseUrl}/account");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _apiKey);

            var response = await _httpClient.SendAsync(request, ct).ConfigureAwait(false);
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    public override string AdaptPrompt(string prompt, VisualGenerationOptions options)
    {
        var adapted = prompt;

        if (options.AspectRatio != "1:1" && !adapted.Contains("--ar"))
        {
            adapted += $" --ar {options.AspectRatio}";
        }

        if (options.Style == "photorealistic" && !adapted.Contains("--style"))
        {
            adapted += " --style raw";
        }

        if (options.Quality > 75 && !adapted.Contains("--quality"))
        {
            adapted += " --quality 2";
        }

        if (options.NegativePrompts != null && options.NegativePrompts.Length > 0)
        {
            var negPrompt = string.Join(", ", options.NegativePrompts);
            adapted += $" --no {negPrompt}";
        }

        return adapted;
    }

    public override decimal GetCostEstimate(VisualGenerationOptions options)
    {
        return CostPerImage;
    }

    private async Task<string?> PollForCompletion(string taskId, CancellationToken ct)
    {
        const int maxAttempts = 60;
        const int pollIntervalMs = 2000;

        for (int i = 0; i < maxAttempts; i++)
        {
            try
            {
                await Task.Delay(pollIntervalMs, ct).ConfigureAwait(false);

                var request = new HttpRequestMessage(HttpMethod.Get, $"{_baseUrl}/tasks/{taskId}");
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _apiKey);

                var response = await _httpClient.SendAsync(request, ct).ConfigureAwait(false);
                var content = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);

                if (response.IsSuccessStatusCode)
                {
                    var status = JsonSerializer.Deserialize<MidjourneyTaskStatus>(content);
                    if (status?.Status == "completed" && status.ImageUrl != null)
                    {
                        var imageBytes = await _httpClient.GetByteArrayAsync(status.ImageUrl, ct).ConfigureAwait(false);
                        var tempPath = Path.Combine(Path.GetTempPath(), $"midjourney_{Guid.NewGuid()}.png");
                        await File.WriteAllBytesAsync(tempPath, imageBytes, ct).ConfigureAwait(false);
                        return tempPath;
                    }
                    else if (status?.Status == "failed")
                    {
                        Logger.LogWarning("Midjourney task {TaskId} failed", taskId);
                        return null;
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.LogWarning(ex, "Error polling Midjourney task {TaskId}", taskId);
            }
        }

        Logger.LogWarning("Midjourney task {TaskId} timed out after {Seconds} seconds", taskId, maxAttempts * pollIntervalMs / 1000);
        return null;
    }

    private class MidjourneyResponse
    {
        public string? TaskId { get; set; }
        public string? Status { get; set; }
    }

    private class MidjourneyTaskStatus
    {
        public string? Status { get; set; }
        public string? ImageUrl { get; set; }
        public string? Error { get; set; }
    }
}
