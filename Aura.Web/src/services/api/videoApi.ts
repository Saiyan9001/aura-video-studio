/**
 * Video Generation API Service
 * Provides typed methods for video generation operations
 */

import { post, get, createAbortController } from './apiClient';
import type { ExtendedAxiosRequestConfig } from './apiClient';

/**
 * Video generation request interface
 */
export interface VideoGenerationRequest {
  brief: {
    topic: string;
    audience: string;
    goal: string;
    tone: string;
    language: string;
    aspect: string;
  };
  planSpec: {
    targetDuration: string;
    pacing: string;
    density: string;
    style: string;
  };
  voiceSpec: {
    voiceName: string;
    rate: number;
    pitch: number;
    pause: string;
  };
  renderSpec: {
    res: string;
    container: string;
    videoBitrateK: number;
    audioBitrateK: number;
    fps: number;
    codec: string;
    qualityLevel: string;
    enableSceneCut: boolean;
  };
}

/**
 * Video generation response interface
 */
export interface VideoGenerationResponse {
  jobId: string;
  status: string;
  stage: string;
  correlationId: string;
}

/**
 * Video status response interface
 */
export interface VideoStatus {
  id: string;
  status: 'Queued' | 'Running' | 'Done' | 'Failed' | 'Skipped' | 'Canceled';
  stage: string;
  percent: number;
  eta?: string;
  artifacts: Array<{
    name: string;
    path: string;
    type: string;
    sizeBytes: number;
    createdAt: string;
  }>;
  logs: string[];
  startedAt: string;
  finishedAt?: string;
  correlationId?: string;
  errorMessage?: string;
  phase?: string;
  progressMessage?: string;
  outputPath?: string;
  subtitlePath?: string;
}

/**
 * Progress update from SSE
 */
export interface ProgressUpdate {
  type:
    | 'job-status'
    | 'step-progress'
    | 'step-status'
    | 'job-completed'
    | 'job-failed'
    | 'job-cancelled'
    | 'warning'
    | 'error';
  data: unknown;
  eventId?: string;
}

/**
 * Generate a video
 */
export async function generateVideo(
  request: VideoGenerationRequest,
  config?: ExtendedAxiosRequestConfig
): Promise<VideoGenerationResponse> {
  return post<VideoGenerationResponse>('/api/jobs', request, config);
}

/**
 * Get video generation status
 */
export async function getVideoStatus(
  id: string,
  config?: ExtendedAxiosRequestConfig
): Promise<VideoStatus> {
  return get<VideoStatus>(`/api/jobs/${id}`, config);
}

/**
 * Stream progress updates via Server-Sent Events
 * Returns EventSource for manual management or use with useSSEConnection hook
 */
export function streamProgress(
  id: string,
  onProgress: (update: ProgressUpdate) => void
): EventSource {
  const eventSource = new EventSource(`/api/jobs/${id}/events`);

  // Handle all event types
  const eventTypes = [
    'job-status',
    'step-progress',
    'step-status',
    'job-completed',
    'job-failed',
    'job-cancelled',
    'warning',
    'error',
  ];

  eventTypes.forEach((eventType) => {
    eventSource.addEventListener(eventType, (event: MessageEvent) => {
      try {
        const data = JSON.parse(event.data);
        onProgress({
          type: eventType as ProgressUpdate['type'],
          data,
          eventId: event.lastEventId,
        });
      } catch (error) {
        console.error(`Failed to parse ${eventType} event:`, error);
      }
    });
  });

  // Handle connection errors
  eventSource.onerror = (error) => {
    console.error('SSE connection error:', error);
    onProgress({
      type: 'error',
      data: { message: 'Connection lost. Attempting to reconnect...' },
    });
  };

  return eventSource;
}

/**
 * Cancel a video generation job
 */
export async function cancelVideoGeneration(
  id: string,
  config?: ExtendedAxiosRequestConfig
): Promise<void> {
  await post<void>(`/api/jobs/${id}/cancel`, undefined, config);
}

/**
 * List all jobs
 */
export async function listJobs(
  config?: ExtendedAxiosRequestConfig
): Promise<{ jobs: VideoStatus[] }> {
  return get<{ jobs: VideoStatus[] }>('/api/jobs', config);
}

/**
 * Create an abort controller for request cancellation
 */
export { createAbortController };
