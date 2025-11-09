using System;

namespace Aura.Core.Models;

/// <summary>
/// System resource metrics
/// </summary>
public class SystemResourceMetrics
{
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    public CpuMetrics Cpu { get; set; } = new();
    public MemoryMetrics Memory { get; set; } = new();
    public GpuMetrics? Gpu { get; set; }
    public DiskMetrics[] Disks { get; set; } = Array.Empty<DiskMetrics>();
    public NetworkMetrics Network { get; set; } = new();
}

/// <summary>
/// CPU usage metrics
/// </summary>
public class CpuMetrics
{
    public double OverallUsagePercent { get; set; }
    public double[] PerCoreUsagePercent { get; set; } = Array.Empty<double>();
    public int LogicalCores { get; set; }
    public int PhysicalCores { get; set; }
    public double ProcessUsagePercent { get; set; }
}

/// <summary>
/// Memory usage metrics
/// </summary>
public class MemoryMetrics
{
    public long TotalBytes { get; set; }
    public long AvailableBytes { get; set; }
    public long UsedBytes { get; set; }
    public double UsagePercent { get; set; }
    public long ProcessUsageBytes { get; set; }
    public long ProcessPrivateBytes { get; set; }
    public long ProcessWorkingSetBytes { get; set; }
}

/// <summary>
/// GPU usage metrics
/// </summary>
public class GpuMetrics
{
    public string Name { get; set; } = string.Empty;
    public string Vendor { get; set; } = string.Empty;
    public double UsagePercent { get; set; }
    public long TotalMemoryBytes { get; set; }
    public long UsedMemoryBytes { get; set; }
    public long AvailableMemoryBytes { get; set; }
    public double MemoryUsagePercent { get; set; }
    public double TemperatureCelsius { get; set; }
}

/// <summary>
/// Disk I/O and space metrics
/// </summary>
public class DiskMetrics
{
    public string DriveName { get; set; } = string.Empty;
    public string DriveLabel { get; set; } = string.Empty;
    public long TotalBytes { get; set; }
    public long AvailableBytes { get; set; }
    public long UsedBytes { get; set; }
    public double UsagePercent { get; set; }
    public long ReadBytesPerSecond { get; set; }
    public long WriteBytesPerSecond { get; set; }
}

/// <summary>
/// Network bandwidth metrics
/// </summary>
public class NetworkMetrics
{
    public long BytesSentPerSecond { get; set; }
    public long BytesReceivedPerSecond { get; set; }
    public long TotalBytesSent { get; set; }
    public long TotalBytesReceived { get; set; }
}

/// <summary>
/// Process-specific metrics
/// </summary>
public class ProcessMetrics
{
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    public ComponentMemoryMetrics[] ComponentMemory { get; set; } = Array.Empty<ComponentMemoryMetrics>();
    public ThreadPoolMetrics ThreadPool { get; set; } = new();
    public ProviderConnectionMetrics[] ProviderConnections { get; set; } = Array.Empty<ProviderConnectionMetrics>();
    public long CacheMemoryBytes { get; set; }
}

/// <summary>
/// Memory usage by component
/// </summary>
public class ComponentMemoryMetrics
{
    public string ComponentName { get; set; } = string.Empty;
    public long AllocatedBytes { get; set; }
    public int ActiveInstances { get; set; }
}

/// <summary>
/// Thread pool usage metrics
/// </summary>
public class ThreadPoolMetrics
{
    public int AvailableWorkerThreads { get; set; }
    public int AvailableCompletionPortThreads { get; set; }
    public int MaxWorkerThreads { get; set; }
    public int MaxCompletionPortThreads { get; set; }
    public int BusyWorkerThreads { get; set; }
    public int BusyCompletionPortThreads { get; set; }
}

/// <summary>
/// Provider connection metrics
/// </summary>
public class ProviderConnectionMetrics
{
    public string ProviderName { get; set; } = string.Empty;
    public int ActiveConnections { get; set; }
    public int QueuedRequests { get; set; }
    public double AverageResponseTimeMs { get; set; }
}
