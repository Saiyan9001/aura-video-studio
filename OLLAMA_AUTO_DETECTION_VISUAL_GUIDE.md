# Ollama Auto-Detection - Visual Guide

## UI Components Overview

### Ollama Card Location
The Ollama card is displayed on the **Settings → Downloads → Engines** tab, positioned between the FFmpeg card and the list of other engines.

```
Settings Page
└── Downloads Tab
    └── Engines Tab
        ├── FFmpeg Card (existing)
        ├── Ollama Card (NEW - auto-detects)
        └── Other Engines (Stable Diffusion, etc.)
```

### Ollama Card Structure

```
┌─────────────────────────────────────────────────────────────────┐
│  🎯 Ollama (Local AI) [Optional]        [Detected ✓] [Auto-Detect↻] │
│  Run AI models locally for script generation. Privacy-focused    │
│  alternative to cloud APIs.                                      │
├─────────────────────────────────────────────────────────────────┤
│  ℹ️  If Ollama is running locally (port 11434), detection is     │
│     automatic.                                                   │
│                                                                   │
│  ✅ Ollama is running and available at http://localhost:11434    │
└─────────────────────────────────────────────────────────────────┘
```

## Status States

### 1. Detected (Ollama Running)
```
┌─────────────────────────────────────────────────────────────────┐
│  ✓ Ollama (Local AI) [Optional]        [Detected ✓] [Auto-Detect↻] │
│                                                                   │
│  ✅ Ollama is running and available at http://localhost:11434    │
└─────────────────────────────────────────────────────────────────┘
```
- **Icon**: Green checkmark (✓)
- **Status Badge**: Green "Detected" badge with checkmark icon
- **Message**: Green success box with confirmation text
- **Button State**: Enabled, can re-check if needed

### 2. Not Found (Ollama Not Running)
```
┌─────────────────────────────────────────────────────────────────┐
│  ⚠️ Ollama (Local AI) [Optional]        [Not Found] [Auto-Detect↻] │
│                                                                   │
│  Ollama is not currently running. It's optional and can be       │
│  configured later in Settings if you want to use local AI models.│
│  [Learn More About Ollama →]                                     │
└─────────────────────────────────────────────────────────────────┘
```
- **Icon**: Gray warning icon (⚠️)
- **Status Badge**: Subtle "Not Found" badge
- **Message**: Gray info box with explanation and link to Ollama website
- **Button State**: Enabled, can retry detection

### 3. Checking (Detection in Progress)
```
┌─────────────────────────────────────────────────────────────────┐
│  ⏳ Ollama (Local AI) [Optional]        [Checking...⟳] [Auto-Detect↻] │
│                                                                   │
│  ℹ️  If Ollama is running locally (port 11434), detection is     │
│     automatic.                                                   │
└─────────────────────────────────────────────────────────────────┘
```
- **Icon**: Spinner animation (⏳)
- **Status Badge**: Neutral "Checking..." badge with spinner
- **Message**: Info box with helper text
- **Button State**: Disabled during check

## Component Hierarchy

```typescript
<OllamaCard>
  <CardHeader>
    <div className="header">
      <div className="title">
        {getStatusIcon()}              // ✓, ⚠️, or ⏳
        <Text>Ollama (Local AI)</Text>
        <Badge>Optional</Badge>
      </div>
      <div className="actions">
        {getStatusBadge()}             // Detected, Not Found, or Checking...
        <Button icon={ArrowSync}>Auto-Detect</Button>
      </div>
    </div>
    <Text className="helperText">
      Run AI models locally for script generation. Privacy-focused
      alternative to cloud APIs.
    </Text>
  </CardHeader>
  
  <CardPreview className="content">
    <div className="infoBox">        // Blue info box
      ℹ️ If Ollama is running locally (port 11434), detection is automatic.
    </div>
    
    {/* Conditional success/error message based on detection result */}
  </CardPreview>
</OllamaCard>
```

## Auto-Detection Flow

```
Page Load
    ↓
useOllamaDetection(true)
    ↓
Check sessionStorage cache
    ├─ Cached & Fresh? → Display cached result (instant)
    └─ No cache/Expired? → Continue to probe
        ↓
    Probe: fetch('http://localhost:11434/api/tags')
        ├─ Timeout: 2000ms
        └─ AbortController for cleanup
        ↓
    First attempt result
        ├─ Success (200 OK) → Display "Detected", cache result
        └─ Failure → Wait 500ms, retry once
            ↓
        Second attempt result
            ├─ Success → Display "Detected", cache result
            └─ Failure → Display "Not Found"
```

## User Interactions

### Automatic Detection (On Page Load)
1. User navigates to Settings → Downloads → Engines
2. OllamaCard renders with "Checking..." status
3. Within ~1 second:
   - If Ollama running: Changes to "Detected" ✓
   - If Ollama not running: Changes to "Not Found" ⚠️
4. Result cached in sessionStorage for 5 minutes

### Manual Detection (Auto-Detect Button)
1. User clicks "Auto-Detect" button
2. Button becomes disabled, status shows "Checking..."
3. New detection attempt (bypasses cache)
4. Within ~1 second, status updates to result
5. New result cached in sessionStorage

### Session Caching Behavior
- **First visit**: ~1 second for detection
- **Subsequent visits (within 5 min)**: Instant (uses cache)
- **After 5 minutes**: Fresh detection on next visit
- **Cache cleared on**: Browser close, manual cache clear

## Styling Details

### Colors (Fluent UI Tokens)
- **Detected**: `colorPaletteGreenForeground1` / `colorPaletteGreenBackground1`
- **Not Found**: `colorNeutralForeground3` / `colorNeutralBackground2`
- **Checking**: `colorNeutralForeground1` / `colorNeutralBackground1`
- **Info Box**: `colorBrandForeground1` / `colorNeutralBackground2`

### Icons
- **Detected**: `Checkmark24Regular` (32px, green)
- **Not Found**: `Warning24Regular` (32px, gray)
- **Checking**: `Spinner` (medium size)
- **Auto-Detect Button**: `ArrowSync24Regular`
- **Info Box**: `Info24Regular`

### Spacing
- Card padding: `spacingVerticalM`
- Gap between elements: `spacingVerticalS`
- Header gap: `spacingHorizontalS`
- Info box padding: `spacingVerticalS`

## Comparison with Wizard Component

### OllamaDependencyCard (Wizard - UNCHANGED)
- Location: First-run wizard, dependencies step
- Auto-detection: Optional via prop `autoDetect={true}`
- Button location: Bottom of card, in separate section
- No session caching
- Simpler UI (no info boxes)

### OllamaCard (Settings/Downloads - NEW)
- Location: Settings → Downloads → Engines tab
- Auto-detection: Always on by default
- Button location: Card header, next to status badge
- Session caching enabled
- Enhanced UI with info boxes and helper text
- Prominent status display

## Browser Compatibility
- ✅ Chrome/Edge (tested)
- ✅ Firefox (expected to work)
- ✅ Safari (expected to work with CORS limitations)
- ℹ️ AbortController supported in all modern browsers
- ℹ️ sessionStorage supported in all modern browsers
- ⚠️ CORS may block probe in some browsers (fails silently to "Not Found")
