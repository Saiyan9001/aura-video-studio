/**
 * User-friendly error messages and actionable guidance for API errors
 * Maps backend error codes to helpful messages that guide users on what to do next
 */

export interface ErrorMessage {
  title: string;
  message: string;
  actions?: string[];
  severity: 'error' | 'warning' | 'info';
}

/**
 * HTTP status code to error message mapping
 */
export const HTTP_ERROR_MESSAGES: Record<number, ErrorMessage> = {
  // 4xx Client Errors
  400: {
    title: 'Invalid Request',
    message: 'The request contains invalid data. Please check your input and try again.',
    actions: [
      'Verify all required fields are filled',
      'Check that values are in the correct format',
    ],
    severity: 'error',
  },
  401: {
    title: 'Authentication Required',
    message: 'You need to be logged in to perform this action.',
    actions: ['Please log in', 'Check if your session has expired'],
    severity: 'warning',
  },
  403: {
    title: 'Access Denied',
    message: 'You do not have permission to perform this action.',
    actions: ['Contact your administrator for access', 'Verify you have the required permissions'],
    severity: 'error',
  },
  404: {
    title: 'Not Found',
    message: 'The requested resource could not be found.',
    actions: ['Verify the resource exists', 'Check if it may have been deleted'],
    severity: 'warning',
  },
  408: {
    title: 'Request Timeout',
    message: 'The request took too long to complete.',
    actions: ['Try again', 'Check your internet connection'],
    severity: 'warning',
  },
  409: {
    title: 'Conflict',
    message: 'The request conflicts with the current state of the resource.',
    actions: ['Refresh the page', 'Try again with updated data'],
    severity: 'warning',
  },
  413: {
    title: 'Request Too Large',
    message: 'The file or data you are trying to upload is too large.',
    actions: ['Reduce file size', 'Split into smaller uploads'],
    severity: 'error',
  },
  422: {
    title: 'Validation Failed',
    message: 'The data you provided failed validation.',
    actions: ['Check all required fields', 'Ensure data is in the correct format'],
    severity: 'error',
  },
  428: {
    title: 'Precondition Required',
    message: 'Required setup or configuration is missing.',
    actions: [
      'Complete required setup steps',
      'Check dependencies are installed',
      'Verify configuration is complete',
    ],
    severity: 'warning',
  },
  429: {
    title: 'Too Many Requests',
    message: 'You have made too many requests. Please slow down.',
    actions: ['Wait a moment before trying again', 'Reduce request frequency'],
    severity: 'warning',
  },

  // 5xx Server Errors
  500: {
    title: 'Server Error',
    message: 'An unexpected error occurred on the server.',
    actions: ['Try again in a few moments', 'Contact support if the problem persists'],
    severity: 'error',
  },
  502: {
    title: 'Bad Gateway',
    message: 'The server received an invalid response.',
    actions: ['Try again in a few moments', 'Check service status'],
    severity: 'error',
  },
  503: {
    title: 'Service Unavailable',
    message: 'The service is temporarily unavailable.',
    actions: ['Try again shortly', 'Check if maintenance is scheduled'],
    severity: 'error',
  },
  504: {
    title: 'Gateway Timeout',
    message: 'The server did not respond in time.',
    actions: ['Try again', 'Contact support if this continues'],
    severity: 'error',
  },
};

/**
 * Application-specific error codes to error message mapping
 */
export const APP_ERROR_MESSAGES: Record<string, ErrorMessage> = {
  // Network/Connection Errors
  NETWORK_ERROR: {
    title: 'Network Error',
    message: 'Unable to connect to the server. Please check your internet connection.',
    actions: ['Check your internet connection', 'Try again', 'Contact support if offline'],
    severity: 'error',
  },
  TIMEOUT_ERROR: {
    title: 'Request Timeout',
    message: 'The operation took too long to complete.',
    actions: ['Try again', 'Check your internet connection', 'Contact support if this persists'],
    severity: 'warning',
  },
  CANCELLED_ERROR: {
    title: 'Request Cancelled',
    message: 'The request was cancelled.',
    actions: [],
    severity: 'info',
  },

  // Authentication/Authorization Errors
  E306: {
    title: 'Authentication Failed',
    message: 'Invalid API key or authentication token.',
    actions: [
      'Check your API key configuration',
      'Verify credentials are correct',
      'Try logging in again',
    ],
    severity: 'error',
  },
  AUTH_TOKEN_EXPIRED: {
    title: 'Session Expired',
    message: 'Your session has expired. Please log in again.',
    actions: ['Log in again to continue'],
    severity: 'warning',
  },

  // Script Generation Errors
  E300: {
    title: 'Script Generation Failed',
    message: 'The AI provider encountered an error while generating the script.',
    actions: [
      'Try again with a different prompt',
      'Check AI provider configuration',
      'Verify API quota is not exceeded',
    ],
    severity: 'error',
  },
  E301: {
    title: 'Script Validation Failed',
    message: 'The generated script did not meet validation requirements.',
    actions: ['Try adjusting your prompt', 'Contact support if this continues'],
    severity: 'error',
  },

  // Content Planning Errors
  E304: {
    title: 'Invalid Content Plan',
    message: 'The content plan parameters are invalid.',
    actions: ['Check duration is within allowed range', 'Verify all required fields are set'],
    severity: 'error',
  },
  E305: {
    title: 'Content Generation Failed',
    message: 'Failed to generate content for the plan.',
    actions: ['Try with different parameters', 'Check provider configuration'],
    severity: 'error',
  },

  // Video Generation Errors
  E310: {
    title: 'Video Generation Failed',
    message: 'An error occurred during video generation.',
    actions: ['Check input assets are valid', 'Try again', 'Contact support if this persists'],
    severity: 'error',
  },
  E311: {
    title: 'Video Encoding Failed',
    message: 'Failed to encode the video.',
    actions: ['Check video settings', 'Try a different output format', 'Reduce video quality'],
    severity: 'error',
  },

  // Asset/Upload Errors
  E320: {
    title: 'Asset Upload Failed',
    message: 'Failed to upload the asset.',
    actions: ['Check file size and format', 'Verify internet connection', 'Try again'],
    severity: 'error',
  },
  E321: {
    title: 'Unsupported File Type',
    message: 'The file type is not supported.',
    actions: ['Check supported file formats', 'Convert file to a supported format'],
    severity: 'error',
  },
  E322: {
    title: 'Asset Processing Failed',
    message: 'Failed to process the uploaded asset.',
    actions: ['Check file is not corrupted', 'Try uploading again', 'Use a different file'],
    severity: 'error',
  },

  // Provider/Configuration Errors
  E330: {
    title: 'Provider Not Configured',
    message: 'The required service provider is not configured.',
    actions: [
      'Configure the provider in settings',
      'Check API keys are set',
      'Contact administrator',
    ],
    severity: 'error',
  },
  E331: {
    title: 'Provider Error',
    message: 'The external service provider returned an error.',
    actions: ['Check provider status', 'Verify API quota', 'Try a different provider'],
    severity: 'error',
  },
  E332: {
    title: 'Rate Limit Exceeded',
    message: 'You have exceeded the rate limit for this provider.',
    actions: [
      'Wait before trying again',
      'Upgrade your plan for higher limits',
      'Use a different provider',
    ],
    severity: 'warning',
  },

  // Generic Fallback
  UNKNOWN_ERROR: {
    title: 'Unknown Error',
    message: 'An unexpected error occurred.',
    actions: ['Try again', 'Contact support if the problem persists'],
    severity: 'error',
  },
};

/**
 * Get user-friendly error message for HTTP status code
 */
export function getHttpErrorMessage(status: number): ErrorMessage {
  return (
    HTTP_ERROR_MESSAGES[status] || {
      title: `Error ${status}`,
      message: 'An error occurred while processing your request.',
      actions: ['Try again', 'Contact support if this persists'],
      severity: 'error',
    }
  );
}

/**
 * Get user-friendly error message for application error code
 */
export function getAppErrorMessage(errorCode: string): ErrorMessage {
  return APP_ERROR_MESSAGES[errorCode] || APP_ERROR_MESSAGES.UNKNOWN_ERROR;
}

/**
 * Determine if an error is transient and can be retried
 */
export function isTransientError(status?: number, errorCode?: string): boolean {
  // Network errors (no status)
  if (!status) return true;

  // 5xx server errors are generally transient
  if (status >= 500 && status < 600) return true;

  // Specific 4xx errors that are transient
  if (status === 408 || status === 429) return true;

  // Specific error codes that are transient
  if (errorCode === 'NETWORK_ERROR' || errorCode === 'TIMEOUT_ERROR') return true;

  return false;
}

/**
 * Determine if an error should trigger circuit breaker
 */
export function shouldTriggerCircuitBreaker(status?: number, errorCode?: string): boolean {
  // 5xx errors indicate backend issues
  if (status && status >= 500 && status < 600) return true;

  // Network errors indicate connectivity issues
  if (errorCode === 'NETWORK_ERROR') return true;

  // Timeouts indicate backend is too slow
  if (status === 504 || errorCode === 'TIMEOUT_ERROR') return true;

  return false;
}
