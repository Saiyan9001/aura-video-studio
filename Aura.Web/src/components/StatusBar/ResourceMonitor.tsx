import {
  makeStyles,
  tokens,
  Text,
  ProgressBar,
  Tooltip,
} from '@fluentui/react-components';
import {
  Warning24Regular,
  Info24Regular,
} from '@fluentui/react-icons';
import { useActivity } from '../../state/activityContext';

const useStyles = makeStyles({
  container: {
    padding: '12px 16px',
    backgroundColor: tokens.colorNeutralBackground2,
    borderRadius: tokens.borderRadiusMedium,
    border: `1px solid ${tokens.colorNeutralStroke1}`,
  },
  header: {
    display: 'flex',
    alignItems: 'center',
    justifyContent: 'space-between',
    marginBottom: '12px',
  },
  metrics: {
    display: 'grid',
    gridTemplateColumns: 'repeat(auto-fit, minmax(200px, 1fr))',
    gap: '12px',
  },
  metric: {
    display: 'flex',
    flexDirection: 'column',
    gap: '4px',
  },
  metricHeader: {
    display: 'flex',
    alignItems: 'center',
    justifyContent: 'space-between',
  },
  metricLabel: {
    fontSize: tokens.fontSizeBase200,
    fontWeight: tokens.fontWeightSemibold,
  },
  metricValue: {
    fontSize: tokens.fontSizeBase200,
    color: tokens.colorNeutralForeground3,
  },
  warning: {
    color: tokens.colorPaletteYellowForeground1,
  },
  warningBanner: {
    marginTop: '12px',
    padding: '8px 12px',
    backgroundColor: tokens.colorPaletteYellowBackground1,
    borderRadius: tokens.borderRadiusSmall,
    display: 'flex',
    alignItems: 'flex-start',
    gap: '8px',
  },
  warningText: {
    fontSize: tokens.fontSizeBase200,
    color: tokens.colorPaletteYellowForeground1,
  },
});

function getUsageColor(percentage: number): 'brand' | 'success' | 'warning' | 'error' {
  if (percentage >= 90) return 'error';
  if (percentage >= 75) return 'warning';
  return 'brand';
}

function getUsageWarning(cpu: number, memory: number, gpu: number | undefined): string | null {
  if (cpu >= 95 || memory >= 95 || (gpu !== undefined && gpu >= 95)) {
    return 'System resources are critically high. Consider closing other applications or reducing concurrent operations.';
  }
  if (cpu >= 85 || memory >= 85 || (gpu !== undefined && gpu >= 85)) {
    return 'System resources are running high. Performance may be affected.';
  }
  return null;
}

export function ResourceMonitor() {
  const styles = useStyles();
  const { resourceUsage, activeActivities } = useActivity();

  if (!resourceUsage) {
    return null;
  }

  const warning = getUsageWarning(resourceUsage.cpu, resourceUsage.memory, resourceUsage.gpu);
  const hasActiveOperations = activeActivities.length > 0;

  return (
    <div className={styles.container}>
      <div className={styles.header}>
        <Text weight="semibold">Resource Usage</Text>
        {hasActiveOperations && (
          <Tooltip content="Resources are being monitored during active operations" relationship="label">
            <Info24Regular style={{ color: tokens.colorNeutralForeground3 }} />
          </Tooltip>
        )}
      </div>

      <div className={styles.metrics}>
        <div className={styles.metric}>
          <div className={styles.metricHeader}>
            <Text className={styles.metricLabel}>CPU</Text>
            <Text className={styles.metricValue}>
              {resourceUsage.cpu.toFixed(1)}%
              {resourceUsage.cpu >= 85 && (
                <Warning24Regular className={styles.warning} style={{ marginLeft: '4px' }} />
              )}
            </Text>
          </div>
          <ProgressBar 
            value={resourceUsage.cpu / 100}
            color={getUsageColor(resourceUsage.cpu)}
          />
        </div>

        <div className={styles.metric}>
          <div className={styles.metricHeader}>
            <Text className={styles.metricLabel}>Memory</Text>
            <Text className={styles.metricValue}>
              {resourceUsage.memory.toFixed(1)}%
              {resourceUsage.memory >= 85 && (
                <Warning24Regular className={styles.warning} style={{ marginLeft: '4px' }} />
              )}
            </Text>
          </div>
          <ProgressBar 
            value={resourceUsage.memory / 100}
            color={getUsageColor(resourceUsage.memory)}
          />
        </div>

        {resourceUsage.gpu !== undefined && (
          <div className={styles.metric}>
            <div className={styles.metricHeader}>
              <Text className={styles.metricLabel}>GPU</Text>
              <Text className={styles.metricValue}>
                {resourceUsage.gpu.toFixed(1)}%
                {resourceUsage.gpu >= 85 && (
                  <Warning24Regular className={styles.warning} style={{ marginLeft: '4px' }} />
                )}
              </Text>
            </div>
            <ProgressBar 
              value={resourceUsage.gpu / 100}
              color={getUsageColor(resourceUsage.gpu)}
            />
          </div>
        )}

        {resourceUsage.diskIO !== undefined && (
          <div className={styles.metric}>
            <div className={styles.metricHeader}>
              <Text className={styles.metricLabel}>Disk I/O</Text>
              <Text className={styles.metricValue}>
                {resourceUsage.diskIO.toFixed(1)} MB/s
              </Text>
            </div>
          </div>
        )}
      </div>

      {warning && (
        <div className={styles.warningBanner}>
          <Warning24Regular style={{ color: tokens.colorPaletteYellowForeground1, flexShrink: 0 }} />
          <Text className={styles.warningText}>{warning}</Text>
        </div>
      )}
    </div>
  );
}
