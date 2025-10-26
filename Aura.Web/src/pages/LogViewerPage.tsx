import {
  makeStyles,
  tokens,
  Title1,
  Text,
  Button,
  Card,
  Spinner,
  Select,
  Input,
  Badge,
  Body1,
  Caption1,
} from '@fluentui/react-components';
import { ArrowClockwise24Regular, Copy24Regular, Filter24Regular } from '@fluentui/react-icons';
import { useState, useEffect, useCallback } from 'react';

const useStyles = makeStyles({
  container: {
    maxWidth: '1400px',
    margin: '0 auto',
  },
  header: {
    display: 'flex',
    justifyContent: 'space-between',
    alignItems: 'center',
    marginBottom: tokens.spacingVerticalXL,
    flexWrap: 'wrap',
    gap: tokens.spacingVerticalM,
  },
  headerContent: {
    display: 'flex',
    flexDirection: 'column',
    gap: tokens.spacingVerticalS,
  },
  subtitle: {
    color: tokens.colorNeutralForeground3,
  },
  controls: {
    display: 'flex',
    gap: tokens.spacingHorizontalM,
    marginBottom: tokens.spacingVerticalL,
    flexWrap: 'wrap',
    alignItems: 'center',
  },
  logCard: {
    padding: tokens.spacingVerticalM,
    marginBottom: tokens.spacingVerticalS,
    fontFamily: 'monospace',
    fontSize: '12px',
    cursor: 'pointer',
    ':hover': {
      backgroundColor: tokens.colorNeutralBackground1Hover,
    },
  },
  logEntry: {
    display: 'flex',
    flexDirection: 'column',
    gap: tokens.spacingVerticalXS,
  },
  logHeader: {
    display: 'flex',
    justifyContent: 'space-between',
    alignItems: 'center',
    gap: tokens.spacingHorizontalM,
  },
  logMeta: {
    display: 'flex',
    gap: tokens.spacingHorizontalM,
    alignItems: 'center',
    flexWrap: 'wrap',
  },
  logMessage: {
    wordBreak: 'break-word',
    whiteSpace: 'pre-wrap',
  },
  levelBadge: {
    minWidth: '60px',
    textAlign: 'center',
  },
  emptyState: {
    textAlign: 'center',
    padding: tokens.spacingVerticalXXXL,
  },
  loadingState: {
    display: 'flex',
    justifyContent: 'center',
    padding: tokens.spacingVerticalXXXL,
  },
  stats: {
    display: 'flex',
    gap: tokens.spacingHorizontalL,
    marginBottom: tokens.spacingVerticalL,
    flexWrap: 'wrap',
  },
  statCard: {
    padding: tokens.spacingVerticalM,
    flex: '1',
    minWidth: '150px',
  },
});

interface LogEntry {
  timestamp: string;
  level: string;
  correlationId: string;
  message: string;
  rawLine: string;
}

interface LogsResponse {
  logs: LogEntry[];
  file: string;
  totalLines: number;
  message?: string;
}

export function LogViewerPage() {
  const styles = useStyles();
  const [logs, setLogs] = useState<LogEntry[]>([]);
  const [loading, setLoading] = useState(true);
  const [levelFilter, setLevelFilter] = useState<string>('');
  const [correlationIdFilter, setCorrelationIdFilter] = useState<string>('');
  const [lines, setLines] = useState<string>('500');
  const [totalLines, setTotalLines] = useState<number>(0);
  const [logFile, setLogFile] = useState<string>('');
  const [copySuccess, setCopySuccess] = useState<string>('');

  const fetchLogs = useCallback(async () => {
    setLoading(true);
    try {
      const params = new URLSearchParams();
      if (levelFilter) params.append('level', levelFilter);
      if (correlationIdFilter) params.append('correlationId', correlationIdFilter);
      if (lines) params.append('lines', lines);

      const response = await fetch(`/api/logs?${params.toString()}`);
      const data: LogsResponse = await response.json();

      setLogs(data.logs || []);
      setTotalLines(data.totalLines || 0);
      setLogFile(data.file || '');
    } catch (error) {
      console.error('Error fetching logs:', error);
    } finally {
      setLoading(false);
    }
  }, [levelFilter, correlationIdFilter, lines]);

  useEffect(() => {
    fetchLogs();
  }, [fetchLogs]);

  const handleRefresh = () => {
    fetchLogs();
  };

  const handleFilter = () => {
    fetchLogs();
  };

  const handleCopyLog = (log: LogEntry) => {
    const logDetails = {
      timestamp: log.timestamp,
      level: log.level,
      correlationId: log.correlationId,
      message: log.message,
    };

    navigator.clipboard
      .writeText(JSON.stringify(logDetails, null, 2))
      .then(() => {
        setCopySuccess(log.correlationId);
        setTimeout(() => setCopySuccess(''), 2000);
      })
      .catch((err) => console.error('Failed to copy:', err));
  };

  const getLevelBadgeColor = (level: string): 'danger' | 'warning' | 'success' | 'informative' => {
    const levelUpper = level.toUpperCase();
    if (levelUpper.includes('ERR') || levelUpper.includes('FTL')) return 'danger';
    if (levelUpper.includes('WRN') || levelUpper.includes('WAR')) return 'warning';
    if (levelUpper.includes('INF')) return 'informative';
    return 'success';
  };

  const levelCounts = logs.reduce(
    (acc, log) => {
      const level = log.level.toUpperCase();
      acc[level] = (acc[level] || 0) + 1;
      return acc;
    },
    {} as Record<string, number>
  );

  return (
    <div className={styles.container}>
      <div className={styles.header}>
        <div className={styles.headerContent}>
          <Title1>Log Viewer</Title1>
          <Text className={styles.subtitle}>
            View and filter application logs for diagnostics and debugging
          </Text>
        </div>
        <Button appearance="primary" icon={<ArrowClockwise24Regular />} onClick={handleRefresh}>
          Refresh
        </Button>
      </div>

      <div className={styles.stats}>
        <Card className={styles.statCard}>
          <Caption1>Log File</Caption1>
          <Body1>{logFile || 'No logs'}</Body1>
        </Card>
        <Card className={styles.statCard}>
          <Caption1>Total Lines</Caption1>
          <Body1>{totalLines}</Body1>
        </Card>
        <Card className={styles.statCard}>
          <Caption1>Filtered Lines</Caption1>
          <Body1>{logs.length}</Body1>
        </Card>
        {Object.entries(levelCounts).map(([level, count]) => (
          <Card key={level} className={styles.statCard}>
            <Caption1>{level}</Caption1>
            <Body1>{count}</Body1>
          </Card>
        ))}
      </div>

      <div className={styles.controls}>
        <Filter24Regular />
        <Select
          value={levelFilter}
          onChange={(_, data) => setLevelFilter(data.value)}
          style={{ minWidth: '150px' }}
        >
          <option value="">All Levels</option>
          <option value="INF">Information</option>
          <option value="WRN">Warning</option>
          <option value="ERR">Error</option>
          <option value="FTL">Fatal</option>
        </Select>
        <Input
          placeholder="Filter by Correlation ID"
          value={correlationIdFilter}
          onChange={(_, data) => setCorrelationIdFilter(data.value)}
          style={{ minWidth: '250px' }}
        />
        <Input
          placeholder="Number of lines"
          value={lines}
          onChange={(_, data) => setLines(data.value)}
          type="number"
          style={{ width: '120px' }}
        />
        <Button appearance="primary" onClick={handleFilter}>
          Apply Filters
        </Button>
      </div>

      {loading ? (
        <div className={styles.loadingState}>
          <Spinner label="Loading logs..." />
        </div>
      ) : logs.length === 0 ? (
        <div className={styles.emptyState}>
          <Text>No logs found matching your filters.</Text>
        </div>
      ) : (
        <div>
          {logs.map((log, index) => (
            <Card key={index} className={styles.logCard} onClick={() => handleCopyLog(log)}>
              <div className={styles.logEntry}>
                <div className={styles.logHeader}>
                  <div className={styles.logMeta}>
                    <Badge
                      appearance="filled"
                      color={getLevelBadgeColor(log.level)}
                      className={styles.levelBadge}
                    >
                      {log.level}
                    </Badge>
                    <Caption1>{log.timestamp}</Caption1>
                    {log.correlationId && (
                      <Badge appearance="outline" color="informative">
                        {log.correlationId}
                      </Badge>
                    )}
                  </div>
                  <div>
                    {copySuccess === log.correlationId && (
                      <Caption1 style={{ color: tokens.colorPaletteGreenForeground1 }}>
                        Copied!
                      </Caption1>
                    )}
                    <Copy24Regular />
                  </div>
                </div>
                <div className={styles.logMessage}>
                  <Body1>{log.message}</Body1>
                </div>
              </div>
            </Card>
          ))}
        </div>
      )}
    </div>
  );
}
