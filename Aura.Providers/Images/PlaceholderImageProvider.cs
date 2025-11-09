using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Aura.Core.Models;
using Aura.Core.Providers;
using Microsoft.Extensions.Logging;
using SkiaSharp;

namespace Aura.Providers.Images;

/// <summary>
/// Fallback image provider that generates colored placeholder cards with text.
/// Always available - no external dependencies required.
/// Used when no other image providers are configured.
/// </summary>
public class PlaceholderImageProvider : IStockProvider
{
    private readonly ILogger<PlaceholderImageProvider> _logger;
    private readonly string _outputDirectory;
    private readonly Random _random;

    private static readonly SKColor[] ColorPalette =
    {
        SKColor.Parse("#2C3E50"), // Dark blue-gray
        SKColor.Parse("#E74C3C"), // Red
        SKColor.Parse("#3498DB"), // Blue
        SKColor.Parse("#2ECC71"), // Green
        SKColor.Parse("#F39C12"), // Orange
        SKColor.Parse("#9B59B6"), // Purple
        SKColor.Parse("#1ABC9C"), // Turquoise
        SKColor.Parse("#34495E"), // Midnight blue
        SKColor.Parse("#16A085"), // Green Sea
        SKColor.Parse("#27AE60"), // Nephritis
        SKColor.Parse("#2980B9"), // Belize Hole
        SKColor.Parse("#8E44AD"), // Wisteria
    };

    public PlaceholderImageProvider(
        ILogger<PlaceholderImageProvider> logger,
        string? outputDirectory = null)
    {
        _logger = logger;
        _outputDirectory = outputDirectory ?? Path.Combine(Path.GetTempPath(), "aura-placeholders");
        _random = new Random(42);

        if (!Directory.Exists(_outputDirectory))
        {
            Directory.CreateDirectory(_outputDirectory);
            _logger.LogInformation("Created placeholder image directory: {Directory}", _outputDirectory);
        }
    }

    public Task<IReadOnlyList<Asset>> SearchAsync(string query, int count, CancellationToken ct)
    {
        _logger.LogInformation("Generating {Count} placeholder images for: {Query}", count, query);

        var assets = new List<Asset>();

        try
        {
            for (int i = 0; i < count; i++)
            {
                var imagePath = GeneratePlaceholderImage(query, i);
                
                assets.Add(new Asset(
                    Kind: "image",
                    PathOrUrl: imagePath,
                    License: "Generated",
                    Attribution: "Aura Placeholder Generator"
                ));
            }

            _logger.LogInformation("Generated {Count} placeholder images", assets.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating placeholder images for: {Query}", query);
        }

        return Task.FromResult<IReadOnlyList<Asset>>(assets);
    }

    private string GeneratePlaceholderImage(string text, int index)
    {
        const int width = 1920;
        const int height = 1080;

        var fileName = $"placeholder_{Guid.NewGuid():N}.png";
        var outputPath = Path.Combine(_outputDirectory, fileName);

        var backgroundColor = ColorPalette[index % ColorPalette.Length];
        var textColor = GetContrastColor(backgroundColor);

        using var surface = SKSurface.Create(new SKImageInfo(width, height));
        var canvas = surface.Canvas;

        canvas.Clear(backgroundColor);

        var maxTextWidth = text.Length > 50 ? 50 : text.Length;
        var displayText = text.Length > maxTextWidth ? text.Substring(0, maxTextWidth) + "..." : text;

        var fontSize = 80f;
        using var paint = new SKPaint
        {
            Color = textColor,
            IsAntialias = true,
            Style = SKPaintStyle.Fill,
            TextAlign = SKTextAlign.Center,
            Typeface = SKTypeface.FromFamilyName("Arial", SKFontStyle.Bold),
            TextSize = fontSize
        };

        var textBounds = new SKRect();
        paint.MeasureText(displayText, ref textBounds);

        while (textBounds.Width > width - 200 && fontSize > 20)
        {
            fontSize -= 5;
            paint.TextSize = fontSize;
            paint.MeasureText(displayText, ref textBounds);
        }

        var x = width / 2f;
        var y = (height - textBounds.Height) / 2f - textBounds.Top;

        using (var shadowPaint = paint.Clone())
        {
            shadowPaint.Color = SKColors.Black.WithAlpha(128);
            canvas.DrawText(displayText, x + 4, y + 4, shadowPaint);
        }

        canvas.DrawText(displayText, x, y, paint);

        var iconSize = 120f;
        var iconY = y + textBounds.Height + 80;
        DrawIcon(canvas, width / 2f, iconY, iconSize, textColor.WithAlpha(180));

        using var image = surface.Snapshot();
        using var data = image.Encode(SKEncodedImageFormat.Png, 90);
        using var stream = File.OpenWrite(outputPath);
        data.SaveTo(stream);

        _logger.LogDebug("Generated placeholder image: {Path}", outputPath);
        return outputPath;
    }

    private void DrawIcon(SKCanvas canvas, float centerX, float centerY, float size, SKColor color)
    {
        using var paint = new SKPaint
        {
            Color = color,
            IsAntialias = true,
            Style = SKPaintStyle.Stroke,
            StrokeWidth = 8
        };

        var rect = new SKRect(
            centerX - size / 2,
            centerY - size / 2,
            centerX + size / 2,
            centerY + size / 2
        );

        canvas.DrawRoundRect(rect, 10, 10, paint);

        var innerSize = size * 0.5f;
        var innerRect = new SKRect(
            centerX - innerSize / 2,
            centerY - innerSize / 2,
            centerX + innerSize / 2,
            centerY + innerSize / 2
        );

        paint.Style = SKPaintStyle.Fill;
        canvas.DrawCircle(centerX, centerY, innerSize / 2, paint);
    }

    private SKColor GetContrastColor(SKColor backgroundColor)
    {
        var luminance = (0.299 * backgroundColor.Red + 
                        0.587 * backgroundColor.Green + 
                        0.114 * backgroundColor.Blue) / 255;

        return luminance > 0.5 ? SKColors.Black : SKColors.White;
    }
}
