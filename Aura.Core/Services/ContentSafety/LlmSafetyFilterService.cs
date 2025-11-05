using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Aura.Core.Models.ContentSafety;
using Microsoft.Extensions.Logging;

namespace Aura.Core.Services.ContentSafety;

/// <summary>
/// Service for filtering and moderating LLM prompts and responses for safety
/// </summary>
public class LlmSafetyFilterService
{
    private readonly ILogger<LlmSafetyFilterService> _logger;
    private readonly ContentSafetyService _contentSafetyService;

    private static readonly HashSet<string> UnsafePromptIndicators = new(StringComparer.OrdinalIgnoreCase)
    {
        "jailbreak", "ignore previous", "ignore instructions", "bypass filter",
        "you are now", "pretend you are", "roleplay as", "act as if",
        "unrestricted mode", "developer mode", "DAN mode", "evil mode"
    };

    private static readonly Dictionary<string, string> PromptModificationStrategies = new()
    {
        { "violence", "action scenes with appropriate context" },
        { "explicit", "mature themes handled professionally" },
        { "controversial", "balanced perspective on sensitive topics" },
        { "hate", "respectful discussion of diversity" },
        { "illegal", "educational discussion within legal boundaries" },
        { "weapon", "historical or educational context" },
        { "drug", "health education perspective" },
        { "political", "objective analysis without partisan bias" }
    };

    public LlmSafetyFilterService(
        ILogger<LlmSafetyFilterService> logger,
        ContentSafetyService contentSafetyService)
    {
        _logger = logger;
        _contentSafetyService = contentSafetyService;
    }

    /// <summary>
    /// Validates a prompt before sending to LLM
    /// </summary>
    public async Task<PromptSafetyResult> ValidatePromptAsync(
        string prompt,
        SafetyPolicy policy,
        CancellationToken ct = default)
    {
        _logger.LogInformation("Validating LLM prompt for safety");

        var result = new PromptSafetyResult { OriginalPrompt = prompt };

        if (ContainsJailbreakAttempt(prompt))
        {
            result.IsSafe = false;
            result.BlockReason = "Prompt contains potential jailbreak or instruction override attempt";
            result.SuggestedAction = "Remove attempts to bypass safety guidelines and rephrase your request";
            _logger.LogWarning("Blocked jailbreak attempt in prompt");
            return result;
        }

        var analysisResult = await _contentSafetyService.AnalyzeContentAsync(
            Guid.NewGuid().ToString(),
            prompt,
            policy,
            ct);

        if (!analysisResult.IsSafe && !policy.AllowUserOverride)
        {
            result.IsSafe = false;
            result.BlockReason = $"Prompt violates content safety policy: {string.Join(", ", analysisResult.Violations.Select(v => v.Reason))}";
            result.Violations = analysisResult.Violations;
            result.ModifiedPrompt = ApplyPromptModifications(prompt, analysisResult);
            result.SuggestedAction = GenerateSuggestedAction(analysisResult);
            result.Explanation = GenerateSafetyExplanation(analysisResult);
            result.Alternatives = GenerateAlternatives(prompt, analysisResult);
            return result;
        }

        if (analysisResult.Violations.Any())
        {
            result.RequiresReview = analysisResult.RequiresReview;
            result.Warnings = analysisResult.Violations.Select(v => v.Reason).ToList();
            result.ModifiedPrompt = ApplyPromptModifications(prompt, analysisResult);
            result.CanProceedWithWarning = policy.AllowUserOverride;
            result.Explanation = GenerateSafetyExplanation(analysisResult);
        }

        result.IsSafe = true;
        _logger.LogInformation("Prompt validation complete. Safe: {IsSafe}, Warnings: {WarningCount}", 
            result.IsSafe, result.Warnings.Count);

        return result;
    }

    /// <summary>
    /// Validates an LLM response before presenting to user
    /// </summary>
    public async Task<ResponseSafetyResult> ValidateResponseAsync(
        string response,
        SafetyPolicy policy,
        CancellationToken ct = default)
    {
        _logger.LogInformation("Validating LLM response for safety");

        var result = new ResponseSafetyResult { OriginalResponse = response };

        var analysisResult = await _contentSafetyService.AnalyzeContentAsync(
            Guid.NewGuid().ToString(),
            response,
            policy,
            ct);

        if (!analysisResult.IsSafe)
        {
            result.IsSafe = false;
            result.BlockReason = $"Response violates content safety policy: {string.Join(", ", analysisResult.Violations.Select(v => v.Reason))}";
            result.Violations = analysisResult.Violations;
            result.SuggestedAction = "Regenerate with modified prompt or stricter safety settings";
            return result;
        }

        if (analysisResult.AllowWithDisclaimer && !string.IsNullOrEmpty(analysisResult.RecommendedDisclaimer))
        {
            result.RequiresDisclaimer = true;
            result.Disclaimer = analysisResult.RecommendedDisclaimer;
        }

        result.IsSafe = true;
        result.CategoryScores = analysisResult.CategoryScores;

        _logger.LogInformation("Response validation complete. Safe: {IsSafe}", result.IsSafe);

        return result;
    }

    /// <summary>
    /// Automatically modifies a prompt to avoid safety triggers while preserving intent
    /// </summary>
    public string ApplyPromptModifications(string prompt, SafetyAnalysisResult analysis)
    {
        var modified = prompt;

        foreach (var violation in analysis.Violations.Where(v => !string.IsNullOrEmpty(v.SuggestedFix)))
        {
            if (!string.IsNullOrEmpty(violation.MatchedContent) && !string.IsNullOrEmpty(violation.SuggestedFix))
            {
                modified = modified.Replace(violation.MatchedContent, violation.SuggestedFix, StringComparison.OrdinalIgnoreCase);
            }
        }

        foreach (var (keyword, replacement) in PromptModificationStrategies)
        {
            if (modified.Contains(keyword, StringComparison.OrdinalIgnoreCase) &&
                analysis.Violations.Any(v => v.Reason.Contains(keyword, StringComparison.OrdinalIgnoreCase)))
            {
                var keywordIndex = modified.IndexOf(keyword, StringComparison.OrdinalIgnoreCase);
                if (keywordIndex >= 0)
                {
                    modified = modified.Remove(keywordIndex, keyword.Length).Insert(keywordIndex, replacement);
                }
            }
        }

        if (modified == prompt)
        {
            modified = $"[Professional context] {prompt}";
        }

        return modified;
    }

    /// <summary>
    /// Generates user-friendly explanation of why content was blocked
    /// </summary>
    public string GenerateSafetyExplanation(SafetyAnalysisResult analysis)
    {
        if (!analysis.Violations.Any())
        {
            return "Content meets safety guidelines.";
        }

        var explanations = new List<string>
        {
            "Your request was flagged for the following reasons:"
        };

        var groupedViolations = analysis.Violations
            .GroupBy(v => v.Category)
            .OrderByDescending(g => g.Max(v => v.SeverityScore));

        foreach (var group in groupedViolations)
        {
            var categoryName = group.Key.ToString().Replace("_", " ");
            var severity = group.Max(v => v.SeverityScore);
            var severityText = severity >= 8 ? "High" : severity >= 5 ? "Medium" : "Low";

            explanations.Add($"- {categoryName} ({severityText} severity): {string.Join("; ", group.Select(v => v.Reason))}");
        }

        explanations.Add("\nWe want to ensure content remains appropriate for all audiences and complies with content guidelines.");

        return string.Join("\n", explanations);
    }

    /// <summary>
    /// Generates alternative suggestions for blocked content
    /// </summary>
    public List<string> GenerateAlternatives(string prompt, SafetyAnalysisResult analysis)
    {
        var alternatives = new List<string>();

        if (analysis.Violations.Any(v => v.Category == SafetyCategoryType.Violence))
        {
            alternatives.Add("Focus on the emotional journey and character development rather than explicit action");
            alternatives.Add("Use metaphorical language to convey intensity without graphic details");
        }

        if (analysis.Violations.Any(v => v.Category == SafetyCategoryType.ControversialTopics))
        {
            alternatives.Add("Present multiple perspectives to provide balanced coverage");
            alternatives.Add("Focus on educational and informative aspects");
            alternatives.Add("Use neutral language and cite credible sources");
        }

        if (analysis.Violations.Any(v => v.Category == SafetyCategoryType.Profanity))
        {
            alternatives.Add("Use professional language appropriate for general audiences");
            alternatives.Add("Express ideas with impactful but family-friendly vocabulary");
        }

        if (analysis.SuggestedFixes.Any())
        {
            alternatives.AddRange(analysis.SuggestedFixes.Select(fix => $"Apply suggested fix: {fix}"));
        }

        if (!alternatives.Any())
        {
            alternatives.Add("Rephrase your request with more general, educational, or informative framing");
            alternatives.Add("Focus on the positive outcomes and constructive aspects of your topic");
        }

        return alternatives;
    }

    private bool ContainsJailbreakAttempt(string prompt)
    {
        var lowerPrompt = prompt.ToLowerInvariant();

        if (UnsafePromptIndicators.Any(indicator => lowerPrompt.Contains(indicator)))
        {
            return true;
        }

        if (lowerPrompt.Contains("ignore") && (lowerPrompt.Contains("previous") || lowerPrompt.Contains("above") || lowerPrompt.Contains("instruction")))
        {
            return true;
        }

        return false;
    }

    private string GenerateSuggestedAction(SafetyAnalysisResult analysis)
    {
        var actions = new List<string>();

        if (analysis.Violations.Any(v => v.RecommendedAction == SafetyAction.Block))
        {
            actions.Add("Rephrase your request to comply with content guidelines");
        }

        if (analysis.Violations.Any(v => v.RecommendedAction == SafetyAction.AutoFix))
        {
            actions.Add("Use the suggested modified version of your prompt");
        }

        if (analysis.Violations.Any(v => v.CanOverride))
        {
            actions.Add("Review the warnings and proceed if appropriate for your use case");
        }

        return string.Join(" or ", actions);
    }
}

/// <summary>
/// Result of prompt safety validation
/// </summary>
public class PromptSafetyResult
{
    public bool IsSafe { get; set; }
    public string OriginalPrompt { get; set; } = string.Empty;
    public string? ModifiedPrompt { get; set; }
    public string? BlockReason { get; set; }
    public string? SuggestedAction { get; set; }
    public string? Explanation { get; set; }
    public List<string> Warnings { get; set; } = new();
    public List<string> Alternatives { get; set; } = new();
    public List<SafetyViolation> Violations { get; set; } = new();
    public bool RequiresReview { get; set; }
    public bool CanProceedWithWarning { get; set; }
}

/// <summary>
/// Result of response safety validation
/// </summary>
public class ResponseSafetyResult
{
    public bool IsSafe { get; set; }
    public string OriginalResponse { get; set; } = string.Empty;
    public string? BlockReason { get; set; }
    public string? SuggestedAction { get; set; }
    public bool RequiresDisclaimer { get; set; }
    public string? Disclaimer { get; set; }
    public List<SafetyViolation> Violations { get; set; } = new();
    public Dictionary<SafetyCategoryType, int> CategoryScores { get; set; } = new();
}
