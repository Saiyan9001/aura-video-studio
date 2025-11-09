import {
  makeStyles,
  tokens,
  Title2,
  Title3,
  Text,
  Card,
  Button,
  Spinner,
  Badge,
  ProgressBar,
} from '@fluentui/react-components';
import {
  Checkmark24Regular,
  Dismiss24Regular,
  Warning24Regular,
  ArrowDownload24Regular,
  Settings24Regular,
} from '@fluentui/react-icons';
import { useState, useEffect } from 'react';
import { PathSelector } from '../common/PathSelector';

const useStyles = makeStyles({
  container: {
    display: 'flex',
    flexDirection: 'column',
    gap: tokens.spacingVerticalL,
  },
  header: {
    textAlign: 'center',
    marginBottom: tokens.spacingVerticalL,
  },
  dependenciesGrid: {
    display: 'grid',
    gridTemplateColumns: '1fr',
    gap: tokens.spacingVerticalM,
  },
  dependencyCard: {
    padding: tokens.spacingVerticalL,
    transition: 'all 0.2s ease-in-out',
    ':hover': {
      boxShadow: tokens.shadow8,
    },
  },
  dependencyHeader: {
    display: 'flex',
    alignItems: 'center',
    justifyContent: 'space-between',
    marginBottom: tokens.spacingVerticalM,
  },
  dependencyInfo: {
    display: 'flex',
    alignItems: 'center',
    gap: tokens.spacingHorizontalM,
    flex: 1,
  },
  statusIcon: {
    fontSize: '32px',
  },
  dependencyDetails: {
    flex: 1,
  },
  dependencyName: {
    marginBottom: tokens.spacingVerticalXS,
  },
  dependencyDescription: {
    color: tokens.colorNeutralForeground3,
    display: 'block',
  },
  actionsContainer: {
    display: 'flex',
    gap: tokens.spacingHorizontalS,
    alignItems: 'center',
  },
  detailsSection: {
    marginTop: tokens.spacingVerticalM,
    padding: tokens.spacingVerticalM,
    backgroundColor: tokens.colorNeutralBackground2,
    borderRadius: tokens.borderRadiusMedium,
  },
  detailRow: {
    display: 'flex',
    justifyContent: 'space-between',
    marginBottom: tokens.spacingVerticalXS,
  },
  progressSection: {
    marginTop: tokens.spacingVerticalM,
  },
  summaryCard: {
    padding: tokens.spacingVerticalL,
    backgroundColor: tokens.colorNeutralBackground3,
    marginTop: tokens.spacingVerticalL,
  },
  statusList: {
    display: 'flex',
    flexDirection: 'column',
    gap: tokens.spacingVerticalS,
    marginTop: tokens.spacingVerticalM,
  },
  statusItem: {
    display: 'flex',
    alignItems: 'center',
    gap: tokens.spacingHorizontalM,
  },
});

export interface Dependency {
  id: string;
  name: string;
  description: string;
  required: boolean;
  status: 'checking' | 'installed' | 'missing' | 'skipped' | 'error';
  version?: string;
  installPath?: string;
  canAutoInstall: boolean;
  installing?: boolean;
  installProgress?: number;
  errorMessage?: string;
}

export interface DependencyCheckProps {
  dependencies: Dependency[];
  onAutoInstall: (dependencyId: string) => Promise<void>;
  onManualInstall: (dependencyId: string) => void;
  onSkip: (dependencyId: string) => void;
  onAssignPath?: (dependencyId: string, path: string) => Promise<void>;
  onRescan?: () => Promise<void>;
  isScanning?: boolean;
}

export function DependencyCheck({
  dependencies,
  onAutoInstall,
  onManualInstall,
  onSkip,
  onAssignPath,
  onRescan,
  isScanning = false,
}: DependencyCheckProps) {
  const styles = useStyles();
  const [expandedIds, setExpandedIds] = useState<Set<string>>(new Set());
  const [manualPaths, setManualPaths] = useState<Record<string, string>>({});
  const [assigningPath, setAssigningPath] = useState<string | null>(null);
  const [pathErrors, setPathErrors] = useState<Record<string, string>>({});

  useEffect(() => {
    // Auto-expand all "Not Found" (missing) items
    const missingDepIds = dependencies
      .filter((dep) => dep.status === 'missing')
      .map((dep) => dep.id);

    if (missingDepIds.length > 0) {
      setExpandedIds(new Set(missingDepIds));
    }
  }, [dependencies]);

  const handlePathChange = (depId: string, path: string) => {
    setManualPaths((prev) => ({ ...prev, [depId]: path }));
    setPathErrors((prev) => ({ ...prev, [depId]: '' }));
  };

  const handleAssignPath = async (depId: string) => {
    const path = manualPaths[depId];
    if (!path || !path.trim()) {
      setPathErrors((prev) => ({ ...prev, [depId]: 'Please enter a valid path' }));
      return;
    }

    if (onAssignPath) {
      try {
        setAssigningPath(depId);
        setPathErrors((prev) => ({ ...prev, [depId]: '' }));
        await onAssignPath(depId, path.trim());
      } catch (error: unknown) {
        const errorMessage = error instanceof Error ? error.message : 'Failed to assign path';
        setPathErrors((prev) => ({ ...prev, [depId]: errorMessage }));
      } finally {
        setAssigningPath(null);
      }
    }
  };

  const getStatusIcon = (status: Dependency['status']) => {
    switch (status) {
      case 'checking':
        return <Spinner size="medium" className={styles.statusIcon} />;
      case 'installed':
        return (
          <Checkmark24Regular
            className={styles.statusIcon}
            style={{ color: tokens.colorPaletteGreenForeground1 }}
          />
        );
      case 'missing':
        return (
          <Warning24Regular
            className={styles.statusIcon}
            style={{ color: tokens.colorPaletteYellowForeground1 }}
          />
        );
      case 'skipped':
        return (
          <Warning24Regular
            className={styles.statusIcon}
            style={{ color: tokens.colorNeutralForeground3 }}
          />
        );
      case 'error':
        return (
          <Dismiss24Regular
            className={styles.statusIcon}
            style={{ color: tokens.colorPaletteRedForeground1 }}
          />
        );
    }
  };

  const getStatusBadge = (dep: Dependency) => {
    if (dep.installing) {
      return (
        <Badge appearance="filled" color="informative">
          Installing...
        </Badge>
      );
    }
    switch (dep.status) {
      case 'checking':
        return <Badge appearance="outline">Checking...</Badge>;
      case 'installed':
        return (
          <Badge appearance="filled" color="success">
            Installed
          </Badge>
        );
      case 'missing':
        return (
          <Badge appearance="filled" color="warning">
            Not Found
          </Badge>
        );
      case 'skipped':
        return (
          <Badge appearance="tint" color="warning">
            Skipped
          </Badge>
        );
      case 'error':
        return (
          <Badge appearance="filled" color="danger">
            Error
          </Badge>
        );
    }
  };

  const installedCount = dependencies.filter((d) => d.status === 'installed').length;
  const skippedCount = dependencies.filter((d) => d.status === 'skipped').length;
  const requiredCount = dependencies.filter((d) => d.required).length;
  const requiredInstalled = dependencies.filter(
    (d) => d.required && d.status === 'installed'
  ).length;

  // All required installed only if there are required deps and all are installed
  const allRequiredInstalled = requiredCount > 0 && requiredInstalled === requiredCount;

  return (
    <div className={styles.container}>
      <div className={styles.header}>
        <Title2>Dependency Validation</Title2>
        <Text>
          Checking for required components to ensure the best experience. We&apos;ll help you
          install anything that&apos;s missing.
        </Text>
      </div>

      {/* Overall Summary */}
      <Card className={styles.summaryCard}>
        <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
          <div>
            <Title3 style={{ marginBottom: tokens.spacingVerticalXS }}>
              {allRequiredInstalled
                ? '✓ All Required Dependencies Installed'
                : 'Dependencies Status'}
            </Title3>
            <Text size={300}>
              {installedCount} of {dependencies.length} components installed
              {requiredCount > 0 && ` (${requiredInstalled}/${requiredCount} required)`}
              {skippedCount > 0 && `, ${skippedCount} skipped`}
            </Text>
          </div>
          {onRescan && (
            <Button
              appearance="secondary"
              onClick={onRescan}
              disabled={isScanning}
              icon={isScanning ? <Spinner size="tiny" /> : <Settings24Regular />}
            >
              {isScanning ? 'Scanning...' : 'Rescan'}
            </Button>
          )}
        </div>
      </Card>

      {/* Dependencies List */}
      <div className={styles.dependenciesGrid}>
        {dependencies.map((dep) => (
          <Card key={dep.id} className={styles.dependencyCard}>
            <div className={styles.dependencyHeader}>
              <div className={styles.dependencyInfo}>
                {getStatusIcon(dep.status)}
                <div className={styles.dependencyDetails}>
                  <Title3 className={styles.dependencyName}>
                    {dep.name}
                    {dep.required && (
                      <Badge
                        appearance="tint"
                        color="danger"
                        style={{ marginLeft: tokens.spacingHorizontalS }}
                      >
                        Required
                      </Badge>
                    )}
                  </Title3>
                  <Text className={styles.dependencyDescription} size={200}>
                    {dep.description}
                  </Text>
                </div>
              </div>
              <div className={styles.actionsContainer}>{getStatusBadge(dep)}</div>
            </div>

            {/* Installation Progress */}
            {dep.installing && dep.installProgress !== undefined && (
              <div className={styles.progressSection}>
                <ProgressBar value={dep.installProgress} max={100} />
                <Text size={200} style={{ marginTop: tokens.spacingVerticalXS }}>
                  Installing... {dep.installProgress}%
                </Text>
              </div>
            )}

            {/* Details Section */}
            {expandedIds.has(dep.id) && dep.status !== 'checking' && (
              <div className={styles.detailsSection}>
                {dep.status === 'installed' && (
                  <>
                    <div className={styles.detailRow}>
                      <Text weight="semibold">Version:</Text>
                      <Text>{dep.version || 'Unknown'}</Text>
                    </div>
                    {dep.installPath && (
                      <div className={styles.detailRow}>
                        <Text weight="semibold">Location:</Text>
                        <Text size={200}>{dep.installPath}</Text>
                      </div>
                    )}
                  </>
                )}

                {dep.status === 'skipped' && (
                  <>
                    <Text
                      style={{
                        color: tokens.colorNeutralForeground3,
                        marginBottom: tokens.spacingVerticalM,
                      }}
                    >
                      ⚠ Skipped - You can install this later in Settings
                    </Text>
                    <Text weight="semibold" style={{ marginBottom: tokens.spacingVerticalS }}>
                      Installation Options:
                    </Text>
                    <div
                      style={{ display: 'flex', gap: tokens.spacingHorizontalS, flexWrap: 'wrap' }}
                    >
                      {dep.canAutoInstall && (
                        <Button
                          appearance="primary"
                          icon={<ArrowDownload24Regular />}
                          onClick={() => onAutoInstall(dep.id)}
                          disabled={dep.installing}
                        >
                          Install Now
                        </Button>
                      )}
                      <Button
                        appearance="secondary"
                        onClick={() => onManualInstall(dep.id)}
                        disabled={dep.installing}
                      >
                        Download Guide
                      </Button>
                    </div>

                    {onAssignPath && (
                      <>
                        <Text
                          weight="semibold"
                          style={{
                            marginTop: tokens.spacingVerticalM,
                            marginBottom: tokens.spacingVerticalS,
                          }}
                        >
                          Or assign existing installation:
                        </Text>
                        <PathSelector
                          label={`${dep.name} Installation Path`}
                          placeholder={getPlaceholderForDependency(dep.id)}
                          value={manualPaths[dep.id] || ''}
                          onChange={(path) => handlePathChange(dep.id, path)}
                          onValidate={async (path) => {
                            return await validateDependencyPath(dep.id, path);
                          }}
                          helpText={getHelpTextForDependency(dep.id)}
                          defaultPath={getDefaultPathForDependency(dep.id)}
                          dependencyId={dep.id}
                          disabled={assigningPath === dep.id}
                          autoDetect={async () => {
                            return await autoDetectDependency(dep.id);
                          }}
                        />
                        <Button
                          appearance="primary"
                          onClick={() => handleAssignPath(dep.id)}
                          disabled={assigningPath === dep.id || !manualPaths[dep.id]}
                          style={{ marginTop: tokens.spacingVerticalS }}
                        >
                          {assigningPath === dep.id ? <Spinner size="tiny" /> : 'Apply Path'}
                        </Button>
                        {pathErrors[dep.id] && (
                          <Text
                            size={200}
                            style={{
                              color: tokens.colorPaletteRedForeground1,
                              marginTop: tokens.spacingVerticalXS,
                            }}
                          >
                            {pathErrors[dep.id]}
                          </Text>
                        )}
                      </>
                    )}
                  </>
                )}

                {dep.status === 'missing' && (
                  <>
                    <Text weight="semibold" style={{ marginBottom: tokens.spacingVerticalS }}>
                      Installation Options:
                    </Text>
                    <div
                      style={{ display: 'flex', gap: tokens.spacingHorizontalS, flexWrap: 'wrap' }}
                    >
                      {dep.canAutoInstall && (
                        <Button
                          appearance="primary"
                          icon={<ArrowDownload24Regular />}
                          onClick={() => onAutoInstall(dep.id)}
                          disabled={dep.installing}
                        >
                          Auto Install
                        </Button>
                      )}
                      <Button
                        appearance="secondary"
                        onClick={() => onManualInstall(dep.id)}
                        disabled={dep.installing}
                      >
                        Download Guide
                      </Button>
                      {!dep.required && (
                        <Button
                          appearance="subtle"
                          onClick={() => onSkip(dep.id)}
                          disabled={dep.installing}
                        >
                          Skip
                        </Button>
                      )}
                    </div>

                    {onAssignPath && (
                      <>
                        <Text
                          weight="semibold"
                          style={{
                            marginTop: tokens.spacingVerticalM,
                            marginBottom: tokens.spacingVerticalS,
                          }}
                        >
                          Or assign existing installation:
                        </Text>
                        <PathSelector
                          label={`${dep.name} Installation Path`}
                          placeholder={getPlaceholderForDependency(dep.id)}
                          value={manualPaths[dep.id] || ''}
                          onChange={(path) => handlePathChange(dep.id, path)}
                          onValidate={async (path) => {
                            return await validateDependencyPath(dep.id, path);
                          }}
                          helpText={getHelpTextForDependency(dep.id)}
                          defaultPath={getDefaultPathForDependency(dep.id)}
                          dependencyId={dep.id}
                          disabled={assigningPath === dep.id}
                          autoDetect={async () => {
                            return await autoDetectDependency(dep.id);
                          }}
                        />
                        <Button
                          appearance="primary"
                          onClick={() => handleAssignPath(dep.id)}
                          disabled={assigningPath === dep.id || !manualPaths[dep.id]}
                          style={{ marginTop: tokens.spacingVerticalS }}
                        >
                          {assigningPath === dep.id ? <Spinner size="tiny" /> : 'Apply Path'}
                        </Button>
                        {pathErrors[dep.id] && (
                          <Text
                            size={200}
                            style={{
                              color: tokens.colorPaletteRedForeground1,
                              marginTop: tokens.spacingVerticalXS,
                            }}
                          >
                            {pathErrors[dep.id]}
                          </Text>
                        )}
                      </>
                    )}
                  </>
                )}

                {dep.status === 'error' && dep.errorMessage && (
                  <div>
                    <Text weight="semibold" style={{ color: tokens.colorPaletteRedForeground1 }}>
                      Error:{' '}
                    </Text>
                    <Text size={200} style={{ marginTop: tokens.spacingVerticalXS }}>
                      {dep.errorMessage}
                    </Text>
                  </div>
                )}
              </div>
            )}

            {/* Toggle Details Button */}
            {dep.status !== 'checking' && (
              <Button
                appearance="subtle"
                size="small"
                onClick={() => {
                  const newExpandedIds = new Set(expandedIds);
                  if (expandedIds.has(dep.id)) {
                    newExpandedIds.delete(dep.id);
                  } else {
                    newExpandedIds.add(dep.id);
                  }
                  setExpandedIds(newExpandedIds);
                }}
                style={{ marginTop: tokens.spacingVerticalS }}
              >
                {expandedIds.has(dep.id) ? 'Hide Details' : 'Show Details'}
              </Button>
            )}
          </Card>
        ))}
      </div>
    </div>
  );

  function getPlaceholderForDependency(depId: string): string {
    switch (depId.toLowerCase()) {
      case 'ollama':
        return 'Click Browse to select ollama.exe';
      case 'ffmpeg':
        return 'Click Browse to select ffmpeg.exe';
      case 'stable-diffusion':
      case 'stable-diffusion-webui':
        return 'Enter Stable Diffusion WebUI URL or path';
      default:
        return 'Click Browse to select file';
    }
  }

  function getHelpTextForDependency(depId: string): string {
    switch (depId.toLowerCase()) {
      case 'ollama':
        return 'Select the ollama.exe file location. Ollama is required for local AI model processing.';
      case 'ffmpeg':
        return 'Select the ffmpeg.exe file location. FFmpeg is required for video rendering.';
      case 'stable-diffusion':
      case 'stable-diffusion-webui':
        return 'Enter the Stable Diffusion WebUI URL (e.g., http://localhost:7860) or installation path.';
      default:
        return 'Select the installation path or executable file.';
    }
  }

  function getDefaultPathForDependency(depId: string): string {
    switch (depId.toLowerCase()) {
      case 'ollama':
        return 'C:\\Users\\{username}\\AppData\\Local\\Programs\\Ollama\\ollama.exe';
      case 'ffmpeg':
        return 'C:\\ffmpeg\\bin\\ffmpeg.exe';
      case 'stable-diffusion':
      case 'stable-diffusion-webui':
        return 'http://localhost:7860';
      default:
        return '';
    }
  }

  async function validateDependencyPath(
    depId: string,
    path: string
  ): Promise<{ isValid: boolean; message: string; version?: string }> {
    try {
      const response = await fetch(`/api/dependencies/${encodeURIComponent(depId)}/validate-path`, {
        method: 'POST',
        headers: {
          'Content-Type': 'application/json',
        },
        body: JSON.stringify({ path }),
      });

      if (!response.ok) {
        return {
          isValid: false,
          message: `Validation failed: HTTP ${response.status}`,
        };
      }

      const result = await response.json();
      return {
        isValid: result.isValid || false,
        message: result.message || 'Unknown validation result',
        version: result.version,
      };
    } catch (error: unknown) {
      const errorMessage = error instanceof Error ? error.message : 'Validation error';
      return {
        isValid: false,
        message: errorMessage,
      };
    }
  }

  async function autoDetectDependency(depId: string): Promise<string | null> {
    try {
      const response = await fetch(`/api/dependencies/${encodeURIComponent(depId)}/detect`, {
        method: 'POST',
      });

      if (!response.ok) {
        return null;
      }

      const result = await response.json();
      return result.path || null;
    } catch (error: unknown) {
      console.error('Auto-detect failed:', error);
      return null;
    }
  }
}
