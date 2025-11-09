/**
 * Component Error Boundary
 * Catches errors in specific component sections
 */

import { Button, Text } from '@fluentui/react-components';
import { ErrorCircle24Regular, ArrowClockwise24Regular } from '@fluentui/react-icons';
import { Component, ReactNode } from 'react';
import { loggingService } from '../../services/loggingService';

interface Props {
  children: ReactNode;
  fallback?: (error: Error, reset: () => void) => ReactNode;
  onError?: (error: Error, errorInfo: React.ErrorInfo) => void;
  componentName?: string;
}

interface State {
  hasError: boolean;
  error: Error | null;
}

/**
 * Component-level error boundary
 */
export class ComponentErrorBoundary extends Component<Props, State> {
  constructor(props: Props) {
    super(props);
    this.state = {
      hasError: false,
      error: null,
    };
  }

  static getDerivedStateFromError(error: Error): Partial<State> {
    return {
      hasError: true,
      error,
    };
  }

  componentDidCatch(error: Error, errorInfo: React.ErrorInfo): void {
    const { componentName, onError } = this.props;

    // Log error
    loggingService.error(
      `Error in ${componentName || 'component'}`,
      error,
      'ErrorBoundary',
      'component',
      {
        componentStack: errorInfo.componentStack,
      }
    );

    // Call custom error handler if provided
    if (onError) {
      onError(error, errorInfo);
    }
  }

  handleReset = (): void => {
    this.setState({
      hasError: false,
      error: null,
    });
  };

  render(): ReactNode {
    const { hasError, error } = this.state;
    const { children, fallback, componentName } = this.props;

    if (hasError && error) {
      // Use custom fallback if provided
      if (fallback) {
        return fallback(error, this.handleReset);
      }

      // Default error UI
      return (
        <div
          style={{
            padding: '2rem',
            textAlign: 'center',
            border: '1px solid #e0e0e0',
            borderRadius: '8px',
            backgroundColor: '#fef6f6',
          }}
        >
          <ErrorCircle24Regular
            style={{ color: '#d13438', marginBottom: '1rem', fontSize: '2rem' }}
          />
          <Text
            as="h3"
            size={500}
            weight="semibold"
            style={{ marginBottom: '0.5rem', display: 'block' }}
          >
            {componentName ? `Error in ${componentName}` : 'Component Error'}
          </Text>
          <Text as="p" size={300} style={{ marginBottom: '1rem', color: '#666', display: 'block' }}>
            {error.message || 'An error occurred while rendering this component'}
          </Text>
          <Button
            appearance="primary"
            icon={<ArrowClockwise24Regular />}
            onClick={this.handleReset}
          >
            Try Again
          </Button>
        </div>
      );
    }

    return children;
  }
}
