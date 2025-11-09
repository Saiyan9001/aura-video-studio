# Ollama Auto-Detection Implementation

## Overview
This implementation adds automatic detection of Ollama to the Settings/Downloads page (Engines tab), addressing the issue where Ollama often appears as "not found" until the user manually clicks "Auto-detect".

## Changes Made

### New Files
1. **`src/hooks/useOllamaDetection.ts`**
   - Custom React hook for detecting Ollama on localhost:11434
   - Features:
     - Auto-detects on mount (configurable)
     - Probes `http://localhost:11434/api/tags` endpoint
     - 2-second timeout per attempt with AbortController
     - Single retry with 500ms backoff on failure
     - Session caching for positive detections (5 minutes)
     - Returns neutral state (`null`) while checking to avoid flicker

2. **`src/components/Engines/OllamaCard.tsx`**
   - Dedicated Ollama card for Settings/Downloads page
   - Features:
     - Status badges: "Detected" (green), "Not Found" (subtle), "Checking..." (neutral)
     - Auto-detect button in primary actions (next to status badge)
     - Helper text: "If Ollama is running locally (port 11434), detection is automatic."
     - Info box explaining automatic detection
     - Success/not found messaging with appropriate styling

3. **`src/hooks/__tests__/useOllamaDetection.test.ts`**
   - Comprehensive test suite with 10 tests covering:
     - Initialization behavior
     - Auto-detection on mount
     - Cache usage and expiration
     - Retry logic
     - Manual detection trigger
     - Timeout handling
     - Error scenarios

### Modified Files
1. **`src/components/Engines/EnginesTab.tsx`**
   - Added import for OllamaCard
   - Added OllamaCard component after FFmpegCard
   - Minimal 2-line change

## User Experience

### Before
- Ollama status shows as "Not Found" or "Unknown"
- User must manually click "Show Details" → "Auto-Detect" button
- Button is buried in details section
- No indication that detection is automatic

### After
- Ollama is automatically detected within ~1 second of page load
- Status immediately shows "Detected" when Ollama is running
- Auto-detect button is prominently placed next to status badge
- Helper text explains that detection is automatic
- Subsequent visits use cached detection (faster, no flicker)

## Technical Details

### Detection Process
1. On component mount, hook checks sessionStorage cache
2. If cached positive result exists and is fresh (<5 minutes), use it
3. Otherwise, probe `http://localhost:11434/api/tags` with 2s timeout
4. If first attempt fails, retry once after 500ms delay
5. Cache positive results in sessionStorage
6. Update component state with detection result

### Error Handling
- Network errors: Silent failure, shows "Not Found"
- Timeout: Silent failure via AbortController
- CORS errors: Silent failure (expected for localhost probe)
- Cache errors: Logged as warning, continues with fresh detection

### Cache Strategy
- **Storage**: sessionStorage (cleared on browser close)
- **Duration**: 5 minutes for positive detections only
- **Key**: `ollama_detection_cache`
- **Format**: `{ isDetected: boolean, timestamp: number }`
- **Rationale**: Reduces unnecessary network calls, improves performance

## Scope Compliance

✅ **Changes strictly limited to Settings/Downloads page**
- Only modified EnginesTab and created OllamaCard
- No modifications to wizard components

✅ **No modifications to OllamaDependencyCard**
- Wizard component unchanged (as per PR 2 requirements)

✅ **No modifications to global toast/notice components**
- As per PR 38 requirements

✅ **No modifications to FFmpegCard**
- As per PR 36 requirements

✅ **Local hook used only by OllamaCard**
- useOllamaDetection is not imported anywhere else

## Testing

### Unit Tests
Run tests: `npm test -- src/hooks/__tests__/useOllamaDetection.test.ts`

All 10 tests pass in ~1.8 seconds:
- ✅ Initialize with null state when autoDetect is false
- ✅ Auto-detect on mount when autoDetect is true
- ✅ Return cached detection result
- ✅ Retry once on failure
- ✅ Return false when detection fails after retry
- ✅ Cache positive detection results
- ✅ Allow manual detection trigger
- ✅ Handle timeout with AbortController
- ✅ Not use expired cache
- ✅ Ignore cache for negative results

### Manual Testing
1. Start Ollama: `ollama serve` (or install and run Ollama Desktop)
2. Navigate to Settings → Downloads → Engines tab
3. Verify Ollama card shows "Detected" badge within ~1 second
4. Stop Ollama and click Auto-detect button
5. Verify status changes to "Not Found"
6. Restart Ollama and click Auto-detect button
7. Verify status changes back to "Detected"

## Future Enhancements (Not in Scope)
- Add detection toggle in settings to disable auto-detection
- Add custom port configuration for Ollama
- Show detected Ollama version and available models
- Add refresh interval for auto-detection
- Add notification when Ollama becomes available/unavailable

## Related PRs
- PR #2: First-run wizard changes (not touched)
- PR #36: FFmpeg card implementation (not touched)
- PR #38: Toast/Notice component updates (not touched)
