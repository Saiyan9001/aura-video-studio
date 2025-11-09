# System Setup and Dependency Checking Implementation

## Overview

This implementation adds comprehensive system setup and dependency checking capabilities to Aura Video Studio, fulfilling the requirements of PR #27.

## Features Implemented

### 1. Dependency Checker

The `DependencyDetector` service now checks for:

- ✅ **FFmpeg** - Installation and version
- ✅ **Node.js** - Installation and version
- ✅ **npm** - Included with Node.js check
- ✅ **.NET Runtime** - Installation and version
- ✅ **Python** - Installation and version for local AI models
- ✅ **Ollama** - Installation status and service availability
- ✅ **Piper TTS** - Installation path
- ✅ **NVIDIA Drivers** - For hardware acceleration
- ✅ **Disk Space** - Available GB
- ✅ **Internet Connectivity** - Network status

**API Endpoint:** `GET /api/dependencies/check`

**Response Example:**
```json
{
  "success": true,
  "ffmpeg": {
    "installed": true,
    "version": "6.0",
    "installationRequired": false
  },
  "nodejs": {
    "installed": true,
    "version": "20.0.0"
  },
  "dotnet": {
    "installed": true,
    "version": "8.0.0"
  },
  "python": {
    "installed": true,
    "version": "3.11.0"
  },
  "diskSpaceGB": 50.5,
  "internetConnected": true
}
```

### 2. Provider Availability Checking

The `ProviderAvailabilityService` checks:

- ✅ **Ollama Service** - HTTP connectivity test on port 11434
- ✅ **Stable Diffusion** - Checks common endpoints (7860, 7861)
- ✅ **Database** - Connection status (always available for file-based storage)
- ✅ **Network Connectivity** - Internet access test

**API Endpoint:** `GET /api/diagnostics/providers/availability`

**Response Example:**
```json
{
  "success": true,
  "timestamp": "2024-01-15T10:30:00Z",
  "providers": [
    {
      "providerName": "Ollama",
      "providerType": "LLM",
      "isAvailable": true,
      "isReachable": true,
      "status": "Available",
      "latency": null,
      "errorMessage": null
    }
  ],
  "ollamaAvailable": true,
  "stableDiffusionAvailable": false,
  "databaseAvailable": true,
  "networkConnected": true
}
```

### 3. Auto-Configuration

The `AutoConfigurationService` analyzes system capabilities and recommends:

- ✅ **Thread Count** - Based on CPU cores and available memory
- ✅ **Memory Limits** - Percentage of system RAM to allocate
- ✅ **Quality Presets** - Low/Medium/High/Ultra based on GPU and RAM
- ✅ **Hardware Acceleration** - NVENC/AMF/QuickSync detection
- ✅ **Local Provider Usage** - Enable if Ollama/Piper TTS available
- ✅ **Recommended Tier** - Free/Local/Pro based on system capabilities

**API Endpoint:** `GET /api/diagnostics/auto-config`

**Response Example:**
```json
{
  "success": true,
  "recommendedThreadCount": 8,
  "recommendedMemoryLimitMB": 8192,
  "recommendedQualityPreset": "High",
  "useHardwareAcceleration": true,
  "hardwareAccelerationMethod": "nvenc",
  "enableLocalProviders": true,
  "recommendedTier": "Pro",
  "configuredProviders": [
    "FFmpeg",
    "Ollama (Local LLM)",
    "Piper TTS (Local)",
    "RuleBased LLM (Fallback)"
  ]
}
```

### 4. Diagnostic Dashboard

A new `/diagnostics` page provides:

- ✅ **System Dependencies Panel** - Visual status for all dependencies
- ✅ **Provider Availability Panel** - Real-time provider status
- ✅ **Auto-Configuration Panel** - Recommended settings display
- ✅ **Refresh Button** - Reload all diagnostic information
- ✅ **Status Indicators** - Color-coded badges (success/warning/danger)
- ✅ **Metric Cards** - Grid layout for key metrics

**Navigation:** Main menu → "Diagnostics" (with stethoscope icon)

### 5. Testing

#### Backend Tests
- ✅ `DependencyDetectorTests.cs` - 3 tests, all passing
  - Tests dependency detection
  - Tests Node.js detection
  - Tests .NET detection

#### Frontend Tests
- ✅ Updated `dependency-detection.test.ts` - 27 tests, all passing
  - 1.6: Comprehensive Dependency Check (4 tests)
  - 1.7: Provider Availability Check (3 tests)
  - 1.8: Auto-Configuration Detection (3 tests)

## Usage

### For Users

1. **View System Status:**
   - Navigate to **Diagnostics** in the main menu
   - View all system dependencies and their status
   - Check provider availability in real-time
   - See recommended system configuration

2. **Initial Setup:**
   - The diagnostic page helps identify missing dependencies
   - Shows clear status indicators (✓ Installed / ✗ Not Installed)
   - Provides recommended configuration based on your hardware
   - Indicates which tier (Free/Local/Pro) is best for your system

3. **Troubleshooting:**
   - Use the Refresh button to rescan dependencies
   - Check provider status to diagnose connection issues
   - Verify hardware acceleration is detected
   - Confirm network connectivity

### For Developers

#### Using the SetupService

```typescript
import { setupService } from '../services/setupService';

// Check all dependencies
const deps = await setupService.checkDependencies();
console.log('FFmpeg installed:', deps.ffmpeg.installed);

// Check provider availability
const providers = await setupService.checkProviderAvailability();
console.log('Ollama available:', providers.ollamaAvailable);

// Get auto-configuration
const config = await setupService.getAutoConfiguration();
console.log('Recommended threads:', config.recommendedThreadCount);
```

#### Backend Services

```csharp
// Dependency Detection
var detector = new DependencyDetector(logger, ffmpegLocator, httpClient);
var status = await detector.DetectAllDependenciesAsync(cancellationToken);

// Provider Availability
var providerService = new ProviderAvailabilityService(logger, httpClient);
var report = await providerService.CheckAllProvidersAsync(cancellationToken);

// Auto-Configuration
var autoConfig = new AutoConfigurationService(logger, hardwareDetector, dependencyDetector);
var config = await autoConfig.DetectOptimalSettingsAsync(cancellationToken);
```

## Architecture

### Backend (C#)

```
Aura.Core/Services/Setup/
├── DependencyDetector.cs         - Detects all system dependencies
├── ProviderAvailabilityService.cs - Checks provider status
└── AutoConfigurationService.cs    - Recommends optimal settings

Aura.Api/Controllers/
├── DependenciesController.cs     - /api/dependencies/* endpoints
└── DiagnosticsController.cs      - /api/diagnostics/* endpoints
```

### Frontend (TypeScript/React)

```
Aura.Web/src/
├── services/
│   └── setupService.ts           - API client for setup endpoints
├── pages/
│   └── DiagnosticDashboardPage.tsx - Diagnostic UI
└── App.tsx                       - Route configuration
```

## API Reference

### GET /api/dependencies/check

Returns comprehensive dependency status.

**Response:** `DependencyCheckResult`

### GET /api/diagnostics/providers/availability

Returns provider availability report.

**Response:** `ProviderAvailabilityReport`

### GET /api/diagnostics/auto-config

Returns auto-configuration recommendations.

**Response:** `AutoConfigurationResult`

## Next Steps

The following enhancements could be added in future PRs:

1. **Setup Wizard Enhancement** - Integrate dependency checking into FirstRunWizard
2. **Error Log Viewer** - Add log viewer to diagnostic dashboard
3. **Performance Metrics** - Track metrics over time
4. **Diagnostic Report Export** - Export system info as JSON/PDF
5. **Guided Fixes** - Provide step-by-step fixes for missing dependencies

## Testing

Run all tests:
```bash
# Backend tests
dotnet test --filter "FullyQualifiedName~DependencyDetectorTests"

# Frontend tests
npm test -- dependency-detection.test.ts
```

All tests pass successfully:
- Backend: 3/3 tests passing
- Frontend: 27/27 tests passing

## Code Quality

- ✅ Zero placeholders (enforced by pre-commit hooks)
- ✅ TypeScript strict mode enabled
- ✅ All linting checks pass
- ✅ Build verification successful
- ✅ Follows repository conventions
