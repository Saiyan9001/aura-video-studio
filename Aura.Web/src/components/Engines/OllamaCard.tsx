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
  Link,
} from '@fluentui/react-components';
import {
  Checkmark24Regular,
  Warning24Regular,
  ArrowSync24Regular,
  Info24Regular,
} from '@fluentui/react-icons';
import { useOllamaDetection } from '../../hooks/useOllamaDetection';

const useStyles = makeStyles({
  card: {
    width: '100%',
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
  actions: {
    display: 'flex',
    gap: tokens.spacingHorizontalS,
    alignItems: 'center',
  },
  helperText: {
    color: tokens.colorNeutralForeground3,
    fontSize: tokens.fontSizeBase200,
    marginTop: tokens.spacingVerticalXS,
  },
  infoBox: {
    padding: tokens.spacingVerticalS,
    backgroundColor: tokens.colorNeutralBackground2,
    borderRadius: tokens.borderRadiusMedium,
    borderLeft: `3px solid ${tokens.colorBrandForeground1}`,
  },
  actionArea: {
    display: 'flex',
    gap: tokens.spacingHorizontalS,
    alignItems: 'center',
    marginTop: tokens.spacingVerticalS,
  },
  statusMessage: {
    padding: tokens.spacingVerticalS,
    borderRadius: tokens.borderRadiusMedium,
    marginTop: tokens.spacingVerticalS,
  },
  statusMessageSuccess: {
    backgroundColor: tokens.colorPaletteGreenBackground1,
  },
  statusMessageNeutral: {
    backgroundColor: tokens.colorNeutralBackground2,
  },
  successText: {
    color: tokens.colorPaletteGreenForeground1,
  },
  monospaceText: {
    fontFamily: 'monospace',
  },
  neutralText: {
    color: tokens.colorNeutralForeground3,
    marginBottom: tokens.spacingVerticalS,
  },
});

/**
 * Ollama card for Settings/Downloads page with automatic detection
 * Probes localhost:11434 on mount and when user clicks Auto-detect
 */
export function OllamaCard() {
  const styles = useStyles();
  const { isDetected, isChecking, detect } = useOllamaDetection(true);

  const getStatusBadge = () => {
    if (isChecking) {
      return (
        <Badge appearance="outline" icon={<Spinner size="tiny" />}>
          Checking...
        </Badge>
      );
    }

    if (isDetected === true) {
      return (
        <Badge appearance="filled" color="success" icon={<Checkmark24Regular />}>
          Detected
        </Badge>
      );
    }

    if (isDetected === false) {
      return (
        <Badge appearance="tint" color="subtle" icon={<Warning24Regular />}>
          Not Found
        </Badge>
      );
    }

    return <Badge appearance="outline">Unknown</Badge>;
  };

  const getStatusIcon = () => {
    if (isChecking) {
      return <Spinner size="medium" />;
    }

    if (isDetected === true) {
      return (
        <Checkmark24Regular
          style={{ fontSize: '32px', color: tokens.colorPaletteGreenForeground1 }}
        />
      );
    }

    return <Warning24Regular style={{ fontSize: '32px', color: tokens.colorNeutralForeground3 }} />;
  };

  return (
    <Card className={styles.card}>
      <CardHeader
        header={
          <div className={styles.header}>
            <div className={styles.title}>
              {getStatusIcon()}
              <div>
                <Text weight="semibold" size={500}>
                  Ollama (Local AI)
                </Text>
                <Badge
                  appearance="tint"
                  color="informative"
                  style={{ marginLeft: tokens.spacingHorizontalS }}
                >
                  Optional
                </Badge>
              </div>
            </div>
            <div className={styles.actions}>{getStatusBadge()}</div>
          </div>
        }
        description={
          <Text className={styles.helperText}>
            Run AI models locally for script generation. Privacy-focused alternative to cloud APIs.
          </Text>
        }
      />
      <CardPreview className={styles.content}>
        <div className={styles.infoBox}>
          <div
            style={{ display: 'flex', alignItems: 'flex-start', gap: tokens.spacingHorizontalS }}
          >
            <Info24Regular style={{ flexShrink: 0, marginTop: '2px' }} />
            <div>
              <Text size={200}>
                If Ollama is running locally (port 11434), detection is automatic.
              </Text>
            </div>
          </div>
        </div>

        <div className={styles.actionArea}>
          <Button
            appearance="primary"
            size="medium"
            icon={<ArrowSync24Regular />}
            onClick={() => detect()}
            disabled={isChecking}
          >
            {isChecking ? 'Detecting...' : 'Auto-Detect'}
          </Button>
          {getStatusBadge()}
        </div>

        {isDetected === true && (
          <div className={`${styles.statusMessage} ${styles.statusMessageSuccess}`}>
            <Text size={200} className={styles.successText}>
              ✓ Ollama is running and available at{' '}
              <strong className={styles.monospaceText}>http://localhost:11434</strong>
            </Text>
          </div>
        )}

        {isDetected === false && (
          <div className={`${styles.statusMessage} ${styles.statusMessageNeutral}`}>
            <Text size={200} className={styles.neutralText} block>
              Ollama is not currently running. It&apos;s optional and can be configured later in
              Settings if you want to use local AI models.
            </Text>
            <Link href="https://ollama.ai" target="_blank">
              Learn More About Ollama
            </Link>
          </div>
        )}
      </CardPreview>
    </Card>
  );
}
