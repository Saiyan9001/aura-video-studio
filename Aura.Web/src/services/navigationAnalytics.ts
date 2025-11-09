/**
 * Navigation Analytics Service
 * Tracks navigation patterns, feature usage, and time-to-task completion
 */

interface NavigationEvent {
  timestamp: Date;
  fromPath: string;
  toPath: string;
  method: 'click' | 'keyboard' | 'command-palette' | 'swipe' | 'breadcrumb' | 'tab';
  duration?: number; // Time spent on previous page in milliseconds
}

interface FeatureUsageEvent {
  feature: string;
  timestamp: Date;
  context?: string;
}

interface TaskCompletionEvent {
  taskId: string;
  taskName: string;
  startTime: Date;
  endTime: Date;
  duration: number; // In milliseconds
  steps: number;
  success: boolean;
}

class NavigationAnalyticsService {
  private navigationHistory: NavigationEvent[] = [];
  private featureUsage: Map<string, number> = new Map();
  private taskTiming: Map<string, Date> = new Map();
  private currentPath: string = '/';
  private sessionStartTime: Date = new Date();

  constructor() {
    this.loadFromStorage();
  }

  /**
   * Track navigation between pages
   */
  trackNavigation(toPath: string, method: NavigationEvent['method'] = 'click') {
    const fromPath = this.currentPath;
    const now = new Date();

    // Calculate time spent on previous page
    const lastEvent = this.navigationHistory[this.navigationHistory.length - 1];
    const duration = lastEvent ? now.getTime() - lastEvent.timestamp.getTime() : 0;

    const event: NavigationEvent = {
      timestamp: now,
      fromPath,
      toPath,
      method,
      duration,
    };

    this.navigationHistory.push(event);
    this.currentPath = toPath;

    // Persist to localStorage (keep last 100 events)
    if (this.navigationHistory.length > 100) {
      this.navigationHistory = this.navigationHistory.slice(-100);
    }
    this.saveToStorage();
  }

  /**
   * Track feature usage
   */
  trackFeatureUsage(feature: string, context?: string) {
    const event: FeatureUsageEvent = {
      feature,
      timestamp: new Date(),
      context,
    };

    const currentCount = this.featureUsage.get(feature) || 0;
    this.featureUsage.set(feature, currentCount + 1);

    this.saveToStorage();
    console.debug('[Analytics] Feature used:', event);
  }

  /**
   * Start tracking a task
   */
  startTask(taskId: string, taskName: string) {
    this.taskTiming.set(taskId, new Date());
    console.debug('[Analytics] Task started:', { taskId, taskName });
  }

  /**
   * Complete tracking a task
   */
  completeTask(taskId: string, taskName: string, success: boolean = true, steps: number = 1) {
    const startTime = this.taskTiming.get(taskId);
    if (!startTime) {
      console.warn('[Analytics] Task completion tracked without start:', taskId);
      return;
    }

    const endTime = new Date();
    const duration = endTime.getTime() - startTime.getTime();

    const event: TaskCompletionEvent = {
      taskId,
      taskName,
      startTime,
      endTime,
      duration,
      steps,
      success,
    };

    this.taskTiming.delete(taskId);

    // Store completion event
    const completions = this.getTaskCompletions();
    completions.push(event);
    localStorage.setItem('aura-task-completions', JSON.stringify(completions.slice(-50)));

    console.debug('[Analytics] Task completed:', event);
  }

  /**
   * Get navigation patterns report
   */
  getNavigationPatterns() {
    const pathFrequency = new Map<string, number>();
    const methodUsage = new Map<string, number>();
    const pathTransitions = new Map<string, number>();

    this.navigationHistory.forEach((event) => {
      // Count path visits
      pathFrequency.set(event.toPath, (pathFrequency.get(event.toPath) || 0) + 1);

      // Count navigation methods
      methodUsage.set(event.method, (methodUsage.get(event.method) || 0) + 1);

      // Count path transitions
      const transition = `${event.fromPath} → ${event.toPath}`;
      pathTransitions.set(transition, (pathTransitions.get(transition) || 0) + 1);
    });

    return {
      pathFrequency: Array.from(pathFrequency.entries()).sort((a, b) => b[1] - a[1]),
      methodUsage: Array.from(methodUsage.entries()).sort((a, b) => b[1] - a[1]),
      pathTransitions: Array.from(pathTransitions.entries()).sort((a, b) => b[1] - a[1]),
      totalNavigations: this.navigationHistory.length,
    };
  }

  /**
   * Get feature usage report
   */
  getFeatureUsageReport() {
    return {
      features: Array.from(this.featureUsage.entries()).sort((a, b) => b[1] - a[1]),
      totalUsage: Array.from(this.featureUsage.values()).reduce((sum, count) => sum + count, 0),
    };
  }

  /**
   * Get task completion metrics
   */
  getTaskMetrics() {
    const completions = this.getTaskCompletions();

    const avgDurations = new Map<string, { total: number; count: number }>();
    let successCount = 0;
    let failureCount = 0;

    completions.forEach((event) => {
      const current = avgDurations.get(event.taskName) || { total: 0, count: 0 };
      avgDurations.set(event.taskName, {
        total: current.total + event.duration,
        count: current.count + 1,
      });

      if (event.success) {
        successCount++;
      } else {
        failureCount++;
      }
    });

    const averages = Array.from(avgDurations.entries()).map(([taskName, { total, count }]) => ({
      taskName,
      avgDuration: total / count,
      count,
    }));

    return {
      averages: averages.sort((a, b) => b.count - a.count),
      successRate: completions.length > 0 ? successCount / completions.length : 0,
      totalCompleted: completions.length,
      successCount,
      failureCount,
    };
  }

  /**
   * Get unused features (features that haven't been used in the session)
   */
  getUnusedFeatures(allFeatures: string[]): string[] {
    return allFeatures.filter((feature) => !this.featureUsage.has(feature));
  }

  /**
   * Clear all analytics data
   */
  clearAllData() {
    this.navigationHistory = [];
    this.featureUsage.clear();
    this.taskTiming.clear();
    localStorage.removeItem('aura-navigation-analytics');
    localStorage.removeItem('aura-task-completions');
  }

  /**
   * Generate analytics summary
   */
  getSummary() {
    const sessionDuration = new Date().getTime() - this.sessionStartTime.getTime();

    return {
      sessionDuration,
      sessionStart: this.sessionStartTime,
      navigation: this.getNavigationPatterns(),
      features: this.getFeatureUsageReport(),
      tasks: this.getTaskMetrics(),
    };
  }

  private loadFromStorage() {
    try {
      const saved = localStorage.getItem('aura-navigation-analytics');
      if (saved) {
        const data = JSON.parse(saved);
        this.navigationHistory = data.navigationHistory || [];
        this.featureUsage = new Map(data.featureUsage || []);
        this.currentPath = data.currentPath || '/';
      }
    } catch {
      // Ignore errors
    }
  }

  private saveToStorage() {
    try {
      const data = {
        navigationHistory: this.navigationHistory,
        featureUsage: Array.from(this.featureUsage.entries()),
        currentPath: this.currentPath,
      };
      localStorage.setItem('aura-navigation-analytics', JSON.stringify(data));
    } catch {
      // Ignore errors
    }
  }

  private getTaskCompletions(): TaskCompletionEvent[] {
    try {
      const saved = localStorage.getItem('aura-task-completions');
      return saved ? JSON.parse(saved) : [];
    } catch {
      return [];
    }
  }
}

// Export singleton instance
export const navigationAnalytics = new NavigationAnalyticsService();
