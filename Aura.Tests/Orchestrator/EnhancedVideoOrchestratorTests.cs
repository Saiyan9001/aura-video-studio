using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Aura.Core.Errors;
using Aura.Core.Models;
using Aura.Core.Orchestrator;
using Aura.Core.Providers;
using Aura.Core.Services;
using Aura.Core.Services.Providers;
using Aura.Core.Telemetry;
using Aura.Core.Validation;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace Aura.Tests.Orchestrator;

/// <summary>
/// Comprehensive unit tests for EnhancedVideoOrchestrator
/// Tests cover all pipeline stages, error handling, circuit breaker, retry logic, and resource disposal
/// </summary>
public class EnhancedVideoOrchestratorTests : IDisposable
{
    private readonly Mock<ILogger<EnhancedVideoOrchestrator>> _mockLogger;
    private readonly Mock<ILlmProvider> _mockLlmProvider;
    private readonly Mock<ITtsProvider> _mockTtsProvider;
    private readonly Mock<IVideoComposer> _mockVideoComposer;
    private readonly Mock<IImageProvider> _mockImageProvider;
    private readonly Mock<ProviderCircuitBreakerService> _mockCircuitBreaker;
    private readonly Mock<ProviderRetryWrapper> _mockRetryWrapper;
    private readonly Mock<PreGenerationValidator> _mockPreValidator;
    private readonly Mock<ScriptValidator> _mockScriptValidator;
    private readonly Mock<TtsOutputValidator> _mockTtsValidator;
    private readonly Mock<ImageOutputValidator> _mockImageValidator;
    private readonly Mock<LlmOutputValidator> _mockLlmValidator;
    private readonly Mock<ResourceCleanupManager> _mockCleanupManager;
    private readonly Mock<CheckpointManager> _mockCheckpointManager;
    private readonly Mock<RunTelemetryCollector> _mockTelemetryCollector;
    
    private EnhancedVideoOrchestrator _orchestrator;
    private readonly Brief _testBrief;
    private readonly PlanSpec _testPlanSpec;
    private readonly VoiceSpec _testVoiceSpec;
    private readonly RenderSpec _testRenderSpec;
    private readonly SystemProfile _testSystemProfile;

    public EnhancedVideoOrchestratorTests()
    {
        // Create mocks
        _mockLogger = new Mock<ILogger<EnhancedVideoOrchestrator>>();
        _mockLlmProvider = new Mock<ILlmProvider>();
        _mockTtsProvider = new Mock<ITtsProvider>();
        _mockVideoComposer = new Mock<IVideoComposer>();
        _mockImageProvider = new Mock<IImageProvider>();
        _mockCircuitBreaker = new Mock<ProviderCircuitBreakerService>(
            Mock.Of<ILogger<ProviderCircuitBreakerService>>());
        _mockRetryWrapper = new Mock<ProviderRetryWrapper>(
            Mock.Of<ILogger<ProviderRetryWrapper>>(), null);
        _mockPreValidator = new Mock<PreGenerationValidator>(
            Mock.Of<ILogger<PreGenerationValidator>>());
        _mockScriptValidator = new Mock<ScriptValidator>(
            Mock.Of<ILogger<ScriptValidator>>());
        _mockTtsValidator = new Mock<TtsOutputValidator>(
            Mock.Of<ILogger<TtsOutputValidator>>());
        _mockImageValidator = new Mock<ImageOutputValidator>(
            Mock.Of<ILogger<ImageOutputValidator>>());
        _mockLlmValidator = new Mock<LlmOutputValidator>(
            Mock.Of<ILogger<LlmOutputValidator>>());
        _mockCleanupManager = new Mock<ResourceCleanupManager>(
            Mock.Of<ILogger<ResourceCleanupManager>>());
        _mockCheckpointManager = new Mock<CheckpointManager>(
            Mock.Of<Data.ProjectStateRepository>(),
            Mock.Of<ILogger<CheckpointManager>>());
        _mockTelemetryCollector = new Mock<RunTelemetryCollector>(
            Mock.Of<ILogger<RunTelemetryCollector>>());

        // Test data
        _testBrief = new Brief
        {
            Topic = "Test Topic",
            Audience = "General",
            Goal = "Educational",
            Aspect = Aspect.Landscape_16_9
        };

        _testPlanSpec = new PlanSpec
        {
            TargetDuration = TimeSpan.FromSeconds(30),
            Style = "Professional"
        };

        _testVoiceSpec = new VoiceSpec
        {
            VoiceName = "TestVoice",
            Speed = 1.0
        };

        _testRenderSpec = new RenderSpec
        {
            Res = new Resolution { Width = 1920, Height = 1080 },
            Fps = 30,
            Codec = "h264"
        };

        _testSystemProfile = new SystemProfile
        {
            Tier = HardwareTier.B,
            LogicalCores = 8,
            PhysicalCores = 4,
            RamGB = 16
        };

        // Create orchestrator
        _orchestrator = new EnhancedVideoOrchestrator(
            _mockLogger.Object,
            _mockLlmProvider.Object,
            _mockTtsProvider.Object,
            _mockVideoComposer.Object,
            _mockCircuitBreaker.Object,
            _mockRetryWrapper.Object,
            _mockPreValidator.Object,
            _mockScriptValidator.Object,
            _mockTtsValidator.Object,
            _mockImageValidator.Object,
            _mockLlmValidator.Object,
            _mockCleanupManager.Object,
            _mockTelemetryCollector.Object,
            _mockImageProvider.Object,
            _mockCheckpointManager.Object);
    }

    #region Constructor Tests

    [Fact]
    public void Constructor_WithValidDependencies_CreatesInstance()
    {
        // Assert
        Assert.NotNull(_orchestrator);
    }

    [Fact]
    public void Constructor_WithNullLogger_ThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => new EnhancedVideoOrchestrator(
            null!,
            _mockLlmProvider.Object,
            _mockTtsProvider.Object,
            _mockVideoComposer.Object,
            _mockCircuitBreaker.Object,
            _mockRetryWrapper.Object,
            _mockPreValidator.Object,
            _mockScriptValidator.Object,
            _mockTtsValidator.Object,
            _mockImageValidator.Object,
            _mockLlmValidator.Object,
            _mockCleanupManager.Object,
            _mockTelemetryCollector.Object));
    }

    #endregion

    #region Full Pipeline Tests

    [Fact]
    public async Task GenerateVideoAsync_WithValidInput_CompletesSuccessfully()
    {
        // Arrange
        var testScript = "## Introduction\nTest content\n## Conclusion\nMore content";
        var testAudioPath = "/tmp/test_audio.wav";
        var testVideoPath = "/tmp/test_video.mp4";

        SetupSuccessfulPipeline(testScript, testAudioPath, testVideoPath);

        // Act
        var result = await _orchestrator.GenerateVideoAsync(
            _testBrief,
            _testPlanSpec,
            _testVoiceSpec,
            _testRenderSpec,
            _testSystemProfile,
            null,
            null,
            CancellationToken.None);

        // Assert
        Assert.Equal(testVideoPath, result);
        
        // Verify all stages executed
        _mockPreValidator.Verify(v => v.ValidateSystemReadyAsync(
            It.IsAny<Brief>(), It.IsAny<PlanSpec>(), It.IsAny<CancellationToken>()), Times.Once);
        
        VerifyScriptGeneration();
        VerifyVoiceGeneration();
        VerifyRendering();
    }

    [Fact]
    public async Task GenerateVideoAsync_WithProgressReporting_ReportsAllStages()
    {
        // Arrange
        var progressReports = new List<GenerationProgress>();
        var progress = new Progress<GenerationProgress>(p => progressReports.Add(p));
        
        SetupSuccessfulPipeline("## Test\nContent", "/tmp/audio.wav", "/tmp/video.mp4");

        // Act
        await _orchestrator.GenerateVideoAsync(
            _testBrief,
            _testPlanSpec,
            _testVoiceSpec,
            _testRenderSpec,
            _testSystemProfile,
            progress,
            null,
            CancellationToken.None);

        // Assert
        Assert.NotEmpty(progressReports);
        
        // Verify we have progress reports for all major stages
        Assert.Contains(progressReports, p => p.Stage.Contains("Brief", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(progressReports, p => p.Stage.Contains("Script", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(progressReports, p => p.Stage.Contains("TTS", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(progressReports, p => p.Stage.Contains("Render", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task GenerateVideoAsync_WithCheckpointsEnabled_CreatesCheckpoints()
    {
        // Arrange
        var config = new PipelineConfiguration
        {
            EnableCheckpoints = true,
            CheckpointFrequency = 1
        };
        
        SetupSuccessfulPipeline("## Test\nContent", "/tmp/audio.wav", "/tmp/video.mp4");

        _mockCheckpointManager
            .Setup(m => m.CreateProjectStateAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<Brief>(),
                It.IsAny<PlanSpec>(), It.IsAny<VoiceSpec>(), It.IsAny<RenderSpec>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(Guid.NewGuid());

        // Act
        await _orchestrator.GenerateVideoAsync(
            _testBrief,
            _testPlanSpec,
            _testVoiceSpec,
            _testRenderSpec,
            _testSystemProfile,
            null,
            config,
            CancellationToken.None);

        // Assert
        _mockCheckpointManager.Verify(m => m.CreateProjectStateAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<Brief>(),
            It.IsAny<PlanSpec>(), It.IsAny<VoiceSpec>(), It.IsAny<RenderSpec>(),
            It.IsAny<CancellationToken>()), Times.Once);
        
        _mockCheckpointManager.Verify(m => m.SaveCheckpointAsync(
            It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<int>(), It.IsAny<int>(),
            It.IsAny<Dictionary<string, object>>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.AtLeastOnce);
    }

    #endregion

    #region Circuit Breaker Tests

    [Fact]
    public async Task GenerateVideoAsync_WhenCircuitBreakerOpen_ThrowsProviderException()
    {
        // Arrange
        SetupValidation();
        
        _mockCircuitBreaker
            .Setup(cb => cb.CanExecute("LLM"))
            .Returns(false);

        // Act & Assert
        await Assert.ThrowsAsync<ProviderException>(() => _orchestrator.GenerateVideoAsync(
            _testBrief,
            _testPlanSpec,
            _testVoiceSpec,
            _testRenderSpec,
            _testSystemProfile,
            null,
            null,
            CancellationToken.None));
    }

    [Fact]
    public async Task GenerateVideoAsync_OnProviderSuccess_RecordsCircuitBreakerSuccess()
    {
        // Arrange
        SetupSuccessfulPipeline("## Test\nContent", "/tmp/audio.wav", "/tmp/video.mp4");

        // Act
        await _orchestrator.GenerateVideoAsync(
            _testBrief,
            _testPlanSpec,
            _testVoiceSpec,
            _testRenderSpec,
            _testSystemProfile,
            null,
            null,
            CancellationToken.None);

        // Assert
        _mockCircuitBreaker.Verify(cb => cb.RecordSuccess("LLM"), Times.Once);
        _mockCircuitBreaker.Verify(cb => cb.RecordSuccess("TTS"), Times.Once);
    }

    [Fact]
    public async Task GenerateVideoAsync_OnProviderFailure_RecordsCircuitBreakerFailure()
    {
        // Arrange
        SetupValidation();
        
        _mockCircuitBreaker.Setup(cb => cb.CanExecute("LLM")).Returns(true);
        
        var testException = new Exception("Provider failed");
        _mockRetryWrapper
            .Setup(r => r.ExecuteWithRetryAsync(
                It.IsAny<Func<CancellationToken, Task<string>>>(),
                It.Is<string>(s => s.Contains("Script")),
                It.IsAny<CancellationToken>(),
                It.IsAny<int>(),
                It.IsAny<Services.RetryNotificationHandler>(),
                It.IsAny<string>()))
            .ThrowsAsync(testException);

        // Act & Assert
        await Assert.ThrowsAsync<Exception>(() => _orchestrator.GenerateVideoAsync(
            _testBrief,
            _testPlanSpec,
            _testVoiceSpec,
            _testRenderSpec,
            _testSystemProfile,
            null,
            null,
            CancellationToken.None));

        // Assert
        _mockCircuitBreaker.Verify(cb => cb.RecordFailure("LLM", testException), Times.Once);
    }

    #endregion

    #region Retry Logic Tests

    [Fact]
    public async Task GenerateVideoAsync_UsesRetryWrapper_ForAllProviderCalls()
    {
        // Arrange
        SetupSuccessfulPipeline("## Test\nContent", "/tmp/audio.wav", "/tmp/video.mp4");

        // Act
        await _orchestrator.GenerateVideoAsync(
            _testBrief,
            _testPlanSpec,
            _testVoiceSpec,
            _testRenderSpec,
            _testSystemProfile,
            null,
            null,
            CancellationToken.None);

        // Assert - Verify retry wrapper used for critical operations
        _mockRetryWrapper.Verify(r => r.ExecuteWithRetryAsync(
            It.IsAny<Func<CancellationToken, Task<string>>>(),
            It.Is<string>(s => s.Contains("Script")),
            It.IsAny<CancellationToken>(),
            It.IsAny<int>(),
            It.IsAny<Services.RetryNotificationHandler>(),
            It.IsAny<string>()), Times.Once);

        _mockRetryWrapper.Verify(r => r.ExecuteWithRetryAsync(
            It.IsAny<Func<CancellationToken, Task<string>>>(),
            It.Is<string>(s => s.Contains("Voice")),
            It.IsAny<CancellationToken>(),
            It.IsAny<int>(),
            It.IsAny<Services.RetryNotificationHandler>(),
            It.IsAny<string>()), Times.Once);
    }

    [Fact]
    public async Task GenerateVideoAsync_WithCustomRetryConfig_UsesSpecifiedRetryCount()
    {
        // Arrange
        var config = new PipelineConfiguration
        {
            MaxRetryAttempts = 5
        };
        
        SetupSuccessfulPipeline("## Test\nContent", "/tmp/audio.wav", "/tmp/video.mp4");

        // Act
        await _orchestrator.GenerateVideoAsync(
            _testBrief,
            _testPlanSpec,
            _testVoiceSpec,
            _testRenderSpec,
            _testSystemProfile,
            null,
            config,
            CancellationToken.None);

        // Assert
        _mockRetryWrapper.Verify(r => r.ExecuteWithRetryAsync(
            It.IsAny<Func<CancellationToken, Task<string>>>(),
            It.IsAny<string>(),
            It.IsAny<CancellationToken>(),
            5, // Custom retry count
            It.IsAny<Services.RetryNotificationHandler>(),
            It.IsAny<string>()), Times.AtLeastOnce);
    }

    #endregion

    #region Validation Tests

    [Fact]
    public async Task GenerateVideoAsync_WhenValidationFails_ThrowsValidationException()
    {
        // Arrange
        _mockPreValidator
            .Setup(v => v.ValidateSystemReadyAsync(It.IsAny<Brief>(), It.IsAny<PlanSpec>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ValidationResult
            {
                IsValid = false,
                Issues = new List<string> { "Test validation error" }
            });

        // Act & Assert
        await Assert.ThrowsAsync<ValidationException>(() => _orchestrator.GenerateVideoAsync(
            _testBrief,
            _testPlanSpec,
            _testVoiceSpec,
            _testRenderSpec,
            _testSystemProfile,
            null,
            null,
            CancellationToken.None));
    }

    [Fact]
    public async Task GenerateVideoAsync_ValidatesScriptOutput()
    {
        // Arrange
        var testScript = "## Test\nContent";
        SetupValidation();
        SetupScriptGeneration(testScript);
        
        _mockScriptValidator
            .Setup(v => v.Validate(testScript, _testPlanSpec))
            .Returns(new ValidationResult
            {
                IsValid = false,
                Issues = new List<string> { "Script too short" }
            });

        _mockCircuitBreaker.Setup(cb => cb.CanExecute(It.IsAny<string>())).Returns(true);

        // Act & Assert
        await Assert.ThrowsAsync<ValidationException>(() => _orchestrator.GenerateVideoAsync(
            _testBrief,
            _testPlanSpec,
            _testVoiceSpec,
            _testRenderSpec,
            _testSystemProfile,
            null,
            null,
            CancellationToken.None));
    }

    #endregion

    #region Cancellation Tests

    [Fact]
    public async Task GenerateVideoAsync_WhenCancelled_ThrowsOperationCanceledException()
    {
        // Arrange
        var cts = new CancellationTokenSource();
        cts.Cancel();

        SetupValidation();

        // Act & Assert
        await Assert.ThrowsAsync<OperationCanceledException>(() => _orchestrator.GenerateVideoAsync(
            _testBrief,
            _testPlanSpec,
            _testVoiceSpec,
            _testRenderSpec,
            _testSystemProfile,
            null,
            null,
            cts.Token));
    }

    [Fact]
    public async Task GenerateVideoAsync_WhenCancelled_CleansUpResources()
    {
        // Arrange
        var cts = new CancellationTokenSource();
        
        SetupValidation();
        
        _mockCircuitBreaker.Setup(cb => cb.CanExecute(It.IsAny<string>())).Returns(true);
        
        _mockRetryWrapper
            .Setup(r => r.ExecuteWithRetryAsync(
                It.IsAny<Func<CancellationToken, Task<string>>>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>(),
                It.IsAny<int>(),
                It.IsAny<Services.RetryNotificationHandler>(),
                It.IsAny<string>()))
            .Returns(async (Func<CancellationToken, Task<string>> func, string name, CancellationToken ct, int max, Services.RetryNotificationHandler handler, string prov) =>
            {
                cts.Cancel();
                return await func(ct);
            });

        // Act & Assert
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => _orchestrator.GenerateVideoAsync(
            _testBrief,
            _testPlanSpec,
            _testVoiceSpec,
            _testRenderSpec,
            _testSystemProfile,
            null,
            null,
            cts.Token));

        // Assert cleanup was called
        _mockCleanupManager.Verify(c => c.CleanupAll(), Times.Once);
    }

    #endregion

    #region Resource Disposal Tests

    [Fact]
    public async Task DisposeAsync_ReleasesAllResources()
    {
        // Arrange
        var orchestrator = new EnhancedVideoOrchestrator(
            _mockLogger.Object,
            _mockLlmProvider.Object,
            _mockTtsProvider.Object,
            _mockVideoComposer.Object,
            _mockCircuitBreaker.Object,
            _mockRetryWrapper.Object,
            _mockPreValidator.Object,
            _mockScriptValidator.Object,
            _mockTtsValidator.Object,
            _mockImageValidator.Object,
            _mockLlmValidator.Object,
            _mockCleanupManager.Object,
            _mockTelemetryCollector.Object);

        // Act
        await orchestrator.DisposeAsync();

        // Assert - should not throw
        await orchestrator.DisposeAsync(); // Double disposal should be safe
    }

    [Fact]
    public async Task GenerateVideoAsync_AlwaysCleansUpResources_EvenOnFailure()
    {
        // Arrange
        SetupValidation();
        
        _mockCircuitBreaker.Setup(cb => cb.CanExecute(It.IsAny<string>())).Returns(true);
        
        _mockRetryWrapper
            .Setup(r => r.ExecuteWithRetryAsync(
                It.IsAny<Func<CancellationToken, Task<string>>>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>(),
                It.IsAny<int>(),
                It.IsAny<Services.RetryNotificationHandler>(),
                It.IsAny<string>()))
            .ThrowsAsync(new Exception("Test failure"));

        // Act & Assert
        await Assert.ThrowsAsync<Exception>(() => _orchestrator.GenerateVideoAsync(
            _testBrief,
            _testPlanSpec,
            _testVoiceSpec,
            _testRenderSpec,
            _testSystemProfile,
            null,
            null,
            CancellationToken.None));

        // Assert cleanup was called
        _mockCleanupManager.Verify(c => c.CleanupAll(), Times.Once);
    }

    #endregion

    #region Helper Methods

    private void SetupValidation()
    {
        _mockPreValidator
            .Setup(v => v.ValidateSystemReadyAsync(It.IsAny<Brief>(), It.IsAny<PlanSpec>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ValidationResult { IsValid = true });
    }

    private void SetupScriptGeneration(string script)
    {
        _mockRetryWrapper
            .Setup(r => r.ExecuteWithRetryAsync(
                It.IsAny<Func<CancellationToken, Task<string>>>(),
                It.Is<string>(s => s.Contains("Script")),
                It.IsAny<CancellationToken>(),
                It.IsAny<int>(),
                It.IsAny<Services.RetryNotificationHandler>(),
                It.IsAny<string>()))
            .Returns(async (Func<CancellationToken, Task<string>> func, string name, CancellationToken ct, int max, Services.RetryNotificationHandler handler, string prov) =>
            {
                return await func(ct);
            });

        _mockLlmProvider
            .Setup(p => p.DraftScriptAsync(It.IsAny<Brief>(), It.IsAny<PlanSpec>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(script);

        _mockScriptValidator
            .Setup(v => v.Validate(It.IsAny<string>(), It.IsAny<PlanSpec>()))
            .Returns(new ValidationResult { IsValid = true });

        _mockLlmValidator
            .Setup(v => v.ValidateScriptContent(It.IsAny<string>(), It.IsAny<PlanSpec>()))
            .Returns(new ValidationResult { IsValid = true });

        _mockCircuitBreaker.Setup(cb => cb.CanExecute("LLM")).Returns(true);
    }

    private void SetupVoiceGeneration(string audioPath)
    {
        _mockRetryWrapper
            .Setup(r => r.ExecuteWithRetryAsync(
                It.IsAny<Func<CancellationToken, Task<string>>>(),
                It.Is<string>(s => s.Contains("Voice")),
                It.IsAny<CancellationToken>(),
                It.IsAny<int>(),
                It.IsAny<Services.RetryNotificationHandler>(),
                It.IsAny<string>()))
            .Returns(async (Func<CancellationToken, Task<string>> func, string name, CancellationToken ct, int max, Services.RetryNotificationHandler handler, string prov) =>
            {
                return await func(ct);
            });

        _mockTtsProvider
            .Setup(p => p.SynthesizeAsync(It.IsAny<IEnumerable<ScriptLine>>(), It.IsAny<VoiceSpec>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(audioPath);

        _mockTtsValidator
            .Setup(v => v.ValidateAudioFile(It.IsAny<string>(), It.IsAny<TimeSpan>()))
            .Returns(new ValidationResult { IsValid = true });

        _mockCircuitBreaker.Setup(cb => cb.CanExecute("TTS")).Returns(true);
    }

    private void SetupRendering(string videoPath)
    {
        _mockRetryWrapper
            .Setup(r => r.ExecuteWithRetryAsync(
                It.IsAny<Func<CancellationToken, Task<string>>>(),
                It.Is<string>(s => s.Contains("Render")),
                It.IsAny<CancellationToken>(),
                It.IsAny<int>(),
                It.IsAny<Services.RetryNotificationHandler>(),
                It.IsAny<string>()))
            .Returns(async (Func<CancellationToken, Task<string>> func, string name, CancellationToken ct, int max, Services.RetryNotificationHandler handler, string prov) =>
            {
                return await func(ct);
            });

        _mockVideoComposer
            .Setup(c => c.RenderAsync(
                It.IsAny<Providers.Timeline>(),
                It.IsAny<RenderSpec>(),
                It.IsAny<IProgress<RenderProgress>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(videoPath);
    }

    private void SetupSuccessfulPipeline(string script, string audioPath, string videoPath)
    {
        SetupValidation();
        SetupScriptGeneration(script);
        SetupVoiceGeneration(audioPath);
        SetupRendering(videoPath);
    }

    private void VerifyScriptGeneration()
    {
        _mockLlmProvider.Verify(p => p.DraftScriptAsync(
            It.IsAny<Brief>(), It.IsAny<PlanSpec>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    private void VerifyVoiceGeneration()
    {
        _mockTtsProvider.Verify(p => p.SynthesizeAsync(
            It.IsAny<IEnumerable<ScriptLine>>(), It.IsAny<VoiceSpec>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    private void VerifyRendering()
    {
        _mockVideoComposer.Verify(c => c.RenderAsync(
            It.IsAny<Providers.Timeline>(), It.IsAny<RenderSpec>(), 
            It.IsAny<IProgress<RenderProgress>>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    #endregion

    public void Dispose()
    {
        _orchestrator?.DisposeAsync().AsTask().Wait();
    }
}
