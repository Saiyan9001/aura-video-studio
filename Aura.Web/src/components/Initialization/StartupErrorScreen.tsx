/**
 * StartupErrorScreen Component
 *
 * Displays detailed startup error information with recovery options
 * Provides clear error messages and troubleshooting steps
 */

import {
  Button,
  Card,
  CardHeader,
  MessageBar,
  MessageBarBody,
  Text,
} from '@fluentui/react-components';
import {
  DismissCircle24Filled,
  ArrowClockwise24Regular,
  Settings24Regular,
  Document24Regular,
  Info24Regular,
} from '@fluentui/react-icons';
import { useNavigate } from 'react-router-dom';
import type { InitializationError } from './InitializationScreen';
import { env } from '../../config/env';

interface StartupErrorScreenProps {
  error: InitializationError;
  onRetry: () => void;
  enableSafeMode?: boolean;
  enableOfflineMode?: boolean;
}

export function StartupErrorScreen({
  error,
  onRetry,
  enableSafeMode = true,
  enableOfflineMode = true,
}: StartupErrorScreenProps) {
  const navigate = useNavigate();

  const handleSafeMode = () => {
    localStorage.setItem('safeMode', 'true');
    window.location.reload();
  };

  const handleOfflineMode = () => {
    localStorage.setItem('offlineMode', 'true');
    window.location.reload();
  };

  const handleSettings = () => {
    navigate('/settings');
  };

  const handleViewLogs = () => {
    navigate('/logs');
  };

  const copyDiagnostics = () => {
    const diagnostics = {
      timestamp: new Date().toISOString(),
      errorCode: error.code,
      message: error.message,
      details: error.details,
      userAgent: navigator.userAgent,
      url: window.location.href,
      apiUrl: env.apiBaseUrl,
      platform: navigator.platform,
    };

    navigator.clipboard.writeText(JSON.stringify(diagnostics, null, 2));
  };

  const getErrorIcon = () => {
    return <DismissCircle24Filled style={{ color: '#D13438', fontSize: '48px' }} />;
  };

  const getErrorTitle = () => {
    switch (error.code) {
      case 'API_TIMEOUT':
        return 'API Connection Timeout';
      case 'API_UNREACHABLE':
        return 'Cannot Reach API Server';
      case 'DEPENDENCY_MISSING':
        return 'Missing Dependencies';
      case 'CONFIG_INVALID':
        return 'Invalid Configuration';
      default:
        return 'Startup Failed';
    }
  };

  return (
    <div
      style={{
        height: '100vh',
        display: 'flex',
        alignItems: 'center',
        justifyContent: 'center',
        background: 'var(--colorNeutralBackground1)',
        padding: '20px',
      }}
    >
      <div
        style={{
          width: '700px',
          maxWidth: '90vw',
        }}
      >
        <Card
          style={{
            padding: '32px',
            background: 'var(--colorNeutralBackground2)',
          }}
        >
          <div style={{ textAlign: 'center', marginBottom: '24px' }}>
            {getErrorIcon()}
            <Text
              as="h1"
              size={800}
              weight="semibold"
              style={{ marginTop: '16px', marginBottom: '8px', display: 'block' }}
            >
              {getErrorTitle()}
            </Text>
            <Text as="p" size={500} style={{ color: 'var(--colorNeutralForeground2)' }}>
              {error.message}
            </Text>
          </div>

          {error.details && (
            <MessageBar intent="error" style={{ marginBottom: '24px' }} icon={<Info24Regular />}>
              <MessageBarBody>
                <Text weight="semibold" style={{ display: 'block', marginBottom: '4px' }}>
                  Error Details:
                </Text>
                <Text style={{ fontFamily: 'monospace', fontSize: '12px' }}>{error.details}</Text>
              </MessageBarBody>
            </MessageBar>
          )}

          <Card
            style={{
              marginBottom: '24px',
              padding: '16px',
              background: 'var(--colorNeutralBackground3)',
            }}
          >
            <CardHeader
              header={
                <Text weight="semibold" size={500}>
                  Troubleshooting Steps
                </Text>
              }
            />
            <ol style={{ margin: '8px 0 0 0', paddingLeft: '20px' }}>
              {error.suggestions.map((suggestion, index) => (
                <li key={index} style={{ marginBottom: '8px' }}>
                  <Text>{suggestion}</Text>
                </li>
              ))}
            </ol>
          </Card>

          <div
            style={{
              display: 'flex',
              flexDirection: 'column',
              gap: '12px',
              marginBottom: '24px',
            }}
          >
            <div style={{ display: 'flex', gap: '8px', flexWrap: 'wrap' }}>
              {error.recoverable && (
                <Button appearance="primary" icon={<ArrowClockwise24Regular />} onClick={onRetry}>
                  Retry Connection
                </Button>
              )}
              {enableSafeMode && (
                <Button
                  appearance="secondary"
                  icon={<Settings24Regular />}
                  onClick={handleSafeMode}
                >
                  Start in Safe Mode
                </Button>
              )}
              {enableOfflineMode && (
                <Button appearance="secondary" onClick={handleOfflineMode}>
                  Offline Mode
                </Button>
              )}
            </div>

            <div style={{ display: 'flex', gap: '8px', flexWrap: 'wrap' }}>
              <Button appearance="subtle" icon={<Settings24Regular />} onClick={handleSettings}>
                Open Settings
              </Button>
              <Button appearance="subtle" icon={<Document24Regular />} onClick={handleViewLogs}>
                View Logs
              </Button>
              <Button appearance="subtle" onClick={copyDiagnostics}>
                Copy Diagnostics
              </Button>
            </div>
          </div>

          <MessageBar intent="info">
            <MessageBarBody>
              <Text size={300}>
                <strong>Error Code:</strong> {error.code}
                <br />
                If the problem persists, please contact support with this error code and the
                diagnostic information.
              </Text>
            </MessageBarBody>
          </MessageBar>
        </Card>

        <div style={{ textAlign: 'center', marginTop: '16px' }}>
          <Text size={300} style={{ color: 'var(--colorNeutralForeground3)' }}>
            Need help? Check the{' '}
            <a
              href="https://github.com/Coffee285/aura-video-studio/wiki"
              target="_blank"
              rel="noopener noreferrer"
              style={{ color: 'var(--colorBrandForeground1)' }}
            >
              documentation
            </a>{' '}
            or{' '}
            <a
              href="https://github.com/Coffee285/aura-video-studio/issues"
              target="_blank"
              rel="noopener noreferrer"
              style={{ color: 'var(--colorBrandForeground1)' }}
            >
              report an issue
            </a>
          </Text>
        </div>
      </div>
    </div>
  );
}
