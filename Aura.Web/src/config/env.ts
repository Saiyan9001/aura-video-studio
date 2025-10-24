/**
 * Environment configuration
 * Provides type-safe access to environment variables
 */

export const env = {
  // In production, use relative URL so it works from any origin (127.0.0.1, localhost, etc.)
  // In development, use full URL to proxy to the dev API server
  apiBaseUrl: import.meta.env.VITE_API_BASE_URL || (import.meta.env.PROD ? '' : 'http://localhost:5272'),
  appVersion: import.meta.env.VITE_APP_VERSION || '1.0.0',
  appName: import.meta.env.VITE_APP_NAME || 'Aura Video Studio',
  environment: import.meta.env.VITE_ENV || 'development',
  enableAnalytics: import.meta.env.VITE_ENABLE_ANALYTICS === 'true',
  enableDebug: import.meta.env.VITE_ENABLE_DEBUG === 'true',
  isDevelopment: import.meta.env.DEV,
  isProduction: import.meta.env.PROD,
  mode: import.meta.env.MODE,
} as const;

// Export for debugging purposes (only in development)
if (env.isDevelopment && env.enableDebug) {
  // eslint-disable-next-line no-console
  console.log('Environment Configuration:', env);
}
