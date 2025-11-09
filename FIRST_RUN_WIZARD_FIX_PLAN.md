# First-Run Wizard Fix Plan

## Problem Analysis

The first-run wizard has multiple issues preventing it from working properly:

### Current State
1. **Multiple wizard paths**: `FirstRunWizard.tsx` and `SetupWizard.tsx` exist separately
2. **Unclear flow**: Users are confused about which wizard to use
3. **Not prominent**: Welcome page doesn't emphasize setup requirement
4. **Feature gating inconsistent**: Some features check first-run, others check setup completion
5. **Backend endpoints**: `/api/setup/wizard/*` endpoints exist but may not be consistently used

### Key Files Identified

**Frontend:**
- `Aura.Web/src/pages/Onboarding/FirstRunWizard.tsx` - Original first-run wizard
- `Aura.Web/src/pages/Setup/SetupWizard.tsx` - Configure setup wizard  
- `Aura.Web/src/pages/WelcomePage.tsx` - Landing page
- `Aura.Web/src/services/firstRunService.ts` - First-run status management
- `Aura.Web/src/components/ConfigurationGate.tsx` - Feature gating component
- `Aura.Web/src/App.tsx` - Routing and first-run checks

**Backend:**
- `Aura.Api/Controllers/SetupController.cs` - Setup wizard endpoints
- `Aura.Api/Middleware/FirstRunMiddleware.cs` - First-run enforcement
- `Aura.Core/Data/UserSetupEntity.cs` - Database entity for setup status

## Required Fixes

### Phase 1: Consolidate Wizards
1. **Merge into single setup wizard**
   - Use `SetupWizard.tsx` as the primary wizard
   - Remove or deprecate `FirstRunWizard.tsx`
   - Ensure all setup steps are in logical order:
     - Welcome/Introduction
     - FFmpeg/Dependencies Check
     - API Key Configuration (OpenAI, etc.)
     - Workspace/Output Folders
     - Final confirmation

2. **Update routing in App.tsx**
   - Route `/setup` to the unified wizard
   - Redirect `/first-run` to `/setup`
   - Add route guard to enforce setup completion

### Phase 2: Make Setup Prominent on Welcome Page
1. **Add urgent setup banner**
   - Large, visually prominent card at top of WelcomePage
   - Use warning/info color scheme
   - Clear call-to-action: "Complete Initial Setup"
   - Explain WHY setup is required

2. **Block feature access without setup**
   - Show locked state for Create Video, Templates, etc.
   - Add tooltips explaining setup is required
   - Provide direct link to setup wizard from locked features

### Phase 3: Unify Feature Gating
1. **Create single source of truth**
   - Update `firstRunService.ts` to check setup completion
   - Rename to `setupService.ts` for clarity
   - Use `/api/setup/wizard/status` endpoint consistently

2. **Update all feature checks**
   - Search for `hasCompletedFirstRun()` calls
   - Replace with unified `hasCompletedSetup()` check
   - Update ConfigurationGate component
   - Update any locked feature UI

### Phase 4: Backend Consistency
1. **Ensure endpoints work**
   - Test `/api/setup/wizard/status` returns correct data
   - Test `/api/setup/wizard/complete` persists correctly
   - Test `/api/setup/wizard/reset` for re-running wizard

2. **Database migration**
   - Ensure UserSetupEntity table exists
   - Add migration if needed for setup completion tracking

### Phase 5: Polish UX
1. **Setup wizard improvements**
   - Add progress indicator (Step X of Y)
   - Make steps skippable where appropriate
   - Add "Save and Continue Later" option
   - Show completion percentage
   - Add animations/transitions between steps

2. **Welcome page improvements**
   - Add setup status card showing:
     - Current completion status
     - What's left to configure
     - Estimated time to complete
   - Make setup banner dismissible only after completion

3. **Error handling**
   - Clear error messages if setup fails
   - Provide recovery options
   - Add "Skip for now" on non-critical steps

## Implementation Priority

### Critical (Must Fix)
1. ✅ Consolidate wizards into single SetupWizard
2. ✅ Make setup prominent on WelcomePage with urgent banner
3. ✅ Block feature access without setup completion
4. ✅ Update all feature checks to use unified setup status

### Important (Should Fix)
5. ✅ Polish wizard UI/UX with progress indicators
6. ✅ Add "Skip for now" options where appropriate
7. ✅ Improve error messages and recovery

### Nice to Have
8. ⚪ Add setup completion percentage
9. ⚪ Add "Save and Continue Later" functionality
10. ⚪ Add onboarding tooltips/tour after setup

## Testing Checklist

### Manual Testing Required
- [ ] Fresh install - wizard appears immediately
- [ ] Cannot access features without setup
- [ ] Setup wizard completes successfully
- [ ] Setup status persists across sessions
- [ ] Reset setup works from settings
- [ ] All locked features become unlocked after setup
- [ ] Welcome page shows correct setup status
- [ ] Feature gates work correctly

### E2E Tests to Update
- [ ] Update `first-run-wizard.spec.ts` to use new setup wizard
- [ ] Update `first-run-gating.spec.ts` to use setup checks
- [ ] Add new setup wizard flow tests

## Code Changes Needed

### Minimal Changes Approach
1. Keep SetupWizard.tsx, enhance it
2. Update WelcomePage.tsx to show setup banner
3. Update firstRunService.ts (or rename to setupService.ts)
4. Update ConfigurationGate.tsx to use setup status
5. Search/replace hasCompletedFirstRun → hasCompletedSetup in all files
6. Update App.tsx routing

### Files to Modify
- `Aura.Web/src/pages/Setup/SetupWizard.tsx` - Enhance wizard
- `Aura.Web/src/pages/WelcomePage.tsx` - Add setup banner
- `Aura.Web/src/services/firstRunService.ts` - Unify status checks
- `Aura.Web/src/components/ConfigurationGate.tsx` - Update checks
- `Aura.Web/src/App.tsx` - Update routing and guards
- Multiple feature pages - Update to check setup status

## Success Criteria

✅ **Working wizard**: Setup wizard runs successfully on first launch
✅ **Prominent**: Users can't miss the setup requirement on welcome page  
✅ **Consistent**: All features consistently check setup completion
✅ **Polished**: Professional UI with clear progress and instructions
✅ **Tested**: Manual and automated tests confirm functionality

## Next Steps

1. Review current SetupWizard implementation
2. Create enhanced setup banner component for WelcomePage
3. Update routing to enforce setup
4. Update all feature checks
5. Test thoroughly
6. Update documentation
