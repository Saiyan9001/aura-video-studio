using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Aura.Core.Models;
using Aura.Core.Models.Audio;
using Aura.Core.Models.Localization;
using Aura.Core.Providers;
using Aura.Core.Services.Audio;
using Aura.Core.Captions;
using Microsoft.Extensions.Logging;

namespace Aura.Core.Services.Localization;

/// <summary>
/// Orchestrates the complete translation workflow: 
/// Translation → SSML mapping → TTS synthesis → Subtitle generation
/// </summary>
public class TranslationWorkflowOrchestrator
{
    private readonly ILogger<TranslationWorkflowOrchestrator> _logger;
    private readonly TranslationService _translationService;
    private readonly SSMLPlannerService _ssmlPlanner;
    private readonly CaptionBuilder _captionBuilder;

    public TranslationWorkflowOrchestrator(
        ILogger<TranslationWorkflowOrchestrator> logger,
        ILlmProvider llmProvider,
        ILoggerFactory loggerFactory)
    {
        _logger = logger;
        _translationService = new TranslationService(
            loggerFactory.CreateLogger<TranslationService>(),
            llmProvider);
        _ssmlPlanner = new SSMLPlannerService(
            loggerFactory.CreateLogger<SSMLPlannerService>());
        _captionBuilder = new CaptionBuilder(
            loggerFactory.CreateLogger<CaptionBuilder>());
    }

    /// <summary>
    /// Complete workflow: translate script and generate SSML for target language
    /// </summary>
    public async Task<TranslationWorkflowResult> TranslateAndGenerateSSMLAsync(
        TranslationRequest translationRequest,
        VoiceSpec targetVoiceSpec,
        string targetTtsProvider,
        CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();
        _logger.LogInformation(
            "Starting translation workflow: {Source} → {Target}, Provider: {Provider}",
            translationRequest.SourceLanguage,
            translationRequest.TargetLanguage,
            targetTtsProvider);

        var result = new TranslationWorkflowResult();

        try
        {
            // Phase 1: Translate script
            _logger.LogInformation("Phase 1: Translating script");
            var translationResult = await _translationService.TranslateAsync(
                translationRequest,
                cancellationToken);
            result.TranslationResult = translationResult;

            // Phase 2: Convert translated lines to ScriptLine format
            _logger.LogInformation("Phase 2: Converting to SSML-compatible format");
            var translatedScriptLines = translationResult.TranslatedLines
                .Select(tl => new ScriptLine(
                    SceneIndex: tl.SceneIndex,
                    Text: tl.TranslatedText,
                    Start: TimeSpan.FromSeconds(tl.AdjustedStartSeconds),
                    Duration: TimeSpan.FromSeconds(tl.AdjustedDurationSeconds)))
                .ToList();

            // Phase 3: Generate SSML with timing alignment
            _logger.LogInformation("Phase 3: Generating SSML with timing alignment");
            var ssmlPlanRequest = new SSMLPlanRequest
            {
                ScriptLines = translatedScriptLines,
                TargetProvider = targetTtsProvider,
                VoiceSpec = targetVoiceSpec,
                TargetDurations = translatedScriptLines.ToDictionary(
                    line => line.SceneIndex,
                    line => line.Duration.TotalSeconds),
                DurationTolerance = 0.02,
                MaxFittingIterations = 10
            };

            var ssmlPlan = await _ssmlPlanner.PlanSSMLAsync(
                ssmlPlanRequest,
                cancellationToken);
            result.SSMLPlan = ssmlPlan;

            // Phase 4: Generate subtitles (SRT and VTT)
            _logger.LogInformation("Phase 4: Generating subtitle files");
            result.SrtContent = _captionBuilder.GenerateSrt(translatedScriptLines);
            result.VttContent = _captionBuilder.GenerateVtt(translatedScriptLines);

            // Phase 5: Validate timing alignment
            _logger.LogInformation("Phase 5: Validating timing alignment");
            var timingValid = _captionBuilder.ValidateTimecodes(
                translatedScriptLines,
                out var validationMessage);
            
            if (!timingValid)
            {
                _logger.LogWarning("Timing validation issue: {Message}", validationMessage);
                result.ValidationWarnings.Add(validationMessage ?? "Unknown timing issue");
            }

            stopwatch.Stop();
            result.TotalDuration = stopwatch.Elapsed;
            result.Success = true;

            _logger.LogInformation(
                "Translation workflow completed in {Duration:F2}s",
                result.TotalDuration.TotalSeconds);

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Translation workflow failed");
            result.Success = false;
            result.Error = ex.Message;
            stopwatch.Stop();
            result.TotalDuration = stopwatch.Elapsed;
            return result;
        }
    }

    /// <summary>
    /// Generate subtitles for already translated content
    /// </summary>
    public SubtitleExportResult GenerateSubtitles(
        List<TranslatedScriptLine> translatedLines,
        SubtitleFormat format,
        SubtitleStyle? style = null)
    {
        _logger.LogInformation(
            "Generating {Format} subtitles for {Count} lines",
            format,
            translatedLines.Count);

        try
        {
            var scriptLines = translatedLines
                .Select(tl => new ScriptLine(
                    SceneIndex: tl.SceneIndex,
                    Text: tl.TranslatedText,
                    Start: TimeSpan.FromSeconds(tl.AdjustedStartSeconds),
                    Duration: TimeSpan.FromSeconds(tl.AdjustedDurationSeconds)))
                .ToList();

            var content = format switch
            {
                SubtitleFormat.SRT => _captionBuilder.GenerateSrt(scriptLines),
                SubtitleFormat.VTT => _captionBuilder.GenerateVtt(scriptLines),
                _ => throw new ArgumentException($"Unsupported subtitle format: {format}")
            };

            var valid = _captionBuilder.ValidateTimecodes(
                scriptLines,
                out var validationMessage);

            return new SubtitleExportResult
            {
                Content = content,
                Format = format,
                LineCount = translatedLines.Count,
                IsValid = valid,
                ValidationMessage = validationMessage,
                Style = style
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Subtitle generation failed");
            return new SubtitleExportResult
            {
                Content = string.Empty,
                Format = format,
                LineCount = 0,
                IsValid = false,
                ValidationMessage = ex.Message
            };
        }
    }

    /// <summary>
    /// Check if TTS provider supports target language
    /// </summary>
    public bool CheckProviderLanguageSupport(
        string providerName,
        string languageCode)
    {
        var supportedLanguages = GetProviderSupportedLanguages(providerName);
        return supportedLanguages.Contains(languageCode) ||
               supportedLanguages.Contains(languageCode.Split('-')[0]);
    }

    private HashSet<string> GetProviderSupportedLanguages(string providerName)
    {
        return providerName.ToLowerInvariant() switch
        {
            "elevenlabs" => new HashSet<string>
            {
                "en", "es", "fr", "de", "it", "pt", "pl", "hi", "ar", "ja", "ko", "zh"
            },
            "playht" => new HashSet<string>
            {
                "en", "es", "fr", "de", "it", "pt", "pl", "ru", "hi", "ar", "ja", "ko", "zh"
            },
            "windows" => new HashSet<string>
            {
                "en", "es", "fr", "de", "it", "pt", "ru", "ja", "ko", "zh", "ar", "he"
            },
            "piper" => new HashSet<string>
            {
                "en", "es", "fr", "de", "it", "ru", "uk", "nl", "pl"
            },
            "mimic3" => new HashSet<string>
            {
                "en", "es", "fr", "de", "it", "ru", "nl", "fi", "sw"
            },
            _ => new HashSet<string> { "en" }
        };
    }
}

/// <summary>
/// Result of complete translation workflow
/// </summary>
public class TranslationWorkflowResult
{
    public bool Success { get; set; }
    public string? Error { get; set; }
    public TranslationResult? TranslationResult { get; set; }
    public SSMLPlanResult? SSMLPlan { get; set; }
    public string SrtContent { get; set; } = string.Empty;
    public string VttContent { get; set; } = string.Empty;
    public List<string> ValidationWarnings { get; set; } = new();
    public TimeSpan TotalDuration { get; set; }
}

/// <summary>
/// Result of subtitle export
/// </summary>
public class SubtitleExportResult
{
    public string Content { get; set; } = string.Empty;
    public SubtitleFormat Format { get; set; }
    public int LineCount { get; set; }
    public bool IsValid { get; set; }
    public string? ValidationMessage { get; set; }
    public SubtitleStyle? Style { get; set; }
}

/// <summary>
/// Subtitle format enum
/// </summary>
public enum SubtitleFormat
{
    SRT,
    VTT
}

/// <summary>
/// Subtitle style configuration for RTL and font fallback
/// </summary>
public class SubtitleStyle
{
    public string PrimaryFont { get; set; } = "Arial";
    public List<string> FallbackFonts { get; set; } = new() { "DejaVu Sans", "Noto Sans" };
    public int FontSize { get; set; } = 24;
    public string TextColor { get; set; } = "FFFFFF";
    public string BackgroundColor { get; set; } = "000000";
    public int BackgroundOpacity { get; set; } = 128;
    public bool EnableRTL { get; set; }
    public string Alignment { get; set; } = "center";
    public int OutlineWidth { get; set; } = 2;
    public string OutlineColor { get; set; } = "000000";
}
