/**
 * Environment configuration
 * Provides type-safe access to environment variables
 */

import { resolveApiBaseUrl } from './apiBaseUrl';
import { loggingService as logger } from '../services/loggingService';

const apiBaseResolution = resolveApiBaseUrl();

export const env = {
  apiBaseUrl: apiBaseResolution.value,
  appVersion: import.meta.env.VITE_APP_VERSION || '1.0.0',
  appName: import.meta.env.VITE_APP_NAME || 'Aura Video Studio',
  environment: import.meta.env.VITE_ENV || 'development',
  enableAnalytics: import.meta.env.VITE_ENABLE_ANALYTICS === 'true',
  enableDebug: import.meta.env.VITE_ENABLE_DEBUG === 'true',
  enableDevTools: import.meta.env.VITE_ENABLE_DEV_TOOLS === 'true',
  isDevelopment: import.meta.env.DEV,
  isProduction: import.meta.env.PROD,
  mode: import.meta.env.MODE,
} as const;

// Log configuration for debugging purposes (only in development)
if (env.isDevelopment && env.enableDebug) {
  logger.debug('Environment configuration loaded', 'config', 'env', {
    apiBaseUrl: env.apiBaseUrl,
    apiBaseUrlSource: apiBaseResolution.source,
    environment: env.environment,
    appVersion: env.appVersion,
  });
}
