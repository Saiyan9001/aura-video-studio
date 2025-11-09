/**
 * Hook for automatic saving of wizard state
 * Implements debounced auto-save with 30-second interval
 */

import { useEffect, useRef, useCallback, useState } from 'react';
import { saveWizardProject } from '../api/wizardProjects';
import { useNotifications } from '../components/Notifications/Toasts';
import { useWizardProjectStore } from '../state/wizardProject';
import type { SaveWizardProjectRequest } from '../types/wizardProject';

interface UseWizardAutoSaveOptions {
  enabled?: boolean;
  intervalMs?: number;
  projectId?: string;
  projectName: string;
  currentStep: number;
  briefJson?: string;
  planSpecJson?: string;
  voiceSpecJson?: string;
  renderSpecJson?: string;
  onSaveSuccess?: (projectId: string) => void;
  onSaveError?: (error: Error) => void;
}

interface UseWizardAutoSaveReturn {
  triggerManualSave: () => Promise<void>;
  isSaving: boolean;
  lastSaveTime: Date | null;
  saveError: Error | null;
}

/**
 * Auto-save wizard state every 30 seconds (configurable)
 * Debounces rapid changes to reduce API calls
 */
export function useWizardAutoSave(options: UseWizardAutoSaveOptions): UseWizardAutoSaveReturn {
  const {
    enabled = true,
    intervalMs = 30000, // 30 seconds
    projectId,
    projectName,
    currentStep,
    briefJson,
    planSpecJson,
    voiceSpecJson,
    renderSpecJson,
    onSaveSuccess,
    onSaveError,
  } = options;

  const { setSaving, setLastSaveTime, isSaving, lastSaveTime } = useWizardProjectStore();
  const { showSuccessToast, showFailureToast } = useNotifications();

  const [saveError, setSaveError] = useState<Error | null>(null);
  const autoSaveTimerRef = useRef<ReturnType<typeof setTimeout> | null>(null);
  const isSavingRef = useRef(false);
  const lastDataRef = useRef<string>('');

  // Create save payload
  const createSavePayload = useCallback((): SaveWizardProjectRequest => {
    return {
      id: projectId,
      name: projectName,
      currentStep,
      briefJson,
      planSpecJson,
      voiceSpecJson,
      renderSpecJson,
    };
  }, [projectId, projectName, currentStep, briefJson, planSpecJson, voiceSpecJson, renderSpecJson]);

  // Perform the save operation
  const performSave = useCallback(
    async (showNotification = false) => {
      if (isSavingRef.current) {
        return;
      }

      isSavingRef.current = true;
      setSaving(true);
      setSaveError(null);

      try {
        const payload = createSavePayload();
        const response = await saveWizardProject(payload);

        setLastSaveTime(new Date());
        isSavingRef.current = false;
        setSaving(false);

        if (showNotification) {
          showSuccessToast({
            title: 'Project Saved',
            message: `"${projectName}" saved successfully`,
          });
        }

        if (onSaveSuccess) {
          onSaveSuccess(response.id);
        }
      } catch (error: unknown) {
        const err = error instanceof Error ? error : new Error(String(error));
        isSavingRef.current = false;
        setSaving(false);
        setSaveError(err);

        if (showNotification) {
          showFailureToast({
            title: 'Save Failed',
            message: 'Failed to save project. Changes will be retried automatically.',
          });
        }

        if (onSaveError) {
          onSaveError(err);
        }
      }
    },
    [
      createSavePayload,
      projectName,
      setSaving,
      setLastSaveTime,
      showSuccessToast,
      showFailureToast,
      onSaveSuccess,
      onSaveError,
    ]
  );

  // Trigger manual save (with notification)
  const triggerManualSave = useCallback(async () => {
    await performSave(true);
  }, [performSave]);

  // Auto-save effect
  useEffect(() => {
    if (!enabled || !projectName) {
      return;
    }

    // Clear any existing timer
    if (autoSaveTimerRef.current) {
      clearTimeout(autoSaveTimerRef.current);
    }

    // Check if data has changed
    const currentData = JSON.stringify({
      projectName,
      currentStep,
      briefJson,
      planSpecJson,
      voiceSpecJson,
      renderSpecJson,
    });

    // Only schedule save if data changed
    if (currentData !== lastDataRef.current && lastDataRef.current !== '') {
      autoSaveTimerRef.current = setTimeout(() => {
        performSave(false);
      }, intervalMs);
    }

    lastDataRef.current = currentData;

    return () => {
      if (autoSaveTimerRef.current) {
        clearTimeout(autoSaveTimerRef.current);
      }
    };
  }, [
    enabled,
    projectName,
    currentStep,
    briefJson,
    planSpecJson,
    voiceSpecJson,
    renderSpecJson,
    intervalMs,
    performSave,
  ]);

  return {
    triggerManualSave,
    isSaving,
    lastSaveTime,
    saveError,
  };
}
