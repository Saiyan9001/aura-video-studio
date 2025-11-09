using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Aura.Core.Services.FFmpeg;
using Microsoft.Extensions.Logging;

namespace Aura.Core.Services.AudioIntelligence;

/// <summary>
/// Service for comprehensive audio post-processing
/// Handles noise reduction, compression, normalization, and fade effects
/// </summary>
public class AudioPostProcessingService
{
    private readonly ILogger<AudioPostProcessingService> _logger;
    private readonly IFFmpegService _ffmpegService;

    public AudioPostProcessingService(
        ILogger<AudioPostProcessingService> logger,
        IFFmpegService ffmpegService)
    {
        _logger = logger;
        _ffmpegService = ffmpegService;
    }

    /// <summary>
    /// Apply comprehensive post-processing to audio file
    /// </summary>
    public async Task<string> ProcessAudioAsync(
        string inputPath,
        string outputPath,
        AudioPostProcessingConfig config,
        CancellationToken ct = default)
    {
        _logger.LogInformation("Starting audio post-processing: {Input}", inputPath);

        if (!File.Exists(inputPath))
            throw new FileNotFoundException($"Input audio file not found: {inputPath}");

        var filters = BuildFilterChain(config);

        if (filters.Count == 0)
        {
            _logger.LogInformation("No processing configured, copying input to output");
            File.Copy(inputPath, outputPath, true);
            return outputPath;
        }

        var filterString = string.Join(",", filters);
        var arguments = $"-i \"{inputPath}\" -af \"{filterString}\" -ar 48000 -ac 2 \"{outputPath}\"";

        var result = await _ffmpegService.ExecuteAsync(arguments, null, ct);

        if (!result.Success)
        {
            throw new InvalidOperationException(
                $"Audio post-processing failed: {result.ErrorMessage ?? "Unknown error"}");
        }

        _logger.LogInformation("Audio post-processing completed: {Output}", outputPath);
        return outputPath;
    }

    /// <summary>
    /// Remove background noise from audio
    /// </summary>
    public async Task<string> RemoveNoiseAsync(
        string inputPath,
        string outputPath,
        double noiseReduction = 0.21,
        CancellationToken ct = default)
    {
        _logger.LogInformation("Removing noise from audio: {Input}", inputPath);

        if (!File.Exists(inputPath))
            throw new FileNotFoundException($"Input audio file not found: {inputPath}");

        var filter = $"afftdn=nr={noiseReduction}:nf=-25";

        var arguments = $"-i \"{inputPath}\" -af \"{filter}\" -ar 48000 -ac 2 \"{outputPath}\"";

        var result = await _ffmpegService.ExecuteAsync(arguments, null, ct);

        if (!result.Success)
        {
            throw new InvalidOperationException(
                $"Noise removal failed: {result.ErrorMessage ?? "Unknown error"}");
        }

        _logger.LogInformation("Noise removal completed: {Output}", outputPath);
        return outputPath;
    }

    /// <summary>
    /// Apply dynamic range compression for consistent loudness
    /// </summary>
    public async Task<string> ApplyCompressionAsync(
        string inputPath,
        string outputPath,
        CompressionConfig config,
        CancellationToken ct = default)
    {
        _logger.LogInformation("Applying compression to audio: {Input}", inputPath);

        if (!File.Exists(inputPath))
            throw new FileNotFoundException($"Input audio file not found: {inputPath}");

        var filter = $"acompressor=threshold={config.ThresholdDb}dB:" +
                    $"ratio={config.Ratio}:" +
                    $"attack={config.AttackMs}:" +
                    $"release={config.ReleaseMs}:" +
                    $"makeup={config.MakeupGainDb}dB:" +
                    $"knee=2.828427:" +
                    $"link=average:" +
                    $"detection=rms";

        var arguments = $"-i \"{inputPath}\" -af \"{filter}\" -ar 48000 -ac 2 \"{outputPath}\"";

        var result = await _ffmpegService.ExecuteAsync(arguments, null, ct);

        if (!result.Success)
        {
            throw new InvalidOperationException(
                $"Compression failed: {result.ErrorMessage ?? "Unknown error"}");
        }

        _logger.LogInformation("Compression completed: {Output}", outputPath);
        return outputPath;
    }

    /// <summary>
    /// Trim silence from beginning and end of audio
    /// </summary>
    public async Task<string> TrimSilenceAsync(
        string inputPath,
        string outputPath,
        double thresholdDb = -50.0,
        double durationSeconds = 0.5,
        CancellationToken ct = default)
    {
        _logger.LogInformation("Trimming silence from audio: {Input}", inputPath);

        if (!File.Exists(inputPath))
            throw new FileNotFoundException($"Input audio file not found: {inputPath}");

        var filter = $"silenceremove=start_periods=1:start_duration={durationSeconds}:start_threshold={thresholdDb}dB:" +
                    $"stop_periods=-1:stop_duration={durationSeconds}:stop_threshold={thresholdDb}dB";

        var arguments = $"-i \"{inputPath}\" -af \"{filter}\" -ar 48000 -ac 2 \"{outputPath}\"";

        var result = await _ffmpegService.ExecuteAsync(arguments, null, ct);

        if (!result.Success)
        {
            throw new InvalidOperationException(
                $"Silence trimming failed: {result.ErrorMessage ?? "Unknown error"}");
        }

        _logger.LogInformation("Silence trimming completed: {Output}", outputPath);
        return outputPath;
    }

    /// <summary>
    /// Add fade in/out effects
    /// </summary>
    public async Task<string> AddFadesAsync(
        string inputPath,
        string outputPath,
        double fadeInSeconds = 0.1,
        double fadeOutSeconds = 0.3,
        CancellationToken ct = default)
    {
        _logger.LogInformation("Adding fades to audio: {Input}", inputPath);

        if (!File.Exists(inputPath))
            throw new FileNotFoundException($"Input audio file not found: {inputPath}");

        var filter = $"afade=t=in:st=0:d={fadeInSeconds},afade=t=out:st=-1:d={fadeOutSeconds}";

        var arguments = $"-i \"{inputPath}\" -af \"{filter}\" -ar 48000 -ac 2 \"{outputPath}\"";

        var result = await _ffmpegService.ExecuteAsync(arguments, null, ct);

        if (!result.Success)
        {
            throw new InvalidOperationException(
                $"Fade effects failed: {result.ErrorMessage ?? "Unknown error"}");
        }

        _logger.LogInformation("Fade effects completed: {Output}", outputPath);
        return outputPath;
    }

    /// <summary>
    /// Add padding silence at beginning and/or end
    /// </summary>
    public async Task<string> AddPaddingAsync(
        string inputPath,
        string outputPath,
        double startPaddingSeconds = 0.0,
        double endPaddingSeconds = 0.0,
        CancellationToken ct = default)
    {
        _logger.LogInformation("Adding padding to audio: {Input}", inputPath);

        if (!File.Exists(inputPath))
            throw new FileNotFoundException($"Input audio file not found: {inputPath}");

        var filter = $"adelay={startPaddingSeconds * 1000}|{startPaddingSeconds * 1000}," +
                    $"apad=pad_dur={endPaddingSeconds}";

        var arguments = $"-i \"{inputPath}\" -af \"{filter}\" -ar 48000 -ac 2 \"{outputPath}\"";

        var result = await _ffmpegService.ExecuteAsync(arguments, null, ct);

        if (!result.Success)
        {
            throw new InvalidOperationException(
                $"Padding failed: {result.ErrorMessage ?? "Unknown error"}");
        }

        _logger.LogInformation("Padding completed: {Output}", outputPath);
        return outputPath;
    }

    /// <summary>
    /// Apply equalization for voice clarity
    /// </summary>
    public async Task<string> ApplyEqualizationAsync(
        string inputPath,
        string outputPath,
        EqualizationConfig config,
        CancellationToken ct = default)
    {
        _logger.LogInformation("Applying equalization to audio: {Input}", inputPath);

        if (!File.Exists(inputPath))
            throw new FileNotFoundException($"Input audio file not found: {inputPath}");

        var filters = new List<string>();

        if (config.HighPassFrequency > 0)
        {
            filters.Add($"highpass=f={config.HighPassFrequency}");
        }

        if (Math.Abs(config.PresenceBoost) > 0.01)
        {
            filters.Add($"equalizer=f=4000:t=h:width=2000:g={config.PresenceBoost}");
        }

        if (Math.Abs(config.DeEsserReduction) > 0.01)
        {
            filters.Add($"equalizer=f=7000:t=h:width=2000:g={config.DeEsserReduction}");
        }

        if (config.LowPassFrequency > 0)
        {
            filters.Add($"lowpass=f={config.LowPassFrequency}");
        }

        if (filters.Count == 0)
        {
            File.Copy(inputPath, outputPath, true);
            return outputPath;
        }

        var filterString = string.Join(",", filters);
        var arguments = $"-i \"{inputPath}\" -af \"{filterString}\" -ar 48000 -ac 2 \"{outputPath}\"";

        var result = await _ffmpegService.ExecuteAsync(arguments, null, ct);

        if (!result.Success)
        {
            throw new InvalidOperationException(
                $"Equalization failed: {result.ErrorMessage ?? "Unknown error"}");
        }

        _logger.LogInformation("Equalization completed: {Output}", outputPath);
        return outputPath;
    }

    private List<string> BuildFilterChain(AudioPostProcessingConfig config)
    {
        var filters = new List<string>();

        if (config.RemoveNoise && config.NoiseReductionStrength > 0)
        {
            filters.Add($"afftdn=nr={config.NoiseReductionStrength}:nf=-25");
        }

        if (config.ApplyCompression && config.Compression != null)
        {
            var comp = config.Compression;
            filters.Add($"acompressor=threshold={comp.ThresholdDb}dB:" +
                       $"ratio={comp.Ratio}:" +
                       $"attack={comp.AttackMs}:" +
                       $"release={comp.ReleaseMs}:" +
                       $"makeup={comp.MakeupGainDb}dB:" +
                       $"knee=2.828427");
        }

        if (config.ApplyEqualization && config.Equalization != null)
        {
            var eq = config.Equalization;
            
            if (eq.HighPassFrequency > 0)
            {
                filters.Add($"highpass=f={eq.HighPassFrequency}");
            }

            if (Math.Abs(eq.PresenceBoost) > 0.01)
            {
                filters.Add($"equalizer=f=4000:t=h:width=2000:g={eq.PresenceBoost}");
            }

            if (Math.Abs(eq.DeEsserReduction) > 0.01)
            {
                filters.Add($"equalizer=f=7000:t=h:width=2000:g={eq.DeEsserReduction}");
            }
        }

        if (config.TrimSilence)
        {
            filters.Add($"silenceremove=start_periods=1:start_duration=0.5:start_threshold=-50dB:" +
                       $"stop_periods=-1:stop_duration=0.5:stop_threshold=-50dB");
        }

        if (config.AddFades)
        {
            filters.Add($"afade=t=in:st=0:d={config.FadeInSeconds}");
            filters.Add($"afade=t=out:st=-1:d={config.FadeOutSeconds}");
        }

        if (config.Normalize)
        {
            filters.Add($"loudnorm=I={config.TargetLUFS}:TP=-1.5:LRA=11");
        }

        return filters;
    }
}

/// <summary>
/// Configuration for audio post-processing
/// </summary>
public record AudioPostProcessingConfig
{
    public bool RemoveNoise { get; init; } = true;
    public double NoiseReductionStrength { get; init; } = 0.21;
    
    public bool ApplyCompression { get; init; } = true;
    public CompressionConfig? Compression { get; init; } = new()
    {
        ThresholdDb = -20.0,
        Ratio = 3.0,
        AttackMs = 20,
        ReleaseMs = 250,
        MakeupGainDb = 2.0
    };
    
    public bool ApplyEqualization { get; init; } = true;
    public EqualizationConfig? Equalization { get; init; } = new()
    {
        HighPassFrequency = 80,
        PresenceBoost = 3.0,
        DeEsserReduction = -4.0,
        LowPassFrequency = 0
    };
    
    public bool TrimSilence { get; init; } = true;
    
    public bool AddFades { get; init; } = true;
    public double FadeInSeconds { get; init; } = 0.1;
    public double FadeOutSeconds { get; init; } = 0.3;
    
    public bool Normalize { get; init; } = true;
    public double TargetLUFS { get; init; } = -14.0;
}

/// <summary>
/// Compression configuration
/// </summary>
public record CompressionConfig
{
    public double ThresholdDb { get; init; } = -20.0;
    public double Ratio { get; init; } = 3.0;
    public double AttackMs { get; init; } = 20;
    public double ReleaseMs { get; init; } = 250;
    public double MakeupGainDb { get; init; } = 2.0;
}

/// <summary>
/// Equalization configuration
/// </summary>
public record EqualizationConfig
{
    public double HighPassFrequency { get; init; } = 80;
    public double PresenceBoost { get; init; } = 3.0;
    public double DeEsserReduction { get; init; } = -4.0;
    public double LowPassFrequency { get; init; } = 0;
}
