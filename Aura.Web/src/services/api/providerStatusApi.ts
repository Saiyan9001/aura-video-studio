import apiClient from './apiClient';

export interface DetailedProviderStatus {
  name: string;
  category: string;
  isAvailable: boolean;
  isOnline: boolean;
  tier: string;
  features: string[];
  message: string;
}

export interface SystemProviderStatus {
  isOfflineMode: boolean;
  providers: DetailedProviderStatus[];
  onlineProvidersCount: number;
  offlineProvidersCount: number;
  availableFeatures: string[];
  degradedFeatures: string[];
  lastUpdated: string;
  message: string;
}

export interface OfflineModeInfo {
  isOfflineMode: boolean;
  availableFeatures: string[];
  degradedFeatures: string[];
  message: string;
}

export interface FeaturesInfo {
  available: string[];
  degraded: string[];
}

/**
 * API client for provider status and offline mode detection
 */
export const providerStatusApi = {
  /**
   * Get comprehensive status of all providers
   */
  async getStatus(): Promise<SystemProviderStatus> {
    const response = await apiClient.get<SystemProviderStatus>('/api/provider-status');
    return response.data;
  },

  /**
   * Check if system is in offline mode
   */
  async checkOfflineMode(): Promise<OfflineModeInfo> {
    const response = await apiClient.get<OfflineModeInfo>('/api/provider-status/offline-mode');
    return response.data;
  },

  /**
   * Refresh provider status cache
   */
  async refresh(): Promise<void> {
    await apiClient.post('/api/provider-status/refresh');
  },

  /**
   * Get available and degraded features
   */
  async getFeatures(): Promise<FeaturesInfo> {
    const response = await apiClient.get<FeaturesInfo>('/api/provider-status/features');
    return response.data;
  },
};
