/**
 * Unit tests for useRetry hook
 */

import { renderHook, act, waitFor } from '@testing-library/react';
import { describe, it, expect, vi, beforeEach } from 'vitest';
import { useRetry } from '../useRetry';

describe('useRetry', () => {
  beforeEach(() => {
    vi.useFakeTimers();
  });

  it('should execute function successfully on first attempt', async () => {
    const { result } = renderHook(() => useRetry());
    const mockFn = vi.fn().mockResolvedValue('success');

    const promise = act(async () => {
      return result.current.execute(mockFn);
    });

    await act(async () => {
      await vi.runAllTimersAsync();
    });

    await expect(promise).resolves.toBe('success');
    expect(mockFn).toHaveBeenCalledTimes(1);
    expect(result.current.isRetrying).toBe(false);
    expect(result.current.attempt).toBe(0);
  });

  it('should retry on failure and succeed', async () => {
    const { result } = renderHook(() => useRetry({ maxAttempts: 3, initialDelay: 1000 }));

    let callCount = 0;
    const mockFn = vi.fn().mockImplementation(() => {
      callCount++;
      if (callCount < 2) {
        return Promise.reject(new Error('Temporary failure'));
      }
      return Promise.resolve('success');
    });

    const promise = act(async () => {
      return result.current.execute(mockFn);
    });

    await act(async () => {
      await vi.runAllTimersAsync();
    });

    await expect(promise).resolves.toBe('success');
    expect(mockFn).toHaveBeenCalledTimes(2);
  });

  it('should fail after max attempts', async () => {
    const { result } = renderHook(() => useRetry({ maxAttempts: 2, initialDelay: 100 }));

    const mockFn = vi.fn().mockRejectedValue(new Error('Persistent failure'));

    const promise = act(async () => {
      return result.current.execute(mockFn);
    });

    await act(async () => {
      await vi.runAllTimersAsync();
    });

    await expect(promise).rejects.toThrow('Persistent failure');
    expect(mockFn).toHaveBeenCalledTimes(2);
  });

  it('should call onRetry callback', async () => {
    const onRetry = vi.fn();
    const { result } = renderHook(() => useRetry({ maxAttempts: 3, initialDelay: 1000, onRetry }));

    let callCount = 0;
    const mockFn = vi.fn().mockImplementation(() => {
      callCount++;
      if (callCount < 2) {
        return Promise.reject(new Error('Failure'));
      }
      return Promise.resolve('success');
    });

    const promise = act(async () => {
      return result.current.execute(mockFn);
    });

    await act(async () => {
      await vi.runAllTimersAsync();
    });

    await promise;

    expect(onRetry).toHaveBeenCalledWith(1, expect.any(Number));
  });

  it('should call onMaxAttemptsReached when all retries exhausted', async () => {
    const onMaxAttemptsReached = vi.fn();
    const { result } = renderHook(() =>
      useRetry({ maxAttempts: 2, initialDelay: 100, onMaxAttemptsReached })
    );

    const mockFn = vi.fn().mockRejectedValue(new Error('Failure'));

    const promise = act(async () => {
      return result.current.execute(mockFn);
    });

    await act(async () => {
      await vi.runAllTimersAsync();
    });

    await expect(promise).rejects.toThrow();
    expect(onMaxAttemptsReached).toHaveBeenCalled();
  });

  it('should not retry if shouldRetry returns false', async () => {
    const shouldRetry = vi.fn().mockReturnValue(false);
    const { result } = renderHook(() => useRetry({ maxAttempts: 3, shouldRetry }));

    const mockFn = vi.fn().mockRejectedValue(new Error('Non-retryable error'));

    const promise = act(async () => {
      return result.current.execute(mockFn);
    });

    await act(async () => {
      await vi.runAllTimersAsync();
    });

    await expect(promise).rejects.toThrow('Non-retryable error');
    expect(mockFn).toHaveBeenCalledTimes(1);
    expect(shouldRetry).toHaveBeenCalled();
  });

  it('should reset state', async () => {
    const { result } = renderHook(() => useRetry({ maxAttempts: 3 }));

    const mockFn = vi.fn().mockRejectedValue(new Error('Failure'));

    act(() => {
      result.current.execute(mockFn).catch(() => {});
    });

    await act(async () => {
      await vi.advanceTimersByTimeAsync(500);
    });

    act(() => {
      result.current.reset();
    });

    expect(result.current.isRetrying).toBe(false);
    expect(result.current.attempt).toBe(0);
    expect(result.current.nextRetryDelay).toBeNull();
  });

  it('should cancel ongoing retry', async () => {
    const { result } = renderHook(() => useRetry({ maxAttempts: 3, initialDelay: 5000 }));

    const mockFn = vi.fn().mockRejectedValue(new Error('Failure'));

    const promise = act(async () => {
      return result.current.execute(mockFn);
    });

    await act(async () => {
      await vi.advanceTimersByTimeAsync(100);
    });

    act(() => {
      result.current.cancel();
    });

    await act(async () => {
      await vi.runAllTimersAsync();
    });

    expect(result.current.isRetrying).toBe(false);
    expect(result.current.attempt).toBe(0);
  });
});
