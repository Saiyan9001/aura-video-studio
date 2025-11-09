import { useEffect, useCallback } from 'react';
import { useLocation } from 'react-router-dom';
import { navigationAnalytics } from '../services/navigationAnalytics';

/**
 * Hook to automatically track navigation using the analytics service
 */
export function useNavigationTracking() {
  const location = useLocation();

  useEffect(() => {
    // Track page view whenever location changes
    navigationAnalytics.trackNavigation(location.pathname, 'click');
  }, [location]);

  // Return tracking functions for manual tracking
  const trackFeature = useCallback((feature: string, context?: string) => {
    navigationAnalytics.trackFeatureUsage(feature, context);
  }, []);

  const startTask = useCallback((taskId: string, taskName: string) => {
    navigationAnalytics.startTask(taskId, taskName);
  }, []);

  const completeTask = useCallback(
    (taskId: string, taskName: string, success: boolean = true, steps: number = 1) => {
      navigationAnalytics.completeTask(taskId, taskName, success, steps);
    },
    []
  );

  return {
    trackFeature,
    startTask,
    completeTask,
  };
}
