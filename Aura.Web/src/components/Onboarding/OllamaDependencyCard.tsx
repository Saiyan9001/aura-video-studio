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
import { Checkmark24Regular, Warning24Regular, Settings24Regular } from '@fluentui/react-icons';
import { useState, useEffect } from 'react';
import { offlineProvidersApi } from '../../services/api/offlineProvidersApi';

const useStyles = makeStyles({
  card: {
    padding: tokens.spacingVerticalL,
    transition: 'all 0.2s ease-in-out',
    ':hover': {
      boxShadow: tokens.shadow8,
    },
  },
  header: {
    display: 'flex',
    alignItems: 'center',
    justifyContent: 'space-between',
    marginBottom: tokens.spacingVerticalM,
  },
  info: {
    display: 'flex',
    alignItems: 'center',
    gap: tokens.spacingHorizontalM,
    flex: 1,
  },
  statusIcon: {
    fontSize: '32px',
  },
  details: {
    flex: 1,
  },
  name: {
    marginBottom: tokens.spacingVerticalXS,
  },
  description: {
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
});

export interface OllamaDependencyCardProps {
  autoDetect?: boolean;
}

export function OllamaDependencyCard({ autoDetect = true }: OllamaDependencyCardProps) {
  const styles = useStyles();
  const [isAvailable, setIsAvailable] = useState<boolean | null>(null);
  const [isChecking, setIsChecking] = useState(false);
  const [showDetails, setShowDetails] = useState(false);

  const checkStatus = async () => {
    setIsChecking(true);
    try {
      const result = await offlineProvidersApi.checkOllama();
      setIsAvailable(result.isAvailable);
    } catch (error: unknown) {
      console.error('Ollama check failed:', error);
      setIsAvailable(false);
    } finally {
      setIsChecking(false);
    }
  };

  useEffect(() => {
    if (autoDetect) {
      checkStatus();
    }
  }, [autoDetect]);

  const getStatusIcon = () => {
    if (isChecking) {
      return <Spinner size="medium" className={styles.statusIcon} />;
    }

    if (isAvailable) {
      return (
        <Checkmark24Regular
          className={styles.statusIcon}
          style={{ color: tokens.colorPaletteGreenForeground1 }}
        />
      );
    }

    return (
      <Warning24Regular
        className={styles.statusIcon}
        style={{ color: tokens.colorNeutralForeground3 }}
      />
    );
  };

  const getStatusBadge = () => {
    if (isChecking) {
      return <Badge appearance="outline">Checking...</Badge>;
    }

    if (isAvailable) {
      return (
        <Badge appearance="filled" color="success">
          Running
        </Badge>
      );
    }

    return (
      <Badge appearance="tint" color="subtle">
        Not Running
      </Badge>
    );
  };

  return (
    <Card className={styles.card}>
      <div className={styles.header}>
        <div className={styles.info}>
          {getStatusIcon()}
          <div className={styles.details}>
            <Title3 className={styles.name}>
              Ollama (Local AI)
              <Badge
                appearance="tint"
                color="informative"
                style={{ marginLeft: tokens.spacingHorizontalS }}
              >
                Optional
              </Badge>
            </Title3>
            <Text className={styles.description} size={200}>
              Run AI models locally for script generation. Privacy-focused alternative to cloud
              APIs.
            </Text>
          </div>
        </div>
        <div className={styles.actionsContainer}>{getStatusBadge()}</div>
      </div>

      {showDetails && (
        <div className={styles.detailsSection}>
          {isAvailable ? (
            <Text>
              Ollama is running and available at{' '}
              <strong style={{ fontFamily: 'monospace' }}>http://localhost:11434</strong>
            </Text>
          ) : (
            <>
              <Text
                style={{
                  color: tokens.colorNeutralForeground3,
                  marginBottom: tokens.spacingVerticalM,
                }}
              >
                Ollama is not currently running. It&apos;s optional and can be configured later in
                Settings if you want to use local AI models.
              </Text>
              <Button
                appearance="secondary"
                onClick={() => {
                  window.open('https://ollama.ai', '_blank');
                }}
              >
                Learn More About Ollama
              </Button>
            </>
          )}
        </div>
      )}

      <div
        style={{
          display: 'flex',
          gap: tokens.spacingHorizontalS,
          marginTop: tokens.spacingVerticalM,
        }}
      >
        <Button
          appearance="subtle"
          size="small"
          onClick={() => setShowDetails(!showDetails)}
          disabled={isChecking}
        >
          {showDetails ? 'Hide Details' : 'Show Details'}
        </Button>
        <Button
          appearance="subtle"
          size="small"
          icon={<Settings24Regular />}
          onClick={checkStatus}
          disabled={isChecking}
        >
          Auto-Detect
        </Button>
      </div>
    </Card>
  );
}
