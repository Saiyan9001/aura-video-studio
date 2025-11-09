using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Aura.Core.Models.Export;
using Microsoft.Extensions.Logging;

namespace Aura.Core.Services.Render;

/// <summary>
/// Hardware encoder capabilities
/// </summary>
public record HardwareCapabilities(
    bool HasNVENC,
    bool HasAMF,
    bool HasQSV,
    bool HasVideoToolbox,
    List<string> AvailableEncoders,
    GpuMemoryInfo? GpuMemory = null);

/// <summary>
/// GPU memory information
/// </summary>
public record GpuMemoryInfo(
    long TotalMemoryBytes,
    long FreeMemoryBytes,
    long UsedMemoryBytes,
    double UsagePercentage,
    string GpuName);

/// <summary>
/// GPU utilization statistics
/// </summary>
public record GpuUtilization(
    double GpuUsagePercent,
    double MemoryUsagePercent,
    double EncoderUsagePercent,
    double DecoderUsagePercent,
    double TemperatureCelsius);

/// <summary>
/// Selected encoder configuration
/// </summary>
public record EncoderConfig(
    string EncoderName,
    string Description,
    bool IsHardwareAccelerated,
    Dictionary<string, string> Parameters);

/// <summary>
/// Detects and manages hardware-accelerated video encoding
/// </summary>
public class HardwareEncoder
{
    private readonly ILogger<HardwareEncoder> _logger;
    private readonly string _ffmpegPath;
    private HardwareCapabilities? _cachedCapabilities;

    public HardwareEncoder(ILogger<HardwareEncoder> logger, string ffmpegPath = "ffmpeg")
    {
        _logger = logger;
        _ffmpegPath = ffmpegPath;
    }

    /// <summary>
    /// Detects available hardware encoders by querying FFmpeg
    /// </summary>
    public async Task<HardwareCapabilities> DetectHardwareCapabilitiesAsync()
    {
        if (_cachedCapabilities != null)
        {
            return _cachedCapabilities;
        }

        _logger.LogInformation("Detecting hardware encoding capabilities...");

        try
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = _ffmpegPath,
                Arguments = "-encoders",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process = Process.Start(startInfo);
            if (process == null)
            {
                _logger.LogWarning("Failed to start FFmpeg process for encoder detection");
                return CreateFallbackCapabilities();
            }

            var output = await process.StandardOutput.ReadToEndAsync();
            await process.WaitForExitAsync();

            var availableEncoders = ParseEncoders(output);

            var hasNVENC = availableEncoders.Any(e => e.Contains("nvenc"));
            var hasAMF = availableEncoders.Any(e => e.Contains("amf"));
            var hasQSV = availableEncoders.Any(e => e.Contains("qsv"));
            var hasVideoToolbox = availableEncoders.Any(e => e.Contains("videotoolbox"));

            GpuMemoryInfo? gpuMemory = null;
            if (hasNVENC)
            {
                gpuMemory = await GetGpuMemoryInfoAsync();
            }

            _cachedCapabilities = new HardwareCapabilities(
                HasNVENC: hasNVENC,
                HasAMF: hasAMF,
                HasQSV: hasQSV,
                HasVideoToolbox: hasVideoToolbox,
                AvailableEncoders: availableEncoders,
                GpuMemory: gpuMemory
            );

            _logger.LogInformation(
                "Hardware capabilities: NVENC={NVENC}, AMF={AMF}, QSV={QSV}, VideoToolbox={VideoToolbox}, GPU Memory={GpuMemory}GB",
                hasNVENC, hasAMF, hasQSV, hasVideoToolbox, 
                gpuMemory != null ? gpuMemory.TotalMemoryBytes / 1024.0 / 1024.0 / 1024.0 : 0
            );

            return _cachedCapabilities;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error detecting hardware capabilities");
            return CreateFallbackCapabilities();
        }
    }

    /// <summary>
    /// Selects the best encoder based on available hardware and quality requirements
    /// </summary>
    public async Task<EncoderConfig> SelectBestEncoderAsync(
        ExportPreset preset,
        bool preferHardware = true)
    {
        var capabilities = await DetectHardwareCapabilitiesAsync();

        if (!preferHardware)
        {
            _logger.LogInformation("Hardware acceleration disabled by user preference");
            return CreateSoftwareEncoder(preset);
        }

        // Try NVIDIA NVENC first (best quality/performance balance)
        if (capabilities.HasNVENC)
        {
            var nvencEncoder = CreateNVENCEncoder(preset, capabilities);
            if (nvencEncoder != null)
            {
                _logger.LogInformation(
                    "Using {Encoder} (NVIDIA GPU acceleration) for 5-10x faster encoding",
                    nvencEncoder.EncoderName
                );
                return nvencEncoder;
            }
        }

        // Try AMD AMF
        if (capabilities.HasAMF)
        {
            var amfEncoder = CreateAMFEncoder(preset, capabilities);
            if (amfEncoder != null)
            {
                _logger.LogInformation(
                    "Using {Encoder} (AMD GPU acceleration) for 5-10x faster encoding",
                    amfEncoder.EncoderName
                );
                return amfEncoder;
            }
        }

        // Try Intel Quick Sync
        if (capabilities.HasQSV)
        {
            var qsvEncoder = CreateQSVEncoder(preset, capabilities);
            if (qsvEncoder != null)
            {
                _logger.LogInformation(
                    "Using {Encoder} (Intel Quick Sync acceleration) for 3-5x faster encoding",
                    qsvEncoder.EncoderName
                );
                return qsvEncoder;
            }
        }

        // Try Apple VideoToolbox
        if (capabilities.HasVideoToolbox)
        {
            var vtEncoder = CreateVideoToolboxEncoder(preset, capabilities);
            if (vtEncoder != null)
            {
                _logger.LogInformation(
                    "Using {Encoder} (Apple VideoToolbox acceleration) for faster encoding",
                    vtEncoder.EncoderName
                );
                return vtEncoder;
            }
        }

        // Fallback to software encoding
        _logger.LogInformation("No hardware acceleration available, using software encoding");
        return CreateSoftwareEncoder(preset);
    }

    /// <summary>
    /// Gets FFmpeg parameters for the selected encoder
    /// </summary>
    public string GetEncoderArguments(EncoderConfig config)
    {
        var args = new List<string>();

        foreach (var param in config.Parameters)
        {
            args.Add($"{param.Key} {param.Value}");
        }

        return string.Join(" ", args);
    }

    private List<string> ParseEncoders(string output)
    {
        var encoders = new List<string>();
        var lines = output.Split('\n');
        var inEncoderSection = false;

        foreach (var line in lines)
        {
            if (line.Contains("Encoders:"))
            {
                inEncoderSection = true;
                continue;
            }

            if (!inEncoderSection) continue;

            // Look for encoder lines (format: " V..... encodername  description")
            var match = Regex.Match(line, @"^\s*[VAS\.]+\s+(\S+)\s+");
            if (match.Success)
            {
                encoders.Add(match.Groups[1].Value);
            }
        }

        return encoders;
    }

    private EncoderConfig? CreateNVENCEncoder(ExportPreset preset, HardwareCapabilities caps)
    {
        var encoderName = preset.VideoCodec switch
        {
            "libx265" or "hevc" => caps.AvailableEncoders.Contains("hevc_nvenc") ? "hevc_nvenc" : null,
            _ => caps.AvailableEncoders.Contains("h264_nvenc") ? "h264_nvenc" : null
        };

        if (encoderName == null) return null;

        var qualityPreset = preset.Quality switch
        {
            QualityLevel.Draft => "fast",
            QualityLevel.Good => "medium",
            QualityLevel.High => "slow",
            QualityLevel.Maximum => "slow",
            _ => "medium"
        };

        var parameters = new Dictionary<string, string>
        {
            ["-c:v"] = encoderName,
            ["-preset"] = qualityPreset,
            ["-rc"] = "vbr",
            ["-b:v"] = $"{preset.VideoBitrate}k",
            ["-maxrate"] = $"{(int)(preset.VideoBitrate * 1.5)}k",
            ["-bufsize"] = $"{preset.VideoBitrate * 2}k",
            ["-pix_fmt"] = preset.PixelFormat
        };

        return new EncoderConfig(
            EncoderName: encoderName,
            Description: "NVIDIA NVENC GPU acceleration",
            IsHardwareAccelerated: true,
            Parameters: parameters
        );
    }

    private EncoderConfig? CreateAMFEncoder(ExportPreset preset, HardwareCapabilities caps)
    {
        var encoderName = preset.VideoCodec switch
        {
            "libx265" or "hevc" => caps.AvailableEncoders.Contains("hevc_amf") ? "hevc_amf" : null,
            _ => caps.AvailableEncoders.Contains("h264_amf") ? "h264_amf" : null
        };

        if (encoderName == null) return null;

        var qualityLevel = preset.Quality switch
        {
            QualityLevel.Draft => "speed",
            QualityLevel.Good => "balanced",
            QualityLevel.High => "quality",
            QualityLevel.Maximum => "quality",
            _ => "balanced"
        };

        var parameters = new Dictionary<string, string>
        {
            ["-c:v"] = encoderName,
            ["-quality"] = qualityLevel,
            ["-rc"] = "vbr_peak",
            ["-b:v"] = $"{preset.VideoBitrate}k",
            ["-maxrate"] = $"{(int)(preset.VideoBitrate * 1.5)}k",
            ["-pix_fmt"] = preset.PixelFormat
        };

        return new EncoderConfig(
            EncoderName: encoderName,
            Description: "AMD VCE GPU acceleration",
            IsHardwareAccelerated: true,
            Parameters: parameters
        );
    }

    private EncoderConfig? CreateQSVEncoder(ExportPreset preset, HardwareCapabilities caps)
    {
        var encoderName = preset.VideoCodec switch
        {
            "libx265" or "hevc" => caps.AvailableEncoders.Contains("hevc_qsv") ? "hevc_qsv" : null,
            _ => caps.AvailableEncoders.Contains("h264_qsv") ? "h264_qsv" : null
        };

        if (encoderName == null) return null;

        var preset_name = preset.Quality switch
        {
            QualityLevel.Draft => "veryfast",
            QualityLevel.Good => "fast",
            QualityLevel.High => "medium",
            QualityLevel.Maximum => "slow",
            _ => "fast"
        };

        var parameters = new Dictionary<string, string>
        {
            ["-c:v"] = encoderName,
            ["-preset"] = preset_name,
            ["-b:v"] = $"{preset.VideoBitrate}k",
            ["-maxrate"] = $"{(int)(preset.VideoBitrate * 1.5)}k",
            ["-pix_fmt"] = preset.PixelFormat
        };

        return new EncoderConfig(
            EncoderName: encoderName,
            Description: "Intel Quick Sync acceleration",
            IsHardwareAccelerated: true,
            Parameters: parameters
        );
    }

    private EncoderConfig? CreateVideoToolboxEncoder(ExportPreset preset, HardwareCapabilities caps)
    {
        var encoderName = preset.VideoCodec switch
        {
            "libx265" or "hevc" => caps.AvailableEncoders.Contains("hevc_videotoolbox") ? "hevc_videotoolbox" : null,
            _ => caps.AvailableEncoders.Contains("h264_videotoolbox") ? "h264_videotoolbox" : null
        };

        if (encoderName == null) return null;

        var parameters = new Dictionary<string, string>
        {
            ["-c:v"] = encoderName,
            ["-b:v"] = $"{preset.VideoBitrate}k",
            ["-pix_fmt"] = preset.PixelFormat
        };

        return new EncoderConfig(
            EncoderName: encoderName,
            Description: "Apple VideoToolbox acceleration",
            IsHardwareAccelerated: true,
            Parameters: parameters
        );
    }

    private EncoderConfig CreateSoftwareEncoder(ExportPreset preset)
    {
        var encoderName = preset.VideoCodec switch
        {
            "libx265" or "hevc" => "libx265",
            _ => "libx264"
        };

        var preset_name = preset.Quality switch
        {
            QualityLevel.Draft => "ultrafast",
            QualityLevel.Good => "medium",
            QualityLevel.High => "slow",
            QualityLevel.Maximum => "veryslow",
            _ => "medium"
        };

        var crf = preset.Quality switch
        {
            QualityLevel.Draft => "28",
            QualityLevel.Good => "23",
            QualityLevel.High => "20",
            QualityLevel.Maximum => "18",
            _ => "23"
        };

        var parameters = new Dictionary<string, string>
        {
            ["-c:v"] = encoderName,
            ["-preset"] = preset_name,
            ["-crf"] = crf,
            ["-b:v"] = $"{preset.VideoBitrate}k",
            ["-pix_fmt"] = preset.PixelFormat
        };

        return new EncoderConfig(
            EncoderName: encoderName,
            Description: "Software encoding (CPU)",
            IsHardwareAccelerated: false,
            Parameters: parameters
        );
    }

    private HardwareCapabilities CreateFallbackCapabilities()
    {
        return new HardwareCapabilities(
            HasNVENC: false,
            HasAMF: false,
            HasQSV: false,
            HasVideoToolbox: false,
            AvailableEncoders: new List<string> { "libx264", "libx265" },
            GpuMemory: null
        );
    }

    /// <summary>
    /// Monitors GPU memory usage (NVIDIA GPUs only via nvidia-smi)
    /// </summary>
    public async Task<GpuMemoryInfo?> GetGpuMemoryInfoAsync()
    {
        try
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = "nvidia-smi",
                Arguments = "--query-gpu=name,memory.total,memory.free,memory.used --format=csv,noheader,nounits",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process = Process.Start(startInfo);
            if (process == null)
            {
                return null;
            }

            var output = await process.StandardOutput.ReadToEndAsync();
            await process.WaitForExitAsync();

            if (process.ExitCode != 0 || string.IsNullOrWhiteSpace(output))
            {
                return null;
            }

            var parts = output.Trim().Split(',');
            if (parts.Length >= 4)
            {
                var gpuName = parts[0].Trim();
                var totalMb = long.Parse(parts[1].Trim());
                var freeMb = long.Parse(parts[2].Trim());
                var usedMb = long.Parse(parts[3].Trim());

                var totalBytes = totalMb * 1024 * 1024;
                var freeBytes = freeMb * 1024 * 1024;
                var usedBytes = usedMb * 1024 * 1024;
                var usagePercent = (double)usedBytes / totalBytes * 100;

                return new GpuMemoryInfo(
                    TotalMemoryBytes: totalBytes,
                    FreeMemoryBytes: freeBytes,
                    UsedMemoryBytes: usedBytes,
                    UsagePercentage: usagePercent,
                    GpuName: gpuName
                );
            }

            return null;
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Failed to query GPU memory info");
            return null;
        }
    }

    /// <summary>
    /// Monitors GPU utilization (NVIDIA GPUs only via nvidia-smi)
    /// </summary>
    public async Task<GpuUtilization?> GetGpuUtilizationAsync()
    {
        try
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = "nvidia-smi",
                Arguments = "--query-gpu=utilization.gpu,utilization.memory,utilization.encoder,utilization.decoder,temperature.gpu --format=csv,noheader,nounits",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process = Process.Start(startInfo);
            if (process == null)
            {
                return null;
            }

            var output = await process.StandardOutput.ReadToEndAsync();
            await process.WaitForExitAsync();

            if (process.ExitCode != 0 || string.IsNullOrWhiteSpace(output))
            {
                return null;
            }

            var parts = output.Trim().Split(',');
            if (parts.Length >= 5)
            {
                var gpuUsage = double.Parse(parts[0].Trim());
                var memUsage = double.Parse(parts[1].Trim());
                var encUsage = double.Parse(parts[2].Trim());
                var decUsage = double.Parse(parts[3].Trim());
                var temperature = double.Parse(parts[4].Trim());

                return new GpuUtilization(
                    GpuUsagePercent: gpuUsage,
                    MemoryUsagePercent: memUsage,
                    EncoderUsagePercent: encUsage,
                    DecoderUsagePercent: decUsage,
                    TemperatureCelsius: temperature
                );
            }

            return null;
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Failed to query GPU utilization");
            return null;
        }
    }

    /// <summary>
    /// Checks if GPU has sufficient memory for encoding
    /// </summary>
    /// <param name="requiredMemoryBytes">Required memory in bytes</param>
    public async Task<bool> HasSufficientGpuMemoryAsync(long requiredMemoryBytes)
    {
        var memInfo = await GetGpuMemoryInfoAsync();
        if (memInfo == null)
        {
            return true;
        }

        return memInfo.FreeMemoryBytes >= requiredMemoryBytes;
    }

    /// <summary>
    /// Estimates required GPU memory for video encoding
    /// </summary>
    /// <param name="width">Video width</param>
    /// <param name="height">Video height</param>
    /// <param name="fps">Frames per second</param>
    /// <param name="durationSeconds">Video duration in seconds</param>
    public long EstimateRequiredGpuMemory(int width, int height, int fps, double durationSeconds)
    {
        var frameSizeBytes = (long)width * height * 4;
        var bufferedFrames = Math.Min(fps * 2, 60);
        var workingMemory = frameSizeBytes * bufferedFrames;
        var overhead = workingMemory / 2;
        
        return workingMemory + overhead;
    }
}
