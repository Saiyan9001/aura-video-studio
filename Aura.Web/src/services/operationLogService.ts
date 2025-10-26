import { Activity, ActivityCategory, ActivityStatus } from '../state/activityContext';

export interface OperationLogEntry {
  id: string;
  timestamp: Date;
  level: 'info' | 'warning' | 'error';
  category: ActivityCategory;
  operation: string;
  message: string;
  activityId?: string;
  parameters?: Record<string, unknown>;
  error?: {
    message: string;
    code?: string;
    stack?: string;
  };
  duration?: number; // milliseconds
  result?: 'success' | 'failure' | 'cancelled';
}

export interface OperationLogFilter {
  level?: 'info' | 'warning' | 'error';
  category?: ActivityCategory;
  startDate?: Date;
  endDate?: Date;
  searchQuery?: string;
}

class OperationLogService {
  private logs: OperationLogEntry[] = [];
  private maxEntries = 1000; // Keep last 1000 entries
  private listeners: Set<(logs: OperationLogEntry[]) => void> = new Set();

  /**
   * Add a log entry
   */
  log(entry: Omit<OperationLogEntry, 'id' | 'timestamp'>): void {
    const logEntry: OperationLogEntry = {
      ...entry,
      id: `log-${Date.now()}-${Math.random().toString(36).substr(2, 9)}`,
      timestamp: new Date(),
    };

    this.logs.unshift(logEntry); // Add to beginning
    
    // Keep only max entries
    if (this.logs.length > this.maxEntries) {
      this.logs = this.logs.slice(0, this.maxEntries);
    }

    this.notifyListeners();
  }

  /**
   * Log an info message
   */
  info(operation: string, message: string, params?: {
    category?: ActivityCategory;
    activityId?: string;
    parameters?: Record<string, unknown>;
  }): void {
    this.log({
      level: 'info',
      category: params?.category || 'other',
      operation,
      message,
      activityId: params?.activityId,
      parameters: params?.parameters,
    });
  }

  /**
   * Log a warning message
   */
  warning(operation: string, message: string, params?: {
    category?: ActivityCategory;
    activityId?: string;
    parameters?: Record<string, unknown>;
  }): void {
    this.log({
      level: 'warning',
      category: params?.category || 'other',
      operation,
      message,
      activityId: params?.activityId,
      parameters: params?.parameters,
    });
  }

  /**
   * Log an error message
   */
  error(operation: string, message: string, params?: {
    category?: ActivityCategory;
    activityId?: string;
    parameters?: Record<string, unknown>;
    error?: Error;
  }): void {
    this.log({
      level: 'error',
      category: params?.category || 'other',
      operation,
      message,
      activityId: params?.activityId,
      parameters: params?.parameters,
      error: params?.error ? {
        message: params.error.message,
        code: (params.error as any).code,
        stack: params.error.stack,
      } : undefined,
    });
  }

  /**
   * Log an activity lifecycle event
   */
  logActivity(activity: Activity, event: 'started' | 'completed' | 'failed' | 'cancelled'): void {
    const duration = activity.endTime 
      ? activity.endTime.getTime() - activity.startTime.getTime()
      : undefined;

    const level: 'info' | 'warning' | 'error' = 
      event === 'failed' ? 'error' :
      event === 'cancelled' ? 'warning' :
      'info';

    const result: 'success' | 'failure' | 'cancelled' = 
      event === 'completed' ? 'success' :
      event === 'failed' ? 'failure' :
      'cancelled';

    this.log({
      level,
      category: activity.category || 'other',
      operation: activity.title,
      message: `Activity ${event}: ${activity.message}`,
      activityId: activity.id,
      parameters: activity.metadata,
      duration,
      result: event === 'started' ? undefined : result,
      error: activity.error ? { message: activity.error } : undefined,
    });
  }

  /**
   * Get all logs
   */
  getLogs(): OperationLogEntry[] {
    return [...this.logs];
  }

  /**
   * Get filtered logs
   */
  getFilteredLogs(filter: OperationLogFilter): OperationLogEntry[] {
    return this.logs.filter(log => {
      if (filter.level && log.level !== filter.level) {
        return false;
      }
      
      if (filter.category && log.category !== filter.category) {
        return false;
      }
      
      if (filter.startDate && log.timestamp < filter.startDate) {
        return false;
      }
      
      if (filter.endDate && log.timestamp > filter.endDate) {
        return false;
      }
      
      if (filter.searchQuery) {
        const query = filter.searchQuery.toLowerCase();
        const searchableText = [
          log.operation,
          log.message,
          log.activityId || '',
          JSON.stringify(log.parameters || {}),
        ].join(' ').toLowerCase();
        
        if (!searchableText.includes(query)) {
          return false;
        }
      }
      
      return true;
    });
  }

  /**
   * Clear all logs
   */
  clear(): void {
    this.logs = [];
    this.notifyListeners();
  }

  /**
   * Export logs as JSON
   */
  exportAsJSON(): string {
    return JSON.stringify(this.logs, null, 2);
  }

  /**
   * Export logs as CSV
   */
  exportAsCSV(): string {
    const headers = ['Timestamp', 'Level', 'Category', 'Operation', 'Message', 'Activity ID', 'Duration', 'Result'];
    const rows = this.logs.map(log => [
      log.timestamp.toISOString(),
      log.level,
      log.category,
      log.operation,
      log.message.replace(/"/g, '""'), // Escape quotes
      log.activityId || '',
      log.duration?.toString() || '',
      log.result || '',
    ]);

    return [
      headers.join(','),
      ...rows.map(row => row.map(cell => `"${cell}"`).join(',')),
    ].join('\n');
  }

  /**
   * Subscribe to log changes
   */
  subscribe(listener: (logs: OperationLogEntry[]) => void): () => void {
    this.listeners.add(listener);
    return () => {
      this.listeners.delete(listener);
    };
  }

  /**
   * Notify all listeners
   */
  private notifyListeners(): void {
    this.listeners.forEach(listener => listener([...this.logs]));
  }

  /**
   * Get statistics about logs
   */
  getStatistics(): {
    total: number;
    byLevel: Record<string, number>;
    byCategory: Record<string, number>;
    byResult: Record<string, number>;
    averageDuration: number;
    errorRate: number;
  } {
    const byLevel: Record<string, number> = {};
    const byCategory: Record<string, number> = {};
    const byResult: Record<string, number> = {};
    let totalDuration = 0;
    let durationCount = 0;

    this.logs.forEach(log => {
      byLevel[log.level] = (byLevel[log.level] || 0) + 1;
      byCategory[log.category] = (byCategory[log.category] || 0) + 1;
      
      if (log.result) {
        byResult[log.result] = (byResult[log.result] || 0) + 1;
      }
      
      if (log.duration) {
        totalDuration += log.duration;
        durationCount++;
      }
    });

    const errorCount = byLevel.error || 0;
    const total = this.logs.length;

    return {
      total,
      byLevel,
      byCategory,
      byResult,
      averageDuration: durationCount > 0 ? totalDuration / durationCount : 0,
      errorRate: total > 0 ? errorCount / total : 0,
    };
  }
}

// Singleton instance
export const operationLogService = new OperationLogService();
