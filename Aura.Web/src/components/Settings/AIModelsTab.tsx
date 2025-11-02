import {
  makeStyles,
  tokens,
  Title2,
  Title3,
  Text,
  Button,
  Card,
  Spinner,
  Badge,
  Table,
  TableBody,
  TableCell,
  TableRow,
  TableHeader,
  TableHeaderCell,
  Dialog,
  DialogSurface,
  DialogTitle,
  DialogBody,
  DialogContent,
  DialogActions,
} from '@fluentui/react-components';
import {
  Database24Regular,
  Delete20Regular,
  Checkmark20Regular,
  Warning20Regular,
  ArrowSync20Regular,
} from '@fluentui/react-icons';
import { useState, useEffect, useCallback } from 'react';
import { apiUrl } from '../../config/api';
import type { OllamaModel } from '../../types/api-v1';

const useStyles = makeStyles({
  container: {
    display: 'flex',
    flexDirection: 'column',
    gap: tokens.spacingVerticalL,
  },
  section: {
    padding: tokens.spacingVerticalL,
  },
  header: {
    display: 'flex',
    alignItems: 'center',
    gap: tokens.spacingHorizontalM,
    marginBottom: tokens.spacingVerticalL,
  },
  headerIcon: {
    fontSize: '32px',
    color: tokens.colorBrandForeground1,
  },
  subtitle: {
    color: tokens.colorNeutralForeground3,
    marginTop: tokens.spacingVerticalS,
  },
  actions: {
    display: 'flex',
    gap: tokens.spacingHorizontalS,
    marginTop: tokens.spacingVerticalM,
  },
  infoCard: {
    padding: tokens.spacingVerticalM,
    backgroundColor: tokens.colorNeutralBackground3,
    marginBottom: tokens.spacingVerticalL,
  },
  emptyState: {
    textAlign: 'center',
    padding: tokens.spacingVerticalXXL,
    color: tokens.colorNeutralForeground3,
  },
  modelStats: {
    display: 'flex',
    gap: tokens.spacingHorizontalL,
    marginTop: tokens.spacingVerticalM,
  },
  statItem: {
    display: 'flex',
    flexDirection: 'column',
    gap: tokens.spacingVerticalXS,
  },
});

interface ModelInfo {
  id: string;
  name: string;
  provider: string;
  kind: string;
  status: 'available' | 'downloading' | 'not-installed';
  sizeGB?: number;
  sizeMB?: number;
  filePath?: string;
}

interface AIModelsTabProps {
  // Optional props for state management
  onModelsChange?: (models: ModelInfo[]) => void;
}

export const AIModelsTab: React.FC<AIModelsTabProps> = ({ onModelsChange }) => {
  const styles = useStyles();
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [ollamaModels, setOllamaModels] = useState<OllamaModel[]>([]);
  const [ollamaUrl, setOllamaUrl] = useState('http://127.0.0.1:11434');
  const [showDeleteDialog, setShowDeleteDialog] = useState(false);
  const [modelToDelete, setModelToDelete] = useState<ModelInfo | null>(null);
  const [isDeleting, setIsDeleting] = useState(false);

  const loadOllamaModels = useCallback(async () => {
    setLoading(true);
    setError(null);

    try {
      // First, get the Ollama URL from provider paths
      const pathsResponse = await fetch(apiUrl('/api/providers/paths/load'));
      if (pathsResponse.ok) {
        const pathsData = await pathsResponse.json();
        if (pathsData.ollamaUrl) {
          setOllamaUrl(pathsData.ollamaUrl);
        }
      }

      // Then fetch Ollama models
      const modelsResponse = await fetch(
        apiUrl(`/api/engines/ollama/models?url=${encodeURIComponent(ollamaUrl)}`)
      );

      if (modelsResponse.ok) {
        const data = await modelsResponse.json();
        setOllamaModels(data.models || []);

        // Notify parent component if callback provided
        if (onModelsChange) {
          const modelInfos: ModelInfo[] = (data.models || []).map((m: OllamaModel) => ({
            id: m.name,
            name: m.name,
            provider: 'Ollama',
            kind: 'LLM',
            status: 'available' as const,
            sizeGB: m.sizeGB,
          }));
          onModelsChange(modelInfos);
        }
      } else if (modelsResponse.status === 404 || modelsResponse.status === 503) {
        setError(
          'Ollama is not running or not installed. Please start Ollama or install it from the Downloads page.'
        );
      } else {
        throw new Error('Failed to load Ollama models');
      }
    } catch (err) {
      const errorMessage = err instanceof Error ? err.message : 'Failed to load models';
      setError(errorMessage);
      console.error('Error loading Ollama models:', err);
    } finally {
      setLoading(false);
    }
  }, [ollamaUrl, onModelsChange]);

  useEffect(() => {
    loadOllamaModels();
  }, [loadOllamaModels]);

  const handleRefresh = () => {
    loadOllamaModels();
  };

  const handleDeleteModel = (model: OllamaModel) => {
    setModelToDelete({
      id: model.name,
      name: model.name,
      provider: 'Ollama',
      kind: 'LLM',
      status: 'available',
      sizeGB: model.sizeGB,
    });
    setShowDeleteDialog(true);
  };

  const confirmDelete = async () => {
    if (!modelToDelete) return;

    setIsDeleting(true);
    try {
      const response = await fetch(apiUrl('/api/engines/ollama/delete'), {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({
          model: modelToDelete.name,
          url: ollamaUrl,
        }),
      });

      if (response.ok) {
        await loadOllamaModels();
        setShowDeleteDialog(false);
        setModelToDelete(null);
      } else {
        const data = await response.json();
        setError(data.error || 'Failed to delete model');
      }
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Failed to delete model');
    } finally {
      setIsDeleting(false);
    }
  };

  const totalSize = ollamaModels.reduce((sum, m) => sum + (m.sizeGB || 0), 0);

  if (loading && ollamaModels.length === 0) {
    return (
      <Card className={styles.section}>
        <div style={{ textAlign: 'center', padding: tokens.spacingVerticalXXL }}>
          <Spinner size="large" label="Loading AI models..." />
        </div>
      </Card>
    );
  }

  return (
    <div className={styles.container}>
      <Card className={styles.section}>
        <div className={styles.header}>
          <Database24Regular className={styles.headerIcon} />
          <div>
            <Title2>AI Models Management</Title2>
            <Text className={styles.subtitle}>
              Manage locally installed AI models for text generation and other features
            </Text>
          </div>
        </div>

        <Card className={styles.infoCard}>
          <Text weight="semibold" size={300}>
            ℹ️ About AI Models
          </Text>
          <Text size={200} style={{ marginTop: tokens.spacingVerticalXS, display: 'block' }}>
            This section shows AI models that are installed locally on your system. Local models run
            on your hardware and don&apos;t require API keys or internet connection.
          </Text>
          <Text size={200} style={{ marginTop: tokens.spacingVerticalS, display: 'block' }}>
            To install new models, visit the <strong>Downloads</strong> page or use Ollama&apos;s
            command-line interface.
          </Text>
        </Card>

        {error && (
          <Card
            style={{
              padding: tokens.spacingVerticalM,
              backgroundColor: tokens.colorPaletteRedBackground1,
              marginBottom: tokens.spacingVerticalL,
            }}
          >
            <div style={{ display: 'flex', alignItems: 'center', gap: tokens.spacingHorizontalS }}>
              <Warning20Regular />
              <Text>{error}</Text>
            </div>
          </Card>
        )}

        <div className={styles.actions}>
          <Button
            appearance="primary"
            icon={<ArrowSync20Regular />}
            onClick={handleRefresh}
            disabled={loading}
          >
            {loading ? 'Refreshing...' : 'Refresh Models'}
          </Button>
        </div>
      </Card>

      <Card className={styles.section}>
        <Title3>Ollama Models</Title3>
        <Text size={200} className={styles.subtitle}>
          Large Language Models (LLMs) installed via Ollama
        </Text>

        {ollamaModels.length > 0 && (
          <div className={styles.modelStats}>
            <div className={styles.statItem}>
              <Text size={200} style={{ color: tokens.colorNeutralForeground3 }}>
                Total Models
              </Text>
              <Text size={400} weight="semibold">
                {ollamaModels.length}
              </Text>
            </div>
            <div className={styles.statItem}>
              <Text size={200} style={{ color: tokens.colorNeutralForeground3 }}>
                Total Storage
              </Text>
              <Text size={400} weight="semibold">
                {totalSize.toFixed(2)} GB
              </Text>
            </div>
          </div>
        )}

        {ollamaModels.length === 0 && !loading && (
          <div className={styles.emptyState}>
            <Text size={300}>No Ollama models found</Text>
            <Text size={200} style={{ marginTop: tokens.spacingVerticalS, display: 'block' }}>
              Install Ollama from the Downloads page and pull models using the Ollama CLI
            </Text>
            <Text
              size={200}
              style={{
                marginTop: tokens.spacingVerticalXS,
                display: 'block',
                fontFamily: 'monospace',
              }}
            >
              Example: ollama pull llama3.1:8b-q4_k_m
            </Text>
          </div>
        )}

        {ollamaModels.length > 0 && (
          <Table style={{ marginTop: tokens.spacingVerticalL }}>
            <TableHeader>
              <TableRow>
                <TableHeaderCell>Model Name</TableHeaderCell>
                <TableHeaderCell>Size</TableHeaderCell>
                <TableHeaderCell>Status</TableHeaderCell>
                <TableHeaderCell>Actions</TableHeaderCell>
              </TableRow>
            </TableHeader>
            <TableBody>
              {ollamaModels.map((model) => (
                <TableRow key={model.name}>
                  <TableCell>
                    <Text weight="semibold">{model.name}</Text>
                  </TableCell>
                  <TableCell>
                    <Text>{model.sizeGB.toFixed(2)} GB</Text>
                  </TableCell>
                  <TableCell>
                    <Badge appearance="filled" color="success" icon={<Checkmark20Regular />}>
                      Installed
                    </Badge>
                  </TableCell>
                  <TableCell>
                    <div style={{ display: 'flex', gap: tokens.spacingHorizontalXS }}>
                      <Button
                        size="small"
                        appearance="subtle"
                        icon={<Delete20Regular />}
                        onClick={() => handleDeleteModel(model)}
                      >
                        Delete
                      </Button>
                    </div>
                  </TableCell>
                </TableRow>
              ))}
            </TableBody>
          </Table>
        )}
      </Card>

      <Card className={styles.section}>
        <Title3>Cloud Provider Models</Title3>
        <Text size={200} className={styles.subtitle}>
          Models available through API providers (OpenAI, Anthropic, Google)
        </Text>

        <Card className={styles.infoCard} style={{ marginTop: tokens.spacingVerticalM }}>
          <Text weight="semibold" size={300}>
            📋 Available Cloud Models
          </Text>
          <ul style={{ marginTop: tokens.spacingVerticalS, paddingLeft: '20px' }}>
            <li>
              <Text size={200}>
                <strong>OpenAI:</strong> GPT-4, GPT-4 Turbo, GPT-3.5 Turbo (requires API key in API
                Keys tab)
              </Text>
            </li>
            <li>
              <Text size={200}>
                <strong>Anthropic:</strong> Claude 3 Opus, Claude 3 Sonnet, Claude 3 Haiku (requires
                API key)
              </Text>
            </li>
            <li>
              <Text size={200}>
                <strong>Google:</strong> Gemini Pro, Gemini Pro Vision (requires API key)
              </Text>
            </li>
          </ul>
          <Text
            size={200}
            style={{
              marginTop: tokens.spacingVerticalM,
              fontStyle: 'italic',
              color: tokens.colorNeutralForeground3,
            }}
          >
            Configure API keys in the <strong>API Keys</strong> tab to enable cloud models. Model
            selection for specific tasks is available in the <strong>Providers</strong> tab.
          </Text>
        </Card>
      </Card>

      <Card className={styles.section}>
        <Title3>Model Installation</Title3>
        <Text size={200} className={styles.subtitle}>
          Download and install new models
        </Text>

        <Card className={styles.infoCard} style={{ marginTop: tokens.spacingVerticalM }}>
          <Text weight="semibold" size={300}>
            🔧 How to Install Models
          </Text>
          <div style={{ marginTop: tokens.spacingVerticalS }}>
            <Text size={200} style={{ display: 'block', marginBottom: tokens.spacingVerticalXS }}>
              <strong>Option 1: Downloads Page</strong>
            </Text>
            <Text size={200} style={{ display: 'block', marginBottom: tokens.spacingVerticalM }}>
              Visit the Downloads page to automatically install Ollama and recommended models with
              one click.
            </Text>

            <Text size={200} style={{ display: 'block', marginBottom: tokens.spacingVerticalXS }}>
              <strong>Option 2: Ollama CLI</strong>
            </Text>
            <Text size={200} style={{ display: 'block', marginBottom: tokens.spacingVerticalXS }}>
              Use the Ollama command-line interface to pull models:
            </Text>
            <div
              style={{
                fontFamily: 'monospace',
                fontSize: '12px',
                backgroundColor: tokens.colorNeutralBackground1,
                padding: tokens.spacingVerticalS,
                borderRadius: tokens.borderRadiusSmall,
                marginTop: tokens.spacingVerticalXS,
              }}
            >
              ollama pull llama3.1:8b-q4_k_m
              <br />
              ollama pull codellama:7b
              <br />
              ollama pull mistral:latest
            </div>
          </div>
        </Card>
      </Card>

      {/* Delete confirmation dialog */}
      <Dialog open={showDeleteDialog} onOpenChange={(_, data) => setShowDeleteDialog(data.open)}>
        <DialogSurface>
          <DialogTitle>Delete Model</DialogTitle>
          <DialogBody>
            <DialogContent>
              <Text>
                Are you sure you want to delete the model <strong>{modelToDelete?.name}</strong>?
              </Text>
              <Text size={200} style={{ marginTop: tokens.spacingVerticalS, display: 'block' }}>
                This will remove the model from your system and free up{' '}
                {modelToDelete?.sizeGB?.toFixed(2)} GB of storage.
              </Text>
              <Text
                size={200}
                style={{
                  marginTop: tokens.spacingVerticalS,
                  display: 'block',
                  fontStyle: 'italic',
                  color: tokens.colorNeutralForeground3,
                }}
              >
                You can reinstall it later by pulling it again with Ollama.
              </Text>
            </DialogContent>
            <DialogActions>
              <Button appearance="secondary" onClick={() => setShowDeleteDialog(false)}>
                Cancel
              </Button>
              <Button
                appearance="primary"
                onClick={confirmDelete}
                disabled={isDeleting}
                style={{ backgroundColor: tokens.colorPaletteRedBackground3 }}
              >
                {isDeleting ? 'Deleting...' : 'Delete'}
              </Button>
            </DialogActions>
          </DialogBody>
        </DialogSurface>
      </Dialog>
    </div>
  );
};

export default AIModelsTab;
