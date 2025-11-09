# Provider API Key Management and Validation

## Overview

Aura Video Studio provides secure API key storage and validation for multiple cloud service providers. This document describes the provider management system including secure storage, validation endpoints, and fallback strategies.

## Features

### Secure Key Storage
- **Windows**: Uses Windows Data Protection API (DPAPI) for encryption
- **Linux/macOS**: Uses AES-256 encryption with machine-specific keys
- **Storage Location**: `%LOCALAPPDATA%\Aura\secure\apikeys.dat` (Windows) or `~/.local/share/Aura/secure/apikeys.dat` (Linux/macOS)
- **Permissions**: Files are automatically restricted to owner-only access (600 on Unix)

### Supported Providers

#### LLM Providers (Script Generation)
- **OpenAI** - GPT-4, GPT-3.5 models
- **Anthropic** - Claude models
- **Google Gemini** - Gemini models
- **Azure OpenAI** - Microsoft Azure hosted OpenAI
- **Ollama** - Local AI models (free, offline)
- **RuleBased** - Template-based generation (free, always available, offline)

#### Text-to-Speech Providers
- **ElevenLabs** - Premium voice synthesis
- **PlayHT** - Cloud voice synthesis with cloning
- **Windows SAPI** - Built-in Windows TTS (free, always available)
- **Piper** - Local neural TTS (free, offline)
- **Mimic3** - Local neural TTS (free, offline)

#### Image Providers
- **Stability AI** - AI image generation
- **Pexels** - Free stock images
- **Pixabay** - Free stock images
- **Unsplash** - High-quality stock images
- **Local SD** - Local Stable Diffusion (free, requires GPU)

## API Endpoints

### Validation Endpoints

All validation endpoints perform live network checks to verify API keys work correctly.

#### POST /api/providers/openai/validate
Validates OpenAI API key with network check.

**Request**:
```json
{
  "apiKey": "sk-...",
  "baseUrl": null,
  "organizationId": null,
  "projectId": null
}
```

**Response**:
```json
{
  "isValid": true,
  "status": "Valid",
  "message": "OpenAI API key is valid and working",
  "correlationId": "abc123",
  "details": {
    "provider": "OpenAI",
    "keyFormat": "valid",
    "formatValid": true,
    "networkCheckPassed": true,
    "httpStatusCode": 200,
    "responseTimeMs": 150
  }
}
```

#### POST /api/providers/elevenlabs/validate
Validates ElevenLabs API key.

**Request**:
```json
{
  "apiKey": "your-elevenlabs-api-key"
}
```

**Response**: Same format as OpenAI validation

#### POST /api/providers/playht/validate
Validates PlayHT API key.

**Request**:
```json
{
  "apiKey": "your-playht-api-key"
}
```

**Response**: Same format as OpenAI validation

### Status Endpoint

#### GET /api/providers/status
Returns the configuration status of all providers.

**Response**:
```json
[
  {
    "name": "OpenAI",
    "isConfigured": true,
    "isAvailable": true,
    "status": "Configured",
    "lastValidated": "2025-01-15T10:30:00Z"
  },
  {
    "name": "ElevenLabs",
    "isConfigured": false,
    "isAvailable": false,
    "status": "Not Configured"
  },
  {
    "name": "RuleBased",
    "isConfigured": true,
    "isAvailable": true,
    "status": "Always Available (Offline)"
  }
]
```

## Frontend Integration

### Provider Status Dashboard

The Provider Status Dashboard component (`ProviderStatusDashboard.tsx`) displays:
- Visual grid of all providers
- Configuration status (green checkmark = configured, gray dismiss icon = not configured)
- Last validation timestamp
- Refresh button to reload status

**Usage in Settings**:
```tsx
import { ProviderStatusDashboard } from './ProviderStatusDashboard';

// In your settings component:
<ProviderStatusDashboard />
```

### State Management

Provider state is managed using Zustand in `src/state/providers.ts`:

```typescript
import { useProviderStore } from '@/state/providers';

// In your component:
const { providerStatuses, refreshProviderStatuses } = useProviderStore();

// Refresh provider statuses:
await refreshProviderStatuses();
```

## Provider Fallback Strategy

When premium providers are unavailable, the system automatically falls back to free alternatives:

### LLM Fallback Chain
1. **OpenAI/Anthropic/Gemini** (premium, requires API key)
2. **Ollama** (free, local, requires installation)
3. **RuleBased** (free, always available, template-based)

### TTS Fallback Chain
1. **ElevenLabs/PlayHT** (premium, requires API key)
2. **Piper/Mimic3** (free, local, requires installation)
3. **Windows SAPI** (free, always available on Windows)

### Image Fallback Chain
1. **Stability AI** (premium, requires API key)
2. **Local SD** (free, requires GPU with 6GB+ VRAM)
3. **Stock Images** (free, always available)

## Security Considerations

### API Key Storage
- Keys are encrypted before storage using platform-specific encryption
- Windows: DPAPI with CurrentUser scope
- Linux/macOS: AES-256 with machine-specific key
- File permissions restricted to owner only
- Keys never logged or exposed in error messages

### Key Validation
- Network validation uses minimal API calls
- Timeouts prevent hanging requests (15-30 seconds)
- Exponential backoff for rate-limited requests
- Secure handling of validation errors

### Best Practices
1. Never commit API keys to source control
2. Store keys in secure storage service, not configuration files
3. Test keys regularly using validation endpoints
4. Rotate keys periodically for security
5. Use fallback providers when possible to reduce costs

## Troubleshooting

### Key Validation Fails
- Check internet connectivity
- Verify API key is correct and not expired
- Check provider service status
- Review API usage limits and billing

### Storage Errors
- Check file system permissions
- Verify %LOCALAPPDATA% or ~/.local exists
- Check disk space availability
- Review security software blocking file access

### Provider Not Available
- Check if API key is configured
- Verify provider requirements (GPU, OS, etc.)
- Check if provider service is running (for local providers)
- Review error messages in provider status dashboard

## Implementation Notes

The provider API key management system leverages several existing services:
- `ISecureStorageService` - Handles encrypted key storage (DPAPI/AES-256)
- `IKeyValidationService` - Performs network validation tests
- `KeyVaultController` - (deprecated in favor of ProvidersController)
- `ProvidersController` - Main API controller for provider operations

All services are registered in dependency injection and are available throughout the application.
