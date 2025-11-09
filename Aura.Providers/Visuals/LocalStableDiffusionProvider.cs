using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace Aura.Providers.Visuals;

/// <summary>
/// Local Stable Diffusion provider for self-hosted SD WebUI or ComfyUI
/// </summary>
public class LocalStableDiffusionProvider : BaseVisualProvider
{
    private readonly HttpClient _httpClient;
    private readonly string _webUiUrl;
    private readonly bool _isNvidiaGpu;
    private readonly int _vramGB;

    public LocalStableDiffusionProvider(
        ILogger<LocalStableDiffusionProvider> logger,
        HttpClient httpClient,
        string webUiUrl = "http://127.0.0.1:7860",
        bool isNvidiaGpu = true,
        int vramGB = 8) : base(logger)
    {
        _httpClient = httpClient;
        _webUiUrl = webUiUrl;
        _isNvidiaGpu = isNvidiaGpu;
        _vramGB = vramGB;
    }

    public override string ProviderName => "LocalSD";

    public override bool RequiresApiKey => false;

    public override async Task<string?> GenerateImageAsync(
        string prompt,
        VisualGenerationOptions options,
        CancellationToken ct = default)
    {
        if (!_isNvidiaGpu)
        {
            Logger.LogWarning("Local SD requires NVIDIA GPU");
            return null;
        }

        if (_vramGB < 6)
        {
            Logger.LogWarning("Local SD requires at least 6GB VRAM, have {VRAM}GB", _vramGB);
            return null;
        }

        try
        {
            Logger.LogInformation("Generating image with Local SD for prompt: {Prompt}", prompt);

            var adaptedPrompt = AdaptPrompt(prompt, options);
            var model = _vramGB >= 12 ? "sd_xl_base_1.0" : "v1-5-pruned-emaonly";

            var requestBody = new
            {
                prompt = adaptedPrompt,
                negative_prompt = options.NegativePrompts != null && options.NegativePrompts.Length > 0
                    ? string.Join(", ", options.NegativePrompts)
                    : "low quality, blurry, distorted",
                width = options.Width,
                height = options.Height,
                steps = 30,
                cfg_scale = 7,
                sampler_name = "DPM++ 2M Karras",
                override_settings = new
                {
                    sd_model_checkpoint = model
                }
            };

            var request = new HttpRequestMessage(HttpMethod.Post, $"{_webUiUrl}/sdapi/v1/txt2img");
            request.Content = new StringContent(
                JsonSerializer.Serialize(requestBody),
                Encoding.UTF8,
                "application/json");

            var response = await _httpClient.SendAsync(request, ct).ConfigureAwait(false);
            var content = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);

            if (!response.IsSuccessStatusCode)
            {
                Logger.LogWarning("Local SD request failed: {Status} - {Content}", response.StatusCode, content);
                return null;
            }

            var result = JsonSerializer.Deserialize<SdWebUiResponse>(content);
            if (result?.Images != null && result.Images.Count > 0)
            {
                var imageBytes = Convert.FromBase64String(result.Images[0]);
                var tempPath = Path.Combine(Path.GetTempPath(), $"localsd_{Guid.NewGuid()}.png");
                await File.WriteAllBytesAsync(tempPath, imageBytes, ct).ConfigureAwait(false);

                Logger.LogInformation("Local SD image generated successfully: {Path}", tempPath);
                return tempPath;
            }

            Logger.LogWarning("Local SD returned no images");
            return null;
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error generating image with Local SD");
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
            SupportsStylePresets = false,
            SupportedAspectRatios = new() { "16:9", "9:16", "1:1", "4:3", "3:2", "2:3" },
            SupportedStyles = new()
            {
                "photorealistic", "artistic", "anime", "digital-art",
                "fantasy", "realistic", "stylized"
            },
            MaxWidth = _vramGB >= 12 ? 1536 : 1024,
            MaxHeight = _vramGB >= 12 ? 1536 : 1024,
            IsLocal = true,
            IsFree = true,
            CostPerImage = 0m,
            Tier = "Free"
        };
    }

    public override async Task<bool> IsAvailableAsync(CancellationToken ct = default)
    {
        if (!_isNvidiaGpu || _vramGB < 6)
        {
            return false;
        }

        try
        {
            var response = await _httpClient.GetAsync($"{_webUiUrl}/sdapi/v1/sd-models", ct).ConfigureAwait(false);
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

        if (!adapted.Contains("masterpiece") && !adapted.Contains("high quality"))
        {
            adapted = "masterpiece, best quality, " + adapted;
        }

        if (options.Style == "photorealistic" && !adapted.Contains("photorealistic"))
        {
            adapted += ", photorealistic, 8k uhd, dslr";
        }
        else if (options.Style == "anime" && !adapted.Contains("anime"))
        {
            adapted += ", anime style, illustration";
        }

        return adapted;
    }

    public override decimal GetCostEstimate(VisualGenerationOptions options)
    {
        return 0m;
    }

    private class SdWebUiResponse
    {
        public List<string>? Images { get; set; }
        public string? Info { get; set; }
    }
}
