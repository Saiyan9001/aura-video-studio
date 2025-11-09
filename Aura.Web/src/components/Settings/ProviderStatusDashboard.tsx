import {
  makeStyles,
  tokens,
  Title3,
  Text,
  Card,
  Button,
  Spinner,
  Badge,
} from '@fluentui/react-components';
import {
  CheckmarkCircle24Filled,
  DismissCircle24Filled,
  ArrowClockwise24Regular,
} from '@fluentui/react-icons';
import { useEffect } from 'react';
import { useProviderStore } from '../../state/providers';
import type { ProviderStatus } from '../../state/providers';

const useStyles = makeStyles({
  container: {
    padding: tokens.spacingVerticalL,
  },
  header: {
    display: 'flex',
    justifyContent: 'space-between',
    alignItems: 'center',
    marginBottom: tokens.spacingVerticalL,
  },
  grid: {
    display: 'grid',
    gridTemplateColumns: 'repeat(auto-fill, minmax(280px, 1fr))',
    gap: tokens.spacingVerticalM,
  },
  providerCard: {
    padding: tokens.spacingVerticalM,
  },
  providerHeader: {
    display: 'flex',
    justifyContent: 'space-between',
    alignItems: 'center',
    marginBottom: tokens.spacingVerticalS,
  },
  providerName: {
    fontWeight: tokens.fontWeightSemibold,
  },
  statusRow: {
    display: 'flex',
    alignItems: 'center',
    gap: tokens.spacingHorizontalXS,
    marginTop: tokens.spacingVerticalXS,
  },
  icon: {
    fontSize: '20px',
  },
  loadingContainer: {
    display: 'flex',
    justifyContent: 'center',
    alignItems: 'center',
    padding: tokens.spacingVerticalXXL,
  },
});

function ProviderStatusCard({ provider }: { provider: ProviderStatus }) {
  const styles = useStyles();

  const statusIcon = provider.isConfigured ? (
    <CheckmarkCircle24Filled
      className={styles.icon}
      style={{ color: tokens.colorPaletteGreenForeground1 }}
    />
  ) : (
    <DismissCircle24Filled
      className={styles.icon}
      style={{ color: tokens.colorNeutralForeground3 }}
    />
  );

  const statusBadge = provider.isConfigured ? (
    <Badge appearance="filled" color="success">
      Configured
    </Badge>
  ) : (
    <Badge appearance="outline" color="subtle">
      Not Configured
    </Badge>
  );

  return (
    <Card className={styles.providerCard}>
      <div className={styles.providerHeader}>
        <Text className={styles.providerName}>{provider.name}</Text>
        {statusBadge}
      </div>
      <div className={styles.statusRow}>
        {statusIcon}
        <Text size={200}>{provider.status}</Text>
      </div>
      {provider.lastValidated && (
        <Text size={100} style={{ marginTop: tokens.spacingVerticalXS }}>
          Last validated: {new Date(provider.lastValidated).toLocaleString()}
        </Text>
      )}
      {provider.errorMessage && (
        <Text
          size={200}
          style={{
            color: tokens.colorPaletteRedForeground1,
            marginTop: tokens.spacingVerticalXS,
          }}
        >
          {provider.errorMessage}
        </Text>
      )}
    </Card>
  );
}

export function ProviderStatusDashboard() {
  const styles = useStyles();
  const { providerStatuses, isLoadingStatuses, refreshProviderStatuses } = useProviderStore();

  useEffect(() => {
    refreshProviderStatuses();

    const pollInterval = setInterval(() => {
      refreshProviderStatuses();
    }, 30000);

    return () => clearInterval(pollInterval);
  }, [refreshProviderStatuses]);

  if (isLoadingStatuses) {
    return (
      <Card className={styles.container}>
        <div className={styles.loadingContainer}>
          <Spinner label="Loading provider status..." />
        </div>
      </Card>
    );
  }

  return (
    <Card className={styles.container}>
      <div className={styles.header}>
        <Title3>Provider Status</Title3>
        <Button
          icon={<ArrowClockwise24Regular />}
          onClick={refreshProviderStatuses}
          disabled={isLoadingStatuses}
        >
          Refresh
        </Button>
      </div>
      <Text size={200} style={{ marginBottom: tokens.spacingVerticalL }}>
        View the status of all configured providers. Status updates automatically every 30 seconds.
        Green checkmarks indicate configured providers with valid API keys.
      </Text>
      <div className={styles.grid}>
        {providerStatuses.map((provider) => (
          <ProviderStatusCard key={provider.name} provider={provider} />
        ))}
      </div>
    </Card>
  );
}
