using System;
using System.Collections.Generic;
using System.Text;
using Aura.Core.Models;

namespace Aura.Core.Rendering;

/// <summary>
/// Builds deterministic FFmpeg filtergraphs and command arguments for video rendering.
/// Maps spec requirements to encoder-specific parameters.
/// </summary>
public class FFmpegPlanBuilder
{
    /// <summary>
    /// Encoder types supported by the builder
    /// </summary>
    public enum EncoderType
    {
        X264,      // Software H.264
        NVENC_H264, // NVIDIA H.264
        NVENC_HEVC, // NVIDIA HEVC/H.265
        NVENC_AV1,  // NVIDIA AV1 (RTX 40/50 only)
        AMF_H264,   // AMD H.264
        AMF_HEVC,   // AMD HEVC
        QSV_H264,   // Intel QuickSync H.264
        QSV_HEVC    // Intel QuickSync HEVC
    }

    /// <summary>
    /// Quality vs Speed setting (0 = fastest/lower quality, 100 = slowest/highest quality)
    /// </summary>
    public class QualitySettings
    {
        public int QualityLevel { get; set; } = 75; // 0-100
        public int Fps { get; set; } = 30;
        public bool EnableSceneCut { get; set; } = true;
        public string Codec { get; set; } = "H264";
    }

    /// <summary>
    /// Validation result with error message and suggested fix
    /// </summary>
    public class ValidationResult
    {
        public bool IsValid { get; set; }
        public string? ErrorMessage { get; set; }
        public string? SuggestedFix { get; set; }
        public EncoderType? SuggestedEncoder { get; set; }
        public QualitySettings? SuggestedQuality { get; set; }
    }

    /// <summary>
    /// Builds FFmpeg command arguments based on render spec and quality settings
    /// </summary>
    public string BuildRenderCommand(
        RenderSpec spec,
        QualitySettings quality,
        EncoderType encoder,
        string inputVideo,
        string inputAudio,
        string outputPath)
    {
        var args = new StringBuilder();

        // Input files
        args.Append($"-i \"{inputVideo}\" ");
        args.Append($"-i \"{inputAudio}\" ");

        // Video encoding
        AppendVideoEncoderArgs(args, spec, quality, encoder);

        // Audio encoding
        AppendAudioEncoderArgs(args, spec);

        // Frame rate (CFR - Constant Frame Rate)
        args.Append($"-r {quality.Fps} ");

        // GOP (Group of Pictures) - 2x fps for standard keyframe interval
        int gopSize = quality.Fps * 2;
        args.Append($"-g {gopSize} ");

        // Scene-cut keyframes
        if (quality.EnableSceneCut)
        {
            args.Append("-sc_threshold 40 ");
        }

        // Pixel format
        args.Append("-pix_fmt yuv420p ");

        // Color space (BT.709 for HD)
        args.Append("-colorspace bt709 -color_trc bt709 -color_primaries bt709 ");

        // Overwrite output
        args.Append("-y ");

        // Output file
        args.Append($"\"{outputPath}\"");

        return args.ToString();
    }

    /// <summary>
    /// Builds audio processing command with DSP chain for WAV export
    /// DSP chain: HPF -> De-esser -> Compressor -> Limiter
    /// Output: 48kHz/24-bit WAV normalized to -14 LUFS with -1 dBFS peak
    /// </summary>
    public string BuildAudioProcessingCommand(
        string inputAudio,
        string outputWav,
        double targetLufs = -14.0,
        double peakCeiling = -1.0)
    {
        var args = new StringBuilder();

        // Input
        args.Append($"-i \"{inputAudio}\" ");

        // Audio filter chain
        args.Append("-af \"");
        
        // HPF: Remove rumble below 80Hz
        args.Append("highpass=f=80,");
        
        // De-esser: Reduce harsh sibilance around 6-8kHz
        args.Append("treble=g=-3:f=7000:w=2000,");
        
        // Compressor: Dynamic range control (3:1 ratio, -18dB threshold)
        args.Append("acompressor=threshold=-18dB:ratio=3:attack=20:release=250:makeup=6dB,");
        
        // Limiter: Prevent peaks above ceiling
        args.Append($"alimiter=limit={peakCeiling}dB:attack=5:release=50,");
        
        // LUFS normalization
        args.Append($"loudnorm=I={targetLufs}:TP={peakCeiling}:LRA=11");
        
        args.Append("\" ");

        // Output format: 48kHz/24-bit WAV
        args.Append("-ar 48000 ");
        args.Append("-sample_fmt s32 "); // 24-bit equivalent
        args.Append("-c:a pcm_s24le ");

        // Overwrite output
        args.Append("-y ");

        // Output file
        args.Append($"\"{outputWav}\"");

        return args.ToString();
    }

    /// <summary>
    /// Builds a filtergraph for compositing video with overlays, text, and transitions
    /// </summary>
    public string BuildFilterGraph(
        Resolution resolution,
        bool addSubtitles = false,
        string? subtitlePath = null)
    {
        var filters = new List<string>();

        // Scale to target resolution with high-quality scaler
        filters.Add($"scale={resolution.Width}:{resolution.Height}:flags=lanczos");

        // Add subtle motion (Ken Burns effect) - optional
        // filters.Add("zoompan=z='min(zoom+0.0015,1.5)':d=125:x='iw/2-(iw/zoom/2)':y='ih/2-(ih/zoom/2)':s=1920x1080");

        // Add subtitles if requested
        if (addSubtitles && !string.IsNullOrEmpty(subtitlePath))
        {
            // Escape path for FFmpeg
            string escapedPath = subtitlePath.Replace("\\", "\\\\").Replace(":", "\\:");
            filters.Add($"subtitles='{escapedPath}':force_style='FontSize=24,PrimaryColour=&HFFFFFF&,OutlineColour=&H000000&,BorderStyle=3'");
        }

        return string.Join(",", filters);
    }

    private void AppendVideoEncoderArgs(StringBuilder args, RenderSpec spec, QualitySettings quality, EncoderType encoder)
    {
        switch (encoder)
        {
            case EncoderType.X264:
                AppendX264Args(args, spec, quality);
                break;

            case EncoderType.NVENC_H264:
            case EncoderType.NVENC_HEVC:
                AppendNvencArgs(args, spec, quality, encoder);
                break;

            case EncoderType.NVENC_AV1:
                AppendNvencAv1Args(args, spec, quality);
                break;

            case EncoderType.AMF_H264:
            case EncoderType.AMF_HEVC:
                AppendAmfArgs(args, spec, quality, encoder);
                break;

            case EncoderType.QSV_H264:
            case EncoderType.QSV_HEVC:
                AppendQsvArgs(args, spec, quality, encoder);
                break;

            default:
                // Fallback to x264
                AppendX264Args(args, spec, quality);
                break;
        }

        // Video bitrate
        args.Append($"-b:v {spec.VideoBitrateK}k ");
    }

    private void AppendX264Args(StringBuilder args, RenderSpec spec, QualitySettings quality)
    {
        args.Append("-c:v libx264 ");

        // CRF: 28 (fast/lower) -> 14 (slow/higher)
        int crf = 28 - (int)(quality.QualityLevel * 0.14);
        crf = Math.Clamp(crf, 14, 28);
        args.Append($"-crf {crf} ");

        // Preset: veryfast -> slow
        string preset = quality.QualityLevel switch
        {
            >= 90 => "slow",
            >= 75 => "medium",
            >= 50 => "fast",
            >= 25 => "faster",
            _ => "veryfast"
        };
        args.Append($"-preset {preset} ");

        // Tune for film
        args.Append("-tune film ");

        // Profile
        args.Append("-profile:v high ");
    }

    private void AppendNvencArgs(StringBuilder args, RenderSpec spec, QualitySettings quality, EncoderType encoder)
    {
        string codec = encoder == EncoderType.NVENC_H264 ? "h264_nvenc" : "hevc_nvenc";
        args.Append($"-c:v {codec} ");

        // Rate control: Constant Quality (CQ)
        args.Append("-rc cq ");

        // CQ value: 33 (fast/lower) -> 18 (slow/higher)
        int cq = 33 - (int)(quality.QualityLevel * 0.15);
        cq = Math.Clamp(cq, 18, 33);
        args.Append($"-cq {cq} ");

        // Preset: p5 (fast) -> p7 (slow)
        int preset = quality.QualityLevel >= 75 ? 7 : (quality.QualityLevel >= 50 ? 6 : 5);
        args.Append($"-preset p{preset} ");

        // Advanced options
        args.Append("-rc-lookahead 16 ");
        args.Append("-spatial-aq 1 ");
        args.Append("-temporal-aq 1 ");
        args.Append("-bf 3 "); // B-frames
    }

    private void AppendNvencAv1Args(StringBuilder args, RenderSpec spec, QualitySettings quality)
    {
        args.Append("-c:v av1_nvenc ");

        // Rate control: Constant Quality (CQ)
        args.Append("-rc cq ");

        // CQ value: 38 (fast/lower) -> 22 (slow/higher)
        int cq = 38 - (int)(quality.QualityLevel * 0.16);
        cq = Math.Clamp(cq, 22, 38);
        args.Append($"-cq {cq} ");

        // Preset: p5 (fast) -> p7 (slow)
        int preset = quality.QualityLevel >= 75 ? 7 : (quality.QualityLevel >= 50 ? 6 : 5);
        args.Append($"-preset p{preset} ");
    }

    private void AppendAmfArgs(StringBuilder args, RenderSpec spec, QualitySettings quality, EncoderType encoder)
    {
        string codec = encoder == EncoderType.AMF_H264 ? "h264_amf" : "hevc_amf";
        args.Append($"-c:v {codec} ");

        // Quality preset
        string preset = quality.QualityLevel >= 75 ? "quality" : "balanced";
        args.Append($"-quality {preset} ");

        // Rate control
        args.Append("-rc cqp ");
        int qp = 28 - (int)(quality.QualityLevel * 0.14);
        qp = Math.Clamp(qp, 14, 28);
        args.Append($"-qp_i {qp} -qp_p {qp} -qp_b {qp} ");
    }

    private void AppendQsvArgs(StringBuilder args, RenderSpec spec, QualitySettings quality, EncoderType encoder)
    {
        string codec = encoder == EncoderType.QSV_H264 ? "h264_qsv" : "hevc_qsv";
        args.Append($"-c:v {codec} ");

        // Quality preset
        string preset = quality.QualityLevel >= 75 ? "veryslow" : (quality.QualityLevel >= 50 ? "medium" : "fast");
        args.Append($"-preset {preset} ");

        // Global quality (lower is better)
        int globalQuality = 28 - (int)(quality.QualityLevel * 0.14);
        globalQuality = Math.Clamp(globalQuality, 14, 28);
        args.Append($"-global_quality {globalQuality} ");
    }

    private void AppendAudioEncoderArgs(StringBuilder args, RenderSpec spec)
    {
        // AAC codec (most compatible)
        args.Append("-c:a aac ");

        // Audio bitrate
        args.Append($"-b:a {spec.AudioBitrateK}k ");

        // Sample rate (48kHz standard for video)
        args.Append("-ar 48000 ");

        // Stereo channels
        args.Append("-ac 2 ");
    }

    /// <summary>
    /// Detects available encoders on the system
    /// </summary>
    public static List<EncoderType> DetectAvailableEncoders(string ffmpegOutput)
    {
        var available = new List<EncoderType>();

        if (ffmpegOutput.Contains("h264_nvenc"))
            available.Add(EncoderType.NVENC_H264);

        if (ffmpegOutput.Contains("hevc_nvenc"))
            available.Add(EncoderType.NVENC_HEVC);

        if (ffmpegOutput.Contains("av1_nvenc"))
            available.Add(EncoderType.NVENC_AV1);

        if (ffmpegOutput.Contains("h264_amf"))
            available.Add(EncoderType.AMF_H264);

        if (ffmpegOutput.Contains("hevc_amf"))
            available.Add(EncoderType.AMF_HEVC);

        if (ffmpegOutput.Contains("h264_qsv"))
            available.Add(EncoderType.QSV_H264);

        if (ffmpegOutput.Contains("hevc_qsv"))
            available.Add(EncoderType.QSV_HEVC);

        // x264 is always available as software fallback
        available.Add(EncoderType.X264);

        return available;
    }

    /// <summary>
    /// Validates render configuration and suggests fixes for illegal combinations
    /// </summary>
    public ValidationResult ValidateConfiguration(
        RenderSpec spec,
        QualitySettings quality,
        EncoderType encoder,
        List<EncoderType>? availableEncoders = null)
    {
        var result = new ValidationResult { IsValid = true };

        // Check if encoder is available
        if (availableEncoders != null && !availableEncoders.Contains(encoder))
        {
            result.IsValid = false;
            result.ErrorMessage = $"Encoder {encoder} is not available on this system.";
            result.SuggestedFix = "Use software encoder (x264) or install required hardware encoder drivers.";
            result.SuggestedEncoder = EncoderType.X264;
            return result;
        }

        // NVENC AV1 requires RTX 40/50 series or newer
        if (encoder == EncoderType.NVENC_AV1)
        {
            // This is a simplified check - in practice, would check GPU generation
            result.IsValid = true; // Assume validated by detection
            result.ErrorMessage = "NVENC AV1 requires NVIDIA RTX 40/50 series GPU.";
            result.SuggestedFix = "Use NVENC HEVC (h265) or NVENC H264 for older GPUs.";
            result.SuggestedEncoder = EncoderType.NVENC_HEVC;
        }

        // Validate frame rate
        if (quality.Fps < 1 || quality.Fps > 120)
        {
            result.IsValid = false;
            result.ErrorMessage = $"Frame rate {quality.Fps} is out of valid range (1-120 fps).";
            result.SuggestedFix = "Use standard frame rates: 24, 30, 60, or 120 fps.";
            result.SuggestedQuality = new QualitySettings
            {
                QualityLevel = quality.QualityLevel,
                Fps = quality.Fps > 60 ? 60 : 30,
                EnableSceneCut = quality.EnableSceneCut,
                Codec = quality.Codec
            };
            return result;
        }

        // Validate quality level
        if (quality.QualityLevel < 0 || quality.QualityLevel > 100)
        {
            result.IsValid = false;
            result.ErrorMessage = $"Quality level {quality.QualityLevel} is out of valid range (0-100).";
            result.SuggestedFix = "Use quality level between 0 (fastest/lowest) and 100 (slowest/highest).";
            result.SuggestedQuality = new QualitySettings
            {
                QualityLevel = Math.Clamp(quality.QualityLevel, 0, 100),
                Fps = quality.Fps,
                EnableSceneCut = quality.EnableSceneCut,
                Codec = quality.Codec
            };
            return result;
        }

        // Validate resolution
        if (spec.Res.Width < 320 || spec.Res.Height < 240)
        {
            result.IsValid = false;
            result.ErrorMessage = $"Resolution {spec.Res.Width}x{spec.Res.Height} is too small. Minimum is 320x240.";
            result.SuggestedFix = "Use a standard resolution like 1280x720 (HD) or 1920x1080 (Full HD).";
            return result;
        }

        // Warn about high bitrate on low resolution
        if (spec.VideoBitrateK > 20000 && spec.Res.Width < 1920)
        {
            result.IsValid = true;
            result.ErrorMessage = $"Video bitrate {spec.VideoBitrateK}k is very high for resolution {spec.Res.Width}x{spec.Res.Height}.";
            result.SuggestedFix = "Consider reducing bitrate to 8000k for 1080p or 5000k for 720p.";
        }

        // Warn about codec mismatch
        if (quality.Codec == "AV1" && encoder != EncoderType.NVENC_AV1)
        {
            result.IsValid = false;
            result.ErrorMessage = "AV1 codec selected but encoder doesn't support AV1.";
            result.SuggestedFix = "Select NVENC_AV1 encoder or change codec to H264/HEVC.";
            result.SuggestedEncoder = EncoderType.NVENC_AV1;
            return result;
        }

        return result;
    }

    /// <summary>
    /// Gets a safe default encoder based on available hardware
    /// </summary>
    public static EncoderType GetSafeDefaultEncoder(List<EncoderType> availableEncoders)
    {
        // Prefer NVENC H264 for best compatibility and performance
        if (availableEncoders.Contains(EncoderType.NVENC_H264))
            return EncoderType.NVENC_H264;

        // AMF H264 as second choice
        if (availableEncoders.Contains(EncoderType.AMF_H264))
            return EncoderType.AMF_H264;

        // QSV H264 as third choice
        if (availableEncoders.Contains(EncoderType.QSV_H264))
            return EncoderType.QSV_H264;

        // x264 as fallback
        return EncoderType.X264;
    }
}
