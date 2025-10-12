import { useState, useEffect } from 'react';
import {
  Dialog,
  DialogSurface,
  DialogBody,
  DialogTitle,
  DialogContent,
  DialogActions,
  Button,
  Text,
  Badge,
  Spinner,
  Input,
  makeStyles,
  tokens,
  Table,
  TableBody,
  TableCell,
  TableRow,
  TableHeader,
  TableHeaderCell,
  Menu,
  MenuTrigger,
  MenuPopover,
  MenuList,
  MenuItem,
} from '@fluentui/react-components';
import {
  Checkmark24Regular,
  Delete24Regular,
  Folder24Regular,
  FolderAdd24Regular,
  ArrowDownload24Regular,
  Info24Regular,
  MoreHorizontal24Regular,
} from '@fluentui/react-icons';

const useStyles = makeStyles({
  dialog: {
    minWidth: '800px',
    maxWidth: '1000px',
  },
  content: {
    display: 'flex',
    flexDirection: 'column',
    gap: tokens.spacingVerticalM,
  },
  table: {
    minHeight: '300px',
    maxHeight: '500px',
    overflowY: 'auto',
  },
  row: {
    cursor: 'pointer',
    '&:hover': {
      backgroundColor: tokens.colorNeutralBackground1Hover,
    },
  },
  externalBadge: {
    marginLeft: tokens.spacingHorizontalXS,
  },
  pathText: {
    fontSize: tokens.fontSizeBase200,
    color: tokens.colorNeutralForeground3,
    fontFamily: 'monospace',
    wordBreak: 'break-all',
  },
  actions: {
    display: 'flex',
    gap: tokens.spacingHorizontalS,
    marginTop: tokens.spacingVerticalM,
  },
  emptyState: {
    textAlign: 'center',
    padding: tokens.spacingVerticalXXL,
    color: tokens.colorNeutralForeground3,
  },
  sectionHeader: {
    marginTop: tokens.spacingVerticalL,
    marginBottom: tokens.spacingVerticalS,
    fontWeight: tokens.fontWeightSemibold,
  },
});

interface Model {
  id: string;
  name: string;
  kind: string;
  sizeBytes: number;
  sha256?: string;
  filePath?: string;
  isExternal: boolean;
  provenance?: string;
  verificationStatus?: string;
  lastVerified?: string;
  sizeFormatted: string;
}

interface ExternalDirectory {
  path: string;
  kind: string;
  isReadOnly: boolean;
  addedAt: string;
}

interface ModelManagerProps {
  open: boolean;
  onOpenChange: (open: boolean) => void;
  engineId: string;
  engineName: string;
}

export function ModelManager({ open, onOpenChange, engineId, engineName }: ModelManagerProps) {
  const styles = useStyles();
  const [models, setModels] = useState<Model[]>([]);
  const [externalDirs, setExternalDirs] = useState<ExternalDirectory[]>([]);
  const [isLoading, setIsLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [selectedModel, setSelectedModel] = useState<Model | null>(null);
  const [newExternalPath, setNewExternalPath] = useState('');

  const loadModels = async () => {
    setIsLoading(true);
    setError(null);
    try {
      const response = await fetch(`http://127.0.0.1:5005/api/models/list?engineId=${engineId}`);
      if (!response.ok) throw new Error('Failed to load models');
      const data = await response.json();
      setModels(data.models || []);
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Unknown error');
    } finally {
      setIsLoading(false);
    }
  };

  const loadExternalDirectories = async () => {
    try {
      const response = await fetch('http://127.0.0.1:5005/api/models/external-directories');
      if (!response.ok) throw new Error('Failed to load external directories');
      const data = await response.json();
      setExternalDirs(data.directories || []);
    } catch (err) {
      console.error('Failed to load external directories:', err);
    }
  };

  useEffect(() => {
    if (open) {
      loadModels();
      loadExternalDirectories();
    }
  }, [open, engineId]);

  const handleAddExternalFolder = async () => {
    if (!newExternalPath.trim()) {
      alert('Please enter a folder path');
      return;
    }

    try {
      const kind = getModelKindForEngine(engineId);
      const response = await fetch('http://127.0.0.1:5005/api/models/add-external', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({
          kind,
          folderPath: newExternalPath,
          isReadOnly: true,
        }),
      });

      if (!response.ok) {
        const data = await response.json();
        throw new Error(data.error || 'Failed to add external folder');
      }

      alert('External folder added successfully!');
      setNewExternalPath('');
      await loadModels();
      await loadExternalDirectories();
    } catch (err) {
      alert(`Error: ${err instanceof Error ? err.message : 'Unknown error'}`);
    }
  };

  const handleOpenFolder = async (filePath?: string) => {
    if (!filePath) {
      alert('File path not available');
      return;
    }

    try {
      const response = await fetch('http://127.0.0.1:5005/api/models/open-folder', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ filePath }),
      });

      if (!response.ok) {
        const data = await response.json();
        throw new Error(data.error || 'Failed to open folder');
      }
    } catch (err) {
      alert(`Error: ${err instanceof Error ? err.message : 'Unknown error'}`);
    }
  };

  const handleRemoveModel = async (model: Model) => {
    if (!confirm(`Remove ${model.name}? This will delete the file.`)) {
      return;
    }

    try {
      const response = await fetch('http://127.0.0.1:5005/api/models/remove', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({
          modelId: model.id,
          filePath: model.filePath,
        }),
      });

      if (!response.ok) {
        const data = await response.json();
        throw new Error(data.error || 'Failed to remove model');
      }

      alert('Model removed successfully');
      await loadModels();
    } catch (err) {
      alert(`Error: ${err instanceof Error ? err.message : 'Unknown error'}`);
    }
  };

  const handleVerifyModel = async (model: Model) => {
    try {
      const response = await fetch('http://127.0.0.1:5005/api/models/verify', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({
          filePath: model.filePath,
          expectedSha256: model.sha256,
        }),
      });

      if (!response.ok) throw new Error('Failed to verify model');
      const data = await response.json();

      if (data.isValid) {
        alert(`✓ Model verified: ${data.status}`);
      } else {
        alert(`✗ Verification failed: ${data.status}\nIssues: ${data.issues.join(', ')}`);
      }
    } catch (err) {
      alert(`Error: ${err instanceof Error ? err.message : 'Unknown error'}`);
    }
  };

  const getModelKindForEngine = (engineId: string): string => {
    if (engineId === 'stable-diffusion-webui' || engineId === 'comfyui') {
      return 'SD_BASE'; // Default, users can add different kinds separately
    } else if (engineId === 'piper') {
      return 'PIPER_VOICE';
    } else if (engineId === 'mimic3') {
      return 'MIMIC3_VOICE';
    }
    return 'SD_BASE';
  };

  const getModelKindLabel = (kind: string): string => {
    const labels: Record<string, string> = {
      SD_BASE: 'Base Model',
      SD_REFINER: 'Refiner',
      VAE: 'VAE',
      LORA: 'LoRA',
      PIPER_VOICE: 'Voice',
      MIMIC3_VOICE: 'Voice',
    };
    return labels[kind] || kind;
  };

  return (
    <Dialog open={open} onOpenChange={(_, data) => onOpenChange(data.open)}>
      <DialogSurface className={styles.dialog}>
        <DialogBody>
          <DialogTitle>Models & Voices - {engineName}</DialogTitle>
          <DialogContent className={styles.content}>
            {isLoading && <Spinner label="Loading models..." />}
            
            {error && (
              <Text style={{ color: tokens.colorPaletteRedForeground1 }}>
                Error: {error}
              </Text>
            )}

            {!isLoading && !error && models.length === 0 && (
              <div className={styles.emptyState}>
                <Text>No models found. Add an external folder or install models to get started.</Text>
              </div>
            )}

            {!isLoading && !error && models.length > 0 && (
              <div className={styles.table}>
                <Table>
                  <TableHeader>
                    <TableRow>
                      <TableHeaderCell>Name</TableHeaderCell>
                      <TableHeaderCell>Type</TableHeaderCell>
                      <TableHeaderCell>Size</TableHeaderCell>
                      <TableHeaderCell>Status</TableHeaderCell>
                      <TableHeaderCell>Actions</TableHeaderCell>
                    </TableRow>
                  </TableHeader>
                  <TableBody>
                    {models.map((model) => (
                      <TableRow
                        key={model.id}
                        className={styles.row}
                        onClick={() => setSelectedModel(model)}
                      >
                        <TableCell>
                          <Text weight="semibold">{model.name}</Text>
                          {model.isExternal && (
                            <Badge className={styles.externalBadge} appearance="tint" color="informative">
                              External
                            </Badge>
                          )}
                        </TableCell>
                        <TableCell>{getModelKindLabel(model.kind)}</TableCell>
                        <TableCell>{model.sizeFormatted}</TableCell>
                        <TableCell>
                          <Badge
                            appearance="filled"
                            color={model.verificationStatus === 'Valid' ? 'success' : 'warning'}
                          >
                            {model.verificationStatus || 'Unknown'}
                          </Badge>
                        </TableCell>
                        <TableCell>
                          <Menu>
                            <MenuTrigger disableButtonEnhancement>
                              <Button
                                appearance="subtle"
                                icon={<MoreHorizontal24Regular />}
                                size="small"
                              />
                            </MenuTrigger>
                            <MenuPopover>
                              <MenuList>
                                <MenuItem
                                  icon={<Folder24Regular />}
                                  onClick={() => handleOpenFolder(model.filePath)}
                                >
                                  Open Folder
                                </MenuItem>
                                <MenuItem
                                  icon={<Checkmark24Regular />}
                                  onClick={() => handleVerifyModel(model)}
                                >
                                  Verify
                                </MenuItem>
                                {!model.isExternal && (
                                  <MenuItem
                                    icon={<Delete24Regular />}
                                    onClick={() => handleRemoveModel(model)}
                                  >
                                    Remove
                                  </MenuItem>
                                )}
                              </MenuList>
                            </MenuPopover>
                          </Menu>
                        </TableCell>
                      </TableRow>
                    ))}
                  </TableBody>
                </Table>
              </div>
            )}

            {selectedModel && (
              <div>
                <Text className={styles.sectionHeader}>Model Details</Text>
                <Text className={styles.pathText}>
                  <strong>Path:</strong> {selectedModel.filePath || 'Unknown'}
                </Text>
                {selectedModel.provenance && (
                  <Text className={styles.pathText}>
                    <strong>Source:</strong> {selectedModel.provenance}
                  </Text>
                )}
                {selectedModel.sha256 && (
                  <Text className={styles.pathText}>
                    <strong>SHA256:</strong> {selectedModel.sha256}
                  </Text>
                )}
              </div>
            )}

            <div>
              <Text className={styles.sectionHeader}>Add External Folder</Text>
              <div className={styles.actions}>
                <Input
                  placeholder="C:\Users\YourName\Models\stable-diffusion"
                  value={newExternalPath}
                  onChange={(e) => setNewExternalPath(e.target.value)}
                  style={{ flexGrow: 1 }}
                />
                <Button
                  appearance="primary"
                  icon={<FolderAdd24Regular />}
                  onClick={handleAddExternalFolder}
                >
                  Add Folder
                </Button>
              </div>
              <Text className={styles.pathText}>
                <Info24Regular /> Add folders containing your existing models. Files stay in place - no copying.
              </Text>
            </div>

            {externalDirs.length > 0 && (
              <div>
                <Text className={styles.sectionHeader}>Configured External Folders</Text>
                {externalDirs
                  .filter(dir => dir.kind === getModelKindForEngine(engineId))
                  .map((dir, idx) => (
                    <div key={idx}>
                      <Text className={styles.pathText}>
                        📁 {dir.path}
                        {dir.isReadOnly && <Badge appearance="tint">Read-only</Badge>}
                      </Text>
                    </div>
                  ))}
              </div>
            )}
          </DialogContent>
          <DialogActions>
            <Button appearance="secondary" onClick={() => onOpenChange(false)}>
              Close
            </Button>
          </DialogActions>
        </DialogBody>
      </DialogSurface>
    </Dialog>
  );
}
