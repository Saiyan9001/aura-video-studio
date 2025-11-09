import apiClient from './api/apiClient';

export interface TtsProvider {
  name: string;
  type: string;
  tier: string;
  requiresApiKey: boolean;
  supportsOffline: boolean;
  description: string;
}

export interface TtsVoice {
  name: string;
  languageCode?: string;
  gender?: string;
  description?: string;
}

export interface TtsPreviewRequest {
  provider: string;
  voice: string;
  sampleText?: string;
  speed?: number;
  pitch?: number;
}

export interface TtsPreviewResponse {
  success: boolean;
  audioPath: string;
  provider: string;
  voice: string;
  text: string;
  correlationId: string;
}

export interface AudioScene {
  sceneIndex: number;
  text: string;
  startSeconds: number;
  durationSeconds: number;
}

export interface GenerateAudioRequest {
  scenes: AudioScene[];
  provider: string;
  voiceName: string;
  rate?: number;
  pitch?: number;
  pauseStyle?: string;
}

export interface AudioGenerationResult {
  sceneIndex: number;
  audioPath: string | null;
  duration: number;
  success: boolean;
  error?: string;
}

export interface GenerateAudioResponse {
  success: boolean;
  results: AudioGenerationResult[];
  failedScenes: Array<{ sceneIndex: number; error: string }>;
  totalScenes: number;
  successfulScenes: number;
  failedCount: number;
  correlationId: string;
}

export interface RegenerateAudioRequest {
  sceneIndex: number;
  text: string;
  startSeconds: number;
  durationSeconds: number;
  provider: string;
  voiceName: string;
  rate?: number;
  pitch?: number;
  pauseStyle?: string;
}

export interface RegenerateAudioResponse {
  success: boolean;
  sceneIndex: number;
  audioPath: string;
  duration: number;
  correlationId: string;
}

export class TtsService {
  async getAvailableProviders(): Promise<TtsProvider[]> {
    try {
      const response = await apiClient.get<{
        success: boolean;
        providers: TtsProvider[];
        totalCount: number;
        correlationId: string;
      }>('/api/tts/providers');

      if (!response.data.success) {
        throw new Error('Failed to fetch TTS providers');
      }

      return response.data.providers;
    } catch (error: unknown) {
      const errorObj = error instanceof Error ? error : new Error(String(error));
      console.error('Failed to fetch TTS providers:', errorObj.message);
      throw errorObj;
    }
  }

  async getVoicesForProvider(provider: string): Promise<TtsVoice[]> {
    try {
      const response = await apiClient.get<{
        success: boolean;
        provider: string;
        voices: TtsVoice[];
        count: number;
        correlationId: string;
      }>('/api/tts/voices', { params: { provider } });

      if (!response.data.success) {
        throw new Error(`Failed to fetch voices for provider ${provider}`);
      }

      return response.data.voices;
    } catch (error: unknown) {
      const errorObj = error instanceof Error ? error : new Error(String(error));
      console.error(`Failed to fetch voices for provider ${provider}:`, errorObj.message);
      throw errorObj;
    }
  }

  async generatePreview(request: TtsPreviewRequest): Promise<TtsPreviewResponse> {
    try {
      const response = await apiClient.post<TtsPreviewResponse>('/api/tts/preview', {
        provider: request.provider,
        voice: request.voice,
        sampleText: request.sampleText,
        speed: request.speed,
        pitch: request.pitch,
      });

      if (!response.data.success) {
        throw new Error('Failed to generate voice preview');
      }

      return response.data;
    } catch (error: unknown) {
      const errorObj = error instanceof Error ? error : new Error(String(error));
      console.error('Failed to generate voice preview:', errorObj.message);
      throw errorObj;
    }
  }

  async generateAudio(request: GenerateAudioRequest): Promise<GenerateAudioResponse> {
    try {
      const response = await apiClient.post<GenerateAudioResponse>('/api/audio/generate', request);

      return response.data;
    } catch (error: unknown) {
      const errorObj = error instanceof Error ? error : new Error(String(error));
      console.error('Failed to generate audio:', errorObj.message);
      throw errorObj;
    }
  }

  async regenerateAudio(request: RegenerateAudioRequest): Promise<RegenerateAudioResponse> {
    try {
      const response = await apiClient.post<RegenerateAudioResponse>(
        '/api/audio/regenerate',
        request
      );

      if (!response.data.success) {
        throw new Error('Failed to regenerate audio');
      }

      return response.data;
    } catch (error: unknown) {
      const errorObj = error instanceof Error ? error : new Error(String(error));
      console.error('Failed to regenerate audio:', errorObj.message);
      throw errorObj;
    }
  }
}

export const ttsService = new TtsService();
