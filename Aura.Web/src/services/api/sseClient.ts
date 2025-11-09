/**
 * SSE Client for real-time job progress updates
 * Provides auto-reconnect and resilient connection handling
 */

import { apiUrl } from '../../config/api';
import { loggingService as logger } from '../loggingService';

export interface SseEvent {
  type: string;
  data: unknown;
  eventId?: string;
}

export type SseConnectionStatus =
  | 'connecting'
  | 'connected'
  | 'reconnecting'
  | 'disconnected'
  | 'error';

export interface SseConnectionState {
  status: SseConnectionStatus;
  reconnectAttempt: number;
  lastEventId: string | null;
}

export interface JobStatusEvent {
  status: string;
  stage: string;
  percent: number;
  correlationId: string;
}

export interface StepStatusEvent {
  step: string;
  status: string;
  phase: string;
  correlationId: string;
}

export interface StepProgressEvent {
  step: string;
  phase: string;
  progressPct: number;
  message: string;
  correlationId: string;
  substageDetail?: string;
  currentItem?: number;
  totalItems?: number;
  elapsedTime?: string;
  estimatedTimeRemaining?: string;
}

export interface JobCompletedEvent {
  status: string;
  jobId: string;
  artifacts: Array<{
    name: string;
    path: string;
    type: string;
    sizeBytes: number;
  }>;
  output: {
    videoPath: string;
    subtitlePath: string;
    sizeBytes: number;
  };
  correlationId: string;
}

export interface JobFailedEvent {
  status: string;
  jobId: string;
  stage: string;
  errors: Array<{
    code: string;
    message: string;
    remediation: string;
  }>;
  errorMessage?: string;
  logs: string[];
  correlationId: string;
}

export interface JobCancelledEvent {
  status: string;
  jobId: string;
  stage: string;
  message: string;
  correlationId: string;
}

export interface WarningEvent {
  message: string;
  step: string;
  correlationId: string;
}

export interface ErrorEvent {
  message: string;
  correlationId?: string;
}

type EventHandler = (event: SseEvent) => void;

/**
 * SSE Client with auto-reconnect capability and Last-Event-ID support
 */
export class SseClient {
  private eventSource: EventSource | null = null;
  private handlers: Map<string, EventHandler[]> = new Map();
  private reconnectAttempts = 0;
  private maxReconnectAttempts = 5;
  private reconnectDelay = 1000; // Start with 1 second
  private maxReconnectDelay = 30000; // Cap at 30 seconds
  private reconnectTimer: ReturnType<typeof setTimeout> | null = null;
  private url: string;
  private isManualClose = false;
  private lastEventId: string | null = null;
  private connectionStatus: SseConnectionStatus = 'disconnected';
  private statusChangeHandlers: Array<(state: SseConnectionState) => void> = [];

  constructor(jobId: string) {
    this.url = apiUrl(`/api/jobs/${jobId}/events`);
  }

  /**
   * Subscribe to connection status changes
   */
  onStatusChange(handler: (state: SseConnectionState) => void): void {
    this.statusChangeHandlers.push(handler);
  }

  /**
   * Get current connection state
   */
  getConnectionState(): SseConnectionState {
    return {
      status: this.connectionStatus,
      reconnectAttempt: this.reconnectAttempts,
      lastEventId: this.lastEventId,
    };
  }

  /**
   * Notify status change handlers
   */
  private notifyStatusChange(): void {
    const state = this.getConnectionState();
    this.statusChangeHandlers.forEach((handler) => {
      try {
        handler(state);
      } catch (error) {
        logger.error(
          'Error in status change handler',
          error instanceof Error ? error : new Error(String(error)),
          'SseClient',
          'notifyStatusChange'
        );
      }
    });
  }

  /**
   * Connect to the SSE endpoint with Last-Event-ID support for resumption
   */
  connect(): void {
    if (this.eventSource) {
      logger.warn('Already connected', 'SseClient', 'connect');
      return;
    }

    this.isManualClose = false;
    this.connectionStatus = this.reconnectAttempts > 0 ? 'reconnecting' : 'connecting';
    this.notifyStatusChange();

    // Build URL with Last-Event-ID as query parameter for reconnection
    // Note: Native EventSource doesn't support custom headers, so we use query param
    const connectionUrl = this.lastEventId
      ? `${this.url}?lastEventId=${encodeURIComponent(this.lastEventId)}`
      : this.url;

    logger.debug(
      `Connecting to ${this.url}${this.lastEventId ? ` (resuming from event ${this.lastEventId})` : ''}`,
      'SseClient',
      'connect'
    );

    try {
      this.eventSource = new EventSource(connectionUrl);

      this.eventSource.onopen = () => {
        logger.debug('Connected successfully', 'SseClient', 'onopen');
        this.reconnectAttempts = 0;
        this.reconnectDelay = 1000;
        this.connectionStatus = 'connected';
        this.notifyStatusChange();
      };

      this.eventSource.onerror = (error) => {
        logger.error(
          'Connection error',
          error instanceof Error ? error : new Error('SSE connection error'),
          'SseClient',
          'onerror'
        );

        if (this.eventSource?.readyState === EventSource.CLOSED) {
          logger.debug('Connection closed by server', 'SseClient', 'onerror');
          this.connectionStatus = 'disconnected';
          this.notifyStatusChange();
          this.handleConnectionError();
        } else if (this.eventSource?.readyState === EventSource.CONNECTING) {
          logger.debug('Reconnecting...', 'SseClient', 'onerror');
          this.connectionStatus = 'reconnecting';
          this.notifyStatusChange();
        }
      };

      // Register event listeners
      this.registerEventListeners();
    } catch (error) {
      logger.error(
        'Failed to create EventSource',
        error instanceof Error ? error : new Error(String(error)),
        'SseClient',
        'connect'
      );
      this.connectionStatus = 'error';
      this.notifyStatusChange();
      this.handleConnectionError();
    }
  }

  /**
   * Register all SSE event listeners
   */
  private registerEventListeners(): void {
    if (!this.eventSource) return;

    const eventTypes = [
      'job-status',
      'step-status',
      'step-progress',
      'progress-message',
      'warning',
      'error',
      'job-completed',
      'job-failed',
      'job-cancelled',
    ];

    eventTypes.forEach((eventType) => {
      this.eventSource?.addEventListener(eventType, (event: MessageEvent) => {
        try {
          // Track last event ID for reconnection support
          if (event.lastEventId) {
            this.lastEventId = event.lastEventId;
            logger.debug(`Tracked event ID: ${event.lastEventId}`, 'SseClient', 'onEvent');
          }

          const data = JSON.parse(event.data);
          logger.debug(`Received ${eventType}`, 'SseClient', 'onEvent', { data });

          // Emit event with event ID for tracking
          this.emit(eventType, data, event.lastEventId);
        } catch (error) {
          logger.error(
            `Failed to parse ${eventType} event`,
            error instanceof Error ? error : new Error(String(error)),
            'SseClient',
            'onEvent'
          );
        }
      });
    });
  }

  /**
   * Handle connection errors with exponential backoff
   */
  private handleConnectionError(): void {
    if (this.isManualClose) {
      logger.debug('Manual close, not reconnecting', 'SseClient', 'handleConnectionError');
      return;
    }

    if (this.reconnectAttempts >= this.maxReconnectAttempts) {
      logger.error(
        'Max reconnect attempts reached',
        new Error('SSE reconnection failed'),
        'SseClient',
        'handleConnectionError'
      );
      this.emit('error', {
        message: 'Failed to connect to server after multiple attempts. Please refresh the page.',
        correlationId: 'sse-reconnect-failed',
      });
      return;
    }

    this.reconnectAttempts++;
    const calculatedDelay = this.reconnectDelay * Math.pow(2, this.reconnectAttempts - 1);
    const delay = Math.min(calculatedDelay, this.maxReconnectDelay);

    logger.debug(
      `Reconnecting in ${delay}ms (attempt ${this.reconnectAttempts}/${this.maxReconnectAttempts})`,
      'SseClient',
      'handleConnectionError'
    );

    this.reconnectTimer = setTimeout(() => {
      logger.debug('Attempting reconnect...', 'SseClient', 'reconnectTimer');
      this.close();
      this.connect();
    }, delay);
  }

  /**
   * Subscribe to a specific event type
   */
  on(eventType: string, handler: EventHandler): void {
    if (!this.handlers.has(eventType)) {
      this.handlers.set(eventType, []);
    }
    this.handlers.get(eventType)!.push(handler);
  }

  /**
   * Unsubscribe from a specific event type
   */
  off(eventType: string, handler: EventHandler): void {
    const handlers = this.handlers.get(eventType);
    if (handlers) {
      const index = handlers.indexOf(handler);
      if (index !== -1) {
        handlers.splice(index, 1);
      }
    }
  }

  /**
   * Emit an event to all registered handlers
   */
  private emit(eventType: string, data: unknown, eventId?: string): void {
    const handlers = this.handlers.get(eventType);
    if (handlers) {
      handlers.forEach((handler) => {
        try {
          handler({ type: eventType, data, eventId });
        } catch (error) {
          logger.error(
            'Error in event handler',
            error instanceof Error ? error : new Error(String(error)),
            'SseClient',
            'emit'
          );
        }
      });
    }
  }

  /**
   * Close the SSE connection
   */
  close(): void {
    this.isManualClose = true;

    if (this.reconnectTimer) {
      clearTimeout(this.reconnectTimer);
      this.reconnectTimer = null;
    }

    if (this.eventSource) {
      logger.debug('Closing connection', 'SseClient', 'close');
      this.eventSource.close();
      this.eventSource = null;
    }

    this.handlers.clear();
    this.statusChangeHandlers = [];
    this.reconnectAttempts = 0;
    this.connectionStatus = 'disconnected';
    this.notifyStatusChange();
  }

  /**
   * Check if the client is connected
   */
  isConnected(): boolean {
    return this.eventSource?.readyState === EventSource.OPEN;
  }
}

/**
 * Create and manage an SSE client for a job
 */
export function createSseClient(jobId: string): SseClient {
  return new SseClient(jobId);
}
