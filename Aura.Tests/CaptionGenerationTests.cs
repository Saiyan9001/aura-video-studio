using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Aura.Core.Audio;
using Aura.Core.Models;
using Aura.Core.Timeline;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Aura.Tests;

public class CaptionGenerationTests
{
    private readonly AudioProcessor _audioProcessor;
    private readonly TimelineBuilder _timelineBuilder;

    public CaptionGenerationTests()
    {
        _audioProcessor = new AudioProcessor(NullLogger<AudioProcessor>.Instance);
        _timelineBuilder = new TimelineBuilder();
    }

    [Fact]
    public void GenerateSrtSubtitles_Should_ProduceValidFormat()
    {
        // Arrange
        var lines = new List<ScriptLine>
        {
            new ScriptLine(0, "First line of dialogue", TimeSpan.FromSeconds(0), TimeSpan.FromSeconds(3)),
            new ScriptLine(1, "Second line here", TimeSpan.FromSeconds(3), TimeSpan.FromSeconds(2.5)),
            new ScriptLine(2, "Third and final", TimeSpan.FromSeconds(5.5), TimeSpan.FromSeconds(2))
        };

        // Act
        string srt = _audioProcessor.GenerateSrtSubtitles(lines);

        // Assert
        Assert.NotNull(srt);
        
        // Verify structure
        Assert.Contains("1", srt);
        Assert.Contains("2", srt);
        Assert.Contains("3", srt);
        
        // Verify timing format (SRT uses commas)
        Assert.Contains("00:00:00,000 --> 00:00:03,000", srt);
        Assert.Contains("00:00:03,000 --> 00:00:05,500", srt);
        Assert.Contains("00:00:05,500 --> 00:00:07,500", srt);
        
        // Verify text content
        Assert.Contains("First line of dialogue", srt);
        Assert.Contains("Second line here", srt);
        Assert.Contains("Third and final", srt);
    }

    [Fact]
    public void GenerateVttSubtitles_Should_ProduceValidFormat()
    {
        // Arrange
        var lines = new List<ScriptLine>
        {
            new ScriptLine(0, "First subtitle", TimeSpan.FromSeconds(0), TimeSpan.FromSeconds(2)),
            new ScriptLine(1, "Second subtitle", TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(3))
        };

        // Act
        string vtt = _audioProcessor.GenerateVttSubtitles(lines);

        // Assert
        Assert.NotNull(vtt);
        
        // Verify header
        Assert.StartsWith("WEBVTT", vtt);
        
        // Verify timing format (VTT uses dots)
        Assert.Contains("00:00:00.000 --> 00:00:02.000", vtt);
        Assert.Contains("00:00:02.000 --> 00:00:05.000", vtt);
        
        // Verify text content
        Assert.Contains("First subtitle", vtt);
        Assert.Contains("Second subtitle", vtt);
    }

    [Fact]
    public void GenerateSrtSubtitles_Should_HandleLongVideos()
    {
        // Arrange - Test video over 1 hour
        var lines = new List<ScriptLine>
        {
            new ScriptLine(0, "Start", TimeSpan.FromHours(0), TimeSpan.FromSeconds(5)),
            new ScriptLine(1, "Middle", TimeSpan.FromHours(1.5), TimeSpan.FromSeconds(5)),
            new ScriptLine(2, "End", TimeSpan.FromHours(2), TimeSpan.FromSeconds(5))
        };

        // Act
        string srt = _audioProcessor.GenerateSrtSubtitles(lines);

        // Assert
        Assert.Contains("00:00:00,000 --> 00:00:05,000", srt);
        Assert.Contains("01:30:00,000 --> 01:30:05,000", srt);
        Assert.Contains("02:00:00,000 --> 02:00:05,000", srt);
    }

    [Fact]
    public void GenerateVttSubtitles_Should_HandleLongVideos()
    {
        // Arrange - Test video over 1 hour
        var lines = new List<ScriptLine>
        {
            new ScriptLine(0, "Start", TimeSpan.FromHours(0), TimeSpan.FromSeconds(5)),
            new ScriptLine(1, "End", TimeSpan.FromHours(2.5), TimeSpan.FromSeconds(5))
        };

        // Act
        string vtt = _audioProcessor.GenerateVttSubtitles(lines);

        // Assert
        Assert.Contains("00:00:00.000 --> 00:00:05.000", vtt);
        Assert.Contains("02:30:00.000 --> 02:30:05.000", vtt);
    }

    [Fact]
    public void TimelineBuilder_GenerateSubtitles_Should_CreateSrtFromScenes()
    {
        // Arrange
        var scenes = new List<Scene>
        {
            new Scene(0, "Introduction", "Welcome to the video", TimeSpan.FromSeconds(0), TimeSpan.FromSeconds(5)),
            new Scene(1, "Main Content", "Here is the main content", TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(10))
        };

        // Act
        string srt = _timelineBuilder.GenerateSubtitles(scenes, "SRT");

        // Assert
        Assert.NotNull(srt);
        Assert.Contains("Welcome to the video", srt);
        Assert.Contains("Here is the main content", srt);
        Assert.Contains("00:00:00,000 --> 00:00:05,000", srt);
        Assert.Contains("00:00:05,000 --> 00:00:15,000", srt);
    }

    [Fact]
    public void TimelineBuilder_GenerateSubtitles_Should_CreateVttFromScenes()
    {
        // Arrange
        var scenes = new List<Scene>
        {
            new Scene(0, "Scene 1", "First caption", TimeSpan.FromSeconds(0), TimeSpan.FromSeconds(3))
        };

        // Act
        string vtt = _timelineBuilder.GenerateSubtitles(scenes, "VTT");

        // Assert
        Assert.NotNull(vtt);
        Assert.StartsWith("WEBVTT", vtt);
        Assert.Contains("First caption", vtt);
        Assert.Contains("00:00:00.000 --> 00:00:03.000", vtt);
    }

    [Fact]
    public void BuildSubtitleFilter_Should_CreateProperFfmpegCommand()
    {
        // Arrange
        string subtitlePath = "/path/to/subtitles.srt";

        // Act
        string filter = _audioProcessor.BuildSubtitleFilter(
            subtitlePath,
            fontName: "Arial",
            fontSize: 24,
            primaryColor: "FFFFFF",
            outlineColor: "000000",
            outlineWidth: 2
        );

        // Assert
        Assert.Contains("subtitles=", filter);
        Assert.Contains("subtitles.srt", filter);
        Assert.Contains("FontName=Arial", filter);
        Assert.Contains("FontSize=24", filter);
        Assert.Contains("PrimaryColour=&HFFFFFF&", filter);
        Assert.Contains("OutlineColour=&H000000&", filter);
    }

    [Fact]
    public void BuildSubtitleFilter_Should_EscapeSpecialCharacters()
    {
        // Arrange - Path with special characters
        string subtitlePath = "C:\\Users\\Test\\subtitles.srt";

        // Act
        string filter = _audioProcessor.BuildSubtitleFilter(subtitlePath);

        // Assert
        // FFmpeg requires backslashes to be escaped
        Assert.Contains("\\\\", filter);
    }

    [Fact]
    public void GenerateSrtSubtitles_Should_HandleMilliseconds()
    {
        // Arrange
        var lines = new List<ScriptLine>
        {
            new ScriptLine(0, "Test", TimeSpan.FromMilliseconds(1234), TimeSpan.FromMilliseconds(567))
        };

        // Act
        string srt = _audioProcessor.GenerateSrtSubtitles(lines);

        // Assert
        Assert.Contains("00:00:01,234 --> 00:00:01,801", srt);
    }

    [Fact]
    public void GenerateVttSubtitles_Should_HandleMilliseconds()
    {
        // Arrange
        var lines = new List<ScriptLine>
        {
            new ScriptLine(0, "Test", TimeSpan.FromMilliseconds(1234), TimeSpan.FromMilliseconds(567))
        };

        // Act
        string vtt = _audioProcessor.GenerateVttSubtitles(lines);

        // Assert
        Assert.Contains("00:00:01.234 --> 00:00:01.801", vtt);
    }

    [Fact]
    public void GenerateSrtSubtitles_Should_HandleMultipleLines()
    {
        // Arrange
        var lines = new List<ScriptLine>
        {
            new ScriptLine(0, "Line 1", TimeSpan.FromSeconds(0), TimeSpan.FromSeconds(2)),
            new ScriptLine(1, "Line 2", TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(2)),
            new ScriptLine(2, "Line 3", TimeSpan.FromSeconds(4), TimeSpan.FromSeconds(2)),
            new ScriptLine(3, "Line 4", TimeSpan.FromSeconds(6), TimeSpan.FromSeconds(2)),
            new ScriptLine(4, "Line 5", TimeSpan.FromSeconds(8), TimeSpan.FromSeconds(2))
        };

        // Act
        string srt = _audioProcessor.GenerateSrtSubtitles(lines);

        // Assert
        // Verify all 5 subtitle indices are present
        Assert.Contains("1\n", srt);
        Assert.Contains("2\n", srt);
        Assert.Contains("3\n", srt);
        Assert.Contains("4\n", srt);
        Assert.Contains("5\n", srt);
        
        // Verify all lines are present
        for (int i = 1; i <= 5; i++)
        {
            Assert.Contains($"Line {i}", srt);
        }
    }

    [Fact]
    public async Task CaptionsShouldBeWrittenToFile()
    {
        // Arrange
        var lines = new List<ScriptLine>
        {
            new ScriptLine(0, "Caption text", TimeSpan.FromSeconds(0), TimeSpan.FromSeconds(3))
        };
        
        string tempFile = Path.Combine(Path.GetTempPath(), $"test_captions_{Guid.NewGuid()}.srt");

        try
        {
            // Act
            string srt = _audioProcessor.GenerateSrtSubtitles(lines);
            await File.WriteAllTextAsync(tempFile, srt);

            // Assert
            Assert.True(File.Exists(tempFile));
            string content = await File.ReadAllTextAsync(tempFile);
            Assert.Contains("Caption text", content);
            Assert.Contains("00:00:00,000 --> 00:00:03,000", content);
        }
        finally
        {
            // Cleanup
            if (File.Exists(tempFile))
            {
                File.Delete(tempFile);
            }
        }
    }

    [Fact]
    public void TimelineBuilder_Should_LinkCaptionsIntoTimeline()
    {
        // Arrange
        var scenes = new List<Scene>
        {
            new Scene(0, "Scene 1", "Caption 1", TimeSpan.FromSeconds(0), TimeSpan.FromSeconds(5))
        };
        string narrationPath = "/path/to/narration.wav";
        string subtitlesPath = "/path/to/subtitles.srt";

        // Act
        var timeline = _timelineBuilder.BuildTimeline(scenes, narrationPath, subtitlesPath: subtitlesPath);

        // Assert
        Assert.NotNull(timeline);
        Assert.Equal(subtitlesPath, timeline.SubtitlesPath);
    }
}
