import {
  makeStyles,
  tokens,
  Text,
  Button,
} from '@fluentui/react-components';
import {
  CheckmarkCircle24Regular,
  ErrorCircle24Regular,
  ChevronDown24Regular,
  ChevronUp24Regular,
  ArrowClockwise24Regular,
  Clock24Regular,
} from '@fluentui/react-icons';
import { useActivity } from '../../state/activityContext';
import { useState } from 'react';

const useStyles = makeStyles({
  container: {
    display: 'flex',
    flexDirection: 'column',
    gap: '12px',
  },
  header: {
    display: 'flex',
    alignItems: 'center',
    justifyContent: 'space-between',
    marginBottom: '8px',
  },
  historyItem: {
    padding: '12px',
    backgroundColor: tokens.colorNeutralBackground1,
    borderRadius: tokens.borderRadiusMedium,
    border: `1px solid ${tokens.colorNeutralStroke1}`,
    transition: 'all 0.2s ease',
    ':hover': {
      boxShadow: tokens.shadow2,
    },
  },
  itemHeader: {
    display: 'flex',
    alignItems: 'center',
    justifyContent: 'space-between',
    marginBottom: '8px',
  },
  itemTitle: {
    display: 'flex',
    alignItems: 'center',
    gap: '8px',
  },
  itemActions: {
    display: 'flex',
    alignItems: 'center',
    gap: '4px',
  },
  itemDetails: {
    marginTop: '8px',
    padding: '8px',
    backgroundColor: tokens.colorNeutralBackground2,
    borderRadius: tokens.borderRadiusSmall,
    fontSize: tokens.fontSizeBase200,
  },
  detailRow: {
    display: 'flex',
    justifyContent: 'space-between',
    marginBottom: '4px',
  },
  errorMessage: {
    marginTop: '8px',
    padding: '8px',
    backgroundColor: tokens.colorPaletteRedBackground1,
    borderRadius: tokens.borderRadiusSmall,
    color: tokens.colorPaletteRedForeground1,
    fontSize: tokens.fontSizeBase200,
  },
  successBorder: {
    borderLeft: `4px solid ${tokens.colorPaletteGreenBackground2}`,
  },
  failedBorder: {
    borderLeft: `4px solid ${tokens.colorPaletteRedBackground2}`,
  },
  emptyState: {
    padding: '40px 20px',
    textAlign: 'center',
    color: tokens.colorNeutralForeground3,
  },
});

function formatDuration(seconds: number): string {
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

export function OperationHistory() {
  const styles = useStyles();
  const { operationHistory, clearOperationHistory } = useActivity();
  const [expandedItems, setExpandedItems] = useState<Set<string>>(new Set());

  const toggleExpanded = (id: string) => {
    const newExpanded = new Set(expandedItems);
    if (newExpanded.has(id)) {
      newExpanded.delete(id);
    } else {
      newExpanded.add(id);
    }
    setExpandedItems(newExpanded);
  };

  if (operationHistory.length === 0) {
    return (
      <div className={styles.emptyState}>
        <Text>No operation history available</Text>
      </div>
    );
  }

  return (
    <div className={styles.container}>
      <div className={styles.header}>
        <Text weight="semibold">Last {operationHistory.length} operations</Text>
        <Button
          appearance="subtle"
          size="small"
          onClick={clearOperationHistory}
        >
          Clear History
        </Button>
      </div>

      {operationHistory.map((entry) => {
        const isExpanded = expandedItems.has(entry.id);
        
        return (
          <div 
            key={entry.id} 
            className={`${styles.historyItem} ${entry.status === 'completed' ? styles.successBorder : styles.failedBorder}`}
          >
            <div className={styles.itemHeader}>
              <div className={styles.itemTitle}>
                {entry.status === 'completed' ? (
                  <CheckmarkCircle24Regular style={{ color: tokens.colorPaletteGreenForeground1 }} />
                ) : (
                  <ErrorCircle24Regular style={{ color: tokens.colorPaletteRedForeground1 }} />
                )}
                <div>
                  <Text weight="semibold">{entry.title}</Text>
                  <div style={{ display: 'flex', alignItems: 'center', gap: '8px', marginTop: '4px' }}>
                    <Clock24Regular style={{ fontSize: '14px', color: tokens.colorNeutralForeground3 }} />
                    <Text size={200} style={{ color: tokens.colorNeutralForeground3 }}>
                      {entry.endTime.toLocaleString()}
                    </Text>
                    <Text size={200} style={{ color: tokens.colorNeutralForeground3 }}>
                      • Duration: {formatDuration(entry.duration)}
                    </Text>
                  </div>
                </div>
              </div>
              <div className={styles.itemActions}>
                {entry.canRetry && entry.status === 'failed' && (
                  <Button
                    appearance="subtle"
                    size="small"
                    icon={<ArrowClockwise24Regular />}
                    onClick={() => {
                      // In a real implementation, this would retry the operation
                      console.log('Retry operation:', entry.activityId);
                    }}
                  >
                    Retry
                  </Button>
                )}
                <Button
                  appearance="subtle"
                  size="small"
                  icon={isExpanded ? <ChevronUp24Regular /> : <ChevronDown24Regular />}
                  onClick={() => toggleExpanded(entry.id)}
                  aria-label={isExpanded ? "Hide details" : "View details"}
                />
              </div>
            </div>

            {entry.error && (
              <div className={styles.errorMessage}>
                <Text size={200}>{entry.error}</Text>
              </div>
            )}

            {isExpanded && (
              <div className={styles.itemDetails}>
                <div className={styles.detailRow}>
                  <Text>Category:</Text>
                  <Text weight="semibold">{entry.category}</Text>
                </div>
                <div className={styles.detailRow}>
                  <Text>Status:</Text>
                  <Text weight="semibold">{entry.status}</Text>
                </div>
                <div className={styles.detailRow}>
                  <Text>Started:</Text>
                  <Text weight="semibold">{entry.startTime.toLocaleString()}</Text>
                </div>
                <div className={styles.detailRow}>
                  <Text>Completed:</Text>
                  <Text weight="semibold">{entry.endTime.toLocaleString()}</Text>
                </div>
                <div className={styles.detailRow}>
                  <Text>Duration:</Text>
                  <Text weight="semibold">{formatDuration(entry.duration)}</Text>
                </div>
                <div className={styles.detailRow}>
                  <Text>Activity ID:</Text>
                  <Text weight="semibold" style={{ fontFamily: 'monospace', fontSize: '11px' }}>
                    {entry.activityId}
                  </Text>
                </div>
              </div>
            )}
          </div>
        );
      })}
    </div>
  );
}
