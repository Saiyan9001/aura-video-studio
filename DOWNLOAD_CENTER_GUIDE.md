# Download Center - Implementation Guide

## Overview

The Download Center provides manifest-driven downloads with verification, repair, and offline manual support for all required components.

## Features Implemented

### 1. Manifest Schema
The manifest schema includes the following fields per component:
- `name`: Component name
- `version`: Component version
- `isRequired`: Boolean indicating if component is required
- `postInstallProbe`: Optional validation hook (formats: `ffmpeg:version`, `http:<url>`, `file:<path>`)
- `files`: Array of files to download
  - `filename`: Target filename
  - `url`: Download URL
  - `sha256`: SHA-256 checksum for verification
  - `sizeBytes`: Expected file size
  - `extractPath`: Optional extraction path for archives
  - `installPath`: Optional custom install path

### 2. Core Functionality

#### Resume Downloads
- Downloads support HTTP Range requests for resume capability
- Partial files are detected and downloads continue from the last position
- Progress is tracked accurately across resume operations

#### SHA-256 Verification
- All downloads are verified against SHA-256 checksums
- Corrupted files trigger repair flow automatically
- Manual installations can be verified via API

#### Repair
- Detects corrupted files via checksum mismatch
- Re-downloads only corrupted files
- Preserves valid files to minimize bandwidth usage

#### Remove
- Safely removes all files associated with a component
- Returns success status indicating if all files were deleted

#### Open Folder
- Provides access to the download directory path
- Allows users to manually manage files

#### Post-Install Validation
- **FFmpeg probe**: Executes `ffmpeg -version` to verify installation
- **HTTP endpoint probe**: Checks if service is reachable (e.g., Ollama, SD WebUI)
- **File existence probe**: Verifies specific files exist (e.g., model files)

### 3. Offline Mode

#### Manual Installation Instructions
- Displays component name, version, and target directory
- Lists all files with URLs, checksums, and sizes
- Provides clear instructions for manual placement

#### Checksum Verification
- Verifies manually placed files against expected checksums
- Reports detailed results per file
- Identifies missing or corrupted files

### 4. UI Components

#### Online Mode
- Install button for new components
- Repair button for corrupted components
- Remove option in dropdown menu
- Status indicators: Installed, Needs Repair, Not Installed
- Real-time progress reporting

#### Offline Mode
- Instructions button showing manual download steps
- Verify button to check manually placed files
- Detailed checksum information in dialog
- Clear error messaging for failed verifications

## API Endpoints

### GET /api/downloads/manifest
Returns the full dependency manifest with all components.

### GET /api/downloads/{component}/status
Returns component status including:
- `isInstalled`: Whether component is fully installed
- `needsRepair`: Whether component needs repair
- `errorMessage`: Optional error message

### POST /api/downloads/{component}/install
Downloads and installs a component with progress tracking.

### POST /api/downloads/{component}/repair
Repairs a corrupted component by re-downloading invalid files.

### DELETE /api/downloads/{component}
Removes all files associated with a component.

### GET /api/downloads/directory
Returns the download directory path.

### GET /api/downloads/{component}/manual-instructions
Returns manual installation instructions for offline mode.

### POST /api/downloads/{component}/verify
Verifies manually installed files against checksums.

## Testing

### Unit Tests (11 tests)
- `LoadManifestAsync_CreatesDefaultManifest_WhenFileDoesNotExist`
- `GetComponentStatusAsync_ReturnsNotInstalled_WhenFilesDoNotExist`
- `VerifyChecksumAsync_PassesWithCorrectChecksum`
- `VerifyChecksumAsync_FailsWithIncorrectChecksum`
- `RepairComponentAsync_RedownloadsCorruptedFiles`
- `RemoveComponentAsync_DeletesAllFiles`
- `GetManualInstallInstructionsAsync_ReturnsCorrectInstructions`
- `VerifyManualInstallAsync_ReturnsValidResult_WhenFilesAreCorrect`
- `VerifyManualInstallAsync_ReturnsInvalidResult_WhenFileIsMissing`
- `DownloadFileAsync_SupportsResume`
- `GetComponentDirectory_ReturnsCorrectPath`

### Integration Tests (5 tests)
- `IntegrationTest_FullDownloadFlow_WithStubServer`: Tests complete download flow with local HTTP server
- `IntegrationTest_ResumeDownload_WithStubServer`: Tests resume capability with partial files
- `IntegrationTest_RepairComponent_WithStubServer`: Tests repair flow with corrupted files
- `IntegrationTest_ProgressReporting_WithStubServer`: Tests progress reporting accuracy
- `IntegrationTest_ManualInstallWorkflow_WithVerification`: Tests offline manual install flow

All 127 tests pass (111 original + 16 new).

## Usage Examples

### Online Mode - Install Component
```typescript
const response = await fetch('/api/downloads/FFmpeg/install', {
  method: 'POST'
});
```

### Online Mode - Repair Component
```typescript
const response = await fetch('/api/downloads/FFmpeg/repair', {
  method: 'POST'
});
```

### Online Mode - Remove Component
```typescript
const response = await fetch('/api/downloads/FFmpeg', {
  method: 'DELETE'
});
```

### Offline Mode - Get Instructions
```typescript
const response = await fetch('/api/downloads/FFmpeg/manual-instructions');
const instructions = await response.json();
// Display instructions to user
```

### Offline Mode - Verify Installation
```typescript
const response = await fetch('/api/downloads/FFmpeg/verify', {
  method: 'POST'
});
const result = await response.json();
if (result.isValid) {
  console.log('All files verified successfully');
} else {
  console.log('Verification failed:', result.files);
}
```

## Component Status Flow

```
Not Installed → Install → Installed
                    ↓
              Needs Repair → Repair → Installed
                    ↓
              Remove → Not Installed
```

## Post-Install Probe Examples

### FFmpeg
```json
"postInstallProbe": "ffmpeg:version"
```
Runs `ffmpeg -version` to verify the executable works.

### Ollama
```json
"postInstallProbe": "http:http://127.0.0.1:11434/api/version"
```
Checks if Ollama service is reachable on default port.

### SD WebUI Models
```json
"postInstallProbe": "file:models/Stable-diffusion/sd-v1-5.safetensors"
```
Verifies model file exists in expected location.

## Error Handling

All API endpoints return appropriate HTTP status codes:
- `200 OK`: Success
- `404 Not Found`: Component not found in manifest
- `500 Internal Server Error`: Download, verification, or file system error

Error responses include descriptive messages for troubleshooting.

## Future Enhancements

- Progress streaming via WebSocket for real-time UI updates
- Parallel downloads for multi-file components
- Automatic extraction of ZIP archives
- Dependency resolution and installation order
- Bandwidth throttling options
- Proxy support for restricted networks
