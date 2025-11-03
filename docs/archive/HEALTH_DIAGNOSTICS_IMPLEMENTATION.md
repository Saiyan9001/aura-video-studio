> **⚠️ ARCHIVED DOCUMENT**
>
> This document is archived for historical reference only.
> It may contain outdated information. See the [Documentation Index](../DocsIndex.md) for current documentation.

# Health Diagnostics & Preflight Checks Implementation

This document describes the implementation of the comprehensive health diagnostics and preflight checks system.

## Overview

The health diagnostics system provides accurate, actionable system status reporting to prevent false positives (e.g., "All systems ready" when no configuration exists). It includes:

1. **Backend Health Service**: Comprehensive checks for all system components
2. **Frontend Health UI**: User-friendly display with remediation actions
3. **Unit Tests**: Complete test coverage for all check scenarios

## Backend Implementation

### Health Endpoints

Two new REST endpoints provide health information:

#### `/api/health/summary` - High-Level Status
Returns a concise overview of system health:
```json
{
  "overallStatus": "degraded",
  "isReady": true,
  "totalChecks": 15,
  "passedChecks": 12,
  "warningChecks": 2,
  "failedChecks": 1,
  "timestamp": "2025-11-01T23:26:00Z"
}
```

#### `/api/health/details` - Per-Check Details
Returns detailed information for each check with remediation:
```json
{
  "overallStatus": "degraded",
  "isReady": true,
  "checks": [
    {
      "id": "ffmpeg_present",
      "name": "FFmpeg",
      "category": "Video",
      "status": "fail",
      "isRequired": true,
      "message": "FFmpeg not found. Video rendering disabled.",
      "data": {
        "attemptedPaths": ["/usr/bin/ffmpeg", "/usr/local/bin/ffmpeg"]
      },
      "remediationHint": "Download and install FFmpeg to enable video rendering.",
      "remediationActions": [
        {
          "type": "install",
          "label": "Install FFmpeg",
          "description": "Download FFmpeg from the Downloads page",
          "navigateTo": "/downloads",
          "externalUrl": null,
          "parameters": {"component": "ffmpeg"}
        },
        {
          "type": "configure",
          "label": "Configure Path",
          "description": "Set FFmpeg path manually in Settings",
          "navigateTo": "/settings?tab=video",
          "externalUrl": null,
          "parameters": null
        }
      ]
    }
  ],
  "timestamp": "2025-11-01T23:26:00Z"
}
```

### Health Checks

The `HealthDiagnosticsService` performs the following checks:

#### System Checks
- **Configuration**: Validates that configuration directory exists and is accessible
- **Disk Space**: Checks free disk space (fail <1GB, warning <5GB, pass ≥5GB)

#### Video Pipeline Checks
- **FFmpeg**: Validates FFmpeg presence, path, and version
- **GPU Encoders**: Detects hardware encoders (NVENC, AMF, QuickSync) with CPU fallback

#### LLM Provider Checks
- **RuleBased**: Always available (offline template-based generation)
- **OpenAI**: API key configuration + connectivity check (2s timeout)
- **Anthropic**: API key configuration
- **Google Gemini**: API key configuration
- **Ollama**: Local service connectivity check (2s timeout)

#### TTS Provider Checks
- **Windows SAPI**: Platform detection (Windows only)
- **ElevenLabs**: API key + connectivity check (2s timeout)
- **PlayHT**: API key configuration
- **Piper**: Executable presence and path validation
- **Mimic3**: Local service connectivity check (2s timeout)

#### Image Provider Checks
- **Stock Images**: Always available (built-in)
- **Stable Diffusion WebUI**: API endpoint check (2s timeout)
- **Replicate**: API key configuration

### Check Status Values
- **pass**: Check passed successfully
- **warning**: Check failed but not required (or optional capability missing)
- **fail**: Check failed and is required for operation

### Required vs Optional Checks
- **Required**: Must pass for system to be considered "ready"
  - Configuration present
  - FFmpeg available
  - Disk space ≥1GB
- **Optional**: System can function without these
  - GPU encoders (CPU fallback available)
  - Premium LLM providers (RuleBased fallback)
  - Premium TTS providers (Windows SAPI fallback)
  - Image generation providers (Stock images fallback)

### Remediation Actions

Each failed or warning check includes actionable remediation steps:

#### Action Types
- `open_settings`: Navigate to settings page (with optional tab parameter)
- `install`: Navigate to downloads page to install component
- `configure`: Navigate to configuration page
- `start`: Instructions to start a service manually
- `open_help`: Open external documentation URL
- `switch_provider`: Switch to alternative provider

#### Action Structure
```typescript
{
  type: "open_settings",
  label: "Add API Key",
  description: "Configure OpenAI API key in Settings",
  navigateTo: "/settings?tab=api-keys",
  externalUrl: null,
  parameters: { "provider": "OpenAI" }
}
```

## Frontend Implementation

### Types

Added to `src/types/api-v1.ts`:
- `HealthSummaryResponse`: High-level status summary
- `HealthDetailsResponse`: Detailed check results
- `HealthCheckDetail`: Individual check with remediation
- `RemediationAction`: Actionable fix step

### API Client

`src/services/api/healthApi.ts` provides typed functions:
```typescript
getHealthSummary(): Promise<HealthSummaryResponse>
getHealthDetails(): Promise<HealthDetailsResponse>
```

### State Management

`src/state/healthDiagnostics.ts` - Zustand store with persistence:
```typescript
interface HealthDiagnosticsState {
  summary: HealthSummaryResponse | null;
  details: HealthDetailsResponse | null;
  isLoading: boolean;
  error: string | null;
  lastCheckTime: Date | null;
  
  // Actions
  fetchHealthSummary(): Promise<void>;
  fetchHealthDetails(): Promise<void>;
  refreshHealth(): Promise<void>;
  clearError(): void;
  
  // Computed helpers
  isSystemReady(): boolean;
  getRequiredFailedChecks(): HealthCheckDetail[];
  hasMinimalSetup(): boolean;
}
```

Persisted fields:
- `summary`, `details`, `lastCheckTime` (for support diagnostics)

### UI Component

`src/components/HealthDiagnosticsPanel.tsx` - Full-featured diagnostics UI:

Features:
- Grouped checks by category (System, Configuration, LLM, TTS, Image, Video)
- Color-coded status indicators (green=pass, yellow=warning, red=fail)
- Remediation action buttons that navigate to fixes
- Optional/required badge display
- Detailed messages and hints
- "Run Diagnostics" button to refresh
- Overall system status summary

Props:
```typescript
interface HealthDiagnosticsPanelProps {
  showOptional?: boolean;  // Show optional checks (default: true)
  onReady?: (isReady: boolean) => void;  // Callback when readiness changes
}
```

## Testing

### Unit Tests

`Aura.Tests/HealthDiagnosticsServiceTests.cs` - 25 comprehensive tests:

#### Configuration Tests
- ✓ Pass when directory exists
- ✓ Fail when directory missing
- ✓ Remediation actions present on failure

#### FFmpeg Tests
- ✓ Pass when FFmpeg found
- ✓ Fail when FFmpeg not found
- ✓ Remediation actions include install and configure options

#### GPU Tests
- ✓ Pass when GPU detected with hardware encoders
- ✓ Warning when no GPU (CPU fallback available)

#### LLM Provider Tests
- ✓ RuleBased always passes (offline)
- ✓ OpenAI warning when no API key
- ✓ OpenAI pass when API key configured and reachable
- ✓ Anthropic, Gemini, Ollama checks

#### TTS Provider Tests
- ✓ Windows SAPI pass on Windows platform
- ✓ ElevenLabs warning when no API key
- ✓ PlayHT checks
- ✓ Piper, Mimic3 checks

#### Image Provider Tests
- ✓ Stock always passes (built-in)
- ✓ Stable Diffusion, Replicate checks

#### Integration Tests
- ✓ System ready when all required checks pass
- ✓ System unhealthy when required checks fail
- ✓ Remediation actions include navigation URLs
- ✓ Remediation actions include external help URLs

## Usage

### Backend

Health endpoints are automatically registered in `Program.cs` and available at:
- `GET /api/health/summary`
- `GET /api/health/details`

Both endpoints return 200 OK for healthy/degraded, 503 Service Unavailable for unhealthy.

### Frontend

```typescript
import { useHealthDiagnostics } from '../state/healthDiagnostics';
import { HealthDiagnosticsPanel } from '../components/HealthDiagnosticsPanel';

function MyComponent() {
  const { details, isSystemReady, fetchHealthDetails } = useHealthDiagnostics();
  
  return (
    <div>
      <HealthDiagnosticsPanel 
        showOptional={true}
        onReady={(ready) => console.log('System ready:', ready)}
      />
      
      {isSystemReady() && (
        <Button>Generate Video</Button>
      )}
    </div>
  );
}
```

## Integration Points

### Preflight Checks

The existing `PreflightController` and `PreflightService` can be updated to use the health diagnostics system:

```csharp
// In PreflightService
private readonly HealthDiagnosticsService _healthDiagnostics;

public async Task<PreflightReport> RunPreflightAsync(string profileName, CancellationToken ct)
{
    var healthDetails = await _healthDiagnostics.GetHealthDetailsAsync(ct);
    
    // Convert health checks to preflight report format
    // Check profile-specific providers
    // Return consolidated report
}
```

### Generate Button Gating

The "Generate" button should be disabled unless:
1. System is ready (`isSystemReady()` returns true)
2. Profile-specific requirements met:
   - **Demo Mode**: RuleBased + Windows SAPI + FFmpeg
   - **Free-Only**: At least one free provider per stage + FFmpeg
   - **Balanced/Pro**: Required premium providers configured

### First Run Experience

On first run, automatically call `fetchHealthDetails()` and guide users through fixes:
1. Show HealthDiagnosticsPanel
2. For each failed required check, highlight remediation actions
3. After fixes applied, recheck health
4. Once ready, proceed to onboarding

## Configuration

### Backend Configuration

Add to `appsettings.json` (optional overrides):
```json
{
  "HealthChecks": {
    "DiskSpaceThresholdGB": 5.0,
    "DiskSpaceCriticalGB": 1.0,
    "ExternalCheckTimeoutMs": 2000
  }
}
```

### Provider Settings

The following `ProviderSettings` methods are used:
- `GetAuraDataDirectory()`: Configuration directory
- `GetOutputDirectory()`: Output directory for disk space check
- `GetOllamaUrl()`: Ollama service URL (default: http://127.0.0.1:11434)
- `GetStableDiffusionUrl()`: SD WebUI URL (default: http://127.0.0.1:7860)
- `GetMimic3Url()`: Mimic3 service URL (default: http://127.0.0.1:59125)
- `GetPiperPath()`: Piper executable path

## Security Considerations

1. **API Key Exposure**: Keys are never returned in health check responses
2. **Path Disclosure**: Only validated paths are returned (no user file system traversal)
3. **External Checks**: 2-second timeout prevents hanging on unreachable services
4. **Error Messages**: Technical details logged server-side, user-friendly messages returned

## Performance

- **Check Duration**: Most checks complete in <100ms
- **External Checks**: Limited to 2s timeout each (OpenAI, ElevenLabs, Ollama, SD WebUI, Mimic3)
- **Caching**: Frontend persists results to localStorage for quick re-display
- **Parallel Execution**: All checks run concurrently via async/await

## Future Enhancements

1. **Auto-Fix**: Implement one-click fixes for common issues (e.g., download FFmpeg)
2. **Monitoring**: Track health check history over time
3. **Notifications**: Alert users when health status changes
4. **Scheduled Checks**: Periodic background checks with status updates
5. **Provider Scoring**: Rank providers by reliability and performance
6. **Health Dashboard**: Dedicated page for system health visualization

## References

- Backend Service: `Aura.Api/Services/HealthDiagnosticsService.cs`
- Health DTOs: `Aura.Api/Models/ApiModels.V1/HealthDtos.cs`
- API Endpoints: `Aura.Api/Program.cs` (lines 1254-1288)
- Frontend State: `Aura.Web/src/state/healthDiagnostics.ts`
- Frontend Component: `Aura.Web/src/components/HealthDiagnosticsPanel.tsx`
- Unit Tests: `Aura.Tests/HealthDiagnosticsServiceTests.cs`
