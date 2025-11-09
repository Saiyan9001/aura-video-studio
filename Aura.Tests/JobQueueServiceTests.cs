using Aura.Core.Services;
using Microsoft.Extensions.Logging;
using Xunit;

namespace Aura.Tests;

public class JobQueueServiceTests
{
    private readonly JobQueueService _queueService;

    public JobQueueServiceTests()
    {
        var logger = LoggerFactory.Create(builder => builder.AddConsole())
            .CreateLogger<JobQueueService>();
        _queueService = new JobQueueService(logger);
    }

    [Fact]
    public async Task EnqueueJob_AddsJobToQueue()
    {
        // Arrange
        var jobId = "test-job-1";

        // Act
        var result = await _queueService.EnqueueJobAsync(jobId, priority: 5);

        // Assert
        Assert.True(result);
        Assert.Equal(1, _queueService.GetQueueSize());
    }

    [Fact]
    public async Task DequeueJob_ReturnsHighestPriorityJob()
    {
        // Arrange
        await _queueService.EnqueueJobAsync("job-low", priority: 10);
        await _queueService.EnqueueJobAsync("job-high", priority: 1);
        await _queueService.EnqueueJobAsync("job-medium", priority: 5);

        // Act
        var job1 = await _queueService.DequeueJobAsync();
        var job2 = await _queueService.DequeueJobAsync();
        var job3 = await _queueService.DequeueJobAsync();

        // Assert
        Assert.NotNull(job1);
        Assert.Equal("job-high", job1.JobId);
        Assert.NotNull(job2);
        Assert.Equal("job-medium", job2.JobId);
        Assert.NotNull(job3);
        Assert.Equal("job-low", job3.JobId);
    }

    [Fact]
    public async Task CanRetryJob_ReturnsTrueForNewJob()
    {
        // Arrange
        var jobId = "retry-test-1";

        // Act
        var canRetry = _queueService.CanRetryJob(jobId, maxRetries: 3);

        // Assert
        Assert.True(canRetry);
    }

    [Fact]
    public async Task CanRetryJob_ReturnsFalseAfterMaxRetries()
    {
        // Arrange
        var jobId = "retry-test-2";

        // Act - Retry 3 times
        await _queueService.RetryJobAsync(jobId);
        await _queueService.RetryJobAsync(jobId);
        await _queueService.RetryJobAsync(jobId);

        // Assert
        var canRetry = _queueService.CanRetryJob(jobId, maxRetries: 3);
        Assert.False(canRetry);
    }

    [Fact]
    public async Task RetryJobAsync_IncrementsRetryCount()
    {
        // Arrange
        var jobId = "retry-test-3";

        // Act
        await _queueService.RetryJobAsync(jobId);
        var retryState = _queueService.GetRetryState(jobId);

        // Assert
        Assert.NotNull(retryState);
        Assert.Equal(1, retryState.RetryCount);
    }

    [Fact]
    public void ClearRetryState_RemovesRetryState()
    {
        // Arrange
        var jobId = "retry-test-4";
        _queueService.RetryJobAsync(jobId).Wait();

        // Act
        _queueService.ClearRetryState(jobId);
        var retryState = _queueService.GetRetryState(jobId);

        // Assert
        Assert.Null(retryState);
    }

    [Fact]
    public async Task GetQueuedJobIds_ReturnsAllQueuedJobs()
    {
        // Arrange
        await _queueService.EnqueueJobAsync("job-1");
        await _queueService.EnqueueJobAsync("job-2");
        await _queueService.EnqueueJobAsync("job-3");

        // Act
        var queuedIds = _queueService.GetQueuedJobIds();

        // Assert
        Assert.Equal(3, queuedIds.Count);
        Assert.Contains("job-1", queuedIds);
        Assert.Contains("job-2", queuedIds);
        Assert.Contains("job-3", queuedIds);
    }
}
