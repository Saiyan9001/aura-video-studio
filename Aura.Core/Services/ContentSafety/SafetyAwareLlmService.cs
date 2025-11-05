using System;
using System.Threading;
using System.Threading.Tasks;
using Aura.Core.AI.Orchestration;
using Aura.Core.Models.ContentSafety;
using Aura.Core.Providers;
using Microsoft.Extensions.Logging;

namespace Aura.Core.Services.ContentSafety;

/// <summary>
/// Wrapper service that adds content safety checks to LLM operations
/// </summary>
public class SafetyAwareLlmService
{
    private readonly ILogger<SafetyAwareLlmService> _logger;
    private readonly LlmSafetyFilterService _safetyFilter;
    private readonly SafetyIntegrationService _safetyIntegration;
    private readonly UnifiedLlmOrchestrator? _orchestrator;

    public SafetyAwareLlmService(
        ILogger<SafetyAwareLlmService> logger,
        LlmSafetyFilterService safetyFilter,
        SafetyIntegrationService safetyIntegration,
        UnifiedLlmOrchestrator? orchestrator = null)
    {
        _logger = logger;
        _safetyFilter = safetyFilter;
        _safetyIntegration = safetyIntegration;
        _orchestrator = orchestrator;
    }

    /// <summary>
    /// Executes an LLM operation with safety checks on prompt and response
    /// </summary>
    public async Task<SafeLlmOperationResult> ExecuteSafeOperationAsync(
        LlmOperationRequest request,
        ILlmProvider provider,
        SafetyPolicy policy,
        CancellationToken ct = default)
    {
        _logger.LogInformation("Executing safe LLM operation: {OperationType}", request.OperationType);

        var result = new SafeLlmOperationResult
        {
            OperationType = request.OperationType,
            SessionId = request.SessionId
        };

        var promptValidation = await _safetyFilter.ValidatePromptAsync(request.Prompt, policy, ct);
        result.PromptValidation = promptValidation;

        if (!promptValidation.IsSafe && !policy.AllowUserOverride)
        {
            result.Success = false;
            result.ErrorMessage = $"Prompt blocked by safety policy: {promptValidation.BlockReason}";
            _logger.LogWarning("LLM operation blocked due to unsafe prompt");

            await _safetyIntegration.LogSafetyDecisionAsync(
                request.SessionId,
                request.Prompt,
                policy,
                SafetyDecision.Rejected,
                promptValidation.BlockReason,
                ct);

            return result;
        }

        if (promptValidation.RequiresReview)
        {
            result.RequiresUserApproval = true;
            result.WarningMessage = "This prompt contains content that requires review. Please confirm to proceed.";
            _logger.LogInformation("LLM operation requires user review");
            return result;
        }

        var effectivePrompt = promptValidation.ModifiedPrompt ?? request.Prompt;
        if (promptValidation.ModifiedPrompt != null)
        {
            _logger.LogInformation("Using modified prompt for safety compliance");
            result.UsedModifiedPrompt = true;
        }

        string? llmResponse = null;

        try
        {
            if (_orchestrator != null)
            {
                var modifiedRequest = request with { Prompt = effectivePrompt };
                var orchestratorResponse = await _orchestrator.ExecuteOperationAsync(modifiedRequest, provider, ct);
                
                if (!orchestratorResponse.Success)
                {
                    result.Success = false;
                    result.ErrorMessage = orchestratorResponse.ErrorMessage ?? "LLM operation failed";
                    return result;
                }

                llmResponse = orchestratorResponse.Content;
                result.Telemetry = orchestratorResponse.Telemetry;
            }
            else
            {
                llmResponse = await provider.CompleteAsync(effectivePrompt, ct);
            }

            var responseValidation = await _safetyFilter.ValidateResponseAsync(llmResponse, policy, ct);
            result.ResponseValidation = responseValidation;

            if (!responseValidation.IsSafe)
            {
                result.Success = false;
                result.ErrorMessage = $"Response blocked by safety policy: {responseValidation.BlockReason}";
                result.SuggestedAction = "Try regenerating with a modified prompt or stricter safety settings";
                
                _logger.LogWarning("LLM response blocked due to safety violation");

                await _safetyIntegration.LogSafetyDecisionAsync(
                    request.SessionId,
                    llmResponse,
                    policy,
                    SafetyDecision.Rejected,
                    responseValidation.BlockReason,
                    ct);

                return result;
            }

            result.Success = true;
            result.Content = llmResponse;
            result.RequiresDisclaimer = responseValidation.RequiresDisclaimer;
            result.Disclaimer = responseValidation.Disclaimer;

            _logger.LogInformation("Safe LLM operation completed successfully");

            await _safetyIntegration.LogSafetyDecisionAsync(
                request.SessionId,
                llmResponse,
                policy,
                SafetyDecision.Approved,
                "Content passed safety validation",
                ct);

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during safe LLM operation");
            result.Success = false;
            result.ErrorMessage = $"Operation failed: {ex.Message}";
            return result;
        }
    }

    /// <summary>
    /// Validates and potentially modifies a prompt before use
    /// </summary>
    public async Task<PromptSafetyResult> ValidateAndModifyPromptAsync(
        string prompt,
        SafetyPolicy policy,
        CancellationToken ct = default)
    {
        return await _safetyFilter.ValidatePromptAsync(prompt, policy, ct);
    }

    /// <summary>
    /// Checks if a response is safe to present to the user
    /// </summary>
    public async Task<ResponseSafetyResult> ValidateResponseAsync(
        string response,
        SafetyPolicy policy,
        CancellationToken ct = default)
    {
        return await _safetyFilter.ValidateResponseAsync(response, policy, ct);
    }
}

/// <summary>
/// Result of a safety-aware LLM operation
/// </summary>
public class SafeLlmOperationResult
{
    public bool Success { get; set; }
    public string? Content { get; set; }
    public string? ErrorMessage { get; set; }
    public string? WarningMessage { get; set; }
    public string? SuggestedAction { get; set; }
    
    public LlmOperationType OperationType { get; set; }
    public string SessionId { get; set; } = string.Empty;
    
    public PromptSafetyResult? PromptValidation { get; set; }
    public ResponseSafetyResult? ResponseValidation { get; set; }
    
    public bool UsedModifiedPrompt { get; set; }
    public bool RequiresUserApproval { get; set; }
    public bool RequiresDisclaimer { get; set; }
    public string? Disclaimer { get; set; }
    
    public LlmOperationTelemetry? Telemetry { get; set; }
}
