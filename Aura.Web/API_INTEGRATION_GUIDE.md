# Video API Integration and State Management

This PR adds comprehensive video API integration with typed methods, custom hooks, error boundaries, and offline detection.

## Features Added

### 1. Video API Service (`src/services/api/videoApi.ts`)

Typed methods for video generation operations:

```typescript
import { generateVideo, getVideoStatus, cancelVideoGeneration, streamProgress } from '@/services/api/videoApi';

// Generate a video
const response = await generateVideo({
  brief: { topic: 'My Topic', audience: 'General', ... },
  planSpec: { targetDuration: '00:01:00', ... },
  voiceSpec: { voiceName: 'David', ... },
  renderSpec: { res: '1080p', ... }
});

// Check status
const status = await getVideoStatus(response.jobId);

// Stream progress via SSE
const eventSource = streamProgress(response.jobId, (update) => {
  console.log('Progress update:', update);
});

// Cancel generation
await cancelVideoGeneration(response.jobId);
```

### 2. Custom Hooks

#### `useVideoGeneration`

Complete video generation lifecycle management:

```typescript
import { useVideoGeneration } from '@/hooks/useVideoGeneration';

function MyComponent() {
  const { isGenerating, progress, status, error, generate, cancel, retry, reset } = useVideoGeneration({
    onComplete: (status) => console.log('Done!', status),
    onError: (error) => console.error('Failed:', error),
    onProgress: (percent, message) => console.log(`${percent}%: ${message}`)
  });

  const handleGenerate = async () => {
    await generate(videoRequest);
  };

  return (
    <>
      <button onClick={handleGenerate} disabled={isGenerating}>Generate</button>
      {isGenerating && <ProgressBar value={progress} />}
      {error && <ErrorDisplay error={error} onRetry={retry} />}
    </>
  );
}
```

#### `useSSEConnection`

Resilient Server-Sent Events connection with auto-reconnect:

```typescript
import { useSSEConnection } from '@/hooks/useSSEConnection';

function MyComponent() {
  const { isConnected, reconnectAttempt, connect, disconnect } = useSSEConnection({
    onMessage: (message) => console.log('SSE message:', message),
    onError: (error) => console.error('SSE error:', error),
    reconnectDelay: 3000,
    maxReconnectAttempts: 5
  });

  useEffect(() => {
    connect('/api/jobs/123/events');
    return () => disconnect();
  }, []);

  return <div>Connected: {isConnected ? 'Yes' : 'No'}</div>;
}
```

#### `useApiError`

Standardized error handling with user-friendly messages:

```typescript
import { useApiError } from '@/hooks/useApiError';

function MyComponent() {
  const { error, errorInfo, setError, clearError, isRetryable } = useApiError();

  const handleApiCall = async () => {
    try {
      await someApiCall();
    } catch (err) {
      setError(err); // Automatically parses AxiosError
    }
  };

  return (
    <>
      {error && (
        <div>
          <p>{errorInfo?.userMessage}</p>
          {isRetryable && <button onClick={handleApiCall}>Retry</button>}
          <button onClick={clearError}>Dismiss</button>
        </div>
      )}
    </>
  );
}
```

#### `useRetry`

Exponential backoff retry logic:

```typescript
import { useRetry } from '@/hooks/useRetry';

function MyComponent() {
  const { execute, isRetrying, attempt, nextRetryDelay } = useRetry({
    maxAttempts: 3,
    initialDelay: 1000,
    backoffMultiplier: 2,
    onRetry: (attempt, delay) => console.log(`Retry ${attempt} in ${delay}ms`)
  });

  const handleApiCall = async () => {
    const result = await execute(async () => {
      return await riskyApiCall();
    });
    console.log('Success:', result);
  };

  return (
    <>
      <button onClick={handleApiCall} disabled={isRetrying}>Call API</button>
      {isRetrying && <span>Retrying... (attempt {attempt})</span>}
    </>
  );
}
```

#### `useOnlineStatus`

Detect online/offline status:

```typescript
import { useOnlineStatus } from '@/hooks/useOnlineStatus';

function MyComponent() {
  const isOnline = useOnlineStatus();

  return (
    <div>
      {!isOnline && <div className="warning">You are offline</div>}
      <button disabled={!isOnline}>Submit</button>
    </div>
  );
}
```

### 3. Error Boundaries

#### Global Error Boundary

Catches app-level errors:

```typescript
import { GlobalErrorBoundary } from '@/components/ErrorBoundary';

function App() {
  return (
    <GlobalErrorBoundary>
      <YourApp />
    </GlobalErrorBoundary>
  );
}
```

#### Component Error Boundary

Catches errors in specific components:

```typescript
import { ComponentErrorBoundary } from '@/components/ErrorBoundary';

function MyFeature() {
  return (
    <ComponentErrorBoundary
      componentName="Video Editor"
      onError={(error, errorInfo) => logToService(error)}
    >
      <VideoEditor />
    </ComponentErrorBoundary>
  );
}
```

### 4. Offline Detection

Shows warning when offline:

```typescript
import { OfflineIndicator } from '@/components/OfflineIndicator';

function App() {
  return (
    <>
      <OfflineIndicator />
      <YourApp />
    </>
  );
}
```

## Integration with Existing Code

### Using with Zustand Store

The new hooks can be used alongside existing Zustand stores:

```typescript
import { useJobsStore } from '@/state/jobs';
import { useVideoGeneration } from '@/hooks/useVideoGeneration';

function MyComponent() {
  const { activeJob } = useJobsStore();
  const { generate } = useVideoGeneration();

  // Use both together
  const handleGenerate = async () => {
    const response = await generate(request);
    // Job will be tracked by jobs store via SSE
  };
}
```

### Circuit Breaker Pattern

The existing circuit breaker in `apiClient.ts` is automatically used by all new API methods:

- Opens after 5 consecutive failures
- Half-open state after 60 seconds
- Closes after 2 successful requests in half-open state
- Persists state across page reloads

## Testing

All new code has comprehensive unit tests:

```bash
# Run all tests
npm test

# Run specific test suites
npm test src/services/api/__tests__/videoApi.test.ts
npm test src/hooks/__tests__/useApiError.test.ts
npm test src/hooks/__tests__/useRetry.test.ts
```

## Example Usage

See `src/examples/VideoGenerationExample.tsx` for a complete working example.

## Architecture Benefits

1. **Type Safety**: All API methods are fully typed with TypeScript interfaces
2. **Error Handling**: Standardized error handling with user-friendly messages
3. **Resilience**: Built-in retry logic, circuit breaker, and SSE reconnection
4. **Testability**: All code is unit tested with proper mocking
5. **Separation of Concerns**: Clear separation between API layer, business logic, and UI
6. **Reusability**: Hooks can be used in any component
7. **Offline Support**: Graceful degradation when network is unavailable

## Migration Guide

If you have existing code that calls the API directly, you can migrate to the new pattern:

**Before:**

```typescript
const response = await fetch('/api/jobs', {
  method: 'POST',
  body: JSON.stringify(request),
});
const data = await response.json();
```

**After:**

```typescript
import { generateVideo } from '@/services/api/videoApi';
const data = await generateVideo(request);
```

Or use the hook for complete lifecycle management:

```typescript
import { useVideoGeneration } from '@/hooks/useVideoGeneration';
const { generate, isGenerating, progress } = useVideoGeneration();
await generate(request);
```

## Performance Considerations

- Request deduplication prevents duplicate API calls
- Circuit breaker prevents cascading failures
- SSE auto-reconnect uses exponential backoff
- Retry logic uses exponential backoff with jitter
- All hooks properly cleanup on unmount

## Browser Support

- Modern browsers with EventSource support (all major browsers)
- Fallback behavior for offline detection
- Polyfills not required for target environments
