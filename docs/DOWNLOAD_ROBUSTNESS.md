# Download Robustness and Visibility Features

This document describes the download robustness features implemented for engine installation in Aura Video Studio.

## Overview

The download system now supports multiple installation methods and provides better visibility into where engines are installed and how they were obtained.

## Features

### 1. Multi-Mirror Fallback

When downloading engines from official sources, the system automatically tries multiple mirror servers if the primary download fails.

**How it works:**
- Primary URL is tried first with automatic retries (exponential backoff)
- If primary fails with 404 or timeout, next mirror is tried automatically
- Process continues through all available mirrors
- User sees which mirror is being used in progress messages

**Error Codes:**
- `E-DL-404`: Mirror returned 404 Not Found
- `E-DL-TIMEOUT`: Download timed out
- `E-DL-CHECKSUM`: Checksum verification failed

### 2. Custom URL Installation

Users can install engines from custom download URLs when official mirrors are unavailable.

**Use Cases:**
- Official mirrors are down or blocked
- Company-hosted mirrors for faster downloads
- Custom builds or versions
- Offline installations from local web servers

**How to Use:**
1. Click the dropdown arrow on the "Install" button
2. Select "Install from Custom URL..."
3. Enter the full URL to the engine archive
4. The system will download and verify the checksum

**Notes:**
- The file must match the expected format (ZIP or TAR.GZ)
- SHA-256 checksum is verified if available in manifest
- If checksum doesn't match, installation is aborted

### 3. Local File Import

Users can import engines from locally downloaded archive files.

**Use Cases:**
- Already downloaded the engine manually
- Offline installation scenarios
- Slow or unreliable internet connection
- Installing from network shares or USB drives

**How to Use:**
1. Click the dropdown arrow on the "Install" button
2. Select "Install from Local File..."
3. Enter the full path to the local archive file
4. The system will copy and verify the file

**Notes:**
- Enter absolute path (e.g., `C:\Downloads\engine-1.0.0.zip`)
- Checksum is verified if available in manifest
- If checksum doesn't match, user is warned but installation continues
- Original file is not modified or deleted

### 4. Installation Provenance Tracking

Every engine installation now records detailed provenance information.

**Tracked Information:**
- Engine ID and version
- Installation timestamp
- Installation path
- Source type (Mirror, CustomUrl, or LocalFile)
- Download URL or file path
- SHA-256 checksum

**Storage Location:**
The provenance is stored in `install.json` in the engine's installation directory.

**Example:**
```json
{
  "engineId": "ollama",
  "version": "0.1.19",
  "installedAt": "2025-10-12T01:23:45.123Z",
  "installPath": "C:\\Users\\user\\AppData\\Local\\Aura\\Engines\\ollama",
  "source": "Mirror",
  "url": "https://github.com/ollama/ollama/releases/download/v0.1.19/ollama-windows-amd64.zip",
  "sha256": "abc123..."
}
```

### 5. Installation Path Visibility

The UI now prominently displays where engines are installed.

**Features:**
- Full install path shown under each installed engine
- Copy button to copy path to clipboard
- "Open Folder" button to open installation directory in file explorer
- Cross-platform support (Windows Explorer, Linux xdg-open, macOS Finder)

**API Endpoints:**
- `GET /api/engines/provenance?engineId={id}` - Get provenance info
- `POST /api/engines/open-folder` - Open installation folder

## API Changes

### Install Request

The install endpoint now accepts additional parameters:

```json
{
  "engineId": "ollama",
  "version": "0.1.19",
  "port": 11434,
  "customUrl": "https://custom-mirror.com/ollama.zip",  // Optional
  "localFilePath": "C:\\Downloads\\ollama.zip"          // Optional
}
```

**Notes:**
- `customUrl` and `localFilePath` are mutually exclusive
- If `customUrl` is provided, it's used instead of manifest URLs
- If `localFilePath` is provided, file is imported instead of downloaded

### Install Response

The install endpoint now returns the installation path:

```json
{
  "success": true,
  "engineId": "ollama",
  "installPath": "C:\\Users\\user\\AppData\\Local\\Aura\\Engines\\ollama",
  "message": "Engine Ollama installed successfully"
}
```

## Error Handling

### Download Failures

When downloads fail, the system:
1. Tries all available mirrors with exponential backoff
2. Shows which mirror failed and why
3. Suggests alternative installation methods in diagnostics dialog

### Checksum Mismatches

When checksums don't match:
- For network downloads: Tries next mirror automatically
- For custom URLs: Aborts installation and shows error
- For local files: Warns user but allows installation

### User Guidance

When errors occur, the diagnostics dialog shows:
- Specific error codes and messages
- List of mirrors that were tried
- Suggestions to use Custom URL or Local File methods
- Expected vs actual checksums (when applicable)

## Testing

The implementation includes comprehensive unit tests:

### HttpDownloaderMirrorTests (8 tests)
- Mirror fallback on 404 errors
- Trying all mirrors when all fail
- Checksum verification with mirror fallback
- Local file import with and without checksums
- Progress reporting with active mirror

### EngineInstallerTests (2 new tests)
- Reading provenance files
- Handling missing provenance files

**Test Results:** All 18 new tests passing ✅

## Security Considerations

1. **Checksum Verification**: All downloads are verified against expected SHA-256 hashes
2. **HTTPS Only**: Custom URLs should use HTTPS for secure downloads
3. **Path Validation**: Local file paths are validated before import
4. **No Arbitrary Execution**: Downloaded files are only extracted, not executed directly

## Configuration

### Adding Mirrors to Manifest

Mirrors can be added to the engine manifest:

```json
{
  "id": "ollama",
  "urls": {
    "windows": "https://github.com/ollama/ollama/releases/download/v0.1.19/ollama-windows-amd64.zip"
  },
  "mirrors": {
    "windows": [
      "https://mirror1.com/ollama/v0.1.19/ollama-windows-amd64.zip",
      "https://mirror2.com/ollama/v0.1.19/ollama-windows-amd64.zip"
    ]
  }
}
```

## Troubleshooting

### Issue: All mirrors return 404

**Solution:**
1. Check if the engine version is still available
2. Use "Install from Custom URL" with an alternative source
3. Download manually and use "Install from Local File"

### Issue: Checksum verification fails

**Solution:**
1. Try a different mirror
2. Re-download the file (may have been corrupted)
3. Verify you're using the correct version
4. Check the manifest for correct checksum

### Issue: Local file import fails

**Solution:**
1. Verify the file path is absolute and correct
2. Ensure the file is not corrupted (check file size)
3. Make sure the file format matches (ZIP or TAR.GZ)
4. Check file permissions

### Issue: "Open Folder" doesn't work

**Solution:**
1. Verify the installation directory exists
2. Check folder permissions
3. Try copying the path and opening manually
4. On Linux, ensure `xdg-open` is installed

## Future Enhancements

Potential future improvements:
- Torrent/P2P download support
- Download speed throttling
- Mirror server health checking
- Automatic mirror selection based on location
- Resume support for interrupted downloads (already implemented for single URLs)
- File browser for local file selection
- Drag-and-drop support for local files

## Related Documentation

- [Engine Lifecycle Management](ENGINE_LIFECYCLE_IMPLEMENTATION.md)
- [Download Center](DOWNLOAD_CENTER.md)
- [Build and Run Guide](BUILD_AND_RUN.md)
