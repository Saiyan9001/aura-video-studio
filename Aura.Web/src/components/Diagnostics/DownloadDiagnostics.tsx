import { useState } from 'react';
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
  makeStyles,
  tokens,
  Input,
  Label,
} from '@fluentui/react-components';
import {
  Wrench24Regular,
  Folder24Regular,
  Link24Regular,
  Document24Regular,
} from '@fluentui/react-icons';

const useStyles = makeStyles({
  diagnosticsContent: {
    display: 'flex',
    flexDirection: 'column',
    gap: tokens.spacingVerticalM,
  },
  errorSection: {
    padding: tokens.spacingVerticalM,
    backgroundColor: tokens.colorPaletteRedBackground1,
    borderRadius: tokens.borderRadiusMedium,
  },
  errorCode: {
    fontFamily: 'monospace',
    fontSize: tokens.fontSizeBase300,
    color: tokens.colorPaletteRedForeground1,
    fontWeight: 600,
  },
  fixOption: {
    padding: tokens.spacingVerticalM,
    backgroundColor: tokens.colorNeutralBackground2,
    borderRadius: tokens.borderRadiusMedium,
    display: 'flex',
    flexDirection: 'column',
    gap: tokens.spacingVerticalS,
  },
  fixOptionTitle: {
    fontWeight: 600,
  },
  inputGroup: {
    display: 'flex',
    flexDirection: 'column',
    gap: tokens.spacingVerticalXS,
  },
});

export interface DiagnosticsInfo {
  engineId: string;
  engineName: string;
  lastError?: string;
  errorCode?: string;
  failedUrl?: string;
  checksumStatus?: string;
  expectedSha256?: string;
  actualSha256?: string;
  installPath?: string;
}

interface DownloadDiagnosticsProps {
  open: boolean;
  onOpenChange: (open: boolean) => void;
  diagnosticsInfo: DiagnosticsInfo;
  onRetry?: () => Promise<void>;
  onPickExistingPath?: (path: string) => Promise<void>;
  onUseCustomUrl?: (url: string) => Promise<void>;
  onInstallFromLocalFile?: (filePath: string) => Promise<void>;
}

export function DownloadDiagnostics({
  open,
  onOpenChange,
  diagnosticsInfo,
  onRetry,
  onPickExistingPath,
  onUseCustomUrl,
  onInstallFromLocalFile,
}: DownloadDiagnosticsProps) {
  const styles = useStyles();
  const [customUrl, setCustomUrl] = useState('');
  const [existingPath, setExistingPath] = useState('');
  const [localFilePath, setLocalFilePath] = useState('');
  const [isProcessing, setIsProcessing] = useState(false);

  const getErrorExplanation = (errorCode?: string): string => {
    if (!errorCode) return 'Download or installation failed';
    
    switch (errorCode) {
      case 'E-DL-404':
        return '404 Not Found: The download URL is no longer available. The file may have been moved or removed from the server.';
      case 'E-DL-CHECKSUM':
        return 'Checksum Mismatch: The downloaded file is corrupted or incomplete. This could be due to a network interruption or server issue.';
      case 'E-HEALTH-TIMEOUT':
        return 'Health Check Timeout: The engine installed but failed to respond to health checks. It may need manual configuration or have compatibility issues.';
      case 'E-DL-NETWORK':
        return 'Network Error: Could not connect to the download server. Check your internet connection or firewall settings.';
      case 'E-DL-DISK-SPACE':
        return 'Insufficient Disk Space: Not enough free space to complete the download and installation.';
      case 'E-DL-PERMISSION':
        return 'Permission Denied: Cannot write to the installation directory. Try running as administrator or choose a different location.';
      default:
        return `Error: ${errorCode}`;
    }
  };

  const handleRetry = async () => {
    if (!onRetry) return;
    setIsProcessing(true);
    try {
      await onRetry();
      onOpenChange(false);
    } catch (error) {
      console.error('Retry failed:', error);
    } finally {
      setIsProcessing(false);
    }
  };

  const handlePickExistingPath = async () => {
    if (!onPickExistingPath || !existingPath) return;
    setIsProcessing(true);
    try {
      await onPickExistingPath(existingPath);
      onOpenChange(false);
    } catch (error) {
      console.error('Pick existing path failed:', error);
      alert(`Failed to use existing installation: ${error instanceof Error ? error.message : 'Unknown error'}`);
    } finally {
      setIsProcessing(false);
    }
  };

  const handleUseCustomUrl = async () => {
    if (!onUseCustomUrl || !customUrl) return;
    setIsProcessing(true);
    try {
      await onUseCustomUrl(customUrl);
      onOpenChange(false);
    } catch (error) {
      console.error('Use custom URL failed:', error);
      alert(`Failed to download from custom URL: ${error instanceof Error ? error.message : 'Unknown error'}`);
    } finally {
      setIsProcessing(false);
    }
  };

  const handleInstallFromLocalFile = async () => {
    if (!onInstallFromLocalFile || !localFilePath) return;
    setIsProcessing(true);
    try {
      await onInstallFromLocalFile(localFilePath);
      onOpenChange(false);
    } catch (error) {
      console.error('Install from local file failed:', error);
      alert(`Failed to install from local file: ${error instanceof Error ? error.message : 'Unknown error'}`);
    } finally {
      setIsProcessing(false);
    }
  };

  return (
    <Dialog open={open} onOpenChange={(_, data) => onOpenChange(data.open)}>
      <DialogSurface style={{ minWidth: '600px' }}>
        <DialogBody>
          <DialogTitle>Download Diagnostics - {diagnosticsInfo.engineName}</DialogTitle>
          <DialogContent>
            <div className={styles.diagnosticsContent}>
              {diagnosticsInfo.errorCode && (
                <div className={styles.errorSection}>
                  <Text weight="semibold" block>
                    Error Code: <span className={styles.errorCode}>{diagnosticsInfo.errorCode}</span>
                  </Text>
                  <Text size={300} block style={{ marginTop: tokens.spacingVerticalS }}>
                    {getErrorExplanation(diagnosticsInfo.errorCode)}
                  </Text>
                </div>
              )}

              {diagnosticsInfo.lastError && !diagnosticsInfo.errorCode && (
                <div className={styles.errorSection}>
                  <Text weight="semibold" block>Last Error:</Text>
                  <Text size={300} block style={{ marginTop: tokens.spacingVerticalS }}>
                    {diagnosticsInfo.lastError}
                  </Text>
                </div>
              )}

              {diagnosticsInfo.failedUrl && (
                <div>
                  <Text weight="semibold" block>Failed URL:</Text>
                  <Text size={200} style={{ fontFamily: 'monospace', wordBreak: 'break-all' }}>
                    {diagnosticsInfo.failedUrl}
                  </Text>
                </div>
              )}

              {diagnosticsInfo.checksumStatus === 'Invalid' && (
                <div>
                  <Badge appearance="filled" color="danger">Checksum Mismatch</Badge>
                  <Text size={300} block style={{ marginTop: tokens.spacingVerticalS }}>
                    Expected: <code>{diagnosticsInfo.expectedSha256?.substring(0, 16)}...</code>
                  </Text>
                  {diagnosticsInfo.actualSha256 && (
                    <Text size={300} block>
                      Actual: <code>{diagnosticsInfo.actualSha256.substring(0, 16)}...</code>
                    </Text>
                  )}
                </div>
              )}

              <Text weight="semibold" size={400} block style={{ marginTop: tokens.spacingVerticalM }}>
                Fix Options:
              </Text>

              {/* Option 1: Pick existing path */}
              {onPickExistingPath && (
                <div className={styles.fixOption}>
                  <div style={{ display: 'flex', alignItems: 'center', gap: tokens.spacingHorizontalS }}>
                    <Folder24Regular />
                    <Text className={styles.fixOptionTitle}>Use Existing Installation</Text>
                  </div>
                  <Text size={300}>
                    If you already have {diagnosticsInfo.engineName} installed elsewhere, point to its location.
                  </Text>
                  <div className={styles.inputGroup}>
                    <Label>Installation Path:</Label>
                    <Input
                      value={existingPath}
                      onChange={(_, data) => setExistingPath(data.value)}
                      placeholder="e.g., C:\Tools\stable-diffusion-webui"
                      disabled={isProcessing}
                    />
                    <Button
                      appearance="secondary"
                      onClick={handlePickExistingPath}
                      disabled={!existingPath || isProcessing}
                    >
                      Use This Path
                    </Button>
                  </div>
                </div>
              )}

              {/* Option 2: Custom URL */}
              {onUseCustomUrl && (
                <div className={styles.fixOption}>
                  <div style={{ display: 'flex', alignItems: 'center', gap: tokens.spacingHorizontalS }}>
                    <Link24Regular />
                    <Text className={styles.fixOptionTitle}>Try Custom Download URL</Text>
                  </div>
                  <Text size={300}>
                    Use an alternative mirror or download URL if the default one is unavailable.
                  </Text>
                  <div className={styles.inputGroup}>
                    <Label>Custom URL:</Label>
                    <Input
                      value={customUrl}
                      onChange={(_, data) => setCustomUrl(data.value)}
                      placeholder="https://..."
                      disabled={isProcessing}
                    />
                    <Button
                      appearance="secondary"
                      onClick={handleUseCustomUrl}
                      disabled={!customUrl || isProcessing}
                    >
                      Download from This URL
                    </Button>
                  </div>
                </div>
              )}

              {/* Option 3: Install from local file */}
              {onInstallFromLocalFile && (
                <div className={styles.fixOption}>
                  <div style={{ display: 'flex', alignItems: 'center', gap: tokens.spacingHorizontalS }}>
                    <Document24Regular />
                    <Text className={styles.fixOptionTitle}>Install from Local File</Text>
                  </div>
                  <Text size={300}>
                    If you've already downloaded the file manually, install it from your local disk.
                  </Text>
                  <div className={styles.inputGroup}>
                    <Label>Local File Path:</Label>
                    <Input
                      value={localFilePath}
                      onChange={(_, data) => setLocalFilePath(data.value)}
                      placeholder="e.g., C:\Downloads\engine.zip"
                      disabled={isProcessing}
                    />
                    <Button
                      appearance="secondary"
                      onClick={handleInstallFromLocalFile}
                      disabled={!localFilePath || isProcessing}
                    >
                      Install from Local File
                    </Button>
                  </div>
                </div>
              )}

              {/* Option 4: Retry with repair */}
              {onRetry && (
                <div className={styles.fixOption}>
                  <div style={{ display: 'flex', alignItems: 'center', gap: tokens.spacingHorizontalS }}>
                    <Wrench24Regular />
                    <Text className={styles.fixOptionTitle}>Retry with Repair</Text>
                  </div>
                  <Text size={300}>
                    Clean up partial downloads, re-verify checksums, and try downloading again.
                  </Text>
                  <Button
                    appearance="primary"
                    icon={<Wrench24Regular />}
                    onClick={handleRetry}
                    disabled={isProcessing}
                  >
                    Retry with Repair
                  </Button>
                </div>
              )}

              {diagnosticsInfo.installPath && (
                <Text size={200} style={{ marginTop: tokens.spacingVerticalM, color: tokens.colorNeutralForeground3 }}>
                  Install path: {diagnosticsInfo.installPath}
                </Text>
              )}
            </div>
          </DialogContent>
          <DialogActions>
            <Button appearance="secondary" onClick={() => onOpenChange(false)} disabled={isProcessing}>
              Close
            </Button>
          </DialogActions>
        </DialogBody>
      </DialogSurface>
    </Dialog>
  );
}
