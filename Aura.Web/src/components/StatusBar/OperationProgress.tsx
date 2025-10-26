import { useState, useEffect } from 'react';
import {
  makeStyles,
  tokens,
  Text,
  Button,
  ProgressBar,
  Tooltip,
  Slider,
} from '@fluentui/react-components';
import {
  ChevronDown24Regular,
  ChevronUp24Regular,
  Pause24Regular,
  Play24Regular,
  DismissCircle24Regular,
  CheckmarkCircle24Regular,
  ErrorCircle24Regular,
  ArrowDownload24Regular,
  ArrowUpload24Regular,
  Brain24Regular,
  Wand24Regular,
  Clock24Regular,
} from '@fluentui/react-icons';
import { useActivity, type Activity } from '../../state/activityContext';

const useStyles = makeStyles({
  container: {
    backgroundColor: tokens.colorNeutralBackground1,
    borderRadius: tokens.borderRadiusMedium,
    border: `1px solid ${tokens.colorNeutralStroke1}`,
    padding: '12px',
    transition: 'all 0.2s ease',
    ':hover': {
      boxShadow: tokens.shadow4,
    },
  },
  header: {
    display: 'flex',
    alignItems: 'center',
    justifyContent: 'space-between',
    gap: '8px',
    marginBottom: '8px',
  },
  titleSection: {
    display: 'flex',
    alignItems: 'center',
    gap: '8px',
    flex: 1,
  },
  categoryIcon: {
    flexShrink: 0,
  },
  actions: {
    display: 'flex',
    alignItems: 'center',
    gap: '4px',
  },
  progressSection: {
    display: 'flex',
    flexDirection: 'column',
    gap: '8px',
  },
  progressBar: {
    flex: 1,
  },
  progressStats: {
    display: 'flex',
    alignItems: 'center',
    justifyContent: 'space-between',
    fontSize: tokens.fontSizeBase200,
    color: tokens.colorNeutralForeground3,
  },
  detailsSection: {
    marginTop: '12px',
    padding: '12px',
    backgroundColor: tokens.colorNeutralBackground2,
    borderRadius: tokens.borderRadiusSmall,
    fontSize: tokens.fontSizeBase200,
  },
  detailRow: {
    display: 'flex',
    justifyContent: 'space-between',
    marginBottom: '4px',
  },
  priorityControl: {
    marginTop: '8px',
    padding: '8px',
    backgroundColor: tokens.colorNeutralBackground2,
    borderRadius: tokens.borderRadiusSmall,
  },
  errorMessage: {
    marginTop: '8px',
    padding: '8px',
    backgroundColor: tokens.colorPaletteRedBackground1,
    borderRadius: tokens.borderRadiusSmall,
    color: tokens.colorPaletteRedForeground1,
    fontSize: tokens.fontSizeBase200,
  },
  statusRunning: {
    borderLeft: `4px solid ${tokens.colorBrandBackground}`,
  },
  statusPaused: {
    borderLeft: `4px solid ${tokens.colorPaletteYellowBackground2}`,
  },
  statusCompleted: {
    borderLeft: `4px solid ${tokens.colorPaletteGreenBackground2}`,
  },
  statusFailed: {
    borderLeft: `4px solid ${tokens.colorPaletteRedBackground2}`,
  },
});

export interface OperationProgressProps {
  activity: Activity;
  showPriorityControl?: boolean;
}

function formatTime(seconds: number): string {
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

function formatSpeed(speed: number | undefined, unit: string | undefined): string {
  if (!speed || !unit) return '';
  return `${speed.toFixed(2)} ${unit}`;
}

export function OperationProgress({ activity, showPriorityControl = false }: OperationProgressProps) {
  const styles = useStyles();
  const [showDetails, setShowDetails] = useState(false);
  const [localPriority, setLocalPriority] = useState(activity.priority || 5);
  const { updateActivity, removeActivity, pauseActivity, resumeActivity, setPriority } = useActivity();

  // Sync local priority with activity priority
  useEffect(() => {
    setLocalPriority(activity.priority || 5);
  }, [activity.priority]);

  const getCategoryIcon = () => {
    switch (activity.category) {
      case 'import':
        return <ArrowDownload24Regular className={styles.categoryIcon} style={{ color: tokens.colorBrandForeground1 }} />;
      case 'export':
        return <ArrowUpload24Regular className={styles.categoryIcon} style={{ color: tokens.colorPalettePurpleForeground2 }} />;
      case 'analysis':
        return <Brain24Regular className={styles.categoryIcon} style={{ color: tokens.colorPaletteGreenForeground1 }} />;
      case 'effects':
        return <Wand24Regular className={styles.categoryIcon} style={{ color: tokens.colorPaletteDarkOrangeForeground1 }} />;
      default:
        return <Clock24Regular className={styles.categoryIcon} style={{ color: tokens.colorNeutralForeground3 }} />;
    }
  };

  const getStatusIcon = () => {
    switch (activity.status) {
      case 'completed':
        return <CheckmarkCircle24Regular style={{ color: tokens.colorPaletteGreenForeground1 }} />;
      case 'failed':
        return <ErrorCircle24Regular style={{ color: tokens.colorPaletteRedForeground1 }} />;
      case 'paused':
        return <Pause24Regular style={{ color: tokens.colorPaletteYellowForeground1 }} />;
      default:
        return null;
    }
  };

  const getContainerClass = () => {
    switch (activity.status) {
      case 'running':
        return styles.statusRunning;
      case 'paused':
        return styles.statusPaused;
      case 'completed':
        return styles.statusCompleted;
      case 'failed':
        return styles.statusFailed;
      default:
        return '';
    }
  };

  const handleCancel = () => {
    updateActivity(activity.id, { status: 'cancelled' });
  };

  const handlePause = () => {
    pauseActivity(activity.id);
  };

  const handleResume = () => {
    resumeActivity(activity.id);
  };

  const handleDismiss = () => {
    removeActivity(activity.id);
  };

  const handlePriorityChange = (_: unknown, data: { value: number }) => {
    setLocalPriority(data.value);
    setPriority(activity.id, data.value);
  };

  const progress = activity.detailedProgress || { percentage: activity.progress };

  return (
    <div className={`${styles.container} ${getContainerClass()}`}>
      <div className={styles.header}>
        <div className={styles.titleSection}>
          {getCategoryIcon()}
          <div style={{ flex: 1 }}>
            <div style={{ display: 'flex', alignItems: 'center', gap: '8px' }}>
              <Text weight="semibold">{activity.title}</Text>
              {getStatusIcon()}
            </div>
            {activity.message && (
              <Text size={200} style={{ color: tokens.colorNeutralForeground3 }}>
                {activity.message}
              </Text>
            )}
          </div>
        </div>
        <div className={styles.actions}>
          {activity.status === 'running' && activity.canPause && (
            <Tooltip content="Pause" relationship="label">
              <Button
                appearance="subtle"
                size="small"
                icon={<Pause24Regular />}
                onClick={handlePause}
                aria-label="Pause operation"
              />
            </Tooltip>
          )}
          {activity.status === 'paused' && (
            <Tooltip content="Resume" relationship="label">
              <Button
                appearance="subtle"
                size="small"
                icon={<Play24Regular />}
                onClick={handleResume}
                aria-label="Resume operation"
              />
            </Tooltip>
          )}
          {(activity.status === 'running' || activity.status === 'paused') && activity.canCancel && (
            <Tooltip content="Cancel" relationship="label">
              <Button
                appearance="subtle"
                size="small"
                icon={<DismissCircle24Regular />}
                onClick={handleCancel}
                aria-label="Cancel operation"
              />
            </Tooltip>
          )}
          {(activity.status === 'completed' || activity.status === 'failed') && (
            <Tooltip content="Dismiss" relationship="label">
              <Button
                appearance="subtle"
                size="small"
                icon={<DismissCircle24Regular />}
                onClick={handleDismiss}
                aria-label="Dismiss notification"
              />
            </Tooltip>
          )}
          <Tooltip content={showDetails ? "Hide details" : "View details"} relationship="label">
            <Button
              appearance="subtle"
              size="small"
              icon={showDetails ? <ChevronUp24Regular /> : <ChevronDown24Regular />}
              onClick={() => setShowDetails(!showDetails)}
              aria-label={showDetails ? "Hide details" : "View details"}
              aria-expanded={showDetails}
            />
          </Tooltip>
        </div>
      </div>

      {activity.status === 'running' && (
        <div className={styles.progressSection}>
          <div style={{ display: 'flex', alignItems: 'center', gap: '8px' }}>
            <ProgressBar 
              className={styles.progressBar}
              value={progress.percentage / 100} 
            />
            <Text size={200} style={{ minWidth: '45px', textAlign: 'right' }}>
              {progress.percentage.toFixed(0)}%
            </Text>
          </div>
          <div className={styles.progressStats}>
            <Text>
              {progress.currentItems !== undefined && progress.totalItems !== undefined 
                ? `${progress.currentItems} / ${progress.totalItems} items`
                : ''}
            </Text>
            <div style={{ display: 'flex', gap: '12px' }}>
              {progress.speed && (
                <Text>{formatSpeed(progress.speed, progress.speedUnit)}</Text>
              )}
              {progress.timeElapsed !== undefined && (
                <Text>Elapsed: {formatTime(progress.timeElapsed)}</Text>
              )}
              {progress.timeRemaining !== undefined && (
                <Text>Remaining: {formatTime(progress.timeRemaining)}</Text>
              )}
            </div>
          </div>
        </div>
      )}

      {activity.error && (
        <div className={styles.errorMessage}>
          <Text size={200}>{activity.error}</Text>
        </div>
      )}

      {showPriorityControl && activity.status === 'pending' && (
        <div className={styles.priorityControl}>
          <Text size={200} style={{ marginBottom: '4px' }}>
            Priority: {localPriority}
          </Text>
          <Slider
            min={1}
            max={10}
            value={localPriority}
            onChange={handlePriorityChange}
            aria-label="Operation priority"
          />
        </div>
      )}

      {showDetails && (
        <div className={styles.detailsSection}>
          <div className={styles.detailRow}>
            <Text>Category:</Text>
            <Text weight="semibold">{activity.category}</Text>
          </div>
          <div className={styles.detailRow}>
            <Text>Type:</Text>
            <Text weight="semibold">{activity.type}</Text>
          </div>
          <div className={styles.detailRow}>
            <Text>Status:</Text>
            <Text weight="semibold">{activity.status}</Text>
          </div>
          {activity.priority !== undefined && (
            <div className={styles.detailRow}>
              <Text>Priority:</Text>
              <Text weight="semibold">{activity.priority}/10</Text>
            </div>
          )}
          <div className={styles.detailRow}>
            <Text>Started:</Text>
            <Text weight="semibold">{activity.startTime.toLocaleString()}</Text>
          </div>
          {activity.endTime && (
            <div className={styles.detailRow}>
              <Text>Ended:</Text>
              <Text weight="semibold">{activity.endTime.toLocaleString()}</Text>
            </div>
          )}
          {activity.metadata && Object.keys(activity.metadata).length > 0 && (
            <>
              <Text style={{ marginTop: '8px', marginBottom: '4px' }}>Additional Details:</Text>
              {Object.entries(activity.metadata).map(([key, value]) => (
                <div key={key} className={styles.detailRow}>
                  <Text>{key}:</Text>
                  <Text weight="semibold">{String(value)}</Text>
                </div>
              ))}
            </>
          )}
        </div>
      )}
    </div>
  );
}
