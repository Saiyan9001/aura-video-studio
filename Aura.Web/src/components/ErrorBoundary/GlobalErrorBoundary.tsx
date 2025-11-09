/**
 * Global Error Boundary
 * Catches unhandled errors in the entire application
 */

import { Component, ReactNode } from 'react';
import { loggingService } from '../../services/loggingService';
import { EnhancedErrorFallback } from './EnhancedErrorFallback';

interface Props {
  children: ReactNode;
  fallback?: (error: Error, errorInfo: React.ErrorInfo, reset: () => void) => ReactNode;
}

interface State {
  hasError: boolean;
  error: Error | null;
  errorInfo: React.ErrorInfo | null;
}

export class GlobalErrorBoundary extends Component<Props, State> {
  constructor(props: Props) {
    super(props);
    this.state = {
      hasError: false,
      error: null,
      errorInfo: null,
    };
  }

  static getDerivedStateFromError(error: Error): Partial<State> {
    return {
      hasError: true,
      error,
    };
  }

  componentDidCatch(error: Error, errorInfo: React.ErrorInfo): void {
    loggingService.error('Uncaught error in application', error, 'ErrorBoundary', 'global', {
      componentStack: errorInfo.componentStack,
    });

    this.setState({
      errorInfo,
    });
  }

  handleReset = (): void => {
    this.setState({
      hasError: false,
      error: null,
      errorInfo: null,
    });
  };

  render(): ReactNode {
    const { hasError, error, errorInfo } = this.state;
    const { children, fallback } = this.props;

    if (hasError && error) {
      if (fallback && errorInfo) {
        return fallback(error, errorInfo, this.handleReset);
      }

      return (
        <EnhancedErrorFallback
          error={error}
          errorInfo={errorInfo ?? undefined}
          reset={this.handleReset}
          showDetails={true}
        />
      );
    }

    return children;
  }
}
