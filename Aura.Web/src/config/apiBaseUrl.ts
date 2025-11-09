/**
 * Utilities for resolving the API base URL used by the frontend.
 * Falls back to the current origin to avoid cross-origin issues when the UI
 * is served directly from the Aura.Api backend without explicit configuration.
 */

export type ApiBaseUrlSource = 'env' | 'origin' | 'fallback';

export interface ApiBaseUrlResolution {
  value: string;
  source: ApiBaseUrlSource;
  raw?: string | undefined;
}

const DEFAULT_DEV_API_BASE_URL = 'http://127.0.0.1:5005';

const trimValue = (value: unknown): string | undefined => {
  if (typeof value !== 'string') {
    return undefined;
  }

  const trimmed = value.trim();
  return trimmed.length > 0 ? trimmed : undefined;
};

/**
 * Resolve the API base URL from configuration or runtime context.
 */
export function resolveApiBaseUrl(): ApiBaseUrlResolution {
  const rawEnvValue = trimValue(import.meta.env.VITE_API_BASE_URL);

  if (rawEnvValue) {
    return {
      value: rawEnvValue,
      raw: rawEnvValue,
      source: 'env',
    };
  }

  if (typeof window !== 'undefined' && window.location?.origin) {
    return {
      value: window.location.origin,
      raw: window.location.origin,
      source: 'origin',
    };
  }

  return {
    value: DEFAULT_DEV_API_BASE_URL,
    raw: DEFAULT_DEV_API_BASE_URL,
    source: 'fallback',
  };
}
