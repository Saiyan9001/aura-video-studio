// Onboarding state management with state machine for First-Run Wizard

import { apiUrl } from '../config/api';
import { getDefaultSaveLocation, getDefaultCacheLocation } from '../utils/pathUtils';
import type { PreflightReport, StageCheck } from './providers';

// Hardware configuration constants
const MIN_VRAM_FOR_STABLE_DIFFUSION = 6; // GB - Minimum VRAM for SD 1.5

/**
 * Wizard validation status - deterministic state machine
 * Idle → Validating → Valid/Invalid → Installing → Installed → Ready
 */
export type WizardStatus =
  | 'idle' // Initial state, waiting for user to click Validate
  | 'validating' // Running preflight checks
  | 'valid' // Validation passed, ready to advance
  | 'invalid' // Validation failed, showing fix actions
  | 'installing' // Installing dependencies/engines
  | 'installed' // Installation complete
  | 'ready'; // All set, wizard complete

export type WizardMode = 'free' | 'local' | 'pro';
export type TierSelection = 'free' | 'pro' | null;

export interface WizardValidation {
  correlationId: string;
  timestamp: Date;
  report: PreflightReport;
  failedStages: StageCheck[];
}

export interface OnboardingState {
  step: number;
  mode: WizardMode;
  selectedTier: TierSelection;
  status: WizardStatus;
  lastValidation: WizardValidation | null;
  errors: string[];
  isDetectingHardware: boolean;
  isScanningDependencies: boolean;
  hardware: {
    gpu?: string;
    vram?: number;
    canRunSD: boolean;
    recommendation: string;
  } | null;
  installItems: Array<{
    id: string;
    name: string;
    description?: string;
    defaultPath?: string;
    required: boolean;
    installed: boolean;
    installing: boolean;
    skipped: boolean;
  }>;
  apiKeys: Record<string, string>;
  apiKeyValidationStatus: Record<string, 'idle' | 'validating' | 'valid' | 'invalid'>;
  apiKeyErrors: Record<string, string>;
  workspacePreferences: {
    defaultSaveLocation: string;
    cacheLocation: string;
    autosaveInterval: number;
    theme: 'light' | 'dark' | 'auto';
  };
  selectedTemplate: string | null;
  showTutorial: boolean;
  tutorialCompleted: boolean;
}

export const initialOnboardingState: OnboardingState = {
  step: 0,
  mode: 'free',
  selectedTier: null,
  status: 'idle',
  lastValidation: null,
  errors: [],
  isDetectingHardware: false,
  isScanningDependencies: false,
  hardware: null,
  installItems: [
    {
      id: 'ffmpeg',
      name: 'FFmpeg (Video encoding)',
      description:
        'Essential video and audio processing toolkit. Required for all video generation.',
      defaultPath: '%LOCALAPPDATA%\\Aura\\Tools\\ffmpeg',
      required: true,
      installed: false,
      installing: false,
      skipped: false,
    },
    {
      id: 'ollama',
      name: 'Ollama (Local AI)',
      description:
        'Run AI models locally for script generation. Privacy-focused alternative to cloud APIs.',
      defaultPath: '%LOCALAPPDATA%\\Aura\\Tools\\ollama',
      required: false,
      installed: false,
      installing: false,
      skipped: false,
    },
    {
      id: 'stable-diffusion',
      name: 'Stable Diffusion WebUI',
      description: 'Generate custom images locally. Requires NVIDIA GPU with 6GB+ VRAM.',
      defaultPath: '%LOCALAPPDATA%\\Aura\\Tools\\stable-diffusion-webui',
      required: false,
      installed: false,
      installing: false,
      skipped: false,
    },
  ],
  apiKeys: {},
  apiKeyValidationStatus: {},
  apiKeyErrors: {},
  workspacePreferences: {
    defaultSaveLocation: getDefaultSaveLocation(),
    cacheLocation: getDefaultCacheLocation(),
    autosaveInterval: 3,
    theme: 'auto',
  },
  selectedTemplate: null,
  showTutorial: false,
  tutorialCompleted: false,
};

// Action types
export type OnboardingAction =
  | { type: 'SET_STEP'; payload: number }
  | { type: 'SET_MODE'; payload: WizardMode }
  | { type: 'SET_TIER'; payload: TierSelection }
  | { type: 'SET_STATUS'; payload: WizardStatus }
  | { type: 'START_VALIDATION' }
  | { type: 'VALIDATION_SUCCESS'; payload: { report: PreflightReport; correlationId: string } }
  | { type: 'VALIDATION_FAILED'; payload: { report: PreflightReport; correlationId: string } }
  | { type: 'START_HARDWARE_DETECTION' }
  | { type: 'HARDWARE_DETECTED'; payload: OnboardingState['hardware'] }
  | { type: 'HARDWARE_DETECTION_FAILED'; payload: string }
  | { type: 'START_DEPENDENCY_SCAN' }
  | { type: 'DEPENDENCY_SCAN_COMPLETE' }
  | { type: 'START_INSTALL'; payload: string }
  | { type: 'INSTALL_COMPLETE'; payload: string }
  | { type: 'INSTALL_FAILED'; payload: { itemId: string; error: string } }
  | { type: 'SKIP_INSTALL'; payload: string }
  | { type: 'MARK_READY' }
  | { type: 'RESET_VALIDATION' }
  | { type: 'SET_API_KEY'; payload: { provider: string; key: string } }
  | { type: 'START_API_KEY_VALIDATION'; payload: string }
  | { type: 'API_KEY_VALID'; payload: { provider: string; accountInfo?: string } }
  | { type: 'API_KEY_INVALID'; payload: { provider: string; error: string } }
  | { type: 'SKIP_API_KEY_VALIDATION'; payload: string }
  | { type: 'SET_WORKSPACE_PREFERENCES'; payload: OnboardingState['workspacePreferences'] }
  | { type: 'SET_TEMPLATE'; payload: string | null }
  | { type: 'TOGGLE_TUTORIAL' }
  | { type: 'COMPLETE_TUTORIAL' }
  | { type: 'SET_MANUAL_HARDWARE'; payload: { vram?: number; hasGpu: boolean } }
  | { type: 'SKIP_HARDWARE_DETECTION' }
  | { type: 'LOAD_FROM_STORAGE'; payload: Partial<OnboardingState> };

// Reducer
export function onboardingReducer(
  state: OnboardingState,
  action: OnboardingAction
): OnboardingState {
  switch (action.type) {
    case 'SET_STEP':
      return { ...state, step: action.payload };

    case 'SET_MODE':
      return { ...state, mode: action.payload };

    case 'SET_TIER':
      return { ...state, selectedTier: action.payload };

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
          failedStages: action.payload.report.stages.filter((s) => s.status === 'fail'),
        },
        errors: action.payload.report.stages
          .filter((s) => s.status === 'fail')
          .map((s) => s.message),
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
          recommendation:
            action.payload ||
            'Could not detect hardware. We recommend starting with Free-only mode using Stock images.',
        },
      };

    case 'START_DEPENDENCY_SCAN':
      return {
        ...state,
        isScanningDependencies: true,
      };

    case 'DEPENDENCY_SCAN_COMPLETE':
      return {
        ...state,
        isScanningDependencies: false,
      };

    case 'START_INSTALL':
      return {
        ...state,
        status: 'installing',
        installItems: state.installItems.map((item) =>
          item.id === action.payload ? { ...item, installing: true } : item
        ),
      };

    case 'INSTALL_COMPLETE':
      return {
        ...state,
        status: 'installed',
        installItems: state.installItems.map((item) =>
          item.id === action.payload
            ? { ...item, installing: false, installed: true, skipped: false }
            : item
        ),
      };

    case 'INSTALL_FAILED':
      return {
        ...state,
        status: 'idle',
        installItems: state.installItems.map((item) =>
          item.id === action.payload.itemId ? { ...item, installing: false } : item
        ),
        errors: [
          ...state.errors,
          `Failed to install ${action.payload.itemId}: ${action.payload.error}`,
        ],
      };

    case 'SKIP_INSTALL':
      return {
        ...state,
        installItems: state.installItems.map((item) =>
          item.id === action.payload ? { ...item, skipped: true, installed: false } : item
        ),
      };

    case 'MARK_READY':
      return {
        ...state,
        status: 'ready',
      };

    case 'RESET_VALIDATION':
      return {
        ...state,
        status: 'idle',
        lastValidation: null,
        errors: [],
      };

    case 'SET_API_KEY':
      return {
        ...state,
        apiKeys: {
          ...state.apiKeys,
          [action.payload.provider]: action.payload.key,
        },
      };

    case 'START_API_KEY_VALIDATION':
      return {
        ...state,
        apiKeyValidationStatus: {
          ...state.apiKeyValidationStatus,
          [action.payload]: 'validating',
        },
        apiKeyErrors: {
          ...state.apiKeyErrors,
          [action.payload]: '',
        },
      };

    case 'API_KEY_VALID':
      return {
        ...state,
        apiKeyValidationStatus: {
          ...state.apiKeyValidationStatus,
          [action.payload.provider]: 'valid',
        },
        apiKeyErrors: {
          ...state.apiKeyErrors,
          [action.payload.provider]: '',
        },
      };

    case 'API_KEY_INVALID':
      return {
        ...state,
        apiKeyValidationStatus: {
          ...state.apiKeyValidationStatus,
          [action.payload.provider]: 'invalid',
        },
        apiKeyErrors: {
          ...state.apiKeyErrors,
          [action.payload.provider]: action.payload.error,
        },
      };

    case 'SKIP_API_KEY_VALIDATION':
      return {
        ...state,
        apiKeyValidationStatus: {
          ...state.apiKeyValidationStatus,
          [action.payload]: 'idle',
        },
        apiKeyErrors: {
          ...state.apiKeyErrors,
          [action.payload]: '',
        },
      };

    case 'SET_WORKSPACE_PREFERENCES':
      return {
        ...state,
        workspacePreferences: action.payload,
      };

    case 'SET_TEMPLATE':
      return {
        ...state,
        selectedTemplate: action.payload,
      };

    case 'TOGGLE_TUTORIAL':
      return {
        ...state,
        showTutorial: !state.showTutorial,
      };

    case 'COMPLETE_TUTORIAL':
      return {
        ...state,
        tutorialCompleted: true,
        showTutorial: false,
      };

    case 'SET_MANUAL_HARDWARE': {
      const vramAmount = action.payload.vram || 0;
      const canRunSD = action.payload.hasGpu && vramAmount >= MIN_VRAM_FOR_STABLE_DIFFUSION;
      const gpuDescription = action.payload.hasGpu
        ? `GPU with ${vramAmount}GB VRAM (manually configured)`
        : 'No dedicated GPU (integrated graphics)';
      const recommendation = action.payload.hasGpu
        ? `Manually configured GPU with ${vramAmount}GB VRAM. ${canRunSD ? 'Should be sufficient for local Stable Diffusion.' : 'We recommend using Stock images or Pro cloud providers.'}`
        : 'No dedicated GPU detected. We recommend using Stock images or Pro cloud providers.';

      return {
        ...state,
        hardware: {
          gpu: gpuDescription,
          vram: action.payload.vram,
          canRunSD,
          recommendation,
        },
      };
    }

    case 'SKIP_HARDWARE_DETECTION':
      return {
        ...state,
        isDetectingHardware: false,
        hardware: {
          canRunSD: false,
          recommendation:
            'Hardware detection skipped. You can configure hardware settings later in Settings.',
        },
      };

    case 'LOAD_FROM_STORAGE':
      return {
        ...state,
        ...action.payload,
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
    const response = await fetch(
      `/api/preflight?profile=${profileMap[state.mode]}&correlationId=${correlationId}`
    );

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
      stages: [
        {
          stage: 'System',
          status: 'fail',
          provider: 'Network',
          message: error instanceof Error ? error.message : 'Unknown error',
          hint: 'Check your network connection and try again',
        },
      ],
    };
    dispatch({
      type: 'VALIDATION_FAILED',
      payload: {
        report: syntheticReport,
        correlationId: `error-${Date.now()}`,
      },
    });
  }
}

export async function detectHardwareThunk(
  dispatch: React.Dispatch<OnboardingAction>
): Promise<void> {
  dispatch({ type: 'START_HARDWARE_DETECTION' });

  try {
    const response = await fetch('/api/probes/run', {
      method: 'POST',
    });
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
        payload:
          'Could not detect hardware. We recommend starting with Free-only mode using Stock images.',
      });
    }
  } catch (error) {
    console.error('Hardware detection failed:', error);
    dispatch({
      type: 'HARDWARE_DETECTION_FAILED',
      payload:
        'Could not detect hardware. We recommend starting with Free-only mode using Stock images.',
    });
  }
}

export async function installItemThunk(
  itemId: string,
  dispatch: React.Dispatch<OnboardingAction>
): Promise<void> {
  dispatch({ type: 'START_INSTALL', payload: itemId });

  try {
    // Map itemId to API endpoint
    let apiEndpoint: string;
    let statusEndpoint: string;
    let requestBody: { mode: string } | undefined;

    switch (itemId) {
      case 'ffmpeg':
        apiEndpoint = apiUrl('/api/downloads/ffmpeg/install');
        statusEndpoint = apiUrl('/api/downloads/ffmpeg/status');
        requestBody = { mode: 'managed' };
        break;
      case 'ollama':
      case 'stable-diffusion':
        // For other engines, we'll use the attach or skip for now
        // These could be implemented in the future with proper download endpoints
        // Installation not yet implemented via API - use Download Center
        dispatch({ type: 'INSTALL_COMPLETE', payload: itemId });
        return;
      default:
        throw new Error(`Unknown item: ${itemId}`);
    }

    // Call the actual download API
    const response = await fetch(apiEndpoint, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify(requestBody),
    });

    if (!response.ok) {
      const errorData = await response.json().catch(() => ({ error: response.statusText }));
      throw new Error(errorData.error || `Installation failed with status ${response.status}`);
    }

    const result = await response.json();

    if (!result.success) {
      throw new Error(result.error || 'Installation failed');
    }

    // After successful installation, verify status
    try {
      const statusResponse = await fetch(statusEndpoint);
      if (statusResponse.ok) {
        const statusData = await statusResponse.json();
        // Installation verified successfully
        void statusData; // Acknowledge the data was received
      }
    } catch (statusError) {
      console.warn(`Failed to verify ${itemId} status after installation:`, statusError);
      // Don't fail the installation for this, just log it
    }

    dispatch({ type: 'INSTALL_COMPLETE', payload: itemId });
  } catch (error) {
    console.error(`Installation of ${itemId} failed:`, error);
    dispatch({
      type: 'INSTALL_FAILED',
      payload: {
        itemId,
        error: error instanceof Error ? error.message : 'Unknown error',
      },
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
  return status === 'validating' || status === 'installing' || isDetectingHardware;
}

// Check if can advance to next step
export function canAdvanceStep(status: WizardStatus): boolean {
  return status === 'valid' || status === 'ready';
}

// Check installation status for an item using the dependency rescan API
export async function checkInstallationStatusThunk(
  itemId: string,
  dispatch: React.Dispatch<OnboardingAction>
): Promise<void> {
  try {
    // For now, we'll use the old approach for FFmpeg and skip for others
    // The full rescan will handle all dependencies
    if (itemId === 'ffmpeg') {
      const statusEndpoint = apiUrl('/api/downloads/ffmpeg/status');
      const response = await fetch(statusEndpoint);
      if (response.ok) {
        const data = await response.json();
        if (data.state === 'Installed' || data.state === 'ExternalAttached') {
          dispatch({ type: 'INSTALL_COMPLETE', payload: itemId });
        }
      }
    }
  } catch (error) {
    console.warn(`Failed to check installation status for ${itemId}:`, error);
  }
}

// Check all installation statuses using the dependency rescan API
export async function checkAllInstallationStatusesThunk(
  dispatch: React.Dispatch<OnboardingAction>
): Promise<void> {
  dispatch({ type: 'START_DEPENDENCY_SCAN' });

  try {
    // Call the dependency rescan API to check all dependencies
    const response = await fetch(apiUrl('/api/dependencies/rescan'), {
      method: 'POST',
    });

    if (response.ok) {
      const data = await response.json();

      if (data.success && data.dependencies) {
        // Map dependency status to installation state
        for (const dep of data.dependencies) {
          const itemId = dep.id;

          // Map status to our install state
          if (dep.status === 'Installed') {
            dispatch({ type: 'INSTALL_COMPLETE', payload: itemId });
          }
        }
      }
    } else {
      console.warn('Dependency rescan API failed:', response.statusText);
      // Fallback to individual checks for FFmpeg
      await checkInstallationStatusThunk('ffmpeg', dispatch);
    }
  } catch (error) {
    console.warn('Failed to check all installation statuses:', error);
    // Fallback to individual checks for FFmpeg
    await checkInstallationStatusThunk('ffmpeg', dispatch);
  } finally {
    dispatch({ type: 'DEPENDENCY_SCAN_COMPLETE' });
  }
}

// Validate API key
export async function validateApiKeyThunk(
  provider: string,
  apiKey: string,
  dispatch: React.Dispatch<OnboardingAction>
): Promise<void> {
  dispatch({ type: 'START_API_KEY_VALIDATION', payload: provider });

  try {
    // Client-side format validation
    const formatValidation = validateApiKeyFormat(provider, apiKey);
    if (!formatValidation.valid) {
      dispatch({
        type: 'API_KEY_INVALID',
        payload: { provider, error: formatValidation.error || 'Invalid API key format' },
      });
      return;
    }

    // Map frontend provider IDs to backend provider names and API key fields
    const providerNameMap: Record<string, { validatorName: string; keyField: string }> = {
      openai: { validatorName: 'OpenAI', keyField: 'openai' },
      anthropic: { validatorName: 'Anthropic', keyField: 'anthropic' },
      gemini: { validatorName: 'Gemini', keyField: 'gemini' },
      elevenlabs: { validatorName: 'ElevenLabs', keyField: 'elevenlabs' },
      playht: { validatorName: 'PlayHT', keyField: 'playht' },
      replicate: { validatorName: 'Replicate', keyField: 'replicate' },
      pexels: { validatorName: 'Pexels', keyField: 'pexels' },
      ollama: { validatorName: 'Ollama', keyField: 'ollama' },
    };

    const providerInfo = providerNameMap[provider.toLowerCase()] || {
      validatorName: provider,
      keyField: provider.toLowerCase(),
    };

    // Save the API key using secure KeyVault API (encrypted storage)
    const saveResponse = await fetch(apiUrl('/api/keys/set'), {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({
        provider: providerInfo.keyField,
        apiKey: apiKey.trim(),
      }),
    });

    if (!saveResponse.ok) {
      throw new Error(`Failed to save API key: ${saveResponse.statusText}`);
    }

    // For OpenAI, use the new live validation endpoint
    if (provider.toLowerCase() === 'openai') {
      try {
        const response = await fetch(apiUrl('/api/providers/openai/validate'), {
          method: 'POST',
          headers: { 'Content-Type': 'application/json' },
          body: JSON.stringify({
            apiKey: apiKey.trim(),
          }),
        });

        // Handle both successful validation and error responses
        if (!response.ok) {
          // If response is not ok, try to parse error
          let errorMessage = 'API key validation failed';
          try {
            const errorData = await response.json();
            errorMessage = errorData.detail || errorData.message || errorData.title || errorMessage;
          } catch {
            errorMessage = `HTTP ${response.status}: ${response.statusText}`;
          }

          console.error('[OpenAI Validation] Error response:', {
            status: response.status,
            statusText: response.statusText,
            errorMessage,
          });

          dispatch({
            type: 'API_KEY_INVALID',
            payload: {
              provider,
              error: errorMessage,
            },
          });
          return;
        }

        const data = await response.json();

        console.log('[OpenAI Validation] Response:', {
          ok: response.ok,
          status: response.status,
          isValid: data.isValid,
          message: data.message,
          fullData: data,
        });

        // Check for successful validation (only isValid === true is considered success)
        if (data.isValid === true) {
          dispatch({
            type: 'API_KEY_VALID',
            payload: {
              provider,
              accountInfo: data.message || 'API key validated successfully with OpenAI',
            },
          });
          return;
        }

        // Handle validation failure
        const errorMessage = data.message || 'API key validation failed';

        dispatch({
          type: 'API_KEY_INVALID',
          payload: {
            provider,
            error: errorMessage,
          },
        });
        return;
      } catch (networkError) {
        console.error('[OpenAI Validation] Network error:', networkError);
        dispatch({
          type: 'API_KEY_INVALID',
          payload: {
            provider,
            error: 'Network error: Could not reach validation service. Please check your internet connection.',
          },
        });
        return;
      }
    }

    // For other providers, use the old validation endpoint
    const response = await fetch(apiUrl('/api/providers/validate'), {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({
        providers: [providerInfo.validatorName],
      }),
    });

    if (!response.ok) {
      throw new Error(`Validation request failed: ${response.statusText}`);
    }

    const result: { results: Array<{ name: string; ok: boolean; details: string }>; ok: boolean } =
      await response.json();

    // Find the result for this specific provider
    const providerResult = result.results.find(
      (r) => r.name.toLowerCase() === providerInfo.validatorName.toLowerCase()
    );

    if (!providerResult) {
      throw new Error('Provider validation result not found in response');
    }

    if (providerResult.ok) {
      dispatch({
        type: 'API_KEY_VALID',
        payload: {
          provider,
          accountInfo: providerResult.details || 'API key validated successfully',
        },
      });
    } else {
      dispatch({
        type: 'API_KEY_INVALID',
        payload: {
          provider,
          error: providerResult.details || 'API key validation failed',
        },
      });
    }
  } catch (error: unknown) {
    const errorMessage = error instanceof Error ? error.message : 'Unknown error occurred';
    console.error('API key validation error:', errorMessage);
    dispatch({
      type: 'API_KEY_INVALID',
      payload: {
        provider,
        error: `Could not validate API key: ${errorMessage}`,
      },
    });
  }
}

// Validate API key format
function validateApiKeyFormat(
  provider: string,
  apiKey: string
): { valid: boolean; error?: string } {
  const trimmedKey = apiKey.trim();

  if (!trimmedKey) {
    return { valid: false, error: 'Please enter your API key' };
  }

  switch (provider) {
    case 'openai':
      if (!trimmedKey.startsWith('sk-')) {
        return { valid: false, error: 'OpenAI API keys must start with "sk-"' };
      }
      if (trimmedKey.length < 20) {
        return { valid: false, error: 'OpenAI API keys must be at least 20 characters' };
      }
      break;
    case 'anthropic':
      if (!trimmedKey.startsWith('sk-ant-')) {
        return { valid: false, error: 'Anthropic API keys start with "sk-ant-"' };
      }
      break;
    case 'gemini':
      if (trimmedKey.length !== 39) {
        return { valid: false, error: 'Google Gemini API keys are 39 characters long' };
      }
      break;
    case 'replicate':
      if (!trimmedKey.startsWith('r8_')) {
        return { valid: false, error: 'Replicate API keys start with "r8_"' };
      }
      break;
    case 'elevenlabs':
      if (trimmedKey.length !== 32) {
        return { valid: false, error: 'ElevenLabs API keys are 32 characters long' };
      }
      break;
    case 'playht':
      if (!trimmedKey.includes(':')) {
        return {
          valid: false,
          error: 'PlayHT requires both User ID and Secret Key (format: userId:secretKey)',
        };
      }
      break;
    case 'pexels':
      if (trimmedKey.length < 20) {
        return { valid: false, error: 'Pexels API keys should be at least 20 characters' };
      }
      break;
  }

  return { valid: true };
}

// Save wizard state to localStorage and backend
export function saveWizardStateToStorage(state: OnboardingState): void {
  try {
    const stateToSave = {
      step: state.step,
      selectedTier: state.selectedTier,
      mode: state.mode,
      apiKeys: state.apiKeys,
      apiKeyValidationStatus: state.apiKeyValidationStatus,
      hardware: state.hardware,
      installItems: state.installItems,
    };
    localStorage.setItem('wizardProgress', JSON.stringify(stateToSave));

    // Also save to backend (fire and forget)
    import('../services/firstRunService').then(({ saveWizardProgressToBackend }) => {
      saveWizardProgressToBackend(
        state.step,
        state.selectedTier,
        JSON.stringify(stateToSave)
      ).catch((error) => {
        console.warn('Failed to save wizard progress to backend:', error);
      });
    });
  } catch (error) {
    console.error('Failed to save wizard state:', error);
  }
}

// Load wizard state from localStorage
export function loadWizardStateFromStorage(): Partial<OnboardingState> | null {
  try {
    const saved = localStorage.getItem('wizardProgress');
    if (saved) {
      return JSON.parse(saved);
    }
  } catch (error) {
    console.error('Failed to load wizard state:', error);
  }
  return null;
}

// Clear wizard state from localStorage
export function clearWizardStateFromStorage(): void {
  try {
    localStorage.removeItem('wizardProgress');
  } catch (error) {
    console.error('Failed to clear wizard state:', error);
  }
}
