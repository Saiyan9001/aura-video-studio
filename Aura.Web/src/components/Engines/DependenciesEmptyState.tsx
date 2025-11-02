import { makeStyles, tokens, Text, Button, Card } from '@fluentui/react-components';
import { Settings24Regular, ArrowSync24Regular } from '@fluentui/react-icons';
import { useNavigate } from 'react-router-dom';

const useStyles = makeStyles({
  emptyStateCard: {
    padding: tokens.spacingVerticalXXL,
    textAlign: 'center',
    maxWidth: '600px',
    margin: '0 auto',
  },
  icon: {
    fontSize: '64px',
    color: tokens.colorNeutralForeground3,
    marginBottom: tokens.spacingVerticalL,
  },
  title: {
    marginBottom: tokens.spacingVerticalM,
  },
  description: {
    color: tokens.colorNeutralForeground3,
    marginBottom: tokens.spacingVerticalL,
  },
  actions: {
    display: 'flex',
    gap: tokens.spacingHorizontalM,
    justifyContent: 'center',
    flexWrap: 'wrap',
  },
  stepsList: {
    textAlign: 'left',
    marginTop: tokens.spacingVerticalL,
    marginBottom: tokens.spacingVerticalL,
    paddingLeft: tokens.spacingHorizontalXL,
  },
});

interface DependenciesEmptyStateProps {
  onRetry?: () => void;
  isBackendDown?: boolean;
}

export function DependenciesEmptyState({
  onRetry,
  isBackendDown = false,
}: DependenciesEmptyStateProps) {
  const styles = useStyles();
  const navigate = useNavigate();

  if (isBackendDown) {
    return (
      <Card className={styles.emptyStateCard}>
        <div className={styles.icon}>⚠️</div>
        <Text size={600} weight="semibold" className={styles.title}>
          Backend Not Available
        </Text>
        <Text className={styles.description}>
          The Aura backend server is not running or is unreachable. Dependencies management requires
          the backend to be active.
        </Text>
        <div className={styles.stepsList}>
          <Text weight="semibold">To start the backend:</Text>
          <ol>
            <li>Open a terminal in the Aura directory</li>
            <li>
              Run: <code>dotnet run --project Aura.Api</code>
            </li>
            <li>Wait for the server to start (usually on port 5005)</li>
            <li>Return here and click Retry</li>
          </ol>
        </div>
        <div className={styles.actions}>
          {onRetry && (
            <Button appearance="primary" icon={<ArrowSync24Regular />} onClick={onRetry}>
              Retry Connection
            </Button>
          )}
        </div>
      </Card>
    );
  }

  return (
    <Card className={styles.emptyStateCard}>
      <div className={styles.icon}>📦</div>
      <Text size={600} weight="semibold" className={styles.title}>
        Dependencies Not Configured Yet
      </Text>
      <Text className={styles.description}>
        No program dependencies have been installed or detected. Complete the onboarding process or
        manually configure dependencies to get started with video generation.
      </Text>
      <div className={styles.actions}>
        <Button
          appearance="primary"
          icon={<Settings24Regular />}
          onClick={() => navigate('/onboarding')}
        >
          Start Onboarding
        </Button>
        {onRetry && (
          <Button appearance="secondary" icon={<ArrowSync24Regular />} onClick={onRetry}>
            Refresh
          </Button>
        )}
      </div>
    </Card>
  );
}
