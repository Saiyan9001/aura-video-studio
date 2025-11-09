using System;
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
/// OpenAI DALL-E 3 visual provider for high-quality image generation
/// </summary>
public class DallE3Provider : BaseVisualProvider
{
    private readonly HttpClient _httpClient;
    private readonly string? _apiKey;
    private readonly string _baseUrl;
    private const int MaxPromptLength = 1000;
    private const int PromptTruncateLength = 997;
    private const decimal CostPerImageStandard = 0.040m;
    private const decimal CostPerImageHD = 0.080m;

    public DallE3Provider(
        ILogger<DallE3Provider> logger,
        HttpClient httpClient,
        string? apiKey,
        string? baseUrl = null) : base(logger)
    {
        _httpClient = httpClient;
        _apiKey = apiKey;
        _baseUrl = baseUrl ?? "https://api.openai.com/v1";
    }

    public override string ProviderName => "DALL-E 3";

    public override bool RequiresApiKey => true;

    public override async Task<string?> GenerateImageAsync(
        string prompt,
        VisualGenerationOptions options,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(_apiKey))
        {
            Logger.LogWarning("OpenAI API key not configured");
            return null;
        }

        try
        {
            Logger.LogInformation("Generating image with DALL-E 3 for prompt: {Prompt}", prompt);

            var request = new HttpRequestMessage(HttpMethod.Post, $"{_baseUrl}/images/generations");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _apiKey);

            var adaptedPrompt = AdaptPrompt(prompt, options);
            var size = MapToSupportedSize(options.Width, options.Height);
            var quality = options.Quality > 75 ? "hd" : "standard";

            var requestBody = new
            {
                model = "dall-e-3",
                prompt = adaptedPrompt,
                n = 1,
                size = size,
                quality = quality,
                response_format = "url"
            };

            request.Content = new StringContent(
                JsonSerializer.Serialize(requestBody),
                Encoding.UTF8,
                "application/json");

            var response = await _httpClient.SendAsync(request, ct).ConfigureAwait(false);
            var content = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);

            if (!response.IsSuccessStatusCode)
            {
                Logger.LogWarning("DALL-E 3 request failed: {Status} - {Content}", response.StatusCode, content);
                return null;
            }

            var result = JsonSerializer.Deserialize<DallEResponse>(content);
            if (result?.Data != null && result.Data.Count > 0 && result.Data[0].Url != null)
            {
                var imageUrl = result.Data[0].Url;
                var imageBytes = await _httpClient.GetByteArrayAsync(imageUrl, ct).ConfigureAwait(false);
                var tempPath = Path.Combine(Path.GetTempPath(), $"dalle3_{Guid.NewGuid()}.png");
                await File.WriteAllBytesAsync(tempPath, imageBytes, ct).ConfigureAwait(false);

                Logger.LogInformation("DALL-E 3 image generated successfully: {Path}", tempPath);
                return tempPath;
            }

            Logger.LogWarning("DALL-E 3 returned no images");
            return null;
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error generating image with DALL-E 3");
            return null;
        }
    }

    public override VisualProviderCapabilities GetProviderCapabilities()
    {
        return new VisualProviderCapabilities
        {
            ProviderName = ProviderName,
            SupportsNegativePrompts = false,
            SupportsBatchGeneration = false,
            SupportsStylePresets = true,
            SupportedAspectRatios = new() { "1:1", "16:9", "9:16" },
            SupportedStyles = new()
            {
                "natural", "vivid", "photorealistic", "artistic",
                "digital-art", "cinematic", "realistic"
            },
            MaxWidth = 1792,
            MaxHeight = 1024,
            IsLocal = false,
            IsFree = false,
            CostPerImage = CostPerImageHD,
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
            var request = new HttpRequestMessage(HttpMethod.Get, $"{_baseUrl}/models");
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

        if (adapted.Length > MaxPromptLength)
        {
            adapted = adapted.Substring(0, PromptTruncateLength) + "...";
        }

        if (options.Style == "vivid" && !adapted.Contains("vivid"))
        {
            adapted = "Vivid and vibrant: " + adapted;
        }

        return adapted;
    }

    public override decimal GetCostEstimate(VisualGenerationOptions options)
    {
        return options.Quality > 75 ? CostPerImageHD : CostPerImageStandard;
    }

    private static string MapToSupportedSize(int width, int height)
    {
        var ratio = (double)width / height;

        if (Math.Abs(ratio - 1.0) < 0.1)
        {
            return "1024x1024";
        }
        else if (ratio > 1.5)
        {
            return "1792x1024";
        }
        else if (ratio < 0.7)
        {
            return "1024x1792";
        }
        else
        {
            return "1024x1024";
        }
    }

    private class DallEResponse
    {
        public long Created { get; set; }
        public System.Collections.Generic.List<ImageData>? Data { get; set; }
    }

    private class ImageData
    {
        public string? Url { get; set; }
        public string? RevisedPrompt { get; set; }
    }
}
