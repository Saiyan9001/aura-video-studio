import { describe, it, expect, vi, beforeEach } from 'vitest';
import apiClient from '../api/apiClient';
import { TtsService } from '../ttsService';

vi.mock('../api/apiClient');

describe('TtsService', () => {
  let ttsService: TtsService;

  beforeEach(() => {
    ttsService = new TtsService();
    vi.clearAllMocks();
  });

  describe('getAvailableProviders', () => {
    it('should fetch and return available TTS providers', async () => {
      const mockProviders = [
        {
          name: 'ElevenLabs',
          type: 'Cloud',
          tier: 'Pro',
          requiresApiKey: true,
          supportsOffline: false,
          description: 'Premium TTS service',
        },
        {
          name: 'EdgeTTS',
          type: 'Cloud-Free',
          tier: 'Free',
          requiresApiKey: false,
          supportsOffline: false,
          description: 'Free Microsoft Edge TTS',
        },
      ];

      vi.mocked(apiClient.get).mockResolvedValue({
        data: {
          success: true,
          providers: mockProviders,
          totalCount: 2,
          correlationId: 'test-123',
        },
      } as never);

      const result = await ttsService.getAvailableProviders();

      expect(result).toEqual(mockProviders);
      expect(apiClient.get).toHaveBeenCalledWith('/api/tts/providers');
    });

    it('should throw error if request fails', async () => {
      vi.mocked(apiClient.get).mockRejectedValue(new Error('Network error'));

      await expect(ttsService.getAvailableProviders()).rejects.toThrow('Network error');
    });
  });

  describe('getVoicesForProvider', () => {
    it('should fetch and return voices for a specific provider', async () => {
      const mockVoices = [
        { name: 'Adam', gender: 'male', languageCode: 'en-US' },
        { name: 'Nicole', gender: 'female', languageCode: 'en-US' },
      ];

      vi.mocked(apiClient.get).mockResolvedValue({
        data: {
          success: true,
          provider: 'ElevenLabs',
          voices: mockVoices,
          count: 2,
          correlationId: 'test-123',
        },
      } as never);

      const result = await ttsService.getVoicesForProvider('ElevenLabs');

      expect(result).toEqual(mockVoices);
      expect(apiClient.get).toHaveBeenCalledWith('/api/tts/voices', {
        params: { provider: 'ElevenLabs' },
      });
    });
  });

  describe('generateAudio', () => {
    it('should generate audio for multiple scenes', async () => {
      const mockRequest = {
        scenes: [
          { sceneIndex: 0, text: 'Hello world', startSeconds: 0, durationSeconds: 2 },
          { sceneIndex: 1, text: 'How are you', startSeconds: 2, durationSeconds: 2 },
        ],
        provider: 'ElevenLabs',
        voiceName: 'Adam',
        rate: 1.0,
        pitch: 0.0,
        pauseStyle: 'Natural',
      };

      const mockResponse = {
        success: true,
        results: [
          { sceneIndex: 0, audioPath: '/audio/scene0.wav', duration: 2, success: true },
          { sceneIndex: 1, audioPath: '/audio/scene1.wav', duration: 2, success: true },
        ],
        failedScenes: [],
        totalScenes: 2,
        successfulScenes: 2,
        failedCount: 0,
        correlationId: 'test-123',
      };

      vi.mocked(apiClient.post).mockResolvedValue({
        data: mockResponse,
      } as never);

      const result = await ttsService.generateAudio(mockRequest);

      expect(result).toEqual(mockResponse);
      expect(apiClient.post).toHaveBeenCalledWith('/api/audio/generate', mockRequest);
    });
  });
});
