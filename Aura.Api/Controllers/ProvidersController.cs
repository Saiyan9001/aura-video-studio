using Aura.Api.Models.ApiModels.V1;
using Aura.Core.Configuration;
using Aura.Core.Hardware;
using Aura.Core.Models.Providers;
using Aura.Core.Services.Providers;
using Microsoft.AspNetCore.Mvc;
using Serilog;
using System.Runtime.InteropServices;

namespace Aura.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ProvidersController : ControllerBase
{
    private readonly IHardwareDetector _hardwareDetector;
    private readonly IKeyStore _keyStore;
    private readonly LlmProviderRecommendationService? _recommendationService;
    private readonly ProviderHealthMonitoringService? _healthMonitoringService;
    private readonly ProviderCostTrackingService? _costTrackingService;
    private readonly ProviderSettings _settings;
    private readonly OpenAIKeyValidationService _openAIValidationService;
    private readonly Aura.Core.Services.IKeyValidationService _keyValidationService;
    private readonly Aura.Core.Services.ISecureStorageService _secureStorageService;

    public ProvidersController(
        IHardwareDetector hardwareDetector, 
        IKeyStore keyStore,
        ProviderSettings settings,
        OpenAIKeyValidationService openAIValidationService,
        Aura.Core.Services.IKeyValidationService keyValidationService,
        Aura.Core.Services.ISecureStorageService secureStorageService,
        LlmProviderRecommendationService? recommendationService = null,
        ProviderHealthMonitoringService? healthMonitoringService = null,
        ProviderCostTrackingService? costTrackingService = null)
    {
        _hardwareDetector = hardwareDetector;
        _keyStore = keyStore;
        _settings = settings;
        _openAIValidationService = openAIValidationService;
        _keyValidationService = keyValidationService;
        _secureStorageService = secureStorageService;
        _recommendationService = recommendationService;
        _healthMonitoringService = healthMonitoringService;
        _costTrackingService = costTrackingService;
    }

    /// <summary>
    /// Get provider capabilities based on hardware, API keys, and OS detection
    /// </summary>
    [HttpGet("capabilities")]
    public async Task<IActionResult> GetCapabilities()
    {
        try
        {
            var systemProfile = await _hardwareDetector.DetectSystemAsync();
            var capabilities = new List<ProviderCapability>();

            // Stable Diffusion provider
            var sdCapability = GetStableDiffusionCapability(systemProfile);
            capabilities.Add(sdCapability);

            return Ok(capabilities);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error detecting provider capabilities");
            return Problem("Error detecting provider capabilities", statusCode: 500);
        }
    }

    private ProviderCapability GetStableDiffusionCapability(Aura.Core.Models.SystemProfile systemProfile)
    {
        var available = true;
        var reasonCodes = new List<string>();
        
        // Check for NVIDIA GPU
        var hasNvidiaGpu = systemProfile.Gpu?.Vendor?.ToUpperInvariant() == "NVIDIA";
        if (!hasNvidiaGpu)
        {
            available = false;
            reasonCodes.Add("RequiresNvidiaGPU");
        }

        // Check for STABLE_KEY API key
        var stableKey = _keyStore.GetKey("STABLE_KEY") ?? _keyStore.GetKey("stabilityai");
        if (string.IsNullOrWhiteSpace(stableKey))
        {
            available = false;
            reasonCodes.Add("MissingApiKey:STABLE_KEY");
        }

        // Check VRAM requirement (6GB minimum)
        if (systemProfile.Gpu?.VramGB < 6)
        {
            available = false;
            reasonCodes.Add("InsufficientVRAM");
        }

        // Check OS (Windows or Linux)
        var isWindowsOrLinux = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) || 
                               RuntimeInformation.IsOSPlatform(OSPlatform.Linux);
        if (!isWindowsOrLinux)
        {
            available = false;
            reasonCodes.Add("UnsupportedOS");
        }

        return new ProviderCapability
        {
            Name = "StableDiffusion",
            Available = available,
            ReasonCodes = reasonCodes.ToArray(),
            Requirements = new ProviderRequirements
            {
                NeedsKey = new[] { "STABLE_KEY" },
                NeedsGPU = "nvidia",
                MinVRAMMB = 6144,
                Os = new[] { "windows", "linux" }
            }
        };
    }

    /// <summary>
    /// Get provider recommendations for a specific operation type
    /// </summary>
    [HttpGet("recommendations/{operationType}")]
    public async Task<IActionResult> GetRecommendations(
        string operationType,
        [FromQuery] int estimatedInputTokens = 1000)
    {
        if (_recommendationService == null)
        {
            return Problem("Provider recommendation service not available", statusCode: 503);
        }

        try
        {
            if (!Enum.TryParse<LlmOperationType>(operationType, true, out var opType))
            {
                return BadRequest(new { error = $"Invalid operation type: {operationType}" });
            }

            var recommendations = await _recommendationService.GetRecommendationsAsync(
                opType,
                estimatedInputTokens: estimatedInputTokens,
                cancellationToken: HttpContext.RequestAborted);

            var dtos = recommendations.Select(r => new ProviderRecommendationDto(
                r.ProviderName,
                r.Reasoning,
                r.QualityScore,
                r.EstimatedCost,
                r.ExpectedLatencySeconds,
                r.IsAvailable,
                r.HealthStatus.ToString(),
                r.Confidence)).ToList();

            return Ok(dtos);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error getting provider recommendations for {OperationType}", operationType);
            return Problem($"Error getting provider recommendations: {ex.Message}", statusCode: 500);
        }
    }

    /// <summary>
    /// Get health status of all LLM providers
    /// </summary>
    [HttpGet("health")]
    public IActionResult GetProviderHealth()
    {
        if (_healthMonitoringService == null)
        {
            return Problem("Provider health monitoring service not available", statusCode: 503);
        }

        try
        {
            var healthMetrics = _healthMonitoringService.GetAllProviderHealth();
            var dtos = healthMetrics.Select(m => new ProviderHealthDto(
                m.ProviderName,
                m.SuccessRatePercent,
                m.AverageLatencySeconds,
                m.TotalRequests,
                m.ConsecutiveFailures,
                m.Status.ToString())).ToList();

            return Ok(dtos);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error getting provider health metrics");
            return Problem($"Error getting provider health metrics: {ex.Message}", statusCode: 500);
        }
    }

    /// <summary>
    /// Get cost tracking summary for current month
    /// </summary>
    [HttpGet("cost-tracking")]
    public IActionResult GetCostTracking()
    {
        if (_costTrackingService == null)
        {
            return Problem("Cost tracking service not available", statusCode: 503);
        }

        try
        {
            var totalCost = _costTrackingService.GetTotalMonthlyCost();
            var costByProvider = _costTrackingService.GetCostByProvider();
            var costByOperation = _costTrackingService.GetCostByOperation();

            var costByOperationStrings = costByOperation.ToDictionary(
                kvp => kvp.Key.ToString(),
                kvp => kvp.Value);

            var dto = new CostTrackingSummaryDto(
                totalCost,
                costByProvider,
                costByOperationStrings);

            return Ok(dto);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error getting cost tracking summary");
            return Problem($"Error getting cost tracking summary: {ex.Message}", statusCode: 500);
        }
    }

    /// <summary>
    /// Estimate cost for a specific operation
    /// </summary>
    [HttpPost("cost-estimate")]
    public IActionResult EstimateCost([FromBody] ProviderRecommendationRequest request)
    {
        if (_recommendationService == null)
        {
            return Problem("Provider recommendation service not available", statusCode: 503);
        }

        try
        {
            if (!Enum.TryParse<LlmOperationType>(request.OperationType, true, out var opType))
            {
                return BadRequest(new { error = $"Invalid operation type: {request.OperationType}" });
            }

            return Ok(new { message = "Cost estimation endpoint - implementation in progress" });
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error estimating cost");
            return Problem($"Error estimating cost: {ex.Message}", statusCode: 500);
        }
    }

    /// <summary>
    /// Get available provider profiles
    /// </summary>
    [HttpGet("profiles")]
    public IActionResult GetProviderProfiles()
    {
        try
        {
            var profiles = Enum.GetValues<ProviderProfile>()
                .Select(p => new
                {
                    Name = p.ToString(),
                    Description = p switch
                    {
                        ProviderProfile.MaximumQuality => "Always use highest quality provider regardless of cost",
                        ProviderProfile.Balanced => "Balance between quality, cost, and speed",
                        ProviderProfile.BudgetConscious => "Prefer cheaper providers that meet minimum quality threshold",
                        ProviderProfile.SpeedOptimized => "Optimize for fastest response times",
                        ProviderProfile.LocalOnly => "Only use local/offline providers (Ollama, RuleBased)",
                        ProviderProfile.Custom => "User-defined custom rules",
                        _ => ""
                    }
                })
                .ToList();

            return Ok(profiles);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error getting provider profiles");
            return Problem($"Error getting provider profiles: {ex.Message}", statusCode: 500);
        }
    }

    /// <summary>
    /// Test provider connection with API key
    /// </summary>
    [HttpPost("test-connection")]
    public async Task<IActionResult> TestConnection([FromBody] TestProviderConnectionRequest request, CancellationToken cancellationToken)
    {
        var correlationId = HttpContext.TraceIdentifier;
        
        try
        {
            if (string.IsNullOrWhiteSpace(request.ProviderName))
            {
                return BadRequest(new { 
                    success = false,
                    message = "Provider name is required",
                    correlationId
                });
            }

            if (string.IsNullOrWhiteSpace(request.ApiKey))
            {
                return BadRequest(new { 
                    success = false,
                    message = "API key is required",
                    correlationId
                });
            }

            Log.Information(
                "Testing provider connection for {Provider}, CorrelationId: {CorrelationId}",
                request.ProviderName,
                correlationId);

            var startTime = DateTime.UtcNow;
            var result = await _keyValidationService.TestApiKeyAsync(
                request.ProviderName.ToLowerInvariant(),
                request.ApiKey,
                cancellationToken);
            var responseTime = (long)(DateTime.UtcNow - startTime).TotalMilliseconds;

            Log.Information(
                "Provider connection test completed for {Provider}, Success: {Success}, ResponseTime: {ResponseTime}ms, CorrelationId: {CorrelationId}",
                request.ProviderName,
                result.IsValid,
                responseTime,
                correlationId);

            return Ok(new { 
                success = result.IsValid,
                message = result.Message,
                details = result.Details,
                responseTimeMs = responseTime,
                correlationId
            });
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error testing provider connection for {Provider}, CorrelationId: {CorrelationId}", 
                request.ProviderName, correlationId);
            return Problem(
                title: "Connection Test Error",
                detail: $"An error occurred while testing the connection: {ex.Message}",
                statusCode: 500,
                type: "https://docs.aura.studio/errors/connection-test-error",
                instance: correlationId);
        }
    }

    /// <summary>
    /// Validate OpenAI API key with live network verification
    /// </summary>
    [HttpPost("openai/validate")]
    public async Task<IActionResult> ValidateOpenAIKey(
        [FromBody] ValidateOpenAIKeyRequest request,
        CancellationToken cancellationToken)
    {
        var correlationId = HttpContext.TraceIdentifier;
        
        try
        {
            if (string.IsNullOrWhiteSpace(request.ApiKey))
            {
                return BadRequest(new ProviderValidationResponse(
                    IsValid: false,
                    Status: "Invalid",
                    Message: "API key is required",
                    CorrelationId: correlationId,
                    Details: new ValidationDetails(
                        Provider: "OpenAI",
                        KeyFormat: "empty",
                        FormatValid: false)));
            }

            Log.Information(
                "Validating OpenAI API key, CorrelationId: {CorrelationId}",
                correlationId);

            var result = await _openAIValidationService.ValidateKeyAsync(
                request.ApiKey,
                request.BaseUrl,
                request.OrganizationId,
                request.ProjectId,
                correlationId,
                cancellationToken);

            var response = new ProviderValidationResponse(
                IsValid: result.IsValid,
                Status: result.Status,
                Message: result.Message,
                CorrelationId: correlationId,
                Details: new ValidationDetails(
                    Provider: "OpenAI",
                    KeyFormat: result.FormatValid ? "valid" : "invalid",
                    FormatValid: result.FormatValid,
                    NetworkCheckPassed: result.NetworkCheckPassed,
                    HttpStatusCode: result.HttpStatusCode,
                    ErrorType: result.ErrorType,
                    ResponseTimeMs: result.ResponseTimeMs));

            return Ok(response);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error validating OpenAI API key, CorrelationId: {CorrelationId}", correlationId);
            return Problem(
                title: "Validation Error",
                detail: "An unexpected error occurred while validating the API key.",
                statusCode: 500,
                type: "https://docs.aura.studio/errors/validation-error",
                instance: correlationId);
        }
    }

    /// <summary>
    /// Get available OpenAI models for a validated API key
    /// </summary>
    [HttpPost("openai/models")]
    public async Task<IActionResult> GetOpenAIModels(
        [FromBody] ValidateOpenAIKeyRequest request,
        CancellationToken cancellationToken)
    {
        var correlationId = HttpContext.TraceIdentifier;
        
        try
        {
            if (string.IsNullOrWhiteSpace(request.ApiKey))
            {
                return BadRequest(new { 
                    success = false,
                    message = "API key is required",
                    correlationId
                });
            }

            Log.Information(
                "Fetching OpenAI models, CorrelationId: {CorrelationId}",
                correlationId);

            var result = await _openAIValidationService.GetAvailableModelsAsync(
                request.ApiKey,
                request.BaseUrl,
                request.OrganizationId,
                request.ProjectId,
                cancellationToken);

            if (!result.Success)
            {
                return Ok(new { 
                    success = false,
                    message = result.ErrorMessage,
                    correlationId
                });
            }

            return Ok(new { 
                success = true,
                models = result.Models,
                correlationId
            });
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error fetching OpenAI models, CorrelationId: {CorrelationId}", correlationId);
            return Problem(
                title: "Models Fetch Error",
                detail: "An unexpected error occurred while fetching models.",
                statusCode: 500,
                type: "https://docs.aura.studio/errors/models-fetch-error",
                instance: correlationId);
        }
    }

    /// <summary>
    /// Test OpenAI script generation with a simple prompt
    /// </summary>
    [HttpPost("openai/test-generation")]
    public async Task<IActionResult> TestOpenAIGeneration(
        [FromBody] TestOpenAIGenerationRequest request,
        CancellationToken cancellationToken)
    {
        var correlationId = HttpContext.TraceIdentifier;
        
        try
        {
            if (string.IsNullOrWhiteSpace(request.ApiKey))
            {
                return BadRequest(new { 
                    success = false,
                    message = "API key is required",
                    correlationId
                });
            }

            if (string.IsNullOrWhiteSpace(request.Model))
            {
                return BadRequest(new { 
                    success = false,
                    message = "Model is required",
                    correlationId
                });
            }

            Log.Information(
                "Testing OpenAI script generation with model {Model}, CorrelationId: {CorrelationId}",
                request.Model,
                correlationId);

            var result = await _openAIValidationService.TestScriptGenerationAsync(
                request.ApiKey,
                request.Model,
                request.BaseUrl,
                request.OrganizationId,
                request.ProjectId,
                cancellationToken);

            if (!result.Success)
            {
                return Ok(new { 
                    success = false,
                    message = result.ErrorMessage,
                    responseTimeMs = result.ResponseTimeMs,
                    correlationId
                });
            }

            return Ok(new { 
                success = true,
                generatedText = result.GeneratedText,
                model = result.Model,
                responseTimeMs = result.ResponseTimeMs,
                correlationId
            });
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error testing OpenAI generation, CorrelationId: {CorrelationId}", correlationId);
            return Problem(
                title: "Generation Test Error",
                detail: "An unexpected error occurred while testing script generation.",
                statusCode: 500,
                type: "https://docs.aura.studio/errors/generation-test-error",
                instance: correlationId);
        }
    }

    /// <summary>
    /// Validate ElevenLabs API key with live network verification
    /// </summary>
    [HttpPost("elevenlabs/validate")]
    public async Task<IActionResult> ValidateElevenLabsKey(
        [FromBody] ValidateElevenLabsKeyRequest request,
        CancellationToken cancellationToken)
    {
        var correlationId = HttpContext.TraceIdentifier;
        
        try
        {
            if (string.IsNullOrWhiteSpace(request.ApiKey))
            {
                return BadRequest(new ProviderValidationResponse(
                    IsValid: false,
                    Status: "Invalid",
                    Message: "API key is required",
                    CorrelationId: correlationId,
                    Details: new ValidationDetails(
                        Provider: "ElevenLabs",
                        KeyFormat: "empty",
                        FormatValid: false)));
            }

            Log.Information(
                "Validating ElevenLabs API key, CorrelationId: {CorrelationId}",
                correlationId);

            var startTime = DateTime.UtcNow;
            var result = await _keyValidationService.TestApiKeyAsync("elevenlabs", request.ApiKey, cancellationToken);
            var responseTime = (long)(DateTime.UtcNow - startTime).TotalMilliseconds;

            var response = new ProviderValidationResponse(
                IsValid: result.IsValid,
                Status: result.IsValid ? "Valid" : "Invalid",
                Message: result.Message,
                CorrelationId: correlationId,
                Details: new ValidationDetails(
                    Provider: "ElevenLabs",
                    KeyFormat: "valid",
                    FormatValid: true,
                    NetworkCheckPassed: result.IsValid,
                    ResponseTimeMs: responseTime));

            return Ok(response);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error validating ElevenLabs API key, CorrelationId: {CorrelationId}", correlationId);
            return Problem(
                title: "Validation Error",
                detail: "An unexpected error occurred while validating the API key.",
                statusCode: 500,
                type: "https://docs.aura.studio/errors/validation-error",
                instance: correlationId);
        }
    }

    /// <summary>
    /// Validate PlayHT API key with live network verification
    /// </summary>
    [HttpPost("playht/validate")]
    public async Task<IActionResult> ValidatePlayHTKey(
        [FromBody] ValidatePlayHTKeyRequest request,
        CancellationToken cancellationToken)
    {
        var correlationId = HttpContext.TraceIdentifier;
        
        try
        {
            if (string.IsNullOrWhiteSpace(request.ApiKey))
            {
                return BadRequest(new ProviderValidationResponse(
                    IsValid: false,
                    Status: "Invalid",
                    Message: "API key is required",
                    CorrelationId: correlationId,
                    Details: new ValidationDetails(
                        Provider: "PlayHT",
                        KeyFormat: "empty",
                        FormatValid: false)));
            }

            Log.Information(
                "Validating PlayHT API key, CorrelationId: {CorrelationId}",
                correlationId);

            var startTime = DateTime.UtcNow;
            var result = await _keyValidationService.TestApiKeyAsync("playht", request.ApiKey, cancellationToken);
            var responseTime = (long)(DateTime.UtcNow - startTime).TotalMilliseconds;

            var response = new ProviderValidationResponse(
                IsValid: result.IsValid,
                Status: result.IsValid ? "Valid" : "Invalid",
                Message: result.Message,
                CorrelationId: correlationId,
                Details: new ValidationDetails(
                    Provider: "PlayHT",
                    KeyFormat: "valid",
                    FormatValid: true,
                    NetworkCheckPassed: result.IsValid,
                    ResponseTimeMs: responseTime));

            return Ok(response);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error validating PlayHT API key, CorrelationId: {CorrelationId}", correlationId);
            return Problem(
                title: "Validation Error",
                detail: "An unexpected error occurred while validating the API key.",
                statusCode: 500,
                type: "https://docs.aura.studio/errors/validation-error",
                instance: correlationId);
        }
    }

    /// <summary>
    /// Get provider status dashboard showing which providers are configured
    /// </summary>
    [HttpGet("status")]
    public async Task<IActionResult> GetProviderStatus()
    {
        var correlationId = HttpContext.TraceIdentifier;
        
        try
        {
            Log.Information("Getting provider status, CorrelationId: {CorrelationId}", correlationId);

            var configuredProviders = await _secureStorageService.GetConfiguredProvidersAsync();
            
            var providerStatuses = new List<ProviderStatusDto>
            {
                new ProviderStatusDto(
                    Name: "OpenAI",
                    IsConfigured: configuredProviders.Contains("openai"),
                    IsAvailable: configuredProviders.Contains("openai"),
                    Status: configuredProviders.Contains("openai") ? "Configured" : "Not Configured"),
                new ProviderStatusDto(
                    Name: "Anthropic",
                    IsConfigured: configuredProviders.Contains("anthropic"),
                    IsAvailable: configuredProviders.Contains("anthropic"),
                    Status: configuredProviders.Contains("anthropic") ? "Configured" : "Not Configured"),
                new ProviderStatusDto(
                    Name: "Gemini",
                    IsConfigured: configuredProviders.Contains("gemini") || configuredProviders.Contains("google"),
                    IsAvailable: configuredProviders.Contains("gemini") || configuredProviders.Contains("google"),
                    Status: configuredProviders.Contains("gemini") || configuredProviders.Contains("google") ? "Configured" : "Not Configured"),
                new ProviderStatusDto(
                    Name: "ElevenLabs",
                    IsConfigured: configuredProviders.Contains("elevenlabs"),
                    IsAvailable: configuredProviders.Contains("elevenlabs"),
                    Status: configuredProviders.Contains("elevenlabs") ? "Configured" : "Not Configured"),
                new ProviderStatusDto(
                    Name: "PlayHT",
                    IsConfigured: configuredProviders.Contains("playht"),
                    IsAvailable: configuredProviders.Contains("playht"),
                    Status: configuredProviders.Contains("playht") ? "Configured" : "Not Configured"),
                new ProviderStatusDto(
                    Name: "Pexels",
                    IsConfigured: configuredProviders.Contains("pexels"),
                    IsAvailable: configuredProviders.Contains("pexels"),
                    Status: configuredProviders.Contains("pexels") ? "Configured" : "Not Configured"),
                new ProviderStatusDto(
                    Name: "StabilityAI",
                    IsConfigured: configuredProviders.Contains("stabilityai") || configuredProviders.Contains("stability"),
                    IsAvailable: configuredProviders.Contains("stabilityai") || configuredProviders.Contains("stability"),
                    Status: configuredProviders.Contains("stabilityai") || configuredProviders.Contains("stability") ? "Configured" : "Not Configured"),
                new ProviderStatusDto(
                    Name: "RuleBased",
                    IsConfigured: true,
                    IsAvailable: true,
                    Status: "Always Available (Offline)")
            };

            return Ok(providerStatuses);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error getting provider status, CorrelationId: {CorrelationId}", correlationId);
            return Problem(
                title: "Provider Status Error",
                detail: "An unexpected error occurred while getting provider status.",
                statusCode: 500,
                type: "https://docs.aura.studio/errors/provider-status-error",
                instance: correlationId);
        }
    }

    /// <summary>
    /// Get current provider recommendation preferences
    /// </summary>
    [HttpGet("preferences")]
    public IActionResult GetPreferences()
    {
        try
        {
            var dto = new ProviderPreferencesDto(
                EnableRecommendations: _settings.GetEnableRecommendations(),
                AssistanceLevel: _settings.GetAssistanceLevel(),
                EnableHealthMonitoring: _settings.GetEnableHealthMonitoring(),
                EnableCostTracking: _settings.GetEnableCostTracking(),
                EnableLearning: _settings.GetEnableLearning(),
                EnableProfiles: _settings.GetEnableProfiles(),
                EnableAutoFallback: _settings.GetEnableAutoFallback(),
                GlobalDefault: null,
                AlwaysUseDefault: false,
                PerOperationOverrides: null,
                ActiveProfile: "Balanced",
                ExcludedProviders: null,
                PinnedProvider: null,
                FallbackChains: null,
                MonthlyBudgetLimit: null,
                PerProviderBudgetLimits: null,
                HardBudgetLimit: false
            );

            return Ok(dto);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error getting provider preferences");
            return Problem($"Error getting provider preferences: {ex.Message}", statusCode: 500);
        }
    }

    /// <summary>
    /// Update provider recommendation preferences
    /// </summary>
    [HttpPost("preferences")]
    public IActionResult UpdatePreferences([FromBody] ProviderPreferencesDto preferences)
    {
        try
        {
            _settings.SetRecommendationPreferences(
                enableRecommendations: preferences.EnableRecommendations,
                assistanceLevel: preferences.AssistanceLevel,
                enableHealthMonitoring: preferences.EnableHealthMonitoring,
                enableCostTracking: preferences.EnableCostTracking,
                enableLearning: preferences.EnableLearning,
                enableProfiles: preferences.EnableProfiles,
                enableAutoFallback: preferences.EnableAutoFallback
            );

            return Ok(new { message = "Preferences updated successfully" });
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error updating provider preferences");
            return Problem($"Error updating provider preferences: {ex.Message}", statusCode: 500);
        }
    }

    /// <summary>
    /// Get status of all configured providers
    /// </summary>
    [HttpGet("status")]
    public async Task<IActionResult> GetProviderStatus(CancellationToken cancellationToken)
    {
        var correlationId = HttpContext.TraceIdentifier;
        
        try
        {
            Log.Information("Fetching provider status, CorrelationId: {CorrelationId}", correlationId);

            var statuses = new List<ProviderStatusDto>();
            var configuredProviders = await _secureStorageService.GetConfiguredProvidersAsync();

            var providerNames = new[] { "OpenAI", "Anthropic", "Google", "Ollama", "ElevenLabs", "PlayHT", "Windows", "StabilityAI", "StableDiffusion", "Stock" };

            foreach (var provider in providerNames)
            {
                var isConfigured = configuredProviders.Contains(provider, StringComparer.OrdinalIgnoreCase);
                var hasKey = await _secureStorageService.HasApiKeyAsync(provider);

                var status = new ProviderStatusDto(
                    Name: provider,
                    IsConfigured: isConfigured || hasKey,
                    IsAvailable: isConfigured || hasKey || IsLocalProvider(provider),
                    Status: GetProviderStatus(provider, hasKey),
                    LastValidated: null,
                    ErrorMessage: null
                );

                statuses.Add(status);
            }

            return Ok(statuses);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error fetching provider status, CorrelationId: {CorrelationId}", correlationId);
            return Problem(
                title: "Provider Status Error",
                detail: "An error occurred while fetching provider status.",
                statusCode: 500,
                type: "https://docs.aura.studio/errors/provider-status-error",
                instance: correlationId);
        }
    }

    /// <summary>
    /// Get provider priorities
    /// </summary>
    [HttpGet("priorities")]
    public async Task<IActionResult> GetProviderPriorities(CancellationToken cancellationToken)
    {
        var correlationId = HttpContext.TraceIdentifier;
        
        try
        {
            Log.Information("Fetching provider priorities, CorrelationId: {CorrelationId}", correlationId);

            var configPath = Path.Combine(
                _settings.GetAuraDataDirectory(),
                "configurations",
                "provider-config.json");

            if (!System.IO.File.Exists(configPath))
            {
                var defaultPriorities = new Dictionary<string, int>
                {
                    { "OpenAI", 1 },
                    { "Anthropic", 2 },
                    { "Google", 3 },
                    { "Ollama", 4 },
                    { "ElevenLabs", 1 },
                    { "PlayHT", 2 },
                    { "Windows", 3 },
                    { "StabilityAI", 1 },
                    { "StableDiffusion", 2 },
                    { "Stock", 3 }
                };
                
                return Ok(defaultPriorities);
            }

            var json = await System.IO.File.ReadAllTextAsync(configPath, cancellationToken);
            var config = System.Text.Json.JsonSerializer.Deserialize<ProviderConfigurationResponse>(json);
            
            var priorities = config?.Providers?.ToDictionary(
                p => p.Name,
                p => p.Priority
            ) ?? new Dictionary<string, int>();

            return Ok(priorities);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error fetching provider priorities, CorrelationId: {CorrelationId}", correlationId);
            return Problem(
                title: "Provider Priorities Error",
                detail: "An error occurred while fetching provider priorities.",
                statusCode: 500,
                type: "https://docs.aura.studio/errors/provider-priorities-error",
                instance: correlationId);
        }
    }

    /// <summary>
    /// Update provider priorities
    /// </summary>
    [HttpPut("priorities")]
    public async Task<IActionResult> UpdateProviderPriorities(
        [FromBody] Dictionary<string, int> priorities,
        CancellationToken cancellationToken)
    {
        var correlationId = HttpContext.TraceIdentifier;
        
        try
        {
            if (priorities == null || priorities.Count == 0)
            {
                return BadRequest(new { 
                    success = false,
                    message = "Priorities dictionary is required",
                    correlationId
                });
            }

            Log.Information("Updating provider priorities, CorrelationId: {CorrelationId}", correlationId);

            var configPath = Path.Combine(
                _settings.GetAuraDataDirectory(),
                "configurations",
                "provider-config.json");

            if (!System.IO.File.Exists(configPath))
            {
                return NotFound(new { 
                    success = false,
                    message = "Provider configuration not found",
                    correlationId
                });
            }

            var json = await System.IO.File.ReadAllTextAsync(configPath, cancellationToken);
            var config = System.Text.Json.JsonSerializer.Deserialize<ProviderConfigurationResponse>(json);

            if (config?.Providers == null)
            {
                return Problem(
                    title: "Invalid Configuration",
                    detail: "Provider configuration is invalid or empty.",
                    statusCode: 500);
            }

            foreach (var provider in config.Providers)
            {
                if (priorities.TryGetValue(provider.Name, out var priority))
                {
                    provider.Priority = priority;
                }
            }

            var updatedJson = System.Text.Json.JsonSerializer.Serialize(config, new System.Text.Json.JsonSerializerOptions 
            { 
                WriteIndented = true 
            });
            await System.IO.File.WriteAllTextAsync(configPath, updatedJson, cancellationToken);

            Log.Information("Provider priorities updated successfully, CorrelationId: {CorrelationId}", correlationId);

            return Ok(new { 
                success = true,
                message = "Priorities updated successfully",
                correlationId
            });
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error updating provider priorities, CorrelationId: {CorrelationId}", correlationId);
            return Problem(
                title: "Provider Priorities Update Error",
                detail: $"An error occurred while updating provider priorities: {ex.Message}",
                statusCode: 500,
                type: "https://docs.aura.studio/errors/provider-priorities-update-error",
                instance: correlationId);
        }
    }

    private static bool IsLocalProvider(string provider)
    {
        return provider.ToLowerInvariant() switch
        {
            "ollama" => true,
            "windows" => true,
            "stock" => true,
            "stablediffusion" => true,
            _ => false
        };
    }

    private static string GetProviderStatus(string provider, bool hasKey)
    {
        if (IsLocalProvider(provider))
        {
            return "Available";
        }
        return hasKey ? "Configured" : "Not configured";
    }

    /// <summary>
    /// Get Ollama service status with version and model count
    /// </summary>
    [HttpGet("ollama/status")]
    public async Task<IActionResult> GetOllamaStatus(CancellationToken cancellationToken)
    {
        var correlationId = HttpContext.TraceIdentifier;
        
        try
        {
            var ollamaDetectionService = HttpContext.RequestServices.GetService(typeof(Aura.Core.Services.Providers.OllamaDetectionService)) 
                as Aura.Core.Services.Providers.OllamaDetectionService;

            if (ollamaDetectionService == null)
            {
                return Ok(new
                {
                    isAvailable = false,
                    version = (string?)null,
                    modelsCount = 0,
                    message = "Ollama detection service not initialized",
                    correlationId
                });
            }

            var status = await ollamaDetectionService.GetStatusAsync(cancellationToken);
            var models = status.IsRunning 
                ? await ollamaDetectionService.GetModelsAsync(cancellationToken)
                : new List<Aura.Core.Services.Providers.OllamaModel>();

            Log.Information(
                "Ollama status check: Available={Available}, Models={ModelsCount}, CorrelationId: {CorrelationId}",
                status.IsRunning,
                models.Count,
                correlationId);

            return Ok(new
            {
                isAvailable = status.IsRunning,
                version = status.Version,
                modelsCount = models.Count,
                message = status.IsRunning 
                    ? $"Ollama running with {models.Count} models"
                    : status.ErrorMessage ?? "Ollama service not running",
                correlationId
            });
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error checking Ollama status, CorrelationId: {CorrelationId}", correlationId);
            return Ok(new
            {
                isAvailable = false,
                version = (string?)null,
                modelsCount = 0,
                message = $"Error checking Ollama: {ex.Message}",
                correlationId
            });
        }
    }

    /// <summary>
    /// Get list of available Ollama models
    /// </summary>
    [HttpGet("ollama/models")]
    public async Task<IActionResult> GetOllamaModels(CancellationToken cancellationToken)
    {
        var correlationId = HttpContext.TraceIdentifier;
        
        try
        {
            var ollamaDetectionService = HttpContext.RequestServices.GetService(typeof(Aura.Core.Services.Providers.OllamaDetectionService)) 
                as Aura.Core.Services.Providers.OllamaDetectionService;

            if (ollamaDetectionService == null)
            {
                return StatusCode(503, new
                {
                    success = false,
                    message = "Ollama detection service not initialized",
                    installationInstructions = "Install Ollama: curl -fsSL https://ollama.com/install.sh | sh",
                    correlationId
                });
            }

            var status = await ollamaDetectionService.GetStatusAsync(cancellationToken);
            
            if (!status.IsRunning)
            {
                return StatusCode(503, new
                {
                    success = false,
                    message = "Ollama service not running",
                    installationInstructions = "Start Ollama: ollama serve (or install: curl -fsSL https://ollama.com/install.sh | sh)",
                    correlationId
                });
            }

            var models = await ollamaDetectionService.GetModelsAsync(cancellationToken);

            Log.Information(
                "Ollama models fetched: {ModelsCount} models, CorrelationId: {CorrelationId}",
                models.Count,
                correlationId);

            return Ok(new
            {
                success = true,
                models = models.Select(m => new
                {
                    name = m.Name,
                    size = m.Size,
                    sizeFormatted = FormatBytes(m.Size),
                    modified = m.ModifiedAt,
                    modifiedFormatted = m.ModifiedAt != null 
                        ? DateTime.Parse(m.ModifiedAt).ToString("yyyy-MM-dd HH:mm:ss")
                        : null
                }).ToList(),
                correlationId
            });
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error fetching Ollama models, CorrelationId: {CorrelationId}", correlationId);
            return StatusCode(503, new
            {
                success = false,
                message = $"Error fetching Ollama models: {ex.Message}",
                installationInstructions = "Install Ollama: curl -fsSL https://ollama.com/install.sh | sh",
                correlationId
            });
        }
    }

    /// <summary>
    /// Validate Ollama service availability
    /// </summary>
    [HttpPost("ollama/validate")]
    public async Task<IActionResult> ValidateOllama(CancellationToken cancellationToken)
    {
        var correlationId = HttpContext.TraceIdentifier;
        
        try
        {
            var ollamaDetectionService = HttpContext.RequestServices.GetService(typeof(Aura.Core.Services.Providers.OllamaDetectionService)) 
                as Aura.Core.Services.Providers.OllamaDetectionService;

            if (ollamaDetectionService == null)
            {
                return Ok(new
                {
                    success = false,
                    message = "Ollama detection service not initialized",
                    isAvailable = false,
                    modelsCount = 0,
                    models = Array.Empty<object>(),
                    correlationId
                });
            }

            var status = await ollamaDetectionService.GetStatusAsync(cancellationToken);
            var models = status.IsRunning 
                ? await ollamaDetectionService.GetModelsAsync(cancellationToken)
                : new List<Aura.Core.Services.Providers.OllamaModel>();

            Log.Information(
                "Ollama validation: Available={Available}, Models={ModelsCount}, CorrelationId: {CorrelationId}",
                status.IsRunning,
                models.Count,
                correlationId);

            return Ok(new
            {
                success = status.IsRunning,
                message = status.IsRunning 
                    ? $"Ollama is available with {models.Count} models"
                    : status.ErrorMessage ?? "Ollama service not available",
                isAvailable = status.IsRunning,
                version = status.Version,
                modelsCount = models.Count,
                models = models.Select(m => new
                {
                    name = m.Name,
                    size = m.Size,
                    sizeFormatted = FormatBytes(m.Size),
                    modified = m.ModifiedAt
                }).ToList(),
                correlationId
            });
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error validating Ollama, CorrelationId: {CorrelationId}", correlationId);
            return Ok(new
            {
                success = false,
                message = $"Error validating Ollama: {ex.Message}",
                isAvailable = false,
                modelsCount = 0,
                models = Array.Empty<object>(),
                correlationId
            });
        }
    }

    private static string FormatBytes(long bytes)
    {
        string[] sizes = { "B", "KB", "MB", "GB", "TB" };
        double len = bytes;
        int order = 0;
        while (len >= 1024 && order < sizes.Length - 1)
        {
            order++;
            len = len / 1024;
        }
        return $"{len:0.##} {sizes[order]}";
    }
}

/// <summary>
/// Provider configuration response for JSON deserialization
/// </summary>
internal class ProviderConfigurationResponse
{
    public List<ProviderItem> Providers { get; set; } = new();
}

/// <summary>
/// Provider item for JSON deserialization
/// </summary>
internal class ProviderItem
{
    public string Name { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public bool Enabled { get; set; }
    public int Priority { get; set; }
}

/// <summary>
/// Represents a provider's capability and availability status
/// </summary>
public class ProviderCapability
{
    public string Name { get; set; } = string.Empty;
    public bool Available { get; set; }
    public string[] ReasonCodes { get; set; } = Array.Empty<string>();
    public ProviderRequirements Requirements { get; set; } = new();
}

/// <summary>
/// Represents requirements for a provider
/// </summary>
public class ProviderRequirements
{
    public string[] NeedsKey { get; set; } = Array.Empty<string>();
    public string? NeedsGPU { get; set; }
    public int? MinVRAMMB { get; set; }
    public string[] Os { get; set; } = Array.Empty<string>();
}
