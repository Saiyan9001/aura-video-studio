using System;
using System.Collections.Generic;
using Aura.Core.Services.FFmpeg;
using Xunit;

namespace Aura.Tests.Services.FFmpeg;

/// <summary>
/// Tests for advanced FFmpegCommandBuilder features
/// </summary>
public class FFmpegCommandBuilderAdvancedFeaturesTests
{
    [Fact]
    public void AddCrossfadeTransition_IncludesXfadeFilter()
    {
        var builder = new FFmpegCommandBuilder()
            .AddInput("input1.mp4")
            .AddInput("input2.mp4")
            .AddCrossfadeTransition(1.0, 5.0)
            .SetOutput("output.mp4");

        var command = builder.Build();

        Assert.Contains("xfade=transition=fade:duration=1:offset=5", command);
    }

    [Fact]
    public void AddWipeTransition_WithDirection_IncludesCorrectTransition()
    {
        var builder = new FFmpegCommandBuilder()
            .AddInput("input1.mp4")
            .AddInput("input2.mp4")
            .AddWipeTransition(1.0, 5.0, "left")
            .SetOutput("output.mp4");

        var command = builder.Build();

        Assert.Contains("xfade=transition=wipeleft:duration=1:offset=5", command);
    }

    [Fact]
    public void AddDissolveTransition_IncludesDissolveFilter()
    {
        var builder = new FFmpegCommandBuilder()
            .AddInput("input1.mp4")
            .AddInput("input2.mp4")
            .AddDissolveTransition(2.0, 10.0)
            .SetOutput("output.mp4");

        var command = builder.Build();

        Assert.Contains("xfade=transition=dissolve:duration=2:offset=10", command);
    }

    [Fact]
    public void AddKenBurnsEffect_IncludesZoompanFilter()
    {
        var builder = new FFmpegCommandBuilder()
            .AddInput("image.jpg")
            .AddKenBurnsEffect(5.0, 1.0, 1.2, 0.1, 0.1)
            .SetOutput("output.mp4");

        var command = builder.Build();

        Assert.Contains("zoompan=", command);
        Assert.Contains("d=5", command);
    }

    [Fact]
    public void AddPictureInPicture_IncludesOverlayFilter()
    {
        var builder = new FFmpegCommandBuilder()
            .AddInput("main.mp4")
            .AddInput("pip.mp4")
            .AddPictureInPicture(1, "W-w-10", "H-h-10", 0.25)
            .SetOutput("output.mp4");

        var command = builder.Build();

        Assert.Contains("scale=iw*0.25:ih*0.25", command);
        Assert.Contains("overlay=W-w-10:H-h-10", command);
    }

    [Fact]
    public void AddTextOverlay_IncludesDrawtextFilter()
    {
        var builder = new FFmpegCommandBuilder()
            .AddInput("input.mp4")
            .AddTextOverlay("Hello World", null, 48, "(w-text_w)/2", "(h-text_h)/2", "white")
            .SetOutput("output.mp4");

        var command = builder.Build();

        Assert.Contains("drawtext=", command);
        Assert.Contains("text='Hello World'", command);
        Assert.Contains("fontsize=48", command);
        Assert.Contains("fontcolor=white", command);
    }

    [Fact]
    public void AddAnimatedTextOverlay_IncludesDrawtextWithAlpha()
    {
        var builder = new FFmpegCommandBuilder()
            .AddInput("input.mp4")
            .AddAnimatedTextOverlay("Fade Text", 2.0, 5.0, 0.5, 0.5, 48)
            .SetOutput("output.mp4");

        var command = builder.Build();

        Assert.Contains("drawtext=", command);
        Assert.Contains("text='Fade Text'", command);
        Assert.Contains("alpha=", command);
        Assert.Contains("enable='between(t,2,7)'", command);
    }

    [Fact]
    public void AddSlidingTextOverlay_WithDirection_IncludesCorrectMovement()
    {
        var builder = new FFmpegCommandBuilder()
            .AddInput("input.mp4")
            .AddSlidingTextOverlay("Slide Right", 1.0, 3.0, "right", 48)
            .SetOutput("output.mp4");

        var command = builder.Build();

        Assert.Contains("drawtext=", command);
        Assert.Contains("text='Slide Right'", command);
        Assert.Contains("enable='between(t,1,4)'", command);
    }

    [Fact]
    public void AddAudioMix_IncludesAmixFilter()
    {
        var builder = new FFmpegCommandBuilder()
            .AddInput("audio1.mp3")
            .AddInput("audio2.mp3")
            .AddAudioMix(2, new[] { 1.0, 0.5 })
            .SetOutput("output.mp3");

        var command = builder.Build();

        Assert.Contains("volume=1", command);
        Assert.Contains("volume=0.5", command);
        Assert.Contains("amix=inputs=2", command);
    }

    [Fact]
    public void AddAudioDucking_IncludesSidechainCompressFilter()
    {
        var builder = new FFmpegCommandBuilder()
            .AddInput("voice.mp3")
            .AddInput("music.mp3")
            .AddAudioDucking(0, 1, -20, 4, 20, 250)
            .SetOutput("output.mp3");

        var command = builder.Build();

        Assert.Contains("sidechaincompress", command);
        Assert.Contains("threshold=-20dB", command);
        Assert.Contains("ratio=4", command);
    }

    [Fact]
    public void AddWatermark_IncludesOverlayWithOpacity()
    {
        var builder = new FFmpegCommandBuilder()
            .AddInput("input.mp4")
            .AddWatermark("/path/to/watermark.png", "bottom-right", 0.7, 10)
            .SetOutput("output.mp4");

        var command = builder.Build();

        Assert.Contains("movie=/path/to/watermark.png", command);
        Assert.Contains("colorchannelmixer=aa=0.7", command);
        Assert.Contains("overlay=W-w-10:H-h-10", command);
    }

    [Fact]
    public void SetTwoPassEncoding_Pass1_IncludesCorrectOptions()
    {
        var builder = new FFmpegCommandBuilder()
            .AddInput("input.mp4")
            .SetOutput("output.mp4")
            .SetVideoCodec("libx264")
            .SetTwoPassEncoding("/tmp/passlog", 1);

        var command = builder.Build();

        Assert.Contains("-pass 1", command);
        Assert.Contains("-passlogfile \"/tmp/passlog\"", command);
        Assert.Contains("-an", command);
        Assert.Contains("-f null", command);
    }

    [Fact]
    public void SetTwoPassEncoding_Pass2_IncludesCorrectOptions()
    {
        var builder = new FFmpegCommandBuilder()
            .AddInput("input.mp4")
            .SetOutput("output.mp4")
            .SetVideoCodec("libx264")
            .SetTwoPassEncoding("/tmp/passlog", 2);

        var command = builder.Build();

        Assert.Contains("-pass 2", command);
        Assert.Contains("-passlogfile \"/tmp/passlog\"", command);
        Assert.DoesNotContain("-an", command);
    }

    [Fact]
    public void SetTwoPassEncoding_InvalidPass_ThrowsException()
    {
        var builder = new FFmpegCommandBuilder()
            .AddInput("input.mp4")
            .SetOutput("output.mp4");

        Assert.Throws<ArgumentOutOfRangeException>(() => 
            builder.SetTwoPassEncoding("/tmp/passlog", 3));
    }

    [Fact]
    public void AddChapterMarkers_IncludesMetadata()
    {
        var chapters = new List<(TimeSpan, string)>
        {
            (TimeSpan.FromSeconds(0), "Introduction"),
            (TimeSpan.FromSeconds(60), "Main Content"),
            (TimeSpan.FromSeconds(180), "Conclusion")
        };

        var builder = new FFmpegCommandBuilder()
            .AddInput("input.mp4")
            .AddChapterMarkers(chapters)
            .SetOutput("output.mp4");

        var command = builder.Build();

        Assert.Contains("-metadata chapter0_title=\"Introduction\"", command);
        Assert.Contains("-metadata chapter1_title=\"Main Content\"", command);
        Assert.Contains("-metadata chapter2_title=\"Conclusion\"", command);
    }

    [Fact]
    public void SetMaxBitrate_IncludesMaxrateOption()
    {
        var builder = new FFmpegCommandBuilder()
            .AddInput("input.mp4")
            .SetOutput("output.mp4")
            .SetMaxBitrate(8000);

        var command = builder.Build();

        Assert.Contains("-maxrate 8000k", command);
    }

    [Fact]
    public void SetBufferSize_IncludesBufsizeOption()
    {
        var builder = new FFmpegCommandBuilder()
            .AddInput("input.mp4")
            .SetOutput("output.mp4")
            .SetBufferSize(16000);

        var command = builder.Build();

        Assert.Contains("-bufsize 16000k", command);
    }

    [Fact]
    public void MultipleFilters_AreCombinedCorrectly()
    {
        var builder = new FFmpegCommandBuilder()
            .AddInput("input.mp4")
            .AddFadeIn(1.0)
            .AddFadeOut(58.0, 2.0)
            .AddTextOverlay("Test", null, 48)
            .SetOutput("output.mp4");

        var command = builder.Build();

        Assert.Contains("-filter_complex", command);
        Assert.Contains("fade=t=in:st=0:d=1", command);
        Assert.Contains("fade=t=out:st=58:d=2", command);
        Assert.Contains("drawtext=", command);
    }
}
