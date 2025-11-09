import { useState, useEffect, useCallback } from 'react';

interface OllamaDetectionResult {
  isDetected: boolean | null;
  isChecking: boolean;
  lastChecked: Date | null;
  error: string | null;
}

const OLLAMA_URL = 'http://localhost:11434';
const DETECTION_TIMEOUT = 2000;
const SESSION_CACHE_KEY = 'ollama_detection_cache';
const CACHE_DURATION = 5 * 60 * 1000;

interface CachedDetection {
  isDetected: boolean;
  timestamp: number;
}

/**
 * Hook for detecting Ollama availability on localhost:11434
 * - Auto-detects on mount and when manually triggered
 * - Caches positive detections for the session
 * - Uses short timeout (2s) and single retry with small backoff
 * - Returns neutral state while checking to avoid flicker
 */
export function useOllamaDetection(autoDetect = true) {
  const [result, setResult] = useState<OllamaDetectionResult>({
    isDetected: null,
    isChecking: false,
    lastChecked: null,
    error: null,
  });

  const checkFromCache = useCallback((): boolean | null => {
    try {
      const cached = sessionStorage.getItem(SESSION_CACHE_KEY);
      if (cached) {
        const data: CachedDetection = JSON.parse(cached);
        const age = Date.now() - data.timestamp;
        if (age < CACHE_DURATION && data.isDetected) {
          return data.isDetected;
        }
      }
    } catch (error) {
      console.warn('Failed to read Ollama detection cache:', error);
    }
    return null;
  }, []);

  const saveToCache = useCallback((isDetected: boolean) => {
    if (isDetected) {
      try {
        const data: CachedDetection = {
          isDetected,
          timestamp: Date.now(),
        };
        sessionStorage.setItem(SESSION_CACHE_KEY, JSON.stringify(data));
      } catch (error) {
        console.warn('Failed to cache Ollama detection:', error);
      }
    }
  }, []);

  const probeOllama = useCallback(async (): Promise<boolean> => {
    const controller = new AbortController();
    const timeoutId = setTimeout(() => controller.abort(), DETECTION_TIMEOUT);

    try {
      const response = await fetch(`${OLLAMA_URL}/api/tags`, {
        method: 'GET',
        signal: controller.signal,
        headers: {
          Accept: 'application/json',
        },
      });
      clearTimeout(timeoutId);
      return response.ok;
    } catch (error: unknown) {
      clearTimeout(timeoutId);
      return false;
    }
  }, []);

  const detect = useCallback(async () => {
    const cached = checkFromCache();
    if (cached !== null) {
      setResult({
        isDetected: cached,
        isChecking: false,
        lastChecked: new Date(),
        error: null,
      });
      return;
    }

    setResult((prev) => ({ ...prev, isChecking: true, error: null }));

    try {
      let isDetected = await probeOllama();

      if (!isDetected) {
        await new Promise((resolve) => setTimeout(resolve, 500));
        isDetected = await probeOllama();
      }

      saveToCache(isDetected);
      setResult({
        isDetected,
        isChecking: false,
        lastChecked: new Date(),
        error: null,
      });
    } catch (error: unknown) {
      const errorMessage = error instanceof Error ? error.message : 'Network error occurred';
      setResult({
        isDetected: false,
        isChecking: false,
        lastChecked: new Date(),
        error: errorMessage,
      });
    }
  }, [checkFromCache, probeOllama, saveToCache]);

  useEffect(() => {
    if (autoDetect) {
      detect();
    }
  }, [autoDetect, detect]);

  return {
    ...result,
    detect,
  };
}
