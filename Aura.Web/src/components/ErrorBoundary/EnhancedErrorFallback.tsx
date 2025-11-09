/**
 * Enhanced Error Fallback Component
 *
 * Provides detailed error information with error codes and recovery suggestions
 */

import { Button, Card, MessageBar, MessageBarBody, Text } from '@fluentui/react-components';
import {
  DismissCircle24Filled,
  ArrowClockwise24Regular,
  Home24Regular,
  Document24Regular,
  Copy24Regular,
} from '@fluentui/react-icons';
import { useState } from 'react';
import { useNavigate } from 'react-router-dom';

interface EnhancedErrorFallbackProps {
  error: Error;
  errorInfo?: React.ErrorInfo;
  errorCode?: string;
  reset?: () => void;
  showDetails?: boolean;
}

export function EnhancedErrorFallback({
  error,
  errorInfo,
  errorCode,
  reset,
  showDetails = true,
}: EnhancedErrorFallbackProps) {
  const navigate = useNavigate();
  const [copied, setCopied] = useState(false);

  const generatedErrorCode = errorCode || generateErrorCode(error);

  const handleCopyError = () => {
    const errorDetails = {
      errorCode: generatedErrorCode,
      message: error.message,
      stack: error.stack,
      componentStack: errorInfo?.componentStack,
      timestamp: new Date().toISOString(),
      userAgent: navigator.userAgent,
      url: window.location.href,
    };

    navigator.clipboard.writeText(JSON.stringify(errorDetails, null, 2));
    setCopied(true);
    setTimeout(() => setCopied(false), 2000);
  };

  const handleGoHome = () => {
    navigate('/');
  };

  const handleViewLogs = () => {
    navigate('/logs');
  };

  const handleReload = () => {
    window.location.reload();
  };

  const getRecoverySuggestions = (): string[] => {
    const suggestions: string[] = [];

    if (error.message.includes('chunk')) {
      suggestions.push('The application was updated. Try reloading the page.');
    }

    if (error.message.includes('network') || error.message.includes('fetch')) {
      suggestions.push('Check your internet connection and try again.');
    }

    if (error.message.includes('undefined') || error.message.includes('null')) {
      suggestions.push('Some required data may be missing. Try going back to the home page.');
    }

    if (error.message.includes('memory')) {
      suggestions.push('Your device may be low on memory. Close other applications and try again.');
    }

    if (suggestions.length === 0) {
      suggestions.push('Try reloading the page or returning to the home page.');
      suggestions.push('If the problem persists, check the application logs.');
      suggestions.push('Report this issue with the error code to support.');
    }

    return suggestions;
  };

  const suggestions = getRecoverySuggestions();

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
      <Card
        style={{
          width: '700px',
          maxWidth: '90vw',
          padding: '32px',
          background: 'var(--colorNeutralBackground2)',
        }}
      >
        <div style={{ textAlign: 'center', marginBottom: '24px' }}>
          <DismissCircle24Filled style={{ color: '#D13438', fontSize: '48px' }} />
          <Text
            as="h1"
            size={800}
            weight="semibold"
            style={{ marginTop: '16px', marginBottom: '8px', display: 'block' }}
          >
            Application Error
          </Text>
          <Text as="p" size={500} style={{ color: 'var(--colorNeutralForeground2)' }}>
            An unexpected error occurred while running the application
          </Text>
        </div>

        <MessageBar intent="error" style={{ marginBottom: '24px' }}>
          <MessageBarBody>
            <Text weight="semibold" style={{ display: 'block', marginBottom: '4px' }}>
              {error.name}: {error.message}
            </Text>
            {generatedErrorCode && (
              <Text size={300} style={{ fontFamily: 'monospace' }}>
                Error Code: {generatedErrorCode}
              </Text>
            )}
          </MessageBarBody>
        </MessageBar>

        <Card
          style={{
            marginBottom: '24px',
            padding: '16px',
            background: 'var(--colorNeutralBackground3)',
          }}
        >
          <Text weight="semibold" size={500} style={{ display: 'block', marginBottom: '12px' }}>
            Recovery Suggestions
          </Text>
          <ol style={{ margin: '0', paddingLeft: '20px' }}>
            {suggestions.map((suggestion, index) => (
              <li key={index} style={{ marginBottom: '8px' }}>
                <Text>{suggestion}</Text>
              </li>
            ))}
          </ol>
        </Card>

        {showDetails && (
          <details style={{ marginBottom: '24px' }}>
            <summary
              style={{
                cursor: 'pointer',
                padding: '8px',
                background: 'var(--colorNeutralBackground3)',
                borderRadius: '4px',
                marginBottom: '8px',
              }}
            >
              <Text weight="semibold">Technical Details (for developers)</Text>
            </summary>
            <pre
              style={{
                padding: '12px',
                background: 'var(--colorNeutralBackground4)',
                borderRadius: '4px',
                overflow: 'auto',
                fontSize: '12px',
                fontFamily: 'monospace',
                maxHeight: '300px',
              }}
            >
              {error.stack}
              {errorInfo?.componentStack && `\n\nComponent Stack:\n${errorInfo.componentStack}`}
            </pre>
          </details>
        )}

        <div
          style={{
            display: 'flex',
            flexDirection: 'column',
            gap: '12px',
          }}
        >
          <div style={{ display: 'flex', gap: '8px', flexWrap: 'wrap' }}>
            {reset && (
              <Button appearance="primary" icon={<ArrowClockwise24Regular />} onClick={reset}>
                Try Again
              </Button>
            )}
            <Button
              appearance="secondary"
              icon={<ArrowClockwise24Regular />}
              onClick={handleReload}
            >
              Reload Page
            </Button>
            <Button appearance="secondary" icon={<Home24Regular />} onClick={handleGoHome}>
              Go Home
            </Button>
          </div>

          <div style={{ display: 'flex', gap: '8px', flexWrap: 'wrap' }}>
            <Button appearance="subtle" icon={<Copy24Regular />} onClick={handleCopyError}>
              {copied ? 'Copied!' : 'Copy Error Details'}
            </Button>
            <Button appearance="subtle" icon={<Document24Regular />} onClick={handleViewLogs}>
              View Logs
            </Button>
          </div>
        </div>

        <MessageBar intent="info" style={{ marginTop: '24px' }}>
          <MessageBarBody>
            <Text size={300}>
              If you continue to experience this error, please report it on{' '}
              <a
                href="https://github.com/Coffee285/aura-video-studio/issues"
                target="_blank"
                rel="noopener noreferrer"
                style={{ color: 'var(--colorBrandForeground1)' }}
              >
                GitHub
              </a>{' '}
              with the error code above.
            </Text>
          </MessageBarBody>
        </MessageBar>
      </Card>
    </div>
  );
}

function generateErrorCode(error: Error): string {
  const hash = error.message.split('').reduce((acc, char) => acc + char.charCodeAt(0), 0);
  const timestamp = Date.now().toString(36).toUpperCase();
  const code = (hash % 10000).toString().padStart(4, '0');
  return `E${code}-${timestamp}`;
}
