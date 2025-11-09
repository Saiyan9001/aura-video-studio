# Frontend-to-Backend API Integration

This document describes the comprehensive API integration implemented in PR #28.

## Overview

The frontend now has complete, type-safe integration with the backend API through:

- Centralized API client with retry and circuit breaker
- Typed service layers for all major features
- Zustand stores integrated with API endpoints
- Real-time progress updates via Server-Sent Events (SSE)

## API Client (`src/services/api/apiClient.ts`)

### Features

- **Environment-based URL**: Automatically uses `VITE_API_BASE_URL` from environment
- **Auth Interceptors**: Automatically adds Bearer token from localStorage
- **Error Handling**: Comprehensive error handling with user-friendly messages
- **Logging**: Request/response logging in development mode
- **Circuit Breaker**: Prevents cascading failures with automatic recovery
- **Retry Logic**: Automatic retry with exponential backoff for transient errors
- **Timeouts**: Configurable timeouts (default 30s, extendable per request)
- **Request Deduplication**: Prevents duplicate POST/PUT requests

### Usage

```typescript
import { get, post, postWithTimeout } from '@/services/api/apiClient';

// Simple GET request
const data = await get<ResponseType>('/api/endpoint');

// POST with extended timeout
const result = await postWithTimeout<ResponseType>(
  '/api/endpoint',
  { data: 'value' },
  120000 // 2 minute timeout
);
```

## Services

### Video API (`src/services/api/videoApi.ts`)

Handles video generation operations.

```typescript
import { generateVideo, getVideoStatus, cancelVideoGeneration } from '@/services/api/videoApi';

// Generate video
const response = await generateVideo({
  brief: {
    topic: '...',
    audience: '...',
    goal: '...',
    tone: '...',
    language: '...',
    aspect: '...',
  },
  planSpec: { targetDuration: '...', pacing: '...', density: '...', style: '...' },
  voiceSpec: { voiceName: '...', rate: 1.0, pitch: 0.0, pause: '...' },
  renderSpec: {
    res: '...',
    container: '...',
    videoBitrateK: 5000,
    audioBitrateK: 192,
    fps: 30,
    codec: '...',
    qualityLevel: '...',
    enableSceneCut: true,
  },
});

// Check status
const status = await getVideoStatus(response.jobId);

// Cancel job
await cancelVideoGeneration(response.jobId);
```

### Providers API (`src/services/api/providersApi.ts`)

Manages provider configuration, status, and validation.

```typescript
import {
  getProviderStatuses,
  testProviderConnection,
  validateOpenAIKey,
  validateElevenLabsKey,
  getProviderModels,
} from '@/services/api/providersApi';

// Get all provider statuses
const statuses = await getProviderStatuses();

// Test connection
const result = await testProviderConnection('openai', { apiKey: 'sk-...' });

// Validate API key
const validation = await validateOpenAIKey('sk-...');

// Get available models
const models = await getProviderModels('openai');
```

### Wizard Service (`src/services/wizardService.ts`)

Integrates Video Creation Wizard with backend endpoints.

```typescript
import {
  storeBrief,
  fetchAvailableVoices,
  generateScript,
  generatePreview,
  startFinalRendering,
} from '@/services/wizardService';

// Step 1: Store brief
await storeBrief({
  topic: 'AI Introduction',
  audience: 'Students',
  goal: 'Educate',
  tone: 'Professional',
  language: 'English',
  duration: 60,
  videoType: 'educational',
});

// Step 2: Fetch voices
const voices = await fetchAvailableVoices('ElevenLabs');

// Step 3: Generate script
const scriptResult = await generateScript(briefData, styleData);

// Step 4: Generate preview
const previewResult = await generatePreview(briefData, styleData, scriptData, previewConfig);

// Step 5: Start final rendering
const { jobId } = await startFinalRendering(briefData, styleData, scriptData, exportConfig);
```

### Settings Service (`src/services/settingsService.ts`)

Manages user settings with backend persistence.

```typescript
import { settingsService } from '@/services/settingsService';

// Load settings
const settings = await settingsService.loadSettings();

// Save settings
await settingsService.saveSettings(updatedSettings);

// Test API key
const result = await settingsService.testApiKey('openai', 'sk-...');
```

### Project Service (`src/services/projectService.ts`)

Handles project save/load operations.

```typescript
import { getProjects, saveProject, deleteProject } from '@/services/projectService';

// List projects
const projects = await getProjects();

// Save project
await saveProject('My Project', projectFile, projectId, 'Description');

// Delete project
await deleteProject(projectId);
```

## Zustand Stores with API Integration

### Jobs Store (`src/state/jobs.ts`)

Manages video generation jobs with API integration.

```typescript
import { useJobsStore } from '@/state/jobs';

const { createJob, getJob, listJobs, cancelJob, startStreaming, stopStreaming } = useJobsStore();

// Create job
const jobId = await createJob(brief, planSpec, voiceSpec, renderSpec);

// Start SSE streaming for progress
startStreaming(jobId);

// Get job details
await getJob(jobId);

// Cancel job
await cancelJob(jobId);

// Stop streaming when done
stopStreaming();
```

### Provider Config Store (`src/state/providerConfig.ts`)

Manages provider configuration with API integration.

```typescript
import { useProviderConfigStore } from '@/state/providerConfig';

const {
  fetchProviderStatuses,
  loadProviderPreferences,
  saveProviderPreferences,
  testConnection,
  validateApiKey,
} = useProviderConfigStore();

// Fetch provider statuses
await fetchProviderStatuses();

// Test connection
const success = await testConnection('openai', { apiKey: 'sk-...' });

// Validate API key
const result = await validateApiKey('openai', 'sk-...');

// Save preferences
await saveProviderPreferences('Pro', { script: 'OpenAI', tts: 'ElevenLabs' });
```

## Server-Sent Events (SSE) for Real-Time Progress

The SSE client (`src/services/api/sseClient.ts`) provides real-time progress updates.

### Features

- **Auto-reconnect**: Automatically reconnects on connection loss
- **Exponential Backoff**: Prevents overwhelming the server
- **Last-Event-ID Support**: Resumes from last received event
- **Connection State Tracking**: Monitor connection status

### Usage

```typescript
import { createSseClient } from '@/services/api/sseClient';

const sseClient = createSseClient(jobId);

// Listen for progress events
sseClient.on('job-status', (event) => {
  console.log('Job status:', event.data);
});

sseClient.on('step-progress', (event) => {
  console.log('Progress:', event.data.progressPct);
});

sseClient.on('job-completed', (event) => {
  console.log('Job completed:', event.data);
  sseClient.close();
});

sseClient.on('job-failed', (event) => {
  console.error('Job failed:', event.data);
  sseClient.close();
});

// Track connection state
sseClient.onStatusChange((state) => {
  console.log('Connection state:', state.status);
});

// Connect
sseClient.connect();

// Clean up when done
sseClient.close();
```

## Error Handling

All services follow consistent error handling patterns:

```typescript
try {
  const result = await apiCall();
  // Handle success
} catch (error: unknown) {
  const errorObj = error instanceof Error ? error : new Error(String(error));
  logger.error('Operation failed', errorObj, 'component', 'method');

  // Display user-friendly message
  if (error && typeof error === 'object' && 'userMessage' in error) {
    showNotification((error as { userMessage: string }).userMessage);
  } else {
    showNotification('An unexpected error occurred');
  }
}
```

## Loading States

All stores include loading state management:

```typescript
const { loading, isSaving, isTestingConnection } = useStore();

// In components
{loading && <Spinner label="Loading..." />}
{isSaving && <Button disabled>Saving...</Button>}
{isTestingConnection['openai'] && <Spinner size="tiny" />}
```

## Timeouts

Different operations use different timeouts:

- **Normal requests**: 30 seconds (default)
- **Script generation**: 2 minutes (120,000ms)
- **Preview generation**: 3 minutes (180,000ms)
- **Video generation**: 5 minutes (300,000ms)

```typescript
// Using extended timeout
const result = await postWithTimeout<T>(
  '/api/endpoint',
  data,
  180000 // 3 minutes
);
```

## Testing

All services have comprehensive test coverage:

- `src/services/api/__tests__/providersApi.test.ts`
- `src/services/__tests__/wizardService.test.ts`
- `src/services/api/__tests__/videoApi.test.ts`
- `src/state/__tests__/jobs.test.ts`

Run tests:

```bash
npm test
```

## Environment Configuration

Configure API base URL in `.env.development` or `.env.production`:

```env
VITE_API_BASE_URL=http://localhost:5005
```

The app will automatically use this URL for all API calls.

## Next Steps

To complete the integration:

1. **Wire UI Components**: Connect wizard steps to use wizardService methods
2. **Add Loading States**: Show spinners during API calls
3. **Display Error Messages**: Show user-friendly error notifications
4. **Test Progress Updates**: Verify SSE progress updates work correctly
5. **Test Provider UI**: Verify test connection and validate buttons work
6. **Integration Tests**: Write E2E tests for complete workflows

## Troubleshooting

### API calls fail with CORS errors

- Verify backend CORS configuration includes frontend origin
- Check `VITE_API_BASE_URL` is set correctly

### Circuit breaker opens frequently

- Backend may be down or slow
- Check backend logs for errors
- Use `resetCircuitBreaker()` to manually reset if needed

### SSE connection drops

- SSE client will auto-reconnect with exponential backoff
- Check backend logs for SSE endpoint issues
- Maximum 5 reconnect attempts before giving up

### Request deduplication prevents valid requests

- Use `_skipDeduplication: true` in request config
- Or call `clearDeduplicationCache()` to clear cache
