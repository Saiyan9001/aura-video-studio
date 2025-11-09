# Toast Notification Enhancement Implementation

## Overview

This document describes the implementation of enhanced toast notifications with auto-dismiss functionality, visible close buttons, and improved user experience features.

## Features Implemented

### 1. Visible Close Button (X Icon)

**Location**: Every toast footer  
**Appearance**: Transparent button with Dismiss (X) icon  
**Accessibility**: Includes `aria-label="Dismiss notification"`  
**Behavior**: Immediately dismisses the toast when clicked

```tsx
<Button
  size="small"
  appearance="transparent"
  icon={<Dismiss24Regular />}
  onClick={handleDismiss}
  className={styles.closeButton}
  aria-label="Dismiss notification"
/>
```

### 2. Auto-Dismiss Timer

**Default**: 5 seconds (5000ms)  
**Configurable**: Pass `timeout` option in ms  
**Visual Indicator**: Progress bar shows remaining time  
**Behavior**: Automatically dismisses toast when timer reaches zero

```tsx
showSuccessToast({
  title: 'Success',
  message: 'Operation completed',
  timeout: 8000, // Custom 8-second timeout
});
```

### 3. Determinate Progress Bar

**Location**: Bottom of toast, below footer  
**Height**: 3px (increased from 2px for better visibility)  
**Color**: Brand color (`tokens.colorBrandBackground`)  
**Animation**: Smooth CSS transition every 100ms  
**Behavior**: Starts at 100% width, decreases to 0% over timeout duration

```css
.progressBar {
  height: '3px',
  backgroundColor: tokens.colorNeutralBackground3,
  borderRadius: '1px',
  overflow: 'hidden',
  marginTop: tokens.spacingVerticalXS,
}

.progressFill {
  height: '100%',
  backgroundColor: tokens.colorBrandBackground,
  transition: 'width 100ms linear',
}
```

### 4. Pause on Hover

**Trigger**: Mouse enters toast area  
**Behavior**: Progress bar freezes, timer pauses  
**Resume**: When mouse leaves toast area  
**Purpose**: Gives users time to read or interact with toast

Implementation uses `onMouseEnter` and `onMouseLeave` handlers with state management.

### 5. ESC Key to Dismiss

**Trigger**: User presses ESC key  
**Scope**: Global window event listener  
**Behavior**: Dismisses the active toast  
**Cleanup**: Event listener removed on component unmount

```tsx
useEffect(() => {
  const handleKeyDown = (e: KeyboardEvent) => {
    if (e.key === 'Escape') {
      onDismiss?.();
    }
  };

  window.addEventListener('keydown', handleKeyDown);
  return () => window.removeEventListener('keydown', handleKeyDown);
}, [onDismiss]);
```

### 6. View Logs Button (Error Toasts)

**Location**: Error toast footer  
**Appearance**: Subtle button with DocumentBulletList icon  
**Callback**: `onOpenLogs` prop  
**Purpose**: Direct access to error logs for debugging

```tsx
{onOpenLogs && (
  <Button
    size="small"
    appearance="subtle"
    icon={<DocumentBulletList24Regular />}
    onClick={onOpenLogs}
  >
    View Logs
  </Button>
)}
```

### 7. Action Buttons (Success Toasts)

**View Results**: Primary button to view output  
**Open Folder**: Subtle button to reveal in file explorer  
**Example**: Job completion notifications

```tsx
showSuccessToast({
  title: 'Video Generated',
  message: 'Your video is ready!',
  duration: '00:15',
  onViewResults: () => navigate('/preview'),
  onOpenFolder: () => shell.showItemInFolder(path),
});
```

## Visual Layout

### Success Toast Structure
```
┌─────────────────────────────────────────┐
│ ✓ Success Title                         │
│ Message content here                    │
│ Duration: 00:15                         │
│                                         │
│ [View Results] [Open Folder]       [X] │
│ ━━━━━━━━━━━━━━━━━━━━━━━━ (progress bar)│
└─────────────────────────────────────────┘
```

### Error Toast Structure
```
┌─────────────────────────────────────────┐
│ ⚠ Error Title                           │
│ Error message here                      │
│ Additional error details                │
│ Correlation ID: abc-123-def-456         │
│ Error Code: E500                        │
│                                         │
│ [Retry] [View Logs]                [X] │
│ ━━━━━━━━━━━━━━━━━━━━━━━━ (progress bar)│
└─────────────────────────────────────────┘
```

## Usage Examples

### Simple Success Toast
```tsx
const { showSuccessToast } = useNotifications();

showSuccessToast({
  title: 'Success!',
  message: 'Operation completed successfully.',
});
```

### Success Toast with Actions
```tsx
showSuccessToast({
  title: 'Video Generated',
  message: 'Your video has been generated successfully.',
  duration: '00:15',
  onViewResults: () => navigate('/preview'),
  onOpenFolder: () => shell.showItemInFolder(outputPath),
});
```

### Simple Error Toast
```tsx
const { showFailureToast } = useNotifications();

showFailureToast({
  title: 'Error',
  message: 'Something went wrong.',
});
```

### Error Toast with Full Details
```tsx
showFailureToast({
  title: 'Generation Failed',
  message: 'Failed to generate video.',
  errorDetails: 'FFmpeg process exited with code 1.',
  correlationId: 'abc-123-def-456',
  errorCode: 'E500',
  onRetry: () => retryGeneration(),
  onOpenLogs: () => openLogsFolder(),
  timeout: 8000, // 8 seconds instead of default 5
});
```

### Custom Timeout
```tsx
showSuccessToast({
  title: 'Important Notice',
  message: 'Please read this carefully.',
  timeout: 10000, // 10 seconds
});

// Disable auto-dismiss (not recommended for production)
showSuccessToast({
  title: 'Manual Dismiss Only',
  message: 'This toast will not auto-dismiss.',
  timeout: 0, // No auto-dismiss
});
```

## API Reference

### SuccessToastOptions

| Property | Type | Required | Default | Description |
|----------|------|----------|---------|-------------|
| `title` | `string` | ✅ | - | Toast title |
| `message` | `string` | ✅ | - | Main message content |
| `duration` | `string` | ❌ | - | Duration label (e.g., "00:15") |
| `onViewResults` | `() => void` | ❌ | - | Callback for "View Results" button |
| `onOpenFolder` | `() => void` | ❌ | - | Callback for "Open Folder" button |
| `timeout` | `number` | ❌ | `5000` | Auto-dismiss timeout in ms |

### FailureToastOptions

| Property | Type | Required | Default | Description |
|----------|------|----------|---------|-------------|
| `title` | `string` | ✅ | - | Toast title |
| `message` | `string` | ✅ | - | Main error message |
| `errorDetails` | `string` | ❌ | - | Additional error details |
| `correlationId` | `string` | ❌ | - | Correlation ID for tracing |
| `errorCode` | `string` | ❌ | - | Error code (e.g., "E500") |
| `onRetry` | `() => void` | ❌ | - | Callback for "Retry" button |
| `onOpenLogs` | `() => void` | ❌ | - | Callback for "View Logs" button |
| `timeout` | `number` | ❌ | `5000` | Auto-dismiss timeout in ms |

## Backward Compatibility

All existing toast usages continue to work without modification. The new features are additive:

- **Close button**: Always visible, no breaking changes
- **Auto-dismiss**: Default 5s, can be disabled with `timeout: 0`
- **Progress bar**: Always visible when timeout > 0
- **View Logs**: Only shown when `onOpenLogs` provided
- **Action buttons**: Only shown when callbacks provided

## Accessibility

1. **Close Button**: Has `aria-label="Dismiss notification"` for screen readers
2. **Keyboard Support**: ESC key dismisses toasts
3. **Visual Progress**: Progress bar provides visual feedback
4. **Hover Pause**: Gives users time to read/interact
5. **Icons**: All buttons have descriptive icons

## Testing

Tests are located in `Aura.Web/src/test/toasts-error-ux.test.tsx`:

- ✅ Toast hook returns functions
- ✅ Success toast returns toast ID
- ✅ Error toast returns toast ID
- ✅ Error toast accepts correlation ID and error code
- ✅ Toasts work without optional callbacks
- ✅ Custom timeout configuration works

Run tests:
```bash
npm test -- src/test/toasts-error-ux.test.tsx
```

## Implementation Details

### Timer Logic

The timer uses a combination of `setInterval` and state management:

1. **Progress calculation**: Decreases by `(interval / remainingTime) * 100` every 100ms
2. **Pause state**: When `isPaused` is true, progress updates are skipped
3. **Resume logic**: Tracks elapsed time to calculate remaining time
4. **Cleanup**: Timer cleared on component unmount

### Toast ID Generation

Toast IDs are generated using timestamps for uniqueness:
```tsx
const toastId = `toast-success-${Date.now()}`;
```

This ensures each toast has a unique identifier for programmatic dismissal.

### FluentUI Integration

The implementation uses FluentUI's toast controller:
- `useToastController(TOASTER_ID)` - Hook to dispatch and dismiss toasts
- `dispatchToast(content, options)` - Displays a toast
- `dismissToast(toastId)` - Removes a specific toast
- `toastId` option - Specifies unique identifier for toast

## Files Modified

1. **Aura.Web/src/components/Notifications/Toasts.tsx** - Main implementation
2. **Aura.Web/src/test/toasts-error-ux.test.tsx** - Updated tests

## Code Quality

- ✅ Zero placeholders (enforced by pre-commit hooks)
- ✅ TypeScript strict mode (no `any` types)
- ✅ ESLint passing (no warnings)
- ✅ Prettier formatting applied
- ✅ All tests passing (6/6)

## Performance Considerations

- Progress bar updates every 100ms (smooth but not excessive)
- Timer cleanup prevents memory leaks
- Event listeners properly removed on unmount
- CSS transitions used for smooth animations

## Future Enhancements (Optional)

1. **Toast sounds**: Audio feedback for accessibility
2. **Position options**: top-left, bottom-right, etc.
3. **Stacking limits**: Prevent UI overflow with many toasts
4. **Animation**: Entrance/exit animations
5. **Toast queue**: Manage multiple simultaneous toasts
6. **Undo action**: Quick undo for destructive operations

## Migration Guide

No migration required! All existing toast usages are backward compatible.

### Optional: Add Close Button to Existing Toasts

Existing toasts automatically get the close button. No code changes needed.

### Optional: Add View Logs to Error Toasts

Add `onOpenLogs` callback to error toasts:

```tsx
// Before
showFailureToast({
  title: 'Error',
  message: 'Something went wrong',
});

// After (optional enhancement)
showFailureToast({
  title: 'Error',
  message: 'Something went wrong',
  onOpenLogs: () => openLogsFolder(), // Add this line
});
```

### Optional: Customize Timeout

Add `timeout` parameter to adjust auto-dismiss duration:

```tsx
showSuccessToast({
  title: 'Success',
  message: 'Operation completed',
  timeout: 8000, // 8 seconds instead of default 5
});
```
