using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using Microsoft.Extensions.Logging;

namespace Aura.Core.Services.Render;

/// <summary>
/// Lower third configuration for speaker identification
/// </summary>
public record LowerThirdConfig(
    string Name,
    string? Title,
    TimeSpan StartTime,
    TimeSpan Duration,
    string? FontFile = null,
    int FontSize = 36,
    string BackgroundColor = "black@0.7",
    string TextColor = "white",
    string Position = "bottom");

/// <summary>
/// Progress bar configuration for educational content
/// </summary>
public record ProgressBarConfig(
    string Style,
    string Color = "blue",
    string BackgroundColor = "gray@0.5",
    int Height = 8,
    string Position = "bottom");

/// <summary>
/// Intro/outro sequence configuration
/// </summary>
public record IntroOutroConfig(
    string Type,
    string? VideoPath,
    string? ImagePath,
    double DurationSeconds,
    string? Text,
    string? LogoPath,
    bool FadeIn = true,
    bool FadeOut = true);

/// <summary>
/// Animated text overlay configuration
/// </summary>
public record AnimatedTextConfig(
    string Text,
    TimeSpan StartTime,
    TimeSpan Duration,
    string AnimationType,
    string? FontFile = null,
    int FontSize = 72,
    string Color = "white",
    string? BackgroundColor = null,
    string Position = "center");

/// <summary>
/// Picture-in-picture configuration
/// </summary>
public record PictureInPictureConfig(
    string VideoPath,
    TimeSpan StartTime,
    TimeSpan Duration,
    string Position = "bottom-right",
    double Scale = 0.25,
    int BorderWidth = 2,
    string BorderColor = "white");

/// <summary>
/// Service for creating professional video features and overlays
/// </summary>
public class ProfessionalFeaturesService
{
    private readonly ILogger<ProfessionalFeaturesService> _logger;

    public ProfessionalFeaturesService(ILogger<ProfessionalFeaturesService> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Builds filter for lower third overlay (name and title display)
    /// </summary>
    public string BuildLowerThirdFilter(LowerThirdConfig config, int videoWidth, int videoHeight)
    {
        _logger.LogDebug(
            "Building lower third for {Name} at {StartTime}",
            config.Name, config.StartTime
        );

        var startSec = config.StartTime.TotalSeconds.ToString(CultureInfo.InvariantCulture);
        var endSec = (config.StartTime + config.Duration).TotalSeconds.ToString(CultureInfo.InvariantCulture);

        var yPosition = config.Position.ToLowerInvariant() switch
        {
            "top" => "50",
            "middle" => "(H-text_h)/2",
            "bottom" => $"H-{config.FontSize * 3}",
            _ => $"H-{config.FontSize * 3}"
        };

        var filters = new List<string>();

        var nameEscaped = EscapeText(config.Name);
        var nameFilter = new StringBuilder();
        nameFilter.Append($"drawtext=text='{nameEscaped}':");
        nameFilter.Append($"fontsize={config.FontSize}:");
        nameFilter.Append($"fontcolor={config.TextColor}:");
        nameFilter.Append($"x=(W-text_w)/2:");
        nameFilter.Append($"y={yPosition}:");
        nameFilter.Append($"box=1:boxcolor={config.BackgroundColor}:boxborderw=15:");
        
        if (!string.IsNullOrEmpty(config.FontFile))
        {
            nameFilter.Append($"fontfile={config.FontFile}:");
        }
        
        nameFilter.Append($"enable='between(t,{startSec},{endSec})'");
        filters.Add(nameFilter.ToString());

        if (!string.IsNullOrEmpty(config.Title))
        {
            var titleEscaped = EscapeText(config.Title);
            var titleYPosition = config.Position.ToLowerInvariant() switch
            {
                "top" => $"50+{config.FontSize + 10}",
                "middle" => $"(H-text_h)/2+{config.FontSize + 10}",
                "bottom" => $"H-{config.FontSize * 2}",
                _ => $"H-{config.FontSize * 2}"
            };

            var titleFilter = new StringBuilder();
            titleFilter.Append($"drawtext=text='{titleEscaped}':");
            titleFilter.Append($"fontsize={config.FontSize * 2 / 3}:");
            titleFilter.Append($"fontcolor={config.TextColor}:");
            titleFilter.Append($"x=(W-text_w)/2:");
            titleFilter.Append($"y={titleYPosition}:");
            titleFilter.Append($"box=1:boxcolor={config.BackgroundColor}:boxborderw=10:");
            
            if (!string.IsNullOrEmpty(config.FontFile))
            {
                titleFilter.Append($"fontfile={config.FontFile}:");
            }
            
            titleFilter.Append($"enable='between(t,{startSec},{endSec})'");
            filters.Add(titleFilter.ToString());
        }

        return string.Join(",", filters);
    }

    /// <summary>
    /// Builds filter for progress bar overlay
    /// </summary>
    public string BuildProgressBarFilter(ProgressBarConfig config, int videoWidth, int videoHeight, double totalDuration)
    {
        _logger.LogDebug("Building {Style} progress bar", config.Style);

        var yPosition = config.Position.ToLowerInvariant() switch
        {
            "top" => "0",
            "middle" => $"(H-{config.Height})/2",
            "bottom" => $"H-{config.Height}",
            _ => $"H-{config.Height}"
        };

        var duration = totalDuration.ToString(CultureInfo.InvariantCulture);
        var height = config.Height.ToString();

        var filter = new StringBuilder();
        
        filter.Append($"drawbox=x=0:y={yPosition}:w=W:h={height}:color={config.BackgroundColor}:t=fill,");
        
        filter.Append($"drawbox=x=0:y={yPosition}:");
        filter.Append($"w='W*t/{duration}':h={height}:");
        filter.Append($"color={config.Color}:t=fill");

        return filter.ToString();
    }

    /// <summary>
    /// Builds filter for animated text overlay with various animation styles
    /// </summary>
    public string BuildAnimatedTextFilter(AnimatedTextConfig config, int videoWidth, int videoHeight)
    {
        _logger.LogDebug(
            "Building {Animation} text animation for '{Text}'",
            config.AnimationType, config.Text
        );

        var startSec = config.StartTime.TotalSeconds.ToString(CultureInfo.InvariantCulture);
        var endSec = (config.StartTime + config.Duration).TotalSeconds.ToString(CultureInfo.InvariantCulture);
        var duration = config.Duration.TotalSeconds.ToString(CultureInfo.InvariantCulture);
        var textEscaped = EscapeText(config.Text);

        var (xExpr, yExpr) = GetAnimationExpressions(
            config.AnimationType,
            config.Position,
            startSec,
            duration,
            videoWidth,
            videoHeight
        );

        var filter = new StringBuilder();
        filter.Append($"drawtext=text='{textEscaped}':");
        filter.Append($"fontsize={config.FontSize}:");
        filter.Append($"fontcolor={config.Color}:");
        filter.Append($"x='{xExpr}':");
        filter.Append($"y='{yExpr}':");
        
        if (!string.IsNullOrEmpty(config.FontFile))
        {
            filter.Append($"fontfile={config.FontFile}:");
        }
        
        if (!string.IsNullOrEmpty(config.BackgroundColor))
        {
            filter.Append($"box=1:boxcolor={config.BackgroundColor}:boxborderw=10:");
        }

        var fadeInDur = Math.Min(0.5, config.Duration.TotalSeconds / 4);
        var fadeOutStart = (config.StartTime.TotalSeconds + config.Duration.TotalSeconds - fadeInDur).ToString(CultureInfo.InvariantCulture);
        var fadeInDurStr = fadeInDur.ToString(CultureInfo.InvariantCulture);
        
        var alpha = $"if(lt(t,{startSec}),0,if(lt(t,{startSec}+{fadeInDurStr}),(t-{startSec})/{fadeInDurStr},if(lt(t,{fadeOutStart}),1,({endSec}-t)/{fadeInDurStr})))";
        filter.Append($"alpha='{alpha}':");
        
        filter.Append($"enable='between(t,{startSec},{endSec})'");

        return filter.ToString();
    }

    /// <summary>
    /// Builds intro sequence filter
    /// </summary>
    public string BuildIntroFilter(IntroOutroConfig config, int videoWidth, int videoHeight)
    {
        _logger.LogInformation("Building intro sequence");

        var filters = new List<string>();

        if (!string.IsNullOrEmpty(config.ImagePath))
        {
            var duration = config.DurationSeconds.ToString(CultureInfo.InvariantCulture);
            var filter = $"movie={config.ImagePath},scale={videoWidth}:{videoHeight}:force_original_aspect_ratio=decrease,pad={videoWidth}:{videoHeight}:(ow-iw)/2:(oh-ih)/2,setpts=PTS-STARTPTS,setdar={videoWidth}/{videoHeight}";
            
            if (config.FadeIn)
            {
                filter += $",fade=t=in:st=0:d=0.5";
            }
            if (config.FadeOut)
            {
                var fadeStart = (config.DurationSeconds - 0.5).ToString(CultureInfo.InvariantCulture);
                filter += $",fade=t=out:st={fadeStart}:d=0.5";
            }
            
            filters.Add(filter);
        }

        if (!string.IsNullOrEmpty(config.Text))
        {
            var textFilter = $"drawtext=text='{EscapeText(config.Text)}':fontsize=72:fontcolor=white:x=(W-text_w)/2:y=(H-text_h)/2";
            filters.Add(textFilter);
        }

        if (!string.IsNullOrEmpty(config.LogoPath))
        {
            var logoFilter = $"movie={config.LogoPath},scale=-1:100[logo];[in][logo]overlay=W-w-20:20";
            filters.Add(logoFilter);
        }

        return filters.Count > 0 ? string.Join(",", filters) : "null";
    }

    /// <summary>
    /// Builds outro sequence filter
    /// </summary>
    public string BuildOutroFilter(IntroOutroConfig config, int videoWidth, int videoHeight, double videoStartTime)
    {
        _logger.LogInformation("Building outro sequence");

        var filters = new List<string>();
        var startSec = videoStartTime.ToString(CultureInfo.InvariantCulture);
        var endSec = (videoStartTime + config.DurationSeconds).ToString(CultureInfo.InvariantCulture);

        if (!string.IsNullOrEmpty(config.ImagePath))
        {
            var filter = $"movie={config.ImagePath},scale={videoWidth}:{videoHeight}:force_original_aspect_ratio=decrease,pad={videoWidth}:{videoHeight}:(ow-iw)/2:(oh-ih)/2";
            
            if (config.FadeIn)
            {
                filter += $",fade=t=in:st={startSec}:d=0.5";
            }
            if (config.FadeOut)
            {
                var fadeStart = (videoStartTime + config.DurationSeconds - 0.5).ToString(CultureInfo.InvariantCulture);
                filter += $",fade=t=out:st={fadeStart}:d=0.5";
            }
            
            filters.Add(filter);
        }

        if (!string.IsNullOrEmpty(config.Text))
        {
            var textFilter = $"drawtext=text='{EscapeText(config.Text)}':fontsize=64:fontcolor=white:x=(W-text_w)/2:y=(H-text_h)/2:enable='between(t,{startSec},{endSec})'";
            filters.Add(textFilter);
        }

        return filters.Count > 0 ? string.Join(",", filters) : "null";
    }

    /// <summary>
    /// Builds picture-in-picture overlay filter
    /// </summary>
    public string BuildPictureInPictureFilter(PictureInPictureConfig config, int mainWidth, int mainHeight)
    {
        _logger.LogDebug(
            "Building picture-in-picture at {Position} with scale {Scale}",
            config.Position, config.Scale
        );

        var startSec = config.StartTime.TotalSeconds.ToString(CultureInfo.InvariantCulture);
        var endSec = (config.StartTime + config.Duration).TotalSeconds.ToString(CultureInfo.InvariantCulture);
        var scale = config.Scale.ToString(CultureInfo.InvariantCulture);

        var (x, y) = GetPictureInPicturePosition(config.Position, mainWidth, mainHeight);

        var filter = new StringBuilder();
        filter.Append($"movie={config.VideoPath},setpts=PTS-STARTPTS+{startSec}/TB");
        filter.Append($",scale=iw*{scale}:ih*{scale}");
        
        if (config.BorderWidth > 0)
        {
            filter.Append($",pad=iw+{config.BorderWidth * 2}:ih+{config.BorderWidth * 2}:{config.BorderWidth}:{config.BorderWidth}:{config.BorderColor}");
        }
        
        filter.Append($"[pip];[in][pip]overlay={x}:{y}:enable='between(t,{startSec},{endSec})'");

        return filter.ToString();
    }

    /// <summary>
    /// Creates a call-to-action overlay
    /// </summary>
    public string BuildCallToActionFilter(
        string text,
        string buttonText,
        TimeSpan startTime,
        TimeSpan duration,
        int videoWidth,
        int videoHeight)
    {
        var startSec = startTime.TotalSeconds.ToString(CultureInfo.InvariantCulture);
        var endSec = (startTime + duration).TotalSeconds.ToString(CultureInfo.InvariantCulture);
        
        var textEscaped = EscapeText(text);
        var buttonEscaped = EscapeText(buttonText);

        var filters = new List<string>();
        
        var mainTextFilter = $"drawtext=text='{textEscaped}':fontsize=48:fontcolor=white:x=(W-text_w)/2:y=H/2-60:box=1:boxcolor=black@0.6:boxborderw=20:enable='between(t,{startSec},{endSec})'";
        filters.Add(mainTextFilter);

        var buttonFilter = $"drawbox=x=(W-300)/2:y=H/2+20:w=300:h=60:color=blue@0.8:t=fill:enable='between(t,{startSec},{endSec})'";
        filters.Add(buttonFilter);

        var buttonTextFilter = $"drawtext=text='{buttonEscaped}':fontsize=32:fontcolor=white:x=(W-text_w)/2:y=H/2+30:enable='between(t,{startSec},{endSec})'";
        filters.Add(buttonTextFilter);

        return string.Join(",", filters);
    }

    /// <summary>
    /// Creates a countdown timer overlay
    /// </summary>
    public string BuildCountdownTimerFilter(
        TimeSpan startTime,
        double countdownSeconds,
        string position = "top-right",
        int fontSize = 72)
    {
        var startSec = startTime.TotalSeconds.ToString(CultureInfo.InvariantCulture);
        var countdownDur = countdownSeconds.ToString(CultureInfo.InvariantCulture);
        var endSec = (startTime.TotalSeconds + countdownSeconds).ToString(CultureInfo.InvariantCulture);

        var (x, y) = position.ToLowerInvariant() switch
        {
            "top-left" => ("20", "20"),
            "top-right" => ("W-text_w-20", "20"),
            "bottom-left" => ("20", "H-text_h-20"),
            "bottom-right" => ("W-text_w-20", "H-text_h-20"),
            "center" => ("(W-text_w)/2", "(H-text_h)/2"),
            _ => ("W-text_w-20", "20")
        };

        var timeExpression = $"'%{{eif\\\\:{countdownDur}-(t-{startSec})\\\\:d\\\\:2}}'";
        
        return $"drawtext=text={timeExpression}:fontsize={fontSize}:fontcolor=white:x={x}:y={y}:box=1:boxcolor=black@0.7:boxborderw=10:enable='between(t,{startSec},{endSec})'";
    }

    private (string x, string y) GetAnimationExpressions(
        string animationType,
        string position,
        string startSec,
        string duration,
        int width,
        int height)
    {
        var (baseX, baseY) = GetTextPosition(position, width, height);

        return animationType.ToLowerInvariant() switch
        {
            "slide-left" => ($"W-((t-{startSec})/{duration})*(W+text_w)", baseY),
            "slide-right" => ($"-text_w+((t-{startSec})/{duration})*(W+text_w)", baseY),
            "slide-up" => (baseX, $"H-((t-{startSec})/{duration})*(H+text_h)"),
            "slide-down" => (baseX, $"-text_h+((t-{startSec})/{duration})*(H+text_h)"),
            "zoom-in" => (baseX, baseY),
            "bounce" => (baseX, $"{baseY}+20*sin(2*3.14*(t-{startSec}))"),
            "pulse" => (baseX, baseY),
            "typewriter" => (baseX, baseY),
            _ => (baseX, baseY)
        };
    }

    private (string x, string y) GetTextPosition(string position, int width, int height)
    {
        return position.ToLowerInvariant() switch
        {
            "top-left" => ("20", "20"),
            "top-center" => ("(W-text_w)/2", "20"),
            "top-right" => ("W-text_w-20", "20"),
            "middle-left" => ("20", "(H-text_h)/2"),
            "center" => ("(W-text_w)/2", "(H-text_h)/2"),
            "middle-right" => ("W-text_w-20", "(H-text_h)/2"),
            "bottom-left" => ("20", "H-text_h-20"),
            "bottom-center" => ("(W-text_w)/2", "H-text_h-20"),
            "bottom-right" => ("W-text_w-20", "H-text_h-20"),
            _ => ("(W-text_w)/2", "(H-text_h)/2")
        };
    }

    private (string x, string y) GetPictureInPicturePosition(string position, int mainWidth, int mainHeight)
    {
        var margin = 20;
        return position.ToLowerInvariant() switch
        {
            "top-left" => (margin.ToString(), margin.ToString()),
            "top-right" => ("W-w-" + margin, margin.ToString()),
            "bottom-left" => (margin.ToString(), "H-h-" + margin),
            "bottom-right" => ("W-w-" + margin, "H-h-" + margin),
            "center" => ("(W-w)/2", "(H-h)/2"),
            _ => ("W-w-" + margin, "H-h-" + margin)
        };
    }

    private string EscapeText(string text)
    {
        return text
            .Replace("\\", "\\\\")
            .Replace(":", "\\:")
            .Replace("'", "\\'")
            .Replace("%", "\\%");
    }
}
