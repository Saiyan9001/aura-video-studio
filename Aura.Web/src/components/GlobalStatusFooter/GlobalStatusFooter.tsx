import { useState } from 'react';
import {
  makeStyles,
  tokens,
  Text,
  ProgressBar,
  Badge,
} from '@fluentui/react-components';
import {
  ChevronUp24Regular,
  ChevronDown24Regular,
} from '@fluentui/react-icons';
import { useActivity } from '../../state/activityContext';
import { ActivityDrawer } from '../StatusBar/ActivityDrawer';

const useStyles = makeStyles({
  footer: {
    position: 'fixed',
    bottom: 0,
    left: 0,
    right: 0,
    backgroundColor: tokens.colorNeutralBackground2,
    borderTop: `1px solid ${tokens.colorNeutralStroke1}`,
    boxShadow: '0 -2px 8px rgba(0, 0, 0, 0.1)',
    zIndex: 999,
    transition: 'all 0.3s ease-in-out',
  },
  statusBar: {
    display: 'flex',
    alignItems: 'center',
    justifyContent: 'space-between',
    padding: '8px 20px',
    minHeight: '48px',
    cursor: 'pointer',
    ':hover': {
      backgroundColor: tokens.colorNeutralBackground3,
    },
  },
  statusLeft: {
    display: 'flex',
    alignItems: 'center',
    gap: '12px',
    flex: 1,
    minWidth: 0,
  },
  statusRight: {
    display: 'flex',
    alignItems: 'center',
    gap: '8px',
  },
  currentOperation: {
    display: 'flex',
    alignItems: 'center',
    gap: '12px',
    flex: 1,
    minWidth: 0,
  },
  operationInfo: {
    display: 'flex',
    flexDirection: 'column',
    gap: '2px',
    flex: 1,
    minWidth: 0,
  },
  operationTitle: {
    fontSize: tokens.fontSizeBase300,
    fontWeight: tokens.fontWeightSemibold,
    overflow: 'hidden',
    textOverflow: 'ellipsis',
    whiteSpace: 'nowrap',
  },
  operationDetails: {
    fontSize: tokens.fontSizeBase200,
    color: tokens.colorNeutralForeground3,
    display: 'flex',
    alignItems: 'center',
    gap: '8px',
  },
  compactProgress: {
    display: 'flex',
    alignItems: 'center',
    gap: '8px',
    minWidth: '200px',
  },
  progressBar: {
    flex: 1,
    minWidth: '120px',
  },
  badge: {
    cursor: 'pointer',
  },
  resourceIndicator: {
    display: 'flex',
    alignItems: 'center',
    gap: '4px',
    fontSize: tokens.fontSizeBase200,
    color: tokens.colorNeutralForeground3,
    padding: '4px 8px',
    borderRadius: tokens.borderRadiusSmall,
    backgroundColor: tokens.colorNeutralBackground1,
  },
  resourceWarning: {
    backgroundColor: tokens.colorPaletteYellowBackground1,
    color: tokens.colorPaletteYellowForeground1,
  },
});

function formatTime(seconds: number | undefined): string {
  if (!seconds) return '--';
  
  if (seconds < 60) {
    return `${Math.floor(seconds)}s`;
  } else if (seconds < 3600) {
    const minutes = Math.floor(seconds / 60);
    const secs = Math.floor(seconds % 60);
    return `${minutes}m ${secs}s`;
  } else {
    const hours = Math.floor(seconds / 3600);
    const minutes = Math.floor((seconds % 3600) / 60);
    return `${hours}h ${minutes}m`;
  }
}

export function GlobalStatusFooter() {
  const styles = useStyles();
  const [isDrawerExpanded, setIsDrawerExpanded] = useState(false);
  const { 
    activities, 
    activeActivities, 
    queuedActivities,
    failedActivities,
    resourceUsage,
  } = useActivity();

  // Don't render footer if there are no activities
  if (activities.length === 0) {
    return null;
  }

  // Get the current active operation (highest priority running operation)
  const currentOperation = activeActivities
    .filter(a => a.status === 'running')
    .sort((a, b) => (b.priority || 5) - (a.priority || 5))[0];

  const activeCount = activeActivities.filter(a => a.status === 'running' || a.status === 'paused').length;
  const queuedCount = queuedActivities.length;
  const failedCount = failedActivities.length;

  const getSummaryText = () => {
    const parts = [];
    if (activeCount > 0) {
      parts.push(`${activeCount} active`);
    }
    if (queuedCount > 0) {
      parts.push(`${queuedCount} queued`);
    }
    if (failedCount > 0) {
      parts.push(`${failedCount} failed`);
    }
    if (parts.length === 0) {
      parts.push('All tasks complete');
    }
    return parts.join(', ');
  };

  const progress = currentOperation?.detailedProgress || (currentOperation ? { percentage: currentOperation.progress } : null);

  // Check for resource warnings
  const hasResourceWarning = resourceUsage && (
    resourceUsage.cpu >= 85 || 
    resourceUsage.memory >= 85 || 
    (resourceUsage.gpu !== undefined && resourceUsage.gpu >= 85)
  );

  return (
    <>
      <div className={styles.footer}>
        <div 
          className={styles.statusBar}
          onClick={() => setIsDrawerExpanded(!isDrawerExpanded)}
          role="button"
          tabIndex={0}
          onKeyDown={(e) => {
            if (e.key === 'Enter' || e.key === ' ') {
              e.preventDefault();
              setIsDrawerExpanded(!isDrawerExpanded);
            }
          }}
          aria-expanded={isDrawerExpanded}
          aria-label="Activity status bar - click to expand"
        >
          <div className={styles.statusLeft}>
            {isDrawerExpanded ? <ChevronDown24Regular /> : <ChevronUp24Regular />}
            
            {currentOperation ? (
              <div className={styles.currentOperation}>
                <div className={styles.operationInfo}>
                  <div className={styles.operationTitle}>
                    {currentOperation.title}
                  </div>
                  <div className={styles.operationDetails}>
                    <span>{currentOperation.message}</span>
                    {progress?.speed && (
                      <span>• {progress.speed.toFixed(2)} {progress.speedUnit}</span>
                    )}
                    {progress?.timeRemaining && (
                      <span>• {formatTime(progress.timeRemaining)} remaining</span>
                    )}
                  </div>
                </div>
                <div className={styles.compactProgress}>
                  <ProgressBar 
                    className={styles.progressBar}
                    value={(progress?.percentage || 0) / 100}
                  />
                  <Text size={200} style={{ minWidth: '45px', textAlign: 'right' }}>
                    {(progress?.percentage || 0).toFixed(0)}%
                  </Text>
                </div>
              </div>
            ) : (
              <Text weight="semibold">{getSummaryText()}</Text>
            )}
          </div>

          <div className={styles.statusRight}>
            {hasResourceWarning && (
              <div className={`${styles.resourceIndicator} ${styles.resourceWarning}`}>
                ⚠ Resources High
              </div>
            )}
            
            {activeCount > 0 && (
              <Badge appearance="filled" color="informative" className={styles.badge}>
                {activeCount} active
              </Badge>
            )}
            {queuedCount > 0 && (
              <Badge appearance="outline" color="informative" className={styles.badge}>
                {queuedCount} queued
              </Badge>
            )}
            {failedCount > 0 && (
              <Badge appearance="filled" color="danger" className={styles.badge}>
                {failedCount} failed
              </Badge>
            )}
          </div>
        </div>
      </div>

      <ActivityDrawer 
        isExpanded={isDrawerExpanded}
        onToggle={() => setIsDrawerExpanded(!isDrawerExpanded)}
      />
    </>
  );
}
