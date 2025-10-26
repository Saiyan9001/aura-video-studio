import { createContext, useContext, useState, useCallback, ReactNode, useEffect, useRef } from 'react';

export type ActivityStatus = 'pending' | 'running' | 'paused' | 'completed' | 'failed' | 'cancelled';

export type ActivityCategory = 'import' | 'export' | 'analysis' | 'effects' | 'other';

export type ActivityType = 
  | 'video-generation'
  | 'api-call'
  | 'file-upload'
  | 'analysis'
  | 'render'
  | 'download'
  | 'other';

export interface ActivityProgress {
  percentage: number; // 0-100
  currentItems?: number;
  totalItems?: number;
  speed?: number; // MB/s or frames/s
  speedUnit?: 'MB/s' | 'frames/s' | 'items/s';
  timeElapsed?: number; // seconds
  timeRemaining?: number; // seconds
}

export interface Activity {
  id: string;
  type: ActivityType;
  category?: ActivityCategory;
  status: ActivityStatus;
  title: string;
  message: string;
  progress: number; // 0-100 (deprecated, use detailedProgress)
  detailedProgress?: ActivityProgress;
  startTime: Date;
  endTime?: Date;
  error?: string;
  canCancel?: boolean;
  canRetry?: boolean;
  canPause?: boolean;
  priority?: number; // 1-10, higher is more important
  parentId?: string; // For batch operations
  metadata?: Record<string, unknown>;
}

export interface OperationHistoryEntry {
  id: string;
  activityId: string;
  title: string;
  category?: ActivityCategory;
  status: 'completed' | 'failed';
  startTime: Date;
  endTime: Date;
  duration: number; // seconds
  error?: string;
  canRetry?: boolean;
}

export interface ResourceUsage {
  cpu: number; // 0-100
  memory: number; // 0-100
  gpu?: number; // 0-100
  diskIO?: number; // MB/s
  timestamp: Date;
}

interface ActivityContextType {
  activities: Activity[];
  activeActivities: Activity[];
  completedActivities: Activity[];
  failedActivities: Activity[];
  queuedActivities: Activity[];
  operationHistory: OperationHistoryEntry[];
  resourceUsage: ResourceUsage | null;
  addActivity: (activity: Omit<Activity, 'id' | 'startTime' | 'status' | 'progress'>) => string;
  updateActivity: (id: string, updates: Partial<Activity>) => void;
  removeActivity: (id: string) => void;
  clearCompleted: () => void;
  clearAll: () => void;
  getActivity: (id: string) => Activity | undefined;
  pauseActivity: (id: string) => void;
  resumeActivity: (id: string) => void;
  setPriority: (id: string, priority: number) => void;
  getOperationHistory: (limit?: number) => OperationHistoryEntry[];
  clearOperationHistory: () => void;
}

const ActivityContext = createContext<ActivityContextType | undefined>(undefined);

const MAX_HISTORY_ENTRIES = 50;

export function ActivityProvider({ children }: { children: ReactNode }) {
  const [activities, setActivities] = useState<Activity[]>([]);
  const [operationHistory, setOperationHistory] = useState<OperationHistoryEntry[]>([]);
  const [resourceUsage, setResourceUsage] = useState<ResourceUsage | null>(null);
  const resourceMonitorRef = useRef<number | null>(null);

  // Monitor resource usage
  useEffect(() => {
    const updateResourceUsage = () => {
      // In a real implementation, this would query system resources
      // For now, we'll generate mock data for demonstration
      const mockUsage: ResourceUsage = {
        cpu: Math.random() * 100,
        memory: Math.random() * 100,
        gpu: Math.random() * 100,
        diskIO: Math.random() * 200,
        timestamp: new Date(),
      };
      setResourceUsage(mockUsage);
    };

    // Update every 2 seconds
    resourceMonitorRef.current = setInterval(updateResourceUsage, 2000);
    updateResourceUsage(); // Initial update

    return () => {
      if (resourceMonitorRef.current) {
        clearInterval(resourceMonitorRef.current);
      }
    };
  }, []);

  const addToHistory = useCallback((activity: Activity) => {
    if (activity.status === 'completed' || activity.status === 'failed') {
      const historyEntry: OperationHistoryEntry = {
        id: `history-${Date.now()}-${Math.random().toString(36).substr(2, 9)}`,
        activityId: activity.id,
        title: activity.title,
        category: activity.category,
        status: activity.status,
        startTime: activity.startTime,
        endTime: activity.endTime || new Date(),
        duration: activity.endTime 
          ? (activity.endTime.getTime() - activity.startTime.getTime()) / 1000 
          : 0,
        error: activity.error,
        canRetry: activity.canRetry,
      };

      setOperationHistory(prev => {
        const updated = [historyEntry, ...prev];
        return updated.slice(0, MAX_HISTORY_ENTRIES);
      });
    }
  }, []);

  const addActivity = useCallback((activity: Omit<Activity, 'id' | 'startTime' | 'status' | 'progress'>) => {
    const id = `activity-${Date.now()}-${Math.random().toString(36).substr(2, 9)}`;
    const newActivity: Activity = {
      ...activity,
      id,
      status: 'pending',
      progress: 0,
      startTime: new Date(),
      category: activity.category || 'other',
      priority: activity.priority || 5,
    };
    
    setActivities(prev => [...prev, newActivity]);
    return id;
  }, []);

  const updateActivity = useCallback((id: string, updates: Partial<Activity>) => {
    setActivities(prev => prev.map(activity => {
      if (activity.id === id) {
        const updated = { ...activity, ...updates };
        
        // Set endTime when status changes to completed, failed, or cancelled
        if (
          updates.status && 
          ['completed', 'failed', 'cancelled'].includes(updates.status) &&
          !updated.endTime
        ) {
          updated.endTime = new Date();
          // Add to history when completed or failed
          addToHistory(updated);
        }
        
        return updated;
      }
      return activity;
    }));
  }, [addToHistory]);

  const removeActivity = useCallback((id: string) => {
    setActivities(prev => prev.filter(activity => activity.id !== id));
  }, []);

  const clearCompleted = useCallback(() => {
    setActivities(prev => prev.filter(activity => activity.status !== 'completed'));
  }, []);

  const clearAll = useCallback(() => {
    setActivities([]);
  }, []);

  const getActivity = useCallback((id: string) => {
    return activities.find(activity => activity.id === id);
  }, [activities]);

  const pauseActivity = useCallback((id: string) => {
    updateActivity(id, { status: 'paused' });
  }, [updateActivity]);

  const resumeActivity = useCallback((id: string) => {
    updateActivity(id, { status: 'running' });
  }, [updateActivity]);

  const setPriority = useCallback((id: string, priority: number) => {
    updateActivity(id, { priority: Math.max(1, Math.min(10, priority)) });
  }, [updateActivity]);

  const getOperationHistory = useCallback((limit = MAX_HISTORY_ENTRIES) => {
    return operationHistory.slice(0, limit);
  }, [operationHistory]);

  const clearOperationHistory = useCallback(() => {
    setOperationHistory([]);
  }, []);

  const activeActivities = activities.filter(
    a => a.status === 'pending' || a.status === 'running' || a.status === 'paused'
  );

  const queuedActivities = activities.filter(
    a => a.status === 'pending'
  ).sort((a, b) => (b.priority || 5) - (a.priority || 5));

  const completedActivities = activities.filter(
    a => a.status === 'completed'
  );

  const failedActivities = activities.filter(
    a => a.status === 'failed'
  );

  const value: ActivityContextType = {
    activities,
    activeActivities,
    completedActivities,
    failedActivities,
    queuedActivities,
    operationHistory,
    resourceUsage,
    addActivity,
    updateActivity,
    removeActivity,
    clearCompleted,
    clearAll,
    getActivity,
    pauseActivity,
    resumeActivity,
    setPriority,
    getOperationHistory,
    clearOperationHistory,
  };

  return (
    <ActivityContext.Provider value={value}>
      {children}
    </ActivityContext.Provider>
  );
}

export function useActivity() {
  const context = useContext(ActivityContext);
  if (!context) {
    throw new Error('useActivity must be used within an ActivityProvider');
  }
  return context;
}
