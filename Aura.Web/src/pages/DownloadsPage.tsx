import { useState, useEffect } from 'react';
import {
  makeStyles,
  tokens,
  Title1,
  Title2,
  Title3,
  Text,
  Button,
  Card,
  Table,
  TableHeader,
  TableRow,
  TableHeaderCell,
  TableBody,
  TableCell,
  Spinner,
  Badge,
  Menu,
  MenuTrigger,
  MenuPopover,
  MenuList,
  MenuItem,
  Dialog,
  DialogTrigger,
  DialogSurface,
  DialogTitle,
  DialogBody,
  DialogActions,
  DialogContent,
} from '@fluentui/react-components';
import { 
  CloudArrowDown24Regular, 
  CheckmarkCircle24Filled,
  ErrorCircle24Filled,
  MoreVertical20Regular,
  Wrench24Regular,
  Delete24Regular,
  FolderOpen24Regular,
  DocumentText24Regular,
} from '@fluentui/react-icons';

interface DependencyComponent {
  name: string;
  version: string;
  isRequired: boolean;
  files: Array<{
    filename: string;
    url: string;
    sha256: string;
    extractPath: string;
    sizeBytes: number;
  }>;
}

interface ComponentStatus {
  name: string;
  isInstalled: boolean;
  needsRepair: boolean;
  errorMessage?: string;
}

interface ComponentStatusState {
  [key: string]: {
    status: ComponentStatus | null;
    isInstalling: boolean;
    error?: string;
  };
}

interface ManualInstructions {
  componentName: string;
  version: string;
  targetDirectory: string;
  files: Array<{
    filename: string;
    url: string;
    sha256: string;
    sizeBytes: number;
    installPath: string;
  }>;
  instructions: string;
}

const useStyles = makeStyles({
  container: {
    maxWidth: '1200px',
    margin: '0 auto',
  },
  header: {
    marginBottom: tokens.spacingVerticalXXL,
  },
  subtitle: {
    color: tokens.colorNeutralForeground3,
    marginTop: tokens.spacingVerticalS,
  },
  card: {
    padding: tokens.spacingVerticalXL,
  },
  tableContainer: {
    marginTop: tokens.spacingVerticalL,
  },
  statusCell: {
    display: 'flex',
    alignItems: 'center',
    gap: tokens.spacingHorizontalS,
  },
  nameCell: {
    display: 'flex',
    flexDirection: 'column',
    gap: tokens.spacingVerticalXXS,
  },
  actionsCell: {
    display: 'flex',
    gap: tokens.spacingHorizontalS,
    alignItems: 'center',
  },
  dialogContent: {
    display: 'flex',
    flexDirection: 'column',
    gap: tokens.spacingVerticalM,
  },
  instructionBox: {
    padding: tokens.spacingVerticalM,
    backgroundColor: tokens.colorNeutralBackground3,
    borderRadius: tokens.borderRadiusMedium,
    fontFamily: 'monospace',
    fontSize: '12px',
  },
});

export function DownloadsPage() {
  const styles = useStyles();
  const [manifest, setManifest] = useState<DependencyComponent[]>([]);
  const [loading, setLoading] = useState(true);
  const [componentStatus, setComponentStatus] = useState<ComponentStatusState>({});
  const [offlineMode, setOfflineMode] = useState(false);
  const [manualInstructions, setManualInstructions] = useState<ManualInstructions | null>(null);
  const [showInstructionsDialog, setShowInstructionsDialog] = useState(false);

  useEffect(() => {
    fetchManifest();
  }, []);

  const fetchManifest = async () => {
    try {
      const response = await fetch('/api/downloads/manifest');
      if (response.ok) {
        const data = await response.json();
        setManifest(data.components || []);
        
        // Check installation status for each component
        if (data.components) {
          for (const component of data.components) {
            checkComponentStatus(component.name);
          }
        }
      }
    } catch (error) {
      console.error('Error fetching manifest:', error);
    } finally {
      setLoading(false);
    }
  };

  const checkComponentStatus = async (componentName: string) => {
    try {
      const response = await fetch(`/api/downloads/${componentName}/status`);
      if (response.ok) {
        const data = await response.json();
        setComponentStatus(prev => ({
          ...prev,
          [componentName]: {
            status: data,
            isInstalling: false,
          },
        }));
      }
    } catch (error) {
      console.error(`Error checking status for ${componentName}:`, error);
    }
  };

  const installComponent = async (componentName: string) => {
    try {
      // Update status to installing
      setComponentStatus(prev => ({
        ...prev,
        [componentName]: {
          status: prev[componentName]?.status || null,
          isInstalling: true,
        },
      }));

      const response = await fetch(`/api/downloads/${componentName}/install`, {
        method: 'POST',
      });

      if (response.ok) {
        // Refresh status
        await checkComponentStatus(componentName);
      } else {
        const errorData = await response.json();
        setComponentStatus(prev => ({
          ...prev,
          [componentName]: {
            status: prev[componentName]?.status || null,
            isInstalling: false,
            error: errorData.message || 'Installation failed',
          },
        }));
      }
    } catch (error) {
      console.error(`Error installing ${componentName}:`, error);
      setComponentStatus(prev => ({
        ...prev,
        [componentName]: {
          status: prev[componentName]?.status || null,
          isInstalling: false,
          error: 'Network error',
        },
      }));
    }
  };

  const repairComponent = async (componentName: string) => {
    try {
      setComponentStatus(prev => ({
        ...prev,
        [componentName]: {
          status: prev[componentName]?.status || null,
          isInstalling: true,
        },
      }));

      const response = await fetch(`/api/downloads/${componentName}/repair`, {
        method: 'POST',
      });

      if (response.ok) {
        await checkComponentStatus(componentName);
      } else {
        const errorData = await response.json();
        setComponentStatus(prev => ({
          ...prev,
          [componentName]: {
            status: prev[componentName]?.status || null,
            isInstalling: false,
            error: errorData.message || 'Repair failed',
          },
        }));
      }
    } catch (error) {
      console.error(`Error repairing ${componentName}:`, error);
      setComponentStatus(prev => ({
        ...prev,
        [componentName]: {
          status: prev[componentName]?.status || null,
          isInstalling: false,
          error: 'Network error',
        },
      }));
    }
  };

  const removeComponent = async (componentName: string) => {
    if (!confirm(`Are you sure you want to remove ${componentName}?`)) {
      return;
    }

    try {
      const response = await fetch(`/api/downloads/${componentName}`, {
        method: 'DELETE',
      });

      if (response.ok) {
        await checkComponentStatus(componentName);
      } else {
        alert('Failed to remove component');
      }
    } catch (error) {
      console.error(`Error removing ${componentName}:`, error);
      alert('Network error');
    }
  };

  const openFolder = async () => {
    try {
      const response = await fetch('/api/downloads/directory');
      if (response.ok) {
        const data = await response.json();
        alert(`Download directory: ${data.directory}\n\nNote: You can open this folder manually from your file explorer.`);
      }
    } catch (error) {
      console.error('Error getting directory:', error);
    }
  };

  const showManualInstructions = async (componentName: string) => {
    try {
      const response = await fetch(`/api/downloads/${componentName}/manual-instructions`);
      if (response.ok) {
        const data = await response.json();
        setManualInstructions(data);
        setShowInstructionsDialog(true);
      }
    } catch (error) {
      console.error(`Error getting manual instructions for ${componentName}:`, error);
    }
  };

  const verifyManualInstall = async (componentName: string) => {
    try {
      const response = await fetch(`/api/downloads/${componentName}/verify`, {
        method: 'POST',
      });

      if (response.ok) {
        const result = await response.json();
        if (result.isValid) {
          alert(`✓ All files verified successfully for ${componentName}`);
          await checkComponentStatus(componentName);
        } else {
          const failedFiles = result.files
            .filter((f: any) => !f.isValid)
            .map((f: any) => `${f.filename}: ${f.errorMessage}`)
            .join('\n');
          alert(`Verification failed:\n\n${failedFiles}`);
        }
      }
    } catch (error) {
      console.error(`Error verifying ${componentName}:`, error);
      alert('Network error');
    }
  };

  const getStatusDisplay = (componentName: string) => {
    const statusData = componentStatus[componentName];
    
    if (!statusData || !statusData.status) {
      return <Spinner size="tiny" />;
    }
    
    if (statusData.isInstalling) {
      return (
        <div className={styles.statusCell}>
          <Spinner size="tiny" />
          <Text>Installing...</Text>
        </div>
      );
    }
    
    const status = statusData.status;
    
    if (statusData.error) {
      return (
        <div className={styles.statusCell}>
          <ErrorCircle24Filled color={tokens.colorPaletteRedForeground1} />
          <Text>{statusData.error}</Text>
        </div>
      );
    }
    
    if (status.needsRepair) {
      return (
        <div className={styles.statusCell}>
          <ErrorCircle24Filled color={tokens.colorPaletteYellowForeground1} />
          <Badge color="warning" appearance="filled">Needs Repair</Badge>
        </div>
      );
    }
    
    if (status.isInstalled) {
      return (
        <div className={styles.statusCell}>
          <CheckmarkCircle24Filled color={tokens.colorPaletteGreenForeground1} />
          <Badge color="success" appearance="filled">Installed</Badge>
        </div>
      );
    }
    
    return (
      <Badge color="warning" appearance="outline">Not installed</Badge>
    );
  };

  return (
    <div className={styles.container}>
      <div className={styles.header}>
        <Title1>Download Center</Title1>
        <Text className={styles.subtitle}>
          Manage dependencies and external tools required for video production
        </Text>
      </div>

      <Card className={styles.card} style={{ marginBottom: tokens.spacingVerticalL }}>
        <div style={{ display: 'flex', gap: tokens.spacingHorizontalM, justifyContent: 'space-between', alignItems: 'center' }}>
          <div style={{ flex: 1 }}>
            <Title2>Download Mode</Title2>
            <Text>
              {offlineMode 
                ? 'Offline mode: Manual placement instructions with checksum verification'
                : 'Online mode: Automatic downloads with resume support'}
            </Text>
          </div>
          <div style={{ display: 'flex', gap: tokens.spacingHorizontalM }}>
            <Button
              appearance={offlineMode ? 'outline' : 'primary'}
              onClick={() => setOfflineMode(false)}
            >
              Online
            </Button>
            <Button
              appearance={offlineMode ? 'primary' : 'outline'}
              onClick={() => setOfflineMode(true)}
            >
              Offline
            </Button>
            <Button
              appearance="subtle"
              icon={<FolderOpen24Regular />}
              onClick={openFolder}
            >
              Open Folder
            </Button>
          </div>
        </div>
      </Card>

      <Card className={styles.card} style={{ marginBottom: tokens.spacingVerticalL, backgroundColor: tokens.colorNeutralBackground3 }}>
        <div style={{ display: 'flex', gap: tokens.spacingHorizontalM, alignItems: 'flex-start' }}>
          <div style={{ flex: 1 }}>
            <Title2>Need to configure local AI tools?</Title2>
            <Text>
              After downloading components here, configure their paths and URLs in <strong>Settings → Local Providers</strong>.
              You can test connections and set custom paths for Stable Diffusion, Ollama, and FFmpeg.
            </Text>
          </div>
        </div>
      </Card>

      {loading ? (
        <Card className={styles.card}>
          <Spinner label="Loading dependencies..." />
        </Card>
      ) : (
        <Card className={styles.card}>
          <Title2>Available Components</Title2>
          <div className={styles.tableContainer}>
            <Table>
              <TableHeader>
                <TableRow>
                  <TableHeaderCell>Component</TableHeaderCell>
                  <TableHeaderCell>Version</TableHeaderCell>
                  <TableHeaderCell>Size</TableHeaderCell>
                  <TableHeaderCell>Status</TableHeaderCell>
                  <TableHeaderCell>Actions</TableHeaderCell>
                </TableRow>
              </TableHeader>
              <TableBody>
                {manifest.map((component) => {
                  const totalSize = component.files.reduce((sum, file) => sum + file.sizeBytes, 0);
                  const statusData = componentStatus[component.name];
                  const status = statusData?.status;
                  
                  return (
                    <TableRow key={component.name}>
                      <TableCell>
                        <div className={styles.nameCell}>
                          <Text weight="semibold">{component.name}</Text>
                          {component.isRequired && (
                            <Badge color="danger" appearance="outline" size="small">
                              Required
                            </Badge>
                          )}
                        </div>
                      </TableCell>
                      <TableCell>
                        <Text>{component.version}</Text>
                      </TableCell>
                      <TableCell>
                        <Text>{(totalSize / 1024 / 1024).toFixed(1)} MB</Text>
                      </TableCell>
                      <TableCell>
                        {getStatusDisplay(component.name)}
                      </TableCell>
                      <TableCell>
                        <div className={styles.actionsCell}>
                          {offlineMode ? (
                            <>
                              <Button
                                size="small"
                                appearance="primary"
                                icon={<DocumentText24Regular />}
                                onClick={() => showManualInstructions(component.name)}
                              >
                                Instructions
                              </Button>
                              {status?.isInstalled && (
                                <Button
                                  size="small"
                                  appearance="outline"
                                  onClick={() => verifyManualInstall(component.name)}
                                >
                                  Verify
                                </Button>
                              )}
                            </>
                          ) : (
                            <>
                              {!status?.isInstalled && !status?.needsRepair && (
                                <Button
                                  size="small"
                                  appearance="primary"
                                  icon={<CloudArrowDown24Regular />}
                                  onClick={() => installComponent(component.name)}
                                  disabled={statusData?.isInstalling}
                                >
                                  {statusData?.isInstalling ? 'Installing...' : 'Install'}
                                </Button>
                              )}
                              {status?.needsRepair && (
                                <Button
                                  size="small"
                                  appearance="primary"
                                  icon={<Wrench24Regular />}
                                  onClick={() => repairComponent(component.name)}
                                  disabled={statusData?.isInstalling}
                                >
                                  {statusData?.isInstalling ? 'Repairing...' : 'Repair'}
                                </Button>
                              )}
                              {(status?.isInstalled || status?.needsRepair) && (
                                <Menu>
                                  <MenuTrigger disableButtonEnhancement>
                                    <Button
                                      size="small"
                                      appearance="subtle"
                                      icon={<MoreVertical20Regular />}
                                    />
                                  </MenuTrigger>
                                  <MenuPopover>
                                    <MenuList>
                                      <MenuItem
                                        icon={<Wrench24Regular />}
                                        onClick={() => repairComponent(component.name)}
                                      >
                                        Repair
                                      </MenuItem>
                                      <MenuItem
                                        icon={<Delete24Regular />}
                                        onClick={() => removeComponent(component.name)}
                                      >
                                        Remove
                                      </MenuItem>
                                    </MenuList>
                                  </MenuPopover>
                                </Menu>
                              )}
                            </>
                          )}
                        </div>
                      </TableCell>
                    </TableRow>
                  );
                })}
              </TableBody>
            </Table>
          </div>
        </Card>
      )}

      <Dialog open={showInstructionsDialog} onOpenChange={(_, data) => setShowInstructionsDialog(data.open)}>
        <DialogSurface>
          <DialogBody>
            <DialogTitle>Manual Installation Instructions</DialogTitle>
            <DialogContent className={styles.dialogContent}>
              {manualInstructions && (
                <>
                  <div>
                    <Title3>{manualInstructions.componentName} v{manualInstructions.version}</Title3>
                    <Text>{manualInstructions.instructions}</Text>
                  </div>
                  
                  <div>
                    <Text weight="semibold">Target Directory:</Text>
                    <div className={styles.instructionBox}>
                      {manualInstructions.targetDirectory}
                    </div>
                  </div>

                  <div>
                    <Text weight="semibold">Files to Download:</Text>
                    {manualInstructions.files.map((file) => (
                      <div key={file.filename} style={{ marginTop: tokens.spacingVerticalM }}>
                        <Text weight="semibold">{file.filename}</Text>
                        <div className={styles.instructionBox}>
                          <div>URL: {file.url}</div>
                          <div>Size: {(file.sizeBytes / 1024 / 1024).toFixed(1)} MB</div>
                          <div>SHA-256: {file.sha256}</div>
                          <div>Install to: {file.installPath}</div>
                        </div>
                      </div>
                    ))}
                  </div>
                </>
              )}
            </DialogContent>
            <DialogActions>
              <Button appearance="secondary" onClick={() => setShowInstructionsDialog(false)}>
                Close
              </Button>
              {manualInstructions && (
                <Button 
                  appearance="primary" 
                  onClick={() => {
                    setShowInstructionsDialog(false);
                    verifyManualInstall(manualInstructions.componentName);
                  }}
                >
                  Verify Installation
                </Button>
              )}
            </DialogActions>
          </DialogBody>
        </DialogSurface>
      </Dialog>
    </div>
  );
}
