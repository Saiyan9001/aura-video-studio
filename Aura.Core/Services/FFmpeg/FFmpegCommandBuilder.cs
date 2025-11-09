using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using Aura.Core.Models;
using Aura.Core.Models.Export;

namespace Aura.Core.Services.FFmpeg;

/// <summary>
/// Builder for constructing FFmpeg command line arguments
/// </summary>
public class FFmpegCommandBuilder
{
    private readonly List<string> _inputFiles = new();
    private readonly List<string> _outputOptions = new();
    private readonly List<string> _filterComplex = new();
    private string? _outputFile;
    private bool _overwrite = true;
    private int? _threads;
    private string? _hwaccel;
    private Dictionary<string, string> _metadata = new();
    private string? _videoCodec;

    /// <summary>
    /// Add an input file
    /// </summary>
    public FFmpegCommandBuilder AddInput(string filePath)
    {
        _inputFiles.Add($"-i \"{filePath}\"");
        return this;
    }

    /// <summary>
    /// Set the output file
    /// </summary>
    public FFmpegCommandBuilder SetOutput(string filePath)
    {
        _outputFile = filePath;
        return this;
    }

    /// <summary>
    /// Set whether to overwrite output file
    /// </summary>
    public FFmpegCommandBuilder SetOverwrite(bool overwrite)
    {
        _overwrite = overwrite;
        return this;
    }

    /// <summary>
    /// Set video codec
    /// </summary>
    public FFmpegCommandBuilder SetVideoCodec(string codec)
    {
        _videoCodec = codec;
        _outputOptions.Add($"-c:v {codec}");
        return this;
    }

    /// <summary>
    /// Set audio codec
    /// </summary>
    public FFmpegCommandBuilder SetAudioCodec(string codec)
    {
        _outputOptions.Add($"-c:a {codec}");
        return this;
    }

    /// <summary>
    /// Set video bitrate
    /// </summary>
    public FFmpegCommandBuilder SetVideoBitrate(int bitrateKbps)
    {
        _outputOptions.Add($"-b:v {bitrateKbps}k");
        return this;
    }

    /// <summary>
    /// Set audio bitrate
    /// </summary>
    public FFmpegCommandBuilder SetAudioBitrate(int bitrateKbps)
    {
        _outputOptions.Add($"-b:a {bitrateKbps}k");
        return this;
    }

    /// <summary>
    /// Set output resolution
    /// </summary>
    public FFmpegCommandBuilder SetResolution(int width, int height)
    {
        _outputOptions.Add($"-s {width}x{height}");
        return this;
    }

    /// <summary>
    /// Set frame rate
    /// </summary>
    public FFmpegCommandBuilder SetFrameRate(int fps)
    {
        _outputOptions.Add($"-r {fps}");
        return this;
    }

    /// <summary>
    /// Set pixel format
    /// </summary>
    public FFmpegCommandBuilder SetPixelFormat(string format)
    {
        _outputOptions.Add($"-pix_fmt {format}");
        return this;
    }

    /// <summary>
    /// Set hardware acceleration
    /// </summary>
    public FFmpegCommandBuilder SetHardwareAcceleration(string hwaccel)
    {
        _hwaccel = hwaccel;
        return this;
    }

    /// <summary>
    /// Set number of threads
    /// </summary>
    public FFmpegCommandBuilder SetThreads(int threads)
    {
        _threads = threads;
        return this;
    }

    /// <summary>
    /// Add a video filter
    /// </summary>
    public FFmpegCommandBuilder AddFilter(string filter)
    {
        _filterComplex.Add(filter);
        return this;
    }

    /// <summary>
    /// Add scale filter with proper aspect ratio handling
    /// </summary>
    public FFmpegCommandBuilder AddScaleFilter(int width, int height, string scaleMode = "fit")
    {
        var filter = scaleMode switch
        {
            "fit" => $"scale={width}:{height}:force_original_aspect_ratio=decrease,pad={width}:{height}:(ow-iw)/2:(oh-ih)/2",
            "crop" => $"scale={width}:{height}:force_original_aspect_ratio=increase,crop={width}:{height}",
            "stretch" => $"scale={width}:{height}",
            _ => $"scale={width}:{height}:force_original_aspect_ratio=decrease,pad={width}:{height}:(ow-iw)/2:(oh-ih)/2"
        };
        
        _filterComplex.Add(filter);
        return this;
    }

    /// <summary>
    /// Add fade in effect
    /// </summary>
    public FFmpegCommandBuilder AddFadeIn(double durationSeconds)
    {
        _filterComplex.Add($"fade=t=in:st=0:d={durationSeconds.ToString(CultureInfo.InvariantCulture)}");
        return this;
    }

    /// <summary>
    /// Add fade out effect
    /// </summary>
    public FFmpegCommandBuilder AddFadeOut(double startSeconds, double durationSeconds)
    {
        _filterComplex.Add($"fade=t=out:st={startSeconds.ToString(CultureInfo.InvariantCulture)}:d={durationSeconds.ToString(CultureInfo.InvariantCulture)}");
        return this;
    }

    /// <summary>
    /// Add crossfade transition between two video streams
    /// </summary>
    /// <param name="durationSeconds">Duration of the crossfade in seconds</param>
    /// <param name="offset">Time offset where transition starts</param>
    public FFmpegCommandBuilder AddCrossfadeTransition(double durationSeconds, double offset)
    {
        var duration = durationSeconds.ToString(CultureInfo.InvariantCulture);
        var offsetStr = offset.ToString(CultureInfo.InvariantCulture);
        _filterComplex.Add($"xfade=transition=fade:duration={duration}:offset={offsetStr}");
        return this;
    }

    /// <summary>
    /// Add wipe transition between two video streams
    /// </summary>
    /// <param name="durationSeconds">Duration of the wipe in seconds</param>
    /// <param name="offset">Time offset where transition starts</param>
    /// <param name="direction">Wipe direction: left, right, up, down</param>
    public FFmpegCommandBuilder AddWipeTransition(double durationSeconds, double offset, string direction = "right")
    {
        var duration = durationSeconds.ToString(CultureInfo.InvariantCulture);
        var offsetStr = offset.ToString(CultureInfo.InvariantCulture);
        var transitionType = direction.ToLowerInvariant() switch
        {
            "left" => "wipeleft",
            "right" => "wiperight",
            "up" => "wipeup",
            "down" => "wipedown",
            _ => "wiperight"
        };
        _filterComplex.Add($"xfade=transition={transitionType}:duration={duration}:offset={offsetStr}");
        return this;
    }

    /// <summary>
    /// Add dissolve transition between two video streams
    /// </summary>
    /// <param name="durationSeconds">Duration of the dissolve in seconds</param>
    /// <param name="offset">Time offset where transition starts</param>
    public FFmpegCommandBuilder AddDissolveTransition(double durationSeconds, double offset)
    {
        var duration = durationSeconds.ToString(CultureInfo.InvariantCulture);
        var offsetStr = offset.ToString(CultureInfo.InvariantCulture);
        _filterComplex.Add($"xfade=transition=dissolve:duration={duration}:offset={offsetStr}");
        return this;
    }

    /// <summary>
    /// Add Ken Burns effect (zoom and pan) to static images
    /// </summary>
    /// <param name="durationSeconds">Duration of the effect</param>
    /// <param name="zoomStart">Starting zoom level (1.0 = no zoom)</param>
    /// <param name="zoomEnd">Ending zoom level (1.5 = 150% zoom)</param>
    /// <param name="panX">Horizontal pan (-1.0 to 1.0, 0 = center)</param>
    /// <param name="panY">Vertical pan (-1.0 to 1.0, 0 = center)</param>
    public FFmpegCommandBuilder AddKenBurnsEffect(double durationSeconds, double zoomStart = 1.0, double zoomEnd = 1.2, double panX = 0.0, double panY = 0.0)
    {
        var duration = durationSeconds.ToString(CultureInfo.InvariantCulture);
        var zS = zoomStart.ToString(CultureInfo.InvariantCulture);
        var zE = zoomEnd.ToString(CultureInfo.InvariantCulture);
        
        var startX = (0.5 - panX * 0.3).ToString(CultureInfo.InvariantCulture);
        var startY = (0.5 - panY * 0.3).ToString(CultureInfo.InvariantCulture);
        var endX = (0.5 + panX * 0.3).ToString(CultureInfo.InvariantCulture);
        var endY = (0.5 + panY * 0.3).ToString(CultureInfo.InvariantCulture);
        
        var filter = $"zoompan=z='if(lte(zoom,{zS}),{zS},if(gte(zoom,{zE}),{zE},zoom+({zE}-{zS})/{duration}/fps))':x='iw/2-(iw/zoom/2)+({endX}-{startX})*on/{duration}/fps*iw':y='ih/2-(ih/zoom/2)+({endY}-{startY})*on/{duration}/fps*ih':d={duration}*fps:s=1920x1080:fps=30";
        _filterComplex.Add(filter);
        return this;
    }

    /// <summary>
    /// Add picture-in-picture overlay
    /// </summary>
    /// <param name="overlayInputIndex">Input index of the overlay video (0-based)</param>
    /// <param name="x">X position (pixels or expression like 'W-w-10')</param>
    /// <param name="y">Y position (pixels or expression like 'H-h-10')</param>
    /// <param name="scale">Scale factor for overlay (1.0 = original size, 0.25 = quarter size)</param>
    public FFmpegCommandBuilder AddPictureInPicture(int overlayInputIndex, string x = "W-w-10", string y = "H-h-10", double scale = 0.25)
    {
        var scaleStr = scale.ToString(CultureInfo.InvariantCulture);
        _filterComplex.Add($"[{overlayInputIndex}:v]scale=iw*{scaleStr}:ih*{scaleStr}[pip];[0:v][pip]overlay={x}:{y}");
        return this;
    }

    /// <summary>
    /// Add text overlay with positioning
    /// </summary>
    /// <param name="text">Text to display</param>
    /// <param name="fontFile">Path to font file</param>
    /// <param name="fontSize">Font size in pixels</param>
    /// <param name="x">X position (pixels or expression like '(w-text_w)/2' for center)</param>
    /// <param name="y">Y position (pixels or expression like '(h-text_h)/2' for center)</param>
    /// <param name="fontColor">Font color (e.g., 'white', '#FFFFFF')</param>
    /// <param name="boxColor">Background box color with alpha (e.g., 'black@0.5')</param>
    public FFmpegCommandBuilder AddTextOverlay(string text, string? fontFile = null, int fontSize = 48, string x = "(w-text_w)/2", string y = "(h-text_h)/2", string fontColor = "white", string? boxColor = null)
    {
        var escapedText = text.Replace(":", "\\:").Replace("'", "\\'");
        var filter = $"drawtext=text='{escapedText}':fontsize={fontSize}:x={x}:y={y}:fontcolor={fontColor}";
        
        if (!string.IsNullOrEmpty(fontFile))
        {
            filter += $":fontfile={fontFile}";
        }
        
        if (!string.IsNullOrEmpty(boxColor))
        {
            filter += $":box=1:boxcolor={boxColor}";
        }
        
        _filterComplex.Add(filter);
        return this;
    }

    /// <summary>
    /// Add animated text overlay with fade in/out
    /// </summary>
    /// <param name="text">Text to display</param>
    /// <param name="startTime">Start time in seconds</param>
    /// <param name="duration">Duration in seconds</param>
    /// <param name="fadeInDuration">Fade in duration in seconds</param>
    /// <param name="fadeOutDuration">Fade out duration in seconds</param>
    /// <param name="fontSize">Font size in pixels</param>
    /// <param name="x">X position expression</param>
    /// <param name="y">Y position expression</param>
    public FFmpegCommandBuilder AddAnimatedTextOverlay(string text, double startTime, double duration, double fadeInDuration = 0.5, double fadeOutDuration = 0.5, int fontSize = 48, string x = "(w-text_w)/2", string y = "(h-text_h)/2")
    {
        var escapedText = text.Replace(":", "\\:").Replace("'", "\\'");
        var st = startTime.ToString(CultureInfo.InvariantCulture);
        var dur = duration.ToString(CultureInfo.InvariantCulture);
        var fadeIn = fadeInDuration.ToString(CultureInfo.InvariantCulture);
        var fadeOut = fadeOutDuration.ToString(CultureInfo.InvariantCulture);
        var endTime = (startTime + duration).ToString(CultureInfo.InvariantCulture);
        var fadeOutStart = (startTime + duration - fadeOutDuration).ToString(CultureInfo.InvariantCulture);
        
        var alpha = $"if(lt(t,{st}),0,if(lt(t,{st}+{fadeIn}),(t-{st})/{fadeIn},if(lt(t,{fadeOutStart}),1,if(lt(t,{endTime}),({endTime}-t)/{fadeOut},0))))";
        var filter = $"drawtext=text='{escapedText}':fontsize={fontSize}:x={x}:y={y}:fontcolor=white:alpha='{alpha}':enable='between(t,{st},{endTime})'";
        
        _filterComplex.Add(filter);
        return this;
    }

    /// <summary>
    /// Add sliding text animation
    /// </summary>
    /// <param name="text">Text to display</param>
    /// <param name="startTime">Start time in seconds</param>
    /// <param name="duration">Duration in seconds</param>
    /// <param name="direction">Slide direction: left, right, up, down</param>
    /// <param name="fontSize">Font size in pixels</param>
    public FFmpegCommandBuilder AddSlidingTextOverlay(string text, double startTime, double duration, string direction = "left", int fontSize = 48)
    {
        var escapedText = text.Replace(":", "\\:").Replace("'", "\\'");
        var st = startTime.ToString(CultureInfo.InvariantCulture);
        var dur = duration.ToString(CultureInfo.InvariantCulture);
        var endTime = (startTime + duration).ToString(CultureInfo.InvariantCulture);
        
        var (xExpr, yExpr) = direction.ToLowerInvariant() switch
        {
            "left" => ($"w-((t-{st})/{dur})*(w+text_w)", "(h-text_h)/2"),
            "right" => ($"-text_w+((t-{st})/{dur})*(w+text_w)", "(h-text_h)/2"),
            "up" => ("(w-text_w)/2", $"h-((t-{st})/{dur})*(h+text_h)"),
            "down" => ("(w-text_w)/2", $"-text_h+((t-{st})/{dur})*(h+text_h)"),
            _ => ($"w-((t-{st})/{dur})*(w+text_w)", "(h-text_h)/2")
        };
        
        var filter = $"drawtext=text='{escapedText}':fontsize={fontSize}:x='{xExpr}':y='{yExpr}':fontcolor=white:enable='between(t,{st},{endTime})'";
        _filterComplex.Add(filter);
        return this;
    }

    /// <summary>
    /// Add audio mixing from multiple sources
    /// </summary>
    /// <param name="inputCount">Number of audio inputs to mix</param>
    /// <param name="weights">Volume weights for each input (1.0 = full volume)</param>
    public FFmpegCommandBuilder AddAudioMix(int inputCount, double[]? weights = null)
    {
        var inputs = new List<string>();
        for (int i = 0; i < inputCount; i++)
        {
            var weight = weights != null && i < weights.Length ? weights[i] : 1.0;
            inputs.Add($"[{i}:a]volume={weight.ToString(CultureInfo.InvariantCulture)}[a{i}]");
        }
        
        var mixInputs = string.Join("", Enumerable.Range(0, inputCount).Select(i => $"[a{i}]"));
        var filter = string.Join(";", inputs) + $";{mixInputs}amix=inputs={inputCount}:duration=longest[aout]";
        _filterComplex.Add(filter);
        return this;
    }

    /// <summary>
    /// Add audio ducking (lower background audio when foreground audio is present)
    /// </summary>
    /// <param name="foregroundIndex">Input index of foreground audio (e.g., voice)</param>
    /// <param name="backgroundIndex">Input index of background audio (e.g., music)</param>
    /// <param name="threshold">Threshold in dB for ducking (-40 to 0)</param>
    /// <param name="ratio">Reduction ratio (1 to 20, higher = more reduction)</param>
    /// <param name="attack">Attack time in milliseconds</param>
    /// <param name="release">Release time in milliseconds</param>
    public FFmpegCommandBuilder AddAudioDucking(int foregroundIndex = 0, int backgroundIndex = 1, double threshold = -20, double ratio = 4, int attack = 20, int release = 250)
    {
        var thresholdStr = threshold.ToString(CultureInfo.InvariantCulture);
        var ratioStr = ratio.ToString(CultureInfo.InvariantCulture);
        var filter = $"[{backgroundIndex}:a][{foregroundIndex}:a]sidechaincompress=threshold={thresholdStr}dB:ratio={ratioStr}:attack={attack}:release={release}[aout]";
        _filterComplex.Add(filter);
        return this;
    }

    /// <summary>
    /// Add watermark overlay
    /// </summary>
    /// <param name="watermarkPath">Path to watermark image</param>
    /// <param name="position">Position: top-left, top-right, bottom-left, bottom-right, center</param>
    /// <param name="opacity">Opacity (0.0 to 1.0)</param>
    /// <param name="margin">Margin from edges in pixels</param>
    public FFmpegCommandBuilder AddWatermark(string watermarkPath, string position = "bottom-right", double opacity = 0.7, int margin = 10)
    {
        var (x, y) = position.ToLowerInvariant() switch
        {
            "top-left" => (margin.ToString(), margin.ToString()),
            "top-right" => ($"W-w-{margin}", margin.ToString()),
            "bottom-left" => (margin.ToString(), $"H-h-{margin}"),
            "bottom-right" => ($"W-w-{margin}", $"H-h-{margin}"),
            "center" => ("(W-w)/2", "(H-h)/2"),
            _ => ($"W-w-{margin}", $"H-h-{margin}")
        };
        
        var opacityStr = opacity.ToString(CultureInfo.InvariantCulture);
        _filterComplex.Add($"movie={watermarkPath},format=rgba,colorchannelmixer=aa={opacityStr}[wm];[0:v][wm]overlay={x}:{y}");
        return this;
    }

    /// <summary>
    /// Set encoding preset (ultrafast, fast, medium, slow, slower)
    /// </summary>
    public FFmpegCommandBuilder SetPreset(string preset)
    {
        _outputOptions.Add($"-preset {preset}");
        return this;
    }

    /// <summary>
    /// Set CRF quality (0-51, lower is better quality)
    /// </summary>
    public FFmpegCommandBuilder SetCRF(int crf)
    {
        if (crf < 0 || crf > 51)
        {
            throw new ArgumentOutOfRangeException(nameof(crf), "CRF must be between 0 and 51");
        }
        _outputOptions.Add($"-crf {crf}");
        return this;
    }

    /// <summary>
    /// Add metadata
    /// </summary>
    public FFmpegCommandBuilder AddMetadata(string key, string value)
    {
        _metadata[key] = value;
        return this;
    }

    /// <summary>
    /// Set start time for trimming
    /// </summary>
    public FFmpegCommandBuilder SetStartTime(TimeSpan time)
    {
        _outputOptions.Add($"-ss {time:hh\\:mm\\:ss\\.fff}");
        return this;
    }

    /// <summary>
    /// Set duration for trimming
    /// </summary>
    public FFmpegCommandBuilder SetDuration(TimeSpan duration)
    {
        _outputOptions.Add($"-t {duration:hh\\:mm\\:ss\\.fff}");
        return this;
    }

    /// <summary>
    /// Set audio sample rate
    /// </summary>
    public FFmpegCommandBuilder SetAudioSampleRate(int sampleRate)
    {
        _outputOptions.Add($"-ar {sampleRate}");
        return this;
    }

    /// <summary>
    /// Set audio channels
    /// </summary>
    public FFmpegCommandBuilder SetAudioChannels(int channels)
    {
        _outputOptions.Add($"-ac {channels}");
        return this;
    }

    /// <summary>
    /// Set color space for HDR/advanced encoding
    /// </summary>
    public FFmpegCommandBuilder SetColorSpace(string colorSpace)
    {
        _outputOptions.Add($"-colorspace {colorSpace}");
        return this;
    }

    /// <summary>
    /// Set color transfer function for HDR
    /// </summary>
    public FFmpegCommandBuilder SetColorTransfer(string colorTransfer)
    {
        _outputOptions.Add($"-color_trc {colorTransfer}");
        return this;
    }

    /// <summary>
    /// Set color primaries for HDR
    /// </summary>
    public FFmpegCommandBuilder SetColorPrimaries(string colorPrimaries)
    {
        _outputOptions.Add($"-color_primaries {colorPrimaries}");
        return this;
    }

    /// <summary>
    /// Set HDR metadata (MaxCLL and MaxFALL) for x265/hevc encoders
    /// </summary>
    public FFmpegCommandBuilder SetHdrMetadata(int? maxCll, int? maxFall)
    {
        if (maxCll.HasValue && maxFall.HasValue)
        {
            if (_videoCodec?.Contains("x265", StringComparison.OrdinalIgnoreCase) == true ||
                _videoCodec?.Contains("libx265", StringComparison.OrdinalIgnoreCase) == true)
            {
                _outputOptions.Add($"-x265-params \"max-cll={maxCll.Value},{maxFall.Value}\"");
            }
        }
        return this;
    }

    /// <summary>
    /// Apply advanced codec options from AdvancedCodecOptions model
    /// </summary>
    public FFmpegCommandBuilder ApplyAdvancedCodecOptions(AdvancedCodecOptions options)
    {
        SetPixelFormat(options.GetPixelFormat());
        SetColorSpace(options.GetColorSpace());
        SetColorPrimaries(options.GetColorPrimaries());
        
        var colorTransfer = options.GetColorTransfer();
        if (!string.IsNullOrEmpty(colorTransfer))
        {
            SetColorTransfer(colorTransfer);
        }
        
        if (options.IsHdr && options.MaxContentLightLevel.HasValue && options.MaxFrameAverageLightLevel.HasValue)
        {
            SetHdrMetadata(options.MaxContentLightLevel.Value, options.MaxFrameAverageLightLevel.Value);
        }
        
        return this;
    }

    /// <summary>
    /// Enable two-pass encoding for better quality
    /// </summary>
    /// <param name="passLogFile">Path to pass log file</param>
    /// <param name="pass">Pass number (1 or 2)</param>
    public FFmpegCommandBuilder SetTwoPassEncoding(string passLogFile, int pass)
    {
        if (pass != 1 && pass != 2)
        {
            throw new ArgumentOutOfRangeException(nameof(pass), "Pass must be 1 or 2");
        }

        _outputOptions.Add($"-pass {pass}");
        _outputOptions.Add($"-passlogfile \"{passLogFile}\"");
        
        if (pass == 1)
        {
            _outputOptions.Add("-an");
            _outputOptions.Add("-f null");
        }
        
        return this;
    }

    /// <summary>
    /// Add chapter markers for long-form content
    /// </summary>
    /// <param name="chapters">List of chapter markers with time and title</param>
    public FFmpegCommandBuilder AddChapterMarkers(IEnumerable<(TimeSpan time, string title)> chapters)
    {
        var chapterList = chapters.OrderBy(c => c.time).ToList();
        for (int i = 0; i < chapterList.Count; i++)
        {
            var chapter = chapterList[i];
            var startTime = chapter.time.TotalMilliseconds;
            var endTime = i < chapterList.Count - 1 
                ? chapterList[i + 1].time.TotalMilliseconds 
                : startTime + 1000;
            
            AddMetadata($"chapter{i}_start", ((long)startTime).ToString());
            AddMetadata($"chapter{i}_end", ((long)endTime).ToString());
            AddMetadata($"chapter{i}_title", chapter.title);
        }
        return this;
    }

    /// <summary>
    /// Set maximum bitrate for adaptive streaming
    /// </summary>
    /// <param name="maxBitrateKbps">Maximum bitrate in kbps</param>
    public FFmpegCommandBuilder SetMaxBitrate(int maxBitrateKbps)
    {
        _outputOptions.Add($"-maxrate {maxBitrateKbps}k");
        return this;
    }

    /// <summary>
    /// Set buffer size for rate control
    /// </summary>
    /// <param name="bufferSizeKbps">Buffer size in kbps</param>
    public FFmpegCommandBuilder SetBufferSize(int bufferSizeKbps)
    {
        _outputOptions.Add($"-bufsize {bufferSizeKbps}k");
        return this;
    }

    /// <summary>
    /// Build the complete FFmpeg command
    /// </summary>
    public string Build()
    {
        if (_outputFile == null)
        {
            throw new InvalidOperationException("Output file must be set");
        }

        var command = new StringBuilder();

        // Add overwrite flag
        if (_overwrite)
        {
            command.Append("-y ");
        }

        // Add hardware acceleration
        if (!string.IsNullOrEmpty(_hwaccel))
        {
            command.Append($"-hwaccel {_hwaccel} ");
        }

        // Add input files
        foreach (var input in _inputFiles)
        {
            command.Append($"{input} ");
        }

        // Add threads
        if (_threads.HasValue)
        {
            command.Append($"-threads {_threads.Value} ");
        }

        // Add filter complex
        if (_filterComplex.Count > 0)
        {
            var filters = string.Join(",", _filterComplex);
            command.Append($"-filter_complex \"{filters}\" ");
        }

        // Add output options
        foreach (var option in _outputOptions)
        {
            command.Append($"{option} ");
        }

        // Add metadata
        foreach (var metadata in _metadata)
        {
            command.Append($"-metadata {metadata.Key}=\"{metadata.Value}\" ");
        }

        // Add output file
        command.Append($"\"{_outputFile}\"");

        return command.ToString();
    }

    /// <summary>
    /// Create a builder from an export preset
    /// </summary>
    public static FFmpegCommandBuilder FromPreset(ExportPreset preset, string inputFile, string outputFile)
    {
        var builder = new FFmpegCommandBuilder()
            .AddInput(inputFile)
            .SetOutput(outputFile)
            .SetVideoCodec(preset.VideoCodec)
            .SetAudioCodec(preset.AudioCodec)
            .SetVideoBitrate(preset.VideoBitrate)
            .SetAudioBitrate(preset.AudioBitrate)
            .SetResolution(preset.Resolution.Width, preset.Resolution.Height)
            .SetFrameRate(preset.FrameRate)
            .SetPixelFormat(preset.PixelFormat);

        // Set encoding preset based on quality
        var encodingPreset = preset.Quality switch
        {
            QualityLevel.Draft => "ultrafast",
            QualityLevel.Good => "fast",
            QualityLevel.High => "medium",
            QualityLevel.Maximum => "slow",
            _ => "medium"
        };
        
        builder.SetPreset(encodingPreset);

        return builder;
    }

    /// <summary>
    /// Create a builder for generating a thumbnail
    /// </summary>
    public static FFmpegCommandBuilder CreateThumbnailCommand(
        string inputFile, 
        string outputFile, 
        TimeSpan position,
        int width = 1280,
        int height = 720)
    {
        return new FFmpegCommandBuilder()
            .AddInput(inputFile)
            .SetOutput(outputFile)
            .SetStartTime(position)
            .AddFilter($"scale={width}:{height}:force_original_aspect_ratio=decrease")
            .SetVideoCodec("mjpeg")
            .AddMetadata("comment", "Generated by Aura Video Studio");
    }
}

/// <summary>
/// Extension methods for common FFmpeg operations
/// </summary>
public static class FFmpegCommandBuilderExtensions
{
    /// <summary>
    /// Configure builder for platform-optimized export
    /// </summary>
    public static FFmpegCommandBuilder ConfigureForPlatform(
        this FFmpegCommandBuilder builder,
        IPlatformExportProfile profile,
        Resolution targetResolution)
    {
        // Set recommended bitrate
        builder.SetVideoBitrate(profile.RecommendedVideoBitrate);
        builder.SetAudioBitrate(profile.RecommendedAudioBitrate);
        
        // Set recommended frame rate
        builder.SetFrameRate(profile.RecommendedFrameRate);
        
        // Add platform-specific optimizations
        builder.AddMetadata("platform", profile.PlatformName);
        
        return builder;
    }
}
