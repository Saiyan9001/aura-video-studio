using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using Microsoft.Extensions.Logging;

namespace Aura.Core.Services.Render;

/// <summary>
/// Transition types supported
/// </summary>
public enum TransitionType
{
    None,
    Fade,
    Crossfade,
    Wipe,
    Slide,
    Dissolve,
    Zoom,
    Circular,
    Radial,
    Pixelize,
    Blur,
    Curtain
}

/// <summary>
/// Wipe direction for wipe transitions
/// </summary>
public enum WipeDirection
{
    Left,
    Right,
    Up,
    Down
}

/// <summary>
/// Slide direction for slide transitions
/// </summary>
public enum SlideDirection
{
    Left,
    Right,
    Up,
    Down
}

/// <summary>
/// Transition configuration
/// </summary>
public record TransitionConfig(
    TransitionType Type,
    double DurationSeconds,
    double OffsetSeconds,
    WipeDirection? WipeDirection = null,
    SlideDirection? SlideDirection = null,
    string? CustomTransition = null);

/// <summary>
/// Service for building professional video transitions using FFmpeg xfade filter
/// </summary>
public class TransitionEffectsService
{
    private readonly ILogger<TransitionEffectsService> _logger;

    public TransitionEffectsService(ILogger<TransitionEffectsService> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Builds FFmpeg filter for transition between two video clips
    /// </summary>
    public string BuildTransitionFilter(TransitionConfig config, int inputIndex1 = 0, int inputIndex2 = 1)
    {
        _logger.LogDebug(
            "Building {Type} transition at {Offset}s for {Duration}s",
            config.Type, config.OffsetSeconds, config.DurationSeconds
        );

        var duration = config.DurationSeconds.ToString(CultureInfo.InvariantCulture);
        var offset = config.OffsetSeconds.ToString(CultureInfo.InvariantCulture);

        var transitionName = config.Type switch
        {
            TransitionType.Fade => "fade",
            TransitionType.Crossfade => "fade",
            TransitionType.Wipe => GetWipeTransition(config.WipeDirection ?? Render.WipeDirection.Right),
            TransitionType.Slide => GetSlideTransition(config.SlideDirection ?? Render.SlideDirection.Left),
            TransitionType.Dissolve => "dissolve",
            TransitionType.Zoom => "zoomin",
            TransitionType.Circular => "circleopen",
            TransitionType.Radial => "radial",
            TransitionType.Pixelize => "pixelize",
            TransitionType.Blur => "fadeblack",
            TransitionType.Curtain => "vertopen",
            _ => "fade"
        };

        if (!string.IsNullOrEmpty(config.CustomTransition))
        {
            transitionName = config.CustomTransition;
        }

        return $"[{inputIndex1}:v][{inputIndex2}:v]xfade=transition={transitionName}:duration={duration}:offset={offset}";
    }

    /// <summary>
    /// Builds complex filter graph for multiple clips with transitions
    /// </summary>
    public string BuildMultiClipTransitionFilter(List<TransitionConfig> transitions, int clipCount)
    {
        if (clipCount < 2)
        {
            return "[0:v]copy[outv]";
        }

        if (transitions.Count == 0)
        {
            return BuildConcatFilter(clipCount);
        }

        var filterParts = new List<string>();
        var currentLabel = "0:v";

        for (int i = 0; i < transitions.Count && i < clipCount - 1; i++)
        {
            var nextLabel = i == transitions.Count - 1 ? "outv" : $"v{i + 1}";
            var nextInput = $"{i + 1}:v";

            var transition = BuildTransitionFilterWithLabels(
                transitions[i],
                currentLabel,
                nextInput,
                nextLabel
            );

            filterParts.Add(transition);
            currentLabel = nextLabel;
        }

        return string.Join(";", filterParts);
    }

    /// <summary>
    /// Creates a fade in effect at the start of a video
    /// </summary>
    public string BuildFadeInFilter(double durationSeconds, string inputLabel = "0:v", string outputLabel = "v1")
    {
        var duration = durationSeconds.ToString(CultureInfo.InvariantCulture);
        return $"[{inputLabel}]fade=t=in:st=0:d={duration}[{outputLabel}]";
    }

    /// <summary>
    /// Creates a fade out effect at the end of a video
    /// </summary>
    public string BuildFadeOutFilter(double startSeconds, double durationSeconds, string inputLabel = "0:v", string outputLabel = "v1")
    {
        var start = startSeconds.ToString(CultureInfo.InvariantCulture);
        var duration = durationSeconds.ToString(CultureInfo.InvariantCulture);
        return $"[{inputLabel}]fade=t=out:st={start}:d={duration}[{outputLabel}]";
    }

    /// <summary>
    /// Creates fade in and fade out for entire video
    /// </summary>
    public string BuildFadeInOutFilter(double fadeInDuration, double videoLength, double fadeOutDuration)
    {
        var fadeIn = fadeInDuration.ToString(CultureInfo.InvariantCulture);
        var fadeOutStart = (videoLength - fadeOutDuration).ToString(CultureInfo.InvariantCulture);
        var fadeOutDur = fadeOutDuration.ToString(CultureInfo.InvariantCulture);
        
        return $"fade=t=in:st=0:d={fadeIn},fade=t=out:st={fadeOutStart}:d={fadeOutDur}";
    }

    /// <summary>
    /// Gets all available transition types
    /// </summary>
    public List<string> GetAvailableTransitions()
    {
        return new List<string>
        {
            "fade", "fadeblack", "fadewhite",
            "distance", "wipeleft", "wiperight", "wipeup", "wipedown",
            "slideleft", "slideright", "slideup", "slidedown",
            "smoothleft", "smoothright", "smoothup", "smoothdown",
            "circlecrop", "rectcrop", "circleclose", "circleopen",
            "horzclose", "horzopen", "vertclose", "vertopen",
            "diagbl", "diagbr", "diagtl", "diagtr",
            "hlslice", "hrslice", "vuslice", "vdslice",
            "dissolve", "pixelize", "radial",
            "hblur", "fadegrays", "wipetl", "wipetr", "wipebl", "wipebr",
            "squeezeh", "squeezev", "zoomin"
        };
    }

    /// <summary>
    /// Validates transition timing for multiple clips
    /// </summary>
    public bool ValidateTransitionTiming(
        List<double> clipDurations,
        List<TransitionConfig> transitions,
        out string? errorMessage)
    {
        if (clipDurations.Count < 2)
        {
            errorMessage = "At least 2 clips required for transitions";
            return false;
        }

        if (transitions.Count != clipDurations.Count - 1)
        {
            errorMessage = $"Expected {clipDurations.Count - 1} transitions, got {transitions.Count}";
            return false;
        }

        var cumulativeTime = 0.0;
        for (int i = 0; i < transitions.Count; i++)
        {
            var clipDuration = clipDurations[i];
            var transition = transitions[i];

            cumulativeTime += clipDuration;

            var expectedOffset = cumulativeTime - transition.DurationSeconds;
            var offsetDiff = Math.Abs(transition.OffsetSeconds - expectedOffset);

            if (offsetDiff > 0.1)
            {
                errorMessage = $"Transition {i} offset mismatch: expected ~{expectedOffset:F2}s, got {transition.OffsetSeconds:F2}s";
                return false;
            }

            if (transition.DurationSeconds > clipDuration || transition.DurationSeconds > clipDurations[i + 1])
            {
                errorMessage = $"Transition {i} duration ({transition.DurationSeconds}s) exceeds clip duration";
                return false;
            }
        }

        errorMessage = null;
        return true;
    }

    /// <summary>
    /// Calculates optimal transition offset for clips
    /// </summary>
    public List<TransitionConfig> CalculateTransitionOffsets(
        List<double> clipDurations,
        TransitionType defaultType = TransitionType.Crossfade,
        double transitionDuration = 0.5)
    {
        var transitions = new List<TransitionConfig>();
        var cumulativeTime = 0.0;

        for (int i = 0; i < clipDurations.Count - 1; i++)
        {
            cumulativeTime += clipDurations[i];
            var offset = cumulativeTime - transitionDuration;

            transitions.Add(new TransitionConfig(
                Type: defaultType,
                DurationSeconds: transitionDuration,
                OffsetSeconds: Math.Max(0, offset)
            ));
        }

        _logger.LogDebug("Calculated {Count} transition offsets", transitions.Count);
        return transitions;
    }

    /// <summary>
    /// Creates a cinematic black fade transition
    /// </summary>
    public string BuildCinematicFadeFilter(double blackDuration = 0.3, double fadeDuration = 0.5)
    {
        var black = blackDuration.ToString(CultureInfo.InvariantCulture);
        var fade = fadeDuration.ToString(CultureInfo.InvariantCulture);
        
        return $"fade=t=out:st=0:d={fade}:color=black,fade=t=in:st={black}:d={fade}:color=black";
    }

    private string BuildTransitionFilterWithLabels(
        TransitionConfig config,
        string input1Label,
        string input2Label,
        string outputLabel)
    {
        var duration = config.DurationSeconds.ToString(CultureInfo.InvariantCulture);
        var offset = config.OffsetSeconds.ToString(CultureInfo.InvariantCulture);

        var transitionName = config.Type switch
        {
            TransitionType.Fade => "fade",
            TransitionType.Crossfade => "fade",
            TransitionType.Wipe => GetWipeTransition(config.WipeDirection ?? Render.WipeDirection.Right),
            TransitionType.Slide => GetSlideTransition(config.SlideDirection ?? Render.SlideDirection.Left),
            TransitionType.Dissolve => "dissolve",
            TransitionType.Zoom => "zoomin",
            TransitionType.Circular => "circleopen",
            TransitionType.Radial => "radial",
            TransitionType.Pixelize => "pixelize",
            TransitionType.Blur => "fadeblack",
            TransitionType.Curtain => "vertopen",
            _ => "fade"
        };

        if (!string.IsNullOrEmpty(config.CustomTransition))
        {
            transitionName = config.CustomTransition;
        }

        return $"[{input1Label}][{input2Label}]xfade=transition={transitionName}:duration={duration}:offset={offset}[{outputLabel}]";
    }

    private string GetWipeTransition(WipeDirection direction)
    {
        return direction switch
        {
            Render.WipeDirection.Left => "wipeleft",
            Render.WipeDirection.Right => "wiperight",
            Render.WipeDirection.Up => "wipeup",
            Render.WipeDirection.Down => "wipedown",
            _ => "wiperight"
        };
    }

    private string GetSlideTransition(SlideDirection direction)
    {
        return direction switch
        {
            Render.SlideDirection.Left => "slideleft",
            Render.SlideDirection.Right => "slideright",
            Render.SlideDirection.Up => "slideup",
            Render.SlideDirection.Down => "slidedown",
            _ => "slideleft"
        };
    }

    private string BuildConcatFilter(int clipCount)
    {
        var inputs = new StringBuilder();
        for (int i = 0; i < clipCount; i++)
        {
            inputs.Append($"[{i}:v]");
        }
        return $"{inputs}concat=n={clipCount}:v=1:a=0[outv]";
    }
}
