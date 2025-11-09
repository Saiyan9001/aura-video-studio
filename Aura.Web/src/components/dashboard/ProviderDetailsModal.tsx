import {
  Dialog,
  DialogSurface,
  DialogTitle,
  DialogBody,
  DialogActions,
  DialogContent,
  Button,
  makeStyles,
  tokens,
  Text,
  Badge,
} from '@fluentui/react-components';
import {
  Dismiss24Regular,
  CheckmarkCircle24Filled,
  Warning24Filled,
  ErrorCircle24Filled,
} from '@fluentui/react-icons';
import type { ProviderHealth } from '../../state/dashboard';

const useStyles = makeStyles({
  content: {
    display: 'flex',
    flexDirection: 'column',
    gap: tokens.spacingVerticalL,
  },
  statRow: {
    display: 'flex',
    justifyContent: 'space-between',
    alignItems: 'center',
    padding: tokens.spacingVerticalM,
    borderRadius: tokens.borderRadiusMedium,
    backgroundColor: tokens.colorNeutralBackground2,
  },
  statLabel: {
    color: tokens.colorNeutralForeground3,
  },
  statValue: {
    fontWeight: tokens.fontWeightSemibold,
    fontSize: tokens.fontSizeBase400,
  },
  statusHeader: {
    display: 'flex',
    alignItems: 'center',
    gap: tokens.spacingHorizontalM,
    marginBottom: tokens.spacingVerticalM,
  },
  statusIcon: {
    fontSize: '32px',
  },
});

interface ProviderDetailsModalProps {
  provider: ProviderHealth | null;
  open: boolean;
  onClose: () => void;
}

export function ProviderDetailsModal({ provider, open, onClose }: ProviderDetailsModalProps) {
  const styles = useStyles();

  if (!provider) {
    return null;
  }

  const getStatusIcon = () => {
    switch (provider.status) {
      case 'healthy':
        return (
          <CheckmarkCircle24Filled
            className={styles.statusIcon}
            style={{ color: tokens.colorPaletteGreenForeground1 }}
          />
        );
      case 'degraded':
        return (
          <Warning24Filled
            className={styles.statusIcon}
            style={{ color: tokens.colorPaletteYellowForeground1 }}
          />
        );
      case 'down':
        return (
          <ErrorCircle24Filled
            className={styles.statusIcon}
            style={{ color: tokens.colorPaletteRedForeground1 }}
          />
        );
    }
  };

  const getStatusColor = (): 'success' | 'warning' | 'danger' => {
    switch (provider.status) {
      case 'healthy':
        return 'success';
      case 'degraded':
        return 'warning';
      case 'down':
        return 'danger';
    }
  };

  return (
    <Dialog open={open} onOpenChange={(_e, data) => !data.open && onClose()}>
      <DialogSurface>
        <DialogBody>
          <DialogTitle
            action={
              <Button
                appearance="subtle"
                aria-label="close"
                icon={<Dismiss24Regular />}
                onClick={onClose}
              />
            }
          >
            {provider.name} Details
          </DialogTitle>
          <DialogContent className={styles.content}>
            <div className={styles.statusHeader}>
              {getStatusIcon()}
              <div>
                <Text size={500} weight="semibold">
                  Status: {provider.status}
                </Text>
                <Badge appearance="filled" color={getStatusColor()}>
                  {provider.status.toUpperCase()}
                </Badge>
              </div>
            </div>

            <div className={styles.statRow}>
              <Text className={styles.statLabel}>Response Time</Text>
              <Text className={styles.statValue}>{provider.responseTime}ms</Text>
            </div>

            <div className={styles.statRow}>
              <Text className={styles.statLabel}>Error Rate</Text>
              <Text className={styles.statValue}>{provider.errorRate.toFixed(2)}%</Text>
            </div>

            {provider.status === 'degraded' && (
              <div
                style={{
                  padding: tokens.spacingVerticalM,
                  backgroundColor: tokens.colorPaletteYellowBackground1,
                  borderRadius: tokens.borderRadiusMedium,
                }}
              >
                <Text>
                  This provider is experiencing degraded performance. Some requests may be slower
                  than usual.
                </Text>
              </div>
            )}

            {provider.status === 'down' && (
              <div
                style={{
                  padding: tokens.spacingVerticalM,
                  backgroundColor: tokens.colorPaletteRedBackground1,
                  borderRadius: tokens.borderRadiusMedium,
                }}
              >
                <Text>
                  This provider is currently unavailable. Please check back later or use an
                  alternative provider.
                </Text>
              </div>
            )}
          </DialogContent>
          <DialogActions>
            <Button appearance="primary" onClick={onClose}>
              Close
            </Button>
          </DialogActions>
        </DialogBody>
      </DialogSurface>
    </Dialog>
  );
}
