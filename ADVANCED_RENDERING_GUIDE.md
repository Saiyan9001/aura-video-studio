# Advanced Video Rendering Features - Implementation Guide

## Overview

This guide documents the advanced video rendering features implemented for PR #18, including complex transitions, effects, audio processing, GPU monitoring, and real-time rendering analytics.

## 1. Video Transitions and Effects

### Crossfade Transition

Smooth transition between two video clips with gradual opacity change.

```csharp
var builder = new FFmpegCommandBuilder()
    .AddInput("clip1.mp4")
    .AddInput("clip2.mp4")
    .AddCrossfadeTransition(duration: 1.5, offset: 5.0)
    .SetOutput("output.mp4");
```

**Parameters:**
- `durationSeconds`: Duration of the crossfade (default: 1.0 seconds)
- `offset`: Time offset where transition starts

### Wipe Transition

Directional wipe transition between clips.

```csharp
builder.AddWipeTransition(
    durationSeconds: 1.0, 
    offset: 5.0, 
    direction: "right" // Options: left, right, up, down
);
```

### Dissolve Transition

Classic dissolve effect between clips.

```csharp
builder.AddDissolveTransition(durationSeconds: 2.0, offset: 10.0);
```

## 2. Ken Burns Effect

Dynamic zoom and pan effect for still images.

```csharp
builder.AddKenBurnsEffect(
    durationSeconds: 5.0,
    zoomStart: 1.0,      // Starting zoom (1.0 = no zoom)
    zoomEnd: 1.3,        // Ending zoom (1.3 = 130%)
    panX: 0.1,           // Horizontal pan (-1.0 to 1.0)
    panY: -0.1           // Vertical pan (-1.0 to 1.0)
);
```

**Use Cases:**
- Bring life to static images
- Create motion in photo slideshows
- Highlight specific areas of images

## 3. Picture-in-Picture (PiP)

Overlay one video on top of another.

```csharp
builder.AddInput("main_video.mp4")
       .AddInput("overlay_video.mp4")
       .AddPictureInPicture(
           overlayInputIndex: 1,
           x: "W-w-10",          // 10px from right edge
           y: "H-h-10",          // 10px from bottom edge
           scale: 0.25           // 25% of original size
       );
```

**Common Positions:**
- Bottom-right: `x: "W-w-10"`, `y: "H-h-10"`
- Top-right: `x: "W-w-10"`, `y: "10"`
- Bottom-left: `x: "10"`, `y: "H-h-10"`
- Center: `x: "(W-w)/2"`, `y: "(H-h)/2"`

## 4. Text Overlays

### Static Text Overlay

```csharp
builder.AddTextOverlay(
    text: "Hello World",
    fontFile: "/path/to/font.ttf",  // Optional
    fontSize: 48,
    x: "(w-text_w)/2",              // Center horizontally
    y: "(h-text_h)/2",              // Center vertically
    fontColor: "white",
    boxColor: "black@0.5"           // Optional background
);
```

### Animated Text with Fade

```csharp
builder.AddAnimatedTextOverlay(
    text: "Fade In and Out",
    startTime: 2.0,           // Start at 2 seconds
    duration: 5.0,            // Display for 5 seconds
    fadeInDuration: 0.5,      // Fade in over 0.5 seconds
    fadeOutDuration: 0.5,     // Fade out over 0.5 seconds
    fontSize: 48
);
```

### Sliding Text Animation

```csharp
builder.AddSlidingTextOverlay(
    text: "Breaking News",
    startTime: 1.0,
    duration: 3.0,
    direction: "left",  // Options: left, right, up, down
    fontSize: 48
);
```

**Use Cases:**
- Lower thirds
- Titles and credits
- Subtitles
- Breaking news tickers

## 5. Audio Processing

### Audio Mixing

Combine multiple audio sources with custom volume levels.

```csharp
builder.AddInput("voice.mp3")
       .AddInput("music.mp3")
       .AddInput("sfx.mp3")
       .AddAudioMix(
           inputCount: 3,
           weights: new[] { 1.0, 0.3, 0.5 } // Voice full, music 30%, SFX 50%
       );
```

### Audio Ducking

Automatically lower background audio when foreground audio is present (perfect for voice-over with background music).

```csharp
builder.AddInput("voice.mp3")      // Foreground
       .AddInput("music.mp3")      // Background
       .AddAudioDucking(
           foregroundIndex: 0,
           backgroundIndex: 1,
           threshold: -20,         // Trigger at -20dB
           ratio: 4,               // Reduce by 4:1
           attack: 20,             // Attack time in ms
           release: 250            // Release time in ms
       );
```

**Parameters:**
- `threshold`: Level at which ducking activates (-40 to 0 dB)
- `ratio`: Reduction ratio (1 to 20, higher = more reduction)
- `attack`: How quickly ducking applies (milliseconds)
- `release`: How quickly ducking releases (milliseconds)

## 6. Watermark

Add a logo or watermark to video.

```csharp
builder.AddWatermark(
    watermarkPath: "/path/to/logo.png",
    position: "bottom-right",  // top-left, top-right, bottom-left, bottom-right, center
    opacity: 0.7,              // 0.0 to 1.0
    margin: 10                 // Margin from edges in pixels
);
```

## 7. Two-Pass Encoding

For better quality, especially with constrained bitrates.

```csharp
// First pass
var pass1 = new FFmpegCommandBuilder()
    .AddInput("input.mp4")
    .SetOutput("output.mp4")
    .SetVideoCodec("libx264")
    .SetVideoBitrate(5000)
    .SetTwoPassEncoding("/tmp/passlog", pass: 1)
    .Build();

// Second pass
var pass2 = new FFmpegCommandBuilder()
    .AddInput("input.mp4")
    .SetOutput("output.mp4")
    .SetVideoCodec("libx264")
    .SetVideoBitrate(5000)
    .SetTwoPassEncoding("/tmp/passlog", pass: 2)
    .Build();

// Execute both passes sequentially
```

**Benefits:**
- Better quality at same bitrate
- More accurate bitrate targeting
- Recommended for final delivery

## 8. Chapter Markers

Add chapters for navigation in long-form content.

```csharp
var chapters = new List<(TimeSpan, string)>
{
    (TimeSpan.FromSeconds(0), "Introduction"),
    (TimeSpan.FromMinutes(2), "Setup Instructions"),
    (TimeSpan.FromMinutes(10), "Advanced Features"),
    (TimeSpan.FromMinutes(25), "Conclusion")
};

builder.AddChapterMarkers(chapters);
```

## 9. GPU Monitoring and Memory Management

### Detect GPU Capabilities with Memory Info

```csharp
var encoder = new HardwareEncoder(logger, "ffmpeg");
var capabilities = await encoder.DetectHardwareCapabilitiesAsync();

if (capabilities.HasNVENC && capabilities.GpuMemory != null)
{
    Console.WriteLine($"GPU: {capabilities.GpuMemory.GpuName}");
    Console.WriteLine($"Total Memory: {capabilities.GpuMemory.TotalMemoryBytes / 1024.0 / 1024.0 / 1024.0:F2} GB");
    Console.WriteLine($"Free Memory: {capabilities.GpuMemory.FreeMemoryBytes / 1024.0 / 1024.0 / 1024.0:F2} GB");
    Console.WriteLine($"Usage: {capabilities.GpuMemory.UsagePercentage:F1}%");
}
```

### Monitor GPU Utilization

```csharp
var utilization = await encoder.GetGpuUtilizationAsync();

if (utilization != null)
{
    Console.WriteLine($"GPU Usage: {utilization.GpuUsagePercent:F1}%");
    Console.WriteLine($"Memory Usage: {utilization.MemoryUsagePercent:F1}%");
    Console.WriteLine($"Encoder Usage: {utilization.EncoderUsagePercent:F1}%");
    Console.WriteLine($"Temperature: {utilization.TemperatureCelsius:F1}°C");
}
```

### Check GPU Memory Availability

```csharp
// Estimate required memory for encoding
var required = encoder.EstimateRequiredGpuMemory(
    width: 3840,
    height: 2160,
    fps: 30,
    durationSeconds: 600
);

// Check if GPU has sufficient memory
var hasSufficient = await encoder.HasSufficientGpuMemoryAsync(required);

if (!hasSufficient)
{
    Console.WriteLine("Insufficient GPU memory, falling back to CPU encoding");
}
```

## 10. Real-Time Rendering Monitoring

### Setup Render Monitor

```csharp
var monitor = new RenderMonitor(logger, hardwareEncoder);

// Start monitoring when FFmpeg process starts
monitor.StartMonitoring(ffmpegProcess, totalFrames: 9000);

// Parse FFmpeg output lines
foreach (var line in ffmpegOutput)
{
    monitor.ParseProgressLine(line, totalFrames: 9000);
    monitor.ParseErrorLine(line);
    
    // Display current stats
    var stats = monitor.CurrentStats;
    if (stats != null)
    {
        Console.WriteLine($"Progress: {stats.ProgressPercent:F1}%");
        Console.WriteLine($"FPS: {stats.CurrentFps:F1} (avg: {stats.AverageFps:F1})");
        Console.WriteLine($"Bitrate: {stats.CurrentBitrate:F1} kbits/s");
        Console.WriteLine($"Speed: {stats.Speed:F2}x");
        Console.WriteLine($"ETA: {stats.Estimated}");
        Console.WriteLine($"CPU: {stats.CpuUsagePercent:F1}%");
        Console.WriteLine($"Memory: {stats.MemoryUsageMb:F1} MB");
        
        if (stats.GpuStats != null)
        {
            Console.WriteLine($"GPU: {stats.GpuStats.GpuUsagePercent:F1}%");
            Console.WriteLine($"GPU Encoder: {stats.GpuStats.EncoderUsagePercent:F1}%");
        }
    }
}

// Stop monitoring when done
await monitor.StopMonitoringAsync();
```

### Error Detection and Health Monitoring

```csharp
// Check for errors
if (monitor.Errors.Count > 0)
{
    foreach (var error in monitor.Errors)
    {
        Console.WriteLine($"[{error.Timestamp}] {error.Message}");
        Console.WriteLine($"  Recoverable: {error.IsRecoverable}");
    }
}

// Check if there are critical errors
if (monitor.HasCriticalErrors())
{
    Console.WriteLine("Critical errors detected, aborting render");
}

// Get overall health status
var health = monitor.GetHealthStatus();
Console.WriteLine($"Health: {health}"); // Healthy, Degraded, Warning, Critical, Unknown
```

### Generate Preview Frames

```csharp
// Generate preview at specific timestamp
var preview = await monitor.GeneratePreviewFrameAsync(
    inputVideo: "/path/to/video.mp4",
    timestamp: TimeSpan.FromSeconds(30),
    outputPath: "/tmp/preview.jpg",
    width: 640,
    height: 360
);

if (preview != null)
{
    Console.WriteLine($"Preview saved: {preview.FilePath}");
}
```

## 11. Complete Example: Professional Video with All Features

```csharp
var builder = new FFmpegCommandBuilder()
    // Add main video and overlay
    .AddInput("main_video.mp4")
    .AddInput("intro_clip.mp4")
    .AddInput("pip_video.mp4")
    .AddInput("voice.mp3")
    .AddInput("background_music.mp3")
    
    // Add crossfade between intro and main
    .AddCrossfadeTransition(1.5, 5.0)
    
    // Add picture-in-picture
    .AddPictureInPicture(2, "W-w-10", "H-h-10", 0.25)
    
    // Add animated title
    .AddAnimatedTextOverlay("My Video Title", 1.0, 3.0, 0.5, 0.5, 72)
    
    // Add lower third subtitle
    .AddTextOverlay("by John Doe", null, 36, "(w-text_w)/2", "h-100", "white", "black@0.7")
    
    // Add watermark
    .AddWatermark("/path/to/logo.png", "bottom-right", 0.6, 15)
    
    // Mix voice and music with ducking
    .AddAudioDucking(3, 4, -20, 4, 20, 250)
    
    // Add chapters
    .AddChapterMarkers(new[]
    {
        (TimeSpan.Zero, "Introduction"),
        (TimeSpan.FromMinutes(2), "Main Content"),
        (TimeSpan.FromMinutes(10), "Conclusion")
    })
    
    // Set output parameters
    .SetOutput("final_video.mp4")
    .SetVideoCodec("libx264")
    .SetVideoBitrate(8000)
    .SetAudioCodec("aac")
    .SetAudioBitrate(256)
    .SetResolution(1920, 1080)
    .SetFrameRate(30)
    .SetPreset("medium")
    .SetCRF(23)
    .SetMaxBitrate(10000)
    .SetBufferSize(20000);

var command = builder.Build();
```

## Performance Considerations

### GPU Memory Management

- 1080p video: ~300-500 MB GPU memory
- 4K video: ~1-2 GB GPU memory
- Always check available memory before encoding
- Monitor temperature and throttling

### Rendering Speed

- **Hardware encoding**: 5-10x faster than CPU
  - NVENC: Best quality/speed balance
  - AMF: Good for AMD GPUs
  - QuickSync: Good for Intel CPUs
- **Two-pass encoding**: ~2x slower but better quality
- **Complex filters**: May reduce speed by 20-50%

### Best Practices

1. **Use hardware acceleration when available**
2. **Monitor GPU memory to prevent OOM**
3. **Use two-pass for final delivery, single-pass for previews**
4. **Add error detection and recovery**
5. **Generate preview frames for quality checks**
6. **Monitor health status during long renders**
7. **Use appropriate presets for target platforms**

## Troubleshooting

### GPU Memory Issues

If you encounter GPU memory errors:
1. Check available memory: `GetGpuMemoryInfoAsync()`
2. Reduce resolution or complexity
3. Fall back to CPU encoding
4. Process in smaller segments

### Slow Rendering

If rendering is slower than expected:
1. Check GPU utilization (should be >80% when using hardware encoding)
2. Verify hardware encoder is actually being used
3. Reduce filter complexity
4. Check CPU/memory bottlenecks

### Quality Issues

If output quality is poor:
1. Use two-pass encoding
2. Increase bitrate
3. Lower CRF value (lower = better quality)
4. Use slower preset (veryslow, slower, slow)
5. Check source quality

## API Reference

See the following files for complete API documentation:
- `FFmpegCommandBuilder.cs` - All rendering commands
- `HardwareEncoder.cs` - GPU detection and monitoring
- `RenderMonitor.cs` - Real-time progress and error tracking
- `RenderPresets.cs` - Platform-specific presets

## Examples Repository

For complete working examples, see:
- `Aura.Tests/Services/FFmpeg/FFmpegCommandBuilderAdvancedFeaturesTests.cs`
- `Aura.Tests/Services/Render/HardwareEncoderMonitoringTests.cs`
- `Aura.Tests/Services/Render/RenderMonitorTests.cs`
