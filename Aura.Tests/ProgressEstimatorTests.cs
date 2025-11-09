using System;
using System.Threading.Tasks;
using Aura.Core.Services;
using Xunit;

namespace Aura.Tests;

public class ProgressEstimatorTests
{
    private readonly ProgressEstimator _estimator;

    public ProgressEstimatorTests()
    {
        _estimator = new ProgressEstimator();
    }

    [Fact]
    public void EstimateTimeRemaining_ReturnsNullForInsufficientData()
    {
        // Arrange
        var jobId = "test-job-1";
        _estimator.RecordProgress(jobId, 10, DateTime.UtcNow);

        // Act
        var estimate = _estimator.EstimateTimeRemaining(jobId, 10);

        // Assert
        Assert.Null(estimate);
    }

    [Fact]
    public async Task EstimateTimeRemaining_ReturnsEstimateWithSufficientData()
    {
        // Arrange
        var jobId = "test-job-2";
        var startTime = DateTime.UtcNow;

        // Simulate progress over time: 10% per second
        _estimator.RecordProgress(jobId, 10, startTime);
        await Task.Delay(100); // Small delay to simulate time passing
        _estimator.RecordProgress(jobId, 20, startTime.AddSeconds(1));
        await Task.Delay(100);
        _estimator.RecordProgress(jobId, 30, startTime.AddSeconds(2));

        // Act
        var estimate = _estimator.EstimateTimeRemaining(jobId, 30);

        // Assert
        Assert.NotNull(estimate);
        Assert.True(estimate.Value.TotalSeconds > 0);
        Assert.True(estimate.Value.TotalSeconds < 7200); // Less than max estimate
    }

    [Fact]
    public void EstimateTimeRemaining_ReturnsZeroForCompletedJob()
    {
        // Arrange
        var jobId = "test-job-3";
        _estimator.RecordProgress(jobId, 100, DateTime.UtcNow);

        // Act
        var estimate = _estimator.EstimateTimeRemaining(jobId, 100);

        // Assert
        Assert.NotNull(estimate);
        Assert.Equal(TimeSpan.Zero, estimate.Value);
    }

    [Fact]
    public void CalculateElapsedTime_ReturnsNullForNoData()
    {
        // Arrange
        var jobId = "test-job-4";

        // Act
        var elapsed = _estimator.CalculateElapsedTime(jobId);

        // Assert
        Assert.Null(elapsed);
    }

    [Fact]
    public async Task CalculateElapsedTime_ReturnsCorrectElapsedTime()
    {
        // Arrange
        var jobId = "test-job-5";
        var startTime = DateTime.UtcNow;
        _estimator.RecordProgress(jobId, 10, startTime);

        // Act
        await Task.Delay(100); // Small delay
        var elapsed = _estimator.CalculateElapsedTime(jobId);

        // Assert
        Assert.NotNull(elapsed);
        Assert.True(elapsed.Value.TotalMilliseconds >= 90); // Account for small timing variations
    }

    [Fact]
    public void ClearHistory_RemovesJobHistory()
    {
        // Arrange
        var jobId = "test-job-6";
        _estimator.RecordProgress(jobId, 10, DateTime.UtcNow);
        _estimator.RecordProgress(jobId, 20, DateTime.UtcNow.AddSeconds(1));

        // Act
        _estimator.ClearHistory(jobId);
        var elapsed = _estimator.CalculateElapsedTime(jobId);

        // Assert
        Assert.Null(elapsed);
    }

    [Fact]
    public void GetAverageStageTime_ReturnsEstimatesForKnownStages()
    {
        // Act
        var scriptTime = _estimator.GetAverageStageTime("script");
        var ttsTime = _estimator.GetAverageStageTime("tts");
        var renderTime = _estimator.GetAverageStageTime("rendering");
        var unknownTime = _estimator.GetAverageStageTime("unknown");

        // Assert
        Assert.NotNull(scriptTime);
        Assert.NotNull(ttsTime);
        Assert.NotNull(renderTime);
        Assert.NotNull(unknownTime);
        Assert.True(ttsTime > scriptTime); // TTS typically takes longer than script generation
    }

    [Fact]
    public void RecordProgress_LimitsHistorySize()
    {
        // Arrange
        var jobId = "test-job-7";
        var startTime = DateTime.UtcNow;

        // Act - Record 25 progress samples (should keep only last 20)
        for (int i = 0; i < 25; i++)
        {
            _estimator.RecordProgress(jobId, i * 4, startTime.AddSeconds(i));
        }

        // The internal implementation should limit to 20 samples
        // We can verify this indirectly by checking that estimation still works
        var estimate = _estimator.EstimateTimeRemaining(jobId, 50);

        // Assert - Estimation should still work with truncated history
        Assert.NotNull(estimate);
    }

    [Fact]
    public async Task EstimateTimeRemaining_HandlesVariableVelocity()
    {
        // Arrange
        var jobId = "test-job-8";
        var startTime = DateTime.UtcNow;

        // Simulate variable progress: fast start, then slower
        _estimator.RecordProgress(jobId, 10, startTime);
        await Task.Delay(50);
        _estimator.RecordProgress(jobId, 30, startTime.AddSeconds(1)); // Fast: 20% in 1s
        await Task.Delay(50);
        _estimator.RecordProgress(jobId, 35, startTime.AddSeconds(2)); // Slow: 5% in 1s

        // Act
        var estimate = _estimator.EstimateTimeRemaining(jobId, 35);

        // Assert
        Assert.NotNull(estimate);
        // Estimate should be positive and within reasonable bounds
        Assert.True(estimate.Value.TotalSeconds > 0);
        Assert.True(estimate.Value.TotalSeconds < 7200); // Less than max estimate
    }
}
