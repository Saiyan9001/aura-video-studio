using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Management;
using System.Net.NetworkInformation;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Aura.Core.Models;
using Microsoft.Extensions.Logging;

namespace Aura.Core.Services.Resources;

/// <summary>
/// Service for monitoring system and process resource usage
/// </summary>
public class SystemResourceMonitor
{
    private readonly ILogger<SystemResourceMonitor> _logger;
    private readonly Process _currentProcess;
    private readonly PerformanceCounter? _cpuCounter;
    private readonly Dictionary<string, PerformanceCounter> _perCoreCounters = new();
    private DateTime _lastNetworkUpdate = DateTime.MinValue;
    private long _lastBytesSent;
    private long _lastBytesReceived;
    private readonly object _metricsLock = new();
    private SystemResourceMetrics? _lastSystemMetrics;
    private ProcessMetrics? _lastProcessMetrics;

    public SystemResourceMonitor(ILogger<SystemResourceMonitor> logger)
    {
        _logger = logger;
        _currentProcess = Process.GetCurrentProcess();

        try
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                _cpuCounter = new PerformanceCounter("Processor", "% Processor Time", "_Total", true);
                InitializePerCoreCounters();
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to initialize performance counters. Some metrics may be unavailable.");
        }
    }

    private void InitializePerCoreCounters()
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            return;

        try
        {
            var coreCount = Environment.ProcessorCount;
            for (int i = 0; i < coreCount; i++)
            {
                var counter = new PerformanceCounter("Processor", "% Processor Time", i.ToString(), true);
                _perCoreCounters[i.ToString()] = counter;
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to initialize per-core performance counters");
        }
    }

    /// <summary>
    /// Collects current system resource metrics
    /// </summary>
    public async Task<SystemResourceMetrics> CollectSystemMetricsAsync(CancellationToken cancellationToken = default)
    {
        var metrics = new SystemResourceMetrics
        {
            Timestamp = DateTime.UtcNow,
            Cpu = await CollectCpuMetricsAsync(cancellationToken).ConfigureAwait(false),
            Memory = CollectMemoryMetrics(),
            Gpu = await CollectGpuMetricsAsync(cancellationToken).ConfigureAwait(false),
            Disks = CollectDiskMetrics(),
            Network = await CollectNetworkMetricsAsync(cancellationToken).ConfigureAwait(false)
        };

        lock (_metricsLock)
        {
            _lastSystemMetrics = metrics;
        }

        return metrics;
    }

    /// <summary>
    /// Collects process-specific metrics
    /// </summary>
    public ProcessMetrics CollectProcessMetrics()
    {
        var metrics = new ProcessMetrics
        {
            Timestamp = DateTime.UtcNow,
            ThreadPool = CollectThreadPoolMetrics(),
            CacheMemoryBytes = GC.GetTotalMemory(false),
            ComponentMemory = CollectComponentMemoryMetrics(),
            ProviderConnections = CollectProviderConnectionMetrics()
        };

        lock (_metricsLock)
        {
            _lastProcessMetrics = metrics;
        }

        return metrics;
    }

    /// <summary>
    /// Gets the last collected system metrics without triggering a new collection
    /// </summary>
    public SystemResourceMetrics? GetLastSystemMetrics()
    {
        lock (_metricsLock)
        {
            return _lastSystemMetrics;
        }
    }

    /// <summary>
    /// Gets the last collected process metrics without triggering a new collection
    /// </summary>
    public ProcessMetrics? GetLastProcessMetrics()
    {
        lock (_metricsLock)
        {
            return _lastProcessMetrics;
        }
    }

    private async Task<CpuMetrics> CollectCpuMetricsAsync(CancellationToken cancellationToken)
    {
        var metrics = new CpuMetrics
        {
            LogicalCores = Environment.ProcessorCount,
            PhysicalCores = Environment.ProcessorCount / 2 // Rough estimate
        };

        try
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows) && _cpuCounter != null)
            {
                _cpuCounter.NextValue();
                await Task.Delay(100, cancellationToken).ConfigureAwait(false);
                metrics.OverallUsagePercent = _cpuCounter.NextValue();

                var perCoreUsage = new List<double>();
                foreach (var counter in _perCoreCounters.Values)
                {
                    counter.NextValue();
                    await Task.Delay(10, cancellationToken).ConfigureAwait(false);
                    perCoreUsage.Add(counter.NextValue());
                }
                metrics.PerCoreUsagePercent = perCoreUsage.ToArray();
            }
            else
            {
                metrics.OverallUsagePercent = GetCpuUsageNonWindows();
            }

            _currentProcess.Refresh();
            var totalTime = _currentProcess.TotalProcessorTime.TotalMilliseconds;
            var elapsedTime = (DateTime.UtcNow - _currentProcess.StartTime).TotalMilliseconds;
            metrics.ProcessUsagePercent = (totalTime / (elapsedTime * metrics.LogicalCores)) * 100.0;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to collect CPU metrics");
        }

        return metrics;
    }

    private double GetCpuUsageNonWindows()
    {
        try
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                var output = ExecuteCommand("top", "-bn1 | grep 'Cpu(s)' | sed 's/.*, *\\([0-9.]*\\)%* id.*/\\1/' | awk '{print 100 - $1}'");
                if (double.TryParse(output.Trim(), out var usage))
                {
                    return usage;
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Failed to get CPU usage on non-Windows platform");
        }

        return 0.0;
    }

    private MemoryMetrics CollectMemoryMetrics()
    {
        var metrics = new MemoryMetrics();

        try
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                var memStatus = new MEMORYSTATUSEX();
                if (GlobalMemoryStatusEx(memStatus))
                {
                    metrics.TotalBytes = (long)memStatus.ullTotalPhys;
                    metrics.AvailableBytes = (long)memStatus.ullAvailPhys;
                    metrics.UsedBytes = metrics.TotalBytes - metrics.AvailableBytes;
                    metrics.UsagePercent = ((double)metrics.UsedBytes / metrics.TotalBytes) * 100.0;
                }
            }
            else
            {
                var memInfo = GetMemoryInfoNonWindows();
                metrics.TotalBytes = memInfo.total;
                metrics.AvailableBytes = memInfo.available;
                metrics.UsedBytes = memInfo.used;
                metrics.UsagePercent = ((double)metrics.UsedBytes / metrics.TotalBytes) * 100.0;
            }

            _currentProcess.Refresh();
            metrics.ProcessUsageBytes = _currentProcess.WorkingSet64;
            metrics.ProcessPrivateBytes = _currentProcess.PrivateMemorySize64;
            metrics.ProcessWorkingSetBytes = _currentProcess.WorkingSet64;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to collect memory metrics");
        }

        return metrics;
    }

    private (long total, long available, long used) GetMemoryInfoNonWindows()
    {
        try
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                var lines = File.ReadAllLines("/proc/meminfo");
                long total = 0, available = 0;

                foreach (var line in lines)
                {
                    if (line.StartsWith("MemTotal:", StringComparison.Ordinal))
                    {
                        var parts = line.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                        if (parts.Length >= 2 && long.TryParse(parts[1], out var value))
                        {
                            total = value * 1024; // Convert from KB to bytes
                        }
                    }
                    else if (line.StartsWith("MemAvailable:", StringComparison.Ordinal))
                    {
                        var parts = line.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                        if (parts.Length >= 2 && long.TryParse(parts[1], out var value))
                        {
                            available = value * 1024; // Convert from KB to bytes
                        }
                    }
                }

                return (total, available, total - available);
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Failed to read memory info on non-Windows platform");
        }

        return (0, 0, 0);
    }

    private async Task<GpuMetrics?> CollectGpuMetricsAsync(CancellationToken cancellationToken)
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            return await CollectGpuMetricsLinuxAsync(cancellationToken).ConfigureAwait(false);
        }

        try
        {
            using var searcher = new ManagementObjectSearcher("SELECT * FROM Win32_VideoController");
            foreach (ManagementObject obj in searcher.Get())
            {
                var name = obj["Name"]?.ToString() ?? "Unknown";
                var adapterRam = obj["AdapterRAM"] != null ? Convert.ToInt64(obj["AdapterRAM"]) : 0;

                var metrics = new GpuMetrics
                {
                    Name = name,
                    Vendor = DetermineVendor(name),
                    TotalMemoryBytes = adapterRam
                };

                var nvidiaSmiMetrics = await TryGetNvidiaSmiMetricsAsync(cancellationToken).ConfigureAwait(false);
                if (nvidiaSmiMetrics != null)
                {
                    metrics.UsagePercent = nvidiaSmiMetrics.Value.usage;
                    metrics.UsedMemoryBytes = nvidiaSmiMetrics.Value.usedMemory;
                    metrics.AvailableMemoryBytes = metrics.TotalMemoryBytes - metrics.UsedMemoryBytes;
                    metrics.MemoryUsagePercent = metrics.TotalMemoryBytes > 0
                        ? ((double)metrics.UsedMemoryBytes / metrics.TotalMemoryBytes) * 100.0
                        : 0.0;
                    metrics.TemperatureCelsius = nvidiaSmiMetrics.Value.temperature;
                }

                return metrics;
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Failed to collect GPU metrics");
        }

        return null;
    }

    private async Task<GpuMetrics?> CollectGpuMetricsLinuxAsync(CancellationToken cancellationToken)
    {
        var nvidiaSmiMetrics = await TryGetNvidiaSmiMetricsAsync(cancellationToken).ConfigureAwait(false);
        if (nvidiaSmiMetrics != null)
        {
            return new GpuMetrics
            {
                Name = "NVIDIA GPU",
                Vendor = "NVIDIA",
                UsagePercent = nvidiaSmiMetrics.Value.usage,
                UsedMemoryBytes = nvidiaSmiMetrics.Value.usedMemory,
                TotalMemoryBytes = nvidiaSmiMetrics.Value.totalMemory,
                AvailableMemoryBytes = nvidiaSmiMetrics.Value.totalMemory - nvidiaSmiMetrics.Value.usedMemory,
                MemoryUsagePercent = nvidiaSmiMetrics.Value.totalMemory > 0
                    ? ((double)nvidiaSmiMetrics.Value.usedMemory / nvidiaSmiMetrics.Value.totalMemory) * 100.0
                    : 0.0,
                TemperatureCelsius = nvidiaSmiMetrics.Value.temperature
            };
        }

        return null;
    }

    private async Task<(double usage, long usedMemory, long totalMemory, double temperature)?> TryGetNvidiaSmiMetricsAsync(CancellationToken cancellationToken)
    {
        try
        {
            var output = await Task.Run(() => 
                ExecuteCommand("nvidia-smi", "--query-gpu=utilization.gpu,memory.used,memory.total,temperature.gpu --format=csv,noheader,nounits"),
                cancellationToken).ConfigureAwait(false);

            if (!string.IsNullOrWhiteSpace(output))
            {
                var parts = output.Trim().Split(',');
                if (parts.Length >= 4)
                {
                    var usage = double.TryParse(parts[0].Trim(), out var u) ? u : 0.0;
                    var usedMem = long.TryParse(parts[1].Trim(), out var um) ? um * 1024 * 1024 : 0L;
                    var totalMem = long.TryParse(parts[2].Trim(), out var tm) ? tm * 1024 * 1024 : 0L;
                    var temp = double.TryParse(parts[3].Trim(), out var t) ? t : 0.0;

                    return (usage, usedMem, totalMem, temp);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "nvidia-smi not available or failed");
        }

        return null;
    }

    private string DetermineVendor(string name)
    {
        var nameLower = name.ToLowerInvariant();
        if (nameLower.Contains("nvidia") || nameLower.Contains("geforce") || nameLower.Contains("rtx") || nameLower.Contains("gtx"))
            return "NVIDIA";
        if (nameLower.Contains("amd") || nameLower.Contains("radeon"))
            return "AMD";
        if (nameLower.Contains("intel"))
            return "Intel";

        return "Unknown";
    }

    private DiskMetrics[] CollectDiskMetrics()
    {
        var metrics = new List<DiskMetrics>();

        try
        {
            var drives = DriveInfo.GetDrives();
            foreach (var drive in drives)
            {
                if (!drive.IsReady)
                    continue;

                metrics.Add(new DiskMetrics
                {
                    DriveName = drive.Name,
                    DriveLabel = drive.VolumeLabel,
                    TotalBytes = drive.TotalSize,
                    AvailableBytes = drive.AvailableFreeSpace,
                    UsedBytes = drive.TotalSize - drive.AvailableFreeSpace,
                    UsagePercent = ((double)(drive.TotalSize - drive.AvailableFreeSpace) / drive.TotalSize) * 100.0
                });
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to collect disk metrics");
        }

        return metrics.ToArray();
    }

    private async Task<NetworkMetrics> CollectNetworkMetricsAsync(CancellationToken cancellationToken)
    {
        var metrics = new NetworkMetrics();

        try
        {
            await Task.Run(() =>
            {
                var interfaces = NetworkInterface.GetAllNetworkInterfaces();
                long totalBytesSent = 0;
                long totalBytesReceived = 0;

                foreach (var iface in interfaces)
                {
                    if (iface.OperationalStatus != OperationalStatus.Up)
                        continue;

                    var stats = iface.GetIPv4Statistics();
                    totalBytesSent += stats.BytesSent;
                    totalBytesReceived += stats.BytesReceived;
                }

                metrics.TotalBytesSent = totalBytesSent;
                metrics.TotalBytesReceived = totalBytesReceived;

                var now = DateTime.UtcNow;
                if (_lastNetworkUpdate != DateTime.MinValue)
                {
                    var elapsedSeconds = (now - _lastNetworkUpdate).TotalSeconds;
                    if (elapsedSeconds > 0)
                    {
                        metrics.BytesSentPerSecond = (long)((totalBytesSent - _lastBytesSent) / elapsedSeconds);
                        metrics.BytesReceivedPerSecond = (long)((totalBytesReceived - _lastBytesReceived) / elapsedSeconds);
                    }
                }

                _lastBytesSent = totalBytesSent;
                _lastBytesReceived = totalBytesReceived;
                _lastNetworkUpdate = now;
            }, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to collect network metrics");
        }

        return metrics;
    }

    private ThreadPoolMetrics CollectThreadPoolMetrics()
    {
        ThreadPool.GetAvailableThreads(out int availableWorkerThreads, out int availableCompletionPortThreads);
        ThreadPool.GetMaxThreads(out int maxWorkerThreads, out int maxCompletionPortThreads);

        return new ThreadPoolMetrics
        {
            AvailableWorkerThreads = availableWorkerThreads,
            AvailableCompletionPortThreads = availableCompletionPortThreads,
            MaxWorkerThreads = maxWorkerThreads,
            MaxCompletionPortThreads = maxCompletionPortThreads,
            BusyWorkerThreads = maxWorkerThreads - availableWorkerThreads,
            BusyCompletionPortThreads = maxCompletionPortThreads - availableCompletionPortThreads
        };
    }

    private ComponentMemoryMetrics[] CollectComponentMemoryMetrics()
    {
        var metrics = new List<ComponentMemoryMetrics>();

        try
        {
            var gen0 = GC.CollectionCount(0);
            var gen1 = GC.CollectionCount(1);
            var gen2 = GC.CollectionCount(2);

            metrics.Add(new ComponentMemoryMetrics
            {
                ComponentName = "GC.Gen0",
                AllocatedBytes = GC.GetTotalMemory(false),
                ActiveInstances = gen0
            });

            metrics.Add(new ComponentMemoryMetrics
            {
                ComponentName = "GC.Gen1",
                AllocatedBytes = GC.GetTotalMemory(false),
                ActiveInstances = gen1
            });

            metrics.Add(new ComponentMemoryMetrics
            {
                ComponentName = "GC.Gen2",
                AllocatedBytes = GC.GetTotalMemory(false),
                ActiveInstances = gen2
            });
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to collect component memory metrics");
        }

        return metrics.ToArray();
    }

    private ProviderConnectionMetrics[] CollectProviderConnectionMetrics()
    {
        return Array.Empty<ProviderConnectionMetrics>();
    }

    private string ExecuteCommand(string command, string arguments)
    {
        try
        {
            var processStartInfo = new ProcessStartInfo
            {
                FileName = command,
                Arguments = arguments,
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process = Process.Start(processStartInfo);
            if (process == null)
                return string.Empty;

            using var reader = process.StandardOutput;
            var result = reader.ReadToEnd();
            process.WaitForExit(5000);
            return result;
        }
        catch
        {
            return string.Empty;
        }
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
    private class MEMORYSTATUSEX
    {
        public uint dwLength = (uint)Marshal.SizeOf(typeof(MEMORYSTATUSEX));
        public uint dwMemoryLoad;
        public ulong ullTotalPhys;
        public ulong ullAvailPhys;
        public ulong ullTotalPageFile;
        public ulong ullAvailPageFile;
        public ulong ullTotalVirtual;
        public ulong ullAvailVirtual;
        public ulong ullAvailExtendedVirtual;
    }

    [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GlobalMemoryStatusEx([In, Out] MEMORYSTATUSEX lpBuffer);
}
