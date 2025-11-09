using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Aura.Core.Models.Visual;
using Microsoft.Extensions.Logging;

namespace Aura.Core.Services.Visual;

/// <summary>
/// Enhanced fallback service with tiered strategy for ensuring every scene gets visuals
/// Tier 1: AI Generation (Stable Diffusion/DALL-E)
/// Tier 2: Stock Photo Search
/// Tier 3: Abstract Backgrounds with Text
/// Tier 4: Solid Color with Scene Number (Emergency)
/// </summary>
public class EnhancedFallbackService
{
    private readonly ILogger<EnhancedFallbackService> _logger;

    public EnhancedFallbackService(ILogger<EnhancedFallbackService> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Generate fallback visual using tiered strategy
    /// </summary>
    public async Task<FallbackVisualResult> GenerateFallbackVisualAsync(
        OptimizedVisualPrompt prompt,
        FallbackTier tier,
        CancellationToken ct = default)
    {
        _logger.LogInformation("Generating tier {Tier} fallback for scene {SceneIndex}",
            tier, prompt.SceneIndex);

        return tier switch
        {
            FallbackTier.StockPhotos => await GenerateStockPhotoFallbackAsync(prompt, ct),
            FallbackTier.AbstractBackground => await GenerateAbstractBackgroundAsync(prompt, ct),
            FallbackTier.SolidColor => await GenerateSolidColorFallbackAsync(prompt, ct),
            _ => throw new ArgumentException($"Invalid fallback tier: {tier}")
        };
    }

    /// <summary>
    /// Generate stock photo fallback using smart keyword extraction
    /// </summary>
    private async Task<FallbackVisualResult> GenerateStockPhotoFallbackAsync(
        OptimizedVisualPrompt prompt,
        CancellationToken ct)
    {
        _logger.LogDebug("Generating stock photo fallback for scene {SceneIndex}", prompt.SceneIndex);

        await Task.Delay(1, ct);

        var keywords = ExtractSmartKeywords(prompt);
        var searchQuery = string.Join(" ", keywords.Take(3));

        var imageUrl = $"https://source.unsplash.com/1920x1080/?{Uri.EscapeDataString(searchQuery)}";

        return new FallbackVisualResult
        {
            SceneIndex = prompt.SceneIndex,
            ImageUrl = imageUrl,
            FallbackTier = FallbackTier.StockPhotos,
            Source = "Unsplash",
            Width = 1920,
            Height = 1080,
            Description = $"Stock photo for: {searchQuery}",
            Keywords = keywords,
            SuccessReason = $"Generated stock photo URL with keywords: {searchQuery}"
        };
    }

    /// <summary>
    /// Generate abstract background with text overlay
    /// </summary>
    private async Task<FallbackVisualResult> GenerateAbstractBackgroundAsync(
        OptimizedVisualPrompt prompt,
        CancellationToken ct)
    {
        _logger.LogDebug("Generating abstract background for scene {SceneIndex}", prompt.SceneIndex);

        await Task.Delay(1, ct);

        var gradient = GenerateGradientBackground(prompt);
        var textOverlay = GenerateTextOverlay(prompt);

        var imageUrl = $"fallback://abstract/{prompt.SceneIndex}";

        return new FallbackVisualResult
        {
            SceneIndex = prompt.SceneIndex,
            ImageUrl = imageUrl,
            FallbackTier = FallbackTier.AbstractBackground,
            Source = "Generated",
            Width = 1920,
            Height = 1080,
            Description = "Abstract gradient background with text overlay",
            GradientConfig = gradient,
            TextOverlay = textOverlay,
            SuccessReason = "Generated abstract background with scene text"
        };
    }

    /// <summary>
    /// Generate solid color fallback with scene number (emergency tier)
    /// </summary>
    private async Task<FallbackVisualResult> GenerateSolidColorFallbackAsync(
        OptimizedVisualPrompt prompt,
        CancellationToken ct)
    {
        _logger.LogWarning("Using emergency solid color fallback for scene {SceneIndex}", prompt.SceneIndex);

        await Task.Delay(1, ct);

        var color = SelectColorFromPrompt(prompt);
        var imageUrl = $"fallback://solid/{prompt.SceneIndex}/{color}";

        var textOverlay = new TextOverlay
        {
            Text = $"Scene {prompt.SceneIndex + 1}",
            FontSize = 72,
            Position = new TextPosition { X = 0.5, Y = 0.5 },
            Color = GetContrastColor(color),
            Font = "Arial",
            Alignment = "center"
        };

        return new FallbackVisualResult
        {
            SceneIndex = prompt.SceneIndex,
            ImageUrl = imageUrl,
            FallbackTier = FallbackTier.SolidColor,
            Source = "Emergency",
            Width = 1920,
            Height = 1080,
            Description = $"Solid {color} background with scene number",
            SolidColor = color,
            TextOverlay = textOverlay,
            SuccessReason = "Emergency fallback - solid color with scene identifier"
        };
    }

    /// <summary>
    /// Extract smart keywords from prompt for stock photo search
    /// </summary>
    private IReadOnlyList<string> ExtractSmartKeywords(OptimizedVisualPrompt prompt)
    {
        var keywords = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        if (!string.IsNullOrEmpty(prompt.BasePrompt.Subject))
        {
            var subjectWords = prompt.BasePrompt.Subject.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            foreach (var word in subjectWords.Take(3))
            {
                if (word.Length > 3)
                {
                    keywords.Add(word);
                }
            }
        }

        if (prompt.BasePrompt.NarrativeKeywords.Count > 0)
        {
            foreach (var keyword in prompt.BasePrompt.NarrativeKeywords.Take(3))
            {
                if (keyword.Length > 3 && !IsCommonWord(keyword))
                {
                    keywords.Add(keyword);
                }
            }
        }

        if (prompt.BasePrompt.StyleKeywords.Count > 0)
        {
            var styleKeyword = prompt.BasePrompt.StyleKeywords
                .FirstOrDefault(k => !k.Contains("quality") && !k.Contains("professional") && k.Length > 3);
            if (styleKeyword != null)
            {
                keywords.Add(styleKeyword);
            }
        }

        if (prompt.BasePrompt.Lighting?.TimeOfDay != null && prompt.BasePrompt.Lighting.TimeOfDay != "day")
        {
            keywords.Add(prompt.BasePrompt.Lighting.TimeOfDay);
        }

        if (keywords.Count == 0)
        {
            keywords.Add(prompt.BasePrompt.Style.ToString().ToLowerInvariant());
        }

        return keywords.ToList();
    }

    /// <summary>
    /// Generate gradient background configuration
    /// </summary>
    private GradientConfig GenerateGradientBackground(OptimizedVisualPrompt prompt)
    {
        var colorPalette = prompt.BasePrompt.ColorPalette.Count > 0
            ? prompt.BasePrompt.ColorPalette
            : new[] { "#34495E", "#2C3E50", "#1A252F" };

        var startColor = colorPalette.ElementAtOrDefault(0) ?? "#34495E";
        var endColor = colorPalette.ElementAtOrDefault(1) ?? "#2C3E50";

        var angle = DetermineGradientAngle(prompt.BasePrompt.Camera.Angle);

        return new GradientConfig
        {
            StartColor = startColor,
            EndColor = endColor,
            Angle = angle,
            Type = "linear"
        };
    }

    /// <summary>
    /// Generate text overlay configuration
    /// </summary>
    private TextOverlay GenerateTextOverlay(OptimizedVisualPrompt prompt)
    {
        var text = prompt.BasePrompt.Subject;
        if (string.IsNullOrEmpty(text) && prompt.BasePrompt.NarrativeKeywords.Count > 0)
        {
            text = string.Join(" • ", prompt.BasePrompt.NarrativeKeywords.Take(3));
        }
        if (string.IsNullOrEmpty(text))
        {
            text = $"Scene {prompt.SceneIndex + 1}";
        }

        return new TextOverlay
        {
            Text = text,
            FontSize = 48,
            Position = new TextPosition { X = 0.5, Y = 0.5 },
            Color = "#FFFFFF",
            Font = "Arial",
            Alignment = "center",
            Shadow = true
        };
    }

    /// <summary>
    /// Select color from prompt's color palette
    /// </summary>
    private string SelectColorFromPrompt(OptimizedVisualPrompt prompt)
    {
        if (prompt.BasePrompt.ColorPalette.Count > 0)
        {
            return prompt.BasePrompt.ColorPalette[0];
        }

        return prompt.BasePrompt.Style switch
        {
            VisualStyle.Dramatic => "#1A1A1A",
            VisualStyle.Cinematic => "#2C3E50",
            VisualStyle.Modern => "#34495E",
            VisualStyle.Vintage => "#8B7355",
            _ => "#34495E"
        };
    }

    /// <summary>
    /// Get contrasting color for text
    /// </summary>
    private string GetContrastColor(string hexColor)
    {
        if (hexColor.StartsWith("#") && hexColor.Length == 7)
        {
            var r = Convert.ToInt32(hexColor.Substring(1, 2), 16);
            var g = Convert.ToInt32(hexColor.Substring(3, 2), 16);
            var b = Convert.ToInt32(hexColor.Substring(5, 2), 16);

            var luminance = (0.299 * r + 0.587 * g + 0.114 * b) / 255;

            return luminance > 0.5 ? "#000000" : "#FFFFFF";
        }

        return "#FFFFFF";
    }

    /// <summary>
    /// Determine gradient angle based on camera angle
    /// </summary>
    private int DetermineGradientAngle(CameraAngle angle)
    {
        return angle switch
        {
            CameraAngle.HighAngle => 180,
            CameraAngle.LowAngle => 0,
            CameraAngle.DutchAngle => 45,
            _ => 135
        };
    }

    /// <summary>
    /// Check if word is common and should be filtered
    /// </summary>
    private static readonly HashSet<string> CommonWords = new(StringComparer.OrdinalIgnoreCase)
    {
        "the", "and", "with", "that", "this", "from", "have", "they", "what",
        "about", "which", "when", "where", "professional", "high", "quality"
    };

    private bool IsCommonWord(string word)
    {
        return CommonWords.Contains(word);
    }
}

/// <summary>
/// Fallback tier levels
/// </summary>
public enum FallbackTier
{
    /// <summary>
    /// Primary: AI generation (handled by providers, not fallback)
    /// </summary>
    AIGeneration = 0,

    /// <summary>
    /// Secondary: Stock photo search
    /// </summary>
    StockPhotos = 1,

    /// <summary>
    /// Tertiary: Abstract background with text
    /// </summary>
    AbstractBackground = 2,

    /// <summary>
    /// Emergency: Solid color with scene number
    /// </summary>
    SolidColor = 3
}

/// <summary>
/// Result of fallback visual generation
/// </summary>
public record FallbackVisualResult
{
    public int SceneIndex { get; init; }
    public string ImageUrl { get; init; } = string.Empty;
    public FallbackTier FallbackTier { get; init; }
    public string Source { get; init; } = string.Empty;
    public int Width { get; init; }
    public int Height { get; init; }
    public string Description { get; init; } = string.Empty;
    public IReadOnlyList<string> Keywords { get; init; } = Array.Empty<string>();
    public GradientConfig? GradientConfig { get; init; }
    public TextOverlay? TextOverlay { get; init; }
    public string? SolidColor { get; init; }
    public string SuccessReason { get; init; } = string.Empty;
}

/// <summary>
/// Gradient configuration
/// </summary>
public record GradientConfig
{
    public string StartColor { get; init; } = string.Empty;
    public string EndColor { get; init; } = string.Empty;
    public int Angle { get; init; }
    public string Type { get; init; } = "linear";
}

/// <summary>
/// Text overlay configuration
/// </summary>
public record TextOverlay
{
    public string Text { get; init; } = string.Empty;
    public int FontSize { get; init; }
    public TextPosition Position { get; init; } = new();
    public string Color { get; init; } = "#FFFFFF";
    public string Font { get; init; } = "Arial";
    public string Alignment { get; init; } = "center";
    public bool Shadow { get; init; }
}

/// <summary>
/// Text position in normalized coordinates
/// </summary>
public record TextPosition
{
    public double X { get; init; }
    public double Y { get; init; }
}
