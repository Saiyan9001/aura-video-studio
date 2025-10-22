# Visual Verification Guide

## Quick Demo and Generate Video Button Fix

This guide shows what users should expect after the emergency fix has been applied.

## Before the Fix

### Symptoms
- ❌ Click "Quick Demo" button → No response
- ❌ Click "Generate Video" button → No response  
- ❌ No error messages shown to user
- ❌ No console logs to help diagnose
- ❌ Button appears to work (shows loading state briefly) but nothing happens
- ❌ No job is created in the backend
- ❌ Silent failure - user left confused

### Root Cause
Port mismatch between frontend proxy (5272) and backend API (5005) caused all API requests to fail silently.

## After the Fix

### What Users Will See

#### 1. Quick Demo Button (Step 1 of Wizard)

**Location**: Create Wizard → Step 1 (Brief) → Bottom of page

**Expected Behavior**:
1. Click "Quick Demo (Safe)" button
2. Button changes to show "Starting..." with spinner
3. Console shows detailed logging:
   ```
   [QUICK DEMO] Button clicked
   [QUICK DEMO] Current settings: { topic: "" }
   [QUICK DEMO] Starting quick demo generation...
   [QUICK DEMO] Calling validation endpoint...
   [QUICK DEMO] Validation response status: 200
   [QUICK DEMO] Calling quick demo endpoint...
   [QUICK DEMO] API response status: 200
   [QUICK DEMO] API response data: { jobId: "abc-123", ... }
   [QUICK DEMO] Job created successfully: abc-123
   ```
4. Success toast notification appears: "Quick Demo Started - Job ID: abc-123"
5. Generation Panel slides in from the right showing progress
6. Status bar at bottom shows progress percentage
7. After 10-30 seconds, video completes and success notification appears

**Visual Elements**:
- ✅ Button with spinning icon during generation
- ✅ Green success toast notification
- ✅ Generation panel with progress bars
- ✅ Stage indicators (Script → Voice → Visuals → Compose → Render)
- ✅ Console logs with [QUICK DEMO] prefix

#### 2. Generate Video Button (Step 3 of Wizard)

**Location**: Create Wizard → Step 3 (Review) → Bottom right

**Prerequisites**:
1. Fill in topic (required field in Step 1)
2. Navigate to Step 3
3. Run preflight check (green checkmark)

**Expected Behavior**:
1. Click "Generate Video" button
2. Button changes to show "Generating..." with play icon
3. Console shows detailed logging:
   ```
   [GENERATE VIDEO] Button clicked
   [GENERATE VIDEO] Current settings: { brief: {...}, planSpec: {...} }
   [GENERATE VIDEO] Starting video generation...
   [GENERATE VIDEO] Normalized brief: {...}
   [GENERATE VIDEO] Request data: {...}
   [GENERATE VIDEO] Calling API endpoint /api/jobs...
   [GENERATE VIDEO] API response status: 200
   [GENERATE VIDEO] Job created successfully: xyz-789
   ```
4. Generation Panel slides in from the right showing progress
5. Status bar at bottom shows progress percentage
6. Real-time updates every 2 seconds showing stage progression
7. After 1-5 minutes (depending on video length), video completes

**Visual Elements**:
- ✅ Button disabled during generation
- ✅ Generation panel with live progress updates
- ✅ Stage progression visualization
- ✅ ETA countdown (when available)
- ✅ Console logs with [GENERATE VIDEO] prefix
- ✅ Success notification when complete with "Open folder" button

#### 3. Error Handling (If Something Goes Wrong)

**Backend Not Running**:
- ❌ Error toast: "Failed to Start Quick Demo"
- ❌ Console error with full details
- ❌ Message suggests checking if backend is running

**Validation Failure**:
- ⚠️ Warning toast: "Validation Failed"
- ⚠️ Lists specific issues (e.g., "Topic too short", "Invalid duration")
- ⚠️ Generation does not proceed

**Network Error**:
- ❌ Error toast: "Failed to Start Generation"
- ❌ Console shows network error details
- ❌ Retry option available in error notification

## Console Logging Examples

### Successful Quick Demo Flow
```
[QUICK DEMO] Button clicked
[QUICK DEMO] Current settings: { topic: "" }
[QUICK DEMO] Starting quick demo generation...
[QUICK DEMO] Calling validation endpoint...
[QUICK DEMO] Validation response status: 200
[QUICK DEMO] Validation data: { isValid: true }
[QUICK DEMO] Calling quick demo endpoint...
[QUICK DEMO] Request data: { topic: null }
[QUICK DEMO] API response status: 200
[QUICK DEMO] API response data: {
  jobId: "550e8400-e29b-41d4-a716-446655440000",
  status: "queued",
  message: "Quick demo job created successfully"
}
[QUICK DEMO] Job created successfully: 550e8400-e29b-41d4-a716-446655440000
[QUICK DEMO] Resetting generating state
```

### Successful Generate Video Flow
```
[GENERATE VIDEO] Button clicked
[GENERATE VIDEO] Current settings: {
  brief: { topic: "AI Revolution", audience: "General", ... },
  planSpec: { targetDurationMinutes: 3, pacing: "Conversational", ... }
}
[GENERATE VIDEO] Starting video generation...
[GENERATE VIDEO] Normalized brief: { ... }
[GENERATE VIDEO] Normalized planSpec: { ... }
[GENERATE VIDEO] Request data: {
  brief: { ... },
  planSpec: { targetDuration: "PT3M", ... },
  voiceSpec: { ... },
  renderSpec: { ... }
}
[GENERATE VIDEO] Calling API endpoint /api/jobs...
[GENERATE VIDEO] API response status: 200
[GENERATE VIDEO] API response data: {
  jobId: "660e8400-e29b-41d4-a716-446655440001",
  status: "Running",
  stage: "Script"
}
[GENERATE VIDEO] Job created successfully: 660e8400-e29b-41d4-a716-446655440001
[GENERATE VIDEO] Resetting generating state
```

### Error Flow (Backend Not Running)
```
[QUICK DEMO] Button clicked
[QUICK DEMO] Current settings: { topic: "" }
[QUICK DEMO] Starting quick demo generation...
[QUICK DEMO] Calling validation endpoint...
[QUICK DEMO] Exception caught: TypeError: Failed to fetch
[QUICK DEMO] Error details: {
  message: "Failed to fetch",
  stack: "TypeError: Failed to fetch\n    at handleQuickDemo..."
}
[QUICK DEMO] Parsed error info: {
  title: "Network Error",
  message: "Failed to connect to API. Please ensure the backend is running.",
  errorDetails: "Failed to fetch"
}
[QUICK DEMO] Resetting generating state
```

## Testing Checklist

### Prerequisites
- [ ] Backend running on port 5005 (`cd Aura.Api && dotnet run`)
- [ ] Frontend running on port 5173 (`cd Aura.Web && npm run dev`)
- [ ] Browser console open (F12 → Console tab)

### Quick Demo Button Tests
- [ ] Click button → Console shows [QUICK DEMO] logs
- [ ] Button shows "Starting..." state
- [ ] Success toast appears
- [ ] Generation panel opens
- [ ] Progress updates show in real-time
- [ ] Job completes successfully OR shows clear error

### Generate Video Button Tests
- [ ] Fill in topic field
- [ ] Navigate to Step 3
- [ ] Run preflight check
- [ ] Click "Generate Video" → Console shows [GENERATE VIDEO] logs
- [ ] Button shows "Generating..." state
- [ ] Generation panel opens
- [ ] Progress updates show in real-time
- [ ] Job completes successfully OR shows clear error

### Error Handling Tests
- [ ] Stop backend → Click Quick Demo → See clear error message
- [ ] Invalid topic → See validation error
- [ ] Network timeout → See retry option in error

## Success Criteria

✅ Every button click produces console output
✅ Every API call is logged with request and response
✅ Errors show both in console AND in UI (toast notification)
✅ Loading states are visible to user
✅ Progress updates work in real-time
✅ Jobs complete successfully and show output files
✅ No silent failures

## Screenshots

### Quick Demo Button Location
The "Quick Demo (Safe)" button appears at the bottom of Step 1 in the Create Wizard, below the main form fields. It has a prominent blue color (primary appearance) and includes a play icon.

### Generate Video Button Location  
The "Generate Video" button appears at the bottom right of Step 3 in the Create Wizard, after the preflight check has been run and passed (or overridden).

### Generation Panel
The Generation Panel slides in from the right side of the screen when a job starts. It shows:
- Job ID
- Current stage (Script, Voice, Visuals, Compose, Render)
- Progress percentage
- ETA (when available)
- Stage icons (checkmarks for completed, spinner for in-progress, numbers for pending)
- Log viewer (expandable)
- Output files list (when complete)

### Console Output
The browser console (F12) shows all API interactions with clear prefixes:
- `[QUICK DEMO]` for Quick Demo button actions
- `[GENERATE VIDEO]` for Generate Video button actions
- Full request/response data logged
- Error stack traces when things fail

## Next Steps After Verification

If everything works as expected:
1. ✅ Mark the issue as resolved
2. ✅ Update user documentation with console logging tips
3. ✅ Consider adding telemetry for production monitoring

If issues persist:
1. Check that both frontend and backend are running
2. Verify ports are correct (5173 for frontend, 5005 for backend)
3. Review console logs for specific error messages
4. Check CORS configuration if seeing CORS errors
5. Verify all dependencies are installed (`npm install` and `dotnet restore`)
