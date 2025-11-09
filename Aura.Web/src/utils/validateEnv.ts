/**
 * Environment variable validation utility
 * Validates required environment variables before app initialization
 */

import { resolveApiBaseUrl, type ApiBaseUrlSource } from '../config/apiBaseUrl';

interface ValidationResult {
  isValid: boolean;
  errors: string[];
  resolvedApiBaseUrl: string;
  source: ApiBaseUrlSource;
}

/**
 * Validates that all required environment variables are properly configured
 * @throws Error if validation fails with detailed message
 */
export function validateEnvironment(): void {
  const result = checkEnvironment();

  if (!result.isValid) {
    const errorMessage = createErrorMessage(result.errors);
    throw new Error(errorMessage);
  }

  // Surface informational message when falling back to automatic resolution
  if (typeof console !== 'undefined' && result.source !== 'env') {
    const sourceDescription =
      result.source === 'origin'
        ? 'the current browser origin'
        : 'the default development fallback (http://127.0.0.1:5005)';

    console.info(
      `[Environment] VITE_API_BASE_URL not configured. Using ${sourceDescription}: ${result.resolvedApiBaseUrl}`
    );
  }
}

/**
 * Checks environment variables and returns validation result
 */
function checkEnvironment(): ValidationResult {
  const errors: string[] = [];
  const resolution = resolveApiBaseUrl();

  validateApiBaseUrl(resolution.value, resolution.source, errors);

  return {
    isValid: errors.length === 0,
    errors,
    resolvedApiBaseUrl: resolution.value,
    source: resolution.source,
  };
}

function validateApiBaseUrl(value: string, source: ApiBaseUrlSource, errors: string[]): void {
  if (!value || value.trim().length === 0) {
    errors.push('API base URL could not be resolved');
    return;
  }

  // Allow relative paths when explicitly provided via environment configuration
  if (source === 'env' && value.startsWith('/')) {
    return;
  }

  try {
    const isRelativePath = value.startsWith('/');
    const baseForRelative =
      isRelativePath && typeof window !== 'undefined'
        ? window.location.origin
        : isRelativePath
          ? 'http://127.0.0.1'
          : undefined;

    const url = new URL(value, baseForRelative);

    if (!['http:', 'https:'].includes(url.protocol)) {
      errors.push(
        `${source === 'env' ? 'VITE_API_BASE_URL' : 'Resolved API base URL'} has invalid protocol "${
          url.protocol
        }" (must be http: or https:)`
      );
    }
  } catch (error) {
    errors.push(
      `${
        source === 'env' ? 'VITE_API_BASE_URL' : 'Resolved API base URL'
      } is not a valid URL or path: "${value}"`
    );
  }
}

/**
 * Creates a user-friendly error message with fix instructions
 */
function createErrorMessage(errors: string[]): string {
  const errorList = errors.map((e) => `  • ${e}`).join('\n');

  return `
Environment Configuration Error
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

The application cannot start due to missing or invalid environment configuration:

${errorList}

How to fix:
━━━━━━━━━━━━

If you want to target a different API origin, create a file named ".env.local"
in the Aura.Web directory and add the following line (adjust as needed):

   VITE_API_BASE_URL=http://127.0.0.1:5005

For production deployments, use your production API URL:

   VITE_API_BASE_URL=https://your-api-domain.com

Save the file and rebuild or restart the application after making changes.

Note: The .env.local file should NOT be committed to version control.
It's already listed in .gitignore.
`.trim();
}
