/**
 * Script API Tests
 */

import { describe, it, expect, vi, beforeEach } from 'vitest';
import * as apiClient from '../apiClient';
import {
  generateScript,
  getScript,
  updateScene,
  regenerateScript,
  regenerateScene,
  listProviders,
  exportScript,
  type GenerateScriptRequest,
  type UpdateSceneRequest,
  type RegenerateScriptRequest,
} from '../scriptApi';

vi.mock('../apiClient');

describe('Script API', () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  describe('generateScript', () => {
    it('should call POST with correct endpoint and data', async () => {
      const mockResponse = {
        scriptId: 'script-123',
        title: 'Test Script',
        scenes: [],
        totalDurationSeconds: 60,
        metadata: {
          generatedAt: '2024-01-01T00:00:00Z',
          providerName: 'RuleBased',
          modelUsed: 'template-v1',
          tokensUsed: 100,
          estimatedCost: 0,
          tier: 'Free',
          generationTimeSeconds: 1.5,
        },
        correlationId: 'corr-123',
      };

      vi.mocked(apiClient.post).mockResolvedValue(mockResponse);

      const request: GenerateScriptRequest = {
        topic: 'Test Topic',
        targetDurationSeconds: 60,
      };

      const result = await generateScript(request);

      expect(apiClient.post).toHaveBeenCalledWith('/api/scripts/generate', request, undefined);
      expect(result).toEqual(mockResponse);
    });
  });

  describe('getScript', () => {
    it('should call GET with correct endpoint', async () => {
      const mockResponse = {
        scriptId: 'script-123',
        title: 'Test Script',
        scenes: [],
        totalDurationSeconds: 60,
        metadata: {
          generatedAt: '2024-01-01T00:00:00Z',
          providerName: 'RuleBased',
          modelUsed: 'template-v1',
          tokensUsed: 100,
          estimatedCost: 0,
          tier: 'Free',
          generationTimeSeconds: 1.5,
        },
        correlationId: 'corr-123',
      };

      vi.mocked(apiClient.get).mockResolvedValue(mockResponse);

      const result = await getScript('script-123');

      expect(apiClient.get).toHaveBeenCalledWith('/api/scripts/script-123', undefined);
      expect(result).toEqual(mockResponse);
    });
  });

  describe('updateScene', () => {
    it('should call PUT with correct endpoint and data', async () => {
      const mockResponse = {
        scriptId: 'script-123',
        title: 'Test Script',
        scenes: [],
        totalDurationSeconds: 60,
        metadata: {
          generatedAt: '2024-01-01T00:00:00Z',
          providerName: 'RuleBased',
          modelUsed: 'template-v1',
          tokensUsed: 100,
          estimatedCost: 0,
          tier: 'Free',
          generationTimeSeconds: 1.5,
        },
        correlationId: 'corr-123',
      };

      vi.mocked(apiClient.put).mockResolvedValue(mockResponse);

      const request: UpdateSceneRequest = {
        narration: 'Updated narration text',
      };

      const result = await updateScene('script-123', 1, request);

      expect(apiClient.put).toHaveBeenCalledWith(
        '/api/scripts/script-123/scenes/1',
        request,
        undefined
      );
      expect(result).toEqual(mockResponse);
    });
  });

  describe('regenerateScript', () => {
    it('should call POST with correct endpoint and data', async () => {
      const mockResponse = {
        scriptId: 'script-123',
        title: 'Test Script',
        scenes: [],
        totalDurationSeconds: 60,
        metadata: {
          generatedAt: '2024-01-01T00:00:00Z',
          providerName: 'OpenAI',
          modelUsed: 'gpt-4',
          tokensUsed: 200,
          estimatedCost: 0.01,
          tier: 'Pro',
          generationTimeSeconds: 2.5,
        },
        correlationId: 'corr-123',
      };

      vi.mocked(apiClient.post).mockResolvedValue(mockResponse);

      const request: RegenerateScriptRequest = {
        preferredProvider: 'OpenAI',
      };

      const result = await regenerateScript('script-123', request);

      expect(apiClient.post).toHaveBeenCalledWith(
        '/api/scripts/script-123/regenerate',
        request,
        undefined
      );
      expect(result).toEqual(mockResponse);
    });
  });

  describe('regenerateScene', () => {
    it('should call POST with correct endpoint', async () => {
      const mockResponse = {
        scriptId: 'script-123',
        title: 'Test Script',
        scenes: [],
        totalDurationSeconds: 60,
        metadata: {
          generatedAt: '2024-01-01T00:00:00Z',
          providerName: 'RuleBased',
          modelUsed: 'template-v1',
          tokensUsed: 100,
          estimatedCost: 0,
          tier: 'Free',
          generationTimeSeconds: 1.5,
        },
        correlationId: 'corr-123',
      };

      vi.mocked(apiClient.post).mockResolvedValue(mockResponse);

      const result = await regenerateScene('script-123', 1);

      expect(apiClient.post).toHaveBeenCalledWith(
        '/api/scripts/script-123/scenes/1/regenerate',
        {},
        undefined
      );
      expect(result).toEqual(mockResponse);
    });
  });

  describe('listProviders', () => {
    it('should call GET with correct endpoint', async () => {
      const mockResponse = {
        providers: [
          {
            name: 'RuleBased',
            tier: 'Free',
            isAvailable: true,
            requiresInternet: false,
            requiresApiKey: false,
            capabilities: ['offline', 'deterministic'],
            defaultModel: 'template-v1',
            estimatedCostPer1KTokens: 0,
            availableModels: ['template-v1'],
          },
        ],
        correlationId: 'corr-123',
      };

      vi.mocked(apiClient.get).mockResolvedValue(mockResponse);

      const result = await listProviders();

      expect(apiClient.get).toHaveBeenCalledWith('/api/scripts/providers', undefined);
      expect(result).toEqual(mockResponse);
    });
  });

  describe('exportScript', () => {
    it('should call GET with correct endpoint and responseType', async () => {
      const mockBlob = new Blob(['test content'], { type: 'text/plain' });
      vi.mocked(apiClient.get).mockResolvedValue(mockBlob);

      const result = await exportScript('script-123', 'text');

      expect(apiClient.get).toHaveBeenCalledWith('/api/scripts/script-123/export?format=text', {
        responseType: 'blob',
      });
      expect(result).toEqual(mockBlob);
    });

    it('should default to text format', async () => {
      const mockBlob = new Blob(['test content'], { type: 'text/plain' });
      vi.mocked(apiClient.get).mockResolvedValue(mockBlob);

      await exportScript('script-123');

      expect(apiClient.get).toHaveBeenCalledWith('/api/scripts/script-123/export?format=text', {
        responseType: 'blob',
      });
    });
  });
});
