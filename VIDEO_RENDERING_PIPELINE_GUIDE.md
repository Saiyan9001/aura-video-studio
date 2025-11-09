# Video Rendering Pipeline with Hardware Detection - Usage Guide

## Overview

The video rendering pipeline now supports multiple rendering backends with automatic hardware detection and fallback capabilities. This guide explains how to use the new rendering provider system.

## Architecture

### Rendering Providers

Five rendering providers are available, each with different capabilities and priorities:

1. **FFmpegProvider** (Priority: 100) - Auto-detecting primary renderer
   - Automatically detects and uses best available hardware acceleration
   - Falls back to software encoding if no hardware available

2. **FFmpegNvidiaProvider** (Priority: 90) - NVIDIA NVENC acceleration
   - Uses NVIDIA GPU hardware encoding (h264_nvenc, hevc_nvenc)
   - Requires NVIDIA GPU with NVENC support (GTX 10 series or newer recommended)

3. **FFmpegAmdProvider** (Priority: 80) - AMD VCE acceleration
   - Uses AMD GPU hardware encoding (h264_amf, hevc_amf)
   - Requires AMD GPU with VCE support (RX 400 series or newer recommended)

4. **FFmpegIntelProvider** (Priority: 70) - Intel QuickSync acceleration
   - Uses Intel QuickSync hardware encoding (h264_qsv, hevc_qsv)
   - Requires Intel CPU with integrated graphics (6th gen or newer recommended)

5. **BasicFFmpegProvider** (Priority: 10) - Software-only fallback
   - Pure CPU encoding (libx264, libx265)
   - Works on any system with FFmpeg
   - Slowest but most compatible

### RenderingProviderSelector

The `RenderingProviderSelector` service manages provider selection and automatic fallback:

- **SelectBestProviderAsync**: Selects the best available provider based on user tier and hardware
- **GetAvailableProvidersAsync**: Lists all available providers with their capabilities
- **RenderWithFallbackAsync**: Renders a video with automatic fallback on failure

## Usage Examples

### Basic Usage

```csharp
// Inject the selector service
private readonly RenderingProviderSelector _providerSelector;

// Render a video with automatic provider selection
var timeline = CreateTimeline();
var spec = new RenderSpec(...);
var progress = new Progress<RenderProgress>();

// For premium users (hardware acceleration priority)
var outputPath = await _providerSelector.RenderWithFallbackAsync(
    timeline,
    spec,
    progress,
    isPremium: true,
    cancellationToken);

// For free users (software encoding)
var outputPath = await _providerSelector.RenderWithFallbackAsync(
    timeline,
    spec,
    progress,
    isPremium: false,
    cancellationToken);
```

### Manual Provider Selection

```csharp
// Get the best provider for a user
var provider = await _providerSelector.SelectBestProviderAsync(
    isPremium: true,
    preferHardware: true);

// Check provider capabilities
var capabilities = await provider.GetHardwareCapabilitiesAsync();
Console.WriteLine($"Using {capabilities.ProviderName}");
Console.WriteLine($"Hardware: {capabilities.IsHardwareAccelerated}");
Console.WriteLine($"Type: {capabilities.AccelerationType}");

// Render with the selected provider
var outputPath = await provider.RenderVideoAsync(
    timeline,
    spec,
    progress,
    cancellationToken);
```

### List Available Providers

```csharp
// Get all available providers
var providers = await _providerSelector.GetAvailableProvidersAsync();

foreach (var (provider, capabilities) in providers)
{
    Console.WriteLine($"Provider: {capabilities.ProviderName}");
    Console.WriteLine($"  Priority: {provider.Priority}");
    Console.WriteLine($"  Hardware: {capabilities.IsHardwareAccelerated}");
    Console.WriteLine($"  Type: {capabilities.AccelerationType}");
    Console.WriteLine($"  Codecs: {string.Join(", ", capabilities.SupportedCodecs)}");
    Console.WriteLine($"  Description: {capabilities.Description}");
    Console.WriteLine();
}
```

## Service Registration

The rendering providers are automatically registered when you call `AddAuraProviders()`:

```csharp
services.AddAuraProviders(); // Registers all providers including rendering
```

Or register them separately:

```csharp
services.AddRenderingProviders(); // Registers only rendering providers
```

## Hardware Detection

The system automatically detects available hardware encoders:

- **NVENC**: Detects NVIDIA GPU with h264_nvenc/hevc_nvenc support
- **AMF**: Detects AMD GPU with h264_amf/hevc_amf support
- **QuickSync**: Detects Intel CPU with h264_qsv/hevc_qsv support
- **Software**: Always available as fallback

Detection is performed by querying FFmpeg for available encoders:

```bash
ffmpeg -encoders | grep nvenc  # Check for NVENC
ffmpeg -encoders | grep amf    # Check for AMF
ffmpeg -encoders | grep qsv    # Check for QuickSync
```

## User Tier Support

The rendering pipeline supports two user tiers:

### Premium Users (isPremium: true)
- Get hardware acceleration priority
- Providers with hardware acceleration are tried first
- 5-10x faster rendering with GPU acceleration
- Better for real-time rendering and high-volume usage

### Free Users (isPremium: false)
- Use software encoding
- More predictable resource usage
- Works on any system
- Sufficient for occasional rendering

## Automatic Fallback

The `RenderWithFallbackAsync` method provides automatic fallback:

1. Tries highest priority available provider
2. If that fails, tries next available provider
3. Continues until success or all providers exhausted
4. Logs each attempt with reasoning
5. Throws exception only if all providers fail

Example fallback chain for premium user:
1. FFmpegProvider (auto-detect) → tries hardware first
2. If hardware fails → FFmpegNvidiaProvider (if available)
3. If NVENC fails → FFmpegAmdProvider (if available)
4. If AMF fails → FFmpegIntelProvider (if available)
5. If QuickSync fails → BasicFFmpegProvider (software fallback)

## Performance Characteristics

| Provider | Speed | Quality | Compatibility | Requirements |
|----------|-------|---------|---------------|--------------|
| NVENC | 5-10x | High | NVIDIA GPUs | GTX 10+ series |
| AMF | 5-10x | High | AMD GPUs | RX 400+ series |
| QuickSync | 3-5x | Good | Intel CPUs | 6th gen+ |
| Software | 1x | High | Universal | Any CPU |

## Error Handling

All providers properly handle errors and provide detailed logging:

```csharp
try
{
    var output = await _providerSelector.RenderWithFallbackAsync(
        timeline, spec, progress, isPremium: true);
}
catch (InvalidOperationException ex)
{
    // All providers failed
    _logger.LogError(ex, "Rendering failed with all providers");
    
    // Check available providers
    var available = await _providerSelector.GetAvailableProvidersAsync();
    if (available.Count == 0)
    {
        // No providers available - FFmpeg not installed?
        throw new InvalidOperationException(
            "No rendering providers available. Please install FFmpeg.");
    }
    
    // Providers available but all failed - check logs
    throw;
}
```

## Testing

The rendering pipeline includes comprehensive tests:

- Provider priority tests
- Hardware capability detection tests
- Availability tests
- Automatic fallback tests
- User tier tests

Run tests:
```bash
dotnet test --filter "FullyQualifiedName~Aura.Tests.Rendering"
```

## Migration from Existing Code

If you're using `FfmpegVideoComposer` directly, you can migrate to the new provider system:

**Before:**
```csharp
var composer = new FfmpegVideoComposer(logger, ffmpegLocator);
var output = await composer.RenderAsync(timeline, spec, progress, ct);
```

**After:**
```csharp
var provider = await _providerSelector.SelectBestProviderAsync(isPremium);
var output = await provider.RenderVideoAsync(timeline, spec, progress, ct);
```

Or use automatic fallback:
```csharp
var output = await _providerSelector.RenderWithFallbackAsync(
    timeline, spec, progress, isPremium, ct);
```

## Best Practices

1. **Use RenderWithFallbackAsync** for production code - it provides automatic fallback
2. **Check available providers** at startup to inform users about hardware capabilities
3. **Log provider selection** to help debug rendering issues
4. **Use premium flag** based on actual user subscription status
5. **Handle cancellation properly** - all providers support CancellationToken
6. **Monitor performance** - hardware encoding should be 5-10x faster than software

## Future Enhancements

Potential future enhancements:

- VideoToolbox support for macOS (Apple Silicon acceleration)
- AV1 hardware encoding support (when widely available)
- Multi-GPU support for parallel rendering
- Provider preference configuration per user
- Cost tracking per provider type
- Performance analytics and recommendations

## Troubleshooting

### Hardware provider not available

Check FFmpeg encoders:
```bash
ffmpeg -encoders | grep -E "nvenc|amf|qsv"
```

Install appropriate drivers:
- NVIDIA: Latest GeForce drivers
- AMD: Latest Adrenalin drivers
- Intel: Latest graphics drivers

### All providers failing

Check FFmpeg installation:
```bash
ffmpeg -version
```

Verify FFmpeg path in application settings.

### Performance not improved

- Check GPU utilization during rendering
- Verify hardware encoder is actually being used (check logs)
- Some video codecs/settings may force software encoding
- Quality settings may override hardware encoding

## Support

For issues or questions:
1. Check logs for provider selection and errors
2. Verify hardware detection: `GetAvailableProvidersAsync()`
3. Test FFmpeg directly: `ffmpeg -encoders`
4. Check GPU drivers are up to date
