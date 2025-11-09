/**
 * useRetry Hook
 * Implements retry logic with exponential backoff
 */

import { useState, useCallback, useRef } from 'react';

export interface UseRetryOptions {
  maxAttempts?: number;
  initialDelay?: number;
  maxDelay?: number;
  backoffMultiplier?: number;
  onRetry?: (attempt: number, delay: number) => void;
  onMaxAttemptsReached?: () => void;
  shouldRetry?: (error: unknown) => boolean;
}

export interface UseRetryResult<T> {
  execute: (fn: () => Promise<T>) => Promise<T>;
  isRetrying: boolean;
  attempt: number;
  nextRetryDelay: number | null;
  reset: () => void;
  cancel: () => void;
}

/**
 * Hook for retry logic with exponential backoff
 */
export function useRetry<T = unknown>(options: UseRetryOptions = {}): UseRetryResult<T> {
  const {
    maxAttempts = 3,
    initialDelay = 1000,
    maxDelay = 30000,
    backoffMultiplier = 2,
    onRetry,
    onMaxAttemptsReached,
    shouldRetry = () => true,
  } = options;

  const [isRetrying, setIsRetrying] = useState(false);
  const [attempt, setAttempt] = useState(0);
  const [nextRetryDelay, setNextRetryDelay] = useState<number | null>(null);

  const abortControllerRef = useRef<AbortController | null>(null);
  const timeoutRef = useRef<ReturnType<typeof setTimeout> | null>(null);

  /**
   * Calculate delay with exponential backoff and jitter
   */
  const calculateDelay = useCallback(
    (attemptNumber: number): number => {
      // Exponential backoff: delay * (multiplier ^ attempt)
      const exponentialDelay = initialDelay * Math.pow(backoffMultiplier, attemptNumber);

      // Add jitter (±25%) to prevent thundering herd
      const jitter = exponentialDelay * 0.25 * (Math.random() * 2 - 1);
      const delayWithJitter = exponentialDelay + jitter;

      // Cap at maximum delay
      return Math.min(delayWithJitter, maxDelay);
    },
    [initialDelay, backoffMultiplier, maxDelay]
  );

  /**
   * Execute function with retry logic
   */
  const execute = useCallback(
    async (fn: () => Promise<T>): Promise<T> => {
      let lastError: unknown;
      let currentAttempt = 0;

      // Create abort controller for cancellation
      abortControllerRef.current = new AbortController();

      while (currentAttempt < maxAttempts) {
        try {
          // Check if cancelled
          if (abortControllerRef.current.signal.aborted) {
            throw new Error('Operation cancelled');
          }

          setAttempt(currentAttempt);
          setIsRetrying(currentAttempt > 0);

          // Execute the function
          const result = await fn();

          // Success - reset state and return
          setIsRetrying(false);
          setAttempt(0);
          setNextRetryDelay(null);
          return result;
        } catch (error) {
          lastError = error;

          // Check if we should retry this error
          if (!shouldRetry(error)) {
            throw error;
          }

          currentAttempt++;

          // Check if we've exhausted all attempts
          if (currentAttempt >= maxAttempts) {
            setIsRetrying(false);
            setNextRetryDelay(null);
            if (onMaxAttemptsReached) {
              onMaxAttemptsReached();
            }
            throw error;
          }

          // Calculate delay and notify
          const delay = calculateDelay(currentAttempt - 1);
          setNextRetryDelay(delay);

          if (onRetry) {
            onRetry(currentAttempt, delay);
          }

          // Wait before retrying (with cancellation support)
          await new Promise<void>((resolve, reject) => {
            timeoutRef.current = setTimeout(() => {
              if (abortControllerRef.current?.signal.aborted) {
                reject(new Error('Operation cancelled'));
              } else {
                resolve();
              }
            }, delay);
          });
        }
      }

      // This should never be reached, but TypeScript needs it
      throw lastError;
    },
    [maxAttempts, calculateDelay, shouldRetry, onRetry, onMaxAttemptsReached]
  );

  /**
   * Reset retry state
   */
  const reset = useCallback(() => {
    setIsRetrying(false);
    setAttempt(0);
    setNextRetryDelay(null);

    if (timeoutRef.current) {
      clearTimeout(timeoutRef.current);
      timeoutRef.current = null;
    }
  }, []);

  /**
   * Cancel ongoing retry
   */
  const cancel = useCallback(() => {
    if (abortControllerRef.current) {
      abortControllerRef.current.abort();
    }

    if (timeoutRef.current) {
      clearTimeout(timeoutRef.current);
      timeoutRef.current = null;
    }

    reset();
  }, [reset]);

  return {
    execute,
    isRetrying,
    attempt,
    nextRetryDelay,
    reset,
    cancel,
  };
}
