import {
  Drawer,
  DrawerHeader,
  DrawerBody,
  Button,
  Text,
  ProgressBar,
  makeStyles,
  Dialog,
  DialogTrigger,
  DialogSurface,
  DialogTitle,
  DialogBody,
  DialogActions,
  DialogContent,
} from '@fluentui/react-components';
import { Dismiss24Regular, Stop20Regular } from '@fluentui/react-icons';
import { useState, useEffect } from 'react';
import { apiUrl } from '../config/api';

const useStyles = makeStyles({
  drawer: {
    width: '420px',
  },
  progressSection: {
    marginBottom: '20px',
  },
  progressText: {
    display: 'flex',
    justifyContent: 'space-between',
    marginTop: '8px',
    marginBottom: '4px',
  },
  stageText: {
    marginBottom: '8px',
    fontWeight: '600',
  },
  timeText: {
    display: 'flex',
    justifyContent: 'space-between',
    marginTop: '8px',
    fontSize: '12px',
    color: '#666',
  },
  logEntry: {
    padding: '8px',
    borderBottom: '1px solid #e0e0e0',
    fontSize: '12px',
    fontFamily: 'monospace',
    wordBreak: 'break-word',
  },
  logContainer: {
    maxHeight: '400px',
    overflowY: 'auto',
    marginTop: '8px',
  },
  actionButtons: {
    display: 'flex',
    gap: '8px',
    marginTop: '16px',
  },
});

export interface JobProgressDrawerProps {
  isOpen: boolean;
  onClose: () => void;
  jobId: string;
}

export function JobProgressDrawer({ isOpen, onClose, jobId }: JobProgressDrawerProps) {
  const styles = useStyles();
  const [progress, setProgress] = useState(0);
  const [status, setStatus] = useState('Running');
  const [stage, setStage] = useState('Initializing');
  const [logs, setLogs] = useState<string[]>([]);
  const [eta, setEta] = useState<string | null>(null);
  const [elapsed, setElapsed] = useState<string>('');
  const [isCancelling, setIsCancelling] = useState(false);
  const [showCancelDialog, setShowCancelDialog] = useState(false);

  // Format duration from ISO string or seconds
  const formatDuration = (value: string | number | null | undefined): string => {
    if (!value) return '';

    let totalSeconds = 0;
    if (typeof value === 'string') {
      // Parse ISO 8601 duration format like "PT1H2M3S" or timestamp
      if (value.startsWith('PT')) {
        // eslint-disable-next-line security/detect-unsafe-regex
        const match = value.match(/PT(?:(\d+)H)?(?:(\d+)M)?(?:(\d+(?:\.\d+)?)S)?/);
        if (match) {
          const hours = parseInt(match[1] || '0', 10);
          const minutes = parseInt(match[2] || '0', 10);
          const seconds = parseFloat(match[3] || '0');
          totalSeconds = hours * 3600 + minutes * 60 + seconds;
        }
      } else {
        // Try parsing as timestamp
        const date = new Date(value);
        if (!isNaN(date.getTime())) {
          totalSeconds = Math.floor((Date.now() - date.getTime()) / 1000);
        }
      }
    } else {
      totalSeconds = value;
    }

    if (totalSeconds <= 0) return '';

    const hours = Math.floor(totalSeconds / 3600);
    const minutes = Math.floor((totalSeconds % 3600) / 60);
    const seconds = Math.floor(totalSeconds % 60);

    if (hours > 0) {
      return `${hours}h ${minutes}m ${seconds}s`;
    } else if (minutes > 0) {
      return `${minutes}m ${seconds}s`;
    } else {
      return `${seconds}s`;
    }
  };

  useEffect(() => {
    if (!isOpen || !jobId) {
      return;
    }

    const pollProgress = async () => {
      try {
        // Fetch job progress with ETA
        const progressResponse = await fetch(apiUrl(`/api/jobs/${jobId}/progress`));
        if (progressResponse.ok) {
          const progressData = await progressResponse.json();
          setProgress(progressData.progress || 0);
          setStatus(progressData.status || 'running');
          setStage(progressData.currentStage || 'Processing');

          // Calculate elapsed time
          if (progressData.startedAt) {
            const start = new Date(progressData.startedAt);
            const elapsedMs = Date.now() - start.getTime();
            setElapsed(formatDuration(Math.floor(elapsedMs / 1000)));
          }
        }

        // Fetch full job details for logs and ETA
        const jobResponse = await fetch(apiUrl(`/api/jobs/${jobId}`));
        if (jobResponse.ok) {
          const jobData = await jobResponse.json();
          if (jobData.Logs && Array.isArray(jobData.Logs)) {
            setLogs(jobData.Logs.slice(-50)); // Show last 50 logs
          }

          // Set ETA if available
          if (jobData.Eta) {
            setEta(formatDuration(jobData.Eta));
          } else {
            setEta(null);
          }
        }
      } catch (error) {
        console.error('Error polling job progress:', error);
      }
    };

    // Poll immediately
    pollProgress();

    // Set up polling interval
    const interval = setInterval(() => {
      pollProgress();

      // Stop polling if completed or failed
      if (status === 'completed' || status === 'failed') {
        clearInterval(interval);
      }
    }, 1000);

    return () => clearInterval(interval);
  }, [isOpen, jobId, status]);

  const handleCancelJob = async () => {
    setIsCancelling(true);
    setShowCancelDialog(false);

    try {
      const response = await fetch(apiUrl(`/api/jobs/${jobId}/cancel`), {
        method: 'POST',
      });

      if (response.ok) {
        setStatus('cancelled');
      } else {
        console.error('Failed to cancel job');
      }
    } catch (error) {
      console.error('Error cancelling job:', error);
    } finally {
      setIsCancelling(false);
    }
  };

  const isRunning = status === 'running';
  const canCancel = isRunning && !isCancelling;

  return (
    <Drawer open={isOpen} position="end" className={styles.drawer}>
      <DrawerHeader>
        <Text weight="semibold">Job Progress</Text>
        <Button appearance="subtle" icon={<Dismiss24Regular />} onClick={onClose} />
      </DrawerHeader>
      <DrawerBody>
        <div className={styles.progressSection}>
          <div className={styles.stageText}>
            <Text>Stage: {stage}</Text>
          </div>
          <ProgressBar value={progress / 100} />
          <div className={styles.progressText}>
            <Text>{progress}%</Text>
            <Text weight="semibold">{status}</Text>
          </div>
          <div className={styles.timeText}>
            <Text>Elapsed: {elapsed || '-'}</Text>
            {eta && <Text>ETA: {eta}</Text>}
          </div>
        </div>

        {canCancel && (
          <div className={styles.actionButtons}>
            <Dialog
              open={showCancelDialog}
              onOpenChange={(_, data) => setShowCancelDialog(data.open)}
            >
              <DialogTrigger disableButtonEnhancement>
                <Button appearance="outline" icon={<Stop20Regular />}>
                  Cancel Job
                </Button>
              </DialogTrigger>
              <DialogSurface>
                <DialogBody>
                  <DialogTitle>Cancel Job?</DialogTitle>
                  <DialogContent>
                    <Text>
                      Are you sure you want to cancel this job? This will stop the video generation
                      and clean up temporary files. This action cannot be undone.
                    </Text>
                  </DialogContent>
                  <DialogActions>
                    <DialogTrigger disableButtonEnhancement>
                      <Button appearance="secondary">No, keep running</Button>
                    </DialogTrigger>
                    <Button appearance="primary" onClick={handleCancelJob}>
                      Yes, cancel job
                    </Button>
                  </DialogActions>
                </DialogBody>
              </DialogSurface>
            </Dialog>
          </div>
        )}

        {isCancelling && <Text>Cancelling job...</Text>}

        <Text weight="semibold">Logs:</Text>
        <div className={styles.logContainer}>
          {logs.length === 0 ? (
            <Text>No logs available yet...</Text>
          ) : (
            logs.map((log, index) => (
              <div key={index} className={styles.logEntry}>
                {log}
              </div>
            ))
          )}
        </div>
      </DrawerBody>
    </Drawer>
  );
}
