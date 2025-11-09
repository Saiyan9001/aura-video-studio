import apiClient from './apiClient';

export interface FFmpegHardwareAcceleration {
  nvencSupported: boolean;
  amfSupported: boolean;
  quickSyncSupported: boolean;
  videoToolboxSupported: boolean;
  availableEncoders: string[];
}

export interface FFmpegStatus {
  installed: boolean;
  valid: boolean;
  version: string | null;
  path: string | null;
  source: string;
  error: string | null;
  versionMeetsRequirement: boolean;
  minimumVersion: string;
  hardwareAcceleration: FFmpegHardwareAcceleration;
  correlationId: string;
}

export interface FFmpegInstallRequest {
  version?: string;
}

export interface FFmpegInstallResponse {
  success: boolean;
  message: string;
  version?: string;
  path?: string;
  correlationId: string;
}

/**
 * API client for FFmpeg status and installation
 */
export const ffmpegClient = {
  /**
   * Get comprehensive FFmpeg status
   */
  async getStatus(): Promise<FFmpegStatus> {
    const response = await apiClient.get<FFmpegStatus>('/api/system/ffmpeg/status');
    return response.data;
  },

  /**
   * Install managed FFmpeg
   */
  async install(request?: FFmpegInstallRequest): Promise<FFmpegInstallResponse> {
    const response = await apiClient.post<FFmpegInstallResponse>('/api/ffmpeg/install', request);
    return response.data;
  },
};
