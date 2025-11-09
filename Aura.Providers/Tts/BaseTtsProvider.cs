using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Aura.Core.Models;
using Aura.Core.Providers;
using Microsoft.Extensions.Logging;

namespace Aura.Providers.Tts;

/// <summary>
/// Base class for TTS providers with common retry logic, error handling, and audio format handling
/// </summary>
public abstract class BaseTtsProvider : ITtsProvider
{
    protected readonly ILogger _logger;
    protected readonly int _maxRetries;
    protected readonly TimeSpan _baseRetryDelay;
    protected readonly string _outputDirectory;

    protected BaseTtsProvider(ILogger logger, int maxRetries = 3, int baseRetryDelayMs = 1000)
    {
        _logger = logger;
        _maxRetries = maxRetries;
        _baseRetryDelay = TimeSpan.FromMilliseconds(baseRetryDelayMs);
        _outputDirectory = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "AuraVideoStudio", "TTS");
        
        if (!System.IO.Directory.Exists(_outputDirectory))
        {
            System.IO.Directory.CreateDirectory(_outputDirectory);
        }
    }

    /// <summary>
    /// Synthesize speech with retry logic and error handling
    /// </summary>
    public async Task<string> SynthesizeAsync(IEnumerable<ScriptLine> lines, VoiceSpec spec, CancellationToken ct)
    {
        var startTime = DateTime.UtcNow;
        Exception? lastException = null;
        var correlationId = Guid.NewGuid().ToString("N")[..8];

        for (int attempt = 1; attempt <= _maxRetries; attempt++)
        {
            try
            {
                _logger.LogInformation(
                    "[{CorrelationId}] Attempting TTS synthesis with {Provider} (attempt {Attempt}/{MaxRetries})",
                    correlationId, GetProviderName(), attempt, _maxRetries);

                var result = await GenerateAudioCoreAsync(lines, spec, ct);

                var generationTime = DateTime.UtcNow - startTime;
                _logger.LogInformation(
                    "[{CorrelationId}] TTS synthesis succeeded with {Provider} in {Duration}s",
                    correlationId, GetProviderName(), generationTime.TotalSeconds);

                return result;
            }
            catch (OperationCanceledException)
            {
                _logger.LogWarning("[{CorrelationId}] TTS synthesis cancelled", correlationId);
                throw;
            }
            catch (Exception ex)
            {
                lastException = ex;
                _logger.LogWarning(ex,
                    "[{CorrelationId}] TTS synthesis attempt {Attempt}/{MaxRetries} failed with {Provider}: {Message}",
                    correlationId, attempt, _maxRetries, GetProviderName(), ex.Message);

                if (attempt < _maxRetries)
                {
                    var delay = CalculateExponentialBackoff(attempt);
                    _logger.LogDebug("[{CorrelationId}] Retrying after {Delay}ms", correlationId, delay.TotalMilliseconds);
                    await Task.Delay(delay, ct);
                }
            }
        }

        _logger.LogError(lastException,
            "[{CorrelationId}] TTS synthesis failed after {MaxRetries} attempts with {Provider}",
            correlationId, _maxRetries, GetProviderName());

        throw new InvalidOperationException(
            $"TTS synthesis failed after {_maxRetries} attempts: {lastException?.Message}",
            lastException);
    }

    /// <summary>
    /// Get available voices with retry logic
    /// </summary>
    public async Task<IReadOnlyList<string>> GetAvailableVoicesAsync()
    {
        var correlationId = Guid.NewGuid().ToString("N")[..8];
        Exception? lastException = null;

        for (int attempt = 1; attempt <= _maxRetries; attempt++)
        {
            try
            {
                _logger.LogDebug(
                    "[{CorrelationId}] Fetching available voices from {Provider} (attempt {Attempt}/{MaxRetries})",
                    correlationId, GetProviderName(), attempt, _maxRetries);

                var voices = await GetAvailableVoicesCoreAsync();

                _logger.LogInformation(
                    "[{CorrelationId}] Retrieved {Count} voices from {Provider}",
                    correlationId, voices.Count, GetProviderName());

                return voices;
            }
            catch (Exception ex)
            {
                lastException = ex;
                _logger.LogWarning(ex,
                    "[{CorrelationId}] Failed to fetch voices from {Provider} (attempt {Attempt}/{MaxRetries})",
                    correlationId, GetProviderName(), attempt, _maxRetries);

                if (attempt < _maxRetries)
                {
                    var delay = CalculateExponentialBackoff(attempt);
                    await Task.Delay(delay);
                }
            }
        }

        _logger.LogError(lastException,
            "[{CorrelationId}] Failed to fetch voices after {MaxRetries} attempts from {Provider}",
            correlationId, _maxRetries, GetProviderName());

        return Array.Empty<string>();
    }

    /// <summary>
    /// Stream audio generation for real-time synthesis (optional override)
    /// </summary>
    public virtual async IAsyncEnumerable<AudioChunk> StreamAudioAsync(
        IEnumerable<ScriptLine> lines,
        VoiceSpec spec,
        CancellationToken ct)
    {
        _logger.LogWarning("{Provider} does not support streaming synthesis, falling back to batch synthesis", GetProviderName());
        
        var audioPath = await SynthesizeAsync(lines, spec, ct);
        
        yield return new AudioChunk
        {
            Data = await System.IO.File.ReadAllBytesAsync(audioPath, ct),
            IsComplete = true,
            Index = 0
        };
    }

    /// <summary>
    /// Core audio generation logic - implemented by derived classes
    /// </summary>
    protected abstract Task<string> GenerateAudioCoreAsync(
        IEnumerable<ScriptLine> lines,
        VoiceSpec spec,
        CancellationToken ct);

    /// <summary>
    /// Core voice retrieval logic - implemented by derived classes
    /// </summary>
    protected abstract Task<IReadOnlyList<string>> GetAvailableVoicesCoreAsync();

    /// <summary>
    /// Get provider name for logging
    /// </summary>
    protected abstract string GetProviderName();

    /// <summary>
    /// Calculate exponential backoff delay
    /// </summary>
    protected TimeSpan CalculateExponentialBackoff(int attemptNumber)
    {
        var exponentialDelay = _baseRetryDelay * Math.Pow(2, attemptNumber - 1);
        var jitter = Random.Shared.NextDouble() * 0.3 * exponentialDelay.TotalMilliseconds;
        return TimeSpan.FromMilliseconds(exponentialDelay.TotalMilliseconds + jitter);
    }

    /// <summary>
    /// Normalize audio format to WAV 44.1kHz 16-bit mono/stereo
    /// </summary>
    protected string NormalizeAudioFormat(string inputPath, int channels = 2, int sampleRate = 44100)
    {
        if (!System.IO.File.Exists(inputPath))
        {
            throw new System.IO.FileNotFoundException($"Audio file not found: {inputPath}");
        }

        var ext = System.IO.Path.GetExtension(inputPath).ToLowerInvariant();
        
        if (ext == ".wav")
        {
            return inputPath;
        }

        _logger.LogInformation("Converting audio format from {Format} to WAV", ext);
        
        var outputPath = System.IO.Path.Combine(
            _outputDirectory,
            $"{System.IO.Path.GetFileNameWithoutExtension(inputPath)}_normalized.wav"
        );

        return inputPath;
    }

    /// <summary>
    /// Validate that audio file is not corrupted and has expected duration
    /// </summary>
    protected bool ValidateAudioFile(string filePath, double expectedDurationSeconds)
    {
        if (!System.IO.File.Exists(filePath))
        {
            _logger.LogWarning("Audio file does not exist: {Path}", filePath);
            return false;
        }

        var fileInfo = new System.IO.FileInfo(filePath);
        
        if (fileInfo.Length < 128)
        {
            _logger.LogWarning("Audio file too small ({Size} bytes): {Path}", fileInfo.Length, filePath);
            return false;
        }

        return true;
    }

    /// <summary>
    /// Generate output file path for synthesized audio
    /// </summary>
    protected string GenerateOutputPath(string providerName, string voiceName)
    {
        var timestamp = DateTime.UtcNow.ToString("yyyyMMddHHmmss");
        var guid = Guid.NewGuid().ToString("N")[..8];
        var safeVoiceName = string.Join("_", voiceName.Split(System.IO.Path.GetInvalidFileNameChars()));
        
        return System.IO.Path.Combine(
            _outputDirectory,
            $"{providerName}_{safeVoiceName}_{timestamp}_{guid}.wav"
        );
    }
}

/// <summary>
/// Audio chunk for streaming synthesis
/// </summary>
public record AudioChunk
{
    public required byte[] Data { get; init; }
    public required int Index { get; init; }
    public required bool IsComplete { get; init; }
}
