import {
  makeStyles,
  tokens,
  Card,
  Title3,
  Text,
  Button,
  Spinner,
  Badge,
  ProgressBar,
} from '@fluentui/react-components';
import {
  CheckmarkCircle24Filled,
  Dismiss24Regular,
  Warning24Regular,
  ArrowDownload24Regular,
  Info24Regular,
} from '@fluentui/react-icons';
import { useState, useEffect } from 'react';
import type { FC } from 'react';
import { API_BASE_URL } from '../../config/api';

const useStyles = makeStyles({
  container: {
    display: 'flex',
    flexDirection: 'column',
    gap: tokens.spacingVerticalL,
  },
  statusCard: {
    padding: tokens.spacingVerticalL,
  },
  statusHeader: {
    display: 'flex',
    alignItems: 'center',
    gap: tokens.spacingHorizontalM,
    marginBottom: tokens.spacingVerticalM,
  },
  statusDetails: {
    display: 'flex',
    flexDirection: 'column',
    gap: tokens.spacingVerticalS,
    marginLeft: '36px',
  },
  hardwareList: {
    display: 'flex',
    flexWrap: 'wrap',
    gap: tokens.spacingHorizontalS,
    marginTop: tokens.spacingVerticalS,
  },
  actionButtons: {
    display: 'flex',
    gap: tokens.spacingHorizontalS,
    marginTop: tokens.spacingVerticalM,
  },
  infoBox: {
    padding: tokens.spacingVerticalM,
    backgroundColor: tokens.colorNeutralBackground2,
    borderRadius: tokens.borderRadiusMedium,
    display: 'flex',
    gap: tokens.spacingHorizontalS,
    alignItems: 'flex-start',
  },
});

interface FFmpegStatus {
  installed: boolean;
  valid: boolean;
  version?: string;
  path?: string;
  source: string;
  error?: string;
  versionMeetsRequirement: boolean;
  minimumVersion: string;
  hardwareAcceleration: {
    nvencSupported: boolean;
    amfSupported: boolean;
    quickSyncSupported: boolean;
    videoToolboxSupported: boolean;
    availableEncoders: string[];
  };
}

interface FFmpegSetupProps {
  onStatusChange?: (installed: boolean) => void;
}

export const FFmpegSetup: FC<FFmpegSetupProps> = ({ onStatusChange }) => {
  const styles = useStyles();
  const [status, setStatus] = useState<FFmpegStatus | null>(null);
  const [loading, setLoading] = useState(true);
  const [installing, setInstalling] = useState(false);
  const [installProgress, setInstallProgress] = useState(0);

  const checkStatus = async () => {
    try {
      setLoading(true);
      const response = await fetch(`${API_BASE_URL}/api/system/ffmpeg/status`);

      if (!response.ok) {
        // Handle HTTP errors with detailed messages
        const errorText = await response.text();
        let errorMessage = `HTTP ${response.status}: ${response.statusText}`;

        try {
          const errorData = JSON.parse(errorText);
          errorMessage = errorData.message || errorData.error || errorMessage;
        } catch {
          // If not JSON, use the text response or default message
          errorMessage = errorText || errorMessage;
        }

        console.error('FFmpeg status check failed:', errorMessage);

        // Set a minimal status object with error information
        setStatus({
          installed: false,
          valid: false,
          version: undefined,
          path: undefined,
          source: 'None',
          error: `Unable to check FFmpeg status: ${errorMessage}`,
          versionMeetsRequirement: false,
          minimumVersion: '4.0',
          hardwareAcceleration: {
            nvencSupported: false,
            amfSupported: false,
            quickSyncSupported: false,
            videoToolboxSupported: false,
            availableEncoders: [],
          },
        });
        onStatusChange?.(false);
        return;
      }

      const data = await response.json();
      setStatus(data);
      onStatusChange?.(data.installed && data.valid);
    } catch (error: unknown) {
      const errorObj = error instanceof Error ? error : new Error(String(error));
      console.error('Failed to check FFmpeg status:', errorObj.message);

      // Set error status on network or parse errors
      setStatus({
        installed: false,
        valid: false,
        version: undefined,
        path: undefined,
        source: 'None',
        error: `Network error: ${errorObj.message}`,
        versionMeetsRequirement: false,
        minimumVersion: '4.0',
        hardwareAcceleration: {
          nvencSupported: false,
          amfSupported: false,
          quickSyncSupported: false,
          videoToolboxSupported: false,
          availableEncoders: [],
        },
      });
      onStatusChange?.(false);
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    checkStatus();
    // Only run once on mount
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, []);

  const handleInstall = async () => {
    try {
      setInstalling(true);
      setInstallProgress(0);

      const response = await fetch(`${API_BASE_URL}/api/ffmpeg/install`, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ version: 'latest' }),
      });

      if (!response.ok) {
        throw new Error('Installation failed');
      }

      await response.json();
      setInstallProgress(100);

      setTimeout(async () => {
        await checkStatus();
        setInstalling(false);
        setInstallProgress(0);
      }, 1000);
    } catch (error: unknown) {
      const errorObj = error instanceof Error ? error : new Error(String(error));
      console.error('Failed to install FFmpeg:', errorObj.message);
      setInstalling(false);
      setInstallProgress(0);
    }
  };

  if (loading) {
    return (
      <div className={styles.container}>
        <Spinner label="Checking FFmpeg status..." />
      </div>
    );
  }

  if (!status) {
    return (
      <div className={styles.container}>
        <Card className={styles.statusCard}>
          <div className={styles.statusHeader}>
            <Warning24Regular style={{ color: tokens.colorPaletteYellowForeground2 }} />
            <Title3>FFmpeg Status Unknown</Title3>
          </div>
          <Text>Unable to check FFmpeg status. Please try again.</Text>
          <div className={styles.actionButtons}>
            <Button appearance="primary" onClick={checkStatus}>
              Retry
            </Button>
          </div>
        </Card>
      </div>
    );
  }

  const hasHardwareAcceleration =
    status.hardwareAcceleration.nvencSupported ||
    status.hardwareAcceleration.amfSupported ||
    status.hardwareAcceleration.quickSyncSupported ||
    status.hardwareAcceleration.videoToolboxSupported;

  return (
    <div className={styles.container}>
      <Card className={styles.statusCard}>
        <div className={styles.statusHeader}>
          {status.installed && status.valid ? (
            <CheckmarkCircle24Filled style={{ color: tokens.colorPaletteGreenForeground1 }} />
          ) : (
            <Dismiss24Regular style={{ color: tokens.colorPaletteRedForeground1 }} />
          )}
          <Title3>FFmpeg Status</Title3>
        </div>

        <div className={styles.statusDetails}>
          <Text>
            <strong>Installation:</strong>{' '}
            {status.installed && status.valid ? (
              <Badge appearance="filled" color="success">
                Installed
              </Badge>
            ) : (
              <Badge appearance="filled" color="danger">
                Not Installed
              </Badge>
            )}
          </Text>

          {status.version && (
            <Text>
              <strong>Version:</strong> {status.version}
              {status.versionMeetsRequirement ? (
                <Badge
                  appearance="outline"
                  color="success"
                  style={{ marginLeft: tokens.spacingHorizontalS }}
                >
                  ✓ {status.minimumVersion}+
                </Badge>
              ) : (
                <Badge
                  appearance="outline"
                  color="warning"
                  style={{ marginLeft: tokens.spacingHorizontalS }}
                >
                  Requires {status.minimumVersion}+
                </Badge>
              )}
            </Text>
          )}

          {status.path && (
            <Text>
              <strong>Location:</strong> {status.path}
            </Text>
          )}

          {status.source && (
            <Text>
              <strong>Source:</strong>{' '}
              <Badge appearance="outline">
                {status.source === 'Managed'
                  ? 'Managed Installation'
                  : status.source === 'PATH'
                    ? 'System PATH'
                    : status.source === 'Configured'
                      ? 'User Configured'
                      : status.source}
              </Badge>
            </Text>
          )}

          {status.error && (
            <Text style={{ color: tokens.colorPaletteRedForeground1 }}>
              <strong>Error:</strong> {status.error}
            </Text>
          )}
        </div>

        {status.installed && status.valid && (
          <>
            <div className={styles.statusDetails} style={{ marginTop: tokens.spacingVerticalM }}>
              <Text weight="semibold">Hardware Acceleration:</Text>
              {hasHardwareAcceleration ? (
                <div className={styles.hardwareList}>
                  {status.hardwareAcceleration.nvencSupported && (
                    <Badge appearance="filled" color="brand">
                      NVIDIA NVENC
                    </Badge>
                  )}
                  {status.hardwareAcceleration.amfSupported && (
                    <Badge appearance="filled" color="danger">
                      AMD AMF
                    </Badge>
                  )}
                  {status.hardwareAcceleration.quickSyncSupported && (
                    <Badge appearance="filled" color="informative">
                      Intel QuickSync
                    </Badge>
                  )}
                  {status.hardwareAcceleration.videoToolboxSupported && (
                    <Badge appearance="filled" color="success">
                      VideoToolbox
                    </Badge>
                  )}
                </div>
              ) : (
                <Text size={200}>
                  <Badge appearance="outline">CPU Only (Slower)</Badge>
                  <span style={{ marginLeft: tokens.spacingHorizontalS }}>
                    Hardware acceleration not available
                  </span>
                </Text>
              )}
            </div>

            {hasHardwareAcceleration && (
              <div className={styles.infoBox} style={{ marginTop: tokens.spacingVerticalM }}>
                <Info24Regular />
                <Text size={200}>
                  Hardware acceleration detected! Video rendering will be 5-10x faster with GPU
                  encoding.
                </Text>
              </div>
            )}
          </>
        )}

        {!status.installed && (
          <div className={styles.actionButtons}>
            <Button
              appearance="primary"
              icon={<ArrowDownload24Regular />}
              onClick={handleInstall}
              disabled={installing}
            >
              {installing ? 'Installing...' : 'Install FFmpeg'}
            </Button>
            <Button appearance="secondary" onClick={checkStatus} disabled={installing}>
              Refresh Status
            </Button>
          </div>
        )}

        {installing && (
          <div style={{ marginTop: tokens.spacingVerticalM }}>
            <ProgressBar value={installProgress} max={100} />
            <Text size={200} style={{ marginTop: tokens.spacingVerticalXS }}>
              Downloading and installing FFmpeg...
            </Text>
          </div>
        )}
      </Card>

      {!status.installed && (
        <div className={styles.infoBox}>
          <Info24Regular />
          <div>
            <Text weight="semibold">About FFmpeg</Text>
            <Text size={200}>
              FFmpeg is required for video rendering and must be version 4.0 or higher. The
              installer will download and configure FFmpeg automatically for Windows users. For
              other platforms, please install FFmpeg manually and ensure it&apos;s available in your
              PATH.
            </Text>
          </div>
        </div>
      )}
    </div>
  );
};

export default FFmpegSetup;
