// Onboarding state management with state machine for First-Run Wizard

import type { PreflightReport, StageCheck } from './providers';

/**
 * Wizard validation status - deterministic state machine
 * Idle → Validating → Valid/Invalid → Installing → Installed → Ready
 */
export type WizardStatus = 
  | 'idle'           // Initial state, waiting for user to click Validate
  | 'validating'     // Running preflight checks
  | 'valid'          // Validation passed, ready to advance
  | 'invalid'        // Validation failed, showing fix actions
  | 'installing'     // Installing dependencies/engines
  | 'installed'      // Installation complete
  | 'ready';         // All set, wizard complete

export type WizardMode = 'free' | 'local' | 'pro';

export interface WizardValidation {
  correlationId: string;
  timestamp: Date;
  report: PreflightReport;
  failedStages: StageCheck[];
}

export interface OnboardingState {
  step: number;
  mode: WizardMode;
  status: WizardStatus;
  lastValidation: WizardValidation | null;
  errors: string[];
  isDetectingHardware: boolean;
  hardware: {
    gpu?: string;
    vram?: number;
    canRunSD: boolean;
    recommendation: string;
  } | null;
  installItems: Array<{
    id: string;
    name: string;
    required: boolean;
    installed: boolean;
    installing: boolean;
    skipped: boolean;
    existingPath?: string;
  }>;
  showingPathPicker?: string; // engineId for which path picker is shown
}

export const initialOnboardingState: OnboardingState = {
  step: 0,
  mode: 'free',
  status: 'idle',
  lastValidation: null,
  errors: [],
  isDetectingHardware: false,
  hardware: null,
  installItems: [
    { id: 'ffmpeg', name: 'FFmpeg (Video encoding)', required: true, installed: false, installing: false, skipped: false },
    { id: 'ollama', name: 'Ollama (Local AI)', required: false, installed: false, installing: false, skipped: false },
    { id: 'stable-diffusion', name: 'Stable Diffusion WebUI', required: false, installed: false, installing: false, skipped: false },
    { id: 'piper', name: 'Piper TTS', required: false, installed: false, installing: false, skipped: false },
  ],
  showingPathPicker: undefined,
};

// Action types
export type OnboardingAction =
  | { type: 'SET_STEP'; payload: number }
  | { type: 'SET_MODE'; payload: WizardMode }
  | { type: 'SET_STATUS'; payload: WizardStatus }
  | { type: 'START_VALIDATION' }
  | { type: 'VALIDATION_SUCCESS'; payload: { report: PreflightReport; correlationId: string } }
  | { type: 'VALIDATION_FAILED'; payload: { report: PreflightReport; correlationId: string } }
  | { type: 'START_HARDWARE_DETECTION' }
  | { type: 'HARDWARE_DETECTED'; payload: OnboardingState['hardware'] }
  | { type: 'HARDWARE_DETECTION_FAILED'; payload: string }
  | { type: 'START_INSTALL'; payload: string }
  | { type: 'INSTALL_COMPLETE'; payload: string }
  | { type: 'INSTALL_FAILED'; payload: { itemId: string; error: string } }
  | { type: 'SKIP_INSTALL'; payload: string }
  | { type: 'SET_EXISTING_PATH'; payload: { itemId: string; path: string } }
  | { type: 'SHOW_PATH_PICKER'; payload: string | undefined }
  | { type: 'MARK_READY' }
  | { type: 'RESET_VALIDATION' };

// Reducer
export function onboardingReducer(state: OnboardingState, action: OnboardingAction): OnboardingState {
  switch (action.type) {
    case 'SET_STEP':
      return { ...state, step: action.payload };

    case 'SET_MODE':
      return { ...state, mode: action.payload };

    case 'SET_STATUS':
      return { ...state, status: action.payload };

    case 'START_VALIDATION':
      return {
        ...state,
        status: 'validating',
        errors: [],
        lastValidation: null,
      };

    case 'VALIDATION_SUCCESS':
      return {
        ...state,
        status: 'valid',
        lastValidation: {
          correlationId: action.payload.correlationId,
          timestamp: new Date(),
          report: action.payload.report,
          failedStages: [],
        },
        errors: [],
      };

    case 'VALIDATION_FAILED':
      return {
        ...state,
        status: 'invalid',
        lastValidation: {
          correlationId: action.payload.correlationId,
          timestamp: new Date(),
          report: action.payload.report,
          failedStages: action.payload.report.stages.filter(s => s.status === 'fail'),
        },
        errors: action.payload.report.stages
          .filter(s => s.status === 'fail')
          .map(s => s.message),
      };

    case 'START_HARDWARE_DETECTION':
      return {
        ...state,
        isDetectingHardware: true,
        hardware: null,
      };

    case 'HARDWARE_DETECTED':
      return {
        ...state,
        isDetectingHardware: false,
        hardware: action.payload,
      };

    case 'HARDWARE_DETECTION_FAILED':
      return {
        ...state,
        isDetectingHardware: false,
        hardware: {
          canRunSD: false,
          recommendation: action.payload || 'Could not detect hardware. We recommend starting with Free-only mode using Stock images.',
        },
      };

    case 'START_INSTALL':
      return {
        ...state,
        status: 'installing',
        installItems: state.installItems.map(item =>
          item.id === action.payload ? { ...item, installing: true } : item
        ),
      };

    case 'INSTALL_COMPLETE':
      return {
        ...state,
        status: 'installed',
        installItems: state.installItems.map(item =>
          item.id === action.payload ? { ...item, installing: false, installed: true } : item
        ),
      };

    case 'INSTALL_FAILED':
      return {
        ...state,
        status: 'idle',
        installItems: state.installItems.map(item =>
          item.id === action.payload.itemId ? { ...item, installing: false } : item
        ),
        errors: [...state.errors, `Failed to install ${action.payload.itemId}: ${action.payload.error}`],
      };

    case 'MARK_READY':
      return {
        ...state,
        status: 'ready',
      };

    case 'SKIP_INSTALL':
      return {
        ...state,
        installItems: state.installItems.map(item =>
          item.id === action.payload ? { ...item, skipped: true } : item
        ),
      };

    case 'SET_EXISTING_PATH':
      return {
        ...state,
        installItems: state.installItems.map(item =>
          item.id === action.payload.itemId 
            ? { ...item, existingPath: action.payload.path, installed: true, skipped: false } 
            : item
        ),
        showingPathPicker: undefined,
      };

    case 'SHOW_PATH_PICKER':
      return {
        ...state,
        showingPathPicker: action.payload,
      };

    case 'RESET_VALIDATION':
      return {
        ...state,
        status: 'idle',
        lastValidation: null,
        errors: [],
      };

    default:
      return state;
  }
}

// Thunks / async actions
export async function runValidationThunk(
  state: OnboardingState,
  dispatch: React.Dispatch<OnboardingAction>
): Promise<void> {
  dispatch({ type: 'START_VALIDATION' });

  try {
    const profileMap: Record<WizardMode, string> = {
      free: 'Free-Only',
      local: 'Balanced Mix',
      pro: 'Pro-Max',
    };

    const correlationId = `validation-${Date.now()}-${Math.random().toString(36).substring(7)}`;
    const response = await fetch(`/api/preflight?profile=${profileMap[state.mode]}&correlationId=${correlationId}`);

    if (!response.ok) {
      throw new Error(`Preflight check failed: ${response.statusText}`);
    }

    const report: PreflightReport = await response.json();

    if (report.ok) {
      dispatch({ type: 'VALIDATION_SUCCESS', payload: { report, correlationId } });
    } else {
      dispatch({ type: 'VALIDATION_FAILED', payload: { report, correlationId } });
    }
  } catch (error) {
    console.error('Validation failed:', error);
    // Create a synthetic failed report
    const syntheticReport: PreflightReport = {
      ok: false,
      stages: [{
        stage: 'System',
        status: 'fail',
        provider: 'Network',
        message: error instanceof Error ? error.message : 'Unknown error',
        hint: 'Check your network connection and try again',
      }],
    };
    dispatch({ 
      type: 'VALIDATION_FAILED', 
      payload: { 
        report: syntheticReport, 
        correlationId: `error-${Date.now()}` 
      } 
    });
  }
}

export async function detectHardwareThunk(
  dispatch: React.Dispatch<OnboardingAction>
): Promise<void> {
  dispatch({ type: 'START_HARDWARE_DETECTION' });

  try {
    const response = await fetch('/api/hardware/probe');
    if (response.ok) {
      const data = await response.json();

      const gpuInfo = data.gpu || 'Unknown GPU';
      const vramGB = data.vramGB || 0;
      const canRunSD = data.enableLocalDiffusion || false;

      let recommendation = '';
      if (canRunSD) {
        recommendation = `Your ${gpuInfo} with ${vramGB}GB VRAM can run Stable Diffusion locally!`;
      } else {
        recommendation = `Your system doesn't meet requirements for local Stable Diffusion. We recommend using Stock images or Pro cloud providers.`;
      }

      dispatch({
        type: 'HARDWARE_DETECTED',
        payload: {
          gpu: gpuInfo,
          vram: vramGB,
          canRunSD,
          recommendation,
        },
      });
    } else {
      dispatch({ 
        type: 'HARDWARE_DETECTION_FAILED', 
        payload: 'Could not detect hardware. We recommend starting with Free-only mode using Stock images.' 
      });
    }
  } catch (error) {
    console.error('Hardware detection failed:', error);
    dispatch({ 
      type: 'HARDWARE_DETECTION_FAILED', 
      payload: 'Could not detect hardware. We recommend starting with Free-only mode using Stock images.' 
    });
  }
}

export async function installItemThunk(
  itemId: string,
  dispatch: React.Dispatch<OnboardingAction>
): Promise<void> {
  dispatch({ type: 'START_INSTALL', payload: itemId });

  try {
    // Simulate installation (in real app, this would call the download API)
    await new Promise(resolve => setTimeout(resolve, 2000));

    dispatch({ type: 'INSTALL_COMPLETE', payload: itemId });
  } catch (error) {
    console.error(`Installation of ${itemId} failed:`, error);
    dispatch({ 
      type: 'INSTALL_FAILED', 
      payload: { 
        itemId, 
        error: error instanceof Error ? error.message : 'Unknown error' 
      } 
    });
  }
}

// Button label helper
export function getButtonLabel(status: WizardStatus, isLastStep: boolean): string {
  switch (status) {
    case 'idle':
      return isLastStep ? 'Validate' : 'Next';
    case 'validating':
      return 'Validating…';
    case 'valid':
      return 'Next';
    case 'invalid':
      return 'Fix Issues';
    case 'installing':
      return 'Installing…';
    case 'installed':
      return 'Validate';
    case 'ready':
      return 'Continue';
    default:
      return 'Next';
  }
}

// Button disabled helper
export function isButtonDisabled(status: WizardStatus, isDetectingHardware: boolean): boolean {
  return (
    status === 'validating' ||
    status === 'installing' ||
    isDetectingHardware
  );
}

// Check if can advance to next step
export function canAdvanceStep(status: WizardStatus): boolean {
  return status === 'valid' || status === 'ready';
}
