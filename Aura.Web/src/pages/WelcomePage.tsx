import {
  makeStyles,
  tokens,
  Title1,
  Title2,
  Title3,
  Text,
  Button,
  Card,
  CardHeader,
  Spinner,
  Badge,
  Tooltip,
} from '@fluentui/react-components';
import { Play24Regular, Settings24Regular, Rocket24Regular } from '@fluentui/react-icons';
import { useState, useEffect } from 'react';
import { useNavigate } from 'react-router-dom';
import { FirstRunDiagnostics } from '../components/FirstRunDiagnostics';
import { SystemCheckCard } from '../components/SystemCheckCard';
import { TooltipContent, TooltipWithLink } from '../components/Tooltips';
import { apiUrl } from '../config/api';
import { hasCompletedFirstRun } from '../services/firstRunService';
import type { HardwareCapabilities } from '../types';

const useStyles = makeStyles({
  container: {
    maxWidth: '1200px',
    margin: '0 auto',
  },
  hero: {
    textAlign: 'center',
    marginBottom: tokens.spacingVerticalXXXL,
    padding: tokens.spacingVerticalXXL,
    borderRadius: tokens.borderRadiusLarge,
    background: `linear-gradient(135deg, ${tokens.colorNeutralBackground2} 0%, ${tokens.colorNeutralBackground1} 100%)`,
    boxShadow: '0 4px 12px rgba(0, 0, 0, 0.1)',
  },
  title: {
    marginBottom: tokens.spacingVerticalM,
    display: 'block',
    background: `linear-gradient(135deg, ${tokens.colorBrandForeground1} 0%, ${tokens.colorPalettePurpleForeground2} 100%)`,
    WebkitBackgroundClip: 'text',
    WebkitTextFillColor: 'transparent',
    backgroundClip: 'text',
  },
  subtitle: {
    color: tokens.colorNeutralForeground2,
    marginBottom: tokens.spacingVerticalXL,
    display: 'block',
  },
  actions: {
    display: 'flex',
    gap: tokens.spacingHorizontalM,
    justifyContent: 'center',
    marginTop: tokens.spacingVerticalXL,
  },
  firstTimeCallout: {
    marginBottom: tokens.spacingVerticalXXXL,
    padding: tokens.spacingVerticalXXL,
    borderRadius: tokens.borderRadiusLarge,
    background: `linear-gradient(135deg, ${tokens.colorNeutralBackground2} 0%, ${tokens.colorNeutralBackground1} 100%)`,
    boxShadow: tokens.shadow16,
    textAlign: 'center',
    border: `2px solid ${tokens.colorBrandStroke1}`,
  },
  calloutTitle: {
    marginBottom: tokens.spacingVerticalL,
    fontSize: '28px',
    fontWeight: tokens.fontWeightBold,
    color: tokens.colorNeutralForeground1,
  },
  calloutText: {
    fontSize: '16px',
    color: tokens.colorNeutralForeground2,
    maxWidth: '600px',
    margin: '0 auto',
    lineHeight: '1.6',
    marginBottom: tokens.spacingVerticalXL,
  },
  primaryOnboardingButton: {
    fontSize: '18px',
    padding: '16px 48px',
    height: 'auto',
    minHeight: '56px',
    fontWeight: tokens.fontWeightSemibold,
  },
  reconfigureCallout: {
    marginBottom: tokens.spacingVerticalXXL,
    padding: tokens.spacingVerticalXL,
    borderRadius: tokens.borderRadiusLarge,
    background: `linear-gradient(135deg, ${tokens.colorNeutralBackground2} 0%, ${tokens.colorNeutralBackground1} 100%)`,
    boxShadow: tokens.shadow8,
    textAlign: 'center',
    border: `1px solid ${tokens.colorNeutralStroke1}`,
  },
  reconfigureTitle: {
    marginBottom: tokens.spacingVerticalM,
    fontSize: '20px',
    fontWeight: tokens.fontWeightSemibold,
    color: tokens.colorNeutralForeground1,
  },
  reconfigureText: {
    fontSize: '14px',
    color: tokens.colorNeutralForeground2,
    maxWidth: '500px',
    margin: '0 auto',
    lineHeight: '1.5',
    marginBottom: tokens.spacingVerticalL,
  },
  secondaryOnboardingButton: {
    fontSize: '16px',
    padding: '12px 32px',
    height: 'auto',
    minHeight: '48px',
    fontWeight: tokens.fontWeightSemibold,
  },
  grid: {
    display: 'grid',
    gridTemplateColumns: 'repeat(auto-fit, minmax(300px, 1fr))',
    gap: tokens.spacingVerticalL,
    marginTop: tokens.spacingVerticalXXL,
  },
  card: {
    height: '100%',
    transition: 'all 0.3s cubic-bezier(0.4, 0, 0.2, 1)',
    cursor: 'default',
    ':hover': {
      transform: 'translateY(-4px)',
      boxShadow: '0 8px 20px rgba(0, 0, 0, 0.15)',
    },
  },
  statusBadge: {
    marginLeft: tokens.spacingHorizontalS,
  },
  cardContent: {
    display: 'flex',
    flexDirection: 'column',
    gap: tokens.spacingVerticalS,
  },
});

interface HealthStatus {
  status: string;
  timestamp: string;
}

export function WelcomePage() {
  const styles = useStyles();
  const navigate = useNavigate();
  const [health, setHealth] = useState<HealthStatus | null>(null);
  const [capabilities, setCapabilities] = useState<HardwareCapabilities | null>(null);
  const [loading, setLoading] = useState(true);
  const [isFirstRun, setIsFirstRun] = useState(false);
  const [checkingFirstRun, setCheckingFirstRun] = useState(true);

  useEffect(() => {
    // Check first-run status
    const checkFirstRunStatus = async () => {
      try {
        const completed = await hasCompletedFirstRun();
        setIsFirstRun(!completed);
      } catch (error) {
        console.error('Error checking first-run status:', error);
        setIsFirstRun(false);
      } finally {
        setCheckingFirstRun(false);
      }
    };

    checkFirstRunStatus();
  }, []);

  useEffect(() => {
    // Fetch system info on mount
    const fetchHealthData = async () => {
      try {
        const response = await fetch(apiUrl('/api/healthz'));
        if (response.ok) {
          return await response.json();
        }
        console.warn('Health check failed with status:', response.status);
        return null;
      } catch (err) {
        console.error('Failed to fetch health data:', err);
        return null;
      }
    };

    const fetchCapabilitiesData = async () => {
      try {
        const response = await fetch(apiUrl('/api/capabilities'));
        if (response.ok) {
          return await response.json();
        }
        console.warn('Capabilities check failed with status:', response.status);
        return null;
      } catch (err) {
        console.error('Failed to fetch capabilities data:', err);
        return null;
      }
    };

    Promise.all([fetchHealthData(), fetchCapabilitiesData()])
      .then(([healthData, capData]) => {
        setHealth(healthData);
        setCapabilities(capData);
        setLoading(false);
      })
      .catch((err) => {
        console.error('Failed to fetch system info:', err);
        setLoading(false);
      });
  }, []);

  return (
    <div className={styles.container}>
      {/* First-Run Diagnostics */}
      <FirstRunDiagnostics
        autoRun={true}
        onNeedsSetup={() => {
          // Optionally redirect to onboarding if critical issues found
        }}
      />

      {/* System Health Check */}
      <SystemCheckCard autoRetry={true} retryInterval={30000} />

      {/* Prominent First-Time Setup Callout - only show if user hasn't completed onboarding */}
      {!checkingFirstRun && isFirstRun && (
        <div className={styles.firstTimeCallout}>
          <Title3 className={styles.calloutTitle}>
            <Rocket24Regular
              style={{
                fontSize: '32px',
                marginRight: tokens.spacingHorizontalM,
                verticalAlign: 'middle',
              }}
            />
            🚀 REQUIRED: Complete Initial Setup Before Proceeding
          </Title3>
          <Text className={styles.calloutText}>
            <strong>You must complete this setup before creating videos.</strong> This quick 3-5
            minute wizard will:
            <br />
            <br />
            ✓ Configure your AI providers (free or paid options available)
            <br />
            ✓ Detect your hardware capabilities for optimal performance
            <br />
            ✓ Set up your workspace and ensure all dependencies are ready
            <br />
            <br />
            Don&apos;t worry - the setup is flexible. You can use completely free tools or add API
            keys for premium services. You&apos;ll have full control over your configuration!
          </Text>
          <Button
            appearance="primary"
            size="large"
            className={styles.primaryOnboardingButton}
            icon={<Rocket24Regular />}
            onClick={() => navigate('/setup')}
          >
            ▶ Start Required Setup Now
          </Button>
        </div>
      )}

      {/* Reconfigure Setup Callout - show for returning users who have completed onboarding */}
      {!checkingFirstRun && !isFirstRun && (
        <div className={styles.reconfigureCallout}>
          <Title3 className={styles.reconfigureTitle}>
            <Settings24Regular
              style={{
                fontSize: '24px',
                marginRight: tokens.spacingHorizontalM,
                verticalAlign: 'middle',
              }}
            />
            Need to Reconfigure Your Setup?
          </Title3>
          <Text className={styles.reconfigureText}>
            Use the setup wizard to update API keys, configure dependencies, or adjust your
            workspace preferences.
          </Text>
          <Button
            appearance="primary"
            size="large"
            className={styles.secondaryOnboardingButton}
            icon={<Settings24Regular />}
            onClick={() => navigate('/setup')}
          >
            Configure Setup
          </Button>
        </div>
      )}

      <div className={styles.hero}>
        <Title1 className={styles.title}>Welcome to Aura Video Studio</Title1>
        <Text size={500} className={styles.subtitle}>
          Create professional YouTube videos with AI-powered automation
        </Text>

        <div className={styles.actions}>
          <Tooltip
            content={<TooltipWithLink content={TooltipContent.welcomeCreateNew} />}
            relationship="description"
          >
            <Button
              appearance="primary"
              size="large"
              icon={<Play24Regular />}
              onClick={() => navigate('/create')}
            >
              Create Video
            </Button>
          </Tooltip>
          <Tooltip
            content={<TooltipWithLink content={TooltipContent.welcomeSettings} />}
            relationship="description"
          >
            <Button size="large" icon={<Settings24Regular />} onClick={() => navigate('/settings')}>
              Settings
            </Button>
          </Tooltip>
        </div>
      </div>

      <div className={styles.grid}>
        <Card className={styles.card}>
          <CardHeader
            header={<Title2>System Status</Title2>}
            description={
              loading ? (
                <Spinner size="small" label="Checking..." />
              ) : health ? (
                <Text>
                  API is {health.status}
                  <Badge appearance="filled" color="success" className={styles.statusBadge}>
                    Online
                  </Badge>
                </Text>
              ) : (
                <Text>
                  API unavailable
                  <Badge appearance="filled" color="danger" className={styles.statusBadge}>
                    Offline
                  </Badge>
                </Text>
              )
            }
          />
        </Card>

        <Card className={styles.card}>
          <CardHeader
            header={<Title2>Hardware</Title2>}
            description={
              loading ? (
                <Spinner size="small" label="Detecting..." />
              ) : capabilities ? (
                <div className={styles.cardContent}>
                  <Text>Tier: {capabilities.tier}</Text>
                  <Text>CPU: {capabilities.cpu.threads} threads</Text>
                  <Text>RAM: {capabilities.ram.gb} GB</Text>
                  {capabilities.gpu && (
                    <Text>
                      GPU: {capabilities.gpu.model} ({capabilities.gpu.vramGB} GB)
                    </Text>
                  )}
                </div>
              ) : (
                <Text>Hardware detection failed</Text>
              )
            }
          />
        </Card>

        <Card className={styles.card}>
          <CardHeader
            header={<Title2>Features Available</Title2>}
            description={
              loading ? (
                <Spinner size="small" />
              ) : capabilities ? (
                <div className={styles.cardContent}>
                  <Text>NVENC: {capabilities.enableNVENC ? '✓ Yes' : '✗ No'}</Text>
                  <Text>Stable Diffusion: {capabilities.enableSD ? '✓ Yes' : '✗ No'}</Text>
                  <Text>Offline Mode: {capabilities.offlineOnly ? '✓ Active' : '✗ Inactive'}</Text>
                </div>
              ) : (
                <Text>No capability data</Text>
              )
            }
          />
        </Card>
      </div>
    </div>
  );
}
