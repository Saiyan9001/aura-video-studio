import {
  makeStyles,
  tokens,
  Button,
  Toast as FluentToast,
  ToastTitle,
  ToastBody,
  ToastFooter,
  useToastController,
  useId,
} from '@fluentui/react-components';
import {
  CheckmarkCircle24Regular,
  ErrorCircle24Regular,
  Warning24Regular,
  Info24Regular,
  Open24Regular,
} from '@fluentui/react-icons';
import { useEffect } from 'react';
import { useActivity } from '../../state/activityContext';

const useStyles = makeStyles({
  toastFooter: {
    display: 'flex',
    gap: tokens.spacingHorizontalS,
    marginTop: tokens.spacingVerticalS,
  },
});

export type ToastIntent = 'success' | 'error' | 'warning' | 'info';

export interface ToastOptions {
  intent: ToastIntent;
  title: string;
  message?: string;
  duration?: number; // in milliseconds, -1 for no auto-dismiss
  action?: {
    label: string;
    onClick: () => void;
  };
  secondaryAction?: {
    label: string;
    onClick: () => void;
  };
}

function getToastIcon(intent: ToastIntent) {
  switch (intent) {
    case 'success':
      return <CheckmarkCircle24Regular style={{ color: tokens.colorPaletteGreenForeground1 }} />;
    case 'error':
      return <ErrorCircle24Regular style={{ color: tokens.colorPaletteRedForeground1 }} />;
    case 'warning':
      return <Warning24Regular style={{ color: tokens.colorPaletteYellowForeground1 }} />;
    case 'info':
      return <Info24Regular style={{ color: tokens.colorBrandForeground1 }} />;
  }
}

/**
 * Hook to display toast notifications
 */
export function useToastNotifications() {
  const toasterId = useId('toast-notifications');
  const { dispatchToast } = useToastController(toasterId);
  const styles = useStyles();

  const showToast = (options: ToastOptions) => {
    const { intent, title, message, duration = 5000, action, secondaryAction } = options;

    dispatchToast(
      <FluentToast>
        <ToastTitle action={getToastIcon(intent)}>
          {title}
        </ToastTitle>
        {message && (
          <ToastBody>
            {message}
          </ToastBody>
        )}
        {(action || secondaryAction) && (
          <ToastFooter className={styles.toastFooter}>
            {action && (
              <Button
                size="small"
                appearance="primary"
                icon={<Open24Regular />}
                onClick={action.onClick}
              >
                {action.label}
              </Button>
            )}
            {secondaryAction && (
              <Button
                size="small"
                appearance="subtle"
                onClick={secondaryAction.onClick}
              >
                {secondaryAction.label}
              </Button>
            )}
          </ToastFooter>
        )}
      </FluentToast>,
      { 
        intent, 
        timeout: duration,
      }
    );
  };

  const showSuccessToast = (title: string, message?: string, action?: ToastOptions['action']) => {
    showToast({ intent: 'success', title, message, action });
  };

  const showErrorToast = (title: string, message?: string, action?: ToastOptions['action']) => {
    showToast({ intent: 'error', title, message, duration: -1, action });
  };

  const showWarningToast = (title: string, message?: string, action?: ToastOptions['action']) => {
    showToast({ intent: 'warning', title, message, action });
  };

  const showInfoToast = (title: string, message?: string, action?: ToastOptions['action']) => {
    showToast({ intent: 'info', title, message, action });
  };

  return {
    showToast,
    showSuccessToast,
    showErrorToast,
    showWarningToast,
    showInfoToast,
    toasterId,
  };
}

/**
 * Activity watcher that shows toast notifications for completed operations
 */
export function ActivityToastWatcher() {
  const { activities } = useActivity();
  const { showSuccessToast, showErrorToast } = useToastNotifications();

  useEffect(() => {
    // Watch for activities that just completed or failed
    const recentActivities = activities.filter(activity => {
      if (!activity.endTime) return false;
      
      // Only show toasts for activities that ended in the last 2 seconds
      const timeSinceEnd = Date.now() - activity.endTime.getTime();
      return timeSinceEnd < 2000;
    });

    recentActivities.forEach(activity => {
      if (activity.status === 'completed') {
        const duration = activity.endTime 
          ? Math.floor((activity.endTime.getTime() - activity.startTime.getTime()) / 1000)
          : 0;
        
        showSuccessToast(
          `${activity.title} completed`,
          `Completed successfully in ${duration}s`,
          activity.metadata?.outputPath
            ? {
                label: 'Open File',
                onClick: () => {
                  console.log('Opening file:', activity.metadata?.outputPath);
                },
              }
            : undefined
        );
      } else if (activity.status === 'failed') {
        showErrorToast(
          `${activity.title} failed`,
          activity.error || 'Operation failed',
          activity.canRetry
            ? {
                label: 'Retry',
                onClick: () => {
                  console.log('Retrying activity:', activity.id);
                },
              }
            : undefined
        );
      }
    });
  }, [activities, showSuccessToast, showErrorToast]);

  return null;
}
