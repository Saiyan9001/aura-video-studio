/**
 * LazyLoad Component
 *
 * Wrapper component that lazy loads heavy components only when they become visible
 * or when explicitly triggered. Helps reduce initial bundle size and improves
 * initial page load performance.
 */

import { Spinner, makeStyles, tokens } from '@fluentui/react-components';
import { Suspense, lazy, ComponentType, ReactNode } from 'react';

const useStyles = makeStyles({
  loadingContainer: {
    display: 'flex',
    flexDirection: 'column',
    alignItems: 'center',
    justifyContent: 'center',
    padding: tokens.spacingVerticalXXL,
    minHeight: '200px',
  },
  loadingText: {
    marginTop: tokens.spacingVerticalM,
    color: tokens.colorNeutralForeground3,
  },
});

interface LazyLoadProps {
  /**
   * Factory function that returns a dynamic import
   */
  factory: () => Promise<{ default: ComponentType<Record<string, unknown>> }>;

  /**
   * Props to pass to the lazy-loaded component
   */
  componentProps?: Record<string, unknown>;

  /**
   * Custom loading fallback
   */
  fallback?: ReactNode;

  /**
   * Loading message to display
   */
  loadingMessage?: string;
}

/**
 * LazyLoad component for code-splitting heavy components
 */
export function LazyLoad({
  factory,
  componentProps = {},
  fallback,
  loadingMessage = 'Loading...',
}: LazyLoadProps) {
  const styles = useStyles();
  const LazyComponent = lazy(factory);

  const defaultFallback = (
    <div className={styles.loadingContainer}>
      <Spinner size="large" />
      {loadingMessage && <div className={styles.loadingText}>{loadingMessage}</div>}
    </div>
  );

  return (
    <Suspense fallback={fallback || defaultFallback}>
      <LazyComponent {...componentProps} />
    </Suspense>
  );
}

/**
 * Helper function to create a lazy-loaded component
 */
// eslint-disable-next-line react-refresh/only-export-components -- Utility function for creating lazy components, exported for reuse across the app
export function createLazyComponent<T = Record<string, unknown>>(
  factory: () => Promise<{ default: ComponentType<T> }>
) {
  return lazy(factory);
}

/**
 * Preload a lazy component to improve perceived performance
 */
// eslint-disable-next-line react-refresh/only-export-components -- Utility function for preloading components, exported for performance optimization
export function preloadLazyComponent(factory: () => Promise<{ default: ComponentType<unknown> }>) {
  // Trigger the import without rendering
  factory().catch((err) => {
    console.error('Failed to preload component:', err);
  });
}
