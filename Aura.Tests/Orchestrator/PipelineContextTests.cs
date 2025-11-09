using System;
using System.Linq;
using System.Threading.Channels;
using System.Threading.Tasks;
using Aura.Core.Models;
using Aura.Core.Orchestrator;
using Xunit;

namespace Aura.Tests.Orchestrator;

/// <summary>
/// Unit tests for PipelineContext class covering state management, metrics, and channels
/// </summary>
public class PipelineContextTests : IDisposable
{
    private readonly PipelineContext _context;
    private readonly Brief _testBrief;
    private readonly PlanSpec _testPlanSpec;
    private readonly VoiceSpec _testVoiceSpec;
    private readonly RenderSpec _testRenderSpec;
    private readonly SystemProfile _testSystemProfile;

    public PipelineContextTests()
    {
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

        _context = new PipelineContext(
            "test-correlation-id",
            _testBrief,
            _testPlanSpec,
            _testVoiceSpec,
            _testRenderSpec,
            _testSystemProfile);
    }

    #region Constructor Tests

    [Fact]
    public void Constructor_WithValidInputs_CreatesContext()
    {
        // Assert
        Assert.NotNull(_context);
        Assert.Equal("test-correlation-id", _context.CorrelationId);
        Assert.Equal(_testBrief, _context.Brief);
        Assert.Equal(_testPlanSpec, _context.PlanSpec);
        Assert.Equal(_testVoiceSpec, _context.VoiceSpec);
        Assert.Equal(_testRenderSpec, _context.RenderSpec);
        Assert.Equal(_testSystemProfile, _context.SystemProfile);
        Assert.Equal(PipelineState.Initialized, _context.State);
        Assert.Equal("Initialization", _context.CurrentStage);
    }

    [Fact]
    public void Constructor_WithNullCorrelationId_ThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => new PipelineContext(
            null!,
            _testBrief,
            _testPlanSpec,
            _testVoiceSpec,
            _testRenderSpec,
            _testSystemProfile));
    }

    [Fact]
    public void Constructor_CreatesChannels()
    {
        // Assert
        Assert.NotNull(_context.ScriptChannel);
        Assert.NotNull(_context.SceneChannel);
        Assert.NotNull(_context.AssetChannel);
    }

    #endregion

    #region State Management Tests

    [Fact]
    public void MarkCompleted_SetsStateAndCompletedAt()
    {
        // Act
        _context.MarkCompleted();

        // Assert
        Assert.Equal(PipelineState.Completed, _context.State);
        Assert.NotNull(_context.CompletedAt);
    }

    [Fact]
    public void MarkFailed_SetsStateAndCompletedAt()
    {
        // Act
        _context.MarkFailed();

        // Assert
        Assert.Equal(PipelineState.Failed, _context.State);
        Assert.NotNull(_context.CompletedAt);
    }

    [Fact]
    public void MarkCancelled_SetsStateAndCompletedAt()
    {
        // Act
        _context.MarkCancelled();

        // Assert
        Assert.Equal(PipelineState.Cancelled, _context.State);
        Assert.NotNull(_context.CompletedAt);
    }

    [Fact]
    public void GetElapsedTime_BeforeCompletion_ReturnsTimeSinceStart()
    {
        // Arrange
        Task.Delay(100).Wait(); // Small delay

        // Act
        var elapsed = _context.GetElapsedTime();

        // Assert
        Assert.True(elapsed.TotalMilliseconds >= 100);
    }

    [Fact]
    public void GetElapsedTime_AfterCompletion_ReturnsCompletedTime()
    {
        // Arrange
        Task.Delay(50).Wait();
        _context.MarkCompleted();
        var completedTime = _context.GetElapsedTime();
        Task.Delay(50).Wait();

        // Act
        var currentElapsed = _context.GetElapsedTime();

        // Assert
        Assert.Equal(completedTime.TotalSeconds, currentElapsed.TotalSeconds, 2); // Within 2 decimal places
    }

    #endregion

    #region Stage Output Tests

    [Fact]
    public void SetStageOutput_StoresOutput()
    {
        // Arrange
        var testOutput = "Test script content";

        // Act
        _context.SetStageOutput("ScriptGeneration", testOutput);

        // Assert
        var retrieved = _context.GetStageOutput<string>("ScriptGeneration");
        Assert.Equal(testOutput, retrieved);
    }

    [Fact]
    public void GetStageOutput_WithWrongType_ReturnsDefault()
    {
        // Arrange
        _context.SetStageOutput("ScriptGeneration", "Test script");

        // Act
        var retrieved = _context.GetStageOutput<int>("ScriptGeneration");

        // Assert
        Assert.Equal(0, retrieved); // Default for int
    }

    [Fact]
    public void GetStageOutput_ForNonExistentStage_ReturnsDefault()
    {
        // Act
        var retrieved = _context.GetStageOutput<string>("NonExistent");

        // Assert
        Assert.Null(retrieved);
    }

    [Fact]
    public void SetStageOutput_OverwritesExistingValue()
    {
        // Arrange
        _context.SetStageOutput("Test", "First value");
        
        // Act
        _context.SetStageOutput("Test", "Second value");

        // Assert
        var retrieved = _context.GetStageOutput<string>("Test");
        Assert.Equal("Second value", retrieved);
    }

    #endregion

    #region Metrics Tests

    [Fact]
    public void RecordStageMetrics_StoresMetrics()
    {
        // Arrange
        var metrics = new PipelineStageMetrics
        {
            StageName = "TestStage",
            StartTime = DateTime.UtcNow,
            EndTime = DateTime.UtcNow.AddSeconds(5),
            ItemsProcessed = 10,
            ItemsFailed = 2,
            ProviderUsed = "TestProvider"
        };

        // Act
        _context.RecordStageMetrics("TestStage", metrics);

        // Assert
        var retrieved = _context.GetStageMetrics("TestStage");
        Assert.NotNull(retrieved);
        Assert.Equal(metrics.StageName, retrieved.StageName);
        Assert.Equal(metrics.ItemsProcessed, retrieved.ItemsProcessed);
        Assert.Equal(metrics.ProviderUsed, retrieved.ProviderUsed);
    }

    [Fact]
    public void GetStageMetrics_ForNonExistentStage_ReturnsNull()
    {
        // Act
        var retrieved = _context.GetStageMetrics("NonExistent");

        // Assert
        Assert.Null(retrieved);
    }

    [Fact]
    public void GetAllMetrics_ReturnsAllRecordedMetrics()
    {
        // Arrange
        var metrics1 = CreateTestMetrics("Stage1");
        var metrics2 = CreateTestMetrics("Stage2");
        var metrics3 = CreateTestMetrics("Stage3");

        _context.RecordStageMetrics("Stage1", metrics1);
        _context.RecordStageMetrics("Stage2", metrics2);
        _context.RecordStageMetrics("Stage3", metrics3);

        // Act
        var allMetrics = _context.GetAllMetrics();

        // Assert
        Assert.Equal(3, allMetrics.Count);
        Assert.Contains(allMetrics, kvp => kvp.Key == "Stage1");
        Assert.Contains(allMetrics, kvp => kvp.Key == "Stage2");
        Assert.Contains(allMetrics, kvp => kvp.Key == "Stage3");
    }

    [Fact]
    public void PipelineStageMetrics_Duration_CalculatesCorrectly()
    {
        // Arrange
        var startTime = DateTime.UtcNow;
        var endTime = startTime.AddSeconds(10);
        
        var metrics = new PipelineStageMetrics
        {
            StartTime = startTime,
            EndTime = endTime
        };

        // Assert
        Assert.Equal(10, metrics.Duration.TotalSeconds);
    }

    #endregion

    #region Error Recording Tests

    [Fact]
    public void RecordError_AddsErrorToList()
    {
        // Arrange
        var exception = new Exception("Test error");

        // Act
        _context.RecordError("TestStage", exception, isRecoverable: true);

        // Assert
        Assert.Single(_context.Errors);
        var error = _context.Errors[0];
        Assert.Equal("TestStage", error.StageName);
        Assert.Equal(exception, error.Exception);
        Assert.Equal("Test error", error.Message);
        Assert.True(error.IsRecoverable);
    }

    [Fact]
    public void RecordError_MultipleErrors_StoresAll()
    {
        // Arrange
        var error1 = new Exception("Error 1");
        var error2 = new Exception("Error 2");
        var error3 = new Exception("Error 3");

        // Act
        _context.RecordError("Stage1", error1, isRecoverable: true);
        _context.RecordError("Stage2", error2, isRecoverable: false);
        _context.RecordError("Stage3", error3, isRecoverable: true);

        // Assert
        Assert.Equal(3, _context.Errors.Count);
        Assert.Contains(_context.Errors, e => e.StageName == "Stage1");
        Assert.Contains(_context.Errors, e => e.StageName == "Stage2");
        Assert.Contains(_context.Errors, e => e.StageName == "Stage3");
    }

    #endregion

    #region Channel Tests

    [Fact]
    public async Task ScriptChannel_CanWriteAndRead()
    {
        // Arrange
        var testScript = "Test script content";

        // Act
        await _context.ScriptChannel.Writer.WriteAsync(testScript);
        var result = await _context.ScriptChannel.Reader.ReadAsync();

        // Assert
        Assert.Equal(testScript, result);
    }

    [Fact]
    public async Task SceneChannel_CanWriteAndReadMultiple()
    {
        // Arrange
        var scene1 = new Scene(0, "Scene 1", "Content 1", TimeSpan.Zero, TimeSpan.FromSeconds(5));
        var scene2 = new Scene(1, "Scene 2", "Content 2", TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(5));

        // Act
        await _context.SceneChannel.Writer.WriteAsync(scene1);
        await _context.SceneChannel.Writer.WriteAsync(scene2);
        
        var result1 = await _context.SceneChannel.Reader.ReadAsync();
        var result2 = await _context.SceneChannel.Reader.ReadAsync();

        // Assert
        Assert.Equal(scene1.Index, result1.Index);
        Assert.Equal(scene2.Index, result2.Index);
    }

    [Fact]
    public async Task AssetChannel_CanWriteAndReadBatches()
    {
        // Arrange
        var batch = new AssetBatch
        {
            SceneIndex = 0,
            Assets = new[] { new Asset("image.jpg", AssetType.Image, 0, TimeSpan.FromSeconds(5)) }
        };

        // Act
        await _context.AssetChannel.Writer.WriteAsync(batch);
        var result = await _context.AssetChannel.Reader.ReadAsync();

        // Assert
        Assert.Equal(batch.SceneIndex, result.SceneIndex);
        Assert.Single(result.Assets);
    }

    [Fact]
    public void Dispose_CompletesAllChannels()
    {
        // Act
        _context.Dispose();

        // Assert
        Assert.True(_context.ScriptChannel.Reader.Completion.IsCompleted);
        Assert.True(_context.SceneChannel.Reader.Completion.IsCompleted);
        Assert.True(_context.AssetChannel.Reader.Completion.IsCompleted);
    }

    #endregion

    #region Checkpoint Tests

    [Fact]
    public void CheckpointProjectId_CanBeSetAndRetrieved()
    {
        // Arrange
        var projectId = Guid.NewGuid();

        // Act
        _context.CheckpointProjectId = projectId;

        // Assert
        Assert.Equal(projectId, _context.CheckpointProjectId);
    }

    [Fact]
    public void LastCheckpointStage_CanBeSetAndRetrieved()
    {
        // Arrange
        var stageName = "ScriptGeneration";

        // Act
        _context.LastCheckpointStage = stageName;

        // Assert
        Assert.Equal(stageName, _context.LastCheckpointStage);
    }

    #endregion

    #region PipelineConfiguration Tests

    [Fact]
    public void PipelineConfiguration_DefaultValues_AreCorrect()
    {
        // Arrange & Act
        var config = new PipelineConfiguration();

        // Assert
        Assert.True(config.EnableCheckpoints);
        Assert.Equal(1, config.CheckpointFrequency);
        Assert.True(config.EnableMetrics);
        Assert.Equal(3, config.MaxRetryAttempts);
        Assert.True(config.EnableCircuitBreaker);
        Assert.Equal(3, config.MaxConcurrency);
        Assert.True(config.EnableStreaming);
        Assert.Equal(10, config.ChannelBufferSize);
        Assert.Equal(TimeSpan.FromMinutes(10), config.StageTimeout);
        Assert.Equal(TimeSpan.FromHours(1), config.PipelineTimeout);
    }

    [Fact]
    public void PipelineConfiguration_CanBeCustomized()
    {
        // Arrange & Act
        var config = new PipelineConfiguration
        {
            EnableCheckpoints = false,
            CheckpointFrequency = 2,
            MaxRetryAttempts = 5,
            MaxConcurrency = 10,
            StageTimeout = TimeSpan.FromMinutes(20),
            PipelineTimeout = TimeSpan.FromHours(2)
        };

        // Assert
        Assert.False(config.EnableCheckpoints);
        Assert.Equal(2, config.CheckpointFrequency);
        Assert.Equal(5, config.MaxRetryAttempts);
        Assert.Equal(10, config.MaxConcurrency);
        Assert.Equal(TimeSpan.FromMinutes(20), config.StageTimeout);
        Assert.Equal(TimeSpan.FromHours(2), config.PipelineTimeout);
    }

    #endregion

    #region Helper Methods

    private PipelineStageMetrics CreateTestMetrics(string stageName)
    {
        return new PipelineStageMetrics
        {
            StageName = stageName,
            StartTime = DateTime.UtcNow,
            EndTime = DateTime.UtcNow.AddSeconds(5),
            ItemsProcessed = 10,
            ItemsFailed = 0,
            ProviderUsed = "TestProvider"
        };
    }

    #endregion

    public void Dispose()
    {
        _context?.Dispose();
    }
}
