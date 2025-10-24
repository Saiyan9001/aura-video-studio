/**
 * API configuration - centralized API base URL management
 *
 * This allows the frontend to connect to the backend API regardless of port.
 * The API URL can be configured via environment variables or auto-detected.
 */

/**
 * Get the API base URL from environment or use a sensible default
 */
function getApiBaseUrl(): string {
  // Try environment variable first (for production builds)
  if (import.meta.env.VITE_API_URL) {
    return import.meta.env.VITE_API_URL;
  }

  // In development, use proxy configuration
  // The Vite dev server will proxy /api requests to the backend
  if (import.meta.env.DEV) {
    // Return empty string to use relative URLs, which will be proxied by Vite
    return '';
  }

  // Production - use relative URLs since frontend is served from same origin as API
  // This ensures it works with both 127.0.0.1:5005 and localhost:5005
  return '';
}

export const API_BASE_URL = getApiBaseUrl();

/**
 * Construct a full API endpoint URL
 */
export function apiUrl(path: string): string {
  // Ensure path starts with /
  const cleanPath = path.startsWith('/') ? path : `/${path}`;
  return `${API_BASE_URL}${cleanPath}`;
}

/**
 * Helper to make API calls with proper error handling
 */
export async function apiFetch<T = unknown>(path: string, options?: RequestInit): Promise<T> {
  const url = apiUrl(path);
  const response = await fetch(url, {
    ...options,
    headers: {
      'Content-Type': 'application/json',
      ...options?.headers,
    },
  });

  if (!response.ok) {
    const errorText = await response.text();
    throw new Error(`API request failed: ${response.status} ${response.statusText}\n${errorText}`);
  }

  // Handle empty responses
  const text = await response.text();
  if (!text) {
    return undefined as T;
  }

  return JSON.parse(text) as T;
}
