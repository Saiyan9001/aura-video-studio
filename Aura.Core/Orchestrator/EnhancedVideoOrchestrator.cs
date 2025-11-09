using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using Aura.Core.Errors;
using Aura.Core.Models;
using Aura.Core.Providers;
using Aura.Core.Services;
using Aura.Core.Services.Providers;
using Aura.Core.Validation;
using Microsoft.Extensions.Logging;

namespace Aura.Core.Orchestrator;

/// <summary>
/// Enhanced video orchestrator implementing the complete pipeline with:
/// - Circuit breaker pattern for provider failures
/// - Exponential backoff retry logic
/// - Memory-efficient streaming using Channels
/// - Checkpoint/resume capability
/// - IAsyncDisposable for proper resource cleanup
/// - Performance metrics collection
/// - Comprehensive logging with correlation IDs
/// </summary>
public sealed class EnhancedVideoOrchestrator : IAsyncDisposable
{
    private readonly ILogger<EnhancedVideoOrchestrator> _logger;
    private readonly ILlmProvider _llmProvider;
    private readonly ITtsProvider _ttsProvider;
    private readonly IVideoComposer _videoComposer;
    private readonly IImageProvider? _imageProvider;
    private readonly ProviderCircuitBreakerService _circuitBreaker;
    private readonly ProviderRetryWrapper _retryWrapper;
    private readonly PreGenerationValidator _preGenerationValidator;
    private readonly ScriptValidator _scriptValidator;
    private readonly TtsOutputValidator _ttsValidator;
    private readonly ImageOutputValidator _imageValidator;
    private readonly LlmOutputValidator _llmValidator;
    private readonly ResourceCleanupManager _cleanupManager;
    private readonly CheckpointManager? _checkpointManager;
    private readonly Telemetry.RunTelemetryCollector _telemetryCollector;
    
    private readonly SemaphoreSlim _concurrencySemaphore;
    private bool _disposed;

    public EnhancedVideoOrchestrator(
        ILogger<EnhancedVideoOrchestrator> logger,
        ILlmProvider llmProvider,
        ITtsProvider ttsProvider,
        IVideoComposer videoComposer,
        ProviderCircuitBreakerService circuitBreaker,
        ProviderRetryWrapper retryWrapper,
        PreGenerationValidator preGenerationValidator,
        ScriptValidator scriptValidator,
        TtsOutputValidator ttsValidator,
        ImageOutputValidator imageValidator,
        LlmOutputValidator llmValidator,
        ResourceCleanupManager cleanupManager,
        Telemetry.RunTelemetryCollector telemetryCollector,
        IImageProvider? imageProvider = null,
        CheckpointManager? checkpointManager = null,
        int maxConcurrency = 3)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _llmProvider = llmProvider ?? throw new ArgumentNullException(nameof(llmProvider));
        _ttsProvider = ttsProvider ?? throw new ArgumentNullException(nameof(ttsProvider));
        _videoComposer = videoComposer ?? throw new ArgumentNullException(nameof(videoComposer));
        _circuitBreaker = circuitBreaker ?? throw new ArgumentNullException(nameof(circuitBreaker));
        _retryWrapper = retryWrapper ?? throw new ArgumentNullException(nameof(retryWrapper));
        _preGenerationValidator = preGenerationValidator ?? throw new ArgumentNullException(nameof(preGenerationValidator));
        _scriptValidator = scriptValidator ?? throw new ArgumentNullException(nameof(scriptValidator));
        _ttsValidator = ttsValidator ?? throw new ArgumentNullException(nameof(ttsValidator));
        _imageValidator = imageValidator ?? throw new ArgumentNullException(nameof(imageValidator));
        _llmValidator = llmValidator ?? throw new ArgumentNullException(nameof(llmValidator));
        _cleanupManager = cleanupManager ?? throw new ArgumentNullException(nameof(cleanupManager));
        _telemetryCollector = telemetryCollector ?? throw new ArgumentNullException(nameof(telemetryCollector));
        
        _imageProvider = imageProvider;
        _checkpointManager = checkpointManager;
        _concurrencySemaphore = new SemaphoreSlim(maxConcurrency, maxConcurrency);
    }

    /// <summary>
    /// Executes the complete video generation pipeline with state management and error handling
    /// </summary>
    public async Task<string> GenerateVideoAsync(
        Brief brief,
        PlanSpec planSpec,
        VoiceSpec voiceSpec,
        RenderSpec renderSpec,
        SystemProfile systemProfile,
        IProgress<GenerationProgress>? progress = null,
        PipelineConfiguration? configuration = null,
        CancellationToken ct = default,
        string? jobId = null)
    {
        configuration ??= new PipelineConfiguration();
        var correlationId = jobId ?? Guid.NewGuid().ToString();
        
        _logger.LogInformation(
            "[{CorrelationId}] Starting enhanced video generation pipeline for topic: {Topic}",
            correlationId, brief.Topic);

        using var context = new PipelineContext(correlationId, brief, planSpec, voiceSpec, renderSpec, systemProfile);
        context.State = PipelineState.Running;

        try
        {
            // Stage 0: Brief Validation
            await ExecuteBriefValidationStageAsync(context, progress, ct);

            // Stage 1: Script Generation
            await ExecuteScriptGenerationStageAsync(context, progress, configuration, ct);

            // Stage 2: Scene Parsing
            await ExecuteSceneParsingStageAsync(context, progress, ct);

            // Stage 3: Voice Generation
            await ExecuteVoiceGenerationStageAsync(context, progress, configuration, ct);

            // Stage 4: Visual Asset Generation (if provider available)
            if (_imageProvider != null)
            {
                await ExecuteVisualGenerationStageAsync(context, progress, configuration, ct);
            }

            // Stage 5: Video Composition & Rendering
            await ExecuteRenderingStageAsync(context, progress, configuration, ct);

            context.MarkCompleted();
            
            _logger.LogInformation(
                "[{CorrelationId}] Pipeline completed successfully in {Elapsed:F2}s",
                correlationId, context.GetElapsedTime().TotalSeconds);

            return context.FinalVideoPath ?? throw new InvalidOperationException("No video path produced");
        }
        catch (OperationCanceledException)
        {
            context.MarkCancelled();
            _logger.LogWarning("[{CorrelationId}] Pipeline cancelled after {Elapsed:F2}s",
                correlationId, context.GetElapsedTime().TotalSeconds);
            throw;
        }
        catch (Exception ex)
        {
            context.MarkFailed();
            context.RecordError(context.CurrentStage, ex, isRecoverable: false);
            
            _logger.LogError(ex,
                "[{CorrelationId}] Pipeline failed at stage {Stage} after {Elapsed:F2}s",
                correlationId, context.CurrentStage, context.GetElapsedTime().TotalSeconds);
            
            LogPipelineMetrics(context);
            throw;
        }
        finally
        {
            _cleanupManager.CleanupAll();
            LogPipelineMetrics(context);
        }
    }

    #region Pipeline Stages

    private async Task ExecuteBriefValidationStageAsync(
        PipelineContext context,
        IProgress<GenerationProgress>? progress,
        CancellationToken ct)
    {
        const string stageName = "BriefValidation";
        context.CurrentStage = stageName;
        var sw = Stopwatch.StartNew();

        _logger.LogInformation("[{CorrelationId}] Stage: {Stage}", context.CorrelationId, stageName);
        progress?.Report(ProgressBuilder.CreateBriefProgress(0, "Validating brief and system readiness", context.CorrelationId));

        try
        {
            var validationResult = await _preGenerationValidator
                .ValidateSystemReadyAsync(context.Brief, context.PlanSpec, ct);

            if (!validationResult.IsValid)
            {
                var issues = string.Join("; ", validationResult.Issues);
                throw new ValidationException("System validation failed", validationResult.Issues);
            }

            progress?.Report(ProgressBuilder.CreateBriefProgress(100, "Validation complete", context.CorrelationId));
            
            RecordStageMetrics(context, stageName, sw, itemsProcessed: 1);
        }
        catch (Exception ex)
        {
            context.RecordError(stageName, ex, isRecoverable: false);
            throw;
        }
    }

    private async Task ExecuteScriptGenerationStageAsync(
        PipelineContext context,
        IProgress<GenerationProgress>? progress,
        PipelineConfiguration configuration,
        CancellationToken ct)
    {
        const string stageName = "ScriptGeneration";
        context.CurrentStage = stageName;
        var sw = Stopwatch.StartNew();

        _logger.LogInformation("[{CorrelationId}] Stage: {Stage}", context.CorrelationId, stageName);
        progress?.Report(ProgressBuilder.CreateScriptProgress(0, "Generating script with AI", context.CorrelationId));

        try
        {
            // Check circuit breaker
            if (!_circuitBreaker.CanExecute("LLM"))
            {
                throw new ProviderException(
                    "LLM",
                    ProviderType.Llm,
                    "Circuit breaker is open",
                    "CIRCUIT_OPEN");
            }

            var script = await _retryWrapper.ExecuteWithRetryAsync(
                async (ctRetry) =>
                {
                    progress?.Report(ProgressBuilder.CreateScriptProgress(30, "Calling LLM provider", context.CorrelationId));
                    
                    var generatedScript = await _llmProvider.DraftScriptAsync(
                        context.Brief,
                        context.PlanSpec,
                        ctRetry);

                    progress?.Report(ProgressBuilder.CreateScriptProgress(60, "Validating script quality", context.CorrelationId));

                    // Validate script
                    var structuralValidation = _scriptValidator.Validate(generatedScript, context.PlanSpec);
                    var contentValidation = _llmValidator.ValidateScriptContent(generatedScript, context.PlanSpec);

                    if (!structuralValidation.IsValid || !contentValidation.IsValid)
                    {
                        var allIssues = structuralValidation.Issues.Concat(contentValidation.Issues).ToList();
                        _logger.LogWarning("[{CorrelationId}] Script validation failed: {Issues}",
                            context.CorrelationId, string.Join(", ", allIssues));
                        throw new ValidationException("Script validation failed", allIssues);
                    }

                    return generatedScript;
                },
                "Script Generation",
                ct,
                maxRetries: configuration.MaxRetryAttempts,
                providerName: "LLM");

            context.GeneratedScript = script;
            context.SetStageOutput(stageName, script);
            _circuitBreaker.RecordSuccess("LLM");

            progress?.Report(ProgressBuilder.CreateScriptProgress(100, "Script generated successfully", context.CorrelationId));

            RecordStageMetrics(context, stageName, sw, 
                itemsProcessed: 1,
                providerUsed: "LLM",
                providerModel: _llmProvider.GetType().Name);

            // Create checkpoint
            if (configuration.EnableCheckpoints && _checkpointManager != null)
            {
                await SaveCheckpointAsync(context, stageName, ct);
            }
        }
        catch (Exception ex)
        {
            _circuitBreaker.RecordFailure("LLM", ex);
            context.RecordError(stageName, ex, isRecoverable: true);
            throw;
        }
    }

    private async Task ExecuteSceneParsingStageAsync(
        PipelineContext context,
        IProgress<GenerationProgress>? progress,
        CancellationToken ct)
    {
        const string stageName = "SceneParsing";
        context.CurrentStage = stageName;
        var sw = Stopwatch.StartNew();

        _logger.LogInformation("[{CorrelationId}] Stage: {Stage}", context.CorrelationId, stageName);
        progress?.Report(ProgressBuilder.CreateScriptProgress(100, "Parsing script into scenes", context.CorrelationId));

        try
        {
            if (string.IsNullOrEmpty(context.GeneratedScript))
            {
                throw new InvalidOperationException("No script available for parsing");
            }

            var scenes = ParseScriptIntoScenes(context.GeneratedScript, context.PlanSpec.TargetDuration);
            context.ParsedScenes = scenes;
            context.SetStageOutput(stageName, scenes);

            _logger.LogInformation("[{CorrelationId}] Parsed {Count} scenes", context.CorrelationId, scenes.Count);

            // Stream scenes to channel for downstream processing
            foreach (var scene in scenes)
            {
                await context.SceneChannel.Writer.WriteAsync(scene, ct);
            }

            RecordStageMetrics(context, stageName, sw, itemsProcessed: scenes.Count);
        }
        catch (Exception ex)
        {
            context.RecordError(stageName, ex, isRecoverable: false);
            throw;
        }
    }

    private async Task ExecuteVoiceGenerationStageAsync(
        PipelineContext context,
        IProgress<GenerationProgress>? progress,
        PipelineConfiguration configuration,
        CancellationToken ct)
    {
        const string stageName = "VoiceGeneration";
        context.CurrentStage = stageName;
        var sw = Stopwatch.StartNew();

        _logger.LogInformation("[{CorrelationId}] Stage: {Stage}", context.CorrelationId, stageName);
        progress?.Report(ProgressBuilder.CreateTtsProgress(0, "Generating voice narration", correlationId: context.CorrelationId));

        try
        {
            if (context.ParsedScenes == null || context.ParsedScenes.Count == 0)
            {
                throw new InvalidOperationException("No scenes available for voice generation");
            }

            // Check circuit breaker
            if (!_circuitBreaker.CanExecute("TTS"))
            {
                throw new ProviderException(
                    "TTS",
                    ProviderType.Tts,
                    "Circuit breaker is open",
                    "CIRCUIT_OPEN");
            }

            var scriptLines = ConvertScenesToScriptLines(context.ParsedScenes);

            var narrationPath = await _retryWrapper.ExecuteWithRetryAsync(
                async (ctRetry) =>
                {
                    progress?.Report(ProgressBuilder.CreateTtsProgress(30, "Synthesizing audio", correlationId: context.CorrelationId));

                    var audioPath = await _ttsProvider.SynthesizeAsync(
                        scriptLines,
                        context.VoiceSpec,
                        ctRetry);

                    progress?.Report(ProgressBuilder.CreateTtsProgress(70, "Validating audio quality", correlationId: context.CorrelationId));

                    // Validate audio output
                    var minDuration = TimeSpan.FromSeconds(Math.Max(5, context.PlanSpec.TargetDuration.TotalSeconds * 0.3));
                    var audioValidation = _ttsValidator.ValidateAudioFile(audioPath, minDuration);

                    if (!audioValidation.IsValid)
                    {
                        _logger.LogWarning("[{CorrelationId}] Audio validation failed: {Issues}",
                            context.CorrelationId, string.Join(", ", audioValidation.Issues));
                        throw new ValidationException("Audio validation failed", audioValidation.Issues);
                    }

                    _cleanupManager.RegisterTempFile(audioPath);
                    return audioPath;
                },
                "Voice Generation",
                ct,
                maxRetries: configuration.MaxRetryAttempts,
                providerName: "TTS");

            context.NarrationPath = narrationPath;
            context.SetStageOutput(stageName, narrationPath);
            _circuitBreaker.RecordSuccess("TTS");

            progress?.Report(ProgressBuilder.CreateTtsProgress(100, "Voice generation complete", correlationId: context.CorrelationId));

            RecordStageMetrics(context, stageName, sw,
                itemsProcessed: scriptLines.Count,
                providerUsed: "TTS",
                providerModel: _ttsProvider.GetType().Name);

            // Create checkpoint
            if (configuration.EnableCheckpoints && _checkpointManager != null)
            {
                await SaveCheckpointAsync(context, stageName, ct);
            }
        }
        catch (Exception ex)
        {
            _circuitBreaker.RecordFailure("TTS", ex);
            context.RecordError(stageName, ex, isRecoverable: true);
            throw;
        }
    }

    private async Task ExecuteVisualGenerationStageAsync(
        PipelineContext context,
        IProgress<GenerationProgress>? progress,
        PipelineConfiguration configuration,
        CancellationToken ct)
    {
        const string stageName = "VisualGeneration";
        context.CurrentStage = stageName;
        var sw = Stopwatch.StartNew();

        _logger.LogInformation("[{CorrelationId}] Stage: {Stage}", context.CorrelationId, stageName);
        progress?.Report(ProgressBuilder.CreateImageProgress(0, "Generating visual assets", correlationId: context.CorrelationId));

        try
        {
            if (context.ParsedScenes == null || _imageProvider == null)
            {
                _logger.LogWarning("[{CorrelationId}] Skipping visual generation - no scenes or image provider", context.CorrelationId);
                return;
            }

            // Check circuit breaker
            if (!_circuitBreaker.CanExecute("ImageProvider"))
            {
                _logger.LogWarning("[{CorrelationId}] Image provider circuit breaker is open, skipping visual generation", 
                    context.CorrelationId);
                return;
            }

            var totalScenes = context.ParsedScenes.Count;
            var visualSpec = new VisualSpec(context.PlanSpec.Style, context.Brief.Aspect, Array.Empty<string>());

            // Process scenes in parallel with concurrency limit
            var tasks = context.ParsedScenes.Select(async (scene, index) =>
            {
                await _concurrencySemaphore.WaitAsync(ct);
                try
                {
                    progress?.Report(ProgressBuilder.CreateImageProgress(
                        (index * 100.0) / totalScenes,
                        $"Generating assets for scene {index + 1}",
                        index + 1,
                        totalScenes,
                        context.CorrelationId));

                    var assets = await _retryWrapper.ExecuteWithRetryAsync(
                        async (ctRetry) =>
                        {
                            var generatedAssets = await _imageProvider.FetchOrGenerateAsync(scene, visualSpec, ctRetry);

                            // Validate images (lenient - don't fail pipeline if no assets)
                            var validation = _imageValidator.ValidateImageAssets(generatedAssets, expectedMinCount: 0);
                            if (!validation.IsValid)
                            {
                                _logger.LogWarning("[{CorrelationId}] Image validation warning for scene {Index}: {Issues}",
                                    context.CorrelationId, index, string.Join(", ", validation.Issues));
                            }

                            // Register for cleanup
                            foreach (var asset in generatedAssets)
                            {
                                if (!string.IsNullOrEmpty(asset.PathOrUrl) &&
                                    !asset.PathOrUrl.StartsWith("http", StringComparison.OrdinalIgnoreCase))
                                {
                                    _cleanupManager.RegisterTempFile(asset.PathOrUrl);
                                }
                            }

                            return generatedAssets;
                        },
                        $"Image Generation (Scene {index})",
                        ct,
                        maxRetries: configuration.MaxRetryAttempts,
                        providerName: "ImageProvider");

                    // Stream to channel
                    await context.AssetChannel.Writer.WriteAsync(
                        new AssetBatch { SceneIndex = index, Assets = assets },
                        ct);

                    return (index, assets);
                }
                finally
                {
                    _concurrencySemaphore.Release();
                }
            });

            var results = await Task.WhenAll(tasks);

            // Collect results
            foreach (var (index, assets) in results)
            {
                context.SceneAssets[index] = assets;
            }

            context.AssetChannel.Writer.Complete();
            _circuitBreaker.RecordSuccess("ImageProvider");

            progress?.Report(ProgressBuilder.CreateImageProgress(100, "Visual generation complete", correlationId: context.CorrelationId));

            RecordStageMetrics(context, stageName, sw,
                itemsProcessed: totalScenes,
                providerUsed: "ImageProvider",
                providerModel: _imageProvider.GetType().Name);

            // Create checkpoint
            if (configuration.EnableCheckpoints && _checkpointManager != null)
            {
                await SaveCheckpointAsync(context, stageName, ct);
            }
        }
        catch (Exception ex)
        {
            _circuitBreaker.RecordFailure("ImageProvider", ex);
            context.RecordError(stageName, ex, isRecoverable: true);
            
            // Don't throw - visual generation is optional
            _logger.LogWarning(ex, "[{CorrelationId}] Visual generation failed but continuing", context.CorrelationId);
        }
    }

    private async Task ExecuteRenderingStageAsync(
        PipelineContext context,
        IProgress<GenerationProgress>? progress,
        PipelineConfiguration configuration,
        CancellationToken ct)
    {
        const string stageName = "Rendering";
        context.CurrentStage = stageName;
        var sw = Stopwatch.StartNew();

        _logger.LogInformation("[{CorrelationId}] Stage: {Stage}", context.CorrelationId, stageName);
        progress?.Report(ProgressBuilder.CreateRenderProgress(0, "Rendering final video", correlationId: context.CorrelationId));

        try
        {
            if (context.ParsedScenes == null || string.IsNullOrEmpty(context.NarrationPath))
            {
                throw new InvalidOperationException("Missing scenes or narration for rendering");
            }

            var timeline = new Providers.Timeline(
                Scenes: context.ParsedScenes,
                SceneAssets: context.SceneAssets,
                NarrationPath: context.NarrationPath,
                MusicPath: context.MusicPath ?? string.Empty,
                SubtitlesPath: context.SubtitlesPath);

            var renderProgress = new Progress<RenderProgress>(p =>
            {
                progress?.Report(ProgressBuilder.CreateRenderProgress(
                    p.Percentage,
                    $"Rendering: {p.CurrentStage}",
                    correlationId: context.CorrelationId));
            });

            var outputPath = await _retryWrapper.ExecuteWithRetryAsync(
                async (ctRetry) =>
                {
                    return await _videoComposer.RenderAsync(timeline, context.RenderSpec, renderProgress, ctRetry);
                },
                "Video Rendering",
                ct,
                maxRetries: 1, // Rendering failures are typically not recoverable
                providerName: "VideoComposer");

            context.FinalVideoPath = outputPath;
            context.SetStageOutput(stageName, outputPath);

            progress?.Report(ProgressBuilder.CreateRenderProgress(100, "Rendering complete", correlationId: context.CorrelationId));

            RecordStageMetrics(context, stageName, sw,
                itemsProcessed: 1,
                providerUsed: "VideoComposer",
                providerModel: "FFmpeg");
        }
        catch (Exception ex)
        {
            context.RecordError(stageName, ex, isRecoverable: false);
            throw;
        }
    }

    #endregion

    #region Helper Methods

    private List<Scene> ParseScriptIntoScenes(string script, TimeSpan targetDuration)
    {
        var scenes = new List<Scene>();
        var lines = script.Split('\n', StringSplitOptions.RemoveEmptyEntries);

        string? currentHeading = null;
        var currentScriptLines = new List<string>();
        int sceneIndex = 0;

        foreach (var line in lines)
        {
            if (line.StartsWith("## "))
            {
                if (currentHeading != null && currentScriptLines.Count > 0)
                {
                    var sceneScript = string.Join("\n", currentScriptLines);
                    scenes.Add(new Scene(sceneIndex++, currentHeading, sceneScript, TimeSpan.Zero, TimeSpan.Zero));
                    currentScriptLines.Clear();
                }
                currentHeading = line.Substring(3).Trim();
            }
            else if (!line.StartsWith("#") && !string.IsNullOrWhiteSpace(line))
            {
                currentScriptLines.Add(line);
            }
        }

        if (currentHeading != null && currentScriptLines.Count > 0)
        {
            var sceneScript = string.Join("\n", currentScriptLines);
            scenes.Add(new Scene(sceneIndex++, currentHeading, sceneScript, TimeSpan.Zero, TimeSpan.Zero));
        }

        // Calculate timings
        int totalWords = scenes.Sum(s => CountWords(s.Script));
        TimeSpan currentStart = TimeSpan.Zero;

        for (int i = 0; i < scenes.Count; i++)
        {
            int sceneWords = CountWords(scenes[i].Script);
            double proportion = totalWords > 0 ? (double)sceneWords / totalWords : 1.0 / scenes.Count;
            TimeSpan duration = TimeSpan.FromSeconds(targetDuration.TotalSeconds * proportion);

            scenes[i] = scenes[i] with
            {
                Start = currentStart,
                Duration = duration
            };

            currentStart += duration;
        }

        return scenes;
    }

    private List<ScriptLine> ConvertScenesToScriptLines(List<Scene> scenes)
    {
        return scenes.Select(scene => new ScriptLine(
            SceneIndex: scene.Index,
            Text: scene.Script,
            Start: scene.Start,
            Duration: scene.Duration
        )).ToList();
    }

    private int CountWords(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return 0;

        return text.Split(new[] { ' ', '\t', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries).Length;
    }

    private void RecordStageMetrics(
        PipelineContext context,
        string stageName,
        Stopwatch sw,
        int itemsProcessed = 0,
        int itemsFailed = 0,
        string? providerUsed = null,
        string? providerModel = null)
    {
        sw.Stop();
        
        var metrics = new PipelineStageMetrics
        {
            StageName = stageName,
            StartTime = DateTime.UtcNow - sw.Elapsed,
            EndTime = DateTime.UtcNow,
            MemoryUsedBytes = GC.GetTotalMemory(false),
            ItemsProcessed = itemsProcessed,
            ItemsFailed = itemsFailed,
            ProviderUsed = providerUsed,
            ProviderModel = providerModel
        };

        context.RecordStageMetrics(stageName, metrics);

        _logger.LogInformation(
            "[{CorrelationId}] Stage {Stage} completed in {Duration:F2}s, processed {Items} items",
            context.CorrelationId, stageName, metrics.Duration.TotalSeconds, itemsProcessed);
    }

    private async Task SaveCheckpointAsync(PipelineContext context, string stageName, CancellationToken ct)
    {
        if (_checkpointManager == null) return;

        try
        {
            if (!context.CheckpointProjectId.HasValue)
            {
                context.CheckpointProjectId = await _checkpointManager.CreateProjectStateAsync(
                    context.Brief.Topic ?? "Untitled",
                    context.CorrelationId,
                    context.Brief,
                    context.PlanSpec,
                    context.VoiceSpec,
                    context.RenderSpec,
                    ct);
            }

            var completedScenes = context.ParsedScenes?.Count ?? 0;
            var totalScenes = context.ParsedScenes?.Count ?? 0;

            await _checkpointManager.SaveCheckpointAsync(
                context.CheckpointProjectId.Value,
                stageName,
                completedScenes,
                totalScenes,
                outputFilePath: context.FinalVideoPath,
                ct: ct);

            context.LastCheckpointStage = stageName;

            _logger.LogDebug("[{CorrelationId}] Checkpoint saved for stage {Stage}",
                context.CorrelationId, stageName);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[{CorrelationId}] Failed to save checkpoint for stage {Stage}",
                context.CorrelationId, stageName);
        }
    }

    private void LogPipelineMetrics(PipelineContext context)
    {
        var metrics = context.GetAllMetrics();
        if (metrics.Count == 0) return;

        _logger.LogInformation(
            "[{CorrelationId}] Pipeline Metrics Summary:",
            context.CorrelationId);

        foreach (var (stage, metric) in metrics)
        {
            _logger.LogInformation(
                "[{CorrelationId}]   {Stage}: {Duration:F2}s ({Items} items)",
                context.CorrelationId, stage, metric.Duration.TotalSeconds, metric.ItemsProcessed);
        }

        var totalDuration = metrics.Values.Sum(m => m.Duration.TotalSeconds);
        _logger.LogInformation(
            "[{CorrelationId}] Total pipeline time: {Duration:F2}s",
            context.CorrelationId, totalDuration);
    }

    #endregion

    public async ValueTask DisposeAsync()
    {
        if (_disposed) return;

        try
        {
            _concurrencySemaphore?.Dispose();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error disposing semaphore");
        }

        _disposed = true;
        GC.SuppressFinalize(this);
    }
}
