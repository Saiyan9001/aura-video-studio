using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace Aura.Providers.Validation;

/// <summary>
/// Validator for Azure OpenAI Service connectivity
/// </summary>
public class AzureOpenAiValidator : IProviderValidator
{
    private readonly ILogger<AzureOpenAiValidator> _logger;
    private readonly IKeyStore _keyStore;

    public string ProviderName => "Azure";

    public AzureOpenAiValidator(ILogger<AzureOpenAiValidator> logger, IKeyStore keyStore)
    {
        _logger = logger;
        _keyStore = keyStore;
    }

    public async Task<ValidationResult> ValidateAsync(CancellationToken ct = default)
    {
        var sw = Stopwatch.StartNew();
        
        try
        {
            var apiKey = await _keyStore.GetKeyAsync("azure");
            if (string.IsNullOrEmpty(apiKey))
            {
                sw.Stop();
                return ValidationResult.Failure(ProviderName, "No API key configured", sw.ElapsedMilliseconds);
            }

            // Azure OpenAI requires endpoint configuration which we don't have yet
            sw.Stop();
            return ValidationResult.Failure(ProviderName, "Azure OpenAI validation not fully implemented", sw.ElapsedMilliseconds);
        }
        catch (Exception ex)
        {
            sw.Stop();
            _logger.LogError(ex, "Error validating Azure OpenAI");
            return ValidationResult.Failure(ProviderName, $"Error: {ex.Message}", sw.ElapsedMilliseconds);
        }
    }
}

/// <summary>
/// Validator for Google Gemini API connectivity
/// </summary>
public class GeminiValidator : IProviderValidator
{
    private readonly ILogger<GeminiValidator> _logger;
    private readonly IKeyStore _keyStore;

    public string ProviderName => "Gemini";

    public GeminiValidator(ILogger<GeminiValidator> logger, IKeyStore keyStore)
    {
        _logger = logger;
        _keyStore = keyStore;
    }

    public async Task<ValidationResult> ValidateAsync(CancellationToken ct = default)
    {
        var sw = Stopwatch.StartNew();
        
        try
        {
            var apiKey = await _keyStore.GetKeyAsync("gemini");
            if (string.IsNullOrEmpty(apiKey))
            {
                sw.Stop();
                return ValidationResult.Failure(ProviderName, "No API key configured", sw.ElapsedMilliseconds);
            }

            // Gemini validation not fully implemented yet
            sw.Stop();
            return ValidationResult.Failure(ProviderName, "Gemini validation not fully implemented", sw.ElapsedMilliseconds);
        }
        catch (Exception ex)
        {
            sw.Stop();
            _logger.LogError(ex, "Error validating Gemini");
            return ValidationResult.Failure(ProviderName, $"Error: {ex.Message}", sw.ElapsedMilliseconds);
        }
    }
}

/// <summary>
/// Validator for PlayHT TTS API connectivity
/// </summary>
public class PlayHtValidator : IProviderValidator
{
    private readonly ILogger<PlayHtValidator> _logger;
    private readonly IKeyStore _keyStore;

    public string ProviderName => "PlayHT";

    public PlayHtValidator(ILogger<PlayHtValidator> logger, IKeyStore keyStore)
    {
        _logger = logger;
        _keyStore = keyStore;
    }

    public async Task<ValidationResult> ValidateAsync(CancellationToken ct = default)
    {
        var sw = Stopwatch.StartNew();
        
        try
        {
            var apiKey = await _keyStore.GetKeyAsync("playht");
            if (string.IsNullOrEmpty(apiKey))
            {
                sw.Stop();
                return ValidationResult.Failure(ProviderName, "No API key configured", sw.ElapsedMilliseconds);
            }

            // PlayHT validation not fully implemented yet
            sw.Stop();
            return ValidationResult.Failure(ProviderName, "PlayHT validation not fully implemented", sw.ElapsedMilliseconds);
        }
        catch (Exception ex)
        {
            sw.Stop();
            _logger.LogError(ex, "Error validating PlayHT");
            return ValidationResult.Failure(ProviderName, $"Error: {ex.Message}", sw.ElapsedMilliseconds);
        }
    }
}
