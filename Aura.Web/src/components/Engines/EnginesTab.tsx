import {
  makeStyles,
  tokens,
  Text,
  Spinner,
  MessageBar,
  MessageBarBody,
  Button,
  Card,
  Badge,
  Divider,
  Tooltip,
} from '@fluentui/react-components';
import {
  Folder24Regular,
  Globe24Regular,
  Copy24Regular,
  Info24Regular,
} from '@fluentui/react-icons';
import { useEffect, useState } from 'react';
import { useEnginesStore } from '../../state/engines';
import { EngineCard } from './EngineCard';
import { FFmpegCard } from './FFmpegCard';
import { OllamaCard } from './OllamaCard';

const useStyles = makeStyles({
  container: {
    display: 'flex',
    flexDirection: 'column',
    gap: tokens.spacingVerticalL,
    padding: tokens.spacingVerticalL,
  },
  header: {
    marginBottom: tokens.spacingVerticalM,
  },
  subtitle: {
    color: tokens.colorNeutralForeground3,
    marginTop: tokens.spacingVerticalXS,
  },
  enginesList: {
    display: 'flex',
    flexDirection: 'column',
    gap: tokens.spacingVerticalM,
  },
  loadingContainer: {
    display: 'flex',
    justifyContent: 'center',
    alignItems: 'center',
    padding: tokens.spacingVerticalXXL,
  },
  emptyState: {
    textAlign: 'center',
    padding: tokens.spacingVerticalXXL,
    color: tokens.colorNeutralForeground3,
  },
  instanceCard: {
    padding: tokens.spacingVerticalM,
    marginBottom: tokens.spacingVerticalS,
  },
  instanceRow: {
    display: 'flex',
    justifyContent: 'space-between',
    alignItems: 'center',
    marginBottom: tokens.spacingVerticalXS,
  },
  instanceHeader: {
    display: 'flex',
    alignItems: 'center',
    gap: tokens.spacingHorizontalS,
    flexWrap: 'wrap',
  },
  instancePath: {
    fontFamily: 'monospace',
    fontSize: tokens.fontSizeBase200,
    color: tokens.colorNeutralForeground3,
    wordBreak: 'break-all',
    display: 'flex',
    alignItems: 'center',
    gap: tokens.spacingHorizontalXS,
  },
  instanceActions: {
    display: 'flex',
    gap: tokens.spacingHorizontalS,
    flexWrap: 'wrap',
  },
  copyButton: {
    cursor: 'pointer',
    padding: '2px',
    ':hover': {
      color: tokens.colorBrandForeground1,
    },
  },
  modeTooltip: {
    cursor: 'help',
  },
});

export function EnginesTab() {
  const styles = useStyles();
  const {
    engines,
    instances,
    isLoading,
    error,
    fetchEngines,
    fetchInstances,
    openFolder,
    openWebUI,
  } = useEnginesStore();
  const [copiedPath, setCopiedPath] = useState<string | null>(null);

  useEffect(() => {
    fetchEngines();
    fetchInstances();
  }, [fetchEngines, fetchInstances]);

  const copyToClipboard = async (text: string, id: string) => {
    try {
      await navigator.clipboard.writeText(text);
      setCopiedPath(id);
      setTimeout(() => setCopiedPath(null), 2000);
    } catch (err) {
      console.error('Failed to copy:', err);
    }
  };

  if (isLoading && engines.length === 0) {
    return (
      <div className={styles.loadingContainer}>
        <Spinner label="Loading engines..." />
      </div>
    );
  }

  return (
    <div className={styles.container}>
      <div className={styles.header}>
        <Text size={600} weight="semibold">
          Local Engines
        </Text>
        <Text className={styles.subtitle}>
          Install and manage local AI engines for offline image generation and text-to-speech. All
          engines are installed to your local system without admin privileges.
        </Text>
      </div>

      {error && (
        <MessageBar intent="error">
          <MessageBarBody>{error}</MessageBarBody>
        </MessageBar>
      )}

      {/* Attached Instances Section */}
      {instances.length > 0 && (
        <>
          <div className={styles.header}>
            <Text size={500} weight="semibold">
              Engine Instances
            </Text>
            <Text className={styles.subtitle}>
              Manage your engine installations (both app-managed and external)
            </Text>
          </div>

          <div className={styles.enginesList}>
            {instances.map((instance) => (
              <Card key={instance.id} className={styles.instanceCard}>
                <div className={styles.instanceRow}>
                  <div className={styles.instanceHeader}>
                    <Text weight="semibold">{instance.name}</Text>
                    <Tooltip
                      content={
                        instance.mode === 'Managed'
                          ? 'Managed: App controls start/stop/process'
                          : 'External: You run it; app only detects/uses it'
                      }
                      relationship="description"
                    >
                      <Badge
                        appearance="filled"
                        color={instance.mode === 'Managed' ? 'brand' : 'success'}
                        className={styles.modeTooltip}
                        icon={<Info24Regular />}
                      >
                        {instance.mode}
                      </Badge>
                    </Tooltip>
                    <Badge appearance="outline" color={instance.isRunning ? 'success' : 'subtle'}>
                      {instance.status}
                    </Badge>
                    {instance.isHealthy && instance.isRunning && (
                      <Badge appearance="filled" color="success">
                        Healthy
                      </Badge>
                    )}
                  </div>
                  <div className={styles.instanceActions}>
                    <Button
                      appearance="subtle"
                      icon={<Folder24Regular />}
                      onClick={() => openFolder(instance.id)}
                    >
                      Open Folder
                    </Button>
                    {instance.port && (
                      <Button
                        appearance="subtle"
                        icon={<Globe24Regular />}
                        onClick={async () => {
                          const url = await openWebUI(instance.id);
                          window.open(url, '_blank');
                        }}
                      >
                        Open Web UI
                      </Button>
                    )}
                  </div>
                </div>
                <div>
                  <div className={styles.instancePath}>
                    <Text>Path: {instance.installPath}</Text>
                    <Tooltip
                      content={
                        copiedPath === `path-${instance.id}` ? 'Copied!' : 'Copy to clipboard'
                      }
                      relationship="label"
                    >
                      <Copy24Regular
                        className={styles.copyButton}
                        onClick={() => copyToClipboard(instance.installPath, `path-${instance.id}`)}
                      />
                    </Tooltip>
                  </div>
                  {instance.port && (
                    <div className={styles.instancePath}>
                      <Text>Port: {instance.port}</Text>
                      <Tooltip
                        content={
                          copiedPath === `port-${instance.id}` ? 'Copied!' : 'Copy to clipboard'
                        }
                        relationship="label"
                      >
                        <Copy24Regular
                          className={styles.copyButton}
                          onClick={() =>
                            copyToClipboard(instance.port?.toString() || '', `port-${instance.id}`)
                          }
                        />
                      </Tooltip>
                    </div>
                  )}
                  {instance.executablePath && (
                    <div className={styles.instancePath}>
                      <Text>Executable: {instance.executablePath}</Text>
                      <Tooltip
                        content={
                          copiedPath === `exe-${instance.id}` ? 'Copied!' : 'Copy to clipboard'
                        }
                        relationship="label"
                      >
                        <Copy24Regular
                          className={styles.copyButton}
                          onClick={() =>
                            copyToClipboard(instance.executablePath || '', `exe-${instance.id}`)
                          }
                        />
                      </Tooltip>
                    </div>
                  )}
                  {instance.notes && (
                    <Text className={styles.instancePath}>Notes: {instance.notes}</Text>
                  )}
                </div>
              </Card>
            ))}
          </div>

          <Divider />
        </>
      )}

      {/* Available Engines Section */}
      <div className={styles.header}>
        <Text size={500} weight="semibold">
          Available Engines
        </Text>
        <Text className={styles.subtitle}>
          Install new engines or attach existing installations
        </Text>
      </div>

      {/* FFmpeg - Special Card */}
      <FFmpegCard />

      {/* Ollama - Special Card with Auto-Detection */}
      <OllamaCard />

      {engines.length === 0 && !isLoading ? (
        <div className={styles.emptyState}>
          <Text>No engines available. Please check your connection and try again.</Text>
        </div>
      ) : (
        <div className={styles.enginesList}>
          {engines.map((engine) => (
            <EngineCard key={engine.id} engine={engine} />
          ))}
        </div>
      )}
    </div>
  );
}
