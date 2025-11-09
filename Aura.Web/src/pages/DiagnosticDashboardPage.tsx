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
} from '@fluentui/react-components';
import {
  Checkmark24Regular,
  Dismiss24Regular,
  ArrowClockwise24Regular,
  Warning24Regular,
  Info24Regular,
} from '@fluentui/react-icons';
import { useEffect, useState } from 'react';
import { useNotifications } from '../components/Notifications/Toasts';
import type {
  DependencyCheckResult,
  ProviderAvailabilityReport,
  AutoConfigurationResult,
} from '../services/setupService';
import { setupService } from '../services/setupService';

const useStyles = makeStyles({
  container: {
    display: 'flex',
    flexDirection: 'column',
    gap: tokens.spacingVerticalL,
    padding: tokens.spacingVerticalXXL,
    maxWidth: '1200px',
    margin: '0 auto',
  },
  header: {
    display: 'flex',
    justifyContent: 'space-between',
    alignItems: 'center',
    marginBottom: tokens.spacingVerticalL,
  },
  section: {
    display: 'flex',
    flexDirection: 'column',
    gap: tokens.spacingVerticalM,
  },
  statusCard: {
    padding: tokens.spacingVerticalL,
  },
  statusRow: {
    display: 'flex',
    justifyContent: 'space-between',
    alignItems: 'center',
    padding: tokens.spacingVerticalS,
  },
  statusIcon: {
    marginRight: tokens.spacingHorizontalS,
  },
  loadingContainer: {
    display: 'flex',
    justifyContent: 'center',
    alignItems: 'center',
    padding: tokens.spacingVerticalXXXL,
  },
  grid: {
    display: 'grid',
    gridTemplateColumns: 'repeat(auto-fit, minmax(300px, 1fr))',
    gap: tokens.spacingVerticalL,
  },
  metricCard: {
    padding: tokens.spacingVerticalL,
    display: 'flex',
    flexDirection: 'column',
    gap: tokens.spacingVerticalS,
  },
});

export function DiagnosticDashboardPage() {
  const styles = useStyles();
  const { showFailureToast } = useNotifications();

  const [loading, setLoading] = useState(true);
  const [dependencies, setDependencies] = useState<DependencyCheckResult | null>(null);
  const [providers, setProviders] = useState<ProviderAvailabilityReport | null>(null);
  const [autoConfig, setAutoConfig] = useState<AutoConfigurationResult | null>(null);

  const loadDiagnostics = async () => {
    setLoading(true);
    try {
      const [deps, provs, config] = await Promise.all([
        setupService.checkDependencies(),
        setupService.checkProviderAvailability(),
        setupService.getAutoConfiguration(),
      ]);

      setDependencies(deps);
      setProviders(provs);
      setAutoConfig(config);
    } catch (error: unknown) {
      const errorMessage = error instanceof Error ? error.message : 'Unknown error';
      showFailureToast({
        title: 'Failed to load diagnostics',
        message: errorMessage,
      });
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    void loadDiagnostics();
  }, []);

  if (loading) {
    return (
      <div className={styles.container}>
        <div className={styles.loadingContainer}>
          <Spinner size="huge" label="Loading diagnostics..." />
        </div>
      </div>
    );
  }

  return (
    <div className={styles.container}>
      <div className={styles.header}>
        <Title2>System Diagnostics</Title2>
        <Button
          icon={<ArrowClockwise24Regular />}
          onClick={() => void loadDiagnostics()}
          appearance="primary"
        >
          Refresh
        </Button>
      </div>

      <div className={styles.section}>
        <Title3>System Dependencies</Title3>
        <Card className={styles.statusCard}>
          {dependencies && (
            <>
              <DependencyStatus
                name="FFmpeg"
                installed={dependencies.ffmpeg.installed}
                version={dependencies.ffmpeg.version}
                required
              />
              <DependencyStatus
                name="Node.js"
                installed={dependencies.nodejs.installed}
                version={dependencies.nodejs.version}
                required
              />
              <DependencyStatus
                name=".NET Runtime"
                installed={dependencies.dotnet.installed}
                version={dependencies.dotnet.version}
                required
              />
              <DependencyStatus
                name="Python"
                installed={dependencies.python.installed}
                version={dependencies.python.version}
              />
              <DependencyStatus
                name="Ollama"
                installed={dependencies.ollama.installed}
                version={dependencies.ollama.version}
              />
              <DependencyStatus
                name="Piper TTS"
                installed={dependencies.piperTts.installed}
                version={dependencies.piperTts.path}
              />
              <DependencyStatus
                name="NVIDIA Drivers"
                installed={dependencies.nvidia.installed}
                version={dependencies.nvidia.version}
              />
              <div className={styles.statusRow}>
                <Text weight="semibold">Disk Space Available:</Text>
                <Badge
                  appearance={dependencies.diskSpaceGB > 10 ? 'filled' : 'outline'}
                  color={dependencies.diskSpaceGB > 10 ? 'success' : 'warning'}
                >
                  {dependencies.diskSpaceGB.toFixed(1)} GB
                </Badge>
              </div>
              <div className={styles.statusRow}>
                <Text weight="semibold">Internet Connected:</Text>
                {dependencies.internetConnected ? (
                  <Badge appearance="filled" color="success">
                    <Checkmark24Regular className={styles.statusIcon} />
                    Connected
                  </Badge>
                ) : (
                  <Badge appearance="filled" color="warning">
                    <Warning24Regular className={styles.statusIcon} />
                    Offline
                  </Badge>
                )}
              </div>
            </>
          )}
        </Card>
      </div>

      <div className={styles.section}>
        <Title3>Provider Availability</Title3>
        <Card className={styles.statusCard}>
          {providers && (
            <>
              <div className={styles.statusRow}>
                <Text weight="semibold">Ollama (Local LLM):</Text>
                <AvailabilityBadge available={providers.ollamaAvailable} />
              </div>
              <div className={styles.statusRow}>
                <Text weight="semibold">Stable Diffusion:</Text>
                <AvailabilityBadge available={providers.stableDiffusionAvailable} />
              </div>
              <div className={styles.statusRow}>
                <Text weight="semibold">Database:</Text>
                <AvailabilityBadge available={providers.databaseAvailable} />
              </div>
              <div className={styles.statusRow}>
                <Text weight="semibold">Network:</Text>
                <AvailabilityBadge available={providers.networkConnected} />
              </div>

              {providers.providers.length > 0 && (
                <>
                  <Title3 style={{ marginTop: tokens.spacingVerticalL }}>Detected Providers</Title3>
                  <Table>
                    <TableHeader>
                      <TableRow>
                        <TableHeaderCell>Provider</TableHeaderCell>
                        <TableHeaderCell>Type</TableHeaderCell>
                        <TableHeaderCell>Status</TableHeaderCell>
                      </TableRow>
                    </TableHeader>
                    <TableBody>
                      {providers.providers.map((provider, index) => (
                        <TableRow key={index}>
                          <TableCell>{provider.providerName}</TableCell>
                          <TableCell>{provider.providerType}</TableCell>
                          <TableCell>
                            <AvailabilityBadge available={provider.isAvailable} />
                          </TableCell>
                        </TableRow>
                      ))}
                    </TableBody>
                  </Table>
                </>
              )}
            </>
          )}
        </Card>
      </div>

      <div className={styles.section}>
        <Title3>Auto-Configuration Recommendations</Title3>
        <div className={styles.grid}>
          {autoConfig && (
            <>
              <Card className={styles.metricCard}>
                <Text weight="semibold">Recommended Tier</Text>
                <Title3>{autoConfig.recommendedTier}</Title3>
              </Card>

              <Card className={styles.metricCard}>
                <Text weight="semibold">Quality Preset</Text>
                <Title3>{autoConfig.recommendedQualityPreset}</Title3>
              </Card>

              <Card className={styles.metricCard}>
                <Text weight="semibold">Thread Count</Text>
                <Title3>{autoConfig.recommendedThreadCount}</Title3>
              </Card>

              <Card className={styles.metricCard}>
                <Text weight="semibold">Memory Limit</Text>
                <Title3>{autoConfig.recommendedMemoryLimitMB} MB</Title3>
              </Card>

              <Card className={styles.metricCard}>
                <Text weight="semibold">Hardware Acceleration</Text>
                <Title3>
                  {autoConfig.useHardwareAcceleration
                    ? autoConfig.hardwareAccelerationMethod?.toUpperCase() || 'Yes'
                    : 'No'}
                </Title3>
              </Card>

              <Card className={styles.metricCard}>
                <Text weight="semibold">Local Providers</Text>
                <Title3>{autoConfig.enableLocalProviders ? 'Enabled' : 'Disabled'}</Title3>
              </Card>
            </>
          )}
        </div>

        {autoConfig && autoConfig.configuredProviders.length > 0 && (
          <Card className={styles.statusCard}>
            <Text weight="semibold">Configured Providers:</Text>
            <ul>
              {autoConfig.configuredProviders.map((provider, index) => (
                <li key={index}>
                  <Text>{provider}</Text>
                </li>
              ))}
            </ul>
          </Card>
        )}
      </div>
    </div>
  );
}

function DependencyStatus({
  name,
  installed,
  version,
  required = false,
}: {
  name: string;
  installed: boolean;
  version: string | null;
  required?: boolean;
}) {
  const styles = useStyles();

  return (
    <div className={styles.statusRow}>
      <Text weight="semibold">
        {name}
        {required && ' *'}:
      </Text>
      {installed ? (
        <Badge appearance="filled" color="success">
          <Checkmark24Regular className={styles.statusIcon} />
          {version || 'Installed'}
        </Badge>
      ) : (
        <Badge appearance="filled" color={required ? 'danger' : 'informative'}>
          {required ? (
            <Dismiss24Regular className={styles.statusIcon} />
          ) : (
            <Info24Regular className={styles.statusIcon} />
          )}
          Not Installed
        </Badge>
      )}
    </div>
  );
}

function AvailabilityBadge({ available }: { available: boolean }) {
  const styles = useStyles();

  return available ? (
    <Badge appearance="filled" color="success">
      <Checkmark24Regular className={styles.statusIcon} />
      Available
    </Badge>
  ) : (
    <Badge appearance="outline" color="subtle">
      <Dismiss24Regular className={styles.statusIcon} />
      Unavailable
    </Badge>
  );
}
