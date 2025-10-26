import {
  makeStyles,
  tokens,
  Button,
  Card,
  Text,
  Badge,
  Spinner,
  MessageBar,
  MessageBarBody,
  Dialog,
  DialogTrigger,
  DialogSurface,
  DialogTitle,
  DialogBody,
  DialogActions,
  DialogContent,
  Input,
  Label,
  Divider,
  Tooltip,
} from '@fluentui/react-components';
import {
  Folder24Regular,
  FolderOpen24Regular,
  Delete24Regular,
  Checkmark24Regular,
  Warning24Regular,
  Add24Regular,
} from '@fluentui/react-icons';
import { useState, useEffect, useCallback } from 'react';
import { apiUrl } from '../../config/api';
import { useNotifications } from '../Notifications/Toasts';

const useStyles = makeStyles({
  container: {
    display: 'flex',
    flexDirection: 'column',
    gap: tokens.spacingVerticalM,
  },
  header: {
    display: 'flex',
    justifyContent: 'space-between',
    alignItems: 'center',
    marginBottom: tokens.spacingVerticalS,
  },
  modelsList: {
    display: 'flex',
    flexDirection: 'column',
    gap: tokens.spacingVerticalS,
  },
  modelCard: {
    padding: tokens.spacingVerticalM,
  },
  modelHeader: {
    display: 'flex',
    justifyContent: 'space-between',
    alignItems: 'center',
    marginBottom: tokens.spacingVerticalXS,
  },
  modelInfo: {
    display: 'flex',
    flexDirection: 'column',
    gap: tokens.spacingVerticalXXS,
  },
  modelName: {
    fontWeight: tokens.fontWeightSemibold,
  },
  modelPath: {
    fontFamily: 'monospace',
    fontSize: tokens.fontSizeBase200,
    color: tokens.colorNeutralForeground3,
    wordBreak: 'break-all',
  },
  modelActions: {
    display: 'flex',
    gap: tokens.spacingHorizontalS,
    alignItems: 'center',
  },
  modelDetails: {
    display: 'flex',
    gap: tokens.spacingHorizontalM,
    marginTop: tokens.spacingVerticalXS,
    flexWrap: 'wrap',
  },
  emptyState: {
    textAlign: 'center',
    padding: tokens.spacingVerticalXXL,
    color: tokens.colorNeutralForeground3,
  },
  formField: {
    display: 'flex',
    flexDirection: 'column',
    gap: tokens.spacingVerticalXS,
    marginBottom: tokens.spacingVerticalM,
  },
  externalFoldersList: {
    display: 'flex',
    flexDirection: 'column',
    gap: tokens.spacingVerticalXS,
    marginTop: tokens.spacingVerticalS,
  },
  externalFolderItem: {
    display: 'flex',
    justifyContent: 'space-between',
    alignItems: 'center',
    padding: tokens.spacingVerticalS,
    backgroundColor: tokens.colorNeutralBackground2,
    borderRadius: tokens.borderRadiusMedium,
  },
});

interface InstalledModel {
  id: string;
  name: string;
  kind: string;
  filePath: string;
  sizeBytes: number;
  sha256?: string;
  isExternal: boolean;
  isVerified: boolean;
  verificationStatus: string;
  installedAt?: string;
  source?: string;
  description?: string;
  language?: string;
  quality?: string;
}

interface ExternalFolder {
  kind: string;
  folderPath: string;
  isReadOnly: boolean;
  addedAt: string;
}

interface ModelManagerProps {
  engineId: string;
  engineName: string;
}

export function ModelManager({ engineId, engineName }: ModelManagerProps) {
  const styles = useStyles();
  const { showSuccessToast, showFailureToast } = useNotifications();
  const [models, setModels] = useState<InstalledModel[]>([]);
  const [externalFolders, setExternalFolders] = useState<ExternalFolder[]>([]);
  const [isLoading, setIsLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [addFolderPath, setAddFolderPath] = useState('');
  const [isAddingFolder, setIsAddingFolder] = useState(false);
  const [showAddFolderDialog, setShowAddFolderDialog] = useState(false);

  const loadModels = useCallback(async () => {
    setIsLoading(true);
    setError(null);
    try {
      const response = await fetch(apiUrl(`/api/models/list?engineId=${engineId}`));
      if (!response.ok) {
        throw new Error('Failed to load models');
      }
      const data = await response.json();
      setModels(data.models || []);
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Failed to load models');
    } finally {
      setIsLoading(false);
    }
  }, [engineId]);

  const loadExternalFolders = useCallback(async () => {
    try {
      const response = await fetch(apiUrl('/api/models/external-folders'));
      if (response.ok) {
        const data = await response.json();
        setExternalFolders(data.folders || []);
      }
    } catch (err) {
      console.error('Failed to load external folders:', err);
    }
  }, []);

  useEffect(() => {
    loadModels();
    loadExternalFolders();
  }, [loadModels, loadExternalFolders]);

  const handleAddExternalFolder = async () => {
    if (!addFolderPath.trim()) {
      showFailureToast({
        title: 'Path Required',
        message: 'Please enter a folder path',
      });
      return;
    }

    setIsAddingFolder(true);
    try {
      // Determine model kind based on engine
      let kind = 'PIPER_VOICE';
      if (
        engineId.toLowerCase().includes('stable-diffusion') ||
        engineId.toLowerCase().includes('comfy')
      ) {
        kind = 'SD_BASE'; // Could be more specific based on user selection
      } else if (engineId.toLowerCase().includes('mimic')) {
        kind = 'MIMIC3_VOICE';
      }

      const response = await fetch(apiUrl('/api/models/add-external'), {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({
          kind,
          folderPath: addFolderPath.trim(),
          isReadOnly: true,
        }),
      });

      if (!response.ok) {
        const error = await response.json();
        throw new Error(error.error || 'Failed to add external folder');
      }

      const result = await response.json();
      showSuccessToast({
        title: 'Folder Added',
        message: `Added external folder! Discovered ${result.modelsDiscovered} models.`,
      });
      setAddFolderPath('');
      setShowAddFolderDialog(false);
      await loadModels();
      await loadExternalFolders();
    } catch (err) {
      showFailureToast({
        title: 'Add Folder Failed',
        message: err instanceof Error ? err.message : 'Failed to add external folder',
      });
    } finally {
      setIsAddingFolder(false);
    }
  };

  const handleRemoveModel = async (model: InstalledModel) => {
    if (!confirm(`Remove ${model.name}? This will delete the file.`)) {
      return;
    }

    try {
      const response = await fetch(apiUrl('/api/models/remove'), {
        method: 'DELETE',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({
          modelId: model.id,
          filePath: model.filePath,
        }),
      });

      if (!response.ok) {
        const error = await response.json();
        throw new Error(error.error || 'Failed to remove model');
      }

      await loadModels();
    } catch (err) {
      showFailureToast({
        title: 'Remove Failed',
        message: err instanceof Error ? err.message : 'Failed to remove model',
      });
    }
  };

  const handleOpenFolder = async (filePath: string) => {
    try {
      const response = await fetch(apiUrl('/api/models/open-folder'), {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ filePath }),
      });

      if (!response.ok) {
        const error = await response.json();
        throw new Error(error.error || 'Failed to open folder');
      }
    } catch (err) {
      showFailureToast({
        title: 'Open Folder Failed',
        message: err instanceof Error ? err.message : 'Failed to open folder',
      });
    }
  };

  const handleVerifyModel = async (model: InstalledModel) => {
    try {
      const response = await fetch(apiUrl('/api/models/verify'), {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({
          filePath: model.filePath,
          expectedSha256: model.sha256,
        }),
      });

      if (!response.ok) {
        throw new Error('Verification failed');
      }

      const result = await response.json();
      showSuccessToast({
        title: 'Verification Complete',
        message: `Verification: ${result.status}`,
      });
      await loadModels();
    } catch (err) {
      showFailureToast({
        title: 'Verification Failed',
        message: err instanceof Error ? err.message : 'Verification failed',
      });
    }
  };

  const formatSize = (bytes: number): string => {
    if (bytes < 1024) return `${bytes} B`;
    if (bytes < 1024 * 1024) return `${(bytes / 1024).toFixed(1)} KB`;
    if (bytes < 1024 * 1024 * 1024) return `${(bytes / (1024 * 1024)).toFixed(1)} MB`;
    return `${(bytes / (1024 * 1024 * 1024)).toFixed(2)} GB`;
  };

  const getVerificationBadge = (model: InstalledModel) => {
    if (model.isVerified) {
      return (
        <Badge appearance="tint" color="success" icon={<Checkmark24Regular />}>
          Verified
        </Badge>
      );
    }
    if (model.verificationStatus.includes('Unknown')) {
      return (
        <Badge appearance="outline" color="warning" icon={<Warning24Regular />}>
          Unknown checksum
        </Badge>
      );
    }
    return (
      <Badge appearance="outline" color="danger" icon={<Warning24Regular />}>
        Not verified
      </Badge>
    );
  };

  if (isLoading) {
    return (
      <div
        style={{ display: 'flex', justifyContent: 'center', padding: tokens.spacingVerticalXXL }}
      >
        <Spinner label="Loading models..." />
      </div>
    );
  }

  if (error) {
    return (
      <MessageBar intent="error">
        <MessageBarBody>{error}</MessageBarBody>
      </MessageBar>
    );
  }

  const totalSize = models.reduce((sum, m) => sum + m.sizeBytes, 0);

  return (
    <div className={styles.container}>
      <div className={styles.header}>
        <div>
          <Text size={500} weight="semibold">
            Models & Voices for {engineName}
          </Text>
          <Text size={200} style={{ color: tokens.colorNeutralForeground3, display: 'block' }}>
            {models.length} items • {formatSize(totalSize)} total
          </Text>
        </div>
        <Dialog
          open={showAddFolderDialog}
          onOpenChange={(_, data) => setShowAddFolderDialog(data.open)}
        >
          <DialogTrigger disableButtonEnhancement>
            <Button appearance="primary" icon={<Add24Regular />}>
              Add External Folder
            </Button>
          </DialogTrigger>
          <DialogSurface>
            <DialogBody>
              <DialogTitle>Add External Folder</DialogTitle>
              <DialogContent>
                <div className={styles.formField}>
                  <Label>Folder Path</Label>
                  <Input
                    value={addFolderPath}
                    onChange={(e) => setAddFolderPath(e.target.value)}
                    placeholder="C:\MyModels\SD"
                  />
                  <Text size={200} style={{ color: tokens.colorNeutralForeground3 }}>
                    Models in this folder will be indexed (read-only)
                  </Text>
                </div>
              </DialogContent>
              <DialogActions>
                <DialogTrigger disableButtonEnhancement>
                  <Button appearance="secondary">Cancel</Button>
                </DialogTrigger>
                <Button
                  appearance="primary"
                  onClick={handleAddExternalFolder}
                  disabled={isAddingFolder}
                >
                  {isAddingFolder ? 'Adding...' : 'Add Folder'}
                </Button>
              </DialogActions>
            </DialogBody>
          </DialogSurface>
        </Dialog>
      </div>

      {externalFolders.length > 0 && (
        <>
          <Divider />
          <div>
            <Text size={400} weight="semibold">
              External Folders
            </Text>
            <div className={styles.externalFoldersList}>
              {externalFolders.map((folder, idx) => (
                <div key={idx} className={styles.externalFolderItem}>
                  <div>
                    <Text style={{ fontFamily: 'monospace', fontSize: tokens.fontSizeBase200 }}>
                      {folder.folderPath}
                    </Text>
                    <Text
                      size={200}
                      style={{ color: tokens.colorNeutralForeground3, display: 'block' }}
                    >
                      {folder.kind} • {folder.isReadOnly ? 'Read-only' : 'Read/Write'}
                    </Text>
                  </div>
                  <Button
                    appearance="subtle"
                    icon={<FolderOpen24Regular />}
                    onClick={() => handleOpenFolder(folder.folderPath)}
                  >
                    Open
                  </Button>
                </div>
              ))}
            </div>
          </div>
        </>
      )}

      <Divider />

      {models.length === 0 ? (
        <div className={styles.emptyState}>
          <Text>No models installed yet</Text>
          <Text size={200} style={{ display: 'block', marginTop: tokens.spacingVerticalXS }}>
            Add an external folder to index existing models
          </Text>
        </div>
      ) : (
        <div className={styles.modelsList}>
          {models.map((model) => (
            <Card key={model.id} className={styles.modelCard}>
              <div className={styles.modelHeader}>
                <div className={styles.modelInfo}>
                  <Text className={styles.modelName}>{model.name}</Text>
                  <Text className={styles.modelPath}>{model.filePath}</Text>
                </div>
                <div className={styles.modelActions}>
                  {getVerificationBadge(model)}
                  {model.isExternal && (
                    <Badge appearance="outline" icon={<Folder24Regular />}>
                      External
                    </Badge>
                  )}
                  <Tooltip content="Open folder" relationship="label">
                    <Button
                      appearance="subtle"
                      icon={<FolderOpen24Regular />}
                      onClick={() => handleOpenFolder(model.filePath)}
                    />
                  </Tooltip>
                  {model.sha256 && (
                    <Tooltip content="Verify checksum" relationship="label">
                      <Button
                        appearance="subtle"
                        icon={<Checkmark24Regular />}
                        onClick={() => handleVerifyModel(model)}
                      />
                    </Tooltip>
                  )}
                  {!model.isExternal && (
                    <Tooltip content="Remove model" relationship="label">
                      <Button
                        appearance="subtle"
                        icon={<Delete24Regular />}
                        onClick={() => handleRemoveModel(model)}
                      />
                    </Tooltip>
                  )}
                </div>
              </div>
              <div className={styles.modelDetails}>
                <Text size={200} style={{ color: tokens.colorNeutralForeground3 }}>
                  Size: {formatSize(model.sizeBytes)}
                </Text>
                {model.language && (
                  <Text size={200} style={{ color: tokens.colorNeutralForeground3 }}>
                    Language: {model.language}
                  </Text>
                )}
                {model.quality && (
                  <Text size={200} style={{ color: tokens.colorNeutralForeground3 }}>
                    Quality: {model.quality}
                  </Text>
                )}
                {model.kind && (
                  <Text size={200} style={{ color: tokens.colorNeutralForeground3 }}>
                    Type: {model.kind}
                  </Text>
                )}
              </div>
            </Card>
          ))}
        </div>
      )}
    </div>
  );
}
