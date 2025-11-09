/**
 * Tests for ConfigurationRecoveryService
 */

import { describe, it, expect, beforeEach, afterEach, vi } from 'vitest';
import { configurationRecoveryService } from '../configurationRecoveryService';

describe('ConfigurationRecoveryService', () => {
  const CONFIG_STORAGE_KEY = 'aura_config';
  const CONFIG_BACKUP_KEY = 'aura_config_backup';
  const CONFIG_VERSION_KEY = 'aura_config_version';

  beforeEach(() => {
    localStorage.clear();
    vi.clearAllMocks();
  });

  afterEach(() => {
    localStorage.clear();
  });

  describe('validateConfiguration', () => {
    it('returns valid with warnings when no config exists', () => {
      const result = configurationRecoveryService.validateConfiguration();

      expect(result.valid).toBe(true);
      expect(result.errors).toHaveLength(0);
      expect(result.warnings).toHaveLength(1);
      expect(result.warnings[0]).toContain('No configuration found');
    });

    it('returns invalid when config is not valid JSON', () => {
      localStorage.setItem(CONFIG_STORAGE_KEY, 'invalid json{');

      const result = configurationRecoveryService.validateConfiguration();

      expect(result.valid).toBe(false);
      expect(result.errors).toHaveLength(1);
      expect(result.errors[0]).toContain('not valid JSON');
    });

    it('returns invalid when config is not an object', () => {
      localStorage.setItem(CONFIG_STORAGE_KEY, '"string value"');

      const result = configurationRecoveryService.validateConfiguration();

      expect(result.valid).toBe(false);
      expect(result.errors).toHaveLength(1);
      expect(result.errors[0]).toContain('not a valid object');
    });

    it('returns valid when config is a valid object', () => {
      const validConfig = { theme: 'dark', apiUrl: 'http://localhost:5005' };
      localStorage.setItem(CONFIG_STORAGE_KEY, JSON.stringify(validConfig));
      localStorage.setItem(CONFIG_VERSION_KEY, '1.0.0');

      const result = configurationRecoveryService.validateConfiguration();

      expect(result.valid).toBe(true);
      expect(result.errors).toHaveLength(0);
    });
  });

  describe('backupConfiguration', () => {
    it('returns false when no config to backup', async () => {
      const result = await configurationRecoveryService.backupConfiguration();

      expect(result).toBe(false);
    });

    it('creates backup when config exists', async () => {
      const config = { theme: 'dark' };
      localStorage.setItem(CONFIG_STORAGE_KEY, JSON.stringify(config));
      localStorage.setItem(CONFIG_VERSION_KEY, '1.0.0');

      const result = await configurationRecoveryService.backupConfiguration();

      expect(result).toBe(true);

      const backupsStr = localStorage.getItem(CONFIG_BACKUP_KEY);
      expect(backupsStr).toBeTruthy();

      const backups = JSON.parse(backupsStr!);
      expect(backups).toHaveLength(1);
      expect(backups[0].config).toEqual(config);
      expect(backups[0].version).toBe('1.0.0');
    });

    it('limits backups to maximum count', async () => {
      const config = { theme: 'dark' };
      localStorage.setItem(CONFIG_STORAGE_KEY, JSON.stringify(config));

      for (let i = 0; i < 7; i++) {
        await configurationRecoveryService.backupConfiguration();
      }

      const backupsStr = localStorage.getItem(CONFIG_BACKUP_KEY);
      const backups = JSON.parse(backupsStr!);

      expect(backups.length).toBeLessThanOrEqual(5);
    });
  });

  describe('restoreFromBackup', () => {
    it('returns false when no backups available', async () => {
      const result = await configurationRecoveryService.restoreFromBackup();

      expect(result).toBe(false);
    });

    it('restores from most recent backup by default', async () => {
      const oldConfig = { theme: 'light' };
      const newConfig = { theme: 'dark' };

      localStorage.setItem(CONFIG_STORAGE_KEY, JSON.stringify(oldConfig));
      await configurationRecoveryService.backupConfiguration();

      localStorage.setItem(CONFIG_STORAGE_KEY, JSON.stringify(newConfig));
      await configurationRecoveryService.backupConfiguration();

      localStorage.setItem(CONFIG_STORAGE_KEY, JSON.stringify({ theme: 'blue' }));

      const result = await configurationRecoveryService.restoreFromBackup(0);

      expect(result).toBe(true);

      const restoredStr = localStorage.getItem(CONFIG_STORAGE_KEY);
      const restored = JSON.parse(restoredStr!);
      expect(restored).toEqual(newConfig);
    });
  });

  describe('resetToDefaults', () => {
    it('creates backup before resetting', async () => {
      const config = { theme: 'dark' };
      localStorage.setItem(CONFIG_STORAGE_KEY, JSON.stringify(config));

      await configurationRecoveryService.resetToDefaults();

      const backups = configurationRecoveryService.listBackups();
      expect(backups.length).toBeGreaterThan(0);
    });

    it('sets default configuration', async () => {
      const result = await configurationRecoveryService.resetToDefaults();

      expect(result).toBe(true);

      const configStr = localStorage.getItem(CONFIG_STORAGE_KEY);
      const config = JSON.parse(configStr!);

      expect(config).toHaveProperty('theme');
      expect(config).toHaveProperty('apiUrl');
      expect(config).toHaveProperty('providers');
      expect(config).toHaveProperty('settings');
    });
  });

  describe('listBackups', () => {
    it('returns empty array when no backups', () => {
      const backups = configurationRecoveryService.listBackups();

      expect(backups).toEqual([]);
    });

    it('returns all available backups', async () => {
      localStorage.setItem(CONFIG_STORAGE_KEY, JSON.stringify({ theme: 'dark' }));

      await configurationRecoveryService.backupConfiguration();
      await configurationRecoveryService.backupConfiguration();

      const backups = configurationRecoveryService.listBackups();

      expect(backups.length).toBeGreaterThan(0);
      expect(backups[0]).toHaveProperty('timestamp');
      expect(backups[0]).toHaveProperty('config');
      expect(backups[0]).toHaveProperty('version');
    });
  });

  describe('autoRecover', () => {
    it('returns true when config is already valid', async () => {
      const validConfig = { theme: 'dark' };
      localStorage.setItem(CONFIG_STORAGE_KEY, JSON.stringify(validConfig));
      localStorage.setItem(CONFIG_VERSION_KEY, '1.0.0');

      const result = await configurationRecoveryService.autoRecover();

      expect(result).toBe(true);
    });

    it('attempts to restore from backup when config is invalid', async () => {
      const validConfig = { theme: 'dark' };
      localStorage.setItem(CONFIG_STORAGE_KEY, JSON.stringify(validConfig));
      await configurationRecoveryService.backupConfiguration();

      localStorage.setItem(CONFIG_STORAGE_KEY, 'invalid json{');

      const result = await configurationRecoveryService.autoRecover();

      expect(result).toBe(true);

      const restoredStr = localStorage.getItem(CONFIG_STORAGE_KEY);
      const restored = JSON.parse(restoredStr!);
      expect(restored).toEqual(validConfig);
    });

    it('resets to defaults when no valid backup exists', async () => {
      localStorage.setItem(CONFIG_STORAGE_KEY, 'invalid json{');

      const result = await configurationRecoveryService.autoRecover();

      expect(result).toBe(true);

      const configStr = localStorage.getItem(CONFIG_STORAGE_KEY);
      const config = JSON.parse(configStr!);
      expect(config).toHaveProperty('theme');
    });
  });
});
