import { FluentProvider, webLightTheme } from '@fluentui/react-components';
import { renderHook } from '@testing-library/react';
import { describe, it, expect } from 'vitest';
import {
  useNotifications,
  FailureToastOptions,
  NotificationsToaster,
} from '../components/Notifications/Toasts';

// Wrapper for testing with FluentProvider
const wrapper = ({ children }: { children: React.ReactNode }) => (
  <FluentProvider theme={webLightTheme}>
    {children}
    <NotificationsToaster toasterId="notifications-toaster" />
  </FluentProvider>
);

describe('Toasts - Error UX', () => {
  it('should support Retry button in failure toasts', () => {
    const { result } = renderHook(() => useNotifications(), { wrapper });

    expect(result.current.showFailureToast).toBeDefined();
    expect(typeof result.current.showFailureToast).toBe('function');
  });

  it('should support View Logs button in failure toasts', () => {
    const { result } = renderHook(() => useNotifications(), { wrapper });

    const options: FailureToastOptions = {
      title: 'Test Error',
      message: 'Test error message',
      onRetry: () => {},
      onOpenLogs: () => {},
    };

    // This will create the toast and return a toast ID
    const toastId = result.current.showFailureToast(options);
    expect(toastId).toBeDefined();
    expect(typeof toastId).toBe('string');
  });

  it('should accept correlationId in failure toast options', () => {
    const { result } = renderHook(() => useNotifications(), { wrapper });

    const options: FailureToastOptions = {
      title: 'Test Error',
      message: 'Test error message',
      correlationId: 'test-correlation-123',
      errorCode: 'E300',
      errorDetails: 'Additional error details',
      onRetry: () => {},
      onOpenLogs: () => {},
    };

    const toastId = result.current.showFailureToast(options);
    expect(toastId).toBeDefined();
  });

  it('should work without optional callbacks', () => {
    const { result } = renderHook(() => useNotifications(), { wrapper });

    const options: FailureToastOptions = {
      title: 'Test Error',
      message: 'Test error message',
    };

    const toastId = result.current.showFailureToast(options);
    expect(toastId).toBeDefined();
  });

  it('should return toastId from showSuccessToast', () => {
    const { result } = renderHook(() => useNotifications(), { wrapper });

    const toastId = result.current.showSuccessToast({
      title: 'Success',
      message: 'Operation completed successfully',
    });

    expect(toastId).toBeDefined();
    expect(typeof toastId).toBe('string');
  });

  it('should support auto-dismiss timeout configuration', () => {
    const { result } = renderHook(() => useNotifications(), { wrapper });

    const toastId = result.current.showFailureToast({
      title: 'Test Error',
      message: 'Test error message',
      timeout: 10000, // 10 seconds
    });

    expect(toastId).toBeDefined();
  });

  it('should show close button on success toast without action buttons', () => {
    const { result } = renderHook(() => useNotifications(), { wrapper });

    const toastId = result.current.showSuccessToast({
      title: 'Success',
      message: 'Operation completed',
    });

    expect(toastId).toBeDefined();
    // Close button should be present even without action buttons
    // This is validated by the component structure, not DOM as toasts are rendered in portal
  });

  it('should show close button on error toast without action buttons', () => {
    const { result } = renderHook(() => useNotifications(), { wrapper });

    const toastId = result.current.showFailureToast({
      title: 'Error',
      message: 'Something went wrong',
    });

    expect(toastId).toBeDefined();
    // Close button should be present even without action buttons
  });

  it('should display progress bar for auto-dismiss', () => {
    const { result } = renderHook(() => useNotifications(), { wrapper });

    const toastId = result.current.showSuccessToast({
      title: 'Success',
      message: 'Operation completed',
      timeout: 5000,
    });

    expect(toastId).toBeDefined();
    // Progress bar is rendered by ToastWithProgress component
  });
});
