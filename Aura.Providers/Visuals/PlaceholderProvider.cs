using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace Aura.Providers.Visuals;

/// <summary>
/// Placeholder visual provider that always succeeds by generating a simple image file.
/// This is the guaranteed fallback provider.
/// </summary>
public class PlaceholderProvider : BaseVisualProvider
{
    private static readonly (byte R, byte G, byte B)[] ProfessionalColors = new[]
    {
        ((byte)41, (byte)128, (byte)185),   // Blue
        ((byte)39, (byte)174, (byte)96),    // Green
        ((byte)142, (byte)68, (byte)173),   // Purple
        ((byte)230, (byte)126, (byte)34),   // Orange
        ((byte)231, (byte)76, (byte)60),    // Red
        ((byte)52, (byte)73, (byte)94),     // Dark Blue
        ((byte)44, (byte)62, (byte)80),     // Navy
        ((byte)149, (byte)165, (byte)166)   // Gray
    };

    public PlaceholderProvider(ILogger<PlaceholderProvider> logger) : base(logger)
    {
    }

    public override string ProviderName => "Placeholder";

    public override bool RequiresApiKey => false;

    /// <summary>
    /// Generates a single image from a prompt.
    /// Note: This implementation generates text-based placeholder files to avoid
    /// cross-platform graphics library dependencies. In production, replace with
    /// a proper image generation library like SkiaSharp or ImageSharp.
    /// </summary>
    public override Task<string?> GenerateImageAsync(
        string prompt,
        VisualGenerationOptions options,
        CancellationToken ct = default)
    {
        try
        {
            Logger.LogInformation("Generating placeholder image for prompt: {Prompt}", prompt);

            var tempPath = Path.Combine(Path.GetTempPath(), $"placeholder_{Guid.NewGuid()}.txt");
            
            var color = SelectColorForPrompt(prompt);
            var truncatedPrompt = prompt.Length > 50 ? prompt.Substring(0, 47) + "..." : prompt;
            
            var content = $"Placeholder Image\n" +
                         $"Prompt: {truncatedPrompt}\n" +
                         $"Size: {options.Width}x{options.Height}\n" +
                         $"Color: RGB({color.R},{color.G},{color.B})\n" +
                         $"Generated: {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC";
            
            File.WriteAllText(tempPath, content);

            Logger.LogInformation("Placeholder image generated successfully at: {Path}", tempPath);
            return Task.FromResult<string?>(tempPath);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Failed to generate placeholder image");
            return Task.FromResult<string?>(null);
        }
    }

    public override VisualProviderCapabilities GetProviderCapabilities()
    {
        return new VisualProviderCapabilities
        {
            ProviderName = ProviderName,
            SupportsNegativePrompts = false,
            SupportsBatchGeneration = true,
            SupportsStylePresets = false,
            SupportedAspectRatios = new List<string> { "16:9", "9:16", "1:1", "4:3" },
            SupportedStyles = new List<string> { "solid" },
            MaxWidth = 4096,
            MaxHeight = 4096,
            IsLocal = true,
            IsFree = true,
            CostPerImage = 0m,
            Tier = "Free"
        };
    }

    public override Task<bool> IsAvailableAsync(CancellationToken ct = default)
    {
        return Task.FromResult(true);
    }

    private static (byte R, byte G, byte B) SelectColorForPrompt(string prompt)
    {
        var hash = prompt.GetHashCode();
        var index = Math.Abs(hash) % ProfessionalColors.Length;
        return ProfessionalColors[index];
    }
}
