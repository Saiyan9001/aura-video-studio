import { Activity } from '../state/activityContext';

export interface QueuedOperation {
  id: string;
  priority: number;
  activity: Activity;
  dependencies?: string[]; // IDs of operations that must complete first
  maxConcurrent?: number; // Max concurrent operations of this type
}

export interface OperationQueueConfig {
  maxConcurrentOperations: number;
  priorityThreshold: number; // Only run operations with priority >= this
}

class OperationQueueService {
  private queue: QueuedOperation[] = [];
  private running: Map<string, QueuedOperation> = new Map();
  private config: OperationQueueConfig = {
    maxConcurrentOperations: 3,
    priorityThreshold: 1,
  };
  private listeners: Set<() => void> = new Set();

  /**
   * Add an operation to the queue
   */
  enqueue(operation: QueuedOperation): void {
    this.queue.push(operation);
    this.sortQueue();
    this.notifyListeners();
    this.processQueue();
  }

  /**
   * Remove an operation from the queue
   */
  dequeue(operationId: string): void {
    this.queue = this.queue.filter(op => op.id !== operationId);
    this.running.delete(operationId);
    this.notifyListeners();
    this.processQueue();
  }

  /**
   * Update operation priority
   */
  updatePriority(operationId: string, priority: number): void {
    const operation = this.queue.find(op => op.id === operationId);
    if (operation) {
      operation.priority = priority;
      this.sortQueue();
      this.notifyListeners();
    }
  }

  /**
   * Get current queue state
   */
  getQueueState() {
    return {
      queued: [...this.queue],
      running: Array.from(this.running.values()),
      totalQueued: this.queue.length,
      totalRunning: this.running.size,
    };
  }

  /**
   * Configure the queue
   */
  configure(config: Partial<OperationQueueConfig>): void {
    this.config = { ...this.config, ...config };
    this.processQueue();
  }

  /**
   * Subscribe to queue changes
   */
  subscribe(listener: () => void): () => void {
    this.listeners.add(listener);
    return () => {
      this.listeners.delete(listener);
    };
  }

  /**
   * Process the queue and start operations
   */
  private processQueue(): void {
    // Remove completed operations from running
    const completedIds: string[] = [];
    this.running.forEach((op, id) => {
      if (op.activity.status === 'completed' || op.activity.status === 'failed' || op.activity.status === 'cancelled') {
        completedIds.push(id);
      }
    });
    completedIds.forEach(id => this.running.delete(id));

    // Check if we can start more operations
    while (
      this.running.size < this.config.maxConcurrentOperations &&
      this.queue.length > 0
    ) {
      const nextOp = this.findNextOperation();
      if (!nextOp) break;

      // Remove from queue and add to running
      this.queue = this.queue.filter(op => op.id !== nextOp.id);
      this.running.set(nextOp.id, nextOp);
      
      // In a real implementation, this would actually start the operation
      // For now, we just update the activity status
      if (nextOp.activity.status === 'pending') {
        nextOp.activity.status = 'running';
      }
    }

    this.notifyListeners();
  }

  /**
   * Find the next operation to run based on priority and dependencies
   */
  private findNextOperation(): QueuedOperation | null {
    for (const operation of this.queue) {
      // Check priority threshold
      if (operation.priority < this.config.priorityThreshold) {
        continue;
      }

      // Check dependencies
      if (operation.dependencies && operation.dependencies.length > 0) {
        const dependenciesMet = operation.dependencies.every(depId => {
          const runningOp = this.running.get(depId);
          if (!runningOp) return true; // Dependency not running, assume completed
          return runningOp.activity.status === 'completed';
        });
        
        if (!dependenciesMet) {
          continue;
        }
      }

      // Check max concurrent for this operation type
      if (operation.maxConcurrent) {
        const sameTypeCount = Array.from(this.running.values()).filter(
          op => op.activity.type === operation.activity.type
        ).length;
        
        if (sameTypeCount >= operation.maxConcurrent) {
          continue;
        }
      }

      return operation;
    }

    return null;
  }

  /**
   * Sort queue by priority (highest first)
   */
  private sortQueue(): void {
    this.queue.sort((a, b) => b.priority - a.priority);
  }

  /**
   * Notify all listeners of queue changes
   */
  private notifyListeners(): void {
    this.listeners.forEach(listener => listener());
  }

  /**
   * Clear the queue
   */
  clear(): void {
    this.queue = [];
    this.running.clear();
    this.notifyListeners();
  }

  /**
   * Pause all running operations
   */
  pauseAll(): void {
    this.running.forEach(op => {
      if (op.activity.canPause && op.activity.status === 'running') {
        op.activity.status = 'paused';
      }
    });
    this.notifyListeners();
  }

  /**
   * Resume all paused operations
   */
  resumeAll(): void {
    this.running.forEach(op => {
      if (op.activity.status === 'paused') {
        op.activity.status = 'running';
      }
    });
    this.notifyListeners();
  }
}

// Singleton instance
export const operationQueueService = new OperationQueueService();
