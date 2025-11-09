# Mandatory First-Run Setup Wizard - Implementation Summary

## Overview

This document describes the mandatory first-run setup wizard implementation that ensures all new users complete essential configuration before accessing the application.

## Key Features

### 1. Mandatory Full-Screen Wizard

The setup wizard is now completely mandatory and displayed as a full-screen overlay:

- **Cannot be dismissed**: No "X" button, no "Skip" button, no "Save and Exit" option
- **Blocks all navigation**: Sidebar and main navigation are hidden during setup
- **Full-screen overlay**: Wizard is rendered at `z-index: 10000` covering the entire app
- **Completion required**: User cannot access any features until setup is complete

### 2. Simplified 5-Step Flow

Reduced from 9 steps to 5 essential mandatory steps:

#### Step 1: Welcome
- Clear welcome message: "Welcome to Aura Video Studio - Let's get you set up!"
- Brief explanation of what will be configured
- Single "Get Started" button

#### Step 2: FFmpeg Installation (REQUIRED)
- **Why required**: "FFmpeg is the industry-standard tool for video processing. Aura uses it to render your videos, add transitions, apply effects, and export in various formats. Without FFmpeg, video generation cannot proceed."
- **Auto-installation**: One-click install that automatically:
  - Detects OS (Windows/Mac/Linux)
  - Downloads appropriate FFmpeg version
  - Extracts to application directory
  - Sets up PATH automatically
  - Verifies installation
- **Validation**: Cannot proceed without FFmpeg installed
- **Error handling**: Shows clear error message if installation fails with retry option

#### Step 3: Provider Configuration (AT LEAST ONE REQUIRED)
- **Why required**: "LLM providers power the AI script generation. You need at least one configured to create video scripts automatically. Premium providers (OpenAI, Anthropic) offer higher quality, while offline mode provides basic functionality without API keys."
- **Options**:
  - Configure premium providers (OpenAI, Anthropic, Google Gemini)
  - Use offline mode (rule-based generation)
- **Prominent "Skip for now - Use offline mode" button**: 
  - One-click to enable offline mode
  - Shows toast: "Offline Mode Enabled - Using rule-based script generation"
  - Automatically marks provider requirement as satisfied
- **Validation**: Cannot proceed without at least one provider configured OR offline mode enabled
- **Clear labeling**: "Required for core functionality"
- **Error message**: Shows red warning card if user tries to proceed without any provider

#### Step 4: Workspace Setup (REQUIRED)
- **Why required**: "Aura needs to know where to save your generated videos and temporary files. We've pre-filled sensible defaults for your operating system, but you can customize these locations."
- **Pre-filled defaults**:
  - Windows: `%USERPROFILE%\Videos\Aura`
  - macOS: `~/Movies/Aura`
  - Linux: `~/Videos/Aura`
- **Features**:
  - Browse button for custom location selection
  - Path validation
  - Auto-creates directories if they don't exist
- **Validation**: Cannot proceed without workspace location configured

#### Step 5: Setup Complete
- **Success screen** with checkmarks showing:
  - ✓ FFmpeg installed and ready
  - ✓ X LLM provider(s) configured OR Offline mode enabled
  - ✓ Workspace configured
- **"Start Creating Videos" button**: Completes setup and navigates to main app
- **No "Never show again" option**: Setup is truly first-run only, stored in database

### 3. Progress Indicators

- **Step counter**: "Step X of 5 - Required Setup" at top of wizard
- **Progress bar**: Visual progress indicator showing current step
- **Step labels**: Clear labels for each step (not just numbers)
- **Cannot skip ahead**: Can only go back to previous steps, not forward

### 4. Validation at Each Step

Each step has strict validation before allowing "Next":

```typescript
// Step 1 → Step 2: FFmpeg check
if (!ffmpegReady) {
  showFailureToast({
    title: 'FFmpeg Required',
    message: 'Please install FFmpeg before continuing.'
  });
  return; // Blocks navigation
}

// Step 2 → Step 3: Provider check
if (!hasAtLeastOneProvider) {
  showFailureToast({
    title: 'Provider Required',
    message: 'Please configure at least one LLM provider or choose offline mode.'
  });
  return; // Blocks navigation
}

// Step 3 → Step 4: Workspace check
if (!state.workspacePreferences?.defaultSaveLocation) {
  showFailureToast({
    title: 'Workspace Required',
    message: 'Please configure your workspace location.'
  });
  return; // Blocks navigation
}
```

### 5. FFmpeg Auto-Installation

The backend implements full FFmpeg auto-installation:

**Backend Implementation** (`DependencyInstaller.cs`):
1. Detects operating system
2. Downloads appropriate FFmpeg build:
   - **Windows**: FFmpeg 7.0.2 full build from GyanD/codexffmpeg
   - **Linux**: Static build from johnvansickle.com
   - **macOS**: Latest build from evermeet.cx
3. Extracts to `%LOCALAPPDATA%\Aura\ffmpeg` (or equivalent)
4. Sets up PATH
5. Verifies installation
6. Reports progress via Server-Sent Events (SSE)

**Frontend Integration** (`FFmpegDependencyCard.tsx`):
- Shows current FFmpeg status (installed, version, path)
- "Install Managed FFmpeg" button triggers auto-installation
- Progress bar shows download and extraction progress
- Auto-verifies after installation
- Calls `onInstallComplete` callback to enable "Next" button

### 6. Configuration Gate

After setup completes, the `ConfigurationGate` component still validates settings on app load:
- Checks if first-run is complete
- Validates FFmpeg is still available
- Validates workspace paths are valid
- Shows non-dismissible error if configuration becomes invalid
- Allows re-running setup from Settings

## Technical Implementation

### App.tsx Changes

```typescript
// Show mandatory wizard as full-screen overlay if first run
if (shouldShowOnboarding) {
  return (
    <QueryClientProvider client={queryClient}>
      <ThemeContext.Provider value={{ isDarkMode, toggleTheme }}>
        <FluentProvider theme={isDarkMode ? webDarkTheme : webLightTheme}>
          <BrowserRouter>
            <FirstRunWizard
              onComplete={async () => {
                setShouldShowOnboarding(false);
                await markFirstRunCompleted();
              }}
            />
          </BrowserRouter>
        </FluentProvider>
      </ThemeContext.Provider>
    </QueryClientProvider>
  );
}

// Regular app flow (only after setup complete)
return (
  <QueryClientProvider client={queryClient}>
    <ThemeContext.Provider value={{ isDarkMode, toggleTheme }}>
      <FluentProvider theme={isDarkMode ? webDarkTheme : webLightTheme}>
        <ActivityProvider>
          <BrowserRouter>
            {/* Main app with sidebar, routes, etc. */}
          </BrowserRouter>
        </ActivityProvider>
      </FluentProvider>
    </ThemeContext.Provider>
  </QueryClientProvider>
);
```

### FirstRunWizard.tsx Changes

**Key differences from old implementation**:

1. **Removed optional steps**: Tier selection, templates, hardware detection, validation
2. **Simplified to 5 mandatory steps**: Welcome, FFmpeg, Providers, Workspace, Complete
3. **Added validation logic**: Each step validates before allowing "Next"
4. **Removed exit options**: No "Save and Exit", no dismiss button
5. **Full-screen styling**: `position: fixed` with high z-index
6. **Provider offline mode**: Clear "Skip All" option that enables offline mode
7. **Help text**: Each step shows "Why is this required?" explanation

### State Management

The wizard uses the existing `onboarding` state from Zustand, but simplified:

```typescript
// Core state used
- step: number (0-4)
- apiKeys: Record<string, string>
- apiKeyValidationStatus: Record<string, 'valid' | 'invalid' | 'pending'>
- workspacePreferences: { defaultSaveLocation, cacheLocation }
- selectedTier: 'free' | 'pro' (for offline mode detection)
- mode: 'free' | 'pro' (for offline mode)

// State NOT used anymore
- hardware detection
- template selection
- dependency scanning (except FFmpeg)
- validation stages
```

### Persistence

Setup completion is persisted in two places:

1. **LocalStorage** (fast, immediate):
   ```typescript
   localStorage.setItem('hasCompletedFirstRun', 'true');
   ```

2. **Backend Database** (persistent, cross-device):
   ```typescript
   POST /api/setup/wizard/complete
   {
     "version": "1.0.0",
     "selectedTier": "free",
     "lastStep": 10
   }
   ```

## User Experience Flow

### First-Time User

1. **Opens application**
   - Sees loading spinner: "Loading..."
   - Backend checks first-run status

2. **Wizard appears** (full-screen, cannot dismiss)
   - "Welcome to Aura Video Studio - Let's get you set up!"
   - "Step 1 of 5 - Required Setup"
   - Progress bar shows 0%

3. **Step 1: Welcome**
   - Click "Get Started"

4. **Step 2: FFmpeg Installation**
   - Sees FFmpeg status check
   - If not installed: Click "Install Managed FFmpeg"
   - Progress bar shows download/extraction progress
   - Success: FFmpeg version displayed, "Next" button enabled

5. **Step 3: Provider Configuration**
   - Sees list of providers (OpenAI, Anthropic, etc.)
   - Options:
     - **Enter API key** → Validate → Success ✓
     - **Click "Skip All"** → Offline mode enabled → Success ✓
   - "Next" button enabled when at least one provider configured

6. **Step 4: Workspace Setup**
   - Sees pre-filled default paths
   - Can accept defaults or click "Browse" to customize
   - "Next" button always enabled (defaults are valid)

7. **Step 5: Setup Complete**
   - Success screen with checkmarks
   - Click "Start Creating Videos"
   - Setup marked complete in database
   - Wizard closes, main app loads

### Returning User

1. **Opens application**
   - Backend checks: first-run already complete
   - Main app loads directly
   - No wizard shown

### Re-running Setup

If user wants to reconfigure:
1. Go to **Settings** → **Re-run Setup Wizard**
2. Wizard appears again
3. Current configuration pre-filled
4. Can update any settings
5. Completion updates configuration

## Error Handling

### FFmpeg Installation Fails

```
❌ Installation failed
Error: Failed to download FFmpeg from GitHub

[Retry] [Troubleshoot]
```

**Troubleshoot** opens help page with:
- Manual installation instructions
- System requirements
- Firewall configuration tips
- Alternative download links

### Provider Validation Fails

```
❌ Invalid API Key
The provided OpenAI API key is invalid or expired.

[Re-enter Key] [Skip for now - Use offline mode]
```

### Workspace Path Invalid

```
❌ Invalid Path
The specified path is not writable or does not exist.

[Browse] [Use Default]
```

## Testing Checklist

- [ ] Fresh installation flow
  - [ ] Wizard appears on first launch
  - [ ] Cannot dismiss wizard
  - [ ] Cannot access sidebar/navigation
  - [ ] Progress indicator updates correctly

- [ ] FFmpeg installation
  - [ ] Windows: Downloads and installs correctly
  - [ ] macOS: Downloads and installs correctly
  - [ ] Linux: Downloads and installs correctly
  - [ ] PATH is set automatically
  - [ ] Version is verified and displayed

- [ ] Provider configuration
  - [ ] Can configure OpenAI
  - [ ] Can configure Anthropic
  - [ ] Can validate API keys
  - [ ] Can enable offline mode
  - [ ] Cannot proceed without at least one

- [ ] Workspace setup
  - [ ] Default paths are OS-appropriate
  - [ ] Browse button works
  - [ ] Custom paths are validated
  - [ ] Directories are created if needed

- [ ] Completion
  - [ ] Configuration persists to database
  - [ ] Wizard closes on completion
  - [ ] Main app loads successfully
  - [ ] Can create videos immediately

- [ ] Re-entry
  - [ ] Returning users don't see wizard
  - [ ] Can re-run from Settings
  - [ ] Previous configuration pre-filled

- [ ] Error handling
  - [ ] Network errors handled gracefully
  - [ ] Invalid API keys detected
  - [ ] Invalid paths rejected
  - [ ] Retry mechanisms work

## Limitations and Future Enhancements

### Current Limitations

1. **No sample generation**: Removed from completion step
   - Could be added as optional "Quick Demo" button
   - Would require job orchestration ready

2. **No hardware detection**: Removed as optional
   - Could be added as background task
   - Not blocking for basic usage

3. **No templates**: Removed as optional
   - Could be added as post-setup onboarding
   - Not essential for first video

### Potential Enhancements

1. **Auto-detect providers**: Check for existing API keys in environment
2. **Quick test**: Validate complete setup with micro-generation
3. **Setup profiles**: Save multiple configurations (work, personal)
4. **Import settings**: Load configuration from file
5. **Skip for developers**: Environment variable to bypass for dev/test

## Conclusion

The mandatory first-run setup wizard ensures every user has a working configuration before creating their first video. By simplifying to 5 essential steps and adding clear validation, the setup experience is both thorough and user-friendly.

Key achievements:
- ✅ Truly mandatory (cannot skip or dismiss)
- ✅ Clear progress indicators
- ✅ Helpful explanations for each step
- ✅ FFmpeg auto-installation for all OS
- ✅ Flexible provider configuration (premium or offline)
- ✅ Smart defaults for workspace
- ✅ Validation at every step
- ✅ Persistence to prevent re-runs

This implementation resolves the critical usability issue where new users could bypass setup and encounter errors when trying to generate videos.
