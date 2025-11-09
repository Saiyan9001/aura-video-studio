using Aura.Core.Services.Resources;
using Microsoft.Extensions.Logging;
using Xunit;

namespace Aura.Tests;

public class ResourceThrottlerTests
{
    private readonly SystemResourceMonitor _monitor;
    private readonly ResourceThrottler _throttler;

    public ResourceThrottlerTests()
    {
        var monitorLogger = new LoggerFactory().CreateLogger<SystemResourceMonitor>();
        _monitor = new SystemResourceMonitor(monitorLogger);

        var throttlerLogger = new LoggerFactory().CreateLogger<ResourceThrottler>();
        _throttler = new ResourceThrottler(throttlerLogger, _monitor);
    }

    [Fact]
    public async Task TryAcquireJobResourcesAsync_WithSmallMemoryRequest_Succeeds()
    {
        var reservation = await _throttler.TryAcquireJobResourcesAsync(
            "test-job-1",
            estimatedMemoryBytes: 100 * 1024 * 1024, // 100 MB
            requiresGpu: false);

        Assert.NotNull(reservation);
        Assert.Equal("test-job-1", reservation.JobId);
        Assert.Equal(100 * 1024 * 1024, reservation.ReservedMemoryBytes);
        Assert.False(reservation.RequiresGpu);

        _throttler.ReleaseJobResources("test-job-1");
    }

    [Fact]
    public async Task TryAcquireJobResourcesAsync_WithExcessiveMemoryRequest_Fails()
    {
        var reservation = await _throttler.TryAcquireJobResourcesAsync(
            "test-job-excessive",
            estimatedMemoryBytes: long.MaxValue / 2,
            requiresGpu: false);

        Assert.Null(reservation);
    }

    [Fact]
    public async Task ReleaseJobResources_AfterAcquire_AllowsNewAcquisition()
    {
        var reservation1 = await _throttler.TryAcquireJobResourcesAsync(
            "test-job-release",
            estimatedMemoryBytes: 100 * 1024 * 1024,
            requiresGpu: false);

        Assert.NotNull(reservation1);

        _throttler.ReleaseJobResources("test-job-release");

        var reservation2 = await _throttler.TryAcquireJobResourcesAsync(
            "test-job-release-2",
            estimatedMemoryBytes: 100 * 1024 * 1024,
            requiresGpu: false);

        Assert.NotNull(reservation2);

        _throttler.ReleaseJobResources("test-job-release-2");
    }

    [Fact]
    public async Task GetProviderThrottle_CreatesNewState()
    {
        var state = _throttler.GetProviderThrottle("test-provider", maxConcurrent: 3);

        Assert.NotNull(state);
        Assert.Equal("test-provider", state.ProviderName);
        Assert.Equal(3, state.MaxConcurrent);
        Assert.Equal(3, state.CurrentCount);
    }

    [Fact]
    public async Task TryAcquireProviderSlotAsync_WithinLimit_Succeeds()
    {
        var acquired = await _throttler.TryAcquireProviderSlotAsync("test-provider-2", maxConcurrent: 2);

        Assert.True(acquired);

        _throttler.ReleaseProviderSlot("test-provider-2");
    }

    [Fact]
    public async Task TryAcquireProviderSlotAsync_ExceedingLimit_Fails()
    {
        var acquired1 = await _throttler.TryAcquireProviderSlotAsync("test-provider-limit", maxConcurrent: 1);
        var acquired2 = await _throttler.TryAcquireProviderSlotAsync("test-provider-limit", maxConcurrent: 1);

        Assert.True(acquired1);
        Assert.False(acquired2);

        _throttler.ReleaseProviderSlot("test-provider-limit");
    }

    [Fact]
    public void GetUtilizationStats_ReturnsValidStats()
    {
        var stats = _throttler.GetUtilizationStats();

        Assert.NotNull(stats);
        Assert.True(stats.MaxConcurrentJobs > 0);
        Assert.True(stats.ActiveJobs >= 0);
        Assert.True(stats.AvailableJobSlots >= 0);
        Assert.True(stats.TotalReservedMemoryBytes >= 0);
        Assert.NotNull(stats.ActiveProviders);
    }

    [Fact]
    public void AdjustThreadPool_DoesNotThrow()
    {
        var exception = Record.Exception(() => _throttler.AdjustThreadPool());

        Assert.Null(exception);
    }

    [Fact]
    public async Task RecalculateResourceLimitsAsync_DoesNotThrow()
    {
        var exception = await Record.ExceptionAsync(
            async () => await _throttler.RecalculateResourceLimitsAsync());

        Assert.Null(exception);
    }

    [Fact]
    public void ReleaseJobResources_NonExistentJob_DoesNotThrow()
    {
        var exception = Record.Exception(
            () => _throttler.ReleaseJobResources("non-existent-job"));

        Assert.Null(exception);
    }

    [Fact]
    public void ReleaseProviderSlot_NonExistentProvider_DoesNotThrow()
    {
        var exception = Record.Exception(
            () => _throttler.ReleaseProviderSlot("non-existent-provider"));

        Assert.Null(exception);
    }
}
