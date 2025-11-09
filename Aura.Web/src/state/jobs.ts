import { create } from 'zustand';
import { apiUrl } from '../config/api';
import { post, get as apiGet, postWithTimeout } from '../services/api/apiClient';
import {
  SseClient,
  createSseClient,
  type JobStatusEvent,
  type StepProgressEvent,
  type JobCompletedEvent,
  type JobFailedEvent,
  type JobCancelledEvent,
  type SseConnectionState,
} from '../services/api/sseClient';
import { loggingService as logger } from '../services/loggingService';

export interface Job {
  id: string;
  stage: string;
  status: 'Queued' | 'Running' | 'Done' | 'Failed' | 'Skipped' | 'Canceled';
  percent: number;
  eta?: string;
  artifacts: JobArtifact[];
  logs: string[];
  startedAt: string;
  finishedAt?: string;
  correlationId?: string;
  errorMessage?: string;
  failureDetails?: JobFailure;
  phase?: string; // Current phase: plan, tts, visuals, compose, render
  progressMessage?: string; // Latest progress message
  outputPath?: string; // Primary output video path
  subtitlePath?: string; // Subtitle file path
  substageDetail?: string; // Detailed substage info (e.g., "Synthesizing scene 3 of 5")
  currentItem?: number; // Current item being processed
  totalItems?: number; // Total items to process
  elapsedTime?: string; // Elapsed time in format hh:mm:ss
  estimatedTimeRemaining?: string; // Estimated time remaining in format hh:mm:ss
}

export interface JobFailure {
  stage: string;
  message: string;
  correlationId: string;
  stderrSnippet?: string;
  installLogSnippet?: string;
  logPath?: string;
  suggestedActions: string[];
  errorCode?: string;
  failedAt: string;
}

export interface JobArtifact {
  name: string;
  path: string;
  type: string;
  sizeBytes: number;
  createdAt: string;
}

interface JobsState {
  // Active job being displayed
  activeJob: Job | null;

  // All jobs list
  jobs: Job[];

  // Loading states
  loading: boolean;
  streaming: boolean; // Tracks if SSE is active

  // SSE connection state
  connectionState: SseConnectionState | null;

  // Actions
  createJob: (
    brief: Record<string, unknown>,
    planSpec: Record<string, unknown>,
    voiceSpec: Record<string, unknown>,
    renderSpec: Record<string, unknown>
  ) => Promise<string>;
  getJob: (jobId: string) => Promise<void>;
  getFailureDetails: (jobId: string) => Promise<JobFailure | null>;
  listJobs: () => Promise<void>;
  cancelJob: (jobId: string) => Promise<void>;
  setActiveJob: (job: Job | null) => void;
  startStreaming: (jobId: string) => void;
  stopStreaming: () => void;
  updateJobFromSse: (updates: Partial<Job>) => void;
}

let sseClient: SseClient | null = null;

export const useJobsStore = create<JobsState>((set, get) => ({
  activeJob: null,
  jobs: [],
  loading: false,
  streaming: false,
  connectionState: null,

  createJob: async (brief, planSpec, voiceSpec, renderSpec) => {
    set({ loading: true });
    try {
      logger.info('Creating job', 'jobsStore', 'createJob', {
        topic: brief.topic,
        duration: planSpec.targetDuration,
      });

      // Use apiClient with extended timeout for video generation (5 minutes)
      const data = await postWithTimeout<{
        jobId: string;
        status: string;
        stage: string;
        correlationId: string;
      }>(
        '/api/jobs',
        {
          brief,
          planSpec,
          voiceSpec,
          renderSpec,
        },
        300000 // 5 minute timeout for job creation
      );

      const jobId = data.jobId;

      logger.info('Job created successfully', 'jobsStore', 'createJob', { jobId });

      // Start SSE streaming for updates
      get().startStreaming(jobId);

      return jobId;
    } catch (error: unknown) {
      const errorObj = error instanceof Error ? error : new Error(String(error));
      logger.error('Error creating job', errorObj, 'jobsStore', 'createJob');
      throw errorObj;
    } finally {
      set({ loading: false });
    }
  },

  getJob: async (jobId: string) => {
    try {
      logger.debug('Fetching job', 'jobsStore', 'getJob', { jobId });
      const job = await apiGet<Job>(`/api/jobs/${jobId}`);
      set({ activeJob: job });
    } catch (error: unknown) {
      const errorObj = error instanceof Error ? error : new Error(String(error));
      logger.error('Error getting job', errorObj, 'jobsStore', 'getJob', { jobId });
    }
  },

  getFailureDetails: async (jobId: string) => {
    try {
      logger.debug('Fetching failure details', 'jobsStore', 'getFailureDetails', { jobId });
      const failureDetails = await apiGet<JobFailure>(`/api/jobs/${jobId}/failure-details`);

      // Update active job with failure details if it matches
      const state = get();
      if (state.activeJob && state.activeJob.id === jobId) {
        set({
          activeJob: {
            ...state.activeJob,
            failureDetails,
          },
        });
      }

      return failureDetails;
    } catch (error: unknown) {
      const errorObj = error instanceof Error ? error : new Error(String(error));

      // 404 or 400 means no failure details available (job not failed yet)
      if (error && typeof error === 'object' && 'response' in error) {
        const axiosError = error as { response?: { status?: number } };
        if (axiosError.response?.status === 404 || axiosError.response?.status === 400) {
          return null;
        }
      }

      logger.error('Error getting failure details', errorObj, 'jobsStore', 'getFailureDetails', {
        jobId,
      });
      return null;
    }
  },

  listJobs: async () => {
    set({ loading: true });
    try {
      logger.debug('Listing all jobs', 'jobsStore', 'listJobs');
      const data = await apiGet<{ jobs: Job[] }>(apiUrl('/api/jobs'));
      set({ jobs: data.jobs || [] });
    } catch (error: unknown) {
      const errorObj = error instanceof Error ? error : new Error(String(error));
      logger.error('Error listing jobs', errorObj, 'jobsStore', 'listJobs');
    } finally {
      set({ loading: false });
    }
  },

  setActiveJob: (job: Job | null) => {
    set({ activeJob: job });
  },

  cancelJob: async (jobId: string) => {
    try {
      logger.info('Cancelling job', 'jobsStore', 'cancelJob', { jobId });
      await post<void>(`/api/jobs/${jobId}/cancel`, undefined);

      logger.debug(`Job ${jobId} cancellation requested`, 'JobsStore', 'cancelJob');

      // Update active job to reflect cancellation in progress
      const state = get();
      if (state.activeJob && state.activeJob.id === jobId) {
        get().updateJobFromSse({
          status: 'Canceled',
          errorMessage: 'Job cancellation in progress...',
        });
      }
    } catch (error: unknown) {
      const errorObj = error instanceof Error ? error : new Error(String(error));
      logger.error('Error canceling job', errorObj, 'JobsStore', 'cancelJob');
      throw errorObj;
    }
  },

  updateJobFromSse: (updates: Partial<Job>) => {
    const state = get();
    if (state.activeJob) {
      set({
        activeJob: {
          ...state.activeJob,
          ...updates,
        },
      });
    }
  },

  startStreaming: (jobId: string) => {
    // Stop any existing SSE connection
    get().stopStreaming();

    logger.debug(`Starting SSE streaming for job: ${jobId}`, 'JobsStore', 'startStreaming');
    set({
      streaming: true,
      connectionState: { status: 'connecting', reconnectAttempt: 0, lastEventId: null },
    });

    // Create new SSE client
    sseClient = createSseClient(jobId);

    // Track connection status changes
    sseClient.onStatusChange((state) => {
      logger.debug('SSE connection status changed', 'JobsStore', 'statusChange', { state });
      set({ connectionState: state });
    });

    // Handle job status updates
    sseClient.on('job-status', (event) => {
      const data = event.data as JobStatusEvent;
      logger.debug('Job status update', 'JobsStore', 'jobStatus', { data });
      get().updateJobFromSse({
        status: data.status as Job['status'],
        stage: data.stage,
        percent: data.percent,
      });
    });

    // Handle step progress updates
    sseClient.on('step-progress', (event) => {
      const data = event.data as StepProgressEvent;
      logger.debug('Step progress', 'JobsStore', 'stepProgress', { data });
      get().updateJobFromSse({
        stage: data.step,
        phase: data.phase,
        percent: data.progressPct,
        progressMessage: data.message,
        substageDetail: data.substageDetail,
        currentItem: data.currentItem,
        totalItems: data.totalItems,
        elapsedTime: data.elapsedTime,
        estimatedTimeRemaining: data.estimatedTimeRemaining,
      });
    });

    // Handle step status changes (phase transitions)
    sseClient.on('step-status', (event) => {
      const data = event.data as { step: string; status: string; phase: string };
      logger.debug('Step status', 'JobsStore', 'stepStatus', { data });
      get().updateJobFromSse({
        stage: data.step,
        phase: data.phase,
      });
    });

    // Handle job completion
    sseClient.on('job-completed', (event) => {
      const data = event.data as JobCompletedEvent;
      logger.debug('Job completed', 'JobsStore', 'jobCompleted', { data });
      get().updateJobFromSse({
        status: 'Done',
        percent: 100,
        artifacts: data.artifacts.map((a) => ({
          name: a.name,
          path: a.path,
          type: a.type,
          sizeBytes: a.sizeBytes,
          createdAt: new Date().toISOString(),
        })),
        outputPath: data.output?.videoPath,
        subtitlePath: data.output?.subtitlePath,
        finishedAt: new Date().toISOString(),
      });
      get().stopStreaming();
    });

    // Handle job failure
    sseClient.on('job-failed', (event) => {
      const data = event.data as JobFailedEvent;
      logger.error(
        'Job failed',
        new Error(data.errorMessage || 'Job failed'),
        'JobsStore',
        'jobFailed',
        { data }
      );
      get().updateJobFromSse({
        status: 'Failed',
        errorMessage: data.errorMessage || data.errors[0]?.message || 'Job failed',
        logs: data.logs || [],
        finishedAt: new Date().toISOString(),
      });
      get().stopStreaming();
    });

    // Handle job cancellation
    sseClient.on('job-cancelled', (event) => {
      const data = event.data as JobCancelledEvent;
      logger.debug('Job cancelled', 'JobsStore', 'jobCancelled', { data });
      get().updateJobFromSse({
        status: 'Canceled',
        errorMessage: data.message,
        finishedAt: new Date().toISOString(),
      });
      get().stopStreaming();
    });

    // Handle warnings
    sseClient.on('warning', (event) => {
      const data = event.data as { message: string };
      logger.warn(data.message, 'JobsStore', 'warning');
      // Could update logs here if needed
    });

    // Handle errors
    sseClient.on('error', (event) => {
      const data = event.data as { message: string };
      logger.error('SSE error', new Error(data.message), 'JobsStore', 'sseError');
      get().updateJobFromSse({
        status: 'Failed',
        errorMessage: data.message,
      });
      get().stopStreaming();
    });

    // Connect to SSE endpoint
    sseClient.connect();
  },

  stopStreaming: () => {
    if (sseClient) {
      logger.debug('Stopping SSE streaming', 'JobsStore', 'stopStreaming');
      sseClient.close();
      sseClient = null;
    }
    set({ streaming: false });
  },
}));
