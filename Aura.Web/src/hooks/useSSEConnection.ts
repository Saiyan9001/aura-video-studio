/**
 * useSSEConnection Hook
 * Manages EventSource (Server-Sent Events) connections with automatic reconnection
 */

import { useState, useCallback, useRef, useEffect } from 'react';
import { env } from '../config/env';

export interface SSEMessage {
  type: string;
  data: unknown;
  eventId?: string;
}

export interface UseSSEConnectionOptions {
  onMessage?: (message: SSEMessage) => void;
  onError?: (error: Error) => void;
  onOpen?: () => void;
  onClose?: () => void;
  reconnectDelay?: number;
  maxReconnectAttempts?: number;
}

export interface UseSSEConnectionResult {
  isConnected: boolean;
  reconnectAttempt: number;
  connect: (endpoint: string) => void;
  disconnect: () => void;
  send: (type: string, data: unknown) => void;
}

/**
 * Hook for managing SSE connections with auto-reconnect
 */
export function useSSEConnection(options: UseSSEConnectionOptions = {}): UseSSEConnectionResult {
  const {
    onMessage,
    onError,
    onOpen,
    onClose,
    reconnectDelay = 3000,
    maxReconnectAttempts = 5,
  } = options;

  const [isConnected, setIsConnected] = useState(false);
  const [reconnectAttempt, setReconnectAttempt] = useState(0);

  const eventSourceRef = useRef<EventSource | null>(null);
  const reconnectTimeoutRef = useRef<ReturnType<typeof setTimeout> | null>(null);
  const endpointRef = useRef<string | null>(null);
  const lastEventIdRef = useRef<string | null>(null);

  // Connect to SSE endpoint
  const connect = useCallback(
    (endpoint: string) => {
      // Disconnect existing connection
      if (eventSourceRef.current) {
        eventSourceRef.current.close();
      }

      // Clear any pending reconnect
      if (reconnectTimeoutRef.current) {
        clearTimeout(reconnectTimeoutRef.current);
      }

      endpointRef.current = endpoint;

      try {
        // Build URL with base API URL and last event ID if available
        const url = new URL(endpoint, env.apiBaseUrl);
        if (lastEventIdRef.current) {
          url.searchParams.set('lastEventId', lastEventIdRef.current);
        }

        const eventSource = new EventSource(url.toString());
        eventSourceRef.current = eventSource;

        // Connection opened
        eventSource.onopen = () => {
          setIsConnected(true);
          setReconnectAttempt(0);
          if (onOpen) {
            onOpen();
          }
        };

        // Handle all event types
        const eventTypes = [
          'job-status',
          'step-progress',
          'step-status',
          'job-completed',
          'job-failed',
          'job-cancelled',
          'warning',
          'error',
        ];

        eventTypes.forEach((eventType) => {
          eventSource.addEventListener(eventType, (event: MessageEvent) => {
            try {
              // Store last event ID for reconnection
              if (event.lastEventId) {
                lastEventIdRef.current = event.lastEventId;
              }

              const data = JSON.parse(event.data);
              if (onMessage) {
                onMessage({
                  type: eventType,
                  data,
                  eventId: event.lastEventId,
                });
              }
            } catch (error) {
              console.error(`Failed to parse ${eventType} event:`, error);
              if (onError) {
                onError(
                  error instanceof Error ? error : new Error(`Failed to parse ${eventType} event`)
                );
              }
            }
          });
        });

        // Handle errors and reconnection
        eventSource.onerror = () => {
          setIsConnected(false);

          // Don't reconnect if manually disconnected
          if (!endpointRef.current) {
            return;
          }

          const error = new Error('SSE connection error');

          // Check if we should attempt reconnection
          if (reconnectAttempt < maxReconnectAttempts) {
            const delay = reconnectDelay * Math.pow(2, reconnectAttempt);
            console.log(
              `SSE connection lost. Reconnecting in ${delay}ms (attempt ${reconnectAttempt + 1}/${maxReconnectAttempts})...`
            );

            reconnectTimeoutRef.current = setTimeout(() => {
              setReconnectAttempt((prev) => prev + 1);
              if (endpointRef.current) {
                connect(endpointRef.current);
              }
            }, delay);
          } else {
            console.error(
              `SSE connection failed after ${maxReconnectAttempts} attempts. Giving up.`
            );
            if (onError) {
              onError(error);
            }
          }
        };
      } catch (error) {
        const err = error instanceof Error ? error : new Error('Failed to connect to SSE');
        console.error('Failed to create EventSource:', err);
        if (onError) {
          onError(err);
        }
      }
    },
    [reconnectAttempt, reconnectDelay, maxReconnectAttempts, onMessage, onError, onOpen]
  );

  // Disconnect from SSE
  const disconnect = useCallback(() => {
    endpointRef.current = null;
    lastEventIdRef.current = null;
    setReconnectAttempt(0);

    if (reconnectTimeoutRef.current) {
      clearTimeout(reconnectTimeoutRef.current);
      reconnectTimeoutRef.current = null;
    }

    if (eventSourceRef.current) {
      eventSourceRef.current.close();
      eventSourceRef.current = null;
      setIsConnected(false);
      if (onClose) {
        onClose();
      }
    }
  }, [onClose]);

  // Send is not supported by EventSource (it's one-way from server)
  // This is a placeholder for consistency with WebSocket-like APIs
  const send = useCallback(() => {
    // EventSource does not support sending messages to the server
  }, []);

  // Cleanup on unmount
  useEffect(() => {
    return () => {
      disconnect();
    };
  }, [disconnect]);

  return {
    isConnected,
    reconnectAttempt,
    connect,
    disconnect,
    send,
  };
}
