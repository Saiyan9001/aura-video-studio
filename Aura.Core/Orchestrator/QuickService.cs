using System;
using System.Threading;
using System.Threading.Tasks;
using Aura.Core.Models;
using Microsoft.Extensions.Logging;

namespace Aura.Core.Orchestrator;

/// <summary>
/// Service for one-click safe video generation with guaranteed-success settings.
/// Forces Free-only providers: RuleBased LLM + Windows/Null TTS + Stock visuals.
/// Locks render to 1080p30 H.264 for maximum compatibility.
/// </summary>
public class QuickService
{
    private readonly ILogger<QuickService> _logger;
    private readonly JobRunner _jobRunner;

    public QuickService(ILogger<QuickService> logger, JobRunner jobRunner)
    {
        _logger = logger;
        _jobRunner = jobRunner;
    }

    /// <summary>
    /// Creates and starts a quick demo video job with safe defaults.
    /// Generates a 10-15 second video with captions.
    /// Explicitly uses safe providers to ensure success.
    /// </summary>
    public async Task<QuickDemoResult> CreateQuickDemoAsync(
        string? topic = null,
        CancellationToken ct = default)
    {
        var correlationId = $"quick-demo-{DateTime.UtcNow:yyyyMMddHHmmss}";
        
        try
        {
            _logger.LogInformation("[{CorrelationId}] Starting Quick Demo generation with safe defaults", correlationId);

            // Force safe brief settings
            var brief = new Brief(
                Topic: topic ?? "Welcome to Aura Video Studio",
                Audience: "General",
                Goal: "Demonstrate",
                Tone: "Informative",
                Language: "en-US",
                Aspect: Aspect.Widescreen16x9
            );
            
            _logger.LogInformation("[{CorrelationId}] Quick Demo brief created: {Topic}", correlationId, brief.Topic);

            // Target 10-15 seconds duration (0.25 minutes)
            var planSpec = new PlanSpec(
                TargetDuration: TimeSpan.FromSeconds(12),
                Pacing: Pacing.Fast,
                Density: Density.Sparse,
                Style: "Demo"
            );
            
            _logger.LogInformation("[{CorrelationId}] Quick Demo plan: {Duration}s", correlationId, planSpec.TargetDuration.TotalSeconds);

            // Safe voice settings - uses Windows TTS or Null TTS fallback
            // The factory will automatically select the best available provider
            var voiceSpec = new VoiceSpec(
                VoiceName: "en-US-Standard-A",
                Rate: 1.0,
                Pitch: 0.0,
                Pause: PauseStyle.Short
            );
            
            _logger.LogInformation("[{CorrelationId}] Quick Demo voice spec configured", correlationId);

            // Lock to 1080p30 H.264 for maximum compatibility
            var renderSpec = new RenderSpec(
                Res: new Resolution(1920, 1080),
                Container: "mp4",
                VideoBitrateK: 5000,
                AudioBitrateK: 192,
                Fps: 30,
                Codec: "H264",
                QualityLevel: 75,
                EnableSceneCut: true
            );
            
            _logger.LogInformation("[{CorrelationId}] Quick Demo render spec: {Width}x{Height} @ {Fps}fps {Codec}", 
                correlationId, renderSpec.Res.Width, renderSpec.Res.Height, renderSpec.Fps, renderSpec.Codec);

            // Create and start the job with safe defaults and mark as Quick Demo
            _logger.LogInformation("[{CorrelationId}] Creating Quick Demo job with JobRunner", correlationId);
            
            var job = await _jobRunner.CreateAndStartJobAsync(
                brief,
                planSpec,
                voiceSpec,
                renderSpec,
                correlationId: correlationId,
                isQuickDemo: true,
                ct
            );

            _logger.LogInformation("[{CorrelationId}] Quick Demo job created successfully: {JobId}", correlationId, job.Id);

            return new QuickDemoResult(
                Success: true,
                JobId: job.Id,
                Message: "Quick demo started successfully"
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[{CorrelationId}] Failed to create Quick Demo: {Message}", correlationId, ex.Message);
            
            // Never throw - always return structured error
            return new QuickDemoResult(
                Success: false,
                JobId: null,
                Message: $"Failed to create quick demo: {ex.Message}. Correlation ID: {correlationId}"
            );
        }
    }
}

/// <summary>
/// Result of a quick demo generation request
/// </summary>
public record QuickDemoResult(
    bool Success,
    string? JobId,
    string Message
);
