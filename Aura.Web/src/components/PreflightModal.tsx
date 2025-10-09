import { useState } from 'react';
import {
  makeStyles,
  tokens,
  Dialog,
  DialogSurface,
  DialogTitle,
  DialogBody,
  DialogActions,
  DialogContent,
  Button,
  Text,
  Spinner,
} from '@fluentui/react-components';
import {
  CheckmarkCircle24Filled,
  ErrorCircle24Filled,
  Warning24Filled,
  Dismiss24Regular,
} from '@fluentui/react-icons';

const useStyles = makeStyles({
  checkList: {
    display: 'flex',
    flexDirection: 'column',
    gap: tokens.spacingVerticalM,
    marginTop: tokens.spacingVerticalL,
    marginBottom: tokens.spacingVerticalL,
  },
  checkItem: {
    display: 'flex',
    alignItems: 'flex-start',
    gap: tokens.spacingHorizontalM,
    padding: tokens.spacingVerticalS,
    borderRadius: tokens.borderRadiusMedium,
    backgroundColor: tokens.colorNeutralBackground2,
  },
  checkIcon: {
    flexShrink: 0,
    marginTop: '2px',
  },
  checkContent: {
    flex: 1,
    display: 'flex',
    flexDirection: 'column',
    gap: tokens.spacingVerticalXS,
  },
  checkActions: {
    display: 'flex',
    gap: tokens.spacingHorizontalS,
    marginTop: tokens.spacingVerticalXS,
  },
  loading: {
    display: 'flex',
    alignItems: 'center',
    justifyContent: 'center',
    padding: tokens.spacingVerticalXXL,
    gap: tokens.spacingHorizontalM,
  },
  summary: {
    padding: tokens.spacingVerticalM,
    borderRadius: tokens.borderRadiusMedium,
    marginBottom: tokens.spacingVerticalL,
  },
  summaryPass: {
    backgroundColor: tokens.colorPaletteGreenBackground1,
    color: tokens.colorPaletteGreenForeground1,
  },
  summaryFail: {
    backgroundColor: tokens.colorPaletteRedBackground1,
    color: tokens.colorPaletteRedForeground1,
  },
});

interface PreflightCheck {
  name: string;
  ok: boolean;
  message: string;
  fixHint?: string;
  link?: string;
  severity?: string;
}

interface PreflightResult {
  ok: boolean;
  correlationId: string;
  timestamp: string;
  checks: PreflightCheck[];
  canAutoSwitchToFree: boolean;
}

interface PreflightModalProps {
  open: boolean;
  onClose: () => void;
  onAutoSwitchToFree?: () => void;
}

export function PreflightModal({ open, onClose, onAutoSwitchToFree }: PreflightModalProps) {
  const styles = useStyles();
  const [loading, setLoading] = useState(false);
  const [result, setResult] = useState<PreflightResult | null>(null);

  const runPreflight = async () => {
    setLoading(true);
    setResult(null);

    try {
      const response = await fetch('/api/preflight/run', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
      });

      if (response.ok) {
        const data = await response.json();
        setResult(data);
      } else {
        console.error('Preflight check failed:', response.statusText);
      }
    } catch (error) {
      console.error('Error running preflight checks:', error);
    } finally {
      setLoading(false);
    }
  };

  const handleOpen = () => {
    if (open && !result) {
      runPreflight();
    }
  };

  // Run preflight when modal opens
  if (open && !result && !loading) {
    handleOpen();
  }

  const getCheckIcon = (check: PreflightCheck) => {
    if (check.ok) {
      return <CheckmarkCircle24Filled className={styles.checkIcon} style={{ color: tokens.colorPaletteGreenForeground1 }} />;
    }
    if (check.severity === 'warning' || check.severity === 'info') {
      return <Warning24Filled className={styles.checkIcon} style={{ color: tokens.colorPaletteYellowForeground1 }} />;
    }
    return <ErrorCircle24Filled className={styles.checkIcon} style={{ color: tokens.colorPaletteRedForeground1 }} />;
  };

  const handleFixClick = (check: PreflightCheck) => {
    if (check.link) {
      window.location.hash = check.link;
      onClose();
    }
  };

  const handleAutoSwitchToFree = () => {
    if (onAutoSwitchToFree) {
      onAutoSwitchToFree();
      onClose();
    }
  };

  return (
    <Dialog open={open} onOpenChange={(_, data) => !data.open && onClose()}>
      <DialogSurface style={{ maxWidth: '600px', minWidth: '500px' }}>
        <DialogTitle>
          Preflight Checks
          <Button
            appearance="subtle"
            icon={<Dismiss24Regular />}
            onClick={onClose}
            style={{ position: 'absolute', right: tokens.spacingHorizontalM, top: tokens.spacingVerticalM }}
          />
        </DialogTitle>
        <DialogBody>
          <DialogContent>
            {loading && (
              <div className={styles.loading}>
                <Spinner size="large" />
                <Text>Running preflight checks...</Text>
              </div>
            )}

            {result && (
              <>
                <div className={`${styles.summary} ${result.ok ? styles.summaryPass : styles.summaryFail}`}>
                  <Text weight="semibold" size={400}>
                    {result.ok ? '✓ All checks passed' : '✗ Some checks failed'}
                  </Text>
                  <Text size={200} style={{ marginTop: tokens.spacingVerticalXS }}>
                    {result.ok
                      ? 'Your system is ready to generate videos.'
                      : 'Please address the issues below before generating videos.'}
                  </Text>
                </div>

                <div className={styles.checkList}>
                  {result.checks.map((check, index) => (
                    <div key={index} className={styles.checkItem}>
                      {getCheckIcon(check)}
                      <div className={styles.checkContent}>
                        <Text weight="semibold">{check.name}</Text>
                        <Text size={200}>{check.message}</Text>
                        {!check.ok && check.fixHint && (
                          <Text size={200} style={{ color: tokens.colorNeutralForeground3 }}>
                            💡 {check.fixHint}
                          </Text>
                        )}
                        {!check.ok && (check.link || check.fixHint) && (
                          <div className={styles.checkActions}>
                            {check.link && (
                              <Button size="small" appearance="secondary" onClick={() => handleFixClick(check)}>
                                Fix
                              </Button>
                            )}
                          </div>
                        )}
                      </div>
                    </div>
                  ))}
                </div>
              </>
            )}
          </DialogContent>
        </DialogBody>
        <DialogActions>
          {result && !result.ok && result.canAutoSwitchToFree && onAutoSwitchToFree && (
            <Button appearance="primary" onClick={handleAutoSwitchToFree}>
              Auto-switch to Free Path
            </Button>
          )}
          {result && (
            <Button appearance="secondary" onClick={() => runPreflight()}>
              Run Again
            </Button>
          )}
          <Button appearance="secondary" onClick={onClose}>
            Close
          </Button>
        </DialogActions>
      </DialogSurface>
    </Dialog>
  );
}
