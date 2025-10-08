using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Aura.Core.Models;
using Aura.Core.Providers;
using Microsoft.Extensions.Logging;

namespace Aura.Providers.Images;

/// <summary>
/// Stock image provider that fetches free images from Pixabay API.
/// API key is optional - can work with CC0 fallback.
/// </summary>
public class PixabayStockProvider : IStockProvider
{
    private readonly ILogger<PixabayStockProvider> _logger;
    private readonly HttpClient _httpClient;
    private readonly string? _apiKey;

    public PixabayStockProvider(
        ILogger<PixabayStockProvider> logger,
        HttpClient httpClient,
        string? apiKey = null)
    {
        _logger = logger;
        _httpClient = httpClient;
        _apiKey = apiKey;
    }

    public async Task<IReadOnlyList<Asset>> SearchAsync(string query, int count, CancellationToken ct)
    {
        if (string.IsNullOrEmpty(_apiKey))
        {
            _logger.LogWarning("Pixabay API key not provided, returning empty results");
            return Array.Empty<Asset>();
        }

        _logger.LogInformation("Searching Pixabay for: {Query} (count: {Count})", query, count);

        try
        {
            var url = $"https://pixabay.com/api/?key={_apiKey}&q={Uri.EscapeDataString(query)}&per_page={Math.Min(count, 200)}&image_type=photo";
            var response = await _httpClient.GetAsync(url, ct);
            response.EnsureSuccessStatusCode();

            var json = await response.Content.ReadAsStringAsync(ct);
            var doc = JsonDocument.Parse(json);

            var assets = new List<Asset>();

            if (doc.RootElement.TryGetProperty("hits", out var hits))
            {
                foreach (var hit in hits.EnumerateArray())
                {
                    if (assets.Count >= count)
                        break;

                    string? imageUrl = null;
                    if (hit.TryGetProperty("largeImageURL", out var largeUrl))
                    {
                        imageUrl = largeUrl.GetString();
                    }

                    string? user = null;
                    if (hit.TryGetProperty("user", out var userProp))
                    {
                        user = userProp.GetString();
                    }

                    if (!string.IsNullOrEmpty(imageUrl))
                    {
                        assets.Add(new Asset(
                            Kind: "image",
                            PathOrUrl: imageUrl,
                            License: "Pixabay License (Free to use)",
                            Attribution: user != null ? $"Image by {user} on Pixabay" : "Image from Pixabay"
                        ));
                    }
                }
            }

            _logger.LogInformation("Found {Count} images on Pixabay", assets.Count);
            return assets;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error searching Pixabay for {Query}", query);
            return Array.Empty<Asset>();
        }
    }
}
