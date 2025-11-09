/**
 * Wizard project state management with auto-save functionality
 */

import { create } from 'zustand';
import type {
  WizardProjectListItem,
  WizardProjectDetails,
  SaveWizardProjectRequest,
} from '../types/wizardProject';

export interface WizardProjectState {
  currentProject: WizardProjectDetails | null;
  projectList: WizardProjectListItem[];
  isLoading: boolean;
  isSaving: boolean;
  lastSaveTime: Date | null;
  autoSaveEnabled: boolean;
  autoSaveIntervalMs: number;

  setCurrentProject: (project: WizardProjectDetails | null) => void;
  setProjectList: (projects: WizardProjectListItem[]) => void;
  setLoading: (loading: boolean) => void;
  setSaving: (saving: boolean) => void;
  setLastSaveTime: (time: Date | null) => void;
  setAutoSaveEnabled: (enabled: boolean) => void;
  setAutoSaveInterval: (intervalMs: number) => void;

  updateCurrentProjectField: <K extends keyof WizardProjectDetails>(
    field: K,
    value: WizardProjectDetails[K]
  ) => void;

  clearCurrentProject: () => void;
  reset: () => void;
}

const initialState = {
  currentProject: null,
  projectList: [],
  isLoading: false,
  isSaving: false,
  lastSaveTime: null,
  autoSaveEnabled: true,
  autoSaveIntervalMs: 120000, // 2 minutes
};

export const useWizardProjectStore = create<WizardProjectState>((set) => ({
  ...initialState,

  setCurrentProject: (project) => set({ currentProject: project }),

  setProjectList: (projects) => set({ projectList: projects }),

  setLoading: (loading) => set({ isLoading: loading }),

  setSaving: (saving) => set({ isSaving: saving }),

  setLastSaveTime: (time) => set({ lastSaveTime: time }),

  setAutoSaveEnabled: (enabled) => set({ autoSaveEnabled: enabled }),

  setAutoSaveInterval: (intervalMs) => set({ autoSaveIntervalMs: intervalMs }),

  updateCurrentProjectField: (field, value) =>
    set((state) => ({
      currentProject: state.currentProject ? { ...state.currentProject, [field]: value } : null,
    })),

  clearCurrentProject: () => set({ currentProject: null, lastSaveTime: null }),

  reset: () => set(initialState),
}));

/**
 * Helper function to serialize wizard state to JSON for saving
 */
export function serializeWizardState(
  briefData: unknown,
  planData: unknown,
  voiceData: unknown,
  renderData: unknown
): SaveWizardProjectRequest {
  return {
    briefJson: briefData ? JSON.stringify(briefData) : undefined,
    planSpecJson: planData ? JSON.stringify(planData) : undefined,
    voiceSpecJson: voiceData ? JSON.stringify(voiceData) : undefined,
    renderSpecJson: renderData ? JSON.stringify(renderData) : undefined,
  } as SaveWizardProjectRequest;
}

/**
 * Helper function to deserialize project JSON back to wizard state
 */
export function deserializeWizardState(project: WizardProjectDetails): {
  brief: unknown;
  plan: unknown;
  voice: unknown;
  render: unknown;
} {
  return {
    brief: project.briefJson ? JSON.parse(project.briefJson) : null,
    plan: project.planSpecJson ? JSON.parse(project.planSpecJson) : null,
    voice: project.voiceSpecJson ? JSON.parse(project.voiceSpecJson) : null,
    render: project.renderSpecJson ? JSON.parse(project.renderSpecJson) : null,
  };
}

/**
 * Generate a default project name with timestamp
 */
export function generateDefaultProjectName(): string {
  const now = new Date();
  const dateStr = now.toLocaleDateString('en-US', {
    year: 'numeric',
    month: '2-digit',
    day: '2-digit',
  });
  const timeStr = now.toLocaleTimeString('en-US', {
    hour: '2-digit',
    minute: '2-digit',
    hour12: false,
  });
  return `Project ${dateStr} ${timeStr}`;
}
