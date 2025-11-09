using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using Microsoft.Extensions.Logging;

namespace Aura.Core.Services.Assets;

/// <summary>
/// Utility to generate simple placeholder sample images
/// </summary>
public class SampleImageGenerator
{
    private readonly ILogger<SampleImageGenerator> _logger;

    public SampleImageGenerator(ILogger<SampleImageGenerator> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Generate sample placeholder images in the specified directory
    /// </summary>
    public void GenerateSampleImages(string outputDirectory)
    {
        if (!Directory.Exists(outputDirectory))
        {
            Directory.CreateDirectory(outputDirectory);
        }

        _logger.LogInformation("Generating sample placeholder images in {Directory}", outputDirectory);

        GenerateGradientImage(
            Path.Combine(outputDirectory, "sample-gradient-01.jpg"),
            1920, 1080,
            Color.FromArgb(88, 101, 242), // Discord blue
            Color.FromArgb(114, 137, 218)); // Lighter blue

        GenerateGradientImage(
            Path.Combine(outputDirectory, "sample-abstract-01.jpg"),
            1920, 1080,
            Color.FromArgb(138, 43, 226), // Blue-violet
            Color.FromArgb(75, 0, 130)); // Indigo

        GenerateGradientImage(
            Path.Combine(outputDirectory, "sample-nature-01.jpg"),
            1920, 1080,
            Color.FromArgb(34, 139, 34), // Forest green
            Color.FromArgb(135, 206, 250)); // Sky blue

        GenerateGradientImage(
            Path.Combine(outputDirectory, "sample-tech-01.jpg"),
            1920, 1080,
            Color.FromArgb(0, 180, 216), // Cyan
            Color.FromArgb(100, 100, 100)); // Gray

        GenerateGradientImage(
            Path.Combine(outputDirectory, "sample-portrait-01.jpg"),
            1080, 1920,
            Color.FromArgb(255, 99, 71), // Tomato red
            Color.FromArgb(255, 165, 0)); // Orange

        GenerateGradientImage(
            Path.Combine(outputDirectory, "sample-minimal-01.jpg"),
            1920, 1080,
            Color.FromArgb(240, 240, 240), // Light gray
            Color.FromArgb(255, 255, 255)); // White

        _logger.LogInformation("Generated {Count} sample images", 6);
    }

    private void GenerateGradientImage(string outputPath, int width, int height, Color startColor, Color endColor)
    {
        try
        {
            using var bitmap = new Bitmap(width, height);
            using var graphics = Graphics.FromImage(bitmap);

            var colorBlend = new System.Drawing.Drawing2D.ColorBlend(3)
            {
                Colors = new[] { startColor, Color.FromArgb(
                    (startColor.R + endColor.R) / 2,
                    (startColor.G + endColor.G) / 2,
                    (startColor.B + endColor.B) / 2), endColor },
                Positions = new[] { 0.0f, 0.5f, 1.0f }
            };

            using var brush = new System.Drawing.Drawing2D.LinearGradientBrush(
                new Rectangle(0, 0, width, height),
                startColor,
                endColor,
                45f)
            {
                InterpolationColors = colorBlend
            };

            graphics.FillRectangle(brush, 0, 0, width, height);

            var encoder = GetEncoder(ImageFormat.Jpeg);
            var encoderParams = new EncoderParameters(1)
            {
                Param = { [0] = new EncoderParameter(System.Drawing.Imaging.Encoder.Quality, 90L) }
            };

            bitmap.Save(outputPath, encoder, encoderParams);
            _logger.LogDebug("Generated image: {Path}", outputPath);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to generate image: {Path}", outputPath);
        }
    }

    private static ImageCodecInfo GetEncoder(ImageFormat format)
    {
        var codecs = ImageCodecInfo.GetImageEncoders();
        foreach (var codec in codecs)
        {
            if (codec.FormatID == format.Guid)
            {
                return codec;
            }
        }
        return codecs[0];
    }
}
