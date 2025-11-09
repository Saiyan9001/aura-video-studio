import { apiUrl } from '../config/api';
import type { UserSettings } from '../types/settings';
import { createDefaultSettings } from '../types/settings';
import { get, post } from './api/apiClient';
import { loggingService as logger } from './loggingService';

const STORAGE_KEY = 'aura-user-settings';
const CACHE_EXPIRY_MS = 5 * 60 * 1000; // 5 minutes

interface CachedSettings {
  data: UserSettings;
  timestamp: number;
}

/**
 * Settings service for managing user settings with backend persistence
 * and localStorage caching for offline access
 */
export class SettingsService {
  private cache: CachedSettings | null = null;

  /**
   * Load settings from backend with localStorage fallback
   */
  async loadSettings(): Promise<UserSettings> {
    // Check cache first
    const cached = this.getFromCache();
    if (cached) {
      return cached;
    }

    try {
      const settings = await get<UserSettings>(apiUrl('/api/settings/user'));
      this.saveToCache(settings);
      this.saveToLocalStorage(settings);
      return settings;
    } catch (error) {
      logger.error(
        'Error loading settings from backend',
        error instanceof Error ? error : new Error(String(error)),
        'settingsService',
        'loadSettings'
      );

      // Fallback to localStorage
      const localSettings = this.getFromLocalStorage();
      if (localSettings) {
        return localSettings;
      }

      // Last resort: return defaults
      return createDefaultSettings();
    }
  }

  /**
   * Save settings to backend and localStorage
   */
  async saveSettings(settings: UserSettings): Promise<boolean> {
    try {
      await post<void>(apiUrl('/api/settings/user'), settings);
      this.saveToCache(settings);
      this.saveToLocalStorage(settings);
      return true;
    } catch (error) {
      logger.error(
        'Error saving settings',
        error instanceof Error ? error : new Error(String(error)),
        'settingsService',
        'saveSettings'
      );
      // Still save to localStorage even if backend fails
      this.saveToLocalStorage(settings);
      return false;
    }
  }

  /**
   * Reset settings to defaults
   */
  async resetToDefaults(): Promise<UserSettings> {
    const defaults = createDefaultSettings();
    await this.saveSettings(defaults);
    return defaults;
  }

  /**
   * Export settings as JSON file
   */
  exportSettings(settings: UserSettings, filename?: string): void {
    const exportData = {
      ...settings,
      exportedAt: new Date().toISOString(),
    };

    const blob = new Blob([JSON.stringify(exportData, null, 2)], {
      type: 'application/json',
    });
    const url = URL.createObjectURL(blob);
    const a = document.createElement('a');
    a.href = url;
    a.download = filename || `aura-settings-${new Date().toISOString().split('T')[0]}.json`;
    a.click();
    URL.revokeObjectURL(url);
  }

  /**
   * Import settings from JSON file
   */
  async importSettings(file: File): Promise<UserSettings | null> {
    try {
      const text = await file.text();
      const data = JSON.parse(text);

      // Basic validation
      if (!data.version) {
        throw new Error('Invalid settings file: missing version');
      }

      // Remove export timestamp if present
      delete data.exportedAt;

      // Merge with defaults to ensure all fields exist
      const defaults = createDefaultSettings();
      const settings: UserSettings = {
        ...defaults,
        ...data,
        general: { ...defaults.general, ...data.general },
        apiKeys: { ...defaults.apiKeys, ...data.apiKeys },
        fileLocations: { ...defaults.fileLocations, ...data.fileLocations },
        videoDefaults: { ...defaults.videoDefaults, ...data.videoDefaults },
        editorPreferences: { ...defaults.editorPreferences, ...data.editorPreferences },
        ui: { ...defaults.ui, ...data.ui },
        advanced: { ...defaults.advanced, ...data.advanced },
        lastUpdated: new Date().toISOString(),
      };

      return settings;
    } catch (error) {
      logger.error(
        'Error importing settings',
        error instanceof Error ? error : new Error(String(error)),
        'settingsService',
        'importSettings'
      );
      return null;
    }
  }

  /**
   * Test API key connection for a specific provider
   */
  async testApiKey(
    provider: string,
    apiKey: string
  ): Promise<{ success: boolean; message: string; responseTimeMs?: number }> {
    try {
      // For OpenAI, use the new live validation endpoint
      if (provider.toLowerCase() === 'openai') {
        const controller = new AbortController();
        const timeoutId = setTimeout(() => controller.abort(), 15000); // 15 second timeout

        try {
          const response = await fetch(apiUrl('/api/providers/openai/validate'), {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({ apiKey }),
            signal: controller.signal,
          });

          clearTimeout(timeoutId);

          const data = await response.json();

          // Handle different response types
          if (response.ok) {
            return {
              success: data.isValid === true,
              message: data.message || 'API key is valid and verified with OpenAI.',
              responseTimeMs: data.details?.responseTimeMs,
            };
          }

          // Handle error responses (ProblemDetails format)
          if (data.detail || data.title) {
            return {
              success: false,
              message: data.detail || data.title || 'Failed to validate API key',
            };
          }

          // Handle validation response format
          if (data.isValid === false) {
            return {
              success: false,
              message: data.message || 'API key validation failed',
            };
          }

          return {
            success: false,
            message: 'Failed to validate API key',
          };
        } catch (error: unknown) {
          clearTimeout(timeoutId);
          
          if (error instanceof Error && error.name === 'AbortError') {
            return {
              success: false,
              message: 'Connection timeout - check network connectivity',
            };
          }
          throw error;
        }
      }

      // For other providers, use the old endpoint
      const response = await fetch(apiUrl(`/api/settings/test-api-key/${provider}`), {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ apiKey }),
      });

      if (response.ok) {
        return await response.json();
      }

      return {
        success: false,
        message: 'Failed to test API key',
      };
    } catch (error) {
      return {
        success: false,
        message: `Network error: ${error}`,
      };
    }
  }

  /**
   * Get available models for OpenAI API key
   */
  async getOpenAIModels(apiKey: string): Promise<{ success: boolean; models?: string[]; message?: string }> {
    try {
      const controller = new AbortController();
      const timeoutId = setTimeout(() => controller.abort(), 15000); // 15 second timeout

      try {
        const response = await fetch(apiUrl('/api/providers/openai/models'), {
          method: 'POST',
          headers: { 'Content-Type': 'application/json' },
          body: JSON.stringify({ apiKey }),
          signal: controller.signal,
        });

        clearTimeout(timeoutId);

        const data = await response.json();

        if (response.ok && data.success) {
          return {
            success: true,
            models: data.models || [],
          };
        }

        return {
          success: false,
          message: data.message || 'Failed to fetch models',
        };
      } catch (error: unknown) {
        clearTimeout(timeoutId);
        
        if (error instanceof Error && error.name === 'AbortError') {
          return {
            success: false,
            message: 'Connection timeout - check network connectivity',
          };
        }
        throw error;
      }
    } catch (error) {
      return {
        success: false,
        message: `Network error: ${error}`,
      };
    }
  }

  /**
   * Test OpenAI script generation with a specific model
   */
  async testOpenAIGeneration(
    apiKey: string,
    model: string
  ): Promise<{ success: boolean; generatedText?: string; responseTimeMs?: number; message?: string }> {
    try {
      const response = await fetch(apiUrl('/api/providers/openai/test-generation'), {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ apiKey, model }),
      });

      const data = await response.json();

      if (response.ok && data.success) {
        return {
          success: true,
          generatedText: data.generatedText,
          responseTimeMs: data.responseTimeMs,
        };
      }

      return {
        success: false,
        message: data.message || 'Failed to test generation',
        responseTimeMs: data.responseTimeMs,
      };
    } catch (error) {
      return {
        success: false,
        message: `Network error: ${error}`,
      };
    }
  }

  /**
   * Validate file path exists
   */
  async validatePath(path: string): Promise<{ valid: boolean; message: string }> {
    try {
      const response = await fetch(apiUrl('/api/settings/validate-path'), {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ path }),
      });

      if (response.ok) {
        return await response.json();
      }

      return {
        valid: false,
        message: 'Failed to validate path',
      };
    } catch (error) {
      return {
        valid: false,
        message: `Network error: ${error}`,
      };
    }
  }

  /**
   * Get from memory cache
   */
  private getFromCache(): UserSettings | null {
    if (!this.cache) return null;

    const age = Date.now() - this.cache.timestamp;
    if (age > CACHE_EXPIRY_MS) {
      this.cache = null;
      return null;
    }

    return this.cache.data;
  }

  /**
   * Save to memory cache
   */
  private saveToCache(settings: UserSettings): void {
    this.cache = {
      data: settings,
      timestamp: Date.now(),
    };
  }

  /**
   * Get from localStorage
   */
  private getFromLocalStorage(): UserSettings | null {
    try {
      const stored = localStorage.getItem(STORAGE_KEY);
      if (stored) {
        return JSON.parse(stored);
      }
    } catch (error) {
      logger.error(
        'Error reading from localStorage',
        error instanceof Error ? error : new Error(String(error)),
        'settingsService',
        'getFromLocalStorage'
      );
    }
    return null;
  }

  /**
   * Save to localStorage
   */
  private saveToLocalStorage(settings: UserSettings): void {
    try {
      localStorage.setItem(STORAGE_KEY, JSON.stringify(settings));
    } catch (error) {
      logger.error(
        'Error saving to localStorage',
        error instanceof Error ? error : new Error(String(error)),
        'settingsService',
        'saveToLocalStorage'
      );
    }
  }

  /**
   * Clear cache (useful for testing)
   */
  clearCache(): void {
    this.cache = null;
  }
}

// Export singleton instance
export const settingsService = new SettingsService();
