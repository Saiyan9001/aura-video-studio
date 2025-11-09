/**
 * Tests for VisualsClient API
 */

import { describe, it, expect, vi, beforeEach } from 'vitest';
import type { TypedApiClient } from '../typedClient';
import { VisualsClient } from '../visualsClient';

describe('VisualsClient', () => {
  let mockClient: TypedApiClient;
  let visualsClient: VisualsClient;

  beforeEach(() => {
    mockClient = {
      get: vi.fn(),
      post: vi.fn(),
    } as unknown as TypedApiClient;

    visualsClient = new VisualsClient(mockClient);
  });

  describe('getProviders', () => {
    it('should fetch available providers', async () => {
      const mockResponse = {
        providers: [
          {
            name: 'DallE3',
            isAvailable: true,
            requiresApiKey: true,
            capabilities: {
              providerName: 'DallE3',
              supportsNegativePrompts: false,
              supportsBatchGeneration: false,
              supportsStylePresets: true,
              supportedAspectRatios: ['1:1', '16:9'],
              supportedStyles: ['vivid', 'natural'],
              maxWidth: 1024,
              maxHeight: 1024,
              isLocal: false,
              isFree: false,
              costPerImage: 0.04,
              tier: 'Premium',
            },
          },
        ],
        timestamp: '2025-01-01T00:00:00Z',
      };

      (mockClient.get as ReturnType<typeof vi.fn>).mockResolvedValue(mockResponse);

      const result = await visualsClient.getProviders();

      expect(mockClient.get).toHaveBeenCalledWith('/api/visuals/providers');
      expect(result).toEqual(mockResponse);
      expect(result.providers).toHaveLength(1);
      expect(result.providers[0].name).toBe('DallE3');
    });
  });

  describe('generateImage', () => {
    it('should generate a single image', async () => {
      const mockResponse = {
        imagePath: '/images/generated/test.png',
        provider: 'DallE3',
        prompt: 'A beautiful sunset',
        generatedAt: '2025-01-01T00:00:00Z',
      };

      (mockClient.post as ReturnType<typeof vi.fn>).mockResolvedValue(mockResponse);

      const result = await visualsClient.generateImage({
        prompt: 'A beautiful sunset',
        width: 1024,
        height: 1024,
      });

      expect(mockClient.post).toHaveBeenCalledWith('/api/visuals/generate', {
        prompt: 'A beautiful sunset',
        width: 1024,
        height: 1024,
      });
      expect(result).toEqual(mockResponse);
      expect(result.imagePath).toBe('/images/generated/test.png');
    });

    it('should handle generation errors', async () => {
      const mockError = new Error('Generation failed');
      (mockClient.post as ReturnType<typeof vi.fn>).mockRejectedValue(mockError);

      await expect(
        visualsClient.generateImage({
          prompt: 'A beautiful sunset',
        })
      ).rejects.toThrow('Generation failed');
    });
  });

  describe('batchGenerate', () => {
    it('should call batch endpoint first', async () => {
      const mockResponse = {
        images: [
          {
            imagePath: '/images/generated/1.png',
            prompt: 'Scene 1',
            generatedAt: '2025-01-01T00:00:00Z',
          },
          {
            imagePath: '/images/generated/2.png',
            prompt: 'Scene 2',
            generatedAt: '2025-01-01T00:00:00Z',
          },
        ],
        totalGenerated: 2,
        failedCount: 0,
        provider: 'DallE3',
      };

      (mockClient.post as ReturnType<typeof vi.fn>).mockResolvedValue(mockResponse);

      const result = await visualsClient.batchGenerate({
        prompts: ['Scene 1', 'Scene 2'],
        style: 'photorealistic',
      });

      expect(mockClient.post).toHaveBeenCalledWith('/api/visuals/batch', {
        prompts: ['Scene 1', 'Scene 2'],
        style: 'photorealistic',
      });
      expect(result.images).toHaveLength(2);
      expect(result.totalGenerated).toBe(2);
      expect(result.failedCount).toBe(0);
    });

    it('should report progress during batch generation', async () => {
      const mockResponse = {
        images: [
          {
            imagePath: '/images/generated/1.png',
            prompt: 'Scene 1',
            generatedAt: '2025-01-01T00:00:00Z',
          },
        ],
        totalGenerated: 1,
        failedCount: 0,
        provider: 'DallE3',
      };

      (mockClient.post as ReturnType<typeof vi.fn>).mockResolvedValue(mockResponse);

      const progressCallback = vi.fn();
      await visualsClient.batchGenerate(
        {
          prompts: ['Scene 1'],
        },
        progressCallback
      );

      expect(progressCallback).toHaveBeenCalledWith({
        completedCount: 1,
        totalCount: 1,
        currentPrompt: '',
        successCount: 1,
        progressPercentage: 100,
      });
    });

    it('should fallback to sequential generation on batch error', async () => {
      (mockClient.post as ReturnType<typeof vi.fn>)
        .mockRejectedValueOnce(new Error('Batch endpoint not available'))
        .mockResolvedValueOnce({
          imagePath: '/images/generated/1.png',
          provider: 'DallE3',
          prompt: 'Scene 1',
          generatedAt: '2025-01-01T00:00:00Z',
        })
        .mockResolvedValueOnce({
          imagePath: '/images/generated/2.png',
          provider: 'DallE3',
          prompt: 'Scene 2',
          generatedAt: '2025-01-01T00:00:00Z',
        });

      const result = await visualsClient.batchGenerate({
        prompts: ['Scene 1', 'Scene 2'],
      });

      expect(mockClient.post).toHaveBeenCalledTimes(3);
      expect(result.images).toHaveLength(2);
      expect(result.totalGenerated).toBe(2);
      expect(result.failedCount).toBe(0);
    });
  });

  describe('getStyles', () => {
    it('should fetch available styles', async () => {
      const mockResponse = {
        allStyles: ['photorealistic', 'artistic', 'cinematic'],
        stylesByProvider: {
          DallE3: ['vivid', 'natural'],
          StabilityAI: ['photorealistic', 'artistic'],
        },
      };

      (mockClient.get as ReturnType<typeof vi.fn>).mockResolvedValue(mockResponse);

      const result = await visualsClient.getStyles();

      expect(mockClient.get).toHaveBeenCalledWith('/api/visuals/styles');
      expect(result.allStyles).toHaveLength(3);
      expect(result.stylesByProvider).toHaveProperty('DallE3');
    });
  });

  describe('validatePrompt', () => {
    it('should validate a safe prompt', async () => {
      const mockResponse = {
        isValid: true,
        issues: [],
        prompt: 'A beautiful landscape',
        characterCount: 21,
      };

      (mockClient.post as ReturnType<typeof vi.fn>).mockResolvedValue(mockResponse);

      const result = await visualsClient.validatePrompt({
        prompt: 'A beautiful landscape',
      });

      expect(mockClient.post).toHaveBeenCalledWith('/api/visuals/validate', {
        prompt: 'A beautiful landscape',
      });
      expect(result.isValid).toBe(true);
      expect(result.issues).toHaveLength(0);
    });

    it('should detect unsafe content', async () => {
      const mockResponse = {
        isValid: false,
        issues: ['Prompt contains potentially unsafe content'],
        prompt: 'unsafe content',
        characterCount: 14,
      };

      (mockClient.post as ReturnType<typeof vi.fn>).mockResolvedValue(mockResponse);

      const result = await visualsClient.validatePrompt({
        prompt: 'unsafe content',
      });

      expect(result.isValid).toBe(false);
      expect(result.issues).toContain('Prompt contains potentially unsafe content');
    });
  });
});
