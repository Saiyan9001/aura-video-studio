/**
 * Settings Validation Service
 *
 * Validates that required settings are configured and valid.
 * Used by ConfigurationGate to determine if setup is needed.
 */

import { apiUrl } from '../config/api';
import { isValidPath, migrateLegacyPath } from '../utils/pathUtils';

export interface SettingsValidation {
  valid: boolean;
  error?: string;
  missingSettings?: string[];
}

/**
 * Validate that all required settings are configured
 */
export async function validateRequiredSettings(): Promise<SettingsValidation> {
  const missingSettings: string[] = [];

  try {
    // Check FFmpeg availability (required for video generation)
    const ffmpegStatus = await checkFFmpegStatus();
    if (!ffmpegStatus.available) {
      missingSettings.push('FFmpeg');
    }

    // Check default save location - no longer required to be set, we provide a default
    const saveLocation = await getDefaultSaveLocation();
    if (!saveLocation) {
      console.warn('Could not determine default save location');
    }

    // If FFmpeg is missing, return invalid (save location has defaults)
    if (missingSettings.length > 0) {
      return {
        valid: false,
        error: `Required software missing: ${missingSettings.join(', ')}`,
        missingSettings,
      };
    }

    return { valid: true };
  } catch (error: unknown) {
    console.error('Settings validation error:', error);
    return {
      valid: false,
      error: error instanceof Error ? error.message : 'Settings validation failed',
    };
  }
}

/**
 * Check if FFmpeg is available and configured
 */
async function checkFFmpegStatus(): Promise<{ available: boolean; path?: string }> {
  try {
    const response = await fetch(apiUrl('/api/downloads/ffmpeg/status'));

    if (!response.ok) {
      return { available: false };
    }

    const data = await response.json();
    const isAvailable = data.state === 'Installed' || data.state === 'ExternalAttached';

    return {
      available: isAvailable,
      path: data.path,
    };
  } catch (error: unknown) {
    console.error('FFmpeg status check failed:', error);
    return { available: false };
  }
}

/**
 * Get the platform-specific default save location
 * Note: Browser cannot access actual home directory for security reasons,
 * so we return placeholder paths that the backend will resolve properly
 */
function getPlatformDefaultSaveLocation(): string {
  const platform = navigator.platform.toLowerCase();

  if (platform.includes('win')) {
    return '%USERPROFILE%\\Videos\\Aura';
  } else if (platform.includes('mac')) {
    return '~/Movies/Aura';
  } else {
    return '~/Videos/Aura';
  }
}

/**
 * Get the configured default save location from settings
 */
async function getDefaultSaveLocation(): Promise<string | null> {
  try {
    // Check localStorage first (fast path)
    const localSettings = localStorage.getItem('workspacePreferences');
    if (localSettings) {
      const prefs = JSON.parse(localSettings);
      if (prefs.defaultSaveLocation) {
        // Migrate legacy paths if needed
        return migrateLegacyPath(prefs.defaultSaveLocation);
      }
    }

    // Fall back to backend settings
    const response = await fetch(apiUrl('/api/settings/user'));
    if (!response.ok) {
      return getPlatformDefaultSaveLocation();
    }

    const settings = await response.json();
    const saveLocation = settings?.general?.defaultProjectSaveLocation;

    if (saveLocation && isValidPath(saveLocation)) {
      return saveLocation;
    }

    return getPlatformDefaultSaveLocation();
  } catch (error: unknown) {
    console.error('Failed to get default save location:', error);
    return getPlatformDefaultSaveLocation();
  }
}

/**
 * Migrate settings from old format to new format
 * This is called during app initialization
 */
export async function migrateSettingsIfNeeded(): Promise<void> {
  try {
    // Check if migration is needed
    const localSettings = localStorage.getItem('workspacePreferences');
    if (!localSettings) {
      return;
    }

    const prefs = JSON.parse(localSettings);
    let needsMigration = false;
    const migratedPrefs = { ...prefs };

    // Migrate default save location if it has placeholder
    if (prefs.defaultSaveLocation && !isValidPath(prefs.defaultSaveLocation)) {
      migratedPrefs.defaultSaveLocation = migrateLegacyPath(prefs.defaultSaveLocation);
      needsMigration = true;
    }

    // Migrate cache location if it has placeholder
    if (prefs.cacheLocation && !isValidPath(prefs.cacheLocation)) {
      migratedPrefs.cacheLocation = migrateLegacyPath(prefs.cacheLocation);
      needsMigration = true;
    }

    // Save migrated settings
    if (needsMigration) {
      localStorage.setItem('workspacePreferences', JSON.stringify(migratedPrefs));

      // Also update backend settings
      await fetch(apiUrl('/api/settings'), {
        method: 'PUT',
        headers: {
          'Content-Type': 'application/json',
        },
        body: JSON.stringify({
          defaultSaveLocation: migratedPrefs.defaultSaveLocation,
          cacheLocation: migratedPrefs.cacheLocation,
        }),
      });
    }
  } catch (error: unknown) {
    console.error('Settings migration failed:', error);
  }
}

/**
 * Persist workspace preferences to localStorage and backend
 */
export async function saveWorkspacePreferences(preferences: {
  defaultSaveLocation: string;
  cacheLocation: string;
  autosaveInterval: number;
  theme: 'light' | 'dark' | 'auto';
}): Promise<void> {
  try {
    // Save to localStorage for fast access
    localStorage.setItem('workspacePreferences', JSON.stringify(preferences));

    // Save to backend for persistence
    await fetch(apiUrl('/api/settings'), {
      method: 'PUT',
      headers: {
        'Content-Type': 'application/json',
      },
      body: JSON.stringify({
        defaultSaveLocation: preferences.defaultSaveLocation,
        cacheLocation: preferences.cacheLocation,
        autosaveInterval: preferences.autosaveInterval,
        theme: preferences.theme,
      }),
    });
  } catch (error: unknown) {
    console.error('Failed to save workspace preferences:', error);
    throw error;
  }
}
