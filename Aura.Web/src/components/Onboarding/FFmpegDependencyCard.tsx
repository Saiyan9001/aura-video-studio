import {
  makeStyles,
  tokens,
  Title3,
  Text,
  Card,
  Button,
  Spinner,
  Badge,
  ProgressBar,
} from '@fluentui/react-components';
import {
  Checkmark24Regular,
  Warning24Regular,
  ArrowDownload24Regular,
  ArrowClockwise24Regular,
} from '@fluentui/react-icons';
import { useState, useEffect, useRef, useCallback } from 'react';
import { ffmpegClient, type FFmpegStatus } from '../../services/api/ffmpegClient';
import { useNotifications } from '../Notifications/Toasts';

const useStyles = makeStyles({
  card: {
    padding: tokens.spacingVerticalL,
    transition: 'all 0.2s ease-in-out',
    ':hover': {
      boxShadow: tokens.shadow8,
    },
  },
  header: {
    display: 'flex',
    alignItems: 'center',
    justifyContent: 'space-between',
    marginBottom: tokens.spacingVerticalM,
  },
  info: {
    display: 'flex',
    alignItems: 'center',
    gap: tokens.spacingHorizontalM,
    flex: 1,
  },
  statusIcon: {
    fontSize: '32px',
  },
  details: {
    flex: 1,
  },
  name: {
    marginBottom: tokens.spacingVerticalXS,
  },
  description: {
    color: tokens.colorNeutralForeground3,
    display: 'block',
  },
  actionsContainer: {
    display: 'flex',
    gap: tokens.spacingHorizontalS,
    alignItems: 'center',
    flexWrap: 'wrap',
  },
  detailsSection: {
    marginTop: tokens.spacingVerticalM,
    padding: tokens.spacingVerticalM,
    backgroundColor: tokens.colorNeutralBackground2,
    borderRadius: tokens.borderRadiusMedium,
  },
  detailRow: {
    display: 'flex',
    justifyContent: 'space-between',
    marginBottom: tokens.spacingVerticalXS,
  },
  progressSection: {
    marginTop: tokens.spacingVerticalM,
  },
});

export interface FFmpegDependencyCardProps {
  onInstallComplete?: (status: FFmpegStatus) => void;
  onStatusChange?: (status: FFmpegStatus | null) => void;
  autoCheck?: boolean;
  autoExpandDetails?: boolean;
  refreshSignal?: number;
}

export function FFmpegDependencyCard({
  onInstallComplete,
  onStatusChange,
  autoCheck = true,
  autoExpandDetails = false,
  refreshSignal,
}: FFmpegDependencyCardProps) {
  const styles = useStyles();
  const { showSuccessToast, showFailureToast } = useNotifications();
  const [status, setStatus] = useState<FFmpegStatus | null>(null);
  const [isLoading, setIsLoading] = useState(false);
  const [isInstalling, setIsInstalling] = useState(false);
  const [installProgress, setInstallProgress] = useState(0);
  const [error, setError] = useState<string | null>(null);
  const [showDetails, setShowDetails] = useState(autoExpandDetails);
  const lastRefreshSignal = useRef<number | undefined>(refreshSignal);

  const checkStatus = useCallback(async () => {
    setIsLoading(true);
    setError(null);
    try {
      const ffmpegStatus = await ffmpegClient.getStatus();
      setStatus(ffmpegStatus);

        if (ffmpegStatus.installed && ffmpegStatus.valid) {
          onInstallComplete?.(ffmpegStatus);
        }

        onStatusChange?.(ffmpegStatus);
    } catch (err: unknown) {
      let errorMessage = 'Failed to check FFmpeg status';

      if (err && typeof err === 'object') {
        const axiosError = err as {
          response?: {
            data?: {
              message?: string;
              detail?: string;
              error?: string;
            };
            status?: number;
          };
          message?: string;
        };

        if (axiosError.response?.data) {
          const data = axiosError.response.data;
          errorMessage = data.message || data.detail || data.error || errorMessage;

          if (axiosError.response.status === 428) {
            errorMessage = `Setup required: ${errorMessage}. Please complete the setup wizard first.`;
          }
        } else if (axiosError.message) {
          errorMessage = axiosError.message;
        }
      } else if (err instanceof Error) {
        errorMessage = err.message;
      }

        setError(errorMessage);
        onStatusChange?.(null);
    } finally {
      setIsLoading(false);
    }
  }, [onInstallComplete, onStatusChange]);

  useEffect(() => {
    if (autoCheck) {
      void checkStatus();
    }
  }, [autoCheck, checkStatus]);

  // Auto-expand details if FFmpeg is not ready and autoExpandDetails is true
  useEffect(() => {
    if (autoExpandDetails && status && (!status.installed || !status.valid)) {
      setShowDetails(true);
    }
  }, [status, autoExpandDetails]);

  useEffect(() => {
    if (refreshSignal === undefined) {
      return;
    }

    if (lastRefreshSignal.current === refreshSignal) {
      return;
    }

    lastRefreshSignal.current = refreshSignal;
    void checkStatus();
  }, [refreshSignal, checkStatus]);

  const handleInstall = async () => {
    setIsInstalling(true);
    setInstallProgress(0);
    setError(null);

    try {
      const progressInterval = setInterval(() => {
        setInstallProgress((prev) => {
          if (prev >= 90) {
            return prev;
          }
          return prev + 10;
        });
      }, 500);

      const result = await ffmpegClient.install();

      clearInterval(progressInterval);
      setInstallProgress(100);

      if (result.success) {
        showSuccessToast({
          title: 'FFmpeg Installed Successfully',
          message: result.message || 'FFmpeg has been installed and is ready to use.',
        });
        
        // Wait a moment for the backend to finalize, then check status
        await new Promise(resolve => setTimeout(resolve, 1000));
        await checkStatus();
      } else {
        throw new Error(result.message || 'Installation failed');
      }
    } catch (err: unknown) {
      let errorMessage = 'Installation failed';

      if (err && typeof err === 'object') {
        const axiosError = err as {
          response?: {
            data?: {
              message?: string;
              detail?: string;
              error?: string;
            };
            status?: number;
          };
          message?: string;
        };

        if (axiosError.response?.data) {
          const data = axiosError.response.data;
          errorMessage = data.message || data.detail || data.error || errorMessage;
        } else if (axiosError.message) {
          errorMessage = axiosError.message;
        }
      } else if (err instanceof Error) {
        errorMessage = err.message;
      }

      setError(errorMessage);
      showFailureToast({
        title: 'Installation Failed',
        message: errorMessage,
      });
    } finally {
      setIsInstalling(false);
      setInstallProgress(0);
    }
  };

  const getStatusIcon = () => {
    if (isLoading) {
      return <Spinner size="medium" className={styles.statusIcon} />;
    }

    if (!status || !status.installed || !status.valid) {
      return (
        <Warning24Regular
          className={styles.statusIcon}
          style={{ color: tokens.colorPaletteYellowForeground1 }}
        />
      );
    }

    return (
      <Checkmark24Regular
        className={styles.statusIcon}
        style={{ color: tokens.colorPaletteGreenForeground1 }}
      />
    );
  };

  const getStatusBadge = () => {
    if (isInstalling) {
      return (
        <Badge appearance="filled" color="informative">
          Installing...
        </Badge>
      );
    }

    if (isLoading) {
      return <Badge appearance="outline">Checking...</Badge>;
    }

    if (!status || !status.installed || !status.valid) {
      return (
        <Badge appearance="filled" color="warning">
          Not Ready
        </Badge>
      );
    }

    return (
      <Badge appearance="filled" color="success">
        Ready
      </Badge>
    );
  };

  const isReady = Boolean(status?.installed && status?.valid);

  return (
    <Card className={styles.card}>
      <div className={styles.header}>
        <div className={styles.info}>
          {getStatusIcon()}
          <div className={styles.details}>
            <Title3 className={styles.name}>
              FFmpeg (Video Encoding)
              <Badge
                appearance="tint"
                color="danger"
                style={{ marginLeft: tokens.spacingHorizontalS }}
              >
                Required
              </Badge>
            </Title3>
            <Text className={styles.description} size={200}>
              Essential video and audio processing toolkit. Required for all video generation.
            </Text>
          </div>
        </div>
          <div className={styles.actionsContainer}>
            {getStatusBadge()}
            {!isReady && (
              <>
                <Button
                  appearance="primary"
                  icon={<ArrowDownload24Regular />}
                  onClick={handleInstall}
                  disabled={isInstalling || isLoading}
                >
                  Install Managed FFmpeg
                </Button>
                <Button
                  appearance="secondary"
                  icon={<ArrowClockwise24Regular />}
                  onClick={checkStatus}
                  disabled={isInstalling || isLoading}
                >
                  {isLoading ? 'Scanning...' : 'Re-scan'}
                </Button>
              </>
            )}
            {isReady && (
              <Button
                appearance="secondary"
                icon={<ArrowClockwise24Regular />}
                onClick={checkStatus}
                disabled={isLoading}
              >
                {isLoading ? 'Scanning...' : 'Re-scan'}
              </Button>
            )}
          </div>
      </div>

      {isInstalling && installProgress !== undefined && (
        <div className={styles.progressSection}>
          <ProgressBar value={installProgress} max={100} />
          <Text size={200} style={{ marginTop: tokens.spacingVerticalXS }}>
            Installing... {installProgress}%
          </Text>
        </div>
      )}

      {error && (
        <div
          style={{
            marginTop: tokens.spacingVerticalM,
            padding: tokens.spacingVerticalS,
            backgroundColor: tokens.colorPaletteRedBackground1,
            borderRadius: tokens.borderRadiusSmall,
          }}
        >
          <Text
            size={200}
            style={{
              color: tokens.colorPaletteRedForeground1,
            }}
          >
            ⚠ {error}
          </Text>
        </div>
      )}

      {showDetails && status && (
        <div className={styles.detailsSection}>
          {isReady ? (
            <>
              <div className={styles.detailRow}>
                <Text weight="semibold">Version:</Text>
                <Text>{status.version || 'Unknown'}</Text>
              </div>
              <div className={styles.detailRow}>
                <Text weight="semibold">Path:</Text>
                <Text size={200}>{status.path || 'Unknown'}</Text>
              </div>
              <div className={styles.detailRow}>
                <Text weight="semibold">Source:</Text>
                <Text>{status.source}</Text>
              </div>
              {status.hardwareAcceleration.availableEncoders.length > 0 && (
                <div className={styles.detailRow}>
                  <Text weight="semibold">Hardware Acceleration:</Text>
                  <Text size={200}>
                    {status.hardwareAcceleration.nvencSupported && '✓ NVENC '}
                    {status.hardwareAcceleration.amfSupported && '✓ AMF '}
                    {status.hardwareAcceleration.quickSyncSupported && '✓ QuickSync '}
                    {!status.hardwareAcceleration.nvencSupported &&
                      !status.hardwareAcceleration.amfSupported &&
                      !status.hardwareAcceleration.quickSyncSupported &&
                      'CPU Only'}
                  </Text>
                </div>
              )}
            </>
          ) : (
            <>
              <Text
                style={{
                  color: tokens.colorNeutralForeground3,
                  marginBottom: tokens.spacingVerticalM,
                }}
              >
                FFmpeg is not installed or not ready. Install managed FFmpeg to continue.
              </Text>
              {status.error && (
                <Text
                  style={{
                    color: tokens.colorPaletteRedForeground1,
                    marginBottom: tokens.spacingVerticalM,
                    fontWeight: 'semibold',
                  }}
                >
                  Error: {status.error}
                </Text>
              )}
              <div style={{ display: 'flex', gap: tokens.spacingHorizontalS, flexWrap: 'wrap' }}>
                <Button
                  appearance="secondary"
                  onClick={() => {
                    window.open('/downloads', '_blank');
                  }}
                  disabled={isInstalling}
                >
                  Attach Existing...
                </Button>
              </div>
            </>
          )}
        </div>
      )}

      <div
        style={{
          display: 'flex',
          gap: tokens.spacingHorizontalS,
          marginTop: tokens.spacingVerticalM,
        }}
      >
        <Button
          appearance="subtle"
          size="small"
          onClick={() => setShowDetails(!showDetails)}
          disabled={isInstalling || isLoading}
        >
          {showDetails ? 'Hide Details' : 'Show Details'}
        </Button>
      </div>
    </Card>
  );
}
