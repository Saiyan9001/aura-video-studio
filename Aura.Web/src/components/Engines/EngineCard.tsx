import { useState, useEffect } from 'react';
import {
  Card,
  CardHeader,
  CardPreview,
  Button,
  Text,
  Badge,
  Spinner,
  makeStyles,
  tokens,
  Menu,
  MenuTrigger,
  MenuPopover,
  MenuList,
  MenuItem,
  Link,
  Dialog,
  DialogSurface,
  DialogBody,
  DialogTitle,
  DialogContent,
  DialogActions,
} from '@fluentui/react-components';
import {
  Play24Regular,
  Stop24Regular,
  ArrowDownload24Regular,
  Checkmark24Regular,
  Warning24Regular,
  Wrench24Regular,
  Delete24Regular,
  Folder24Regular,
  MoreHorizontal24Regular,
  Info24Regular,
  DocumentFolder24Regular,
} from '@fluentui/react-icons';
import type { EngineManifestEntry, EngineStatus } from '../../types/engines';
import { useEnginesStore } from '../../state/engines';
import { ModelManager } from './ModelManager';

const useStyles = makeStyles({
  card: {
    width: '100%',
    maxWidth: '800px',
    marginBottom: tokens.spacingVerticalM,
  },
  header: {
    display: 'flex',
    justifyContent: 'space-between',
    alignItems: 'center',
  },
  title: {
    display: 'flex',
    alignItems: 'center',
    gap: tokens.spacingHorizontalS,
  },
  content: {
    padding: tokens.spacingVerticalM,
    display: 'flex',
    flexDirection: 'column',
    gap: tokens.spacingVerticalS,
  },
  row: {
    display: 'flex',
    justifyContent: 'space-between',
    alignItems: 'center',
  },
  actions: {
    display: 'flex',
    gap: tokens.spacingHorizontalS,
    flexWrap: 'wrap',
  },
  statusBadge: {
    marginLeft: tokens.spacingHorizontalS,
  },
  metadata: {
    color: tokens.colorNeutralForeground3,
    fontSize: tokens.fontSizeBase200,
  },
  messages: {
    display: 'flex',
    flexDirection: 'column',
    gap: tokens.spacingVerticalXS,
    marginTop: tokens.spacingVerticalS,
  },
  message: {
    color: tokens.colorPaletteRedForeground1,
    fontSize: tokens.fontSizeBase200,
  },
  diagnosticsDialog: {
    minWidth: '500px',
  },
  diagnosticsContent: {
    display: 'flex',
    flexDirection: 'column',
    gap: tokens.spacingVerticalM,
  },
  diagnosticsItem: {
    display: 'flex',
    justifyContent: 'space-between',
    paddingBottom: tokens.spacingVerticalXS,
    borderBottom: `1px solid ${tokens.colorNeutralStroke2}`,
  },
  diagnosticsLabel: {
    fontWeight: 600,
  },
  diagnosticsIssues: {
    padding: tokens.spacingVerticalS,
    backgroundColor: tokens.colorPaletteRedBackground1,
    borderRadius: tokens.borderRadiusMedium,
    marginTop: tokens.spacingVerticalS,
  },
});

interface EngineCardProps {
  engine: EngineManifestEntry;
}

export function EngineCard({ engine }: EngineCardProps) {
  const styles = useStyles();
  const [status, setStatus] = useState<EngineStatus | null>(null);
  const [isProcessing, setIsProcessing] = useState(false);
  const [showDiagnostics, setShowDiagnostics] = useState(false);
  const [diagnosticsData, setDiagnosticsData] = useState<any>(null);
  const [isLoadingDiagnostics, setIsLoadingDiagnostics] = useState(false);
  const [showModelManager, setShowModelManager] = useState(false);
  
  const {
    installEngine,
    verifyEngine,
    repairEngine,
    removeEngine,
    startEngine,
    stopEngine,
    fetchEngineStatus,
    getDiagnostics,
  } = useEnginesStore();

  useEffect(() => {
    loadStatus();
    const interval = setInterval(loadStatus, 5000); // Poll every 5 seconds
    return () => clearInterval(interval);
  }, [engine.id]);

  const loadStatus = async () => {
    try {
      await fetchEngineStatus(engine.id);
      const response = await fetch(`http://127.0.0.1:5005/api/engines/status?engineId=${engine.id}`);
      if (response.ok) {
        const data = await response.json();
        setStatus(data);
      }
    } catch (error) {
      console.error('Failed to load status:', error);
    }
  };

  const handleInstall = async () => {
    setIsProcessing(true);
    try {
      await installEngine(engine.id);
      await loadStatus();
    } catch (error) {
      console.error('Installation failed:', error);
    } finally {
      setIsProcessing(false);
    }
  };

  const handleStart = async () => {
    setIsProcessing(true);
    try {
      await startEngine(engine.id);
      await loadStatus();
    } catch (error) {
      console.error('Start failed:', error);
    } finally {
      setIsProcessing(false);
    }
  };

  const handleStop = async () => {
    setIsProcessing(true);
    try {
      await stopEngine(engine.id);
      await loadStatus();
    } catch (error) {
      console.error('Stop failed:', error);
    } finally {
      setIsProcessing(false);
    }
  };

  const handleVerify = async () => {
    setIsProcessing(true);
    try {
      const result = await verifyEngine(engine.id);
      alert(result.isValid ? 'Verification passed!' : `Verification failed: ${result.issues.join(', ')}`);
    } catch (error) {
      console.error('Verification failed:', error);
    } finally {
      setIsProcessing(false);
    }
  };

  const handleRepair = async () => {
    if (!confirm(`Repair ${engine.name}? This will reinstall the engine.`)) {
      return;
    }
    setIsProcessing(true);
    try {
      await repairEngine(engine.id);
      await loadStatus();
    } catch (error) {
      console.error('Repair failed:', error);
    } finally {
      setIsProcessing(false);
    }
  };

  const handleRemove = async () => {
    if (!confirm(`Remove ${engine.name}? This will delete all files.`)) {
      return;
    }
    setIsProcessing(true);
    try {
      await removeEngine(engine.id);
      await loadStatus();
    } catch (error) {
      console.error('Removal failed:', error);
    } finally {
      setIsProcessing(false);
    }
  };

  const handleOpenFolder = () => {
    if (engine.installPath) {
      // This would need to be implemented via an API endpoint that opens the folder
      alert(`Install path: ${engine.installPath}`);
    }
  };

  const handleShowDiagnostics = async () => {
    setIsLoadingDiagnostics(true);
    setShowDiagnostics(true);
    try {
      const data = await getDiagnostics(engine.id);
      setDiagnosticsData(data);
    } catch (error) {
      console.error('Failed to load diagnostics:', error);
      setDiagnosticsData({ error: 'Failed to load diagnostics' });
    } finally {
      setIsLoadingDiagnostics(false);
    }
  };

  const handleRepairWithRetry = async () => {
    if (!confirm(`Repair ${engine.name}? This will clean up partial downloads, re-verify checksums, and reinstall if needed.`)) {
      return;
    }
    setIsProcessing(true);
    setShowDiagnostics(false);
    try {
      await repairEngine(engine.id);
      await loadStatus();
      alert('Repair completed successfully!');
    } catch (error) {
      console.error('Repair failed:', error);
      alert(`Repair failed: ${error instanceof Error ? error.message : 'Unknown error'}`);
    } finally {
      setIsProcessing(false);
    }
  };

  const formatBytes = (bytes: number) => {
    if (!bytes) return 'Unknown';
    const units = ['B', 'KB', 'MB', 'GB', 'TB'];
    let size = bytes;
    let unitIndex = 0;
    while (size >= 1024 && unitIndex < units.length - 1) {
      size /= 1024;
      unitIndex++;
    }
    return `${size.toFixed(2)} ${units[unitIndex]}`;
  };

  const getStatusBadge = () => {
    if (!status) {
      return <Badge appearance="outline">Unknown</Badge>;
    }

    switch (status.status) {
      case 'running':
        return (
          <Badge appearance="filled" color="success">
            Running {status.health === 'healthy' ? '✓' : '(unreachable)'}
          </Badge>
        );
      case 'installed':
        return <Badge appearance="filled" color="informative">Installed</Badge>;
      case 'not_installed':
        return <Badge appearance="outline">Not Installed</Badge>;
      default:
        return <Badge appearance="outline">Unknown</Badge>;
    }
  };

  const isInstalled = engine.isInstalled || status?.status !== 'not_installed';
  const isRunning = status?.isRunning || false;

  return (
    <Card className={styles.card}>
      <CardHeader
        header={
          <div className={styles.header}>
            <div className={styles.title}>
              {engine.icon && <span>{engine.icon}</span>}
              <Text weight="semibold" size={500}>{engine.name}</Text>
              {getStatusBadge()}
            </div>
          </div>
        }
        description={
          <div>
            <Text className={styles.metadata}>
              Version: {engine.version} • Size: {formatBytes(engine.sizeBytes)}
              {engine.defaultPort && ` • Port: ${status?.port || engine.defaultPort}`}
              {engine.requiredVRAMGB && ` • Requires: ${engine.requiredVRAMGB}GB VRAM`}
            </Text>
            {engine.description && (
              <Text className={styles.metadata} block>{engine.description}</Text>
            )}
            {engine.gatingReason && (
              <Text className={styles.metadata} block style={{ color: tokens.colorPaletteYellowForeground1 }}>
                ⚠️ {engine.gatingReason}
                {engine.vramTooltip && ` • ${engine.vramTooltip}`}
              </Text>
            )}
            {engine.isGated && !engine.canAutoStart && (
              <Text className={styles.metadata} block style={{ color: tokens.colorNeutralForeground3 }}>
                💡 You can still install this engine for future use. It won't auto-start without meeting hardware requirements.
              </Text>
            )}
          </div>
        }
      />
      <CardPreview className={styles.content}>
        <div className={styles.row}>
          <div className={styles.actions}>
            {!isInstalled && (
              <Button
                appearance="primary"
                icon={<ArrowDownload24Regular />}
                onClick={handleInstall}
                disabled={isProcessing}
                title={engine.gatingReason || undefined}
              >
                {isProcessing ? <Spinner size="tiny" /> : engine.isGated && !engine.canAutoStart ? 'Install anyway (for later)' : 'Install'}
              </Button>
            )}
            
            {isInstalled && !isRunning && (
              <Button
                appearance="primary"
                icon={<Play24Regular />}
                onClick={handleStart}
                disabled={isProcessing || (engine.isGated && !engine.canAutoStart)}
                title={engine.isGated && !engine.canAutoStart ? 'Cannot start: hardware requirements not met' : undefined}
              >
                Start
              </Button>
            )}
            
            {isInstalled && isRunning && (
              <Button
                appearance="secondary"
                icon={<Stop24Regular />}
                onClick={handleStop}
                disabled={isProcessing}
              >
                Stop
              </Button>
            )}

            {isInstalled && (
              <Button
                appearance="subtle"
                icon={<DocumentFolder24Regular />}
                onClick={() => setShowModelManager(true)}
                title="Manage models and voices"
              >
                Models & Voices
              </Button>
            )}

            {isInstalled && (
              <Menu>
                <MenuTrigger disableButtonEnhancement>
                  <Button
                    appearance="subtle"
                    icon={<MoreHorizontal24Regular />}
                    disabled={isProcessing}
                  />
                </MenuTrigger>
                <MenuPopover>
                  <MenuList>
                    <MenuItem icon={<Checkmark24Regular />} onClick={handleVerify}>
                      Verify
                    </MenuItem>
                    <MenuItem icon={<Wrench24Regular />} onClick={handleRepair}>
                      Repair
                    </MenuItem>
                    <MenuItem icon={<Folder24Regular />} onClick={handleOpenFolder}>
                      Open Folder
                    </MenuItem>
                    <MenuItem icon={<Delete24Regular />} onClick={handleRemove}>
                      Remove
                    </MenuItem>
                  </MenuList>
                </MenuPopover>
              </Menu>
            )}
          </div>

          {engine.licenseUrl && (
            <Link href={engine.licenseUrl} target="_blank">
              License
            </Link>
          )}
        </div>

        {status?.messages && status.messages.length > 0 && (
          <div className={styles.messages}>
            {status.messages.map((msg, idx) => (
              <Text key={idx} className={styles.message}>
                <Warning24Regular /> {msg}
              </Text>
            ))}
            <Link onClick={handleShowDiagnostics} style={{ cursor: 'pointer' }}>
              <Info24Regular /> Why did this fail? Show diagnostics
            </Link>
          </div>
        )}

        {status?.processId && (
          <Text className={styles.metadata}>
            PID: {status.processId}
            {status.logsPath && ` • Logs: ${status.logsPath}`}
          </Text>
        )}
      </CardPreview>

      {/* Diagnostics Dialog */}
      <Dialog open={showDiagnostics} onOpenChange={(_, data) => setShowDiagnostics(data.open)}>
        <DialogSurface className={styles.diagnosticsDialog}>
          <DialogBody>
            <DialogTitle>Engine Diagnostics - {engine.name}</DialogTitle>
            <DialogContent>
              {isLoadingDiagnostics ? (
                <Spinner label="Loading diagnostics..." />
              ) : diagnosticsData?.error ? (
                <Text style={{ color: tokens.colorPaletteRedForeground1 }}>
                  {diagnosticsData.error}
                </Text>
              ) : diagnosticsData ? (
                <div className={styles.diagnosticsContent}>
                  <div className={styles.diagnosticsItem}>
                    <Text className={styles.diagnosticsLabel}>Install Path:</Text>
                    <Text>{diagnosticsData.installPath}</Text>
                  </div>
                  
                  <div className={styles.diagnosticsItem}>
                    <Text className={styles.diagnosticsLabel}>Is Installed:</Text>
                    <Badge appearance="filled" color={diagnosticsData.isInstalled ? 'success' : 'warning'}>
                      {diagnosticsData.isInstalled ? 'Yes' : 'No'}
                    </Badge>
                  </div>
                  
                  <div className={styles.diagnosticsItem}>
                    <Text className={styles.diagnosticsLabel}>Path Exists:</Text>
                    <Badge appearance="filled" color={diagnosticsData.pathExists ? 'success' : 'danger'}>
                      {diagnosticsData.pathExists ? 'Yes' : 'No'}
                    </Badge>
                  </div>
                  
                  <div className={styles.diagnosticsItem}>
                    <Text className={styles.diagnosticsLabel}>Path Writable:</Text>
                    <Badge appearance="filled" color={diagnosticsData.pathWritable ? 'success' : 'danger'}>
                      {diagnosticsData.pathWritable ? 'Yes' : 'No'}
                    </Badge>
                  </div>
                  
                  <div className={styles.diagnosticsItem}>
                    <Text className={styles.diagnosticsLabel}>Available Disk Space:</Text>
                    <Text>{formatBytes(diagnosticsData.availableDiskSpaceBytes)}</Text>
                  </div>
                  
                  {diagnosticsData.checksumStatus && (
                    <div className={styles.diagnosticsItem}>
                      <Text className={styles.diagnosticsLabel}>Checksum Status:</Text>
                      <Badge appearance="filled" color={diagnosticsData.checksumStatus === 'Valid' ? 'success' : 'danger'}>
                        {diagnosticsData.checksumStatus}
                      </Badge>
                    </div>
                  )}
                  
                  {diagnosticsData.expectedSha256 && (
                    <div className={styles.diagnosticsItem}>
                      <Text className={styles.diagnosticsLabel}>Expected SHA256:</Text>
                      <Text style={{ fontFamily: 'monospace', fontSize: '12px', wordBreak: 'break-all' }}>
                        {diagnosticsData.expectedSha256}
                      </Text>
                    </div>
                  )}
                  
                  {diagnosticsData.actualSha256 && (
                    <div className={styles.diagnosticsItem}>
                      <Text className={styles.diagnosticsLabel}>Actual SHA256:</Text>
                      <Text style={{ 
                        fontFamily: 'monospace', 
                        fontSize: '12px', 
                        wordBreak: 'break-all',
                        color: tokens.colorPaletteRedForeground1 
                      }}>
                        {diagnosticsData.actualSha256}
                      </Text>
                    </div>
                  )}
                  
                  {diagnosticsData.failedUrl && (
                    <div className={styles.diagnosticsItem}>
                      <Text className={styles.diagnosticsLabel}>Download URL:</Text>
                      <Text style={{ fontFamily: 'monospace', fontSize: '12px', wordBreak: 'break-all' }}>
                        {diagnosticsData.failedUrl}
                      </Text>
                    </div>
                  )}
                  
                  {diagnosticsData.lastError && (
                    <div className={styles.diagnosticsItem}>
                      <Text className={styles.diagnosticsLabel}>Last Error:</Text>
                      <Text style={{ color: tokens.colorPaletteRedForeground1 }}>
                        {diagnosticsData.lastError}
                      </Text>
                    </div>
                  )}
                  
                  {diagnosticsData.issues && diagnosticsData.issues.length > 0 && (
                    <div className={styles.diagnosticsIssues}>
                      <Text weight="semibold" block style={{ marginBottom: tokens.spacingVerticalXS }}>
                        Issues Found:
                      </Text>
                      {diagnosticsData.issues.map((issue: string, idx: number) => (
                        <Text key={idx} block style={{ marginTop: tokens.spacingVerticalXXS }}>
                          • {issue}
                        </Text>
                      ))}
                    </div>
                  )}
                </div>
              ) : null}
            </DialogContent>
            <DialogActions>
              {diagnosticsData && !diagnosticsData.error && diagnosticsData.issues && diagnosticsData.issues.length > 0 && (
                <Button 
                  appearance="primary" 
                  icon={<Wrench24Regular />}
                  onClick={handleRepairWithRetry}
                  disabled={isProcessing}
                >
                  Retry with Repair
                </Button>
              )}
              <Button appearance="secondary" onClick={() => setShowDiagnostics(false)}>
                Close
              </Button>
            </DialogActions>
          </DialogBody>
        </DialogSurface>
      </Dialog>

      {/* Models & Voices Manager */}
      <ModelManager
        open={showModelManager}
        onOpenChange={setShowModelManager}
        engineId={engine.id}
        engineName={engine.name}
      />
    </Card>
  );
}
