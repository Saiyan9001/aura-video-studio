> **⚠️ ARCHIVED DOCUMENT**
>
> This document is archived for historical reference only.
> It may contain outdated information. See the [Documentation Index](../DocsIndex.md) for current documentation.

# Health Monitoring and Circuit Breaker Implementation

## Overview

This implementation adds comprehensive health monitoring, graceful degradation, and circuit breaker pattern for provider failures in the Aura Video Studio application.

## Features Implemented

### 1. Circuit Breaker Pattern (Backend)

#### Configuration (`appsettings.json`)
```json
{
  "CircuitBreaker": {
    "FailureThreshold": 5,
    "FailureRateThreshold": 0.5,
    "OpenDurationSeconds": 30,
    "TimeoutSeconds": 10,
    "HealthCheckTimeoutSeconds": 2,
    "RollingWindowSize": 100,
    "RollingWindowMinutes": 5
  }
}
```

#### Key Components

**CircuitBreakerSettings** (`Aura.Core/Configuration/CircuitBreakerSettings.cs`)
- Configurable thresholds for failure detection
- Supports both consecutive failure count and failure rate triggers
- Rolling window for failure rate calculation

**CircuitBreaker** (`Aura.Core/Services/Health/CircuitBreaker.cs`)
- Three states: Closed, Open, Half-Open
- Automatic state transitions based on failures/successes
- Exponential backoff with configurable cooldown period
- Thread-safe implementation with SemaphoreSlim

**State Transitions:**
1. **Closed → Open**: After N consecutive failures or >X% failure rate
2. **Open → Half-Open**: After cooldown period expires
3. **Half-Open → Closed**: On first successful request
4. **Half-Open → Open**: On failure during test period

### 2. Enhanced Health Monitoring (Backend)

#### ProviderHealthMonitor Updates
- Integrated circuit breaker logic
- Extended ProviderHealthMetrics with circuit breaker state
- Configurable health check timeouts (2s for probes vs 10s for operations)

#### ProviderHealthService (`Aura.Core/Services/Health/ProviderHealthService.cs`)
- Centralized health tracking for all provider types
- Provider type categorization (LLM, TTS, Images)
- Health status queries by type
- Circuit breaker reset functionality

#### SystemHealthChecker (`Aura.Core/Services/Health/SystemHealthChecker.cs`)
- FFmpeg availability and version detection
- Disk space monitoring (warns below 1GB)
- Memory usage tracking (warns above 90%)
- System-level health issues reporting

### 3. Health API Endpoints (Backend)

#### New Endpoints in HealthController

**GET `/api/health/llm`**
- Returns health status for all LLM providers
- 503 status if all LLM providers down
- Circuit breaker state for each provider

**GET `/api/health/tts`**
- Returns health status for all TTS providers
- 503 status if all TTS providers down

**GET `/api/health/images`**
- Returns health status for all image providers
- 503 status if all image providers down

**GET `/api/health/system`**
- FFmpeg availability and version
- Disk space (GB)
- Memory usage (%)
- System health issues list
- 503 status if system unhealthy

**POST `/api/health/providers/{name}/reset`**
- Manually reset circuit breaker for a provider
- Clears failure history and consecutive failure count
- Returns 404 if provider not found

#### Response Format
```typescript
{
  providerName: string;
  isHealthy: boolean;
  lastCheckTime: string;
  responseTimeMs: number;
  consecutiveFailures: number;
  lastError?: string;
  successRate: number;
  averageResponseTimeMs: number;
  circuitState: "Closed" | "Open" | "HalfOpen";
  failureRate: number;
  circuitOpenedAt?: string;
}
```

### 4. Frontend Dashboard

#### SystemHealthDashboard (`Aura.Web/src/pages/Health/SystemHealthDashboard.tsx`)

**Features:**
- Real-time health monitoring with 30-second polling
- System status card (FFmpeg, disk, memory)
- Provider health by type (LLM, TTS, Images)
- Circuit breaker state visualization
- Warning banners for critical failures
- Auto-refresh toggle

**Per-Provider Cards Display:**
- Provider name and health badge
- Success rate and failure rate
- Average response time
- Consecutive failure count
- Circuit breaker state
- Last check timestamp
- Last error message (if any)

**Actions:**
- "Test Connection" button per provider
- "Reset Circuit" button when circuit is open
- Manual refresh all
- Auto-refresh toggle (30s interval)

#### UI Components
- Health status badges (Healthy/Unhealthy/Circuit Open/Testing)
- Warning banner when critical providers down
- Metrics display with labels
- Card-based layout with responsive grid

### 5. Testing

#### Unit Tests (`Aura.Tests/CircuitBreakerTests.cs`)
**11 comprehensive test cases:**
1. Initial state verification (Closed)
2. Success keeps circuit closed
3. Consecutive failures open circuit
4. Circuit open throws CircuitBreakerOpenException
5. Open to half-open transition after cooldown
6. Half-open to closed on success
7. Half-open to open on failure
8. Failure rate calculation accuracy
9. Circuit opens on failure rate threshold
10. Reset closes circuit and clears failures
11. Rolling window discards old failures

**All tests passing (11/11)**

## Architecture Decisions

### 1. Circuit Breaker State Persistence
- Currently in-memory (ConcurrentDictionary)
- Suitable for single-instance deployments
- For multi-instance: consider distributed cache (Redis)

### 2. Failure Rate vs Consecutive Failures
- Both conditions can trigger circuit opening
- Consecutive failures: 5 in a row (default)
- Failure rate: 50% over rolling window (default)
- Provides robust detection of both intermittent and persistent failures

### 3. Rolling Window
- Default: 100 requests over 5 minutes
- Prevents old failures from affecting current state
- Configurable size and time span

### 4. Health Check Timeouts
- Health probes: 2 seconds (quick checks)
- Production operations: 10 seconds (allow more time)
- Prevents health checks from blocking operations

## Usage Examples

### Backend - Using Circuit Breaker
```csharp
var circuitBreaker = healthMonitor.GetCircuitBreaker("OpenAI");
if (circuitBreaker != null)
{
    try
    {
        var result = await circuitBreaker.ExecuteAsync(async ct => 
        {
            return await provider.DraftScriptAsync(brief, spec, ct);
        }, cancellationToken);
    }
    catch (CircuitBreakerOpenException)
    {
        // Try fallback provider
        logger.LogWarning("OpenAI circuit open, using fallback");
    }
}
```

### Frontend - Polling Health Status
```typescript
useEffect(() => {
  const fetchHealth = async () => {
    const llmHealth = await apiClient.get('/api/health/llm');
    setLlmHealth(llmHealth.data);
  };
  
  fetchHealth();
  const interval = setInterval(fetchHealth, 30000);
  return () => clearInterval(interval);
}, []);
```

## Configuration Guide

### Tuning Circuit Breaker

**For Sensitive Providers (open quickly):**
```json
{
  "FailureThreshold": 3,
  "FailureRateThreshold": 0.3,
  "OpenDurationSeconds": 60
}
```

**For Resilient Providers (tolerate more failures):**
```json
{
  "FailureThreshold": 10,
  "FailureRateThreshold": 0.7,
  "OpenDurationSeconds": 10
}
```

**For Testing/Development (quick recovery):**
```json
{
  "FailureThreshold": 2,
  "OpenDurationSeconds": 5
}
```

## Monitoring and Observability

### Logs
All circuit breaker state transitions are logged:
- Circuit opening (with reason)
- Transition to half-open
- Circuit closing on recovery
- Manual resets

### Metrics Available
- Provider health status (GET /api/health/providers)
- Circuit breaker states
- Success/failure rates
- Response times
- Consecutive failure counts

## Future Enhancements

### Recommended Additions
1. **Persistent Circuit State**
   - Store in distributed cache for multi-instance deployments
   - Survive application restarts

2. **Integration Tests**
   - Mock provider failures
   - Test automatic fallback chains
   - Verify SSE notifications

3. **E2E Tests**
   - Simulate network timeouts
   - Test UI updates on circuit state changes
   - Verify warning banners display

4. **Provider Status in Video Generation**
   - Pre-flight check before starting generation
   - Show provider status in wizard
   - Suggest fallback providers when primary unavailable

5. **Health Metrics Dashboard**
   - Historical charts of provider health
   - Failure rate trends
   - Circuit open frequency

6. **Alerting**
   - Email/SMS notifications when circuit opens
   - Slack/Teams webhooks for critical failures
   - PagerDuty integration

## Breaking Changes

None. This is a new feature that enhances existing health monitoring without breaking existing functionality.

## Migration Notes

### For Existing Deployments
1. Add CircuitBreaker configuration section to appsettings.json
2. Health endpoints remain backward compatible
3. Old health monitoring continues to work
4. New circuit breaker features opt-in via configuration

### Database Migrations
None required - all state is in-memory

## Performance Impact

- Minimal overhead per request (< 1ms)
- Health checks run asynchronously
- Circuit breaker state checks are O(1)
- Rolling window cleanup is O(1) amortized

## Security Considerations

- Health endpoints do not expose API keys
- Error messages sanitized (no stack traces in responses)
- Correlation IDs for debugging without exposing internals
- Rate limiting applied to health endpoints (100 req/min)

## Documentation Updates

- API documentation updated with new endpoints
- User guide includes health dashboard usage
- Configuration reference includes circuit breaker settings
- Troubleshooting guide covers provider failures

## Contributors

Implemented as part of issue: "Add API health monitoring, graceful degradation, and circuit breaker for provider failures"

## References

- Circuit Breaker Pattern: Microsoft .NET Architecture Guide
- Health Check API: ASP.NET Core Health Checks
- Frontend Patterns: React Best Practices for Real-time Monitoring
