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
/// Stock image provider that fetches free images from Unsplash API.
/// API key is optional - can work with CC0 fallback.
/// </summary>
public class UnsplashStockProvider : IStockProvider
{
    private readonly ILogger<UnsplashStockProvider> _logger;
    private readonly HttpClient _httpClient;
    private readonly string? _apiKey;

    public UnsplashStockProvider(
        ILogger<UnsplashStockProvider> logger,
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
            _logger.LogWarning("Unsplash API key not provided, returning empty results");
            return Array.Empty<Asset>();
        }

        _logger.LogInformation("Searching Unsplash for: {Query} (count: {Count})", query, count);

        try
        {
            var url = $"https://api.unsplash.com/search/photos?query={Uri.EscapeDataString(query)}&per_page={Math.Min(count, 30)}";
            _httpClient.DefaultRequestHeaders.Remove("Authorization");
            _httpClient.DefaultRequestHeaders.Add("Authorization", $"Client-ID {_apiKey}");
            
            var response = await _httpClient.GetAsync(url, ct);
            response.EnsureSuccessStatusCode();

            var json = await response.Content.ReadAsStringAsync(ct);
            var doc = JsonDocument.Parse(json);

            var assets = new List<Asset>();

            if (doc.RootElement.TryGetProperty("results", out var results))
            {
                foreach (var result in results.EnumerateArray())
                {
                    if (assets.Count >= count)
                        break;

                    string? imageUrl = null;
                    if (result.TryGetProperty("urls", out var urls) &&
                        urls.TryGetProperty("regular", out var regular))
                    {
                        imageUrl = regular.GetString();
                    }

                    string? photographer = null;
                    if (result.TryGetProperty("user", out var user) &&
                        user.TryGetProperty("name", out var name))
                    {
                        photographer = name.GetString();
                    }

                    if (!string.IsNullOrEmpty(imageUrl))
                    {
                        assets.Add(new Asset(
                            Kind: "image",
                            PathOrUrl: imageUrl,
                            License: "Unsplash License (Free to use)",
                            Attribution: photographer != null ? $"Photo by {photographer} on Unsplash" : "Photo from Unsplash"
                        ));
                    }
                }
            }

            _logger.LogInformation("Found {Count} images on Unsplash", assets.Count);
            return assets;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error searching Unsplash for {Query}", query);
            return Array.Empty<Asset>();
        }
    }
}
