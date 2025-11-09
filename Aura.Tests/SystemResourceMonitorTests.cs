using Aura.Core.Services.Resources;
using Microsoft.Extensions.Logging;
using Xunit;

namespace Aura.Tests;

public class SystemResourceMonitorTests
{
    private readonly SystemResourceMonitor _monitor;

    public SystemResourceMonitorTests()
    {
        var logger = new LoggerFactory().CreateLogger<SystemResourceMonitor>();
        _monitor = new SystemResourceMonitor(logger);
    }

    [Fact]
    public async Task CollectSystemMetricsAsync_ReturnsMetrics()
    {
        var metrics = await _monitor.CollectSystemMetricsAsync();

        Assert.NotNull(metrics);
        Assert.True(metrics.Timestamp <= DateTime.UtcNow);
        Assert.NotNull(metrics.Cpu);
        Assert.NotNull(metrics.Memory);
        Assert.NotNull(metrics.Disks);
        Assert.NotNull(metrics.Network);
    }

    [Fact]
    public async Task CollectSystemMetricsAsync_CpuMetrics_HasValidData()
    {
        var metrics = await _monitor.CollectSystemMetricsAsync();

        Assert.True(metrics.Cpu.LogicalCores > 0);
        Assert.True(metrics.Cpu.PhysicalCores > 0);
        Assert.True(metrics.Cpu.OverallUsagePercent >= 0);
        Assert.True(metrics.Cpu.OverallUsagePercent <= 100);
    }

    [Fact]
    public async Task CollectSystemMetricsAsync_MemoryMetrics_HasValidData()
    {
        var metrics = await _monitor.CollectSystemMetricsAsync();

        Assert.True(metrics.Memory.TotalBytes > 0);
        Assert.True(metrics.Memory.AvailableBytes >= 0);
        Assert.True(metrics.Memory.UsedBytes >= 0);
        Assert.True(metrics.Memory.UsagePercent >= 0);
        Assert.True(metrics.Memory.UsagePercent <= 100);
        Assert.True(metrics.Memory.ProcessUsageBytes > 0);
    }

    [Fact]
    public async Task CollectSystemMetricsAsync_DiskMetrics_HasValidData()
    {
        var metrics = await _monitor.CollectSystemMetricsAsync();

        if (metrics.Disks.Length == 0)
        {
            Assert.True(true); // OK if no disks reported on this platform
            return;
        }
        
        foreach (var disk in metrics.Disks)
        {
            Assert.False(string.IsNullOrEmpty(disk.DriveName));
            Assert.True(disk.TotalBytes > 0);
            Assert.True(disk.AvailableBytes >= 0);
            Assert.True(disk.UsedBytes >= 0);
            Assert.True(disk.UsagePercent >= 0);
            Assert.True(disk.UsagePercent <= 100);
        }
    }

    [Fact]
    public void CollectProcessMetrics_ReturnsMetrics()
    {
        var metrics = _monitor.CollectProcessMetrics();

        Assert.NotNull(metrics);
        Assert.True(metrics.Timestamp <= DateTime.UtcNow);
        Assert.NotNull(metrics.ThreadPool);
        Assert.True(metrics.CacheMemoryBytes >= 0);
    }

    [Fact]
    public void CollectProcessMetrics_ThreadPoolMetrics_HasValidData()
    {
        var metrics = _monitor.CollectProcessMetrics();

        Assert.True(metrics.ThreadPool.AvailableWorkerThreads >= 0);
        Assert.True(metrics.ThreadPool.MaxWorkerThreads > 0);
        Assert.True(metrics.ThreadPool.BusyWorkerThreads >= 0);
        Assert.True(metrics.ThreadPool.BusyWorkerThreads <= metrics.ThreadPool.MaxWorkerThreads);
    }

    [Fact]
    public async Task GetLastSystemMetrics_AfterCollection_ReturnsLastMetrics()
    {
        var collected = await _monitor.CollectSystemMetricsAsync();
        var last = _monitor.GetLastSystemMetrics();

        Assert.NotNull(last);
        Assert.Equal(collected.Timestamp, last.Timestamp);
    }

    [Fact]
    public void GetLastProcessMetrics_AfterCollection_ReturnsLastMetrics()
    {
        var collected = _monitor.CollectProcessMetrics();
        var last = _monitor.GetLastProcessMetrics();

        Assert.NotNull(last);
        Assert.Equal(collected.Timestamp, last.Timestamp);
    }

    [Fact]
    public void GetLastSystemMetrics_BeforeCollection_ReturnsNull()
    {
        var monitor = new SystemResourceMonitor(
            new LoggerFactory().CreateLogger<SystemResourceMonitor>());
        
        var last = monitor.GetLastSystemMetrics();
        Assert.Null(last);
    }
}
