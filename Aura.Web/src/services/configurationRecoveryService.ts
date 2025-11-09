/**
 * Configuration Recovery Service
 *
 * Handles configuration validation, backup, and recovery
 * Provides mechanisms to detect and fix corrupted configurations
 */

import { env } from '../config/env';
import { loggingService } from './loggingService';

export interface ConfigValidationResult {
  valid: boolean;
  errors: string[];
  warnings: string[];
}

export interface ConfigBackup {
  timestamp: string;
  config: unknown;
  version: string;
}

const CONFIG_STORAGE_KEY = 'aura_config';
const CONFIG_BACKUP_KEY = 'aura_config_backup';
const CONFIG_VERSION_KEY = 'aura_config_version';
const CURRENT_CONFIG_VERSION = '1.0.0';
const MAX_BACKUPS = 5;

class ConfigurationRecoveryService {
  /**
   * Validate current configuration
   */
  public validateConfiguration(): ConfigValidationResult {
    const errors: string[] = [];
    const warnings: string[] = [];

    try {
      const configStr = localStorage.getItem(CONFIG_STORAGE_KEY);

      if (!configStr) {
        warnings.push('No configuration found (using defaults)');
        return { valid: true, errors, warnings };
      }

      let config: unknown;
      try {
        config = JSON.parse(configStr);
      } catch (error: unknown) {
        errors.push('Configuration is not valid JSON');
        loggingService.error(
          'Failed to parse configuration',
          error instanceof Error ? error : new Error(String(error)),
          'ConfigurationRecoveryService',
          'validateConfiguration'
        );
        return { valid: false, errors, warnings };
      }

      if (!config || typeof config !== 'object') {
        errors.push('Configuration is not a valid object');
        return { valid: false, errors, warnings };
      }

      const configVersion = localStorage.getItem(CONFIG_VERSION_KEY);
      if (!configVersion) {
        warnings.push('Configuration version not set');
      } else if (configVersion !== CURRENT_CONFIG_VERSION) {
        warnings.push(
          `Configuration version mismatch (expected ${CURRENT_CONFIG_VERSION}, got ${configVersion})`
        );
      }

      return { valid: errors.length === 0, errors, warnings };
    } catch (error: unknown) {
      errors.push('Unexpected error during validation');
      loggingService.error(
        'Configuration validation failed',
        error instanceof Error ? error : new Error(String(error)),
        'ConfigurationRecoveryService',
        'validateConfiguration'
      );
      return { valid: false, errors, warnings };
    }
  }

  /**
   * Create a backup of current configuration
   */
  public async backupConfiguration(): Promise<boolean> {
    try {
      const configStr = localStorage.getItem(CONFIG_STORAGE_KEY);

      if (!configStr) {
        loggingService.warn(
          'No configuration to backup',
          'ConfigurationRecoveryService',
          'backupConfiguration'
        );
        return false;
      }

      const backup: ConfigBackup = {
        timestamp: new Date().toISOString(),
        config: JSON.parse(configStr),
        version: localStorage.getItem(CONFIG_VERSION_KEY) || 'unknown',
      };

      const backupsStr = localStorage.getItem(CONFIG_BACKUP_KEY);
      const backups: ConfigBackup[] = backupsStr ? JSON.parse(backupsStr) : [];

      backups.unshift(backup);

      if (backups.length > MAX_BACKUPS) {
        backups.splice(MAX_BACKUPS);
      }

      localStorage.setItem(CONFIG_BACKUP_KEY, JSON.stringify(backups));

      loggingService.info(
        `Configuration backed up (${backups.length} backups stored)`,
        'ConfigurationRecoveryService',
        'backupConfiguration'
      );

      return true;
    } catch (error: unknown) {
      loggingService.error(
        'Failed to backup configuration',
        error instanceof Error ? error : new Error(String(error)),
        'ConfigurationRecoveryService',
        'backupConfiguration'
      );
      return false;
    }
  }

  /**
   * Restore configuration from most recent backup
   */
  public async restoreFromBackup(backupIndex: number = 0): Promise<boolean> {
    try {
      const backupsStr = localStorage.getItem(CONFIG_BACKUP_KEY);

      if (!backupsStr) {
        loggingService.warn(
          'No backups available',
          'ConfigurationRecoveryService',
          'restoreFromBackup'
        );
        return false;
      }

      const backups: ConfigBackup[] = JSON.parse(backupsStr);

      if (backupIndex >= backups.length) {
        loggingService.error(
          'Invalid backup index',
          new Error(
            `Backup index ${backupIndex} out of range (${backups.length} backups available)`
          ),
          'ConfigurationRecoveryService',
          'restoreFromBackup'
        );
        return false;
      }

      const backup = backups[backupIndex];

      localStorage.setItem(CONFIG_STORAGE_KEY, JSON.stringify(backup.config));
      localStorage.setItem(CONFIG_VERSION_KEY, backup.version);

      loggingService.info(
        `Configuration restored from backup (${backup.timestamp})`,
        'ConfigurationRecoveryService',
        'restoreFromBackup'
      );

      return true;
    } catch (error: unknown) {
      loggingService.error(
        'Failed to restore configuration',
        error instanceof Error ? error : new Error(String(error)),
        'ConfigurationRecoveryService',
        'restoreFromBackup'
      );
      return false;
    }
  }

  /**
   * Reset configuration to defaults
   */
  public async resetToDefaults(): Promise<boolean> {
    try {
      await this.backupConfiguration();

      const defaultConfig = {
        theme: 'dark',
        apiUrl: env.apiBaseUrl,
        ffmpegPath: '',
        providers: {
          llm: 'RuleBased',
          tts: 'WindowsSAPI',
          images: 'Stock',
        },
        settings: {
          autoSave: true,
          notifications: true,
          telemetry: false,
        },
      };

      localStorage.setItem(CONFIG_STORAGE_KEY, JSON.stringify(defaultConfig));
      localStorage.setItem(CONFIG_VERSION_KEY, CURRENT_CONFIG_VERSION);

      loggingService.info(
        'Configuration reset to defaults',
        'ConfigurationRecoveryService',
        'resetToDefaults'
      );

      return true;
    } catch (error: unknown) {
      loggingService.error(
        'Failed to reset configuration',
        error instanceof Error ? error : new Error(String(error)),
        'ConfigurationRecoveryService',
        'resetToDefaults'
      );
      return false;
    }
  }

  /**
   * Export configuration to JSON file
   */
  public async exportConfiguration(): Promise<void> {
    try {
      const configStr = localStorage.getItem(CONFIG_STORAGE_KEY);
      const version = localStorage.getItem(CONFIG_VERSION_KEY);

      const exportData = {
        version: version || CURRENT_CONFIG_VERSION,
        timestamp: new Date().toISOString(),
        config: configStr ? JSON.parse(configStr) : {},
      };

      const blob = new Blob([JSON.stringify(exportData, null, 2)], {
        type: 'application/json',
      });

      const url = URL.createObjectURL(blob);
      const a = document.createElement('a');
      a.href = url;
      a.download = `aura-config-${new Date().toISOString().split('T')[0]}.json`;
      document.body.appendChild(a);
      a.click();
      document.body.removeChild(a);
      URL.revokeObjectURL(url);

      loggingService.info(
        'Configuration exported',
        'ConfigurationRecoveryService',
        'exportConfiguration'
      );
    } catch (error: unknown) {
      loggingService.error(
        'Failed to export configuration',
        error instanceof Error ? error : new Error(String(error)),
        'ConfigurationRecoveryService',
        'exportConfiguration'
      );
      throw error;
    }
  }

  /**
   * Import configuration from JSON file
   */
  public async importConfiguration(file: File): Promise<boolean> {
    try {
      const text = await file.text();
      const importData = JSON.parse(text);

      if (!importData.config || !importData.version) {
        throw new Error('Invalid configuration file format');
      }

      await this.backupConfiguration();

      localStorage.setItem(CONFIG_STORAGE_KEY, JSON.stringify(importData.config));
      localStorage.setItem(CONFIG_VERSION_KEY, importData.version);

      loggingService.info(
        'Configuration imported',
        'ConfigurationRecoveryService',
        'importConfiguration'
      );

      return true;
    } catch (error: unknown) {
      loggingService.error(
        'Failed to import configuration',
        error instanceof Error ? error : new Error(String(error)),
        'ConfigurationRecoveryService',
        'importConfiguration'
      );
      throw error;
    }
  }

  /**
   * List available backups
   */
  public listBackups(): ConfigBackup[] {
    try {
      const backupsStr = localStorage.getItem(CONFIG_BACKUP_KEY);
      return backupsStr ? JSON.parse(backupsStr) : [];
    } catch (error: unknown) {
      loggingService.error(
        'Failed to list backups',
        error instanceof Error ? error : new Error(String(error)),
        'ConfigurationRecoveryService',
        'listBackups'
      );
      return [];
    }
  }

  /**
   * Auto-recover from corrupted configuration
   */
  public async autoRecover(): Promise<boolean> {
    try {
      const validation = this.validateConfiguration();

      if (validation.valid) {
        return true;
      }

      loggingService.warn(
        'Configuration validation failed, attempting auto-recovery',
        'ConfigurationRecoveryService',
        'autoRecover',
        { errors: validation.errors }
      );

      const backups = this.listBackups();

      if (backups.length > 0) {
        loggingService.info(
          'Attempting to restore from most recent backup',
          'ConfigurationRecoveryService',
          'autoRecover'
        );

        const restored = await this.restoreFromBackup(0);

        if (restored) {
          const revalidation = this.validateConfiguration();
          if (revalidation.valid) {
            loggingService.info(
              'Configuration successfully recovered from backup',
              'ConfigurationRecoveryService',
              'autoRecover'
            );
            return true;
          }
        }
      }

      loggingService.warn(
        'Backup recovery failed, resetting to defaults',
        'ConfigurationRecoveryService',
        'autoRecover'
      );

      return await this.resetToDefaults();
    } catch (error: unknown) {
      loggingService.error(
        'Auto-recovery failed',
        error instanceof Error ? error : new Error(String(error)),
        'ConfigurationRecoveryService',
        'autoRecover'
      );
      return false;
    }
  }
}

export const configurationRecoveryService = new ConfigurationRecoveryService();
