/**
 * useApiError Hook
 * Standardized error handling for API requests
 */

import { AxiosError } from 'axios';
import { useState, useCallback } from 'react';

export interface ApiErrorInfo {
  message: string;
  statusCode?: number;
  errorCode?: string;
  correlationId?: string;
  retryable: boolean;
  userMessage: string;
}

/**
 * Parse API error into user-friendly format
 */
function parseApiError(error: unknown): ApiErrorInfo {
  // Default error info
  let errorInfo: ApiErrorInfo = {
    message: 'An unexpected error occurred',
    retryable: false,
    userMessage: 'An unexpected error occurred. Please try again.',
  };

  if (error instanceof AxiosError) {
    const response = error.response;
    const data = response?.data as Record<string, unknown> | undefined;

    errorInfo = {
      message: error.message,
      statusCode: response?.status,
      errorCode: (data?.errorCode as string) || (data?.code as string),
      correlationId: (data?.correlationId as string) || response?.headers?.['x-correlation-id'],
      retryable: isRetryableError(response?.status),
      userMessage: getUserFriendlyMessage(error),
    };
  } else if (error instanceof Error) {
    errorInfo = {
      message: error.message,
      retryable: false,
      userMessage: error.message,
    };
  }

  return errorInfo;
}

/**
 * Determine if error is retryable
 */
function isRetryableError(statusCode?: number): boolean {
  if (!statusCode) return true; // Network errors are retryable

  // Retryable status codes
  return (
    statusCode === 408 || // Request Timeout
    statusCode === 429 || // Too Many Requests
    statusCode === 500 || // Internal Server Error
    statusCode === 502 || // Bad Gateway
    statusCode === 503 || // Service Unavailable
    statusCode === 504 // Gateway Timeout
  );
}

/**
 * Get user-friendly error message
 */
function getUserFriendlyMessage(error: AxiosError): string {
  const response = error.response;
  const data = response?.data as Record<string, unknown> | undefined;

  // Use custom message from response if available
  if (data?.message || data?.detail) {
    return (data.message || data.detail) as string;
  }

  // Default messages based on status code
  switch (response?.status) {
    case 400:
      return 'Invalid request. Please check your input and try again.';
    case 401:
      return 'Authentication required. Please sign in.';
    case 403:
      return 'You do not have permission to perform this action.';
    case 404:
      return 'The requested resource was not found.';
    case 408:
      return 'Request timed out. Please try again.';
    case 429:
      return 'Too many requests. Please wait a moment and try again.';
    case 500:
      return 'Server error. Please try again later.';
    case 502:
      return 'Bad gateway. The server is temporarily unavailable.';
    case 503:
      return 'Service unavailable. Please try again later.';
    case 504:
      return 'Gateway timeout. The server took too long to respond.';
    default:
      if (!response) {
        return 'Network error. Please check your connection and try again.';
      }
      return 'An error occurred. Please try again.';
  }
}

export interface UseApiErrorResult {
  error: Error | null;
  errorInfo: ApiErrorInfo | null;
  setError: (error: unknown) => void;
  clearError: () => void;
  isRetryable: boolean;
}

/**
 * Hook for standardized API error handling
 */
export function useApiError(): UseApiErrorResult {
  const [error, setErrorState] = useState<Error | null>(null);
  const [errorInfo, setErrorInfo] = useState<ApiErrorInfo | null>(null);

  const setError = useCallback((error: unknown) => {
    const info = parseApiError(error);
    setErrorInfo(info);
    setErrorState(error instanceof Error ? error : new Error(info.message));
  }, []);

  const clearError = useCallback(() => {
    setErrorState(null);
    setErrorInfo(null);
  }, []);

  return {
    error,
    errorInfo,
    setError,
    clearError,
    isRetryable: errorInfo?.retryable ?? false,
  };
}
