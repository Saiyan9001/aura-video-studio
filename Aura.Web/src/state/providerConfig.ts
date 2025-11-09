/**
 * Provider configuration state management
 */

import { create } from 'zustand';
import {
  getProviderStatuses,
  getProviderPreferences,
  updateProviderPreferences,
  testProviderConnection,
  validateOpenAIKey,
  validateElevenLabsKey,
  validatePlayHTKey,
  type ProviderStatus,
} from '../services/api/providersApi';
import { loggingService as logger } from '../services/loggingService';
import type {
  ProviderConfigDto,
  QualityConfigDto,
  ConfigurationProfileDto,
  ConfigValidationResultDto,
} from '../types/api-v1';

interface ProviderConfigState {
  providers: ProviderConfigDto[];
  providerStatuses: ProviderStatus[];
  qualityConfig: QualityConfigDto | null;
  profiles: ConfigurationProfileDto[];
  validationResult: ConfigValidationResultDto | null;
  isLoading: boolean;
  isSaving: boolean;
  isTestingConnection: Record<string, boolean>;
  error: string | null;

  // Actions
  setProviders: (providers: ProviderConfigDto[]) => void;
  setQualityConfig: (config: QualityConfigDto) => void;
  setProfiles: (profiles: ConfigurationProfileDto[]) => void;
  setValidationResult: (result: ConfigValidationResultDto | null) => void;
  setIsLoading: (loading: boolean) => void;
  setIsSaving: (saving: boolean) => void;
  setError: (error: string | null) => void;
  updateProvider: (name: string, updates: Partial<ProviderConfigDto>) => void;
  reorderProviders: (startIndex: number, endIndex: number) => void;
  resetToDefaults: () => void;

  // API Actions
  fetchProviderStatuses: () => Promise<void>;
  loadProviderPreferences: () => Promise<void>;
  saveProviderPreferences: (
    selectedProfile: string,
    customSelections?: Record<string, string>
  ) => Promise<void>;
  testConnection: (
    providerId: string,
    testConfig?: { apiKey?: string; endpoint?: string }
  ) => Promise<boolean>;
  validateApiKey: (
    provider: string,
    apiKey: string,
    userId?: string
  ) => Promise<{ success: boolean; message: string }>;
}

const createDefaultQualityConfig = (): QualityConfigDto => ({
  video: {
    resolution: '1080p',
    width: 1920,
    height: 1080,
    framerate: 30,
    bitratePreset: 'High',
    bitrateKbps: 5000,
    codec: 'h264',
    container: 'mp4',
  },
  audio: {
    bitrate: 192,
    sampleRate: 48000,
    channels: 2,
    codec: 'aac',
  },
  subtitles: {
    fontFamily: 'Arial',
    fontSize: 24,
    fontColor: '#FFFFFF',
    backgroundColor: '#000000',
    backgroundOpacity: 0.7,
    position: 'Bottom',
    outlineWidth: 2,
    outlineColor: '#000000',
  },
});

export const useProviderConfigStore = create<ProviderConfigState>((set) => ({
  providers: [],
  providerStatuses: [],
  qualityConfig: null,
  profiles: [],
  validationResult: null,
  isLoading: false,
  isSaving: false,
  isTestingConnection: {},
  error: null,

  setProviders: (providers) => set({ providers }),

  setQualityConfig: (config) => set({ qualityConfig: config }),

  setProfiles: (profiles) => set({ profiles }),

  setValidationResult: (result) => set({ validationResult: result }),

  setIsLoading: (loading) => set({ isLoading: loading }),

  setIsSaving: (saving) => set({ isSaving: saving }),

  setError: (error) => set({ error }),

  updateProvider: (name, updates) =>
    set((state) => ({
      providers: state.providers.map((p) => (p.name === name ? { ...p, ...updates } : p)),
    })),

  reorderProviders: (startIndex, endIndex) =>
    set((state) => {
      const result = Array.from(state.providers);
      const [removed] = result.splice(startIndex, 1);
      result.splice(endIndex, 0, removed);

      return {
        providers: result.map((p, index) => ({
          ...p,
          priority: index + 1,
        })),
      };
    }),

  resetToDefaults: () =>
    set({
      providers: [],
      qualityConfig: createDefaultQualityConfig(),
      validationResult: null,
      error: null,
    }),

  // API Actions
  fetchProviderStatuses: async () => {
    try {
      logger.debug('Fetching provider statuses', 'providerConfigStore', 'fetchProviderStatuses');
      set({ isLoading: true, error: null });
      const statuses = await getProviderStatuses();
      set({ providerStatuses: statuses });
      logger.info('Provider statuses loaded', 'providerConfigStore', 'fetchProviderStatuses', {
        count: statuses.length,
      });
    } catch (error: unknown) {
      const errorObj = error instanceof Error ? error : new Error(String(error));
      logger.error(
        'Failed to fetch provider statuses',
        errorObj,
        'providerConfigStore',
        'fetchProviderStatuses'
      );
      set({ error: 'Failed to load provider statuses' });
    } finally {
      set({ isLoading: false });
    }
  },

  loadProviderPreferences: async () => {
    try {
      logger.debug(
        'Loading provider preferences',
        'providerConfigStore',
        'loadProviderPreferences'
      );
      set({ isLoading: true, error: null });
      const preferences = await getProviderPreferences();
      logger.info(
        'Provider preferences loaded',
        'providerConfigStore',
        'loadProviderPreferences',
        preferences
      );
      // Update state with loaded preferences
      // Note: This would require mapping preferences to the providers state
    } catch (error: unknown) {
      const errorObj = error instanceof Error ? error : new Error(String(error));
      logger.error(
        'Failed to load provider preferences',
        errorObj,
        'providerConfigStore',
        'loadProviderPreferences'
      );
      set({ error: 'Failed to load provider preferences' });
    } finally {
      set({ isLoading: false });
    }
  },

  saveProviderPreferences: async (
    selectedProfile: string,
    customSelections?: Record<string, string>
  ) => {
    try {
      logger.info('Saving provider preferences', 'providerConfigStore', 'saveProviderPreferences', {
        selectedProfile,
        hasCustomSelections: !!customSelections,
      });
      set({ isSaving: true, error: null });
      await updateProviderPreferences({ selectedProfile, customSelections });
      logger.info(
        'Provider preferences saved successfully',
        'providerConfigStore',
        'saveProviderPreferences'
      );
    } catch (error: unknown) {
      const errorObj = error instanceof Error ? error : new Error(String(error));
      logger.error(
        'Failed to save provider preferences',
        errorObj,
        'providerConfigStore',
        'saveProviderPreferences'
      );
      set({ error: 'Failed to save provider preferences' });
      throw errorObj;
    } finally {
      set({ isSaving: false });
    }
  },

  testConnection: async (
    providerId: string,
    testConfig?: { apiKey?: string; endpoint?: string }
  ) => {
    try {
      logger.info('Testing provider connection', 'providerConfigStore', 'testConnection', {
        providerId,
      });
      set((state) => ({
        isTestingConnection: { ...state.isTestingConnection, [providerId]: true },
      }));

      const result = await testProviderConnection(providerId, testConfig);

      logger.info('Provider connection test completed', 'providerConfigStore', 'testConnection', {
        providerId,
        success: result.success,
      });

      return result.success;
    } catch (error: unknown) {
      const errorObj = error instanceof Error ? error : new Error(String(error));
      logger.error(
        'Provider connection test failed',
        errorObj,
        'providerConfigStore',
        'testConnection',
        {
          providerId,
        }
      );
      return false;
    } finally {
      set((state) => {
        const updated = { ...state.isTestingConnection };
        delete updated[providerId];
        return { isTestingConnection: updated };
      });
    }
  },

  validateApiKey: async (provider: string, apiKey: string, userId?: string) => {
    try {
      logger.info('Validating API key', 'providerConfigStore', 'validateApiKey', { provider });

      let result;
      switch (provider.toLowerCase()) {
        case 'openai':
          result = await validateOpenAIKey(apiKey);
          break;
        case 'elevenlabs':
          result = await validateElevenLabsKey(apiKey);
          break;
        case 'playht':
          if (!userId) {
            return { success: false, message: 'User ID is required for PlayHT validation' };
          }
          result = await validatePlayHTKey(apiKey, userId);
          break;
        default:
          return { success: false, message: `Validation not supported for provider: ${provider}` };
      }

      logger.info('API key validation completed', 'providerConfigStore', 'validateApiKey', {
        provider,
        isValid: result.isValid,
      });

      return { success: result.isValid, message: result.message };
    } catch (error: unknown) {
      const errorObj = error instanceof Error ? error : new Error(String(error));
      logger.error('API key validation failed', errorObj, 'providerConfigStore', 'validateApiKey', {
        provider,
      });
      return { success: false, message: errorObj.message || 'Validation failed' };
    }
  },
}));
