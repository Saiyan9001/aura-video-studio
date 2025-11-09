import {
  Card,
  Text,
  Button,
  Spinner,
  Badge,
  DataGrid,
  DataGridHeader,
  DataGridRow,
  DataGridHeaderCell,
  DataGridBody,
  DataGridCell,
  TableColumnDefinition,
  createTableColumn,
  makeStyles,
  tokens,
  shorthands,
} from '@fluentui/react-components';
import {
  CheckmarkCircle24Regular,
  Warning24Regular,
  DismissCircle24Regular,
  ArrowClockwise24Regular,
} from '@fluentui/react-icons';
import React, { useEffect, useState, useCallback } from 'react';
import apiClient from '../../services/api/apiClient';

const useStyles = makeStyles({
  card: {
    ...shorthands.padding(tokens.spacingVerticalL),
    marginBottom: tokens.spacingVerticalM,
  },
  header: {
    display: 'flex',
    alignItems: 'center',
    justifyContent: 'space-between',
    marginBottom: tokens.spacingVerticalM,
  },
  statusCell: {
    display: 'flex',
    alignItems: 'center',
    ...shorthands.gap(tokens.spacingHorizontalS),
  },
  badge: {
    marginLeft: tokens.spacingHorizontalXS,
  },
  grid: {
    marginTop: tokens.spacingVerticalM,
  },
  error: {
    color: tokens.colorPaletteRedForeground1,
    marginTop: tokens.spacingVerticalS,
  },
});

interface ProviderHealth {
  providerName: string;
  status: string;
  successRatePercent: number;
  averageLatencySeconds: number;
  totalRequests: number;
  consecutiveFailures: number;
  circuitState: string;
  lastUpdated: string;
  nextRetryTime?: string;
}

export const ProviderHealthDashboard: React.FC = () => {
  const styles = useStyles();
  const [providers, setProviders] = useState<ProviderHealth[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [refreshing, setRefreshing] = useState(false);

  const fetchProviderHealth = useCallback(async () => {
    try {
      const response = await apiClient.get<ProviderHealth[]>('/api/providerhealth');
      setProviders(response.data);
      setError(null);
    } catch (err) {
      setError('Failed to fetch provider health data');
      console.error('Error fetching provider health:', err);
    }
  }, []);

  const handleRefresh = async () => {
    setRefreshing(true);
    await fetchProviderHealth();
    setRefreshing(false);
  };

  const handleResetProvider = async (providerName: string) => {
    try {
      await apiClient.post(`/api/providerhealth/${providerName}/reset`);
      await fetchProviderHealth();
    } catch (err) {
      console.error('Error resetting provider:', err);
    }
  };

  useEffect(() => {
    const loadData = async () => {
      setLoading(true);
      await fetchProviderHealth();
      setLoading(false);
    };
    loadData();
  }, [fetchProviderHealth]);

  const getStatusIcon = (status: string) => {
    switch (status.toLowerCase()) {
      case 'healthy':
        return <CheckmarkCircle24Regular />;
      case 'degraded':
        return <Warning24Regular />;
      case 'unhealthy':
        return <DismissCircle24Regular />;
      default:
        return null;
    }
  };

  const getStatusColor = (status: string) => {
    switch (status.toLowerCase()) {
      case 'healthy':
        return 'success';
      case 'degraded':
        return 'warning';
      case 'unhealthy':
        return 'danger';
      default:
        return 'subtle';
    }
  };

  const getCircuitColor = (state: string) => {
    switch (state.toLowerCase()) {
      case 'closed':
        return 'success';
      case 'halfopen':
        return 'warning';
      case 'open':
        return 'danger';
      default:
        return 'subtle';
    }
  };

  const columns: TableColumnDefinition<ProviderHealth>[] = [
    createTableColumn<ProviderHealth>({
      columnId: 'provider',
      compare: (a, b) => a.providerName.localeCompare(b.providerName),
      renderHeaderCell: () => 'Provider',
      renderCell: (item) => <Text weight="semibold">{item.providerName}</Text>,
    }),
    createTableColumn<ProviderHealth>({
      columnId: 'status',
      compare: (a, b) => a.status.localeCompare(b.status),
      renderHeaderCell: () => 'Health',
      renderCell: (item) => (
        <div className={styles.statusCell}>
          {getStatusIcon(item.status)}
          <Badge appearance="filled" color={getStatusColor(item.status) as any}>
            {item.status}
          </Badge>
        </div>
      ),
    }),
    createTableColumn<ProviderHealth>({
      columnId: 'successRate',
      compare: (a, b) => a.successRatePercent - b.successRatePercent,
      renderHeaderCell: () => 'Success Rate',
      renderCell: (item) => <Text>{item.successRatePercent.toFixed(1)}%</Text>,
    }),
    createTableColumn<ProviderHealth>({
      columnId: 'latency',
      compare: (a, b) => a.averageLatencySeconds - b.averageLatencySeconds,
      renderHeaderCell: () => 'Avg Latency',
      renderCell: (item) => <Text>{item.averageLatencySeconds.toFixed(2)}s</Text>,
    }),
    createTableColumn<ProviderHealth>({
      columnId: 'requests',
      compare: (a, b) => a.totalRequests - b.totalRequests,
      renderHeaderCell: () => 'Requests',
      renderCell: (item) => <Text>{item.totalRequests}</Text>,
    }),
    createTableColumn<ProviderHealth>({
      columnId: 'circuit',
      compare: (a, b) => a.circuitState.localeCompare(b.circuitState),
      renderHeaderCell: () => 'Circuit',
      renderCell: (item) => (
        <Badge appearance="outline" color={getCircuitColor(item.circuitState) as any}>
          {item.circuitState}
        </Badge>
      ),
    }),
    createTableColumn<ProviderHealth>({
      columnId: 'actions',
      renderHeaderCell: () => 'Actions',
      renderCell: (item) => (
        <Button
          appearance="subtle"
          size="small"
          onClick={() => handleResetProvider(item.providerName)}
        >
          Reset
        </Button>
      ),
    }),
  ];

  if (loading) {
    return (
      <Card className={styles.card}>
        <div style={{ display: 'flex', alignItems: 'center', gap: tokens.spacingHorizontalM }}>
          <Spinner size="small" />
          <Text>Loading provider health data...</Text>
        </div>
      </Card>
    );
  }

  return (
    <Card className={styles.card}>
      <div className={styles.header}>
        <Text size={500} weight="semibold">
          Provider Health Monitoring
        </Text>
        <Button
          appearance="subtle"
          icon={<ArrowClockwise24Regular />}
          onClick={handleRefresh}
          disabled={refreshing}
        >
          {refreshing ? 'Refreshing...' : 'Refresh'}
        </Button>
      </div>

      {providers.length === 0 ? (
        <Text>No provider health data available. Providers will appear here after first use.</Text>
      ) : (
        <DataGrid items={providers} columns={columns} sortable className={styles.grid}>
          <DataGridHeader>
            <DataGridRow>
              {({ renderHeaderCell }) => (
                <DataGridHeaderCell>{renderHeaderCell()}</DataGridHeaderCell>
              )}
            </DataGridRow>
          </DataGridHeader>
          <DataGridBody<ProviderHealth>>
            {({ item, rowId }) => (
              <DataGridRow<ProviderHealth> key={rowId}>
                {({ renderCell }) => <DataGridCell>{renderCell(item)}</DataGridCell>}
              </DataGridRow>
            )}
          </DataGridBody>
        </DataGrid>
      )}

      {error && <Text className={styles.error}>{error}</Text>}
    </Card>
  );
};
