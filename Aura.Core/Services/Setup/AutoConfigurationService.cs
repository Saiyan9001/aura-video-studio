using System;
using System.Threading;
using System.Threading.Tasks;
using Aura.Core.Hardware;
using Aura.Core.Models;
using Microsoft.Extensions.Logging;

namespace Aura.Core.Services.Setup;

public record AutoConfigurationResult(
    int RecommendedThreadCount,
    long RecommendedMemoryLimitMB,
    string RecommendedQualityPreset,
    bool UseHardwareAcceleration,
    string? HardwareAccelerationMethod,
    bool EnableLocalProviders,
    string RecommendedTier,
    string[] ConfiguredProviders
);

public class AutoConfigurationService
{
    private readonly ILogger<AutoConfigurationService> _logger;
    private readonly IHardwareDetector? _hardwareDetector;
    private readonly DependencyDetector _dependencyDetector;

    public AutoConfigurationService(
        ILogger<AutoConfigurationService> logger,
        IHardwareDetector? hardwareDetector,
        DependencyDetector dependencyDetector)
    {
        _logger = logger;
        _hardwareDetector = hardwareDetector;
        _dependencyDetector = dependencyDetector;
    }

    public async Task<AutoConfigurationResult> DetectOptimalSettingsAsync(CancellationToken ct = default)
    {
        _logger.LogInformation("Starting auto-configuration detection");

        var dependencies = await _dependencyDetector.DetectAllDependenciesAsync(ct).ConfigureAwait(false);
        
        var cpuCores = Environment.ProcessorCount;
        var totalMemoryMB = GetTotalSystemMemoryMB();
        
        SystemProfile? systemProfile = null;
        if (_hardwareDetector != null)
        {
            try
            {
                systemProfile = await _hardwareDetector.DetectSystemAsync().ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Hardware detection failed, using fallback configuration");
            }
        }

        var gpuAvailable = systemProfile?.Gpu != null || dependencies.NvidiaDriversInstalled;
        var vramMB = (systemProfile?.Gpu?.VramGB ?? 0) * 1024;

        var threadCount = CalculateOptimalThreadCount(cpuCores, totalMemoryMB);
        var memoryLimitMB = CalculateMemoryLimit(totalMemoryMB);
        var qualityPreset = DetermineQualityPreset(gpuAvailable, vramMB, totalMemoryMB, dependencies.DiskSpaceGB);
        var (useHwAccel, hwMethod) = DetermineHardwareAcceleration(dependencies, systemProfile);
        var enableLocalProviders = ShouldEnableLocalProviders(dependencies, gpuAvailable);
        var recommendedTier = DetermineRecommendedTier(dependencies, gpuAvailable, vramMB, totalMemoryMB);
        var configuredProviders = DetermineConfiguredProviders(dependencies, enableLocalProviders);

        _logger.LogInformation(
            "Auto-configuration complete: Threads={ThreadCount}, Memory={MemoryMB}MB, Quality={Quality}, HW Accel={HwAccel}",
            threadCount, memoryLimitMB, qualityPreset, useHwAccel);

        return new AutoConfigurationResult(
            RecommendedThreadCount: threadCount,
            RecommendedMemoryLimitMB: memoryLimitMB,
            RecommendedQualityPreset: qualityPreset,
            UseHardwareAcceleration: useHwAccel,
            HardwareAccelerationMethod: hwMethod,
            EnableLocalProviders: enableLocalProviders,
            RecommendedTier: recommendedTier,
            ConfiguredProviders: configuredProviders
        );
    }

    private int CalculateOptimalThreadCount(int cpuCores, long totalMemoryMB)
    {
        if (totalMemoryMB < 4096)
        {
            return Math.Max(2, cpuCores / 2);
        }
        else if (totalMemoryMB < 8192)
        {
            return Math.Max(4, (cpuCores * 3) / 4);
        }
        else if (totalMemoryMB < 16384)
        {
            return Math.Max(4, cpuCores - 2);
        }
        else
        {
            return Math.Max(4, cpuCores - 1);
        }
    }

    private long CalculateMemoryLimit(long totalMemoryMB)
    {
        if (totalMemoryMB < 4096)
        {
            return (long)(totalMemoryMB * 0.5);
        }
        else if (totalMemoryMB < 8192)
        {
            return (long)(totalMemoryMB * 0.6);
        }
        else if (totalMemoryMB < 16384)
        {
            return (long)(totalMemoryMB * 0.7);
        }
        else
        {
            return (long)(totalMemoryMB * 0.75);
        }
    }

    private string DetermineQualityPreset(bool gpuAvailable, long vramMB, long totalMemoryMB, double diskSpaceGB)
    {
        if (diskSpaceGB < 5.0)
        {
            return "Low";
        }

        if (gpuAvailable && vramMB >= 8192 && totalMemoryMB >= 16384)
        {
            return "Ultra";
        }
        else if (gpuAvailable && vramMB >= 6144 && totalMemoryMB >= 8192)
        {
            return "High";
        }
        else if (totalMemoryMB >= 8192)
        {
            return "Medium";
        }
        else
        {
            return "Low";
        }
    }

    private (bool useHwAccel, string? method) DetermineHardwareAcceleration(
        DependencyStatus dependencies,
        SystemProfile? systemProfile)
    {
        if (dependencies.NvidiaDriversInstalled || systemProfile?.Gpu?.Vendor?.Contains("NVIDIA", StringComparison.OrdinalIgnoreCase) == true)
        {
            return (true, "nvenc");
        }

        if (systemProfile?.Gpu?.Vendor?.Contains("AMD", StringComparison.OrdinalIgnoreCase) == true)
        {
            return (true, "amf");
        }

        if (systemProfile?.Gpu?.Vendor?.Contains("Intel", StringComparison.OrdinalIgnoreCase) == true)
        {
            return (true, "qsv");
        }

        return (false, null);
    }

    private bool ShouldEnableLocalProviders(DependencyStatus dependencies, bool gpuAvailable)
    {
        return dependencies.OllamaInstalled || dependencies.PiperTtsInstalled || gpuAvailable;
    }

    private string DetermineRecommendedTier(
        DependencyStatus dependencies,
        bool gpuAvailable,
        long vramMB,
        long totalMemoryMB)
    {
        if (!dependencies.InternetConnected || !dependencies.DotNetInstalled)
        {
            return "Free";
        }

        if (gpuAvailable && vramMB >= 6144 && totalMemoryMB >= 16384 && 
            (dependencies.OllamaInstalled || dependencies.PiperTtsInstalled))
        {
            return "Local";
        }

        if (dependencies.InternetConnected && totalMemoryMB >= 8192)
        {
            return "Pro";
        }

        return "Free";
    }

    private string[] DetermineConfiguredProviders(DependencyStatus dependencies, bool enableLocalProviders)
    {
        var providers = new System.Collections.Generic.List<string>();

        if (dependencies.FFmpegInstalled)
        {
            providers.Add("FFmpeg");
        }

        if (enableLocalProviders)
        {
            if (dependencies.OllamaInstalled)
            {
                providers.Add("Ollama (Local LLM)");
            }

            if (dependencies.PiperTtsInstalled)
            {
                providers.Add("Piper TTS (Local)");
            }
        }

        if (dependencies.InternetConnected)
        {
            providers.Add("RuleBased LLM (Fallback)");
        }

        return providers.ToArray();
    }

    private long GetTotalSystemMemoryMB()
    {
        try
        {
            var gcMemoryInfo = GC.GetGCMemoryInfo();
            var totalMemoryBytes = gcMemoryInfo.TotalAvailableMemoryBytes;
            return totalMemoryBytes / (1024 * 1024);
        }
        catch
        {
            return 8192;
        }
    }
}
