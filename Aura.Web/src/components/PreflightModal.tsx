import { useState } from 'react';
import {
  Dialog,
  DialogTrigger,
  DialogSurface,
  DialogTitle,
  DialogBody,
  DialogActions,
  DialogContent,
  Button,
  makeStyles,
  tokens,
  Text,
  Spinner,
} from '@fluentui/react-components';
import {
  CheckmarkCircle20Filled,
  DismissCircle20Filled,
  Warning20Filled,
} from '@fluentui/react-icons';
import type { PreflightResult } from '../types';

const useStyles = makeStyles({
  checkItem: {
    display: 'flex',
    alignItems: 'flex-start',
    gap: tokens.spacingHorizontalM,
    padding: tokens.spacingVerticalS,
    borderBottom: `1px solid ${tokens.colorNeutralStroke2}`,
    ':last-child': {
      borderBottom: 'none',
    },
  },
  checkIcon: {
    marginTop: tokens.spacingVerticalXXS,
  },
  checkContent: {
    flex: 1,
    display: 'flex',
    flexDirection: 'column',
    gap: tokens.spacingVerticalXXS,
  },
  fixButton: {
    marginTop: tokens.spacingVerticalXS,
  },
  summary: {
    display: 'flex',
    alignItems: 'center',
    gap: tokens.spacingHorizontalS,
    padding: tokens.spacingVerticalM,
    marginBottom: tokens.spacingVerticalM,
    borderRadius: tokens.borderRadiusMedium,
  },
  summarySuccess: {
    backgroundColor: tokens.colorPaletteGreenBackground2,
  },
  summaryError: {
    backgroundColor: tokens.colorPaletteRedBackground2,
  },
  checksContainer: {
    maxHeight: '400px',
    overflowY: 'auto',
  },
});

interface PreflightModalProps {
  open: boolean;
  onOpenChange: (open: boolean) => void;
  onAutoSwitchToFreePath?: () => void;
}

export function PreflightModal({ open, onOpenChange, onAutoSwitchToFreePath }: PreflightModalProps) {
  const styles = useStyles();
  const [result, setResult] = useState<PreflightResult | null>(null);
  const [loading, setLoading] = useState(false);

  const runPreflight = async () => {
    setLoading(true);
    try {
      const response = await fetch('/api/preflight/run', { method: 'POST' });
      if (response.ok) {
        const data = await response.json();
        setResult(data);
      } else {
        console.error('Failed to run preflight checks');
      }
    } catch (error) {
      console.error('Error running preflight checks:', error);
    } finally {
      setLoading(false);
    }
  };

  const handleOpenChange = (newOpen: boolean) => {
    if (newOpen) {
      runPreflight();
    } else {
      setResult(null);
    }
    onOpenChange(newOpen);
  };

  const handleFixClick = (check: PreflightResult['checks'][0]) => {
    if (check.link) {
      if (check.link.startsWith('http')) {
        window.open(check.link, '_blank');
      } else {
        window.location.href = check.link;
      }
    }
  };

  const canAutoSwitchToFreePath = result && !result.ok && onAutoSwitchToFreePath;

  return (
    <Dialog open={open} onOpenChange={(_, data) => handleOpenChange(data.open)}>
      <DialogSurface>
        <DialogBody>
          <DialogTitle>Preflight Configuration Check</DialogTitle>
          <DialogContent>
            {loading && (
              <div style={{ display: 'flex', alignItems: 'center', gap: tokens.spacingHorizontalM, padding: tokens.spacingVerticalXL }}>
                <Spinner size="small" />
                <Text>Running preflight checks...</Text>
              </div>
            )}

            {result && !loading && (
              <>
                <div className={`${styles.summary} ${result.ok ? styles.summarySuccess : styles.summaryError}`}>
                  {result.ok ? (
                    <>
                      <CheckmarkCircle20Filled style={{ color: tokens.colorPaletteGreenForeground1 }} />
                      <Text weight="semibold">All checks passed! Configuration is valid.</Text>
                    </>
                  ) : (
                    <>
                      <DismissCircle20Filled style={{ color: tokens.colorPaletteRedForeground1 }} />
                      <Text weight="semibold">Some checks failed. Please review and fix issues below.</Text>
                    </>
                  )}
                </div>

                <div className={styles.checksContainer}>
                  {result.checks.map((check, index) => (
                    <div key={index} className={styles.checkItem}>
                      <div className={styles.checkIcon}>
                        {check.ok ? (
                          <CheckmarkCircle20Filled style={{ color: tokens.colorPaletteGreenForeground1 }} />
                        ) : (
                          <Warning20Filled style={{ color: tokens.colorPaletteRedForeground1 }} />
                        )}
                      </div>
                      <div className={styles.checkContent}>
                        <Text weight="semibold">{check.name}</Text>
                        <Text size={200}>{check.message}</Text>
                        {!check.ok && check.fixHint && (
                          <>
                            <Text size={200} style={{ color: tokens.colorNeutralForeground3 }}>
                              💡 {check.fixHint}
                            </Text>
                            {check.link && (
                              <Button
                                size="small"
                                appearance="subtle"
                                className={styles.fixButton}
                                onClick={() => handleFixClick(check)}
                              >
                                Fix This
                              </Button>
                            )}
                          </>
                        )}
                      </div>
                    </div>
                  ))}
                </div>
              </>
            )}
          </DialogContent>
          <DialogActions>
            {canAutoSwitchToFreePath && (
              <Button
                appearance="primary"
                onClick={() => {
                  onAutoSwitchToFreePath!();
                  handleOpenChange(false);
                }}
              >
                Auto-switch to Free Path
              </Button>
            )}
            <Button appearance={result?.ok ? 'primary' : 'secondary'} onClick={() => handleOpenChange(false)}>
              {result?.ok ? 'Continue' : 'Close'}
            </Button>
          </DialogActions>
        </DialogBody>
      </DialogSurface>
    </Dialog>
  );
}
