import {
  makeStyles,
  tokens,
  Title1,
  Title2,
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
  TabList,
  Tab,
  SelectTabEvent,
  SelectTabData,
} from '@fluentui/react-components';
import {
  CloudArrowDown24Regular,
  CheckmarkCircle24Filled,
  ErrorCircle24Filled,
  ArrowSync24Regular,
  Settings24Regular,
} from '@fluentui/react-icons';
import { useState, useEffect, useCallback } from 'react';
import { useSearchParams, useNavigate } from 'react-router-dom';
import { EnginesTab } from '../components/Engines/EnginesTab';
import { OfflineModeRecommendations } from '../components/Engines/OfflineModeRecommendations';
import { OfflineProviderStatus } from '../components/Engines/OfflineProviderStatus';
import { TroubleshootingPanel } from '../components/Engines/TroubleshootingPanel';
import { RouteErrorBoundary } from '../components/ErrorBoundary/RouteErrorBoundary';
import { useNotifications } from '../components/Notifications/Toasts';
import { apiUrl } from '../config/api';
import { RescanPanel } from './DownloadCenter/RescanPanel';

interface DependencyComponent {
  name: string;
  version: string;
  isRequired: boolean;
  installPath: string;
  postInstallProbe?: string;
  files: Array<{
    filename: string;
    url: string;
    sha256: string;
    extractPath: string;
    sizeBytes: number;
  }>;
}

interface ComponentStatus {
  [key: string]: {
    isInstalled: boolean;
    isInstalling: boolean;
    isRepairing: boolean;
    needsRepair: boolean;
    error?: string;
    verificationResult?: VerificationResult;
  };
}

interface VerificationResult {
  componentName: string;
  isValid: boolean;
  status: string;
  missingFiles: string[];
  corruptedFiles: string[];
  probeResult?: string;
}

interface ManualInstructions {
  componentName: string;
  version: string;
  installPath: string;
  steps: string[];
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
  emptyState: {
    display: 'flex',
    flexDirection: 'column',
    alignItems: 'center',
    justifyContent: 'center',
    padding: tokens.spacingVerticalXXXL,
    gap: tokens.spacingVerticalL,
    textAlign: 'center',
  },
  emptyStateIcon: {
    fontSize: '48px',
    color: tokens.colorNeutralForeground3,
  },
  emptyStateActions: {
    display: 'flex',
    gap: tokens.spacingHorizontalM,
    marginTop: tokens.spacingVerticalM,
  },
});

export function DownloadsPage() {
  const styles = useStyles();
  const { showSuccessToast, showFailureToast } = useNotifications();
  const [searchParams] = useSearchParams();
  const navigate = useNavigate();
  const [manifest, setManifest] = useState<DependencyComponent[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [componentStatus, setComponentStatus] = useState<ComponentStatus>({});
  const [selectedTab, setSelectedTab] = useState<string>('dependencies');

  const onTabSelect = (_: SelectTabEvent, data: SelectTabData) => {
    setSelectedTab(data.value as string);
  };

  // Helper function to extract error message from response
  const extractErrorMessage = async (
    response: Response,
    defaultMessage: string
  ): Promise<string> => {
    try {
      const errorData = await response.json();
      return errorData.message || errorData.error || defaultMessage;
    } catch {
      return `${defaultMessage} (HTTP ${response.status})`;
    }
  };

  // Helper function to get error message from caught errors
  const getErrorMessage = (error: unknown, fallback = 'Unknown error occurred'): string => {
    if (error instanceof Error) {
      return error.message;
    }
    return fallback;
  };

  // Helper to verify component integrity
  const verifyComponentIntegrity = useCallback(
    async (
      componentName: string,
      isInstalled: boolean
    ): Promise<{ needsRepair: boolean; verificationResult?: VerificationResult } | null> => {
      if (!isInstalled) {
        return null;
      }

      try {
        const verifyResponse = await fetch(apiUrl(`/api/downloads/${componentName}/verify`));
        if (verifyResponse.ok) {
          const verifyData = (await verifyResponse.json()) as VerificationResult;
          return {
            needsRepair: !verifyData.isValid,
            verificationResult: verifyData,
          };
        }
      } catch (error) {
        console.error(`Error verifying ${componentName}:`, error);
      }

      return null;
    },
    []
  );

  const checkComponentStatus = useCallback(
    async (componentName: string) => {
      try {
        const statusResponse = await fetch(apiUrl(`/api/downloads/${componentName}/status`));
        if (!statusResponse.ok) {
          return;
        }

        const statusData = await statusResponse.json();
        const verificationInfo = await verifyComponentIntegrity(
          componentName,
          statusData.isInstalled
        );

        setComponentStatus((prev) => ({
          ...prev,
          [componentName]: {
            isInstalled: statusData.isInstalled,
            isInstalling: false,
            isRepairing: false,
            needsRepair: verificationInfo?.needsRepair || false,
            verificationResult: verificationInfo?.verificationResult,
          },
        }));
      } catch (error) {
        console.error(`Error checking status for ${componentName}:`, error);
      }
    },
    [verifyComponentIntegrity]
  );

  const fetchManifest = useCallback(async () => {
    try {
      setError(null);
      setLoading(true);
      const response = await fetch(apiUrl('/api/downloads/manifest'));
      if (response.ok) {
        const data = await response.json();
        setManifest(data);
        // Check status for each component in parallel
        await Promise.all(
          data.map((component: DependencyComponent) => checkComponentStatus(component.name))
        );
      } else {
        const errorMsg = `Server returned HTTP ${response.status} error. The backend may not be running.`;
        setError(errorMsg);
        showFailureToast({
          title: 'Failed to Load Manifest',
          message: errorMsg,
        });
      }
    } catch (error) {
      console.error('Error loading manifest:', error);
      const errorMsg =
        error instanceof Error ? error.message : 'Failed to connect to backend server';
      setError(errorMsg);
      showFailureToast({
        title: 'Error Loading Manifest',
        message: errorMsg,
      });
    } finally {
      setLoading(false);
    }
  }, [showFailureToast, checkComponentStatus]);

  useEffect(() => {
    fetchManifest();
  }, [fetchManifest]);

  const showManualInstructions = useCallback(
    async (componentName: string) => {
      try {
        const response = await fetch(apiUrl(`/api/downloads/${componentName}/manual`));
        if (response.ok) {
          const data: ManualInstructions = await response.json();

          // Defensive check: ensure steps is an array
          const steps = Array.isArray(data.steps) ? data.steps : [];

          if (steps.length === 0) {
            showFailureToast({
              title: 'No Instructions Available',
              message: `Manual installation instructions for ${componentName} are not available at this time.`,
            });
            return;
          }

          const instructionsText = [
            `Manual Installation Instructions for ${data.componentName} v${data.version}`,
            '',
            `Install Path: ${data.installPath}`,
            '',
            ...steps,
          ].join('\n');
          showSuccessToast({
            title: 'Manual Installation Instructions',
            message: instructionsText,
          });
        } else {
          showFailureToast({
            title: 'Error',
            message: 'Failed to get manual installation instructions',
          });
        }
      } catch (error) {
        console.error(`Error getting manual instructions for ${componentName}:`, error);
        showFailureToast({
          title: 'Error',
          message: getErrorMessage(error, 'Failed to get manual instructions'),
        });
      }
    },
    [showSuccessToast, showFailureToast]
  );

  // Handle ?item= query parameter to auto-show manual instructions
  useEffect(() => {
    const itemParam = searchParams.get('item');
    if (itemParam && !loading) {
      // Show manual instructions for the specified component
      showManualInstructions(itemParam);
    }
  }, [searchParams, loading, showManualInstructions]);

  const installComponent = async (componentName: string) => {
    try {
      // Update status to installing
      setComponentStatus((prev) => ({
        ...prev,
        [componentName]: {
          isInstalled: false,
          isInstalling: true,
          isRepairing: false,
          needsRepair: false,
        },
      }));

      const response = await fetch(apiUrl(`/api/downloads/${componentName}/install`), {
        method: 'POST',
      });

      if (response.ok) {
        showSuccessToast({
          title: 'Installation Started',
          message: `${componentName} is being installed. This may take a few minutes.`,
        });
        // Check status again after install
        await checkComponentStatus(componentName);
      } else {
        const errorMessage = await extractErrorMessage(response, 'Installation failed');

        showFailureToast({
          title: 'Installation Failed',
          message: errorMessage,
        });

        setComponentStatus((prev) => ({
          ...prev,
          [componentName]: {
            isInstalled: false,
            isInstalling: false,
            isRepairing: false,
            needsRepair: false,
            error: errorMessage,
          },
        }));
      }
    } catch (error) {
      console.error(`Error installing ${componentName}:`, error);
      const errorMessage = getErrorMessage(error, 'Network error');

      showFailureToast({
        title: 'Installation Error',
        message: errorMessage,
      });

      setComponentStatus((prev) => ({
        ...prev,
        [componentName]: {
          isInstalled: false,
          isInstalling: false,
          isRepairing: false,
          needsRepair: false,
          error: errorMessage,
        },
      }));
    }
  };

  const repairComponent = async (componentName: string) => {
    try {
      setComponentStatus((prev) => ({
        ...prev,
        [componentName]: {
          ...prev[componentName],
          isRepairing: true,
          error: undefined,
        },
      }));

      const response = await fetch(apiUrl(`/api/downloads/${componentName}/repair`), {
        method: 'POST',
      });

      if (response.ok) {
        showSuccessToast({
          title: 'Repair Complete',
          message: `${componentName} has been repaired successfully.`,
        });
        await checkComponentStatus(componentName);
      } else {
        const errorMessage = await extractErrorMessage(response, 'Repair failed');

        showFailureToast({
          title: 'Repair Failed',
          message: errorMessage,
        });

        setComponentStatus((prev) => ({
          ...prev,
          [componentName]: {
            ...prev[componentName],
            isRepairing: false,
            error: errorMessage,
          },
        }));
      }
    } catch (error) {
      console.error(`Error repairing ${componentName}:`, error);
      const errorMessage = getErrorMessage(error, 'Network error during repair');

      showFailureToast({
        title: 'Repair Error',
        message: errorMessage,
      });

      setComponentStatus((prev) => ({
        ...prev,
        [componentName]: {
          ...prev[componentName],
          isRepairing: false,
          error: errorMessage,
        },
      }));
    }
  };

  const removeComponent = async (componentName: string) => {
    if (!confirm(`Are you sure you want to remove ${componentName}?`)) {
      return;
    }

    try {
      const response = await fetch(apiUrl(`/api/downloads/${componentName}`), {
        method: 'DELETE',
      });

      if (response.ok) {
        showSuccessToast({
          title: 'Component Removed',
          message: `${componentName} has been removed successfully.`,
        });
        await checkComponentStatus(componentName);
      } else {
        const errorMessage = await extractErrorMessage(response, 'Failed to remove component');

        showFailureToast({
          title: 'Remove Failed',
          message: errorMessage,
        });
      }
    } catch (error) {
      console.error(`Error removing ${componentName}:`, error);
      showFailureToast({
        title: 'Network Error',
        message: getErrorMessage(error, 'Network error during removal'),
      });
    }
  };

  const openComponentFolder = async (componentName: string) => {
    try {
      const response = await fetch(apiUrl(`/api/downloads/${componentName}/folder`));
      if (response.ok) {
        const data = await response.json();
        showSuccessToast({
          title: 'Component Folder',
          message: `Path: ${data.path}\n\nPlease navigate to this path manually in your file explorer.`,
        });
      } else {
        showFailureToast({
          title: 'Error',
          message: 'Failed to get component folder path',
        });
      }
    } catch (error) {
      console.error(`Error getting folder for ${componentName}:`, error);
      showFailureToast({
        title: 'Error',
        message: getErrorMessage(error, 'Failed to get component folder'),
      });
    }
  };

  const getStatusDisplay = (componentName: string) => {
    const status = componentStatus[componentName];

    if (!status) {
      return <Spinner size="tiny" />;
    }

    if (status.isInstalling) {
      return (
        <div className={styles.statusCell}>
          <Spinner size="tiny" />
          <Text>Installing...</Text>
        </div>
      );
    }

    if (status.isRepairing) {
      return (
        <div className={styles.statusCell}>
          <Spinner size="tiny" />
          <Text>Repairing...</Text>
        </div>
      );
    }

    if (status.error) {
      return (
        <div className={styles.statusCell}>
          <ErrorCircle24Filled color={tokens.colorPaletteRedForeground1} />
          <Text>{status.error}</Text>
        </div>
      );
    }

    if (status.needsRepair) {
      return (
        <div className={styles.statusCell}>
          <ErrorCircle24Filled color={tokens.colorPaletteYellowForeground1} />
          <Badge color="warning" appearance="filled">
            Needs Repair
          </Badge>
        </div>
      );
    }

    if (status.isInstalled) {
      return (
        <div className={styles.statusCell}>
          <CheckmarkCircle24Filled color={tokens.colorPaletteGreenForeground1} />
          <Badge color="success" appearance="filled">
            Installed
          </Badge>
          {status.verificationResult?.probeResult && (
            <Text size={200} style={{ color: tokens.colorNeutralForeground3 }}>
              ({status.verificationResult.probeResult})
            </Text>
          )}
        </div>
      );
    }

    return (
      <Badge color="warning" appearance="outline">
        Not installed
      </Badge>
    );
  };

  return (
    <RouteErrorBoundary onRetry={fetchManifest}>
      <div className={styles.container}>
        <div className={styles.header}>
          <Title1>Download Center</Title1>
          <Text className={styles.subtitle}>
            Manage dependencies, engines, and external tools required for video production
          </Text>
        </div>

        <TabList
          selectedValue={selectedTab}
          onTabSelect={onTabSelect}
          style={{ marginBottom: tokens.spacingVerticalL }}
        >
          <Tab value="dependencies">Dependencies</Tab>
          <Tab value="engines">Engines</Tab>
          <Tab value="offline-mode">Offline Mode</Tab>
          <Tab value="troubleshooting">Troubleshooting</Tab>
        </TabList>

        {selectedTab === 'dependencies' && (
          <>
            <Card
              className={styles.card}
              style={{
                marginBottom: tokens.spacingVerticalL,
                backgroundColor: tokens.colorNeutralBackground3,
              }}
            >
              <div
                style={{
                  display: 'flex',
                  gap: tokens.spacingHorizontalM,
                  alignItems: 'flex-start',
                }}
              >
                <div style={{ flex: 1 }}>
                  <Title2>Download & Manage Dependencies</Title2>
                  <Text style={{ marginTop: tokens.spacingVerticalS }}>
                    This page helps you download and manage all required components for video
                    production. After downloading, configure their paths in{' '}
                    <strong>Settings → Local Providers</strong>.
                  </Text>
                  <Text style={{ marginTop: tokens.spacingVerticalS }}>
                    <strong>Features:</strong>
                  </Text>
                  <ul
                    style={{
                      marginTop: tokens.spacingVerticalXS,
                      marginLeft: tokens.spacingHorizontalL,
                    }}
                  >
                    <li>✓ SHA-256 checksum verification for all downloads</li>
                    <li>✓ Automatic resume support for interrupted downloads</li>
                    <li>✓ Repair corrupted or incomplete installations</li>
                    <li>✓ Post-install validation checks</li>
                    <li>
                      ✓ Offline mode: Click &quot;Manual&quot; for manual installation instructions
                    </li>
                  </ul>
                </div>
              </div>
            </Card>

            <RescanPanel />

            {loading ? (
              <Card className={styles.card}>
                <Spinner label="Loading dependencies..." />
              </Card>
            ) : error ? (
              <Card className={styles.card}>
                <div className={styles.emptyState}>
                  <ErrorCircle24Filled className={styles.emptyStateIcon} />
                  <div>
                    <Title2>Unable to Load Dependencies</Title2>
                    <Text style={{ marginTop: tokens.spacingVerticalS }}>{error}</Text>
                    <Text
                      style={{
                        marginTop: tokens.spacingVerticalM,
                        color: tokens.colorNeutralForeground3,
                      }}
                    >
                      The backend server may not be running. Please ensure the Aura.Api service is
                      started.
                    </Text>
                  </div>
                  <div className={styles.emptyStateActions}>
                    <Button
                      appearance="primary"
                      icon={<ArrowSync24Regular />}
                      onClick={fetchManifest}
                    >
                      Retry
                    </Button>
                    <Button
                      appearance="subtle"
                      icon={<Settings24Regular />}
                      onClick={() => navigate('/settings')}
                    >
                      Go to Settings
                    </Button>
                  </div>
                </div>
              </Card>
            ) : manifest.length === 0 ? (
              <Card className={styles.card}>
                <div className={styles.emptyState}>
                  <CloudArrowDown24Regular className={styles.emptyStateIcon} />
                  <div>
                    <Title2>No Dependencies Configured</Title2>
                    <Text style={{ marginTop: tokens.spacingVerticalS }}>
                      Dependencies have not been configured yet.
                    </Text>
                    <Text
                      style={{
                        marginTop: tokens.spacingVerticalM,
                        color: tokens.colorNeutralForeground3,
                      }}
                    >
                      Complete the setup wizard to configure dependencies, or set them up manually
                      in Settings.
                    </Text>
                  </div>
                  <div className={styles.emptyStateActions}>
                    <Button appearance="primary" onClick={() => navigate('/setup')}>
                      Launch Setup Wizard
                    </Button>
                    <Button
                      appearance="subtle"
                      icon={<Settings24Regular />}
                      onClick={() => navigate('/settings')}
                    >
                      Go to Settings
                    </Button>
                  </div>
                </div>
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
                        const totalSize = component.files.reduce(
                          (sum, file) => sum + file.sizeBytes,
                          0
                        );
                        const status = componentStatus[component.name];

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
                            <TableCell>{getStatusDisplay(component.name)}</TableCell>
                            <TableCell>
                              <div
                                style={{
                                  display: 'flex',
                                  gap: tokens.spacingHorizontalXS,
                                  flexWrap: 'wrap',
                                }}
                              >
                                {!status?.isInstalled ? (
                                  <>
                                    <Button
                                      size="small"
                                      appearance="primary"
                                      icon={<CloudArrowDown24Regular />}
                                      onClick={() => installComponent(component.name)}
                                      disabled={status?.isInstalling || status?.isRepairing}
                                    >
                                      {status?.isInstalling ? 'Installing...' : 'Install'}
                                    </Button>
                                    <Button
                                      size="small"
                                      appearance="subtle"
                                      onClick={() => showManualInstructions(component.name)}
                                    >
                                      Manual
                                    </Button>
                                  </>
                                ) : (
                                  <>
                                    {status?.needsRepair && (
                                      <Button
                                        size="small"
                                        appearance="primary"
                                        onClick={() => repairComponent(component.name)}
                                        disabled={status?.isRepairing}
                                      >
                                        {status?.isRepairing ? 'Repairing...' : 'Repair'}
                                      </Button>
                                    )}
                                    <Button
                                      size="small"
                                      appearance="subtle"
                                      onClick={() => openComponentFolder(component.name)}
                                    >
                                      Open Folder
                                    </Button>
                                    <Button
                                      size="small"
                                      appearance="subtle"
                                      onClick={() => removeComponent(component.name)}
                                    >
                                      Remove
                                    </Button>
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
          </>
        )}

        {selectedTab === 'engines' && <EnginesTab />}

        {selectedTab === 'offline-mode' && (
          <>
            <OfflineModeRecommendations />
            <OfflineProviderStatus />
          </>
        )}

        {selectedTab === 'troubleshooting' && (
          <Card className={styles.card}>
            <TroubleshootingPanel />
          </Card>
        )}
      </div>
    </RouteErrorBoundary>
  );
}
