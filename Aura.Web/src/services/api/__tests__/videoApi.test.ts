/**
 * Unit tests for Video API methods
 */

import { describe, it, expect, vi, beforeEach } from 'vitest';
import * as apiClient from '../apiClient';
import * as videoApi from '../videoApi';
import type { VideoGenerationRequest, VideoGenerationResponse, VideoStatus } from '../videoApi';

// Mock apiClient methods
vi.mock('../apiClient', async () => {
  const actual = await vi.importActual('../apiClient');
  return {
    ...actual,
    post: vi.fn(),
    get: vi.fn(),
    createAbortController: actual.createAbortController,
    resetCircuitBreaker: vi.fn(),
    clearDeduplicationCache: vi.fn(),
  };
});

describe('videoApi', () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  describe('generateVideo', () => {
    it('should successfully create a video generation job', async () => {
      const request: VideoGenerationRequest = {
        brief: {
          topic: 'Test Topic',
          audience: 'General',
          goal: 'Educate',
          tone: 'Professional',
          language: 'en',
          aspect: 'Widescreen16x9',
        },
        planSpec: {
          targetDuration: '00:01:00',
          pacing: 'Conversational',
          density: 'Balanced',
          style: 'Modern',
        },
        voiceSpec: {
          voiceName: 'David',
          rate: 1.0,
          pitch: 1.0,
          pause: 'Natural',
        },
        renderSpec: {
          res: '1080p',
          container: 'mp4',
          videoBitrateK: 5000,
          audioBitrateK: 192,
          fps: 30,
          codec: 'h264',
          qualityLevel: 'High',
          enableSceneCut: true,
        },
      };

      const response: VideoGenerationResponse = {
        jobId: 'job-123',
        status: 'Queued',
        stage: 'Initializing',
        correlationId: 'test-correlation-id',
      };

      vi.mocked(apiClient.post).mockResolvedValue(response);

      const result = await videoApi.generateVideo(request, {
        _skipRetry: true,
        _skipDeduplication: true,
      });

      expect(result).toEqual(response);
      expect(result.jobId).toBe('job-123');
      expect(apiClient.post).toHaveBeenCalledWith('/api/jobs', request, expect.anything());
    });

    it('should handle validation errors', async () => {
      const request: VideoGenerationRequest = {
        brief: {
          topic: '',
          audience: 'General',
          goal: 'Educate',
          tone: 'Professional',
          language: 'en',
          aspect: 'Widescreen16x9',
        },
        planSpec: {
          targetDuration: '00:01:00',
          pacing: 'Conversational',
          density: 'Balanced',
          style: 'Modern',
        },
        voiceSpec: {
          voiceName: 'David',
          rate: 1.0,
          pitch: 1.0,
          pause: 'Natural',
        },
        renderSpec: {
          res: '1080p',
          container: 'mp4',
          videoBitrateK: 5000,
          audioBitrateK: 192,
          fps: 30,
          codec: 'h264',
          qualityLevel: 'High',
          enableSceneCut: true,
        },
      };

      vi.mocked(apiClient.post).mockRejectedValue(new Error('Invalid Request'));

      await expect(
        videoApi.generateVideo(request, { _skipRetry: true, _skipDeduplication: true })
      ).rejects.toThrow();
    });
  });

  describe('getVideoStatus', () => {
    it('should fetch video generation status', async () => {
      const status: VideoStatus = {
        id: 'job-123',
        status: 'Running',
        stage: 'Script Generation',
        percent: 25,
        artifacts: [],
        logs: [],
        startedAt: '2024-01-01T00:00:00Z',
      };

      vi.mocked(apiClient.get).mockResolvedValue(status);

      const result = await videoApi.getVideoStatus('job-123', { _skipRetry: true });

      expect(result).toEqual(status);
      expect(result.status).toBe('Running');
      expect(result.percent).toBe(25);
      expect(apiClient.get).toHaveBeenCalledWith('/api/jobs/job-123', expect.anything());
    });

    it('should handle 404 for non-existent job', async () => {
      vi.mocked(apiClient.get).mockRejectedValue(new Error('Not Found'));

      await expect(videoApi.getVideoStatus('non-existent', { _skipRetry: true })).rejects.toThrow();
    });
  });

  describe('cancelVideoGeneration', () => {
    it('should successfully cancel a job', async () => {
      vi.mocked(apiClient.post).mockResolvedValue(undefined);

      await expect(
        videoApi.cancelVideoGeneration('job-123', {
          _skipRetry: true,
          _skipDeduplication: true,
        })
      ).resolves.not.toThrow();

      expect(apiClient.post).toHaveBeenCalledWith(
        '/api/jobs/job-123/cancel',
        undefined,
        expect.anything()
      );
    });

    it('should handle errors when cancelling', async () => {
      vi.mocked(apiClient.post).mockRejectedValue(new Error('Internal Server Error'));

      await expect(
        videoApi.cancelVideoGeneration('job-123', { _skipRetry: true, _skipDeduplication: true })
      ).rejects.toThrow();
    });
  });

  describe('listJobs', () => {
    it('should list all jobs', async () => {
      const jobs: VideoStatus[] = [
        {
          id: 'job-1',
          status: 'Done',
          stage: 'Completed',
          percent: 100,
          artifacts: [],
          logs: [],
          startedAt: '2024-01-01T00:00:00Z',
          finishedAt: '2024-01-01T00:05:00Z',
        },
        {
          id: 'job-2',
          status: 'Running',
          stage: 'Rendering',
          percent: 75,
          artifacts: [],
          logs: [],
          startedAt: '2024-01-01T00:10:00Z',
        },
      ];

      vi.mocked(apiClient.get).mockResolvedValue({ jobs });

      const result = await videoApi.listJobs({ _skipRetry: true });

      expect(result.jobs).toHaveLength(2);
      expect(result.jobs[0].id).toBe('job-1');
      expect(result.jobs[1].id).toBe('job-2');
      expect(apiClient.get).toHaveBeenCalledWith('/api/jobs', expect.anything());
    });
  });

  describe('streamProgress', () => {
    it('should create EventSource for progress updates', () => {
      const onProgress = vi.fn();

      // Mock EventSource
      const mockEventSource = {
        addEventListener: vi.fn(),
        removeEventListener: vi.fn(),
        close: vi.fn(),
        onerror: null,
      } as unknown as EventSource;

      vi.stubGlobal(
        'EventSource',
        vi.fn(() => mockEventSource)
      );

      const eventSource = videoApi.streamProgress('job-123', onProgress);

      expect(eventSource).toBeDefined();
      expect(mockEventSource.addEventListener).toHaveBeenCalled();
    });
  });
});
