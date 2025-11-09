/**
 * Integration tests for wizard project save/load functionality
 */

import { describe, it, expect } from 'vitest';
import * as wizardProjectsApi from '../../src/api/wizardProjects';
import { AutoSaveIndicator } from '../../src/components/AutoSaveIndicator';
import { WizardProjectsTab } from '../../src/components/WizardProjectsTab';
import { useWizardAutoSave } from '../../src/hooks/useWizardAutoSave';
import { useWizardProjectStore } from '../../src/state/wizardProject';
import * as wizardProjectTypes from '../../src/types/wizardProject';
import { formatDistanceToNow } from '../../src/utils/dateFormatter';

describe('Wizard Project Save/Load Integration', () => {
  describe('API endpoints', () => {
    it('should have wizard projects API endpoints available', () => {
      expect(wizardProjectsApi.saveWizardProject).toBeDefined();
      expect(wizardProjectsApi.getWizardProject).toBeDefined();
      expect(wizardProjectsApi.getAllWizardProjects).toBeDefined();
      expect(wizardProjectsApi.getRecentWizardProjects).toBeDefined();
      expect(wizardProjectsApi.duplicateWizardProject).toBeDefined();
      expect(wizardProjectsApi.deleteWizardProject).toBeDefined();
      expect(wizardProjectsApi.exportWizardProject).toBeDefined();
      expect(wizardProjectsApi.importWizardProject).toBeDefined();
    });
  });

  describe('State management', () => {
    it('should have wizard project store available', () => {
      expect(useWizardProjectStore).toBeDefined();
    });

    it('should have auto-save hook available', () => {
      expect(useWizardAutoSave).toBeDefined();
    });
  });

  describe('Components', () => {
    it('should have WizardProjectsTab component available', () => {
      expect(WizardProjectsTab).toBeDefined();
    });

    it('should have AutoSaveIndicator component available', () => {
      expect(AutoSaveIndicator).toBeDefined();
    });
  });

  describe('Utilities', () => {
    it('should have date formatting utility', () => {
      expect(formatDistanceToNow).toBeDefined();

      const now = new Date();
      const result = formatDistanceToNow(now);
      expect(result).toBe('just now');

      const fiveMinutesAgo = new Date(Date.now() - 5 * 60 * 1000);
      const result2 = formatDistanceToNow(fiveMinutesAgo);
      expect(result2).toBe('5 minutes ago');
    });
  });

  describe('Type definitions', () => {
    it('should have wizard project types defined', () => {
      expect(wizardProjectTypes).toBeDefined();
    });
  });
});
