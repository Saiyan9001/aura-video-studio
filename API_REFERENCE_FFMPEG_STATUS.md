# FFmpeg Status API - Quick Reference

## Endpoint

```
GET /api/system/ffmpeg/status
```

## Success Response (200 OK)

### Example 1: FFmpeg Installed with NVIDIA GPU

```json
{
  "installed": true,
  "valid": true,
  "version": "4.4.2",
  "path": "/usr/bin/ffmpeg",
  "source": "PATH",
  "error": null,
  "versionMeetsRequirement": true,
  "minimumVersion": "4.0",
  "hardwareAcceleration": {
    "nvencSupported": true,
    "amfSupported": false,
    "quickSyncSupported": false,
    "videoToolboxSupported": false,
    "availableEncoders": [
      "h264_nvenc",
      "hevc_nvenc",
      "av1_nvenc"
    ]
  },
  "correlationId": "0HN7UQOQK5P8B:00000001"
}
```

### Example 2: FFmpeg Not Found

```json
{
  "installed": false,
  "valid": false,
  "version": null,
  "path": null,
  "source": "None",
  "error": "FFmpeg not found in any location. Install managed FFmpeg or configure path in Settings.",
  "versionMeetsRequirement": false,
  "minimumVersion": "4.0",
  "hardwareAcceleration": {
    "nvencSupported": false,
    "amfSupported": false,
    "quickSyncSupported": false,
    "videoToolboxSupported": false,
    "availableEncoders": []
  },
  "correlationId": "0HN7UQOQK5P8C:00000001"
}
```

### Example 3: FFmpeg Installed (Managed) with AMD GPU

```json
{
  "installed": true,
  "valid": true,
  "version": "5.1.2",
  "path": "C:\\Program Files\\AuraVideoStudio\\ffmpeg\\5.1.2\\bin\\ffmpeg.exe",
  "source": "Managed",
  "error": null,
  "versionMeetsRequirement": true,
  "minimumVersion": "4.0",
  "hardwareAcceleration": {
    "nvencSupported": false,
    "amfSupported": true,
    "quickSyncSupported": false,
    "videoToolboxSupported": false,
    "availableEncoders": [
      "h264_amf",
      "hevc_amf"
    ]
  },
  "correlationId": "0HN7UQOQK5P8D:00000001"
}
```

### Example 4: Old Version FFmpeg (Below 4.0)

```json
{
  "installed": true,
  "valid": true,
  "version": "3.4.0",
  "path": "/opt/ffmpeg/bin/ffmpeg",
  "source": "Configured",
  "error": null,
  "versionMeetsRequirement": false,
  "minimumVersion": "4.0",
  "hardwareAcceleration": {
    "nvencSupported": false,
    "amfSupported": false,
    "quickSyncSupported": false,
    "videoToolboxSupported": false,
    "availableEncoders": [
      "libx264"
    ]
  },
  "correlationId": "0HN7UQOQK5P8E:00000001"
}
```

### Example 5: Intel QuickSync Support

```json
{
  "installed": true,
  "valid": true,
  "version": "6.0",
  "path": "/usr/bin/ffmpeg",
  "source": "PATH",
  "error": null,
  "versionMeetsRequirement": true,
  "minimumVersion": "4.0",
  "hardwareAcceleration": {
    "nvencSupported": false,
    "amfSupported": false,
    "quickSyncSupported": true,
    "videoToolboxSupported": false,
    "availableEncoders": [
      "h264_qsv",
      "hevc_qsv",
      "av1_qsv"
    ]
  },
  "correlationId": "0HN7UQOQK5P8F:00000001"
}
```

## Error Response (500 Internal Server Error)

```json
{
  "type": "https://docs.aura.studio/errors/E500",
  "title": "FFmpeg Status Error",
  "status": 500,
  "detail": "Failed to get FFmpeg status: Unexpected error message",
  "correlationId": "0HN7UQOQK5P8G:00000001"
}
```

## Response Fields

| Field | Type | Description |
|-------|------|-------------|
| `installed` | boolean | True if FFmpeg is found and valid |
| `valid` | boolean | True if FFmpeg executable runs successfully |
| `version` | string? | FFmpeg version string (e.g., "4.4.2") |
| `path` | string? | Full path to FFmpeg executable |
| `source` | string | Source of FFmpeg: "Managed", "PATH", "Configured", or "None" |
| `error` | string? | Error message if FFmpeg not found or invalid |
| `versionMeetsRequirement` | boolean | True if version is 4.0 or higher |
| `minimumVersion` | string | Minimum required version (currently "4.0") |
| `hardwareAcceleration.nvencSupported` | boolean | NVIDIA NVENC available |
| `hardwareAcceleration.amfSupported` | boolean | AMD AMF available |
| `hardwareAcceleration.quickSyncSupported` | boolean | Intel QuickSync available |
| `hardwareAcceleration.videoToolboxSupported` | boolean | Apple VideoToolbox available (macOS) |
| `hardwareAcceleration.availableEncoders` | string[] | List of available hardware encoder names |
| `correlationId` | string | Unique request ID for tracing |

## Usage Examples

### cURL

```bash
curl http://localhost:5005/api/system/ffmpeg/status
```

### JavaScript/TypeScript

```typescript
const response = await fetch('/api/system/ffmpeg/status');
const status = await response.json();

console.log('FFmpeg installed:', status.installed);
console.log('Has NVENC:', status.hardwareAcceleration.nvencSupported);

if (status.hardwareAcceleration.nvencSupported) {
  console.log('5-10x faster rendering with NVIDIA GPU!');
}
```

### C# (.NET)

```csharp
var status = await _ffmpegStatusService.GetStatusAsync();

if (status.Installed && status.Valid)
{
    _logger.LogInformation(
        "FFmpeg {Version} available at {Path} with HW accel: {HwAccel}",
        status.Version,
        status.Path,
        status.HardwareAcceleration.NvencSupported ? "NVENC" :
        status.HardwareAcceleration.AmfSupported ? "AMF" :
        status.HardwareAcceleration.QuickSyncSupported ? "QuickSync" :
        "None"
    );
}
```

### Python

```python
import requests

response = requests.get('http://localhost:5005/api/system/ffmpeg/status')
status = response.json()

if status['installed']:
    print(f"FFmpeg {status['version']} is ready!")
    
    hw_accel = status['hardwareAcceleration']
    if hw_accel['nvencSupported']:
        print("NVIDIA GPU acceleration available")
    elif hw_accel['amfSupported']:
        print("AMD GPU acceleration available")
    elif hw_accel['quickSyncSupported']:
        print("Intel QuickSync acceleration available")
    else:
        print("CPU-only encoding (slower)")
```

## Hardware Acceleration Types

| Type | GPU Vendor | Performance | Codecs |
|------|-----------|-------------|---------|
| **NVENC** | NVIDIA | 5-10x faster | H.264, HEVC, AV1 |
| **AMF** | AMD | 5-10x faster | H.264, HEVC |
| **QuickSync** | Intel | 3-5x faster | H.264, HEVC, AV1 |
| **VideoToolbox** | Apple | 3-5x faster | H.264, HEVC |
| **CPU** | Any | 1x baseline | All codecs |

## Common Use Cases

### Check if system is ready for video rendering

```typescript
const status = await fetch('/api/system/ffmpeg/status').then(r => r.json());

const isReady = status.installed && 
                status.valid && 
                status.versionMeetsRequirement;

if (isReady) {
  // Proceed with video rendering
} else {
  // Show installation wizard
}
```

### Select optimal encoder based on hardware

```typescript
const status = await fetch('/api/system/ffmpeg/status').then(r => r.json());

let encoder = 'libx264'; // CPU fallback

if (status.hardwareAcceleration.nvencSupported) {
  encoder = 'h264_nvenc'; // Best quality/performance
} else if (status.hardwareAcceleration.amfSupported) {
  encoder = 'h264_amf';
} else if (status.hardwareAcceleration.quickSyncSupported) {
  encoder = 'h264_qsv';
}

console.log('Using encoder:', encoder);
```

### Display user-friendly hardware info

```typescript
const status = await fetch('/api/system/ffmpeg/status').then(r => r.json());

const hardwareInfo = [];

if (status.hardwareAcceleration.nvencSupported) {
  hardwareInfo.push('NVIDIA GPU acceleration');
}
if (status.hardwareAcceleration.amfSupported) {
  hardwareInfo.push('AMD GPU acceleration');
}
if (status.hardwareAcceleration.quickSyncSupported) {
  hardwareInfo.push('Intel QuickSync');
}

const message = hardwareInfo.length > 0
  ? `Hardware acceleration: ${hardwareInfo.join(', ')}`
  : 'CPU-only encoding (slower)';

console.log(message);
```
