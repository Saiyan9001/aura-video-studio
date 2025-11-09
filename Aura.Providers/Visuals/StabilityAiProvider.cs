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
/// Stability AI visual provider for high-quality image generation via API
/// </summary>
public class StabilityAiProvider : BaseVisualProvider
{
    private readonly HttpClient _httpClient;
    private readonly string? _apiKey;
    private readonly string _baseUrl;
    private const decimal CostPerImageSD15 = 0.002m;
    private const decimal CostPerImageSDXL = 0.004m;

    public StabilityAiProvider(
        ILogger<StabilityAiProvider> logger,
        HttpClient httpClient,
        string? apiKey,
        string? baseUrl = null) : base(logger)
    {
        _httpClient = httpClient;
        _apiKey = apiKey;
        _baseUrl = baseUrl ?? "https://api.stability.ai";
    }

    public override string ProviderName => "StabilityAI";

    public override bool RequiresApiKey => true;

    public override async Task<string?> GenerateImageAsync(
        string prompt,
        VisualGenerationOptions options,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(_apiKey))
        {
            Logger.LogWarning("Stability AI API key not configured");
            return null;
        }

        try
        {
            Logger.LogInformation("Generating image with Stability AI for prompt: {Prompt}", prompt);

            var model = options.Width >= 1024 || options.Height >= 1024
                ? "stable-diffusion-xl-1024-v1-0"
                : "stable-diffusion-v1-6";

            var request = new HttpRequestMessage(HttpMethod.Post, $"{_baseUrl}/v1/generation/{model}/text-to-image");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _apiKey);
            request.Headers.Add("Accept", "application/json");

            var adaptedPrompt = AdaptPrompt(prompt, options);

            var textPrompts = new List<object>
            {
                new { text = adaptedPrompt, weight = 1.0 }
            };

            if (options.NegativePrompts != null && options.NegativePrompts.Length > 0)
            {
                foreach (var negPrompt in options.NegativePrompts)
                {
                    textPrompts.Add(new { text = negPrompt, weight = -1.0 });
                }
            }

            var requestBody = new
            {
                text_prompts = textPrompts,
                cfg_scale = 7,
                height = options.Height,
                width = options.Width,
                samples = 1,
                steps = 30,
                style_preset = MapStyleToPreset(options.Style)
            };

            request.Content = new StringContent(
                JsonSerializer.Serialize(requestBody),
                Encoding.UTF8,
                "application/json");

            var response = await _httpClient.SendAsync(request, ct).ConfigureAwait(false);
            var content = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);

            if (!response.IsSuccessStatusCode)
            {
                Logger.LogWarning("Stability AI request failed: {Status} - {Content}", response.StatusCode, content);
                return null;
            }

            var result = JsonSerializer.Deserialize<StabilityResponse>(content);
            if (result?.Artifacts != null && result.Artifacts.Count > 0 && result.Artifacts[0].Base64 != null)
            {
                var imageBytes = Convert.FromBase64String(result.Artifacts[0].Base64);
                var tempPath = Path.Combine(Path.GetTempPath(), $"stability_{Guid.NewGuid()}.png");
                await File.WriteAllBytesAsync(tempPath, imageBytes, ct).ConfigureAwait(false);

                Logger.LogInformation("Stability AI image generated successfully: {Path}", tempPath);
                return tempPath;
            }

            Logger.LogWarning("Stability AI returned no artifacts");
            return null;
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error generating image with Stability AI");
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
            SupportedAspectRatios = new() { "16:9", "9:16", "1:1", "4:3", "21:9", "9:21" },
            SupportedStyles = new()
            {
                "photorealistic", "digital-art", "anime", "3d-model",
                "fantasy-art", "analog-film", "neon-punk", "isometric",
                "low-poly", "origami", "line-art", "cinematic"
            },
            MaxWidth = 1536,
            MaxHeight = 1536,
            IsLocal = false,
            IsFree = false,
            CostPerImage = CostPerImageSDXL,
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
            var request = new HttpRequestMessage(HttpMethod.Get, $"{_baseUrl}/v1/user/account");
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

        if (!adapted.Contains("high quality") && !adapted.Contains("detailed"))
        {
            adapted += ", highly detailed, high quality";
        }

        if (options.Style == "photorealistic" && !adapted.Contains("photorealistic"))
        {
            adapted += ", photorealistic";
        }

        return adapted;
    }

    public override decimal GetCostEstimate(VisualGenerationOptions options)
    {
        return options.Width >= 1024 || options.Height >= 1024 ? CostPerImageSDXL : CostPerImageSD15;
    }

    private static string? MapStyleToPreset(string style)
    {
        return style.ToLowerInvariant() switch
        {
            "photorealistic" => "photographic",
            "digital-art" => "digital-art",
            "anime" => "anime",
            "3d-model" => "3d-model",
            "fantasy-art" => "fantasy-art",
            "analog-film" => "analog-film",
            "neon-punk" => "neon-punk",
            "isometric" => "isometric",
            "low-poly" => "low-poly",
            "origami" => "origami",
            "line-art" => "line-art",
            "cinematic" => "cinematic",
            _ => null
        };
    }

    private class StabilityResponse
    {
        public List<Artifact>? Artifacts { get; set; }
    }

    private class Artifact
    {
        public string? Base64 { get; set; }
        public string? FinishReason { get; set; }
        public int Seed { get; set; }
    }
}
