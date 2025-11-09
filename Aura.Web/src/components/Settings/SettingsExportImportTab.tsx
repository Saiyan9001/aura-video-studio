import {
  Button,
  Card,
  Checkbox,
  Dialog,
  DialogActions,
  DialogBody,
  DialogContent,
  DialogSurface,
  DialogTitle,
  Field,
  MessageBar,
  MessageBarBody,
  MessageBarTitle,
  Spinner,
  Switch,
  Text,
  Title2,
  Title3,
  makeStyles,
  tokens,
} from '@fluentui/react-components';
import {
  ArrowDownload24Regular,
  ArrowUpload24Regular,
  Warning24Regular,
  CheckmarkCircle24Regular,
  Info24Regular,
  ArrowCounterclockwise24Regular,
  ArrowReset24Regular,
  Shield24Regular,
} from '@fluentui/react-icons';
import { useState, useEffect, useCallback } from 'react';
import type { FC } from 'react';
import { apiUrl } from '../../config/api';
import { configurationRecoveryService } from '../../services/configurationRecoveryService';

const useStyles = makeStyles({
  container: {
    display: 'flex',
    flexDirection: 'column',
    gap: tokens.spacingVerticalL,
  },
  section: {
    padding: tokens.spacingVerticalL,
  },
  buttonGroup: {
    display: 'flex',
    gap: tokens.spacingHorizontalM,
    flexWrap: 'wrap',
  },
  warningBox: {
    backgroundColor: tokens.colorPaletteYellowBackground2,
    padding: tokens.spacingVerticalM,
    borderRadius: tokens.borderRadiusMedium,
    border: `2px solid ${tokens.colorPaletteYellowBorder2}`,
  },
  checkboxList: {
    display: 'flex',
    flexDirection: 'column',
    gap: tokens.spacingVerticalS,
    marginTop: tokens.spacingVerticalM,
  },
  previewBox: {
    backgroundColor: tokens.colorNeutralBackground3,
    padding: tokens.spacingVerticalM,
    borderRadius: tokens.borderRadiusMedium,
    fontFamily: 'monospace',
    fontSize: '12px',
    maxHeight: '200px',
    overflowY: 'auto',
  },
  conflictItem: {
    padding: tokens.spacingVerticalS,
    backgroundColor: tokens.colorNeutralBackground2,
    borderRadius: tokens.borderRadiusSmall,
    marginBottom: tokens.spacingVerticalS,
  },
});

interface ExportModalProps {
  open: boolean;
  onClose: () => void;
}

const ExportModal: FC<ExportModalProps> = ({ open, onClose }) => {
  const styles = useStyles();
  const [includeSecrets, setIncludeSecrets] = useState(false);
  const [selectedKeys, setSelectedKeys] = useState<string[]>([]);
  const [acknowledgeWarning, setAcknowledgeWarning] = useState(false);
  const [loading, setLoading] = useState(false);
  const [preview, setPreview] = useState<Record<string, string> | null>(null);
  const [availableKeys, setAvailableKeys] = useState<string[]>([]);

  const loadPreview = useCallback(async () => {
    try {
      const response = await fetch(apiUrl('/api/settings/export/preview'));
      if (response.ok) {
        const data = await response.json();
        setAvailableKeys(data.availableKeys || []);
        setPreview(data.redactionPreview || {});
      }
    } catch (error) {
      console.error('Error loading preview:', error);
    }
  }, []);

  const handleExport = async () => {
    if (includeSecrets && !acknowledgeWarning) {
      alert('Please acknowledge the security warning');
      return;
    }

    setLoading(true);
    try {
      const response = await fetch(apiUrl('/api/settings/export'), {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({
          includeSecrets,
          selectedSecretKeys: includeSecrets ? selectedKeys : [],
          acknowledgeWarning,
        }),
      });

      if (response.ok) {
        const data = await response.json();

        const exportData = {
          version: data.version,
          exportedAt: data.exportedAt,
          settings: data.settings,
          metadata: data.metadata,
        };

        const blob = new Blob([JSON.stringify(exportData, null, 2)], {
          type: 'application/json',
        });
        const url = URL.createObjectURL(blob);
        const a = document.createElement('a');
        a.href = url;
        const secretsSuffix = includeSecrets ? '-with-secrets' : '';
        a.download = `aura-settings${secretsSuffix}-${new Date().toISOString().split('T')[0]}.json`;
        a.click();
        URL.revokeObjectURL(url);

        onClose();
      } else {
        alert('Failed to export settings');
      }
    } catch (error) {
      console.error('Error exporting settings:', error);
      alert('Error exporting settings');
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    if (open) {
      loadPreview();
    }
  }, [open, loadPreview]);

  return (
    <Dialog open={open} onOpenChange={(_, data) => !data.open && onClose()}>
      <DialogSurface>
        <DialogBody>
          <DialogTitle>Export Settings</DialogTitle>
          <DialogContent>
            <div className={styles.container}>
              <MessageBar intent="info">
                <MessageBarBody>
                  <MessageBarTitle>Default: Secrets Excluded</MessageBarTitle>
                  By default, API keys and secrets are NOT included in exports for your security.
                  Enable below only if you need to transfer settings to another machine.
                </MessageBarBody>
              </MessageBar>

              <Field>
                <Switch
                  label="Include API Keys and Secrets"
                  checked={includeSecrets}
                  onChange={(_, data) => {
                    setIncludeSecrets(data.checked);
                    if (!data.checked) {
                      setSelectedKeys([]);
                      setAcknowledgeWarning(false);
                    }
                  }}
                />
              </Field>

              {includeSecrets && (
                <>
                  <div className={styles.warningBox}>
                    <div
                      style={{
                        display: 'flex',
                        gap: tokens.spacingHorizontalS,
                        alignItems: 'flex-start',
                      }}
                    >
                      <Warning24Regular
                        style={{ color: tokens.colorPaletteYellowForeground1, flexShrink: 0 }}
                      />
                      <div>
                        <Text
                          weight="semibold"
                          style={{ display: 'block', marginBottom: tokens.spacingVerticalXS }}
                        >
                          Security Warning
                        </Text>
                        <Text size={200}>
                          Exported files will contain API keys in PLAIN TEXT. Never share via email,
                          chat, or commit to version control. Use secure storage and delete after
                          use.
                        </Text>
                      </div>
                    </div>
                  </div>

                  <Field label="Select API keys to include:">
                    <div className={styles.checkboxList}>
                      {availableKeys.map((key) => (
                        <Checkbox
                          key={key}
                          label={
                            <div>
                              <Text>{key}</Text>
                              {preview && preview[key] && (
                                <Text
                                  size={200}
                                  style={{
                                    color: tokens.colorNeutralForeground3,
                                    marginLeft: tokens.spacingHorizontalS,
                                  }}
                                >
                                  ({preview[key]})
                                </Text>
                              )}
                            </div>
                          }
                          checked={selectedKeys.includes(key)}
                          onChange={(_, data) => {
                            if (data.checked === true) {
                              setSelectedKeys([...selectedKeys, key]);
                            } else {
                              setSelectedKeys(selectedKeys.filter((k) => k !== key));
                            }
                          }}
                        />
                      ))}
                    </div>
                  </Field>

                  <Field>
                    <Checkbox
                      label="I understand the security risks and will handle this file securely"
                      checked={acknowledgeWarning}
                      onChange={(_, data) => setAcknowledgeWarning(data.checked === true)}
                    />
                  </Field>
                </>
              )}

              {!includeSecrets && (
                <MessageBar intent="success">
                  <MessageBarBody>
                    <div
                      style={{
                        display: 'flex',
                        gap: tokens.spacingHorizontalS,
                        alignItems: 'center',
                      }}
                    >
                      <CheckmarkCircle24Regular />
                      <Text>Secure export: API keys will be redacted (shown as empty)</Text>
                    </div>
                  </MessageBarBody>
                </MessageBar>
              )}
            </div>
          </DialogContent>
          <DialogActions>
            <Button appearance="secondary" onClick={onClose}>
              Cancel
            </Button>
            <Button
              appearance="primary"
              onClick={handleExport}
              disabled={
                loading || (includeSecrets && (!acknowledgeWarning || selectedKeys.length === 0))
              }
              icon={loading ? <Spinner size="tiny" /> : <ArrowDownload24Regular />}
            >
              {loading ? 'Exporting...' : 'Export'}
            </Button>
          </DialogActions>
        </DialogBody>
      </DialogSurface>
    </Dialog>
  );
};

interface ImportModalProps {
  open: boolean;
  onClose: () => void;
  onSuccess: () => void;
}

const ImportModal: FC<ImportModalProps> = ({ open, onClose, onSuccess }) => {
  const styles = useStyles();
  const [file, setFile] = useState<File | null>(null);
  const [loading, setLoading] = useState(false);
  const [dryRunResult, setDryRunResult] = useState<any>(null);
  const [showConfirm, setShowConfirm] = useState(false);

  const handleFileSelect = async (e: React.ChangeEvent<HTMLInputElement>) => {
    const selectedFile = e.target.files?.[0];
    if (selectedFile) {
      setFile(selectedFile);
      await performDryRun(selectedFile);
    }
  };

  const performDryRun = async (importFile: File) => {
    setLoading(true);
    try {
      const text = await importFile.text();
      const data = JSON.parse(text);

      const response = await fetch(apiUrl('/api/settings/import'), {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({
          version: data.version,
          settings: data.settings,
          dryRun: true,
          overwriteExisting: false,
        }),
      });

      if (response.ok) {
        const result = await response.json();
        setDryRunResult(result);
        if (result.conflicts && result.conflicts.totalConflicts > 0) {
          setShowConfirm(true);
        } else {
          setShowConfirm(true);
        }
      } else {
        alert('Failed to analyze import file');
        setFile(null);
      }
    } catch (error) {
      console.error('Error during dry-run:', error);
      alert('Invalid settings file format');
      setFile(null);
    } finally {
      setLoading(false);
    }
  };

  const handleImport = async () => {
    if (!file) return;

    setLoading(true);
    try {
      const text = await file.text();
      const data = JSON.parse(text);

      const response = await fetch(apiUrl('/api/settings/import'), {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({
          version: data.version,
          settings: data.settings,
          dryRun: false,
          overwriteExisting: true,
        }),
      });

      if (response.ok) {
        alert('Settings imported successfully! Please refresh the page.');
        onSuccess();
        onClose();
      } else {
        alert('Failed to import settings');
      }
    } catch (error) {
      console.error('Error importing settings:', error);
      alert('Error importing settings');
    } finally {
      setLoading(false);
    }
  };

  return (
    <Dialog open={open} onOpenChange={(_, data) => !data.open && onClose()}>
      <DialogSurface>
        <DialogBody>
          <DialogTitle>Import Settings</DialogTitle>
          <DialogContent>
            <div className={styles.container}>
              <MessageBar intent="info">
                <MessageBarBody>
                  <MessageBarTitle>Safe Import with Dry-Run</MessageBarTitle>
                  We&apos;ll first analyze your file and show what would change before applying
                  anything.
                </MessageBarBody>
              </MessageBar>

              <Field label="Select settings file (.json)">
                <input
                  type="file"
                  accept=".json"
                  onChange={handleFileSelect}
                  style={{ width: '100%' }}
                />
              </Field>

              {loading && (
                <div
                  style={{ display: 'flex', alignItems: 'center', gap: tokens.spacingHorizontalS }}
                >
                  <Spinner size="small" />
                  <Text>Analyzing file...</Text>
                </div>
              )}

              {dryRunResult && !loading && (
                <>
                  <MessageBar
                    intent={dryRunResult.conflicts?.totalConflicts > 0 ? 'warning' : 'success'}
                  >
                    <MessageBarBody>
                      <MessageBarTitle>
                        {dryRunResult.conflicts?.totalConflicts > 0
                          ? `Found ${dryRunResult.conflicts.totalConflicts} conflicts`
                          : 'No conflicts detected'}
                      </MessageBarTitle>
                      {dryRunResult.message}
                    </MessageBarBody>
                  </MessageBar>

                  {dryRunResult.conflicts && dryRunResult.conflicts.totalConflicts > 0 && (
                    <div>
                      <Title3>Conflicts to Review</Title3>

                      {dryRunResult.conflicts.generalSettings?.length > 0 && (
                        <div>
                          <Text weight="semibold">General Settings:</Text>
                          {dryRunResult.conflicts.generalSettings.map(
                            (conflict: any, i: number) => (
                              <div key={i} className={styles.conflictItem}>
                                <Text size={200}>
                                  <strong>{conflict.key}:</strong> {conflict.currentValue} →{' '}
                                  {conflict.newValue}
                                </Text>
                              </div>
                            )
                          )}
                        </div>
                      )}

                      {dryRunResult.conflicts.apiKeys?.length > 0 && (
                        <div style={{ marginTop: tokens.spacingVerticalM }}>
                          <Text weight="semibold">API Keys:</Text>
                          {dryRunResult.conflicts.apiKeys.map((conflict: any, i: number) => (
                            <div key={i} className={styles.conflictItem}>
                              <Text size={200}>
                                <strong>{conflict.key}:</strong> Will be updated
                              </Text>
                            </div>
                          ))}
                        </div>
                      )}

                      {dryRunResult.conflicts.providerPaths?.length > 0 && (
                        <div style={{ marginTop: tokens.spacingVerticalM }}>
                          <Text weight="semibold">Provider Paths:</Text>
                          {dryRunResult.conflicts.providerPaths.map((conflict: any, i: number) => (
                            <div key={i} className={styles.conflictItem}>
                              <Text size={200}>
                                <strong>{conflict.key}:</strong> {conflict.currentValue} →{' '}
                                {conflict.newValue}
                              </Text>
                            </div>
                          ))}
                        </div>
                      )}
                    </div>
                  )}
                </>
              )}
            </div>
          </DialogContent>
          <DialogActions>
            <Button appearance="secondary" onClick={onClose}>
              Cancel
            </Button>
            <Button
              appearance="primary"
              onClick={handleImport}
              disabled={!file || loading || !showConfirm}
              icon={loading ? <Spinner size="tiny" /> : <ArrowUpload24Regular />}
            >
              {loading ? 'Importing...' : 'Apply Import'}
            </Button>
          </DialogActions>
        </DialogBody>
      </DialogSurface>
    </Dialog>
  );
};

interface SettingsExportImportTabProps {
  onSettingsChange: () => void;
}

export const SettingsExportImportTab: FC<SettingsExportImportTabProps> = ({ onSettingsChange }) => {
  const styles = useStyles();
  const [exportModalOpen, setExportModalOpen] = useState(false);
  const [importModalOpen, setImportModalOpen] = useState(false);
  const [backups, setBackups] = useState<any[]>([]);
  const [recoveryInProgress, setRecoveryInProgress] = useState(false);

  useEffect(() => {
    loadBackups();
  }, []);

  const loadBackups = () => {
    const availableBackups = configurationRecoveryService.listBackups();
    setBackups(availableBackups);
  };

  const handleValidateConfig = () => {
    const result = configurationRecoveryService.validateConfiguration();

    if (result.valid) {
      alert(
        `Configuration is valid!\n\nWarnings: ${result.warnings.length}\n${result.warnings.join('\n')}`
      );
    } else {
      alert(
        `Configuration validation failed!\n\nErrors:\n${result.errors.join('\n')}\n\nWarnings:\n${result.warnings.join('\n')}`
      );
    }
  };

  const handleCreateBackup = async () => {
    const success = await configurationRecoveryService.backupConfiguration();

    if (success) {
      alert('Configuration backup created successfully!');
      loadBackups();
    } else {
      alert('Failed to create configuration backup.');
    }
  };

  const handleRestoreFromBackup = async (index: number) => {
    if (
      !confirm(
        `Restore configuration from backup ${index + 1}? This will replace your current configuration.`
      )
    ) {
      return;
    }

    setRecoveryInProgress(true);
    try {
      const success = await configurationRecoveryService.restoreFromBackup(index);

      if (success) {
        alert('Configuration restored successfully! Page will reload.');
        window.location.reload();
      } else {
        alert('Failed to restore configuration from backup.');
      }
    } finally {
      setRecoveryInProgress(false);
    }
  };

  const handleResetToDefaults = async () => {
    if (
      !confirm(
        'Reset all configuration to defaults? This will create a backup of your current configuration first.'
      )
    ) {
      return;
    }

    setRecoveryInProgress(true);
    try {
      const success = await configurationRecoveryService.resetToDefaults();

      if (success) {
        alert('Configuration reset to defaults! Page will reload.');
        window.location.reload();
      } else {
        alert('Failed to reset configuration to defaults.');
      }
    } finally {
      setRecoveryInProgress(false);
    }
  };

  const handleAutoRecover = async () => {
    if (
      !confirm('Attempt automatic configuration recovery? This will validate and fix any issues.')
    ) {
      return;
    }

    setRecoveryInProgress(true);
    try {
      const success = await configurationRecoveryService.autoRecover();

      if (success) {
        alert('Configuration recovered successfully! Page will reload.');
        window.location.reload();
      } else {
        alert('Auto-recovery failed. Please try resetting to defaults.');
      }
    } finally {
      setRecoveryInProgress(false);
    }
  };

  return (
    <div className={styles.container}>
      <Card className={styles.section}>
        <Title2>Export/Import Settings</Title2>
        <Text style={{ marginBottom: tokens.spacingVerticalL, display: 'block' }}>
          Backup and restore your Aura settings including API keys, provider paths, and preferences.
        </Text>

        <div className={styles.buttonGroup}>
          <Button
            appearance="primary"
            icon={<ArrowDownload24Regular />}
            onClick={() => setExportModalOpen(true)}
          >
            Export Settings
          </Button>
          <Button
            appearance="secondary"
            icon={<ArrowUpload24Regular />}
            onClick={() => setImportModalOpen(true)}
          >
            Import Settings
          </Button>
        </div>

        <MessageBar intent="info" style={{ marginTop: tokens.spacingVerticalL }}>
          <MessageBarBody>
            <div
              style={{ display: 'flex', gap: tokens.spacingHorizontalS, alignItems: 'flex-start' }}
            >
              <Info24Regular style={{ flexShrink: 0 }} />
              <div>
                <MessageBarTitle>Security by Default</MessageBarTitle>
                <Text size={200}>
                  Exports are secretless by default. API keys are NOT included unless you explicitly
                  choose to include them with proper warnings and per-key selection.
                </Text>
              </div>
            </div>
          </MessageBarBody>
        </MessageBar>
      </Card>

      <Card className={styles.section}>
        <Title2>Configuration Recovery</Title2>
        <Text style={{ marginBottom: tokens.spacingVerticalL, display: 'block' }}>
          Validate, backup, and recover your configuration if issues occur.
        </Text>

        <div className={styles.buttonGroup}>
          <Button appearance="secondary" icon={<Shield24Regular />} onClick={handleValidateConfig}>
            Validate Configuration
          </Button>
          <Button
            appearance="secondary"
            icon={<ArrowDownload24Regular />}
            onClick={handleCreateBackup}
          >
            Create Backup
          </Button>
          <Button
            appearance="secondary"
            icon={<ArrowCounterclockwise24Regular />}
            onClick={handleAutoRecover}
            disabled={recoveryInProgress}
          >
            Auto-Recover
          </Button>
          <Button
            appearance="secondary"
            icon={<ArrowReset24Regular />}
            onClick={handleResetToDefaults}
            disabled={recoveryInProgress}
          >
            Reset to Defaults
          </Button>
        </div>

        {backups.length > 0 && (
          <>
            <Title3 style={{ marginTop: tokens.spacingVerticalL }}>
              Available Backups ({backups.length})
            </Title3>
            <div
              style={{
                display: 'flex',
                flexDirection: 'column',
                gap: tokens.spacingVerticalS,
                marginTop: tokens.spacingVerticalM,
              }}
            >
              {backups.map((backup, index) => (
                <Card key={index} style={{ padding: tokens.spacingVerticalM }}>
                  <div
                    style={{
                      display: 'flex',
                      justifyContent: 'space-between',
                      alignItems: 'center',
                    }}
                  >
                    <div>
                      <Text weight="semibold">Backup {index + 1}</Text>
                      <Text
                        size={200}
                        style={{ display: 'block', color: tokens.colorNeutralForeground3 }}
                      >
                        {new Date(backup.timestamp).toLocaleString()}
                      </Text>
                      <Text
                        size={200}
                        style={{ display: 'block', color: tokens.colorNeutralForeground3 }}
                      >
                        Version: {backup.version}
                      </Text>
                    </div>
                    <Button
                      appearance="secondary"
                      size="small"
                      onClick={() => handleRestoreFromBackup(index)}
                      disabled={recoveryInProgress}
                    >
                      Restore
                    </Button>
                  </div>
                </Card>
              ))}
            </div>
          </>
        )}

        <MessageBar intent="warning" style={{ marginTop: tokens.spacingVerticalL }}>
          <MessageBarBody>
            <div
              style={{ display: 'flex', gap: tokens.spacingHorizontalS, alignItems: 'flex-start' }}
            >
              <Warning24Regular style={{ flexShrink: 0 }} />
              <div>
                <MessageBarTitle>Configuration Recovery</MessageBarTitle>
                <Text size={200}>
                  Use these tools to diagnose and fix configuration issues. Auto-recovery will
                  attempt to restore from a backup or reset to defaults if needed.
                </Text>
              </div>
            </div>
          </MessageBarBody>
        </MessageBar>
      </Card>

      <ExportModal open={exportModalOpen} onClose={() => setExportModalOpen(false)} />

      <ImportModal
        open={importModalOpen}
        onClose={() => setImportModalOpen(false)}
        onSuccess={onSettingsChange}
      />
    </div>
  );
};
