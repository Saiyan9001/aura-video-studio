using System;
using Aura.Core.Services.Render;
using Microsoft.Extensions.Logging;
using Xunit;
using Moq;

namespace Aura.Tests.Services.Render;

public class ProfessionalFeaturesServiceTests
{
    private readonly Mock<ILogger<ProfessionalFeaturesService>> _loggerMock;
    private readonly ProfessionalFeaturesService _service;

    public ProfessionalFeaturesServiceTests()
    {
        _loggerMock = new Mock<ILogger<ProfessionalFeaturesService>>();
        _service = new ProfessionalFeaturesService(_loggerMock.Object);
    }

    [Fact]
    public void BuildLowerThirdFilter_WithNameOnly_ReturnsValidFilter()
    {
        var config = new LowerThirdConfig(
            Name: "John Doe",
            Title: null,
            StartTime: TimeSpan.FromSeconds(5),
            Duration: TimeSpan.FromSeconds(3)
        );

        var filter = _service.BuildLowerThirdFilter(config, 1920, 1080);

        Assert.Contains("drawtext", filter);
        Assert.Contains("John Doe", filter);
        Assert.Contains("fontsize=36", filter);
        Assert.Contains("enable='between(t,5,8)'", filter);
    }

    [Fact]
    public void BuildLowerThirdFilter_WithNameAndTitle_ReturnsValidFilter()
    {
        var config = new LowerThirdConfig(
            Name: "Jane Smith",
            Title: "CEO",
            StartTime: TimeSpan.FromSeconds(10),
            Duration: TimeSpan.FromSeconds(5)
        );

        var filter = _service.BuildLowerThirdFilter(config, 1920, 1080);

        Assert.Contains("drawtext", filter);
        Assert.Contains("Jane Smith", filter);
        Assert.Contains("CEO", filter);
        Assert.Contains("enable='between(t,10,15)'", filter);
    }

    [Fact]
    public void BuildProgressBarFilter_ReturnsValidFilter()
    {
        var config = new ProgressBarConfig(
            Style: "linear",
            Color: "blue",
            Height: 10
        );

        var filter = _service.BuildProgressBarFilter(config, 1920, 1080, 60.0);

        Assert.Contains("drawbox", filter);
        Assert.Contains("color=blue", filter);
        Assert.Contains("h=10", filter);
        Assert.Contains("W*t/60", filter);
    }

    [Fact]
    public void BuildAnimatedTextFilter_WithSlideLeft_ReturnsValidFilter()
    {
        var config = new AnimatedTextConfig(
            Text: "Hello World",
            StartTime: TimeSpan.FromSeconds(2),
            Duration: TimeSpan.FromSeconds(3),
            AnimationType: "slide-left",
            FontSize: 72
        );

        var filter = _service.BuildAnimatedTextFilter(config, 1920, 1080);

        Assert.Contains("drawtext", filter);
        Assert.Contains("Hello World", filter);
        Assert.Contains("fontsize=72", filter);
        Assert.Contains("enable='between(t,2,5)'", filter);
    }

    [Fact]
    public void BuildAnimatedTextFilter_WithZoomIn_ReturnsValidFilter()
    {
        var config = new AnimatedTextConfig(
            Text: "Important!",
            StartTime: TimeSpan.FromSeconds(1),
            Duration: TimeSpan.FromSeconds(2),
            AnimationType: "zoom-in",
            FontSize: 96,
            Color: "red"
        );

        var filter = _service.BuildAnimatedTextFilter(config, 1920, 1080);

        Assert.Contains("drawtext", filter);
        Assert.Contains("Important!", filter);
        Assert.Contains("fontsize=96", filter);
        Assert.Contains("fontcolor=red", filter);
        Assert.Contains("alpha=", filter);
    }

    [Fact]
    public void BuildIntroFilter_WithImageAndText_ReturnsValidFilter()
    {
        var config = new IntroOutroConfig(
            Type: "intro",
            VideoPath: null,
            ImagePath: "/path/to/image.jpg",
            DurationSeconds: 3.0,
            Text: "Welcome",
            LogoPath: null,
            FadeIn: true,
            FadeOut: true
        );

        var filter = _service.BuildIntroFilter(config, 1920, 1080);

        Assert.Contains("movie=/path/to/image.jpg", filter);
        Assert.Contains("scale=1920:1080", filter);
        Assert.Contains("fade=t=in", filter);
        Assert.Contains("fade=t=out", filter);
        Assert.Contains("Welcome", filter);
    }

    [Fact]
    public void BuildCallToActionFilter_ReturnsValidFilter()
    {
        var filter = _service.BuildCallToActionFilter(
            text: "Subscribe now!",
            buttonText: "Click Here",
            startTime: TimeSpan.FromSeconds(5),
            duration: TimeSpan.FromSeconds(3),
            videoWidth: 1920,
            videoHeight: 1080
        );

        Assert.Contains("drawtext", filter);
        Assert.Contains("Subscribe now!", filter);
        Assert.Contains("Click Here", filter);
        Assert.Contains("drawbox", filter);
        Assert.Contains("enable='between(t,5,8)'", filter);
    }

    [Fact]
    public void BuildCountdownTimerFilter_ReturnsValidFilter()
    {
        var filter = _service.BuildCountdownTimerFilter(
            startTime: TimeSpan.FromSeconds(10),
            countdownSeconds: 30.0,
            position: "top-right",
            fontSize: 72
        );

        Assert.Contains("drawtext", filter);
        Assert.Contains("fontsize=72", filter);
        Assert.Contains("enable='between(t,10,40)'", filter);
        Assert.Contains("W-text_w-20", filter);
    }

    [Fact]
    public void BuildPictureInPictureFilter_ReturnsValidFilter()
    {
        var config = new PictureInPictureConfig(
            VideoPath: "/path/to/pip.mp4",
            StartTime: TimeSpan.FromSeconds(5),
            Duration: TimeSpan.FromSeconds(10),
            Position: "bottom-right",
            Scale: 0.25,
            BorderWidth: 2,
            BorderColor: "white"
        );

        var filter = _service.BuildPictureInPictureFilter(config, 1920, 1080);

        Assert.Contains("movie=/path/to/pip.mp4", filter);
        Assert.Contains("scale=iw*0.25:ih*0.25", filter);
        Assert.Contains("overlay=", filter);
        Assert.Contains("enable='between(t,5,15)'", filter);
        Assert.Contains("pad=", filter);
        Assert.Contains("white", filter);
    }

    [Theory]
    [InlineData("top-left")]
    [InlineData("top-right")]
    [InlineData("bottom-left")]
    [InlineData("bottom-right")]
    [InlineData("center")]
    public void BuildPictureInPictureFilter_WithDifferentPositions_ReturnsValidFilter(string position)
    {
        var config = new PictureInPictureConfig(
            VideoPath: "/path/to/pip.mp4",
            StartTime: TimeSpan.FromSeconds(0),
            Duration: TimeSpan.FromSeconds(5),
            Position: position,
            Scale: 0.3
        );

        var filter = _service.BuildPictureInPictureFilter(config, 1920, 1080);

        Assert.Contains("movie=/path/to/pip.mp4", filter);
        Assert.Contains("scale=", filter);
        Assert.Contains("overlay=", filter);
    }
}
