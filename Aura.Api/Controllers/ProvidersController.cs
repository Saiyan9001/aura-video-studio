using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Aura.Core.Configuration;
using Aura.Core.Hardware;
using Aura.Providers.Validation;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace Aura.Api.Controllers;

[ApiController]
[Route("api/providers")]
public class ProvidersController : ControllerBase
{
    private readonly ILogger<ProvidersController> _logger;
    private readonly IKeyStore _keyStore;
    private readonly ProviderSettings _providerSettings;
    private readonly HardwareDetector _hardwareDetector;
    private readonly IHttpClientFactory _httpClientFactory;

    public ProvidersController(
        ILogger<ProvidersController> logger,
        IKeyStore keyStore,
        ProviderSettings providerSettings,
        HardwareDetector hardwareDetector,
        IHttpClientFactory httpClientFactory)
    {
        _logger = logger;
        _keyStore = keyStore;
        _providerSettings = providerSettings;
        _hardwareDetector = hardwareDetector;
        _httpClientFactory = httpClientFactory;
    }

    /// <summary>
    /// Validates provider connectivity and configuration
    /// </summary>
    /// <param name="request">Optional list of provider names to validate. If empty, validates all known providers.</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Validation results for each provider</returns>
    [HttpPost("validate")]
    public async Task<ActionResult<ValidateResponse>> ValidateProviders(
        [FromBody] ValidateRequest? request = null,
        CancellationToken ct = default)
    {
        try
        {
            var sw = Stopwatch.StartNew();
            
            // Check OfflineOnly mode
            var systemProfile = await _hardwareDetector.DetectSystemAsync();
            var offlineOnly = systemProfile.OfflineOnly;

            _logger.LogInformation("Starting provider validation. OfflineOnly={OfflineOnly}", offlineOnly);

            // Determine which providers to validate
            var providersToValidate = request?.Providers ?? new List<string>
            {
                "OpenAI", "Azure", "Gemini", "ElevenLabs", "PlayHT", "Ollama", "StableDiffusion"
            };

            var results = new List<ValidationResult>();
            var httpClient = _httpClientFactory.CreateClient();

            foreach (var providerName in providersToValidate)
            {
                try
                {
                    var isCloudProvider = IsCloudProvider(providerName);
                    
                    if (offlineOnly && isCloudProvider)
                    {
                        results.Add(ValidationResult.Failure(
                            providerName,
                            "Offline mode is enabled. Cloud provider validation blocked.",
                            0,
                            "E307"));
                        continue;
                    }

                    var validator = CreateValidator(providerName, httpClient);
                    if (validator == null)
                    {
                        results.Add(ValidationResult.Failure(
                            providerName,
                            "Provider validator not implemented",
                            0));
                        continue;
                    }

                    var result = await validator.ValidateAsync(ct);
                    results.Add(result);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error validating provider {Provider}", providerName);
                    results.Add(ValidationResult.Failure(
                        providerName,
                        $"Validation error: {ex.Message}",
                        0));
                }
            }

            sw.Stop();
            var allOk = results.All(r => r.Ok);

            _logger.LogInformation("Provider validation completed in {ElapsedMs}ms. AllOk={AllOk}", 
                sw.ElapsedMilliseconds, allOk);

            return Ok(new ValidateResponse
            {
                Results = results,
                Ok = allOk
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during provider validation");
            return Problem("Error validating providers", statusCode: 500);
        }
    }

    private bool IsCloudProvider(string providerName)
    {
        var cloudProviders = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "OpenAI", "Azure", "Gemini", "ElevenLabs", "PlayHT"
        };
        
        return cloudProviders.Contains(providerName);
    }

    private IProviderValidator? CreateValidator(string providerName, HttpClient httpClient)
    {
        var loggerFactory = HttpContext.RequestServices.GetRequiredService<ILoggerFactory>();

        return providerName.ToLowerInvariant() switch
        {
            "openai" => new OpenAiValidator(
                loggerFactory.CreateLogger<OpenAiValidator>(),
                _keyStore,
                httpClient),
            
            "azure" => new AzureOpenAiValidator(
                loggerFactory.CreateLogger<AzureOpenAiValidator>(),
                _keyStore),
            
            "gemini" => new GeminiValidator(
                loggerFactory.CreateLogger<GeminiValidator>(),
                _keyStore),
            
            "elevenlabs" => new ElevenLabsValidator(
                loggerFactory.CreateLogger<ElevenLabsValidator>(),
                _keyStore,
                httpClient),
            
            "playht" => new PlayHtValidator(
                loggerFactory.CreateLogger<PlayHtValidator>(),
                _keyStore),
            
            "ollama" => new OllamaValidator(
                loggerFactory.CreateLogger<OllamaValidator>(),
                _providerSettings,
                httpClient),
            
            "stablediffusion" => new StableDiffusionValidator(
                loggerFactory.CreateLogger<StableDiffusionValidator>(),
                _providerSettings,
                httpClient),
            
            _ => null
        };
    }
}

/// <summary>
/// Request for provider validation
/// </summary>
public record ValidateRequest
{
    /// <summary>
    /// Optional list of provider names to validate. If empty, validates all known providers.
    /// </summary>
    public List<string>? Providers { get; init; }
}

/// <summary>
/// Response from provider validation
/// </summary>
public record ValidateResponse
{
    /// <summary>
    /// Validation results for each provider
    /// </summary>
    public List<ValidationResult> Results { get; init; } = new();

    /// <summary>
    /// Whether all providers validated successfully
    /// </summary>
    public bool Ok { get; init; }
}
