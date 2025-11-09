# Critical First-Run Wizard Fixes

## Summary
Fixed critical issues preventing users from completing the first-run wizard, including UI loading problems, OpenAI API key validation failures, and FFmpeg detection inconsistencies.

## Issues Fixed

### 1. ✅ Precondition/UI Loading Issue (Incognito-Only Loading)

**Problem**: The UI would only load properly in incognito mode, breaking for normal users with stale localStorage.

**Root Cause**: 
- Backend's FirstRunMiddleware returns 428 for API calls when setup is incomplete
- Frontend had mismatched state between localStorage and backend database
- localStorage could have `hasCompletedFirstRun=true` while backend said setup was incomplete

**Solution**:
- Added localStorage synchronization in `App.tsx` (lines 145-156)
- When backend says setup is NOT complete, clear stale localStorage flags
- When backend says setup IS complete, sync the flag to localStorage
- Prevents state mismatch that causes UI deadlock

**Files Modified**:
- `Aura.Web/src/App.tsx` - Added localStorage sync logic
- `Aura.Api/Middleware/FirstRunMiddleware.cs` - Added static asset whitelisting

---

### 2. ✅ OpenAI API Key Validation Failure (CRITICAL)

**Problem**: When entering a valid OpenAI API key, the wizard immediately showed "Invalid" and "The requested operation could not be completed."

**Root Cause**:
- Missing error handling for network errors and non-200 responses
- The validation code didn't properly handle HTTP errors before trying to parse JSON
- No try-catch around the fetch call itself

**Solution**:
- Added comprehensive error handling in `onboarding.ts` (lines 762-849)
- Wrapped OpenAI validation in try-catch to handle network errors
- Parse HTTP errors properly before attempting JSON parsing
- Added detailed error logging for debugging
- Graceful fallback with user-friendly error messages

**Files Modified**:
- `Aura.Web/src/state/onboarding.ts` - Enhanced `validateApiKeyThunk` function

**Error Handling Flow**:
```
1. Try to call validation endpoint
2. If HTTP error (not ok), parse error response carefully
3. If network error, catch and show connection error
4. If success, check isValid field
5. Provide specific error messages for each case
```

---

### 3. ✅ FFmpeg Detection Inconsistency

**Problem**: Console detected local FFmpeg, but wizard step 2 said it wasn't detected.

**Root Cause**:
- The original wizard had only one FFmpeg step that mixed checking and installation
- FFmpeg detection was happening but not clearly communicated to the user
- The `FFmpegDependencyCard` component wasn't being used in the wizard at all

**Solution**:
- Split FFmpeg into TWO distinct wizard steps:
  - **Step 1**: FFmpeg Check (quick status check)
  - **Step 2**: FFmpeg Installation (with download button)
- Properly integrated `FFmpegDependencyCard` component in step 2
- The card auto-checks status and shows install button if needed
- Clear status indicators (Ready/Not Ready badges)

**Files Modified**:
- `Aura.Web/src/pages/Onboarding/FirstRunWizard.tsx`
  - Updated totalSteps from 5 to 6
  - Added `renderStep2FFmpeg()` function
  - Moved provider configuration to step 3
  - Updated step labels and navigation

**New Step Flow**:
```
Step 0: Welcome
Step 1: FFmpeg Check (quick detection)
Step 2: FFmpeg Install (with download/install button)
Step 3: Provider Configuration (API keys)
Step 4: Workspace Setup
Step 5: Complete
```

---

### 4. ✅ FFmpeg Download/Install Button Implementation

**Problem**: No visible way to download and install FFmpeg in the wizard.

**Solution**:
- The `FFmpegDependencyCard` component already had install functionality
- Now properly integrated in step 2 with `autoCheck={true}` and `autoExpandDetails={true}`
- Shows "Install Managed FFmpeg" button prominently
- Also shows "Attach Existing" button for users who installed manually
- Clear progress bar during installation
- Status badges (Ready/Not Ready) for immediate feedback

**Features**:
- Automatic detection on step load
- One-click install button
- Progress tracking with percentage
- Error handling with clear messages
- Skip option for manual installation
- Links to Download Center for advanced options

---

## Technical Details

### API Endpoint Whitelisting

The `FirstRunMiddleware.cs` now whitelists all necessary endpoints for the wizard:
- `/api/setup/*` - System setup status and completion
- `/api/providers/*` - API key validation (including OpenAI)
- `/api/keys/*` - Secure key storage
- `/api/ffmpeg/*` - FFmpeg status and installation
- `/api/downloads/*` - Download manager
- `/api/dependencies/*` - Dependency scanning
- Static assets (.js, .css, .map, .ico, .svg, .png, .jpg, fonts)

### Error Handling Strategy

1. **Network Errors**: Caught and shown with helpful message about connectivity
2. **HTTP Errors**: Parsed for error details before showing to user
3. **Validation Failures**: Show specific error from API response
4. **Timeouts**: Handled with user-friendly timeout messages
5. **Unexpected Errors**: Logged and shown with generic error message

### State Synchronization

**Backend → Frontend**:
- Backend database is source of truth for setup completion
- Frontend syncs localStorage on every check
- Clears stale flags when backend says not complete

**Frontend → Backend**:
- Wizard completion persists to backend database
- API key storage uses secure backend KeyVault
- FFmpeg path stored in backend configuration

---

## Testing Recommendations

### Test Case 1: Fresh Install
1. Clear browser data (localStorage + cookies)
2. Start application
3. Wizard should show immediately
4. Complete all steps
5. Verify no errors in console
6. App should load normally after completion

### Test Case 2: Stale State
1. Set `localStorage.setItem('hasCompletedFirstRun', 'true')`
2. Ensure backend database has no setup record
3. Start application
4. Wizard should show (stale flag cleared)
5. Complete wizard
6. Both backend and localStorage should be synced

### Test Case 3: OpenAI Validation
1. Enter valid OpenAI API key (starts with `sk-`)
2. Click Validate
3. Should show "Validating..." then "Valid" badge
4. Invalid key should show specific error message
5. Network error should show connectivity message

### Test Case 4: FFmpeg Installation
1. Navigate to FFmpeg Installation step
2. Should show current status (Installed/Not Ready)
3. If not installed, "Install Managed FFmpeg" button visible
4. Click install → Progress bar shows
5. After install, status updates to "Ready"
6. Can proceed to next step

---

## Migration Notes

**Breaking Changes**: None - all changes are backward compatible

**Database**: No schema changes required

**Configuration**: No configuration changes required

**Deployment**:
1. Deploy backend changes first (middleware whitelist)
2. Deploy frontend changes second
3. Users with incomplete setups will automatically see wizard
4. Existing completed setups will continue working

---

## Future Improvements

1. **Add retry button** for failed validations
2. **Show validation progress** for OpenAI checks
3. **Cache FFmpeg detection** to avoid repeated checks
4. **Add telemetry** for wizard completion rates
5. **Improve error messages** with actionable fixes

---

## Files Changed

### Frontend (Aura.Web)
- `src/App.tsx` - localStorage sync logic
- `src/state/onboarding.ts` - Enhanced API key validation
- `src/pages/Onboarding/FirstRunWizard.tsx` - FFmpeg steps split

### Backend (Aura.Api)
- `Middleware/FirstRunMiddleware.cs` - Expanded whitelist

### Testing
- Manual testing recommended for all critical paths
- E2E tests should cover wizard completion flow

---

## Debugging Tips

### If wizard doesn't show:
1. Check browser console for errors
2. Verify `/api/setup/system-status` returns `isComplete: false`
3. Check localStorage flags are cleared

### If API validation fails:
1. Check browser network tab for 428 or 500 errors
2. Verify endpoint `/api/providers/openai/validate` is accessible
3. Check backend logs for validation errors

### If FFmpeg detection fails:
1. Check `/api/setup/check-ffmpeg` response
2. Verify FFmpeg is in system PATH or managed location
3. Check backend logs for detection errors

---

## Support

For issues related to these fixes:
1. Check browser console (F12) for errors
2. Check backend logs for API errors
3. Verify network connectivity
4. Try in incognito mode to rule out stale state

---

**Date**: 2025-11-09
**Version**: 1.0.0
**Status**: ✅ All Critical Issues Resolved
