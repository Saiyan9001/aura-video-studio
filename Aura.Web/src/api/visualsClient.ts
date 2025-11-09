/**
 * Visuals API Client
 *
 * Provides typed API methods for image generation and visual provider management
 */

import { typedApiClient, TypedApiClient } from './typedClient';

export interface VisualProvider {
  name: string;
  isAvailable: boolean;
  requiresApiKey: boolean;
  capabilities: VisualProviderCapabilities;
}

export interface VisualProviderCapabilities {
  providerName: string;
  supportsNegativePrompts: boolean;
  supportsBatchGeneration: boolean;
  supportsStylePresets: boolean;
  supportedAspectRatios: string[];
  supportedStyles: string[];
  maxWidth: number;
  maxHeight: number;
  isLocal: boolean;
  isFree: boolean;
  costPerImage: number;
  tier: string;
}

export interface GenerateImageRequest {
  prompt: string;
  width?: number;
  height?: number;
  style?: string;
  aspectRatio?: string;
  quality?: number;
  negativePrompts?: string[];
}

export interface GenerateImageResponse {
  imagePath: string;
  provider: string;
  prompt: string;
  generatedAt: string;
}

export interface BatchGenerateRequest {
  prompts: string[];
  width?: number;
  height?: number;
  style?: string;
  aspectRatio?: string;
  quality?: number;
  negativePrompts?: string[];
}

export interface BatchGenerateProgress {
  completedCount: number;
  totalCount: number;
  currentPrompt: string;
  successCount: number;
  progressPercentage: number;
}

export interface BatchGenerateResponse {
  images: GeneratedImage[];
  totalGenerated: number;
  failedCount: number;
  provider: string;
}

export interface GeneratedImage {
  sceneId?: string;
  imagePath: string;
  prompt: string;
  generatedAt: string;
  quality?: number;
  clipScore?: number;
}

export interface ValidatePromptRequest {
  prompt: string;
}

export interface ValidatePromptResponse {
  isValid: boolean;
  issues: string[];
  prompt: string;
  characterCount: number;
}

export interface StylesResponse {
  allStyles: string[];
  stylesByProvider: Record<string, string[]>;
}

export interface ProvidersResponse {
  providers: VisualProvider[];
  timestamp: string;
}

/**
 * API client for visual generation operations
 */
export class VisualsClient {
  private client: TypedApiClient;

  constructor(client?: TypedApiClient) {
    this.client = client || typedApiClient;
  }

  /**
   * Get all available visual providers
   */
  async getProviders(): Promise<ProvidersResponse> {
    return this.client.get<ProvidersResponse>('/api/visuals/providers');
  }

  /**
   * Generate a single image from a prompt
   */
  async generateImage(request: GenerateImageRequest): Promise<GenerateImageResponse> {
    return this.client.post<GenerateImageResponse>('/api/visuals/generate', request);
  }

  /**
   * Generate multiple images in batch
   */
  async batchGenerate(
    request: BatchGenerateRequest,
    onProgress?: (progress: BatchGenerateProgress) => void
  ): Promise<BatchGenerateResponse> {
    const { prompts, ...options } = request;

    try {
      const response = await this.client.post<BatchGenerateResponse>('/api/visuals/batch', {
        prompts,
        ...options,
      });

      if (onProgress) {
        onProgress({
          completedCount: response.images.length,
          totalCount: prompts.length,
          currentPrompt: '',
          successCount: response.images.length,
          progressPercentage: 100,
        });
      }

      return response;
    } catch (error) {
      console.error('Batch generation error, falling back to sequential:', error);

      const images: GeneratedImage[] = [];
      let failedCount = 0;
      let usedProvider = '';

      for (let i = 0; i < prompts.length; i++) {
        try {
          const result = await this.generateImage({
            prompt: prompts[i],
            ...options,
          });

          images.push({
            imagePath: result.imagePath,
            prompt: result.prompt,
            generatedAt: result.generatedAt,
          });

          usedProvider = result.provider;

          if (onProgress) {
            onProgress({
              completedCount: i + 1,
              totalCount: prompts.length,
              currentPrompt: prompts[i],
              successCount: images.length,
              progressPercentage: ((i + 1) / prompts.length) * 100,
            });
          }
        } catch (err) {
          console.error(`Failed to generate image ${i + 1}/${prompts.length}:`, err);
          failedCount++;

          if (onProgress) {
            onProgress({
              completedCount: i + 1,
              totalCount: prompts.length,
              currentPrompt: prompts[i],
              successCount: images.length,
              progressPercentage: ((i + 1) / prompts.length) * 100,
            });
          }
        }
      }

      return {
        images,
        totalGenerated: images.length,
        failedCount,
        provider: usedProvider,
      };
    }
  }

  /**
   * Get available visual styles
   */
  async getStyles(): Promise<StylesResponse> {
    return this.client.get<StylesResponse>('/api/visuals/styles');
  }

  /**
   * Validate a prompt for safety and compatibility
   */
  async validatePrompt(request: ValidatePromptRequest): Promise<ValidatePromptResponse> {
    return this.client.post<ValidatePromptResponse>('/api/visuals/validate', request);
  }
}

// Singleton instance
let visualsClientInstance: VisualsClient | null = null;

/**
 * Get the singleton instance of VisualsClient
 */
export function getVisualsClient(): VisualsClient {
  if (!visualsClientInstance) {
    visualsClientInstance = new VisualsClient();
  }
  return visualsClientInstance;
}
