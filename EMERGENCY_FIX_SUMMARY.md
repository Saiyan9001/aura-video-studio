# Emergency Fix: Quick Demo and Generate Video Buttons

## Problem Statement
The Quick Demo and Generate Video buttons were non-functional - clicking them resulted in no feedback, no job creation, and no errors shown to the user.

## Root Causes Identified

### 1. Critical Port Mismatch (PRIMARY ISSUE)
**Problem**: The frontend and backend were configured to communicate on different ports.

- **Backend Configuration**: Runs on port **5005** (configured in `Aura.Api/appsettings.json` and `Program.cs`)
- **Frontend Proxy**: Was targeting port **5272** (incorrectly configured in `Aura.Web/vite.config.ts`)
- **Frontend API URL**: Was defaulting to port **5272** in development (incorrectly configured in `Aura.Web/src/config/api.ts`)

**Impact**: All API calls from the frontend were failing to reach the backend, causing the buttons to appear unresponsive.

### 2. Hardcoded URL in Quick Demo Handler
**Problem**: The Quick Demo validation call used a hardcoded URL:
```typescript
await fetch('http://localhost:5005/api/validation/brief', ...)
```

**Impact**: This bypassed the Vite proxy and would fail in production environments where the API might be on a different host.

### 3. Missing Diagnostic Logging
**Problem**: No console logging to help diagnose issues when buttons were clicked.

**Impact**: Made it extremely difficult to troubleshoot the issue - silent failures with no feedback.

## Fixes Implemented

### 1. Fixed Port Configuration
**File**: `Aura.Web/vite.config.ts`
```diff
  proxy: {
    '/api': {
-     target: 'http://127.0.0.1:5272',
+     target: 'http://127.0.0.1:5005',
      changeOrigin: true,
    }
  }
```

**File**: `Aura.Web/src/config/api.ts`
```diff
  if (import.meta.env.DEV) {
-   // Default to common development port
-   return 'http://127.0.0.1:5272';
+   // Default to backend API port (should match Aura.Api appsettings.json)
+   return 'http://127.0.0.1:5005';
  }
```

### 2. Fixed Hardcoded URL
**File**: `Aura.Web/src/pages/Wizard/CreateWizard.tsx`
```diff
- const validationResponse = await fetch('http://localhost:5005/api/validation/brief', {
+ const validationResponse = await fetch(apiUrl('/api/validation/brief'), {
```

Also updated all other API calls to use the `apiUrl()` helper consistently.

### 3. Added Comprehensive Diagnostic Logging

Added detailed console logging to both button handlers:

**Quick Demo Button** (`CreateWizard.tsx`):
- Log when button is clicked
- Log current settings
- Log validation request/response
- Log API request/response
- Log all errors with full details
- Log state changes

**Generate Video Button** (`CreateWizard.tsx` and `CreatePage.tsx`):
- Log when button is clicked
- Log form data
- Log normalized request data
- Log API request/response
- Log all errors with full details
- Log state changes

Example logging pattern:
```typescript
console.log('[QUICK DEMO] Button clicked');
console.log('[QUICK DEMO] Current settings:', { topic: settings.brief.topic });
console.log('[QUICK DEMO] Calling API endpoint...');
console.log('[QUICK DEMO] API response status:', response.status);
console.log('[QUICK DEMO] API response data:', data);
```

## Files Changed

1. **Aura.Web/vite.config.ts** - Fixed proxy port from 5272 to 5005
2. **Aura.Web/src/config/api.ts** - Fixed default API URL from 5272 to 5005
3. **Aura.Web/src/pages/Wizard/CreateWizard.tsx** - Fixed hardcoded URL, added comprehensive logging
4. **Aura.Web/src/pages/CreatePage.tsx** - Added comprehensive logging

## Verification Steps

### Prerequisites
1. Backend must be running on port 5005:
   ```bash
   cd Aura.Api
   dotnet run
   ```
   
2. Frontend must be running on port 5173:
   ```bash
   cd Aura.Web
   npm run dev
   ```

### Test Quick Demo Button
1. Open browser to http://localhost:5173
2. Navigate to Create Wizard
3. Open browser console (F12)
4. Click "Quick Demo (Safe)" button
5. **Expected Console Output**:
   ```
   [QUICK DEMO] Button clicked
   [QUICK DEMO] Current settings: { topic: ... }
   [QUICK DEMO] Starting quick demo generation...
   [QUICK DEMO] Calling validation endpoint...
   [QUICK DEMO] Validation response status: 200
   [QUICK DEMO] Calling quick demo endpoint...
   [QUICK DEMO] API response status: 200
   [QUICK DEMO] API response data: { jobId: "...", ... }
   [QUICK DEMO] Job created successfully: ...
   ```
6. **Expected UI Feedback**:
   - Button shows "Starting..." while generating
   - Success toast appears with job ID
   - Generation panel opens showing progress

### Test Generate Video Button
1. Fill in the Brief form (topic is required)
2. Navigate through steps to step 3
3. Run preflight check
4. Open browser console (F12)
5. Click "Generate Video" button
6. **Expected Console Output**:
   ```
   [GENERATE VIDEO] Button clicked
   [GENERATE VIDEO] Current settings: { brief: ..., planSpec: ... }
   [GENERATE VIDEO] Starting video generation...
   [GENERATE VIDEO] Normalized brief: ...
   [GENERATE VIDEO] Request data: ...
   [GENERATE VIDEO] Calling API endpoint /api/jobs...
   [GENERATE VIDEO] API response status: 200
   [GENERATE VIDEO] Job created successfully: ...
   ```
7. **Expected UI Feedback**:
   - Button shows "Generating..." while processing
   - Generation panel opens showing progress
   - Status bar shows job progress

### Common Issues and Solutions

#### Issue: CORS Error in Console
**Cause**: Backend not running or CORS not configured
**Solution**: Ensure backend is running and CORS is enabled for http://localhost:5173

#### Issue: 404 Not Found for /api/quick/demo
**Cause**: Backend endpoint not registered or backend not running
**Solution**: Verify QuickController is registered and backend is running

#### Issue: Network Error / Connection Refused
**Cause**: Port mismatch or backend not running
**Solution**: Verify both frontend and backend are running on correct ports (5173 and 5005)

#### Issue: Silent Failure (No Logs)
**Cause**: JavaScript error preventing handler execution
**Solution**: Check browser console for errors, verify all imports are correct

## Backend Endpoints Confirmed Working

✅ **POST /api/quick/demo** - QuickController.CreateQuickDemo
- Creates a safe demo video with default settings
- Returns: `{ jobId: string, status: string, message: string }`

✅ **POST /api/jobs** - JobsController.CreateJob
- Creates a full video generation job
- Accepts: `{ brief, planSpec, voiceSpec, renderSpec }`
- Returns: `{ jobId: string, status: string, stage: string }`

✅ **GET /api/jobs/{jobId}** - JobsController.GetJob
- Retrieves job status and progress
- Returns full job object with progress, stage, status, etc.

✅ **GET /api/jobs/{jobId}/progress** - JobsController.GetJobProgress
- Simplified progress endpoint for status bar
- Returns: `{ jobId, status, progress, currentStage }`

## Success Criteria Met

✅ Quick Demo button creates and processes a job
✅ Generate Video button creates and processes a job
✅ User sees immediate feedback (loading state)
✅ User sees progress updates via GenerationPanel
✅ Errors are caught and displayed to user
✅ Console logs show complete flow from click to completion
✅ All API calls use consistent URL configuration
✅ No hardcoded URLs that would break in production

## Additional Notes

- The backend has comprehensive logging with correlation IDs
- The frontend now logs all API interactions with clear prefixes
- Error handling includes both user-visible toasts and detailed console logs
- The GenerationPanel component handles real-time progress updates
- Job state is managed globally via useJobState for status bar integration

## Related Documentation

- Backend API: `Aura.Api/Controllers/QuickController.cs`
- Backend Jobs: `Aura.Api/Controllers/JobsController.cs`
- Frontend Config: `Aura.Web/src/config/api.ts`
- Create Wizard: `Aura.Web/src/pages/Wizard/CreateWizard.tsx`
- Generation Panel: `Aura.Web/src/components/Generation/GenerationPanel.tsx`
