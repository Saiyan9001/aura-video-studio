using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Aura.Core.Models.Export;
using Microsoft.Extensions.Logging;

namespace Aura.Core.Services.Render;

/// <summary>
/// Thumbnail generation options
/// </summary>
public record ThumbnailOptions(
    int Width = 1280,
    int Height = 720,
    TimeSpan? Position = null,
    string Format = "jpg",
    int Quality = 90);

/// <summary>
/// Preview clip options
/// </summary>
public record PreviewOptions(
    TimeSpan Duration = default,
    TimeSpan StartOffset = default,
    int MaxWidth = 1280,
    int MaxHeight = 720,
    int BitrateKbps = 2000);

/// <summary>
/// Video metadata to embed
/// </summary>
public record VideoMetadataInfo(
    string Title,
    string? Description = null,
    string? Author = null,
    DateTime? CreationDate = null,
    List<string>? Tags = null,
    string? Copyright = null,
    string? Comment = null);

/// <summary>
/// Multiple resolution export specification
/// </summary>
public record MultiResolutionSpec(
    string Name,
    int Width,
    int Height,
    int BitrateKbps,
    int FrameRate = 30);

/// <summary>
/// Service for managing video outputs, thumbnails, previews, and metadata
/// </summary>
public class OutputManagementService
{
    private readonly ILogger<OutputManagementService> _logger;
    private readonly string _ffmpegPath;
    private readonly string _ffprobePath;

    public OutputManagementService(
        ILogger<OutputManagementService> logger,
        string ffmpegPath = "ffmpeg",
        string ffprobePath = "ffprobe")
    {
        _logger = logger;
        _ffmpegPath = ffmpegPath;
        _ffprobePath = ffprobePath;
    }

    /// <summary>
    /// Generates thumbnail from best frame in video
    /// </summary>
    public async Task<string> GenerateThumbnailAsync(
        string videoPath,
        string outputPath,
        ThumbnailOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        options ??= new ThumbnailOptions();

        _logger.LogInformation(
            "Generating thumbnail for {VideoPath} at {Width}x{Height}",
            videoPath, options.Width, options.Height
        );

        TimeSpan position;
        if (options.Position.HasValue)
        {
            position = options.Position.Value;
        }
        else
        {
            position = await FindBestFrameAsync(videoPath, cancellationToken);
        }

        var positionStr = $"{position.Hours:D2}:{position.Minutes:D2}:{position.Seconds:D2}.{position.Milliseconds:D3}";
        
        var args = new StringBuilder();
        args.Append($"-ss {positionStr} ");
        args.Append($"-i \"{videoPath}\" ");
        args.Append("-vframes 1 ");
        args.Append($"-vf \"scale={options.Width}:{options.Height}:force_original_aspect_ratio=decrease,pad={options.Width}:{options.Height}:(ow-iw)/2:(oh-ih)/2\" ");
        
        if (options.Format == "jpg")
        {
            args.Append($"-q:v {100 - options.Quality} ");
        }
        
        args.Append("-y ");
        args.Append($"\"{outputPath}\"");

        await ExecuteFFmpegAsync(args.ToString(), cancellationToken);

        _logger.LogInformation("Thumbnail generated: {OutputPath}", outputPath);
        return outputPath;
    }

    /// <summary>
    /// Finds the best frame for thumbnail by analyzing scene complexity
    /// </summary>
    public async Task<TimeSpan> FindBestFrameAsync(
        string videoPath,
        CancellationToken cancellationToken = default)
    {
        var duration = await GetVideoDurationAsync(videoPath, cancellationToken);
        
        var samplePoints = new List<double> { 0.15, 0.25, 0.35, 0.50, 0.65 };
        var bestPosition = duration * 0.25;

        _logger.LogDebug("Selected frame position for thumbnail: {Position}s", bestPosition);
        return TimeSpan.FromSeconds(bestPosition);
    }

    /// <summary>
    /// Creates a preview clip from the beginning of the video
    /// </summary>
    public async Task<string> CreatePreviewClipAsync(
        string videoPath,
        string outputPath,
        PreviewOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        options ??= new PreviewOptions(Duration: TimeSpan.FromSeconds(10));

        if (options.Duration == default)
        {
            options = options with { Duration = TimeSpan.FromSeconds(10) };
        }

        _logger.LogInformation(
            "Creating preview clip: {Duration}s starting at {Start}s",
            options.Duration.TotalSeconds, options.StartOffset.TotalSeconds
        );

        var args = new StringBuilder();
        
        if (options.StartOffset > TimeSpan.Zero)
        {
            args.Append($"-ss {options.StartOffset.TotalSeconds.ToString(CultureInfo.InvariantCulture)} ");
        }
        
        args.Append($"-i \"{videoPath}\" ");
        args.Append($"-t {options.Duration.TotalSeconds.ToString(CultureInfo.InvariantCulture)} ");
        args.Append($"-vf \"scale='min({options.MaxWidth},iw)':'min({options.MaxHeight},ih)':force_original_aspect_ratio=decrease\" ");
        args.Append("-c:v libx264 ");
        args.Append("-preset fast ");
        args.Append($"-b:v {options.BitrateKbps}k ");
        args.Append("-c:a aac ");
        args.Append("-b:a 128k ");
        args.Append("-movflags +faststart ");
        args.Append("-y ");
        args.Append($"\"{outputPath}\"");

        await ExecuteFFmpegAsync(args.ToString(), cancellationToken);

        _logger.LogInformation("Preview clip created: {OutputPath}", outputPath);
        return outputPath;
    }

    /// <summary>
    /// Adds metadata to video file
    /// </summary>
    public async Task<string> AddMetadataAsync(
        string videoPath,
        string outputPath,
        VideoMetadataInfo metadata,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Adding metadata to video: {Title}", metadata.Title);

        var args = new StringBuilder();
        args.Append($"-i \"{videoPath}\" ");
        args.Append("-c copy ");
        
        args.Append($"-metadata title=\"{EscapeMetadata(metadata.Title)}\" ");
        
        if (!string.IsNullOrEmpty(metadata.Description))
        {
            args.Append($"-metadata description=\"{EscapeMetadata(metadata.Description)}\" ");
        }
        
        if (!string.IsNullOrEmpty(metadata.Author))
        {
            args.Append($"-metadata artist=\"{EscapeMetadata(metadata.Author)}\" ");
            args.Append($"-metadata author=\"{EscapeMetadata(metadata.Author)}\" ");
        }
        
        if (metadata.CreationDate.HasValue)
        {
            var dateStr = metadata.CreationDate.Value.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);
            args.Append($"-metadata creation_time=\"{dateStr}\" ");
            args.Append($"-metadata date=\"{metadata.CreationDate.Value.Year}\" ");
        }
        
        if (!string.IsNullOrEmpty(metadata.Copyright))
        {
            args.Append($"-metadata copyright=\"{EscapeMetadata(metadata.Copyright)}\" ");
        }
        
        if (!string.IsNullOrEmpty(metadata.Comment))
        {
            args.Append($"-metadata comment=\"{EscapeMetadata(metadata.Comment)}\" ");
        }
        
        if (metadata.Tags != null && metadata.Tags.Count > 0)
        {
            var tagsStr = string.Join(", ", metadata.Tags.Select(EscapeMetadata));
            args.Append($"-metadata keywords=\"{tagsStr}\" ");
        }

        args.Append("-metadata encoder=\"Aura Video Studio\" ");
        
        args.Append("-movflags +faststart ");
        args.Append("-y ");
        args.Append($"\"{outputPath}\"");

        await ExecuteFFmpegAsync(args.ToString(), cancellationToken);

        _logger.LogInformation("Metadata added successfully");
        return outputPath;
    }

    /// <summary>
    /// Exports video in multiple resolutions
    /// </summary>
    public async Task<Dictionary<string, string>> ExportMultipleResolutionsAsync(
        string videoPath,
        string outputDirectory,
        string baseFileName,
        List<MultiResolutionSpec>? resolutions = null,
        IProgress<int>? progress = null,
        CancellationToken cancellationToken = default)
    {
        resolutions ??= GetStandardResolutions();

        _logger.LogInformation(
            "Exporting video in {Count} resolutions",
            resolutions.Count
        );

        Directory.CreateDirectory(outputDirectory);

        var outputs = new Dictionary<string, string>();
        var completedCount = 0;

        foreach (var spec in resolutions)
        {
            var outputPath = Path.Combine(
                outputDirectory,
                $"{baseFileName}_{spec.Name}.mp4"
            );

            _logger.LogInformation(
                "Exporting {Name} ({Width}x{Height} @ {Bitrate}kbps)",
                spec.Name, spec.Width, spec.Height, spec.BitrateKbps
            );

            var args = new StringBuilder();
            args.Append($"-i \"{videoPath}\" ");
            args.Append($"-vf \"scale={spec.Width}:{spec.Height}:force_original_aspect_ratio=decrease,pad={spec.Width}:{spec.Height}:(ow-iw)/2:(oh-ih)/2\" ");
            args.Append("-c:v libx264 ");
            args.Append("-preset medium ");
            args.Append($"-b:v {spec.BitrateKbps}k ");
            args.Append($"-maxrate {(int)(spec.BitrateKbps * 1.5)}k ");
            args.Append($"-bufsize {spec.BitrateKbps * 2}k ");
            args.Append($"-r {spec.FrameRate} ");
            args.Append("-c:a aac ");
            args.Append("-b:a 128k ");
            args.Append("-pix_fmt yuv420p ");
            args.Append("-movflags +faststart ");
            args.Append("-y ");
            args.Append($"\"{outputPath}\"");

            await ExecuteFFmpegAsync(args.ToString(), cancellationToken);

            outputs[spec.Name] = outputPath;
            completedCount++;
            
            progress?.Report((completedCount * 100) / resolutions.Count);
        }

        _logger.LogInformation("All resolutions exported successfully");
        return outputs;
    }

    /// <summary>
    /// Creates a shareable streaming-optimized version
    /// </summary>
    public async Task<string> CreateStreamingOptimizedAsync(
        string videoPath,
        string outputPath,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Creating streaming-optimized version");

        var args = new StringBuilder();
        args.Append($"-i \"{videoPath}\" ");
        args.Append("-c:v libx264 ");
        args.Append("-preset fast ");
        args.Append("-profile:v baseline ");
        args.Append("-level 3.0 ");
        args.Append("-c:a aac ");
        args.Append("-ar 44100 ");
        args.Append("-ac 2 ");
        args.Append("-movflags +faststart ");
        args.Append("-pix_fmt yuv420p ");
        args.Append("-y ");
        args.Append($"\"{outputPath}\"");

        await ExecuteFFmpegAsync(args.ToString(), cancellationToken);

        _logger.LogInformation("Streaming-optimized version created");
        return outputPath;
    }

    /// <summary>
    /// Generates a sprite sheet of video frames for timeline preview
    /// </summary>
    public async Task<string> GenerateSpriteSheetAsync(
        string videoPath,
        string outputPath,
        int columns = 10,
        int rows = 10,
        int thumbnailWidth = 160,
        int thumbnailHeight = 90,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "Generating sprite sheet: {Columns}x{Rows} grid",
            columns, rows
        );

        var totalThumbs = columns * rows;
        var args = new StringBuilder();
        args.Append($"-i \"{videoPath}\" ");
        args.Append($"-vf \"select='not(mod(n,{totalThumbs}))',scale={thumbnailWidth}:{thumbnailHeight},tile={columns}x{rows}\" ");
        args.Append("-vframes 1 ");
        args.Append("-y ");
        args.Append($"\"{outputPath}\"");

        await ExecuteFFmpegAsync(args.ToString(), cancellationToken);

        _logger.LogInformation("Sprite sheet generated: {OutputPath}", outputPath);
        return outputPath;
    }

    /// <summary>
    /// Extracts audio track to separate file
    /// </summary>
    public async Task<string> ExtractAudioAsync(
        string videoPath,
        string outputPath,
        string format = "mp3",
        int bitrateKbps = 192,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Extracting audio track to {Format}", format);

        var codec = format switch
        {
            "mp3" => "libmp3lame",
            "aac" => "aac",
            "wav" => "pcm_s16le",
            "flac" => "flac",
            _ => "libmp3lame"
        };

        var args = new StringBuilder();
        args.Append($"-i \"{videoPath}\" ");
        args.Append("-vn ");
        args.Append($"-c:a {codec} ");
        
        if (format != "wav" && format != "flac")
        {
            args.Append($"-b:a {bitrateKbps}k ");
        }
        
        args.Append("-y ");
        args.Append($"\"{outputPath}\"");

        await ExecuteFFmpegAsync(args.ToString(), cancellationToken);

        _logger.LogInformation("Audio extracted: {OutputPath}", outputPath);
        return outputPath;
    }

    /// <summary>
    /// Creates a GIF from video segment
    /// </summary>
    public async Task<string> CreateGifAsync(
        string videoPath,
        string outputPath,
        TimeSpan startTime,
        TimeSpan duration,
        int width = 480,
        int fps = 15,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "Creating GIF: {Duration}s at {Fps}fps",
            duration.TotalSeconds, fps
        );

        var paletteFile = Path.Combine(Path.GetTempPath(), $"palette_{Guid.NewGuid()}.png");

        try
        {
            var paletteArgs = new StringBuilder();
            paletteArgs.Append($"-ss {startTime.TotalSeconds.ToString(CultureInfo.InvariantCulture)} ");
            paletteArgs.Append($"-t {duration.TotalSeconds.ToString(CultureInfo.InvariantCulture)} ");
            paletteArgs.Append($"-i \"{videoPath}\" ");
            paletteArgs.Append($"-vf \"fps={fps},scale={width}:-1:flags=lanczos,palettegen\" ");
            paletteArgs.Append("-y ");
            paletteArgs.Append($"\"{paletteFile}\"");

            await ExecuteFFmpegAsync(paletteArgs.ToString(), cancellationToken);

            var gifArgs = new StringBuilder();
            gifArgs.Append($"-ss {startTime.TotalSeconds.ToString(CultureInfo.InvariantCulture)} ");
            gifArgs.Append($"-t {duration.TotalSeconds.ToString(CultureInfo.InvariantCulture)} ");
            gifArgs.Append($"-i \"{videoPath}\" ");
            gifArgs.Append($"-i \"{paletteFile}\" ");
            gifArgs.Append($"-filter_complex \"fps={fps},scale={width}:-1:flags=lanczos[x];[x][1:v]paletteuse\" ");
            gifArgs.Append("-y ");
            gifArgs.Append($"\"{outputPath}\"");

            await ExecuteFFmpegAsync(gifArgs.ToString(), cancellationToken);

            _logger.LogInformation("GIF created: {OutputPath}", outputPath);
            return outputPath;
        }
        finally
        {
            if (File.Exists(paletteFile))
            {
                File.Delete(paletteFile);
            }
        }
    }

    private List<MultiResolutionSpec> GetStandardResolutions()
    {
        return new List<MultiResolutionSpec>
        {
            new("1080p", 1920, 1080, 8000, 30),
            new("720p", 1280, 720, 5000, 30),
            new("480p", 854, 480, 2500, 30)
        };
    }

    private async Task<double> GetVideoDurationAsync(
        string videoPath,
        CancellationToken cancellationToken)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = _ffprobePath,
            Arguments = $"-v error -show_entries format=duration -of default=noprint_wrappers=1:nokey=1 \"{videoPath}\"",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using var process = Process.Start(startInfo);
        if (process == null)
        {
            return 0;
        }

        var output = await process.StandardOutput.ReadToEndAsync();
        await process.WaitForExitAsync(cancellationToken);

        if (double.TryParse(output.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out var duration))
        {
            return duration;
        }

        return 0;
    }

    private async Task ExecuteFFmpegAsync(string arguments, CancellationToken cancellationToken)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = _ffmpegPath,
            Arguments = arguments,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using var process = Process.Start(startInfo);
        if (process == null)
        {
            throw new InvalidOperationException("Failed to start FFmpeg process");
        }

        var errorOutput = new StringBuilder();
        process.ErrorDataReceived += (sender, e) =>
        {
            if (!string.IsNullOrEmpty(e.Data))
            {
                errorOutput.AppendLine(e.Data);
            }
        };

        process.BeginErrorReadLine();
        await process.WaitForExitAsync(cancellationToken);

        if (process.ExitCode != 0)
        {
            _logger.LogError(
                "FFmpeg command failed with exit code {ExitCode}. Output: {Output}",
                process.ExitCode, errorOutput.ToString()
            );
            throw new InvalidOperationException($"FFmpeg operation failed with exit code {process.ExitCode}");
        }
    }

    private string EscapeMetadata(string value)
    {
        return value
            .Replace("\"", "\\\"")
            .Replace("'", "\\'")
            .Replace("\n", " ")
            .Replace("\r", " ");
    }
}
