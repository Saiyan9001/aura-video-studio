using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Aura.Core.Models.ContentSafety;
using Microsoft.Extensions.Logging;

namespace Aura.Core.Services.ContentSafety;

/// <summary>
/// Integration service for applying content safety checks in video generation pipeline
/// </summary>
public class SafetyIntegrationService
{
    private readonly ILogger<SafetyIntegrationService> _logger;
    private readonly ContentSafetyService _safetyService;
    private readonly List<SafetyAuditLog> _auditLogs = new();

    public SafetyIntegrationService(
        ILogger<SafetyIntegrationService> logger,
        ContentSafetyService safetyService)
    {
        _logger = logger;
        _safetyService = safetyService;
    }

    /// <summary>
    /// Check if script content passes safety policy before generation
    /// </summary>
    public async Task<SafetyCheckResult> CheckScriptSafetyAsync(
        string script,
        SafetyPolicy policy,
        CancellationToken ct = default)
    {
        _logger.LogInformation("Checking script safety with policy {PolicyName}", policy.Name);

        try
        {
            var result = await _safetyService.AnalyzeContentAsync(
                Guid.NewGuid().ToString(),
                script,
                policy,
                ct);

            var checkResult = new SafetyCheckResult
            {
                Passed = result.IsSafe,
                RequiresReview = result.RequiresReview,
                CanProceed = result.IsSafe || (policy.AllowUserOverride && result.AllowWithDisclaimer),
                AnalysisResult = result,
                Message = DetermineSafetyMessage(result, policy)
            };

            _logger.LogInformation(
                "Safety check complete. Passed: {Passed}, RequiresReview: {RequiresReview}",
                checkResult.Passed,
                checkResult.RequiresReview);

            return checkResult;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking script safety");
            throw;
        }
    }

    /// <summary>
    /// Check if visual prompt passes safety policy
    /// </summary>
    public async Task<SafetyCheckResult> CheckVisualPromptSafetyAsync(
        string prompt,
        SafetyPolicy policy,
        CancellationToken ct = default)
    {
        _logger.LogInformation("Checking visual prompt safety");

        var result = await _safetyService.AnalyzeContentAsync(
            Guid.NewGuid().ToString(),
            prompt,
            policy,
            ct);

        return new SafetyCheckResult
        {
            Passed = result.IsSafe,
            RequiresReview = result.RequiresReview,
            CanProceed = result.IsSafe || policy.AllowUserOverride,
            AnalysisResult = result,
            Message = DetermineSafetyMessage(result, policy)
        };
    }

    /// <summary>
    /// Apply auto-fixes to content based on safety violations
    /// </summary>
    public string ApplyAutoFixes(string content, SafetyAnalysisResult analysisResult)
    {
        var modified = content;

        foreach (var violation in analysisResult.Violations)
        {
            if (violation.RecommendedAction == SafetyAction.AutoFix && !string.IsNullOrEmpty(violation.SuggestedFix))
            {
                if (!string.IsNullOrEmpty(violation.MatchedContent))
                {
                    modified = modified.Replace(violation.MatchedContent, violation.SuggestedFix);
                    _logger.LogInformation(
                        "Applied auto-fix: '{Original}' -> '{Fixed}'",
                        violation.MatchedContent,
                        violation.SuggestedFix);
                }
            }
        }

        return modified;
    }

    /// <summary>
    /// Add required disclaimers to content
    /// </summary>
    public string AddDisclaimers(string content, SafetyAnalysisResult analysisResult, SafetyPolicy policy)
    {
        if (!analysisResult.AllowWithDisclaimer)
            return content;

        var disclaimers = new List<string>();

        if (!string.IsNullOrEmpty(analysisResult.RecommendedDisclaimer))
        {
            disclaimers.Add(analysisResult.RecommendedDisclaimer);
        }

        if (policy.ComplianceSettings?.RequiredDisclosures != null)
        {
            disclaimers.AddRange(policy.ComplianceSettings.RequiredDisclosures);
        }

        if (disclaimers.Count == 0)
            return content;

        var disclaimerText = string.Join("\n\n", disclaimers.Select(d => $"[DISCLAIMER: {d}]"));
        return $"{disclaimerText}\n\n{content}";
    }

    private string DetermineSafetyMessage(SafetyAnalysisResult result, SafetyPolicy policy)
    {
        if (result.IsSafe)
        {
            return "Content passed all safety checks.";
        }

        if (result.Violations.Count == 0)
        {
            return "Content has warnings but can proceed.";
        }

        var blockingViolations = result.Violations.Count(v => v.RecommendedAction == SafetyAction.Block);

        if (blockingViolations > 0)
        {
            if (policy.AllowUserOverride)
            {
                return $"Content has {blockingViolations} blocking violation(s). User override is allowed.";
            }
            else
            {
                return $"Content has {blockingViolations} blocking violation(s) and cannot proceed.";
            }
        }

        if (result.RequiresReview)
        {
            return "Content requires manual review before proceeding.";
        }

        return "Content has minor issues but can proceed with fixes or disclaimers.";
    }
}

/// <summary>
/// Result of a safety check
/// </summary>
public class SafetyCheckResult
{
    public bool Passed { get; set; }
    public bool RequiresReview { get; set; }
    public bool CanProceed { get; set; }
    public SafetyAnalysisResult? AnalysisResult { get; set; }
    public string Message { get; set; } = string.Empty;
}
