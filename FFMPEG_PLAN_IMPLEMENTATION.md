# FFmpeg Plan Builder Implementation Summary

## Overview
This document summarizes the implementation of the deterministic FFmpeg pipeline with guardrails and golden tests for the Aura Video Studio project.

## Implementation Completed

### 1. Guardrails and Validation System

#### ValidationResult Class
Added a new `ValidationResult` class to provide structured validation feedback:
- `IsValid`: Boolean indicating if configuration is valid
- `ErrorMessage`: Descriptive error message for users
- `SuggestedFix`: Actionable suggestion to fix the issue
- `SuggestedEncoder`: Alternative encoder recommendation
- `SuggestedQuality`: Corrected quality settings

#### ValidateConfiguration Method
Comprehensive validation covering:
- **Encoder Availability**: Checks if selected encoder is available on the system
- **Frame Rate**: Validates FPS is within 1-120 range
- **Quality Level**: Ensures quality is within 0-100 range
- **Resolution**: Rejects resolutions smaller than 320x240
- **Bitrate Warnings**: Warns about excessive bitrate for given resolution
- **Codec Matching**: Ensures encoder supports the selected codec (e.g., AV1 requires NVENC_AV1)

#### GetSafeDefaultEncoder Method
Provides safe fallback encoder selection:
1. Prefers NVENC H264 (best compatibility and performance)
2. Falls back to AMF H264 (AMD hardware)
3. Falls back to QSV H264 (Intel QuickSync)
4. Ultimate fallback to x264 (software encoding)

### 2. Audio DSP Chain Implementation

#### BuildAudioProcessingCommand Method
Implements the complete audio processing pipeline:

**DSP Chain Order:**
1. **High-Pass Filter (HPF)**: Removes rumble below 80Hz
   ```
   highpass=f=80
   ```

2. **De-esser**: Reduces harsh sibilance around 6-8kHz
   ```
   treble=g=-3:f=7000:w=2000
   ```

3. **Compressor**: Dynamic range control with 3:1 ratio, -18dB threshold
   ```
   acompressor=threshold=-18dB:ratio=3:attack=20:release=250:makeup=6dB
   ```

4. **Limiter**: Prevents peaks above ceiling (-1 dBFS)
   ```
   alimiter=limit=-1dB:attack=5:release=50
   ```

5. **LUFS Normalization**: Targets -14 LUFS for YouTube standard
   ```
   loudnorm=I=-14:TP=-1:LRA=11
   ```

**Output Format:**
- Sample Rate: 48kHz (industry standard for video)
- Bit Depth: 24-bit (pcm_s24le)
- Format: WAV (uncompressed)

**Supported LUFS Targets:**
- `-16 LUFS`: Voice-only content
- `-14 LUFS`: YouTube standard (default)
- `-12 LUFS`: Music-forward content

### 3. Deterministic Video Encoding

#### Encoder-Specific Parameters

**x264 (Software):**
- CRF Range: 28 (fast/lower) → 14 (slow/higher)
- Presets: veryfast → faster → fast → medium → slow
- Tune: film
- Profile: high
- Pixel Format: yuv420p

**NVENC H264/HEVC:**
- Rate Control: Constant Quality (CQ)
- CQ Range: 33 (fast/lower) → 18 (slow/higher)
- Presets: p5 (fast) → p6 → p7 (slow)
- RC Lookahead: 16 frames
- Spatial AQ: Enabled
- Temporal AQ: Enabled
- B-frames: 3

**NVENC AV1:**
- Rate Control: Constant Quality (CQ)
- CQ Range: 38 (fast/lower) → 22 (slow/higher)
- Presets: p5 (fast) → p6 → p7 (slow)
- Note: Requires RTX 40/50 series GPU

**Frame Rate & GOP:**
- Constant Frame Rate (CFR) enforced with `-r`
- GOP Size: 2 × FPS (e.g., 60 for 30fps, 120 for 60fps)
- Scene-cut keyframes: Threshold 40

**Color Space (BT.709 for HD/4K):**
- `-colorspace bt709`
- `-color_trc bt709`
- `-color_primaries bt709`
- `-pix_fmt yuv420p`

### 4. Comprehensive Test Suite

#### Test Coverage (34 Tests, All Passing)

**Guardrail Tests (8 tests):**
- `ValidateConfiguration_Should_RejectUnavailableEncoder`
- `ValidateConfiguration_Should_RejectInvalidFrameRate`
- `ValidateConfiguration_Should_RejectInvalidQualityLevel`
- `ValidateConfiguration_Should_RejectTooSmallResolution`
- `ValidateConfiguration_Should_WarnAboutHighBitrateOnLowResolution`
- `ValidateConfiguration_Should_RejectCodecMismatch`
- `GetSafeDefaultEncoder_Should_PreferNvencH264`
- `GetSafeDefaultEncoder_Should_FallbackToX264`

**Golden Args Tests (4 tests):**
- `BuildRenderCommand_X264_HighQuality_Should_ProduceExpectedArgs`
  - Validates: CRF 16, preset slow, tune film, profile high, GOP 60
- `BuildRenderCommand_NVENC_H264_Should_ProduceExpectedArgs`
  - Validates: rc cq, preset p7, lookahead 16, spatial-aq, temporal-aq, bf 3
- `BuildRenderCommand_NVENC_HEVC_Should_ProduceExpectedArgs`
  - Validates: hevc_nvenc codec, rc cq, preset p7
- `BuildRenderCommand_NVENC_AV1_Should_ProduceExpectedArgs`
  - Validates: av1_nvenc codec, rc cq, CQ 22-26 range

**Color Space Tests (3 tests):**
- `BuildRenderCommand_Should_UseBT709ColorSpace` (1080p, 720p, 4K)
  - Validates: BT.709 color space for all HD/4K resolutions

**Audio DSP Chain Tests (3 tests):**
- `BuildAudioProcessingCommand_Should_IncludeDspChain`
  - Validates: HPF, de-esser, compressor, limiter, LUFS normalization
- `BuildAudioProcessingCommand_Should_Output48kHz24Bit`
  - Validates: 48kHz sample rate, 24-bit depth, WAV format
- `BuildAudioProcessingCommand_Should_SupportDifferentLufsTargets`
  - Validates: -16, -14, -12 LUFS targets

**Existing Tests (16 tests):**
- Basic parameter inclusion
- NVENC encoder usage
- Quality to CRF mapping
- GOP size calculation
- Scene-cut keyframes
- Filter graph generation
- Encoder detection
- Audio settings

## Definition of Done ✅

### ✅ Render args deterministic
- All encoder parameters are deterministically calculated based on quality settings
- GOP size always 2×FPS
- Color space consistently BT.709
- Audio consistently 48kHz/AAC

### ✅ Illegal configs blocked with actionable fixes
- `ValidateConfiguration` method checks all critical parameters
- `ValidationResult` provides:
  - Clear error messages
  - Actionable fix suggestions
  - Recommended alternative encoders/settings
- Safe defaults available via `GetSafeDefaultEncoder`

### ✅ Tests pass
- 34/34 tests passing
- Golden args validated for each encoder type
- Guardrails tested for all validation scenarios
- Color space tests ensure correct BT.709 usage
- Audio DSP chain fully tested

## Usage Examples

### Example 1: Validate Configuration Before Rendering
```csharp
var builder = new FFmpegPlanBuilder();
var spec = RenderPresets.YouTube1080p;
var quality = new QualitySettings { QualityLevel = 80, Fps = 30 };
var availableEncoders = FFmpegPlanBuilder.DetectAvailableEncoders(ffmpegOutput);

var validation = builder.ValidateConfiguration(spec, quality, EncoderType.NVENC_H264, availableEncoders);

if (!validation.IsValid)
{
    Console.WriteLine($"Error: {validation.ErrorMessage}");
    Console.WriteLine($"Fix: {validation.SuggestedFix}");
    
    if (validation.SuggestedEncoder.HasValue)
    {
        // Use suggested encoder instead
        encoder = validation.SuggestedEncoder.Value;
    }
}
```

### Example 2: Process Audio with DSP Chain
```csharp
var builder = new FFmpegPlanBuilder();

// Process voice narration to -16 LUFS (voice-only standard)
string voiceCommand = builder.BuildAudioProcessingCommand(
    "narration.wav",
    "narration_processed.wav",
    targetLufs: -16.0,
    peakCeiling: -1.0
);

// Process music to -14 LUFS (YouTube standard)
string musicCommand = builder.BuildAudioProcessingCommand(
    "background_music.wav",
    "music_processed.wav",
    targetLufs: -14.0,
    peakCeiling: -1.0
);
```

### Example 3: Get Safe Default Encoder
```csharp
var ffmpegOutput = await RunFFmpegEncodersCommand();
var availableEncoders = FFmpegPlanBuilder.DetectAvailableEncoders(ffmpegOutput);

// Get best available encoder
var encoder = FFmpegPlanBuilder.GetSafeDefaultEncoder(availableEncoders);
// Returns: NVENC_H264 > AMF_H264 > QSV_H264 > X264
```

## Technical Specifications Met

✅ **CFR + GOP = 2×fps**: Implemented via `-r {fps} -g {fps*2}`
✅ **Scene-cut keyframes**: Implemented via `-sc_threshold 40`
✅ **x264 parameters**: CRF 28-14, presets veryfast-slow, tune film, profile high, yuv420p
✅ **NVENC H264/HEVC**: RC CQ, CQ 33-18, presets p5-p7, lookahead 16, spatial-aq, temporal-aq, bf 3
✅ **NVENC AV1**: RC CQ, CQ 38-22, presets p5-p7
✅ **Audio DSP chain**: HPF → De-esser → Compressor → Limiter
✅ **Audio export**: 48kHz/24-bit WAV
✅ **LUFS normalization**: -14 LUFS (YouTube), -16 (voice), -12 (music)
✅ **Peak ceiling**: -1 dBFS
✅ **Guardrails**: Illegal combos blocked with actionable fixes
✅ **Tests**: Golden args, color space, guardrails all tested

## Files Modified

1. **Aura.Core/Rendering/FFmpegPlanBuilder.cs** (+240 lines)
   - Added `ValidationResult` class
   - Added `ValidateConfiguration` method
   - Added `GetSafeDefaultEncoder` method
   - Added `BuildAudioProcessingCommand` method

2. **Aura.Tests/FFmpegPlanBuilderTests.cs** (+320 lines)
   - Added 20 new comprehensive tests
   - All 34 tests passing

## Next Steps (Optional Enhancements)

1. **UI Integration**: Wire up validation to show "Fix" buttons in the UI
2. **Real-time LUFS Metering**: Add live audio level monitoring during rendering
3. **HDR Support**: Extend color space handling for BT.2020/HDR10
4. **GPU Detection**: Integrate with HardwareDetector to automatically validate encoder availability
5. **Preset System**: Create named presets (YouTube, Instagram, TikTok) with optimal settings
