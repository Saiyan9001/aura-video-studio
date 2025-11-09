import {
  Card,
  Text,
  Button,
  Spinner,
  Badge,
  Menu,
  MenuTrigger,
  MenuPopover,
  MenuList,
  MenuItem,
  makeStyles,
  tokens,
  shorthands,
} from '@fluentui/react-components';
import {
  CheckmarkCircle24Regular,
  DismissCircle24Regular,
  Warning24Regular,
  ArrowClockwise24Regular,
  CloudAdd24Regular,
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
  status: {
    display: 'flex',
    alignItems: 'center',
    ...shorthands.gap(tokens.spacingHorizontalS),
  },
  badge: {
    marginLeft: tokens.spacingHorizontalS,
  },
  section: {
    marginTop: tokens.spacingVerticalM,
  },
  modelList: {
    display: 'flex',
    flexDirection: 'column',
    ...shorthands.gap(tokens.spacingVerticalS),
    marginTop: tokens.spacingVerticalS,
  },
  modelItem: {
    display: 'flex',
    alignItems: 'center',
    justifyContent: 'space-between',
    ...shorthands.padding(tokens.spacingVerticalS, tokens.spacingHorizontalM),
    backgroundColor: tokens.colorNeutralBackground1,
    ...shorthands.borderRadius(tokens.borderRadiusMedium),
  },
  modelInfo: {
    display: 'flex',
    flexDirection: 'column',
  },
  actions: {
    display: 'flex',
    ...shorthands.gap(tokens.spacingHorizontalS),
  },
  error: {
    color: tokens.colorPaletteRedForeground1,
    marginTop: tokens.spacingVerticalS,
  },
});

interface OllamaStatus {
  isRunning: boolean;
  isInstalled: boolean;
  version: string | null;
  baseUrl: string;
  errorMessage: string | null;
}

interface OllamaModel {
  name: string;
  size: number;
  sizeFormatted: string;
  modifiedAt: string | null;
  digest: string | null;
}

export const OllamaStatusPanel: React.FC = () => {
  const styles = useStyles();
  const [status, setStatus] = useState<OllamaStatus | null>(null);
  const [models, setModels] = useState<OllamaModel[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [refreshing, setRefreshing] = useState(false);

  const fetchStatus = useCallback(async () => {
    try {
      const response = await apiClient.get<OllamaStatus>('/api/ollama/status');
      setStatus(response.data);
      setError(null);
    } catch (err) {
      setError('Failed to fetch Ollama status');
      console.error('Error fetching Ollama status:', err);
    }
  }, []);

  const fetchModels = useCallback(async () => {
    try {
      const response = await apiClient.get<OllamaModel[]>('/api/ollama/models');
      setModels(response.data);
    } catch (err) {
      console.error('Error fetching Ollama models:', err);
      setModels([]);
    }
  }, []);

  const loadData = useCallback(async () => {
    setLoading(true);
    await Promise.all([fetchStatus(), fetchModels()]);
    setLoading(false);
  }, [fetchStatus, fetchModels]);

  const handleRefresh = async () => {
    setRefreshing(true);
    await loadData();
    setRefreshing(false);
  };

  const handlePullModel = async (modelName: string) => {
    try {
      await apiClient.post(`/api/ollama/models/${modelName}/pull`);
      await fetchModels();
    } catch (err) {
      console.error('Error pulling model:', err);
    }
  };

  useEffect(() => {
    loadData();
  }, [loadData]);

  const getStatusIcon = () => {
    if (!status) return null;
    if (status.isRunning) {
      return <CheckmarkCircle24Regular />;
    }
    if (status.isInstalled) {
      return <Warning24Regular />;
    }
    return <DismissCircle24Regular />;
  };

  const getStatusText = () => {
    if (!status) return 'Unknown';
    if (status.isRunning) return 'Running';
    if (status.isInstalled) return 'Stopped';
    return 'Not Installed';
  };

  const getStatusColor = () => {
    if (!status) return 'subtle';
    if (status.isRunning) return 'success';
    if (status.isInstalled) return 'warning';
    return 'danger';
  };

  if (loading) {
    return (
      <Card className={styles.card}>
        <div style={{ display: 'flex', alignItems: 'center', gap: tokens.spacingHorizontalM }}>
          <Spinner size="small" />
          <Text>Loading Ollama status...</Text>
        </div>
      </Card>
    );
  }

  return (
    <Card className={styles.card}>
      <div className={styles.header}>
        <div className={styles.status}>
          {getStatusIcon()}
          <Text size={500} weight="semibold">
            Ollama Service
          </Text>
          <Badge appearance="filled" color={getStatusColor() as any} className={styles.badge}>
            {getStatusText()}
          </Badge>
        </div>
        <Button
          appearance="subtle"
          icon={<ArrowClockwise24Regular />}
          onClick={handleRefresh}
          disabled={refreshing}
        >
          {refreshing ? 'Refreshing...' : 'Refresh'}
        </Button>
      </div>

      {status && (
        <>
          {status.version && (
            <Text>
              <strong>Version:</strong> {status.version}
            </Text>
          )}
          <Text>
            <strong>Base URL:</strong> {status.baseUrl}
          </Text>

          {status.errorMessage && <Text className={styles.error}>{status.errorMessage}</Text>}
        </>
      )}

      {status?.isRunning && (
        <div className={styles.section}>
          <Text size={400} weight="semibold">
            Installed Models ({models.length})
          </Text>
          <div className={styles.modelList}>
            {models.length === 0 ? (
              <Text>No models installed. Pull a model to get started.</Text>
            ) : (
              models.map((model) => (
                <div key={model.name} className={styles.modelItem}>
                  <div className={styles.modelInfo}>
                    <Text weight="semibold">{model.name}</Text>
                    <Text size={200}>
                      Size: {model.sizeFormatted}
                      {model.modifiedAt &&
                        ` • Modified: ${new Date(model.modifiedAt).toLocaleDateString()}`}
                    </Text>
                  </div>
                  <Menu>
                    <MenuTrigger>
                      <Button appearance="subtle" size="small">
                        Actions
                      </Button>
                    </MenuTrigger>
                    <MenuPopover>
                      <MenuList>
                        <MenuItem onClick={() => console.log('View info:', model.name)}>
                          View Info
                        </MenuItem>
                      </MenuList>
                    </MenuPopover>
                  </Menu>
                </div>
              ))
            )}
          </div>

          <div className={styles.actions}>
            <Button
              appearance="primary"
              icon={<CloudAdd24Regular />}
              onClick={() => handlePullModel('llama3.1:8b-q4_k_m')}
            >
              Pull Recommended Model
            </Button>
          </div>
        </div>
      )}

      {error && <Text className={styles.error}>{error}</Text>}
    </Card>
  );
};
