/**
 * OpenAI API Key Validation Service
 *
 * Provides user-friendly validation of OpenAI API keys with detailed error messages
 */

import type { ValidateOpenAIKeyRequest, ProviderValidationResponse } from '../types/api-v1';
import apiClient from './api/apiClient';

export interface OpenAIValidationResult {
  isValid: boolean;
  status:
    | 'Valid'
    | 'Invalid'
    | 'RateLimited'
    | 'PermissionDenied'
    | 'ServiceIssue'
    | 'NetworkError'
    | 'Timeout';
  message: string;
  canSave: boolean;
}

/**
 * Validate an OpenAI API key with the backend
 */
export async function validateOpenAIKey(
  apiKey: string,
  baseUrl?: string,
  organizationId?: string,
  projectId?: string
): Promise<OpenAIValidationResult> {
  try {
    const request: ValidateOpenAIKeyRequest = {
      apiKey,
      baseUrl,
      organizationId,
      projectId,
    };

    const response = await apiClient.post<ProviderValidationResponse>(
      '/api/providers/openai/validate',
      request
    );

    const result = response.data;

    return mapValidationResponse(result);
  } catch (error: unknown) {
    console.error('OpenAI validation error:', error);

    return {
      isValid: false,
      status: 'NetworkError',
      message: 'Network error while contacting server. Please check your connection.',
      canSave: false,
    };
  }
}

/**
 * Map backend validation response to user-friendly result
 */
function mapValidationResponse(response: ProviderValidationResponse): OpenAIValidationResult {
  const status = response.status;

  switch (status) {
    case 'Valid':
      return {
        isValid: true,
        status: 'Valid',
        message: response.message || 'API key is valid and verified with OpenAI.',
        canSave: true,
      };

    case 'Invalid':
      return {
        isValid: false,
        status: 'Invalid',
        message: response.message || 'Invalid API key. Please check the value and try again.',
        canSave: false,
      };

    case 'PermissionDenied':
      return {
        isValid: false,
        status: 'PermissionDenied',
        message:
          response.message || 'Access denied. Check organization/project permissions or billing.',
        canSave: false,
      };

    case 'RateLimited':
      return {
        isValid: true,
        status: 'RateLimited',
        message:
          response.message ||
          "Rate limited. Your key is valid, but you've hit a limit. Try again later.",
        canSave: true,
      };

    case 'ServiceIssue':
      return {
        isValid: false,
        status: 'ServiceIssue',
        message:
          response.message || 'OpenAI service issue. Your key may be valid; please retry shortly.',
        canSave: true,
      };

    case 'NetworkError':
      return {
        isValid: false,
        status: 'NetworkError',
        message:
          response.message ||
          'Network error while contacting OpenAI. Please check your internet connection.',
        canSave: false,
      };

    case 'Timeout':
      return {
        isValid: false,
        status: 'Timeout',
        message: response.message || 'Request timed out. Please check your internet connection.',
        canSave: false,
      };

    case 'Cancelled':
      return {
        isValid: false,
        status: 'NetworkError',
        message: response.message || 'Validation was cancelled.',
        canSave: false,
      };

    case 'Error':
      return {
        isValid: false,
        status: 'NetworkError',
        message: response.message || 'An error occurred during validation. Please try again.',
        canSave: false,
      };

    default:
      return {
        isValid: false,
        status: 'NetworkError',
        message:
          response.message ||
          'Unexpected response from validation service. Please try again or check server logs.',
        canSave: false,
      };
  }
}

/**
 * Get user-friendly display text for validation status
 */
export function getStatusDisplayText(status: OpenAIValidationResult['status']): string {
  switch (status) {
    case 'Valid':
      return 'Validated ✓';
    case 'Invalid':
      return 'Invalid ✕';
    case 'RateLimited':
      return 'Rate Limited (valid key, retry later)';
    case 'PermissionDenied':
      return 'Permission Denied';
    case 'ServiceIssue':
      return 'Service Issue (retry later)';
    case 'NetworkError':
      return 'Network Error';
    case 'Timeout':
      return 'Timeout';
    default:
      return 'Unknown Status';
  }
}

/**
 * Get color/appearance for validation status
 */
export function getStatusAppearance(
  status: OpenAIValidationResult['status']
): 'success' | 'danger' | 'warning' | 'subtle' {
  switch (status) {
    case 'Valid':
      return 'success';
    case 'Invalid':
    case 'PermissionDenied':
      return 'danger';
    case 'RateLimited':
    case 'ServiceIssue':
      return 'warning';
    case 'NetworkError':
    case 'Timeout':
      return 'subtle';
    default:
      return 'subtle';
  }
}
