import { describe, it, expect } from 'vitest';
import {
  onboardingReducer,
  initialOnboardingState,
  getButtonLabel,
  isButtonDisabled,
  canAdvanceStep,
  type WizardStatus,
} from '../onboarding';
import type { PreflightReport } from '../providers';

describe('onboardingReducer', () => {
  it('should return initial state', () => {
    const state = initialOnboardingState;
    expect(state.step).toBe(0);
    expect(state.mode).toBe('free');
    expect(state.status).toBe('idle');
    expect(state.lastValidation).toBeNull();
    expect(state.errors).toEqual([]);
  });

  it('should handle SET_STEP', () => {
    const state = onboardingReducer(initialOnboardingState, {
      type: 'SET_STEP',
      payload: 2,
    });
    expect(state.step).toBe(2);
  });

  it('should handle SET_MODE', () => {
    const state = onboardingReducer(initialOnboardingState, {
      type: 'SET_MODE',
      payload: 'local',
    });
    expect(state.mode).toBe('local');
  });

  it('should handle START_VALIDATION', () => {
    const state = onboardingReducer(initialOnboardingState, {
      type: 'START_VALIDATION',
    });
    expect(state.status).toBe('validating');
    expect(state.errors).toEqual([]);
    expect(state.lastValidation).toBeNull();
  });

  it('should handle VALIDATION_SUCCESS', () => {
    const report: PreflightReport = {
      ok: true,
      stages: [
        {
          stage: 'Script',
          status: 'pass',
          provider: 'RuleBased',
          message: 'All good',
        },
      ],
    };

    const state = onboardingReducer(initialOnboardingState, {
      type: 'VALIDATION_SUCCESS',
      payload: { report, correlationId: 'test-123' },
    });

    expect(state.status).toBe('valid');
    expect(state.lastValidation).not.toBeNull();
    expect(state.lastValidation?.correlationId).toBe('test-123');
    expect(state.lastValidation?.report).toBe(report);
    expect(state.lastValidation?.failedStages).toEqual([]);
    expect(state.errors).toEqual([]);
  });

  it('should handle VALIDATION_FAILED', () => {
    const report: PreflightReport = {
      ok: false,
      stages: [
        {
          stage: 'Script',
          status: 'fail',
          provider: 'OpenAI',
          message: 'API key not configured',
          hint: 'Add API key in Settings',
        },
        {
          stage: 'TTS',
          status: 'pass',
          provider: 'Windows',
          message: 'All good',
        },
      ],
    };

    const state = onboardingReducer(initialOnboardingState, {
      type: 'VALIDATION_FAILED',
      payload: { report, correlationId: 'test-456' },
    });

    expect(state.status).toBe('invalid');
    expect(state.lastValidation).not.toBeNull();
    expect(state.lastValidation?.correlationId).toBe('test-456');
    expect(state.lastValidation?.failedStages).toHaveLength(1);
    expect(state.lastValidation?.failedStages[0].stage).toBe('Script');
    expect(state.errors).toEqual(['API key not configured']);
  });

  it('should handle START_HARDWARE_DETECTION', () => {
    const state = onboardingReducer(initialOnboardingState, {
      type: 'START_HARDWARE_DETECTION',
    });
    expect(state.isDetectingHardware).toBe(true);
    expect(state.hardware).toBeNull();
  });

  it('should handle HARDWARE_DETECTED', () => {
    const hardware = {
      gpu: 'NVIDIA RTX 3080',
      vram: 10,
      canRunSD: true,
      recommendation: 'Your GPU can run SD!',
    };

    const state = onboardingReducer(initialOnboardingState, {
      type: 'HARDWARE_DETECTED',
      payload: hardware,
    });

    expect(state.isDetectingHardware).toBe(false);
    expect(state.hardware).toEqual(hardware);
  });

  it('should handle HARDWARE_DETECTION_FAILED', () => {
    const state = onboardingReducer(initialOnboardingState, {
      type: 'HARDWARE_DETECTION_FAILED',
      payload: 'Network error',
    });

    expect(state.isDetectingHardware).toBe(false);
    expect(state.hardware).toEqual({
      canRunSD: false,
      recommendation: 'Network error',
    });
  });

  it('should handle START_INSTALL', () => {
    const state = onboardingReducer(initialOnboardingState, {
      type: 'START_INSTALL',
      payload: 'ffmpeg',
    });

    expect(state.status).toBe('installing');
    const ffmpegItem = state.installItems.find(item => item.id === 'ffmpeg');
    expect(ffmpegItem?.installing).toBe(true);
  });

  it('should handle INSTALL_COMPLETE', () => {
    const installingState = onboardingReducer(initialOnboardingState, {
      type: 'START_INSTALL',
      payload: 'ffmpeg',
    });

    const state = onboardingReducer(installingState, {
      type: 'INSTALL_COMPLETE',
      payload: 'ffmpeg',
    });

    expect(state.status).toBe('installed');
    const ffmpegItem = state.installItems.find(item => item.id === 'ffmpeg');
    expect(ffmpegItem?.installing).toBe(false);
    expect(ffmpegItem?.installed).toBe(true);
  });

  it('should handle INSTALL_FAILED', () => {
    const installingState = onboardingReducer(initialOnboardingState, {
      type: 'START_INSTALL',
      payload: 'ffmpeg',
    });

    const state = onboardingReducer(installingState, {
      type: 'INSTALL_FAILED',
      payload: { itemId: 'ffmpeg', error: 'Download failed' },
    });

    expect(state.status).toBe('idle');
    const ffmpegItem = state.installItems.find(item => item.id === 'ffmpeg');
    expect(ffmpegItem?.installing).toBe(false);
    expect(state.errors).toContain('Failed to install ffmpeg: Download failed');
  });

  it('should handle MARK_READY', () => {
    const state = onboardingReducer(initialOnboardingState, {
      type: 'MARK_READY',
    });
    expect(state.status).toBe('ready');
  });

  it('should handle RESET_VALIDATION', () => {
    const validatedState = onboardingReducer(initialOnboardingState, {
      type: 'VALIDATION_SUCCESS',
      payload: {
        report: { ok: true, stages: [] },
        correlationId: 'test-123',
      },
    });

    const state = onboardingReducer(validatedState, {
      type: 'RESET_VALIDATION',
    });

    expect(state.status).toBe('idle');
    expect(state.lastValidation).toBeNull();
    expect(state.errors).toEqual([]);
  });
});

describe('getButtonLabel', () => {
  it('should return correct label for idle state on first step', () => {
    expect(getButtonLabel('idle', false)).toBe('Next');
  });

  it('should return correct label for idle state on last step', () => {
    expect(getButtonLabel('idle', true)).toBe('Validate');
  });

  it('should return correct label for validating state', () => {
    expect(getButtonLabel('validating', true)).toBe('Validating…');
  });

  it('should return correct label for valid state', () => {
    expect(getButtonLabel('valid', true)).toBe('Next');
  });

  it('should return correct label for invalid state', () => {
    expect(getButtonLabel('invalid', true)).toBe('Fix Issues');
  });

  it('should return correct label for installing state', () => {
    expect(getButtonLabel('installing', true)).toBe('Installing…');
  });

  it('should return correct label for installed state', () => {
    expect(getButtonLabel('installed', true)).toBe('Validate');
  });

  it('should return correct label for ready state', () => {
    expect(getButtonLabel('ready', true)).toBe('Continue');
  });
});

describe('isButtonDisabled', () => {
  it('should disable button when validating', () => {
    expect(isButtonDisabled('validating', false)).toBe(true);
  });

  it('should disable button when installing', () => {
    expect(isButtonDisabled('installing', false)).toBe(true);
  });

  it('should disable button when detecting hardware', () => {
    expect(isButtonDisabled('idle', true)).toBe(true);
  });

  it('should not disable button in idle state', () => {
    expect(isButtonDisabled('idle', false)).toBe(false);
  });

  it('should not disable button in valid state', () => {
    expect(isButtonDisabled('valid', false)).toBe(false);
  });

  it('should not disable button in invalid state', () => {
    expect(isButtonDisabled('invalid', false)).toBe(false);
  });
});

describe('canAdvanceStep', () => {
  it('should allow advance when status is valid', () => {
    expect(canAdvanceStep('valid')).toBe(true);
  });

  it('should allow advance when status is ready', () => {
    expect(canAdvanceStep('ready')).toBe(true);
  });

  it('should not allow advance when status is idle', () => {
    expect(canAdvanceStep('idle')).toBe(false);
  });

  it('should not allow advance when status is validating', () => {
    expect(canAdvanceStep('validating')).toBe(false);
  });

  it('should not allow advance when status is invalid', () => {
    expect(canAdvanceStep('invalid')).toBe(false);
  });

  it('should not allow advance when status is installing', () => {
    expect(canAdvanceStep('installing')).toBe(false);
  });
});

describe('State machine transitions', () => {
  it('should follow correct flow: idle → validating → valid → ready', () => {
    let state = initialOnboardingState;
    expect(state.status).toBe('idle');

    // Start validation
    state = onboardingReducer(state, { type: 'START_VALIDATION' });
    expect(state.status).toBe('validating');

    // Validation succeeds
    state = onboardingReducer(state, {
      type: 'VALIDATION_SUCCESS',
      payload: {
        report: { ok: true, stages: [] },
        correlationId: 'test',
      },
    });
    expect(state.status).toBe('valid');

    // Mark ready
    state = onboardingReducer(state, { type: 'MARK_READY' });
    expect(state.status).toBe('ready');
  });

  it('should follow correct flow: idle → validating → invalid', () => {
    let state = initialOnboardingState;
    expect(state.status).toBe('idle');

    // Start validation
    state = onboardingReducer(state, { type: 'START_VALIDATION' });
    expect(state.status).toBe('validating');

    // Validation fails
    state = onboardingReducer(state, {
      type: 'VALIDATION_FAILED',
      payload: {
        report: {
          ok: false,
          stages: [
            {
              stage: 'Test',
              status: 'fail',
              provider: 'TestProvider',
              message: 'Test failed',
            },
          ],
        },
        correlationId: 'test',
      },
    });
    expect(state.status).toBe('invalid');
  });

  it('should follow correct flow: idle → installing → installed', () => {
    let state = initialOnboardingState;
    expect(state.status).toBe('idle');

    // Start install
    state = onboardingReducer(state, {
      type: 'START_INSTALL',
      payload: 'ffmpeg',
    });
    expect(state.status).toBe('installing');

    // Install completes
    state = onboardingReducer(state, {
      type: 'INSTALL_COMPLETE',
      payload: 'ffmpeg',
    });
    expect(state.status).toBe('installed');
  });
});

describe('Path picker and skip functionality', () => {
  it('should handle SKIP_INSTALL action', () => {
    const state = onboardingReducer(initialOnboardingState, {
      type: 'SKIP_INSTALL',
      payload: 'ollama',
    });

    const ollamaItem = state.installItems.find(item => item.id === 'ollama');
    expect(ollamaItem?.skipped).toBe(true);
    expect(ollamaItem?.installed).toBe(false);
  });

  it('should handle SET_EXISTING_PATH action', () => {
    const state = onboardingReducer(initialOnboardingState, {
      type: 'SET_EXISTING_PATH',
      payload: { itemId: 'ffmpeg', path: 'C:\\Tools\\ffmpeg' },
    });

    const ffmpegItem = state.installItems.find(item => item.id === 'ffmpeg');
    expect(ffmpegItem?.existingPath).toBe('C:\\Tools\\ffmpeg');
    expect(ffmpegItem?.installed).toBe(true);
    expect(ffmpegItem?.skipped).toBe(false);
    expect(state.showingPathPicker).toBeUndefined();
  });

  it('should handle SHOW_PATH_PICKER action', () => {
    let state = onboardingReducer(initialOnboardingState, {
      type: 'SHOW_PATH_PICKER',
      payload: 'stable-diffusion',
    });
    expect(state.showingPathPicker).toBe('stable-diffusion');

    // Close path picker
    state = onboardingReducer(state, {
      type: 'SHOW_PATH_PICKER',
      payload: undefined,
    });
    expect(state.showingPathPicker).toBeUndefined();
  });

  it('should use existing path and close picker on SET_EXISTING_PATH', () => {
    let state = onboardingReducer(initialOnboardingState, {
      type: 'SHOW_PATH_PICKER',
      payload: 'piper',
    });
    expect(state.showingPathPicker).toBe('piper');

    state = onboardingReducer(state, {
      type: 'SET_EXISTING_PATH',
      payload: { itemId: 'piper', path: '/usr/local/bin/piper' },
    });

    const piperItem = state.installItems.find(item => item.id === 'piper');
    expect(piperItem?.existingPath).toBe('/usr/local/bin/piper');
    expect(piperItem?.installed).toBe(true);
    expect(state.showingPathPicker).toBeUndefined();
  });

  it('should allow skipping non-required items only', () => {
    const state = initialOnboardingState;
    
    // Check that ffmpeg is required
    const ffmpeg = state.installItems.find(item => item.id === 'ffmpeg');
    expect(ffmpeg?.required).toBe(true);

    // Check that other items are optional
    const ollama = state.installItems.find(item => item.id === 'ollama');
    expect(ollama?.required).toBe(false);

    const sd = state.installItems.find(item => item.id === 'stable-diffusion');
    expect(sd?.required).toBe(false);
  });
});
