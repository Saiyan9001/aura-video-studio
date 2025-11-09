/**
 * Tests for providersApi service
 */

import { describe, it, expect, beforeEach, vi } from 'vitest';
import * as apiClient from '../apiClient';
import {
  getProviderStatuses,
  getProviderConfig,
  updateProviderConfig,
  testProviderConnection,
  validateOpenAIKey,
  validateElevenLabsKey,
  validatePlayHTKey,
  getProviderModels,
  getProviderPreferences,
  updateProviderPreferences,
  type ProviderStatus,
  type ProviderConfig,
} from '../providersApi';

// Mock the apiClient module
vi.mock('../apiClient', () => ({
  get: vi.fn(),
  post: vi.fn(),
  put: vi.fn(),
}));

describe('providersApi', () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  describe('getProviderStatuses', () => {
    it('should fetch provider statuses', async () => {
      const mockStatuses: ProviderStatus[] = [
        {
          providerId: 'openai',
          name: 'OpenAI',
          available: true,
          configured: true,
          healthy: true,
        },
        {
          providerId: 'elevenlabs',
          name: 'ElevenLabs',
          available: false,
          configured: false,
          healthy: false,
          message: 'API key not configured',
        },
      ];

      vi.mocked(apiClient.get).mockResolvedValueOnce(mockStatuses);

      const result = await getProviderStatuses();

      expect(apiClient.get).toHaveBeenCalledWith('/api/providers/status', undefined);
      expect(result).toEqual(mockStatuses);
    });
  });

  describe('getProviderConfig', () => {
    it('should fetch provider configuration', async () => {
      const mockConfig: ProviderConfig = {
        providerId: 'openai',
        enabled: true,
        apiKey: 'sk-test123',
        modelName: 'gpt-4',
      };

      vi.mocked(apiClient.get).mockResolvedValueOnce(mockConfig);

      const result = await getProviderConfig('openai');

      expect(apiClient.get).toHaveBeenCalledWith('/api/providers/openai/config', undefined);
      expect(result).toEqual(mockConfig);
    });
  });

  describe('updateProviderConfig', () => {
    it('should update provider configuration', async () => {
      const updateData: Partial<ProviderConfig> = {
        enabled: true,
        apiKey: 'sk-new-key',
      };

      const mockResponse: ProviderConfig = {
        providerId: 'openai',
        enabled: true,
        apiKey: 'sk-new-key',
        modelName: 'gpt-4',
      };

      vi.mocked(apiClient.put).mockResolvedValueOnce(mockResponse);

      const result = await updateProviderConfig('openai', updateData);

      expect(apiClient.put).toHaveBeenCalledWith(
        '/api/providers/openai/config',
        updateData,
        undefined
      );
      expect(result).toEqual(mockResponse);
    });
  });

  describe('testProviderConnection', () => {
    it('should test provider connection successfully', async () => {
      const mockResponse = {
        success: true,
        message: 'Connection successful',
      };

      vi.mocked(apiClient.post).mockResolvedValueOnce(mockResponse);

      const result = await testProviderConnection('openai', { apiKey: 'sk-test' });

      expect(apiClient.post).toHaveBeenCalledWith(
        '/api/providers/test-connection',
        { providerId: 'openai', apiKey: 'sk-test' },
        undefined
      );
      expect(result).toEqual(mockResponse);
    });

    it('should handle failed connection test', async () => {
      const mockResponse = {
        success: false,
        message: 'Invalid API key',
      };

      vi.mocked(apiClient.post).mockResolvedValueOnce(mockResponse);

      const result = await testProviderConnection('openai', { apiKey: 'sk-invalid' });

      expect(result.success).toBe(false);
      expect(result.message).toBe('Invalid API key');
    });
  });

  describe('validateOpenAIKey', () => {
    it('should validate OpenAI API key', async () => {
      const mockResponse = {
        isValid: true,
        message: 'API key is valid',
      };

      vi.mocked(apiClient.post).mockResolvedValueOnce(mockResponse);

      const result = await validateOpenAIKey('sk-test123');

      expect(apiClient.post).toHaveBeenCalledWith(
        '/api/providers/openai/validate',
        { apiKey: 'sk-test123' },
        undefined
      );
      expect(result.isValid).toBe(true);
    });

    it('should handle invalid OpenAI key', async () => {
      const mockResponse = {
        isValid: false,
        message: 'Invalid API key format',
      };

      vi.mocked(apiClient.post).mockResolvedValueOnce(mockResponse);

      const result = await validateOpenAIKey('invalid-key');

      expect(result.isValid).toBe(false);
    });
  });

  describe('validateElevenLabsKey', () => {
    it('should validate ElevenLabs API key', async () => {
      const mockResponse = {
        isValid: true,
        message: 'API key is valid',
      };

      vi.mocked(apiClient.post).mockResolvedValueOnce(mockResponse);

      const result = await validateElevenLabsKey('el-test123');

      expect(apiClient.post).toHaveBeenCalledWith(
        '/api/providers/elevenlabs/validate',
        { apiKey: 'el-test123' },
        undefined
      );
      expect(result.isValid).toBe(true);
    });
  });

  describe('validatePlayHTKey', () => {
    it('should validate PlayHT API key with user ID', async () => {
      const mockResponse = {
        isValid: true,
        message: 'API key is valid',
      };

      vi.mocked(apiClient.post).mockResolvedValueOnce(mockResponse);

      const result = await validatePlayHTKey('ph-test123', 'user-456');

      expect(apiClient.post).toHaveBeenCalledWith(
        '/api/providers/playht/validate',
        { apiKey: 'ph-test123', userId: 'user-456' },
        undefined
      );
      expect(result.isValid).toBe(true);
    });
  });

  describe('getProviderModels', () => {
    it('should fetch available models for provider', async () => {
      const mockResponse = {
        models: [
          {
            id: 'gpt-4',
            name: 'GPT-4',
            description: 'Most capable model',
            contextLength: 8192,
          },
          {
            id: 'gpt-3.5-turbo',
            name: 'GPT-3.5 Turbo',
            description: 'Fast and affordable',
            contextLength: 4096,
          },
        ],
      };

      vi.mocked(apiClient.get).mockResolvedValueOnce(mockResponse);

      const result = await getProviderModels('openai');

      expect(apiClient.get).toHaveBeenCalledWith('/api/providers/openai/models', undefined);
      expect(result.models).toHaveLength(2);
      expect(result.models[0].id).toBe('gpt-4');
    });
  });

  describe('getProviderPreferences', () => {
    it('should fetch provider preferences', async () => {
      const mockResponse = {
        selectedProfile: 'Pro',
        customSelections: {
          script: 'OpenAI',
          tts: 'ElevenLabs',
        },
      };

      vi.mocked(apiClient.get).mockResolvedValueOnce(mockResponse);

      const result = await getProviderPreferences();

      expect(apiClient.get).toHaveBeenCalledWith('/api/providers/preferences', undefined);
      expect(result.selectedProfile).toBe('Pro');
    });
  });

  describe('updateProviderPreferences', () => {
    it('should update provider preferences', async () => {
      const preferences = {
        selectedProfile: 'Custom',
        customSelections: {
          script: 'Ollama',
          tts: 'Piper',
        },
      };

      vi.mocked(apiClient.post).mockResolvedValueOnce(undefined);

      await updateProviderPreferences(preferences);

      expect(apiClient.post).toHaveBeenCalledWith(
        '/api/providers/preferences',
        preferences,
        undefined
      );
    });
  });
});
