using System;
using System.Collections.Generic;
using Aura.Core.Services.Render;
using Microsoft.Extensions.Logging;
using Xunit;
using Moq;

namespace Aura.Tests.Services.Render;

public class TransitionEffectsServiceTests
{
    private readonly Mock<ILogger<TransitionEffectsService>> _loggerMock;
    private readonly TransitionEffectsService _service;

    public TransitionEffectsServiceTests()
    {
        _loggerMock = new Mock<ILogger<TransitionEffectsService>>();
        _service = new TransitionEffectsService(_loggerMock.Object);
    }

    [Fact]
    public void BuildTransitionFilter_WithFade_ReturnsValidFilter()
    {
        var config = new TransitionConfig(
            Type: TransitionType.Fade,
            DurationSeconds: 0.5,
            OffsetSeconds: 5.0
        );

        var filter = _service.BuildTransitionFilter(config);

        Assert.Contains("xfade", filter);
        Assert.Contains("transition=fade", filter);
        Assert.Contains("duration=0.5", filter);
        Assert.Contains("offset=5", filter);
    }

    [Fact]
    public void BuildTransitionFilter_WithWipeLeft_ReturnsValidFilter()
    {
        var config = new TransitionConfig(
            Type: TransitionType.Wipe,
            DurationSeconds: 1.0,
            OffsetSeconds: 10.0,
            WipeDirection: WipeDirection.Left
        );

        var filter = _service.BuildTransitionFilter(config);

        Assert.Contains("xfade", filter);
        Assert.Contains("transition=wipeleft", filter);
        Assert.Contains("duration=1", filter);
        Assert.Contains("offset=10", filter);
    }

    [Fact]
    public void BuildTransitionFilter_WithSlideRight_ReturnsValidFilter()
    {
        var config = new TransitionConfig(
            Type: TransitionType.Slide,
            DurationSeconds: 0.75,
            OffsetSeconds: 3.5,
            SlideDirection: SlideDirection.Right
        );

        var filter = _service.BuildTransitionFilter(config);

        Assert.Contains("xfade", filter);
        Assert.Contains("transition=slideright", filter);
        Assert.Contains("duration=0.75", filter);
        Assert.Contains("offset=3.5", filter);
    }

    [Fact]
    public void BuildFadeInFilter_ReturnsValidFilter()
    {
        var filter = _service.BuildFadeInFilter(1.0);

        Assert.Contains("fade=t=in", filter);
        Assert.Contains("st=0", filter);
        Assert.Contains("d=1", filter);
    }

    [Fact]
    public void BuildFadeOutFilter_ReturnsValidFilter()
    {
        var filter = _service.BuildFadeOutFilter(9.0, 1.0);

        Assert.Contains("fade=t=out", filter);
        Assert.Contains("st=9", filter);
        Assert.Contains("d=1", filter);
    }

    [Fact]
    public void BuildFadeInOutFilter_ReturnsValidFilter()
    {
        var filter = _service.BuildFadeInOutFilter(0.5, 10.0, 0.5);

        Assert.Contains("fade=t=in", filter);
        Assert.Contains("st=0", filter);
        Assert.Contains("d=0.5", filter);
        Assert.Contains("fade=t=out", filter);
        Assert.Contains("st=9.5", filter);
    }

    [Fact]
    public void GetAvailableTransitions_ReturnsTransitionList()
    {
        var transitions = _service.GetAvailableTransitions();

        Assert.NotEmpty(transitions);
        Assert.Contains("fade", transitions);
        Assert.Contains("wipeleft", transitions);
        Assert.Contains("dissolve", transitions);
        Assert.Contains("zoomin", transitions);
    }

    [Fact]
    public void ValidateTransitionTiming_WithValidTransitions_ReturnsTrue()
    {
        var clipDurations = new List<double> { 5.0, 5.0, 5.0 };
        var transitions = new List<TransitionConfig>
        {
            new(TransitionType.Fade, 0.5, 4.5),
            new(TransitionType.Fade, 0.5, 9.5)
        };

        var result = _service.ValidateTransitionTiming(clipDurations, transitions, out var errorMessage);

        Assert.True(result);
        Assert.Null(errorMessage);
    }

    [Fact]
    public void ValidateTransitionTiming_WithWrongTransitionCount_ReturnsFalse()
    {
        var clipDurations = new List<double> { 5.0, 5.0, 5.0 };
        var transitions = new List<TransitionConfig>
        {
            new(TransitionType.Fade, 0.5, 4.5)
        };

        var result = _service.ValidateTransitionTiming(clipDurations, transitions, out var errorMessage);

        Assert.False(result);
        Assert.NotNull(errorMessage);
        Assert.Contains("Expected 2 transitions", errorMessage);
    }

    [Fact]
    public void ValidateTransitionTiming_WithTooFewClips_ReturnsFalse()
    {
        var clipDurations = new List<double> { 5.0 };
        var transitions = new List<TransitionConfig>();

        var result = _service.ValidateTransitionTiming(clipDurations, transitions, out var errorMessage);

        Assert.False(result);
        Assert.NotNull(errorMessage);
        Assert.Contains("At least 2 clips required", errorMessage);
    }

    [Fact]
    public void CalculateTransitionOffsets_ReturnsCorrectOffsets()
    {
        var clipDurations = new List<double> { 5.0, 5.0, 5.0 };
        var transitionDuration = 0.5;

        var transitions = _service.CalculateTransitionOffsets(clipDurations, TransitionType.Crossfade, transitionDuration);

        Assert.Equal(2, transitions.Count);
        Assert.Equal(4.5, transitions[0].OffsetSeconds);
        Assert.Equal(9.5, transitions[1].OffsetSeconds);
        Assert.All(transitions, t => Assert.Equal(0.5, t.DurationSeconds));
        Assert.All(transitions, t => Assert.Equal(TransitionType.Crossfade, t.Type));
    }

    [Fact]
    public void BuildCinematicFadeFilter_ReturnsValidFilter()
    {
        var filter = _service.BuildCinematicFadeFilter(0.3, 0.5);

        Assert.Contains("fade=t=out", filter);
        Assert.Contains("fade=t=in", filter);
        Assert.Contains("color=black", filter);
        Assert.Contains("d=0.5", filter);
    }
}
