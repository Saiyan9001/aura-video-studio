import { useState, useEffect } from 'react';
import {
  makeStyles,
  tokens,
  Title1,
  Title2,
  Text,
  Button,
  Input,
  Card,
  Field,
  Select,
  Table,
  TableHeader,
  TableRow,
  TableHeaderCell,
  TableBody,
  TableCell,
  Spinner,
  Badge,
} from '@fluentui/react-components';
import { ArrowClockwise24Regular, Copy24Regular, Dismiss24Regular } from '@fluentui/react-icons';

const useStyles = makeStyles({
  container: {
    maxWidth: '1400px',
    margin: '0 auto',
    padding: tokens.spacingVerticalXL,
  },
  header: {
    marginBottom: tokens.spacingVerticalXXL,
    display: 'flex',
    flexDirection: 'column',
    gap: tokens.spacingVerticalS,
  },
  subtitle: {
    color: tokens.colorNeutralForeground3,
  },
  filters: {
    display: 'grid',
    gridTemplateColumns: 'repeat(auto-fit, minmax(200px, 1fr))',
    gap: tokens.spacingHorizontalM,
    marginBottom: tokens.spacingVerticalL,
    padding: tokens.spacingVerticalL,
    backgroundColor: tokens.colorNeutralBackground2,
    borderRadius: tokens.borderRadiusMedium,
  },
  actions: {
    display: 'flex',
    gap: tokens.spacingHorizontalM,
    marginBottom: tokens.spacingVerticalL,
  },
  logTable: {
    marginTop: tokens.spacingVerticalL,
  },
  logMessage: {
    fontFamily: 'monospace',
    fontSize: '12px',
    wordBreak: 'break-word',
  },
  levelBadge: {
    minWidth: '60px',
  },
  timestamp: {
    fontFamily: 'monospace',
    fontSize: '11px',
    color: tokens.colorNeutralForeground3,
  },
  correlationId: {
    fontFamily: 'monospace',
    fontSize: '11px',
    color: tokens.colorBrandForeground1,
    cursor: 'pointer',
  },
  stats: {
    display: 'flex',
    gap: tokens.spacingHorizontalXL,
    marginTop: tokens.spacingVerticalXL,
    padding: tokens.spacingVerticalL,
    backgroundColor: tokens.colorNeutralBackground2,
    borderRadius: tokens.borderRadiusMedium,
  },
  statItem: {
    display: 'flex',
    flexDirection: 'column',
    gap: tokens.spacingVerticalS,
  },
  error: {
    color: tokens.colorPaletteRedForeground1,
    padding: tokens.spacingVerticalL,
    textAlign: 'center',
  },
});

interface LogEntry {
  timestamp: string;
  level: string;
  message: string;
  correlationId?: string;
  exception?: string;
  properties?: Record<string, unknown>;
}

interface LogStats {
  totalFiles: number;
  totalSizeBytes: number;
  oldestLogDate?: string;
  newestLogDate?: string;
}

export function LogViewer() {
  const styles = useStyles();
  const [logs, setLogs] = useState<LogEntry[]>([]);
  const [stats, setStats] = useState<LogStats | null>(null);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);
  
  // Filters
  const [levelFilter, setLevelFilter] = useState<string>('');
  const [searchFilter, setSearchFilter] = useState<string>('');
  const [maxEntries, setMaxEntries] = useState<number>(500);

  const loadLogs = async () => {
    setLoading(true);
    setError(null);
    
    try {
      const params = new URLSearchParams();
      params.append('maxEntries', maxEntries.toString());
      if (levelFilter) params.append('level', levelFilter);
      if (searchFilter) params.append('search', searchFilter);

      const response = await fetch(`http://127.0.0.1:5005/api/logs?${params}`);
      if (!response.ok) {
        throw new Error(`HTTP error! status: ${response.status}`);
      }
      
      const data = await response.json();
      setLogs(data.logs || []);
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Failed to load logs');
      setLogs([]);
    } finally {
      setLoading(false);
    }
  };

  const loadStats = async () => {
    try {
      const response = await fetch('http://127.0.0.1:5005/api/logs/stats');
      if (!response.ok) return;
      
      const data = await response.json();
      setStats(data.stats);
    } catch (err) {
      console.error('Failed to load stats:', err);
    }
  };

  useEffect(() => {
    loadLogs();
    loadStats();
  }, []);

  const handleRefresh = () => {
    loadLogs();
    loadStats();
  };

  const handleClearFilters = () => {
    setLevelFilter('');
    setSearchFilter('');
    setMaxEntries(500);
  };

  const copyToClipboard = (text: string) => {
    navigator.clipboard.writeText(text).then(() => {
      // Could show a toast notification here
    });
  };

  const getLevelBadgeColor = (level: string): 'success' | 'warning' | 'danger' | 'important' | 'informative' => {
    const upperLevel = level.toUpperCase();
    if (upperLevel.includes('ERR')) return 'danger';
    if (upperLevel.includes('WARN')) return 'warning';
    if (upperLevel.includes('INFO')) return 'informative';
    if (upperLevel.includes('DBG') || upperLevel.includes('DEBUG')) return 'success';
    return 'important';
  };

  const formatTimestamp = (timestamp: string) => {
    const date = new Date(timestamp);
    return date.toLocaleString();
  };

  const formatBytes = (bytes: number) => {
    if (bytes < 1024) return `${bytes} B`;
    if (bytes < 1024 * 1024) return `${(bytes / 1024).toFixed(2)} KB`;
    return `${(bytes / (1024 * 1024)).toFixed(2)} MB`;
  };

  return (
    <div className={styles.container}>
      <div className={styles.header}>
        <Title1>Log Viewer</Title1>
        <Text className={styles.subtitle}>
          View and filter application logs with correlation tracking
        </Text>
      </div>

      <Card className={styles.filters}>
        <Field label="Log Level">
          <Select 
            value={levelFilter}
            onChange={(_, data) => setLevelFilter(data.value)}
          >
            <option value="">All Levels</option>
            <option value="Debug">Debug</option>
            <option value="Information">Information</option>
            <option value="Warning">Warning</option>
            <option value="Error">Error</option>
            <option value="Fatal">Fatal</option>
          </Select>
        </Field>

        <Field label="Search">
          <Input
            placeholder="Search in messages..."
            value={searchFilter}
            onChange={(_, data) => setSearchFilter(data.value)}
          />
        </Field>

        <Field label="Max Entries">
          <Input
            type="number"
            value={maxEntries.toString()}
            onChange={(_, data) => setMaxEntries(parseInt(data.value) || 500)}
            min={10}
            max={5000}
          />
        </Field>
      </Card>

      <div className={styles.actions}>
        <Button 
          appearance="primary" 
          icon={<ArrowClockwise24Regular />}
          onClick={handleRefresh}
          disabled={loading}
        >
          {loading ? 'Loading...' : 'Refresh'}
        </Button>
        <Button 
          appearance="subtle"
          onClick={() => loadLogs()}
          disabled={loading}
        >
          Apply Filters
        </Button>
        <Button 
          appearance="subtle"
          icon={<Dismiss24Regular />}
          onClick={handleClearFilters}
        >
          Clear Filters
        </Button>
      </div>

      {error && (
        <Text className={styles.error}>
          Error: {error}
        </Text>
      )}

      {loading ? (
        <div style={{ textAlign: 'center', padding: tokens.spacingVerticalXXL }}>
          <Spinner label="Loading logs..." />
        </div>
      ) : (
        <>
          <Card>
            <Title2>Recent Logs ({logs.length} entries)</Title2>
            <div className={styles.logTable}>
              <Table>
                <TableHeader>
                  <TableRow>
                    <TableHeaderCell>Time</TableHeaderCell>
                    <TableHeaderCell>Level</TableHeaderCell>
                    <TableHeaderCell>Message</TableHeaderCell>
                    <TableHeaderCell>Correlation ID</TableHeaderCell>
                  </TableRow>
                </TableHeader>
                <TableBody>
                  {logs.length === 0 ? (
                    <TableRow>
                      <TableCell colSpan={4} style={{ textAlign: 'center', padding: tokens.spacingVerticalXXL }}>
                        No logs found matching the filters
                      </TableCell>
                    </TableRow>
                  ) : (
                    logs.map((log, index) => (
                      <TableRow key={index}>
                        <TableCell>
                          <Text className={styles.timestamp}>
                            {formatTimestamp(log.timestamp)}
                          </Text>
                        </TableCell>
                        <TableCell>
                          <Badge 
                            appearance="filled"
                            color={getLevelBadgeColor(log.level)}
                            className={styles.levelBadge}
                          >
                            {log.level}
                          </Badge>
                        </TableCell>
                        <TableCell>
                          <Text className={styles.logMessage}>
                            {log.message}
                          </Text>
                          {log.exception && (
                            <Text className={styles.logMessage} style={{ color: tokens.colorPaletteRedForeground1 }}>
                              <br />Exception: {log.exception}
                            </Text>
                          )}
                        </TableCell>
                        <TableCell>
                          {log.correlationId && (
                            <Text 
                              className={styles.correlationId}
                              onClick={() => copyToClipboard(log.correlationId!)}
                              title="Click to copy"
                            >
                              <Copy24Regular style={{ fontSize: '12px', verticalAlign: 'middle' }} /> {log.correlationId}
                            </Text>
                          )}
                        </TableCell>
                      </TableRow>
                    ))
                  )}
                </TableBody>
              </Table>
            </div>
          </Card>

          {stats && (
            <div className={styles.stats}>
              <div className={styles.statItem}>
                <Text weight="semibold">Total Log Files</Text>
                <Text>{stats.totalFiles}</Text>
              </div>
              <div className={styles.statItem}>
                <Text weight="semibold">Total Size</Text>
                <Text>{formatBytes(stats.totalSizeBytes)}</Text>
              </div>
              {stats.oldestLogDate && (
                <div className={styles.statItem}>
                  <Text weight="semibold">Oldest Log</Text>
                  <Text>{new Date(stats.oldestLogDate).toLocaleDateString()}</Text>
                </div>
              )}
              {stats.newestLogDate && (
                <div className={styles.statItem}>
                  <Text weight="semibold">Newest Log</Text>
                  <Text>{new Date(stats.newestLogDate).toLocaleDateString()}</Text>
                </div>
              )}
            </div>
          )}
        </>
      )}
    </div>
  );
}
