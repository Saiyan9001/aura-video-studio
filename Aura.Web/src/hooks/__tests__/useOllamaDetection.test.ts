import { renderHook, waitFor } from '@testing-library/react';
import { describe, it, expect, vi, beforeEach, afterEach } from 'vitest';
import { useOllamaDetection } from '../useOllamaDetection';

describe('useOllamaDetection', () => {
  const originalFetch = global.fetch;

  beforeEach(() => {
    sessionStorage.clear();
    global.fetch = vi.fn();
  });

  afterEach(() => {
    global.fetch = originalFetch;
  });

  it('should initialize with null state when autoDetect is false', () => {
    const { result } = renderHook(() => useOllamaDetection(false));

    expect(result.current.isDetected).toBe(null);
    expect(result.current.isChecking).toBe(false);
    expect(result.current.lastChecked).toBe(null);
    expect(result.current.error).toBe(null);
  });

  it('should auto-detect on mount when autoDetect is true', async () => {
    (global.fetch as ReturnType<typeof vi.fn>).mockResolvedValueOnce({
      ok: true,
    } as Response);

    const { result } = renderHook(() => useOllamaDetection(true));

    expect(result.current.isChecking).toBe(true);

    await waitFor(
      () => {
        expect(result.current.isDetected).toBe(true);
      },
      { timeout: 3000 }
    );

    expect(result.current.isChecking).toBe(false);
    expect(result.current.error).toBe(null);
  });

  it('should return cached detection result', async () => {
    const cachedData = {
      isDetected: true,
      timestamp: Date.now(),
    };
    sessionStorage.setItem('ollama_detection_cache', JSON.stringify(cachedData));

    const { result } = renderHook(() => useOllamaDetection(true));

    await waitFor(
      () => {
        expect(result.current.isDetected).toBe(true);
      },
      { timeout: 1000 }
    );

    expect(result.current.isChecking).toBe(false);
    expect(global.fetch).not.toHaveBeenCalled();
  });

  it('should retry once on failure', async () => {
    (global.fetch as ReturnType<typeof vi.fn>)
      .mockRejectedValueOnce(new Error('Network error'))
      .mockResolvedValueOnce({
        ok: true,
      } as Response);

    const { result } = renderHook(() => useOllamaDetection(true));

    await waitFor(
      () => {
        expect(result.current.isDetected).toBe(true);
      },
      { timeout: 3000 }
    );

    expect(global.fetch).toHaveBeenCalledTimes(2);
  });

  it('should return false when detection fails after retry', async () => {
    (global.fetch as ReturnType<typeof vi.fn>)
      .mockRejectedValueOnce(new Error('Network error'))
      .mockRejectedValueOnce(new Error('Network error'));

    const { result } = renderHook(() => useOllamaDetection(true));

    await waitFor(
      () => {
        expect(result.current.isDetected).toBe(false);
      },
      { timeout: 3000 }
    );

    expect(result.current.isChecking).toBe(false);
    expect(global.fetch).toHaveBeenCalledTimes(2);
  });

  it('should cache positive detection results', async () => {
    (global.fetch as ReturnType<typeof vi.fn>).mockResolvedValueOnce({
      ok: true,
    } as Response);

    const { result } = renderHook(() => useOllamaDetection(true));

    await waitFor(
      () => {
        expect(result.current.isDetected).toBe(true);
      },
      { timeout: 3000 }
    );

    const cached = sessionStorage.getItem('ollama_detection_cache');
    expect(cached).toBeTruthy();

    if (cached) {
      const data = JSON.parse(cached);
      expect(data.isDetected).toBe(true);
      expect(data.timestamp).toBeGreaterThan(0);
    }
  });

  it('should allow manual detection trigger', async () => {
    (global.fetch as ReturnType<typeof vi.fn>).mockResolvedValueOnce({
      ok: true,
    } as Response);

    const { result } = renderHook(() => useOllamaDetection(false));

    expect(result.current.isDetected).toBe(null);

    result.current.detect();

    await waitFor(
      () => {
        expect(result.current.isDetected).toBe(true);
      },
      { timeout: 3000 }
    );

    expect(result.current.isChecking).toBe(false);
  });

  it('should handle timeout with AbortController', async () => {
    const abortError = new Error('AbortError');
    abortError.name = 'AbortError';

    (global.fetch as ReturnType<typeof vi.fn>)
      .mockRejectedValueOnce(abortError)
      .mockRejectedValueOnce(abortError);

    const { result } = renderHook(() => useOllamaDetection(true));

    await waitFor(
      () => {
        expect(result.current.isDetected).toBe(false);
      },
      { timeout: 3000 }
    );

    expect(result.current.isChecking).toBe(false);
  });

  it('should not use expired cache', async () => {
    const expiredCachedData = {
      isDetected: true,
      timestamp: Date.now() - 10 * 60 * 1000,
    };
    sessionStorage.setItem('ollama_detection_cache', JSON.stringify(expiredCachedData));

    (global.fetch as ReturnType<typeof vi.fn>).mockResolvedValueOnce({
      ok: true,
    } as Response);

    const { result } = renderHook(() => useOllamaDetection(true));

    await waitFor(
      () => {
        expect(result.current.isDetected).toBe(true);
      },
      { timeout: 3000 }
    );

    expect(global.fetch).toHaveBeenCalled();
  });

  it('should ignore cache for negative results', async () => {
    const negativeCachedData = {
      isDetected: false,
      timestamp: Date.now(),
    };
    sessionStorage.setItem('ollama_detection_cache', JSON.stringify(negativeCachedData));

    (global.fetch as ReturnType<typeof vi.fn>).mockResolvedValueOnce({
      ok: true,
    } as Response);

    const { result } = renderHook(() => useOllamaDetection(true));

    await waitFor(
      () => {
        expect(result.current.isDetected).toBe(true);
      },
      { timeout: 3000 }
    );

    expect(global.fetch).toHaveBeenCalled();
  });
});
