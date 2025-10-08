using System;
using System.Linq;
using Aura.Core.Models;
using Aura.Core.Rendering;
using Xunit;

namespace Aura.Tests;

public class FFmpegPlanBuilderTests
{
    [Fact]
    public void BuildRenderCommand_Should_IncludeBasicParameters()
    {
        // Arrange
        var builder = new FFmpegPlanBuilder();
        var spec = new RenderSpec(
            Res: new Resolution(1920, 1080),
            Container: "mp4",
            VideoBitrateK: 12000,
            AudioBitrateK: 256
        );
        var quality = new FFmpegPlanBuilder.QualitySettings
        {
            QualityLevel = 75,
            Fps = 30
        };

        // Act
        string command = builder.BuildRenderCommand(
            spec,
            quality,
            FFmpegPlanBuilder.EncoderType.X264,
            "input.mp4",
            "audio.wav",
            "output.mp4"
        );

        // Assert
        Assert.Contains("-i \"input.mp4\"", command);
        Assert.Contains("-i \"audio.wav\"", command);
        Assert.Contains("-c:v libx264", command);
        Assert.Contains("-c:a aac", command);
        Assert.Contains("-b:v 12000k", command);
        Assert.Contains("-b:a 256k", command);
        Assert.Contains("-r 30", command);
        Assert.Contains("\"output.mp4\"", command);
    }

    [Fact]
    public void BuildRenderCommand_Should_UseNvencWhenSpecified()
    {
        // Arrange
        var builder = new FFmpegPlanBuilder();
        var spec = RenderPresets.YouTube1080p;
        var quality = new FFmpegPlanBuilder.QualitySettings
        {
            QualityLevel = 80,
            Fps = 30
        };

        // Act
        string command = builder.BuildRenderCommand(
            spec,
            quality,
            FFmpegPlanBuilder.EncoderType.NVENC_H264,
            "input.mp4",
            "audio.wav",
            "output.mp4"
        );

        // Assert
        Assert.Contains("-c:v h264_nvenc", command);
        Assert.Contains("-rc cq", command);
        Assert.Contains("-preset p", command);
        Assert.Contains("-spatial-aq", command);
        Assert.Contains("-temporal-aq", command);
    }

    [Theory]
    [InlineData(100, "-crf 14")]  // Highest quality -> lowest CRF
    [InlineData(50, "-crf 21")]   // Medium quality
    [InlineData(0, "-crf 28")]    // Lowest quality -> highest CRF
    public void BuildRenderCommand_Should_MapQualityToCrf(int qualityLevel, string expectedCrf)
    {
        // Arrange
        var builder = new FFmpegPlanBuilder();
        var spec = RenderPresets.YouTube1080p;
        var quality = new FFmpegPlanBuilder.QualitySettings
        {
            QualityLevel = qualityLevel,
            Fps = 30
        };

        // Act
        string command = builder.BuildRenderCommand(
            spec,
            quality,
            FFmpegPlanBuilder.EncoderType.X264,
            "input.mp4",
            "audio.wav",
            "output.mp4"
        );

        // Assert
        Assert.Contains(expectedCrf, command);
    }

    [Fact]
    public void BuildRenderCommand_Should_SetGopSizeToTwiceFramerate()
    {
        // Arrange
        var builder = new FFmpegPlanBuilder();
        var spec = RenderPresets.YouTube1080p;
        var quality = new FFmpegPlanBuilder.QualitySettings
        {
            QualityLevel = 75,
            Fps = 30
        };

        // Act
        string command = builder.BuildRenderCommand(
            spec,
            quality,
            FFmpegPlanBuilder.EncoderType.X264,
            "input.mp4",
            "audio.wav",
            "output.mp4"
        );

        // Assert - GOP should be 60 (2x fps)
        Assert.Contains("-g 60", command);
    }

    [Fact]
    public void BuildRenderCommand_Should_EnableSceneCutKeyframes()
    {
        // Arrange
        var builder = new FFmpegPlanBuilder();
        var spec = RenderPresets.YouTube1080p;
        var quality = new FFmpegPlanBuilder.QualitySettings
        {
            QualityLevel = 75,
            Fps = 30,
            EnableSceneCut = true
        };

        // Act
        string command = builder.BuildRenderCommand(
            spec,
            quality,
            FFmpegPlanBuilder.EncoderType.X264,
            "input.mp4",
            "audio.wav",
            "output.mp4"
        );

        // Assert
        Assert.Contains("-sc_threshold", command);
    }

    [Fact]
    public void BuildFilterGraph_Should_IncludeScale()
    {
        // Arrange
        var builder = new FFmpegPlanBuilder();
        var resolution = new Resolution(1920, 1080);

        // Act
        string filterGraph = builder.BuildFilterGraph(resolution);

        // Assert
        Assert.Contains("scale=1920:1080", filterGraph);
        Assert.Contains("lanczos", filterGraph);
    }

    [Fact]
    public void BuildFilterGraph_Should_IncludeSubtitlesWhenRequested()
    {
        // Arrange
        var builder = new FFmpegPlanBuilder();
        var resolution = new Resolution(1920, 1080);
        string subtitlePath = "subtitles.srt";

        // Act
        string filterGraph = builder.BuildFilterGraph(resolution, addSubtitles: true, subtitlePath);

        // Assert
        Assert.Contains("subtitles=", filterGraph);
        Assert.Contains("subtitles.srt", filterGraph);
    }

    [Fact]
    public void DetectAvailableEncoders_Should_FindNvenc()
    {
        // Arrange
        string ffmpegOutput = @"
        Encoders:
         V....D h264_nvenc           NVIDIA NVENC H.264 encoder
         V....D hevc_nvenc           NVIDIA NVENC hevc encoder
         V..... libx264              libx264 H.264 / AVC / MPEG-4 AVC / MPEG-4 part 10
        ";

        // Act
        var encoders = FFmpegPlanBuilder.DetectAvailableEncoders(ffmpegOutput);

        // Assert
        Assert.Contains(FFmpegPlanBuilder.EncoderType.NVENC_H264, encoders);
        Assert.Contains(FFmpegPlanBuilder.EncoderType.NVENC_HEVC, encoders);
        Assert.Contains(FFmpegPlanBuilder.EncoderType.X264, encoders);
    }

    [Fact]
    public void DetectAvailableEncoders_Should_FindAmf()
    {
        // Arrange
        string ffmpegOutput = @"
        Encoders:
         V....D h264_amf             AMD AMF H.264 Encoder
         V....D hevc_amf             AMD AMF HEVC encoder
        ";

        // Act
        var encoders = FFmpegPlanBuilder.DetectAvailableEncoders(ffmpegOutput);

        // Assert
        Assert.Contains(FFmpegPlanBuilder.EncoderType.AMF_H264, encoders);
        Assert.Contains(FFmpegPlanBuilder.EncoderType.AMF_HEVC, encoders);
    }

    [Fact]
    public void DetectAvailableEncoders_Should_AlwaysIncludeX264Fallback()
    {
        // Arrange
        string ffmpegOutput = "No hardware encoders available";

        // Act
        var encoders = FFmpegPlanBuilder.DetectAvailableEncoders(ffmpegOutput);

        // Assert
        Assert.Contains(FFmpegPlanBuilder.EncoderType.X264, encoders);
        Assert.Single(encoders); // Only x264 should be available
    }

    [Fact]
    public void BuildRenderCommand_Should_UseCorrectColorSpace()
    {
        // Arrange
        var builder = new FFmpegPlanBuilder();
        var spec = RenderPresets.YouTube1080p;
        var quality = new FFmpegPlanBuilder.QualitySettings { QualityLevel = 75, Fps = 30 };

        // Act
        string command = builder.BuildRenderCommand(
            spec,
            quality,
            FFmpegPlanBuilder.EncoderType.X264,
            "input.mp4",
            "audio.wav",
            "output.mp4"
        );

        // Assert
        Assert.Contains("-colorspace bt709", command);
        Assert.Contains("-color_trc bt709", command);
        Assert.Contains("-color_primaries bt709", command);
    }

    [Fact]
    public void BuildRenderCommand_Should_UseAacWithCorrectSettings()
    {
        // Arrange
        var builder = new FFmpegPlanBuilder();
        var spec = RenderPresets.YouTube1080p;
        var quality = new FFmpegPlanBuilder.QualitySettings { QualityLevel = 75, Fps = 30 };

        // Act
        string command = builder.BuildRenderCommand(
            spec,
            quality,
            FFmpegPlanBuilder.EncoderType.X264,
            "input.mp4",
            "audio.wav",
            "output.mp4"
        );

        // Assert
        Assert.Contains("-c:a aac", command);
        Assert.Contains("-b:a 256k", command);
        Assert.Contains("-ar 48000", command); // 48kHz sample rate
        Assert.Contains("-ac 2", command);     // Stereo
    }

    // ========== Guardrail Tests ==========

    [Fact]
    public void ValidateConfiguration_Should_RejectUnavailableEncoder()
    {
        // Arrange
        var builder = new FFmpegPlanBuilder();
        var spec = RenderPresets.YouTube1080p;
        var quality = new FFmpegPlanBuilder.QualitySettings { QualityLevel = 75, Fps = 30 };
        var availableEncoders = new List<FFmpegPlanBuilder.EncoderType> { FFmpegPlanBuilder.EncoderType.X264 };

        // Act
        var result = builder.ValidateConfiguration(spec, quality, FFmpegPlanBuilder.EncoderType.NVENC_H264, availableEncoders);

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains("not available", result.ErrorMessage);
        Assert.Equal(FFmpegPlanBuilder.EncoderType.X264, result.SuggestedEncoder);
    }

    [Fact]
    public void ValidateConfiguration_Should_RejectInvalidFrameRate()
    {
        // Arrange
        var builder = new FFmpegPlanBuilder();
        var spec = RenderPresets.YouTube1080p;
        var quality = new FFmpegPlanBuilder.QualitySettings { QualityLevel = 75, Fps = 200 }; // Invalid

        // Act
        var result = builder.ValidateConfiguration(spec, quality, FFmpegPlanBuilder.EncoderType.X264);

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains("Frame rate", result.ErrorMessage);
        Assert.NotNull(result.SuggestedQuality);
        Assert.InRange(result.SuggestedQuality!.Fps, 1, 120);
    }

    [Fact]
    public void ValidateConfiguration_Should_RejectInvalidQualityLevel()
    {
        // Arrange
        var builder = new FFmpegPlanBuilder();
        var spec = RenderPresets.YouTube1080p;
        var quality = new FFmpegPlanBuilder.QualitySettings { QualityLevel = 150, Fps = 30 }; // Invalid

        // Act
        var result = builder.ValidateConfiguration(spec, quality, FFmpegPlanBuilder.EncoderType.X264);

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains("Quality level", result.ErrorMessage);
        Assert.NotNull(result.SuggestedQuality);
        Assert.InRange(result.SuggestedQuality!.QualityLevel, 0, 100);
    }

    [Fact]
    public void ValidateConfiguration_Should_RejectTooSmallResolution()
    {
        // Arrange
        var builder = new FFmpegPlanBuilder();
        var spec = new RenderSpec(
            Res: new Resolution(160, 120), // Too small
            Container: "mp4",
            VideoBitrateK: 1000,
            AudioBitrateK: 128
        );
        var quality = new FFmpegPlanBuilder.QualitySettings { QualityLevel = 75, Fps = 30 };

        // Act
        var result = builder.ValidateConfiguration(spec, quality, FFmpegPlanBuilder.EncoderType.X264);

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains("too small", result.ErrorMessage);
    }

    [Fact]
    public void ValidateConfiguration_Should_WarnAboutHighBitrateOnLowResolution()
    {
        // Arrange
        var builder = new FFmpegPlanBuilder();
        var spec = new RenderSpec(
            Res: new Resolution(1280, 720),
            Container: "mp4",
            VideoBitrateK: 25000, // Very high for 720p
            AudioBitrateK: 256
        );
        var quality = new FFmpegPlanBuilder.QualitySettings { QualityLevel = 75, Fps = 30 };

        // Act
        var result = builder.ValidateConfiguration(spec, quality, FFmpegPlanBuilder.EncoderType.X264);

        // Assert
        Assert.True(result.IsValid); // Just a warning, not an error
        Assert.Contains("bitrate", result.ErrorMessage);
    }

    [Fact]
    public void ValidateConfiguration_Should_RejectCodecMismatch()
    {
        // Arrange
        var builder = new FFmpegPlanBuilder();
        var spec = RenderPresets.YouTube1080p;
        var quality = new FFmpegPlanBuilder.QualitySettings 
        { 
            QualityLevel = 75, 
            Fps = 30, 
            Codec = "AV1" 
        };

        // Act
        var result = builder.ValidateConfiguration(spec, quality, FFmpegPlanBuilder.EncoderType.X264);

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains("AV1", result.ErrorMessage);
        Assert.Equal(FFmpegPlanBuilder.EncoderType.NVENC_AV1, result.SuggestedEncoder);
    }

    [Fact]
    public void GetSafeDefaultEncoder_Should_PreferNvencH264()
    {
        // Arrange
        var availableEncoders = new List<FFmpegPlanBuilder.EncoderType>
        {
            FFmpegPlanBuilder.EncoderType.X264,
            FFmpegPlanBuilder.EncoderType.NVENC_H264,
            FFmpegPlanBuilder.EncoderType.AMF_H264
        };

        // Act
        var encoder = FFmpegPlanBuilder.GetSafeDefaultEncoder(availableEncoders);

        // Assert
        Assert.Equal(FFmpegPlanBuilder.EncoderType.NVENC_H264, encoder);
    }

    [Fact]
    public void GetSafeDefaultEncoder_Should_FallbackToX264()
    {
        // Arrange
        var availableEncoders = new List<FFmpegPlanBuilder.EncoderType>
        {
            FFmpegPlanBuilder.EncoderType.X264
        };

        // Act
        var encoder = FFmpegPlanBuilder.GetSafeDefaultEncoder(availableEncoders);

        // Assert
        Assert.Equal(FFmpegPlanBuilder.EncoderType.X264, encoder);
    }

    // ========== Golden Args Tests ==========

    [Fact]
    public void BuildRenderCommand_X264_HighQuality_Should_ProduceExpectedArgs()
    {
        // Arrange - Golden test for x264 high quality preset
        var builder = new FFmpegPlanBuilder();
        var spec = RenderPresets.YouTube1080p;
        var quality = new FFmpegPlanBuilder.QualitySettings 
        { 
            QualityLevel = 90, 
            Fps = 30,
            EnableSceneCut = true
        };

        // Act
        string command = builder.BuildRenderCommand(
            spec,
            quality,
            FFmpegPlanBuilder.EncoderType.X264,
            "input.mp4",
            "audio.wav",
            "output.mp4"
        );

        // Assert - Golden parameters for x264 high quality
        Assert.Contains("-c:v libx264", command);
        Assert.Contains("-crf 16", command); // High quality = low CRF (90 * 0.14 = 12.6, 28 - 12 = 16)
        Assert.Contains("-preset slow", command);
        Assert.Contains("-tune film", command);
        Assert.Contains("-profile:v high", command);
        Assert.Contains("-pix_fmt yuv420p", command);
        Assert.Contains("-g 60", command); // GOP = 2 * 30fps
        Assert.Contains("-sc_threshold 40", command);
        Assert.Contains("-r 30", command);
    }

    [Fact]
    public void BuildRenderCommand_NVENC_H264_Should_ProduceExpectedArgs()
    {
        // Arrange - Golden test for NVENC H264
        var builder = new FFmpegPlanBuilder();
        var spec = RenderPresets.YouTube1080p;
        var quality = new FFmpegPlanBuilder.QualitySettings 
        { 
            QualityLevel = 80, 
            Fps = 60
        };

        // Act
        string command = builder.BuildRenderCommand(
            spec,
            quality,
            FFmpegPlanBuilder.EncoderType.NVENC_H264,
            "input.mp4",
            "audio.wav",
            "output.mp4"
        );

        // Assert - Golden parameters for NVENC H264
        Assert.Contains("-c:v h264_nvenc", command);
        Assert.Contains("-rc cq", command);
        Assert.Matches(@"-cq (18|19|20|21)", command); // Quality-dependent
        Assert.Contains("-preset p7", command); // QualityLevel >= 75 -> p7
        Assert.Contains("-rc-lookahead 16", command);
        Assert.Contains("-spatial-aq 1", command);
        Assert.Contains("-temporal-aq 1", command);
        Assert.Contains("-bf 3", command);
        Assert.Contains("-g 120", command); // GOP = 2 * 60fps
    }

    [Fact]
    public void BuildRenderCommand_NVENC_HEVC_Should_ProduceExpectedArgs()
    {
        // Arrange - Golden test for NVENC HEVC
        var builder = new FFmpegPlanBuilder();
        var spec = RenderPresets.YouTube1080p;
        var quality = new FFmpegPlanBuilder.QualitySettings 
        { 
            QualityLevel = 75, 
            Fps = 30
        };

        // Act
        string command = builder.BuildRenderCommand(
            spec,
            quality,
            FFmpegPlanBuilder.EncoderType.NVENC_HEVC,
            "input.mp4",
            "audio.wav",
            "output.mp4"
        );

        // Assert - Golden parameters for NVENC HEVC
        Assert.Contains("-c:v hevc_nvenc", command);
        Assert.Contains("-rc cq", command);
        Assert.Contains("-preset p7", command);
        Assert.Contains("-rc-lookahead 16", command);
    }

    [Fact]
    public void BuildRenderCommand_NVENC_AV1_Should_ProduceExpectedArgs()
    {
        // Arrange - Golden test for NVENC AV1
        var builder = new FFmpegPlanBuilder();
        var spec = RenderPresets.YouTube1080p;
        var quality = new FFmpegPlanBuilder.QualitySettings 
        { 
            QualityLevel = 80, 
            Fps = 30
        };

        // Act
        string command = builder.BuildRenderCommand(
            spec,
            quality,
            FFmpegPlanBuilder.EncoderType.NVENC_AV1,
            "input.mp4",
            "audio.wav",
            "output.mp4"
        );

        // Assert - Golden parameters for NVENC AV1
        Assert.Contains("-c:v av1_nvenc", command);
        Assert.Contains("-rc cq", command);
        Assert.Matches(@"-cq (22|23|24|25|26)", command); // AV1 CQ range: 38-22
        Assert.Contains("-preset p7", command); // QualityLevel >= 75 -> p7
    }

    // ========== Color Space Tests ==========

    [Theory]
    [InlineData(1920, 1080)] // Full HD
    [InlineData(1280, 720)]  // HD
    [InlineData(3840, 2160)] // 4K
    public void BuildRenderCommand_Should_UseBT709ColorSpace(int width, int height)
    {
        // Arrange
        var builder = new FFmpegPlanBuilder();
        var spec = new RenderSpec(
            Res: new Resolution(width, height),
            Container: "mp4",
            VideoBitrateK: 12000,
            AudioBitrateK: 256
        );
        var quality = new FFmpegPlanBuilder.QualitySettings { QualityLevel = 75, Fps = 30 };

        // Act
        string command = builder.BuildRenderCommand(
            spec,
            quality,
            FFmpegPlanBuilder.EncoderType.X264,
            "input.mp4",
            "audio.wav",
            "output.mp4"
        );

        // Assert - All HD/4K content should use BT.709
        Assert.Contains("-colorspace bt709", command);
        Assert.Contains("-color_trc bt709", command);
        Assert.Contains("-color_primaries bt709", command);
        Assert.Contains("-pix_fmt yuv420p", command);
    }

    // ========== Audio DSP Chain Tests ==========

    [Fact]
    public void BuildAudioProcessingCommand_Should_IncludeDspChain()
    {
        // Arrange
        var builder = new FFmpegPlanBuilder();

        // Act
        string command = builder.BuildAudioProcessingCommand(
            "input.wav",
            "output.wav",
            targetLufs: -14.0,
            peakCeiling: -1.0
        );

        // Assert - DSP chain: HPF -> De-esser -> Compressor -> Limiter -> LUFS
        Assert.Contains("-i \"input.wav\"", command);
        Assert.Contains("highpass=f=80", command); // HPF
        Assert.Contains("treble=g=-3:f=7000:w=2000", command); // De-esser
        Assert.Contains("acompressor=threshold=-18dB:ratio=3:attack=20:release=250:makeup=6dB", command); // Compressor
        Assert.Contains("alimiter=limit=-1dB:attack=5:release=50", command); // Limiter
        Assert.Contains("loudnorm=I=-14:TP=-1:LRA=11", command); // LUFS normalization
    }

    [Fact]
    public void BuildAudioProcessingCommand_Should_Output48kHz24Bit()
    {
        // Arrange
        var builder = new FFmpegPlanBuilder();

        // Act
        string command = builder.BuildAudioProcessingCommand(
            "input.wav",
            "output.wav"
        );

        // Assert - 48kHz/24-bit WAV output
        Assert.Contains("-ar 48000", command); // 48kHz sample rate
        Assert.Contains("-sample_fmt s32", command); // 24-bit equivalent
        Assert.Contains("-c:a pcm_s24le", command); // PCM 24-bit little-endian
        Assert.Contains("\"output.wav\"", command);
    }

    [Theory]
    [InlineData(-16.0, -1.0)]  // Voice-only standard
    [InlineData(-14.0, -1.0)]  // YouTube standard
    [InlineData(-12.0, -1.0)]  // Music-forward standard
    public void BuildAudioProcessingCommand_Should_SupportDifferentLufsTargets(double targetLufs, double peakCeiling)
    {
        // Arrange
        var builder = new FFmpegPlanBuilder();

        // Act
        string command = builder.BuildAudioProcessingCommand(
            "input.wav",
            "output.wav",
            targetLufs,
            peakCeiling
        );

        // Assert
        Assert.Contains($"loudnorm=I={targetLufs}", command);
        Assert.Contains($"TP={peakCeiling}", command);
    }
}
