import {
  makeStyles,
  tokens,
  Button,
  Toast,
  ToastTitle,
  ToastBody,
  ToastFooter,
  Toaster,
  useToastController,
  useId,
} from '@fluentui/react-components';
import { Copy24Regular, Dismiss24Regular } from '@fluentui/react-icons';

const useStyles = makeStyles({
  toastFooter: {
    display: 'flex',
    gap: tokens.spacingHorizontalS,
    marginTop: tokens.spacingVerticalS,
  },
  toastHeader: {
    display: 'flex',
    justifyContent: 'space-between',
    alignItems: 'flex-start',
    gap: tokens.spacingHorizontalS,
  },
  toastTitleContent: {
    flex: 1,
  },
});

export interface ErrorDetails {
  message: string;
  correlationId?: string;
  errorCode?: string;
  timestamp?: string;
  [key: string]: unknown;
}

export interface ErrorToastOptions {
  title: string;
  details: ErrorDetails;
  intent?: 'error' | 'warning' | 'info';
}

// eslint-disable-next-line react-refresh/only-export-components
export function useErrorToast() {
  const toasterId = useId('error-toaster');
  const { dispatchToast, dismissToast } = useToastController(toasterId);
  const styles = useStyles();

  const showErrorToast = (options: ErrorToastOptions) => {
    const { title, details, intent = 'error' } = options;

    const toastId = `error-toast-${Date.now()}`;

    const handleCopyDetails = () => {
      const detailsJson = JSON.stringify(
        {
          ...details,
          timestamp: details.timestamp || new Date().toISOString(),
        },
        null,
        2
      );
      navigator.clipboard.writeText(detailsJson);
    };

    const handleDismiss = () => {
      dismissToast(toastId);
    };

    dispatchToast(
      <Toast>
        <div className={styles.toastHeader}>
          <div className={styles.toastTitleContent}>
            <ToastTitle>{title}</ToastTitle>
          </div>
          <Button
            size="small"
            appearance="transparent"
            icon={<Dismiss24Regular />}
            onClick={handleDismiss}
            aria-label="Dismiss notification"
          />
        </div>
        <ToastBody>
          <div>
            <div>{details.message}</div>
            {details.correlationId && (
              <div style={{ marginTop: tokens.spacingVerticalXS, fontSize: '12px', opacity: 0.8 }}>
                Correlation ID: {details.correlationId}
              </div>
            )}
            {details.errorCode && (
              <div style={{ marginTop: tokens.spacingVerticalXS, fontSize: '12px', opacity: 0.8 }}>
                Error Code: {details.errorCode}
              </div>
            )}
          </div>
        </ToastBody>
        <ToastFooter className={styles.toastFooter}>
          <Button
            size="small"
            appearance="subtle"
            icon={<Copy24Regular />}
            onClick={handleCopyDetails}
          >
            Copy Details
          </Button>
        </ToastFooter>
      </Toast>,
      { intent, timeout: 10000, toastId }
    );
  };

  return { showErrorToast, toasterId };
}

/**
 * Error Toaster component that should be placed at the app root
 */
export function ErrorToaster({ toasterId }: { toasterId: string }) {
  return <Toaster toasterId={toasterId} position="top-end" />;
}
