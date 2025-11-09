using System;
using Aura.Core.Models;
using Aura.Core.Models.Export;
using Aura.Core.Services.FFmpeg;
using Xunit;

namespace Aura.Tests.Services.FFmpeg;

/// <summary>
/// Tests for FFmpegCommandBuilder to ensure valid command generation
/// </summary>
public class FFmpegCommandBuilderTests
{
    [Fact]
    public void Build_WithMinimalOptions_GeneratesValidCommand()
    {
        // Arrange
        var builder = new FFmpegCommandBuilder()
            .AddInput("input.mp4")
            .SetOutput("output.mp4");
        
        // Act
        var command = builder.Build();
        
        // Assert
        Assert.Contains("-i \"input.mp4\"", command);
        Assert.Contains("\"output.mp4\"", command);
    }
    
    [Fact]
    public void Build_WithVideoCodec_IncludesCodecOption()
    {
        // Arrange
        var builder = new FFmpegCommandBuilder()
            .AddInput("input.mp4")
            .SetOutput("output.mp4")
            .SetVideoCodec("libx264");
        
        // Act
        var command = builder.Build();
        
        // Assert
        Assert.Contains("-c:v libx264", command);
    }
    
    [Fact]
    public void Build_WithHardwareAcceleration_IncludesHwaccelOption()
    {
        // Arrange
        var builder = new FFmpegCommandBuilder()
            .AddInput("input.mp4")
            .SetOutput("output.mp4")
            .SetHardwareAcceleration("cuda");
        
        // Act
        var command = builder.Build();
        
        // Assert
        Assert.Contains("-hwaccel cuda", command);
        Assert.StartsWith("-y -hwaccel cuda", command.Trim());
    }
    
    [Fact]
    public void Build_WithResolutionAndFrameRate_IncludesCorrectOptions()
    {
        // Arrange
        var builder = new FFmpegCommandBuilder()
            .AddInput("input.mp4")
            .SetOutput("output.mp4")
            .SetResolution(1920, 1080)
            .SetFrameRate(30);
        
        // Act
        var command = builder.Build();
        
        // Assert
        Assert.Contains("-s 1920x1080", command);
        Assert.Contains("-r 30", command);
    }
    
    [Fact]
    public void Build_WithAudioSettings_IncludesAudioOptions()
    {
        // Arrange
        var builder = new FFmpegCommandBuilder()
            .AddInput("input.mp4")
            .SetOutput("output.mp4")
            .SetAudioCodec("aac")
            .SetAudioBitrate(192)
            .SetAudioSampleRate(48000)
            .SetAudioChannels(2);
        
        // Act
        var command = builder.Build();
        
        // Assert
        Assert.Contains("-c:a aac", command);
        Assert.Contains("-b:a 192k", command);
        Assert.Contains("-ar 48000", command);
        Assert.Contains("-ac 2", command);
    }
    
    [Fact]
    public void Build_WithMultipleInputs_IncludesAllInputs()
    {
        // Arrange
        var builder = new FFmpegCommandBuilder()
            .AddInput("narration.wav")
            .AddInput("music.mp3")
            .SetOutput("output.mp4");
        
        // Act
        var command = builder.Build();
        
        // Assert
        Assert.Contains("-i \"narration.wav\"", command);
        Assert.Contains("-i \"music.mp3\"", command);
    }
    
    [Fact]
    public void Build_WithCRFAndPreset_IncludesQualitySettings()
    {
        // Arrange
        var builder = new FFmpegCommandBuilder()
            .AddInput("input.mp4")
            .SetOutput("output.mp4")
            .SetCRF(23)
            .SetPreset("medium");
        
        // Act
        var command = builder.Build();
        
        // Assert
        Assert.Contains("-crf 23", command);
        Assert.Contains("-preset medium", command);
    }
    
    [Fact]
    public void Build_WithMetadata_IncludesMetadataOptions()
    {
        // Arrange
        var builder = new FFmpegCommandBuilder()
            .AddInput("input.mp4")
            .SetOutput("output.mp4")
            .AddMetadata("title", "Test Video")
            .AddMetadata("encoder", "Aura Video Studio");
        
        // Act
        var command = builder.Build();
        
        // Assert
        Assert.Contains("-metadata title=\"Test Video\"", command);
        Assert.Contains("-metadata encoder=\"Aura Video Studio\"", command);
    }
    
    [Fact]
    public void Build_WithoutOutput_ThrowsInvalidOperationException()
    {
        // Arrange
        var builder = new FFmpegCommandBuilder()
            .AddInput("input.mp4");
        
        // Act & Assert
        Assert.Throws<InvalidOperationException>(() => builder.Build());
    }
    
    [Fact]
    public void Build_WithOverwrite_IncludesOverwriteFlag()
    {
        // Arrange
        var builder = new FFmpegCommandBuilder()
            .AddInput("input.mp4")
            .SetOutput("output.mp4")
            .SetOverwrite(true);
        
        // Act
        var command = builder.Build();
        
        // Assert
        Assert.StartsWith("-y", command.Trim());
    }
    
    [Fact]
    public void Build_WithoutOverwrite_DoesNotIncludeOverwriteFlag()
    {
        // Arrange
        var builder = new FFmpegCommandBuilder()
            .AddInput("input.mp4")
            .SetOutput("output.mp4")
            .SetOverwrite(false);
        
        // Act
        var command = builder.Build();
        
        // Assert
        Assert.DoesNotContain("-y", command);
    }
    
    [Fact]
    public void FromPreset_CreatesBuilderWithPresetSettings()
    {
        // Arrange
        var preset = new ExportPreset(
            Name: "Test Preset",
            Description: "Test",
            Platform: Platform.Generic,
            Container: "mp4",
            VideoCodec: "libx264",
            AudioCodec: "aac",
            Resolution: new Resolution(1920, 1080),
            FrameRate: 30,
            VideoBitrate: 8000,
            AudioBitrate: 192,
            PixelFormat: "yuv420p",
            ColorSpace: "bt709",
            AspectRatio: AspectRatio.SixteenByNine,
            Quality: QualityLevel.High
        );
        
        // Act
        var command = FFmpegCommandBuilder.FromPreset(preset, "input.mp4", "output.mp4").Build();
        
        // Assert
        Assert.Contains("-i \"input.mp4\"", command);
        Assert.Contains("\"output.mp4\"", command);
        Assert.Contains("-c:v libx264", command);
        Assert.Contains("-c:a aac", command);
        Assert.Contains("-b:v 8000k", command);
        Assert.Contains("-b:a 192k", command);
        Assert.Contains("-s 1920x1080", command);
        Assert.Contains("-r 30", command);
        Assert.Contains("-pix_fmt yuv420p", command);
        Assert.Contains("-preset medium", command); // High quality maps to medium
    }
    
    [Fact]
    public void SetCRF_WithInvalidValue_ThrowsArgumentOutOfRangeException()
    {
        // Arrange
        var builder = new FFmpegCommandBuilder();
        
        // Act & Assert
        Assert.Throws<ArgumentOutOfRangeException>(() => builder.SetCRF(52));
        Assert.Throws<ArgumentOutOfRangeException>(() => builder.SetCRF(-1));
    }
    
    [Fact]
    public void Build_WithFilters_IncludesFilterComplex()
    {
        // Arrange
        var builder = new FFmpegCommandBuilder()
            .AddInput("input.mp4")
            .SetOutput("output.mp4")
            .AddFilter("scale=1920:1080")
            .AddFilter("fade=t=in:st=0:d=1");
        
        // Act
        var command = builder.Build();
        
        // Assert
        Assert.Contains("-filter_complex \"scale=1920:1080,fade=t=in:st=0:d=1\"", command);
    }
    
    [Fact]
    public void AddScaleFilter_WithFitMode_GeneratesCorrectFilter()
    {
        // Arrange
        var builder = new FFmpegCommandBuilder()
            .AddInput("input.mp4")
            .SetOutput("output.mp4")
            .AddScaleFilter(1920, 1080, "fit");
        
        // Act
        var command = builder.Build();
        
        // Assert
        Assert.Contains("-filter_complex", command);
        Assert.Contains("scale=1920:1080:force_original_aspect_ratio=decrease", command);
        Assert.Contains("pad=1920:1080:(ow-iw)/2:(oh-ih)/2", command);
    }
    
    [Fact]
    public void Build_CompleteRenderCommand_GeneratesValidCommand()
    {
        // Arrange - Simulate a full video render scenario
        var builder = new FFmpegCommandBuilder()
            .SetOverwrite(true)
            .SetHardwareAcceleration("cuda")
            .AddInput("narration.wav")
            .AddInput("music.mp3")
            .SetVideoCodec("h264_nvenc")
            .SetAudioCodec("aac")
            .SetPreset("fast")
            .SetCRF(23)
            .SetResolution(1920, 1080)
            .SetFrameRate(30)
            .SetPixelFormat("yuv420p")
            .SetVideoBitrate(8000)
            .SetAudioBitrate(192)
            .SetAudioSampleRate(48000)
            .SetAudioChannels(2)
            .AddMetadata("title", "Generated by Aura")
            .AddMetadata("encoder", "Aura Video Studio")
            .SetOutput("final_output.mp4");
        
        // Act
        var command = builder.Build();
        
        // Assert - Verify all components are present
        Assert.Contains("-y", command);
        Assert.Contains("-hwaccel cuda", command);
        Assert.Contains("-i \"narration.wav\"", command);
        Assert.Contains("-i \"music.mp3\"", command);
        Assert.Contains("-c:v h264_nvenc", command);
        Assert.Contains("-c:a aac", command);
        Assert.Contains("-preset fast", command);
        Assert.Contains("-crf 23", command);
        Assert.Contains("-s 1920x1080", command);
        Assert.Contains("-r 30", command);
        Assert.Contains("-pix_fmt yuv420p", command);
        Assert.Contains("-b:v 8000k", command);
        Assert.Contains("-b:a 192k", command);
        Assert.Contains("-ar 48000", command);
        Assert.Contains("-ac 2", command);
        Assert.Contains("-metadata title=\"Generated by Aura\"", command);
        Assert.Contains("-metadata encoder=\"Aura Video Studio\"", command);
        Assert.Contains("\"final_output.mp4\"", command);
        
        // Verify output is at the end
        Assert.EndsWith("\"final_output.mp4\"", command.Trim());
    }
}
