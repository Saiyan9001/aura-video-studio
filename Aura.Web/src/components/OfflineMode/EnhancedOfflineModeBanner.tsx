import {
  MessageBar,
  MessageBarBody,
  MessageBarTitle,
  Button,
  Spinner,
  Link,
} from '@fluentui/react-components';
import {
  DismissRegular,
  InfoRegular,
  WarningRegular,
  CheckmarkCircleRegular,
  SettingsRegular,
} from '@fluentui/react-icons';
import React, { useEffect, useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { providerStatusApi, type SystemProviderStatus } from '@/services/api/providerStatusApi';

import './OfflineModeBanner.css';

interface EnhancedOfflineModeBannerProps {
  /**
   * Whether to show the banner
   */
  show?: boolean;
  /**
   * Callback when banner is dismissed
   */
  onDismiss?: () => void;
  /**
   * Show configuration link
   */
  showConfigLink?: boolean;
}

/**
 * Enhanced banner component that displays provider status and offline mode
 * Uses the new ProviderStatusService API for comprehensive provider tracking
 */
export const EnhancedOfflineModeBanner: React.FC<EnhancedOfflineModeBannerProps> = ({
  show = true,
  onDismiss,
  showConfigLink = true,
}) => {
  const [status, setStatus] = useState<SystemProviderStatus | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [dismissed, setDismissed] = useState(false);
  const navigate = useNavigate();

  useEffect(() => {
    if (!show) return;

    const checkProviders = async () => {
      try {
        setLoading(true);
        setError(null);
        const result = await providerStatusApi.getStatus();
        setStatus(result);
      } catch (err: unknown) {
        const errorObj = err instanceof Error ? err : new Error(String(err));
        console.error('Failed to check provider status:', errorObj);
        setError('Failed to check provider status');
      } finally {
        setLoading(false);
      }
    };

    checkProviders();

    const interval = setInterval(checkProviders, 30000);
    return () => clearInterval(interval);
  }, [show]);

  const handleDismiss = () => {
    setDismissed(true);
    onDismiss?.();
  };

  const handleConfigureClick = () => {
    navigate('/settings/providers');
  };

  if (!show || dismissed) {
    return null;
  }

  if (loading) {
    return (
      <MessageBar intent="info" className="offline-mode-banner">
        <MessageBarBody>
          <Spinner size="tiny" />
          <span style={{ marginLeft: '8px' }}>Checking provider status...</span>
        </MessageBarBody>
      </MessageBar>
    );
  }

  if (error) {
    return (
      <MessageBar intent="error" className="offline-mode-banner">
        <MessageBarBody>
          <MessageBarTitle>Provider Status Check Failed</MessageBarTitle>
          {error}
        </MessageBarBody>
        {onDismiss && (
          <Button
            appearance="transparent"
            icon={<DismissRegular />}
            onClick={handleDismiss}
            aria-label="Dismiss"
          />
        )}
      </MessageBar>
    );
  }

  if (!status) {
    return null;
  }

  const getMessageIntent = (): 'success' | 'warning' | 'info' | 'error' => {
    if (status.isOfflineMode && status.offlineProvidersCount === 0) {
      return 'error';
    }
    if (status.isOfflineMode) {
      return 'warning';
    }
    if (status.degradedFeatures.length > 0) {
      return 'info';
    }
    return 'success';
  };

  const getIcon = () => {
    const intent = getMessageIntent();
    if (intent === 'success') return <CheckmarkCircleRegular />;
    if (intent === 'warning' || intent === 'error') return <WarningRegular />;
    return <InfoRegular />;
  };

  const getMessage = (): { title: string; body: string } => {
    if (status.isOfflineMode && status.offlineProvidersCount === 0) {
      return {
        title: 'No Providers Available',
        body: 'No online or offline providers are configured. Some features will be limited.',
      };
    }

    if (status.isOfflineMode) {
      return {
        title: 'Running in Offline Mode',
        body: status.message,
      };
    }

    if (status.degradedFeatures.length > 0) {
      return {
        title: 'Some Features Degraded',
        body: 'Some features are using fallback providers. Configure additional providers for full functionality.',
      };
    }

    return {
      title: 'All Systems Operational',
      body: `${status.onlineProvidersCount} online providers and ${status.offlineProvidersCount} offline providers available.`,
    };
  };

  const message = getMessage();

  return (
    <MessageBar intent={getMessageIntent()} className="offline-mode-banner" icon={getIcon()}>
      <MessageBarBody>
        <MessageBarTitle>{message.title}</MessageBarTitle>
        <div>{message.body}</div>

        {status.availableFeatures.length > 0 && (
          <div className="offline-provider-summary" style={{ marginTop: '8px' }}>
            <strong>Available Features:</strong> {status.availableFeatures.join(', ')}
          </div>
        )}

        {status.degradedFeatures.length > 0 && (
          <div className="offline-provider-summary" style={{ marginTop: '4px', color: '#d97706' }}>
            <strong>Degraded:</strong> {status.degradedFeatures.join(', ')}
          </div>
        )}

        <div className="provider-counts" style={{ marginTop: '8px', fontSize: '0.875rem' }}>
          <strong>Providers:</strong>{' '}
          {status.onlineProvidersCount > 0 && `${status.onlineProvidersCount} online`}
          {status.onlineProvidersCount > 0 && status.offlineProvidersCount > 0 && ', '}
          {status.offlineProvidersCount > 0 && `${status.offlineProvidersCount} offline`}
        </div>

        {showConfigLink && (
          <div style={{ marginTop: '8px' }}>
            <Link onClick={handleConfigureClick}>
              <SettingsRegular style={{ marginRight: '4px', verticalAlign: 'middle' }} />
              Configure Providers
            </Link>
          </div>
        )}
      </MessageBarBody>

      {onDismiss && (
        <Button
          appearance="transparent"
          icon={<DismissRegular />}
          onClick={handleDismiss}
          aria-label="Dismiss"
        />
      )}
    </MessageBar>
  );
};
