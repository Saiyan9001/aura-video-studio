# Engines API Endpoints

This document describes the API endpoints for engine management in Aura Video Studio.

## Base URL

All endpoints are relative to: `http://127.0.0.1:5005/api/engines`

## Endpoints

### GET /list

Get list of available engines from manifest.

**Response:**
```json
{
  "engines": [
    {
      "id": "ollama",
      "name": "Ollama",
      "version": "0.1.19",
      "description": "Run large language models locally",
      "sizeBytes": 524288000,
      "defaultPort": 11434,
      "isInstalled": true,
      "installPath": "C:\\Users\\user\\AppData\\Local\\Aura\\Engines\\ollama",
      "isGated": false,
      "canInstall": true,
      "canAutoStart": true,
      "icon": "🦙",
      "tags": ["llm", "local"]
    }
  ],
  "hardwareInfo": {
    "hasNvidia": true,
    "vramGB": 8
  }
}
```

### GET /status

Get status of a specific engine.

**Query Parameters:**
- `engineId` (required): Engine identifier

**Response:**
```json
{
  "engineId": "ollama",
  "name": "Ollama",
  "status": "running",
  "installedVersion": "0.1.19",
  "isRunning": true,
  "port": 11434,
  "health": "healthy",
  "processId": 12345,
  "logsPath": "C:\\Users\\user\\AppData\\Local\\Aura\\Logs\\ollama.log",
  "messages": []
}
```

### POST /install

Install an engine.

**Request Body:**
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
- `customUrl`: Download from a custom URL instead of official mirrors
- `localFilePath`: Import from a local archive file
- `customUrl` and `localFilePath` are mutually exclusive

**Response:**
```json
{
  "success": true,
  "engineId": "ollama",
  "installPath": "C:\\Users\\user\\AppData\\Local\\Aura\\Engines\\ollama",
  "message": "Engine Ollama installed successfully"
}
```

**Error Responses:**
- `400`: Bad request (invalid parameters)
- `404`: Engine not found in manifest
- `499`: Installation cancelled by user
- `500`: Installation failed (network error, checksum mismatch, etc.)

### POST /verify

Verify an engine installation.

**Request Body:**
```json
{
  "engineId": "ollama"
}
```

**Response:**
```json
{
  "engineId": "ollama",
  "isValid": true,
  "status": "Valid",
  "missingFiles": [],
  "issues": []
}
```

### POST /repair

Repair an engine installation by reinstalling.

**Request Body:**
```json
{
  "engineId": "ollama"
}
```

**Response:**
```json
{
  "success": true,
  "engineId": "ollama",
  "installPath": "C:\\Users\\user\\AppData\\Local\\Aura\\Engines\\ollama",
  "message": "Engine Ollama repaired successfully"
}
```

### POST /remove

Remove an engine installation.

**Request Body:**
```json
{
  "engineId": "ollama"
}
```

**Response:**
```json
{
  "success": true,
  "engineId": "ollama",
  "message": "Engine removed successfully"
}
```

### POST /start

Start an engine.

**Request Body:**
```json
{
  "engineId": "ollama",
  "port": 11434,  // Optional
  "args": "--verbose"  // Optional
}
```

**Response:**
```json
{
  "success": true,
  "engineId": "ollama",
  "processId": 12345,
  "port": 11434,
  "logsPath": "C:\\Users\\user\\AppData\\Local\\Aura\\Logs\\ollama.log",
  "message": "Engine Ollama started successfully"
}
```

### POST /stop

Stop a running engine.

**Request Body:**
```json
{
  "engineId": "ollama"
}
```

**Response:**
```json
{
  "success": true,
  "engineId": "ollama",
  "message": "Engine stopped successfully"
}
```

### GET /diagnostics/engine

Get diagnostics for a specific engine.

**Query Parameters:**
- `engineId` (required): Engine identifier

**Response:**
```json
{
  "engineId": "ollama",
  "installPath": "C:\\Users\\user\\AppData\\Local\\Aura\\Engines\\ollama",
  "isInstalled": true,
  "pathExists": true,
  "pathWritable": true,
  "availableDiskSpaceBytes": 107374182400,
  "lastError": null,
  "checksumStatus": "Valid",
  "expectedSha256": "abc123...",
  "actualSha256": null,
  "failedUrl": null,
  "issues": []
}
```

### GET /provenance

**NEW** Get installation provenance information.

**Query Parameters:**
- `engineId` (required): Engine identifier

**Response:**
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

**Source Types:**
- `Mirror`: Installed from official mirrors
- `CustomUrl`: Installed from custom URL
- `LocalFile`: Imported from local file

**Error Responses:**
- `400`: Bad request (missing engineId)
- `404`: Provenance information not found

### POST /open-folder

**NEW** Open the installation folder in file explorer.

**Request Body:**
```json
{
  "engineId": "ollama"
}
```

**Response:**
```json
{
  "success": true,
  "path": "C:\\Users\\user\\AppData\\Local\\Aura\\Engines\\ollama"
}
```

**Error Responses:**
- `400`: Bad request (missing engineId)
- `404`: Installation path does not exist
- `500`: Failed to open folder

**Platform Behavior:**
- Windows: Opens in Explorer
- Linux: Opens with xdg-open
- macOS: Opens with Finder

### POST /restart

Restart a running engine.

**Request Body:**
```json
{
  "engineId": "ollama"
}
```

**Response:**
```json
{
  "message": "Engine ollama restarted successfully"
}
```

### GET /logs

Get logs for an engine.

**Query Parameters:**
- `engineId` (required): Engine identifier
- `tailLines` (optional): Number of lines to return from end (default: 500)

**Response:**
```json
{
  "logs": "2025-10-12 01:23:45 [INFO] Engine started\\n..."
}
```

## Common Error Responses

### 400 Bad Request
Missing or invalid parameters.

```json
{
  "error": "engineId is required"
}
```

### 404 Not Found
Resource not found.

```json
{
  "error": "Engine ollama not found in manifest"
}
```

### 499 Client Closed Request
User cancelled the operation.

```json
{
  "error": "Installation cancelled by user"
}
```

### 500 Internal Server Error
Operation failed.

```json
{
  "error": "Network error during download. Check your internet connection and try again.",
  "details": "HttpRequestException: ..."
}
```

## Download Error Codes

When downloads fail, the system uses specific error codes:

- `E-DL-404`: Mirror returned 404 Not Found
- `E-DL-TIMEOUT`: Download timed out
- `E-DL-CHECKSUM`: Checksum verification failed

These codes are reported in progress messages and diagnostic information.

## Installation Flow

1. Call `POST /install` with engine ID and optional parameters
2. Monitor progress via status polling or WebSocket events
3. On completion, call `GET /status` to verify installation
4. Optionally call `GET /provenance` to get installation details
5. Start the engine with `POST /start`

## Mirror Fallback

When installing engines, the system automatically:
1. Tries the primary URL from the manifest
2. On failure (404, timeout), tries each mirror in sequence
3. Uses exponential backoff for retries on each URL
4. Reports which mirror is being used in progress messages

## Checksum Verification

All downloads are verified using SHA-256 checksums:
1. Download completes
2. System computes SHA-256 hash
3. Compares with expected hash from manifest
4. On mismatch:
   - Network downloads: Try next mirror
   - Custom URLs: Abort installation
   - Local files: Warn user but allow installation

## Related Documentation

- [Download Robustness Features](DOWNLOAD_ROBUSTNESS.md)
- [Engine Lifecycle Management](ENGINE_LIFECYCLE_IMPLEMENTATION.md)
- [Download Center](DOWNLOAD_CENTER.md)
