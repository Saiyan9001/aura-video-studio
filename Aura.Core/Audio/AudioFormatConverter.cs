using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Aura.Core.Dependencies;
using Microsoft.Extensions.Logging;

namespace Aura.Core.Audio;

/// <summary>
/// Service for converting audio between different formats and normalizing audio properties
/// Handles audio from different TTS providers with varying formats, sample rates, and bit depths
/// </summary>
public class AudioFormatConverter
{
    private readonly ILogger<AudioFormatConverter> _logger;
    private readonly IFfmpegLocator _ffmpegLocator;

    public AudioFormatConverter(ILogger<AudioFormatConverter> logger, IFfmpegLocator ffmpegLocator)
    {
        _logger = logger;
        _ffmpegLocator = ffmpegLocator;
    }

    /// <summary>
    /// Converts audio file to WAV format with standardized properties
    /// </summary>
    /// <param name="inputPath">Path to input audio file (mp3, wav, ogg, etc.)</param>
    /// <param name="outputPath">Path for output WAV file (if null, generates one)</param>
    /// <param name="sampleRate">Target sample rate (default: 44100 Hz)</param>
    /// <param name="channels">Target channel count (default: 2 for stereo)</param>
    /// <param name="bitDepth">Target bit depth (default: 16)</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Path to converted audio file</returns>
    public async Task<string> ConvertToWavAsync(
        string inputPath,
        string? outputPath = null,
        int sampleRate = 44100,
        int channels = 2,
        int bitDepth = 16,
        CancellationToken ct = default)
    {
        var correlationId = Guid.NewGuid().ToString("N")[..8];

        if (!File.Exists(inputPath))
        {
            throw new FileNotFoundException($"Input audio file not found: {inputPath}");
        }

        outputPath ??= Path.Combine(
            Path.GetTempPath(),
            "AuraVideoStudio",
            "Audio",
            $"converted_{Path.GetFileNameWithoutExtension(inputPath)}_{correlationId}.wav"
        );

        var outputDir = Path.GetDirectoryName(outputPath);
        if (!string.IsNullOrEmpty(outputDir) && !Directory.Exists(outputDir))
        {
            Directory.CreateDirectory(outputDir);
        }

        var inputExt = Path.GetExtension(inputPath).ToLowerInvariant();

        if (inputExt == ".wav")
        {
            var needsConversion = await NeedsConversionAsync(inputPath, sampleRate, channels, bitDepth, ct);
            
            if (!needsConversion)
            {
                _logger.LogInformation("[{CorrelationId}] Audio already in target format, no conversion needed", correlationId);
                return inputPath;
            }
        }

        _logger.LogInformation(
            "[{CorrelationId}] Converting audio from {InputFormat} to WAV ({SampleRate}Hz, {Channels}ch, {BitDepth}bit)",
            correlationId, inputExt, sampleRate, channels, bitDepth);

        var ffmpegPath = await _ffmpegLocator.GetEffectiveFfmpegPathAsync(ct: ct);
        
        if (string.IsNullOrEmpty(ffmpegPath))
        {
            throw new InvalidOperationException("FFmpeg not found. Please install FFmpeg or configure its path.");
        }

        var args = BuildConversionArgs(inputPath, outputPath, sampleRate, channels, bitDepth);

        var startInfo = new System.Diagnostics.ProcessStartInfo
        {
            FileName = ffmpegPath,
            Arguments = args,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using var process = System.Diagnostics.Process.Start(startInfo);
        
        if (process == null)
        {
            throw new InvalidOperationException("Failed to start FFmpeg process");
        }

        var outputTask = process.StandardOutput.ReadToEndAsync(ct);
        var errorTask = process.StandardError.ReadToEndAsync(ct);

        await process.WaitForExitAsync(ct);

        var output = await outputTask;
        var error = await errorTask;

        if (process.ExitCode != 0)
        {
            _logger.LogError("[{CorrelationId}] FFmpeg conversion failed: {Error}", correlationId, error);
            throw new InvalidOperationException($"Audio conversion failed with exit code {process.ExitCode}: {error}");
        }

        if (!File.Exists(outputPath))
        {
            throw new InvalidOperationException($"Conversion succeeded but output file not found: {outputPath}");
        }

        var fileInfo = new FileInfo(outputPath);
        _logger.LogInformation(
            "[{CorrelationId}] Audio conversion completed: {OutputPath} ({Size} bytes)",
            correlationId, outputPath, fileInfo.Length);

        return outputPath;
    }

    /// <summary>
    /// Normalizes audio volume to target LUFS level
    /// </summary>
    public async Task<string> NormalizeVolumeAsync(
        string inputPath,
        string? outputPath = null,
        double targetLufs = -16.0,
        CancellationToken ct = default)
    {
        var correlationId = Guid.NewGuid().ToString("N")[..8];

        if (!File.Exists(inputPath))
        {
            throw new FileNotFoundException($"Input audio file not found: {inputPath}");
        }

        outputPath ??= Path.Combine(
            Path.GetTempPath(),
            "AuraVideoStudio",
            "Audio",
            $"normalized_{Path.GetFileNameWithoutExtension(inputPath)}_{correlationId}.wav"
        );

        var outputDir = Path.GetDirectoryName(outputPath);
        if (!string.IsNullOrEmpty(outputDir) && !Directory.Exists(outputDir))
        {
            Directory.CreateDirectory(outputDir);
        }

        _logger.LogInformation(
            "[{CorrelationId}] Normalizing audio volume to {TargetLufs} LUFS",
            correlationId, targetLufs);

        var ffmpegPath = await _ffmpegLocator.GetEffectiveFfmpegPathAsync(ct: ct);
        
        if (string.IsNullOrEmpty(ffmpegPath))
        {
            throw new InvalidOperationException("FFmpeg not found. Please install FFmpeg or configure its path.");
        }

        var args = $"-i \"{inputPath}\" -af \"loudnorm=I={targetLufs}:TP=-1.5:LRA=11\" -ar 44100 -y \"{outputPath}\"";

        var startInfo = new System.Diagnostics.ProcessStartInfo
        {
            FileName = ffmpegPath,
            Arguments = args,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using var process = System.Diagnostics.Process.Start(startInfo);
        
        if (process == null)
        {
            throw new InvalidOperationException("Failed to start FFmpeg process");
        }

        await process.WaitForExitAsync(ct);

        if (process.ExitCode != 0)
        {
            var error = await process.StandardError.ReadToEndAsync(ct);
            _logger.LogError("[{CorrelationId}] FFmpeg normalization failed: {Error}", correlationId, error);
            throw new InvalidOperationException($"Audio normalization failed with exit code {process.ExitCode}");
        }

        _logger.LogInformation("[{CorrelationId}] Audio normalization completed: {OutputPath}", correlationId, outputPath);

        return outputPath;
    }

    /// <summary>
    /// Checks if audio file needs conversion to match target properties
    /// </summary>
    private async Task<bool> NeedsConversionAsync(
        string filePath,
        int targetSampleRate,
        int targetChannels,
        int targetBitDepth,
        CancellationToken ct)
    {
        try
        {
            var ffprobePath = Path.Combine(
                Path.GetDirectoryName(await _ffmpegLocator.GetEffectiveFfmpegPathAsync(ct: ct)) ?? string.Empty,
                "ffprobe");

            if (!File.Exists(ffprobePath))
            {
                return true;
            }

            var args = $"-v error -select_streams a:0 -show_entries stream=sample_rate,channels,bits_per_sample -of default=noprint_wrappers=1 \"{filePath}\"";

            var startInfo = new System.Diagnostics.ProcessStartInfo
            {
                FileName = ffprobePath,
                Arguments = args,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process = System.Diagnostics.Process.Start(startInfo);
            
            if (process == null)
            {
                return true;
            }

            var output = await process.StandardOutput.ReadToEndAsync(ct);
            await process.WaitForExitAsync(ct);

            if (process.ExitCode != 0)
            {
                return true;
            }

            var lines = output.Split('\n', StringSplitOptions.RemoveEmptyEntries);
            int? sampleRate = null;
            int? channels = null;
            int? bitDepth = null;

            foreach (var line in lines)
            {
                var parts = line.Split('=');
                if (parts.Length == 2)
                {
                    var key = parts[0].Trim();
                    var value = parts[1].Trim();

                    if (key == "sample_rate" && int.TryParse(value, out var sr))
                    {
                        sampleRate = sr;
                    }
                    else if (key == "channels" && int.TryParse(value, out var ch))
                    {
                        channels = ch;
                    }
                    else if (key == "bits_per_sample" && int.TryParse(value, out var bd))
                    {
                        bitDepth = bd;
                    }
                }
            }

            return sampleRate != targetSampleRate ||
                   channels != targetChannels ||
                   bitDepth != targetBitDepth;
        }
        catch
        {
            return true;
        }
    }

    /// <summary>
    /// Builds FFmpeg arguments for audio conversion
    /// </summary>
    private static string BuildConversionArgs(
        string inputPath,
        string outputPath,
        int sampleRate,
        int channels,
        int bitDepth)
    {
        var sampleFormat = bitDepth switch
        {
            8 => "u8",
            16 => "s16",
            24 => "s24",
            32 => "s32",
            _ => "s16"
        };

        return $"-i \"{inputPath}\" -ar {sampleRate} -ac {channels} -sample_fmt {sampleFormat} -y \"{outputPath}\"";
    }

    /// <summary>
    /// Gets audio file information
    /// </summary>
    public async Task<AudioFileInfo?> GetAudioInfoAsync(string filePath, CancellationToken ct = default)
    {
        if (!File.Exists(filePath))
        {
            return null;
        }

        try
        {
            var ffprobePath = Path.Combine(
                Path.GetDirectoryName(await _ffmpegLocator.GetEffectiveFfmpegPathAsync(ct: ct)) ?? string.Empty,
                "ffprobe");

            if (!File.Exists(ffprobePath))
            {
                return null;
            }

            var args = $"-v error -show_entries format=duration:stream=sample_rate,channels,codec_name -of default=noprint_wrappers=1 \"{filePath}\"";

            var startInfo = new System.Diagnostics.ProcessStartInfo
            {
                FileName = ffprobePath,
                Arguments = args,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process = System.Diagnostics.Process.Start(startInfo);
            
            if (process == null)
            {
                return null;
            }

            var output = await process.StandardOutput.ReadToEndAsync(ct);
            await process.WaitForExitAsync(ct);

            if (process.ExitCode != 0)
            {
                return null;
            }

            var info = new AudioFileInfo { FilePath = filePath };
            var lines = output.Split('\n', StringSplitOptions.RemoveEmptyEntries);

            foreach (var line in lines)
            {
                var parts = line.Split('=');
                if (parts.Length == 2)
                {
                    var key = parts[0].Trim();
                    var value = parts[1].Trim();

                    switch (key)
                    {
                        case "duration" when double.TryParse(value, out var dur):
                            info.Duration = TimeSpan.FromSeconds(dur);
                            break;
                        case "sample_rate" when int.TryParse(value, out var sr):
                            info.SampleRate = sr;
                            break;
                        case "channels" when int.TryParse(value, out var ch):
                            info.Channels = ch;
                            break;
                        case "codec_name":
                            info.Codec = value;
                            break;
                    }
                }
            }

            return info;
        }
        catch
        {
            return null;
        }
    }
}

/// <summary>
/// Audio file information
/// </summary>
public record AudioFileInfo
{
    public required string FilePath { get; init; }
    public TimeSpan Duration { get; set; }
    public int SampleRate { get; set; }
    public int Channels { get; set; }
    public string Codec { get; set; } = string.Empty;
}
