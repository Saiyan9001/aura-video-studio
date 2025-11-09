/**
 * Unit tests for useApiError hook
 */

import { renderHook, act } from '@testing-library/react';
import { AxiosError } from 'axios';
import { describe, it, expect, beforeEach } from 'vitest';
import { useApiError } from '../useApiError';

describe('useApiError', () => {
  it('should initialize with no error', () => {
    const { result } = renderHook(() => useApiError());

    expect(result.current.error).toBeNull();
    expect(result.current.errorInfo).toBeNull();
    expect(result.current.isRetryable).toBe(false);
  });

  it('should set error from AxiosError with 500 status', () => {
    const { result } = renderHook(() => useApiError());

    const axiosError = new AxiosError('Server Error');
    axiosError.response = {
      status: 500,
      statusText: 'Internal Server Error',
      data: { message: 'Internal server error occurred' },
      headers: {},
      config: {} as never,
    };

    act(() => {
      result.current.setError(axiosError);
    });

    expect(result.current.error).toBeTruthy();
    expect(result.current.errorInfo?.statusCode).toBe(500);
    expect(result.current.isRetryable).toBe(true);
  });

  it('should set error from AxiosError with 404 status', () => {
    const { result } = renderHook(() => useApiError());

    const axiosError = new AxiosError('Not Found');
    axiosError.response = {
      status: 404,
      statusText: 'Not Found',
      data: { message: 'Resource not found' },
      headers: {},
      config: {} as never,
    };

    act(() => {
      result.current.setError(axiosError);
    });

    expect(result.current.error).toBeTruthy();
    expect(result.current.errorInfo?.statusCode).toBe(404);
    expect(result.current.isRetryable).toBe(false);
  });

  it('should set error from regular Error', () => {
    const { result } = renderHook(() => useApiError());

    const error = new Error('Something went wrong');

    act(() => {
      result.current.setError(error);
    });

    expect(result.current.error).toBeTruthy();
    expect(result.current.error?.message).toBe('Something went wrong');
    expect(result.current.isRetryable).toBe(false);
  });

  it('should clear error', () => {
    const { result } = renderHook(() => useApiError());

    const error = new Error('Test error');

    act(() => {
      result.current.setError(error);
    });

    expect(result.current.error).toBeTruthy();

    act(() => {
      result.current.clearError();
    });

    expect(result.current.error).toBeNull();
    expect(result.current.errorInfo).toBeNull();
  });

  it('should identify retryable errors (503)', () => {
    const { result } = renderHook(() => useApiError());

    const axiosError = new AxiosError('Service Unavailable');
    axiosError.response = {
      status: 503,
      statusText: 'Service Unavailable',
      data: {},
      headers: {},
      config: {} as never,
    };

    act(() => {
      result.current.setError(axiosError);
    });

    expect(result.current.isRetryable).toBe(true);
  });

  it('should extract correlation ID from response', () => {
    const { result } = renderHook(() => useApiError());

    const axiosError = new AxiosError('Server Error');
    axiosError.response = {
      status: 500,
      statusText: 'Internal Server Error',
      data: { correlationId: 'test-correlation-123' },
      headers: {},
      config: {} as never,
    };

    act(() => {
      result.current.setError(axiosError);
    });

    expect(result.current.errorInfo?.correlationId).toBe('test-correlation-123');
  });

  it('should use custom message from response data', () => {
    const { result } = renderHook(() => useApiError());

    const customMessage = 'Custom error message from server';
    const axiosError = new AxiosError('Server Error');
    axiosError.response = {
      status: 400,
      statusText: 'Bad Request',
      data: { message: customMessage },
      headers: {},
      config: {} as never,
    };

    act(() => {
      result.current.setError(axiosError);
    });

    expect(result.current.errorInfo?.userMessage).toBe(customMessage);
  });
});
