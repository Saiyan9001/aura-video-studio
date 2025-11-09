import {
  makeStyles,
  tokens,
  Card,
  Title3,
  Text,
  Button,
  Badge,
  Spinner,
} from '@fluentui/react-components';
import {
  Video24Regular,
  Checkmark24Regular,
  Dismiss24Regular,
  ArrowDownload24Regular,
  ArrowSync24Regular,
} from '@fluentui/react-icons';
import { useState, useEffect } from 'react';
import { useNotifications } from '../Notifications/Toasts';

const useStyles = makeStyles({
  card: {
    padding: tokens.spacingVerticalXL,
    marginBottom: tokens.spacingVerticalL,
  },
  header: {
    display: 'flex',
    alignItems: 'center',
    gap: tokens.spacingHorizontalM,
    marginBottom: tokens.spacingVerticalM,
  },
  icon: {
    fontSize: '24px',
  },
  content: {
    display: 'flex',
    flexDirection: 'column',
    gap: tokens.spacingVerticalM,
  },
  statusRow: {
    display: 'flex',
    alignItems: 'center',
    gap: tokens.spacingHorizontalS,
  },
  actions: {
    display: 'flex',
    gap: tokens.spacingHorizontalM,
    marginTop: tokens.spacingVerticalM,
  },
  details: {
    fontFamily: 'monospace',
    fontSize: '12px',
    padding: tokens.spacingVerticalS,
    backgroundColor: tokens.colorNeutralBackground3,
    borderRadius: tokens.borderRadiusSmall,
    marginTop: tokens.spacingVerticalS,
  },
});

interface FFmpegStatus {
  installed: boolean;
  version?: string;
  path?: string;
  source?: string;
  valid: boolean;
  error?: string;
}

export function FFmpegStatusCard() {
  const styles = useStyles();
  const { showSuccessToast, showFailureToast } = useNotifications();
  const [status, setStatus] = useState<FFmpegStatus | null>(null);
  const [loading, setLoading] = useState(true);
  const [installing, setInstalling] = useState(false);

  const fetchStatus = async () => {
    setLoading(true);
    try {
      const response = await fetch('/api/system/ffmpeg/status');
      if (response.ok) {
        const data = await response.json();
        setStatus(data);
      } else {
        setStatus({
          installed: false,
          valid: false,
          error: 'Failed to check FFmpeg status',
        });
      }
    } catch (error) {
      console.error('Error fetching FFmpeg status:', error);
      setStatus({
        installed: false,
        valid: false,
        error: 'Network error checking FFmpeg status',
      });
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    fetchStatus();
  }, []);

  const handleInstall = async () => {
    setInstalling(true);
    try {
      const response = await fetch('/api/ffmpeg/install', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ version: null }),
      });

      if (response.ok) {
        const result = await response.json();
        showSuccessToast({
          title: 'FFmpeg Installed',
          message: result.message || 'Managed FFmpeg has been installed successfully.',
        });
        // Wait a moment for the backend to finalize
        await new Promise(resolve => setTimeout(resolve, 1000));
        await fetchStatus();
      } else {
        const errorData = await response.json();
        showFailureToast({
          title: 'Installation Failed',
          message: errorData.message || errorData.detail || 'Failed to install FFmpeg. Please check logs.',
        });
      }
    } catch (error) {
      console.error('Error installing FFmpeg:', error);
      showFailureToast({
        title: 'Installation Error',
        message: 'Error installing FFmpeg. Please check network connection and try again.',
      });
    } finally {
      setInstalling(false);
    }
  };

  const handleRefresh = () => {
    fetchStatus();
  };

  if (loading) {
    return (
      <Card className={styles.card}>
        <div className={styles.header}>
          <Video24Regular className={styles.icon} />
          <Title3>FFmpeg Status</Title3>
        </div>
        <div style={{ display: 'flex', alignItems: 'center', gap: tokens.spacingHorizontalS }}>
          <Spinner size="tiny" />
          <Text>Checking FFmpeg status...</Text>
        </div>
      </Card>
    );
  }

  return (
    <Card className={styles.card}>
      <div className={styles.header}>
        <Video24Regular className={styles.icon} />
        <Title3>FFmpeg Status</Title3>
        {status?.installed && status.valid && (
          <Badge appearance="filled" color="success" icon={<Checkmark24Regular />}>
            Installed
          </Badge>
        )}
        {(!status?.installed || !status?.valid) && (
          <Badge appearance="filled" color="danger" icon={<Dismiss24Regular />}>
            Not Available
          </Badge>
        )}
      </div>

      <div className={styles.content}>
        {status?.installed && status.valid ? (
          <>
            <div className={styles.statusRow}>
              <Text weight="semibold">Source:</Text>
              <Text>{status.source || 'Unknown'}</Text>
            </div>
            {status.version && (
              <div className={styles.statusRow}>
                <Text weight="semibold">Version:</Text>
                <Text>{status.version}</Text>
              </div>
            )}
            {status.path && status.source !== 'PATH' && (
              <div className={styles.details}>
                <Text size={200}>Path: {status.path}</Text>
              </div>
            )}
            {status.source === 'PATH' && (
              <Text size={200} style={{ color: tokens.colorNeutralForeground3 }}>
                FFmpeg is available on system PATH
              </Text>
            )}
          </>
        ) : (
          <>
            <Text>FFmpeg is required for video rendering but is not currently available.</Text>
            {status?.error && (
              <Text size={200} style={{ color: tokens.colorPaletteRedForeground1 }}>
                {status.error}
              </Text>
            )}
            <Text size={200}>
              Install managed FFmpeg for automatic updates and guaranteed compatibility, or
              configure an existing installation in File Locations.
            </Text>
          </>
        )}
      </div>

      <div className={styles.actions}>
        {(!status?.installed || !status?.valid) && (
          <Button
            appearance="primary"
            icon={<ArrowDownload24Regular />}
            onClick={handleInstall}
            disabled={installing}
          >
            {installing ? 'Installing...' : 'Install Managed FFmpeg'}
          </Button>
        )}
        <Button
          appearance="secondary"
          icon={<ArrowSync24Regular />}
          onClick={handleRefresh}
          disabled={loading || installing}
        >
          Refresh Status
        </Button>
      </div>
    </Card>
  );
}
