using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Aura.Core.Models;
using Aura.Core.Providers;
using Microsoft.Extensions.Logging;

namespace Aura.Providers.Images;

/// <summary>
/// Offline stock image provider that uses CC0 license images from local pack.
/// Fallback for when no API keys are available or offline mode is enabled.
/// </summary>
public class OfflineStockProvider : IStockProvider
{
    private readonly ILogger<OfflineStockProvider> _logger;
    private readonly string _cc0PackDirectory;
    private readonly List<string> _availableImages;

    public OfflineStockProvider(
        ILogger<OfflineStockProvider> logger,
        string? cc0PackDirectory = null)
    {
        _logger = logger;
        _cc0PackDirectory = cc0PackDirectory ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Aura", "cc0-pack");

        _availableImages = new List<string>();
        LoadAvailableImages();
    }

    private void LoadAvailableImages()
    {
        try
        {
            if (Directory.Exists(_cc0PackDirectory))
            {
                var extensions = new[] { ".jpg", ".jpeg", ".png", ".webp" };
                _availableImages.AddRange(
                    Directory.GetFiles(_cc0PackDirectory)
                        .Where(f => extensions.Contains(Path.GetExtension(f).ToLowerInvariant()))
                );
                _logger.LogInformation("Loaded {Count} CC0 images from {Directory}", 
                    _availableImages.Count, _cc0PackDirectory);
            }
            else
            {
                _logger.LogWarning("CC0 pack directory not found: {Directory}", _cc0PackDirectory);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading CC0 pack from {Directory}", _cc0PackDirectory);
        }
    }

    public Task<IReadOnlyList<Asset>> SearchAsync(string query, int count, CancellationToken ct)
    {
        _logger.LogInformation("Searching offline CC0 pack for: {Query} (count: {Count})", query, count);

        var assets = new List<Asset>();

        if (_availableImages.Count == 0)
        {
            _logger.LogWarning("No CC0 images available in pack");
            // Return placeholder/solid color slides as ultimate fallback
            for (int i = 0; i < count; i++)
            {
                assets.Add(new Asset(
                    Kind: "slide",
                    PathOrUrl: "solid:#1e1e1e", // Dark gray solid color
                    License: "CC0 (Public Domain)",
                    Attribution: "Solid color slide"
                ));
            }
            return Task.FromResult<IReadOnlyList<Asset>>(assets);
        }

        // Simple selection: pick random images from pack
        var random = new Random();
        var selectedCount = Math.Min(count, _availableImages.Count);
        
        var selected = _availableImages
            .OrderBy(_ => random.Next())
            .Take(selectedCount)
            .ToList();

        foreach (var imagePath in selected)
        {
            assets.Add(new Asset(
                Kind: "image",
                PathOrUrl: imagePath,
                License: "CC0 (Public Domain)",
                Attribution: "From local CC0 pack"
            ));
        }

        // If we need more images than available, reuse with solid color slides
        while (assets.Count < count)
        {
            assets.Add(new Asset(
                Kind: "slide",
                PathOrUrl: "solid:#2d2d2d",
                License: "CC0 (Public Domain)",
                Attribution: "Solid color slide"
            ));
        }

        _logger.LogInformation("Selected {Count} assets from offline CC0 pack", assets.Count);
        return Task.FromResult<IReadOnlyList<Asset>>(assets);
    }
}
