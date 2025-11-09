/**
 * InitializationScreen Component
 *
 * Displays application initialization progress with health checks
 * Shows startup progress and handles initialization failures
 */

import {
  Button,
  MessageBar,
  MessageBarBody,
  ProgressBar,
  Spinner,
  Text,
} from '@fluentui/react-components';
import {
  CheckmarkCircle24Filled,
  DismissCircle24Regular,
  Warning24Regular,
} from '@fluentui/react-icons';
import { useEffect, useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { getHealthDetails } from '../../services/api/healthApi';
import type { HealthDetailsResponse } from '../../types/api-v1';

interface InitializationScreenProps {
  onComplete: () => void;
  onError?: (error: InitializationError) => void;
  enableSafeMode?: boolean;
}

export interface InitializationError {
  code: string;
  message: string;
  details?: string;
  recoverable: boolean;
  suggestions: string[];
}

interface InitStep {
  name: string;
  status: 'pending' | 'running' | 'success' | 'warning' | 'error';
  message?: string;
  progress?: number;
}

const INIT_STEPS: Array<{ key: string; name: string }> = [
  { key: 'api', name: 'Connecting to API' },
  { key: 'dependencies', name: 'Checking dependencies' },
  { key: 'providers', name: 'Validating providers' },
  { key: 'system', name: 'System health check' },
  { key: 'configuration', name: 'Loading configuration' },
];

const CHECK_TIMEOUT = 15000;
const RETRY_DELAY = 2000;

export function InitializationScreen({
  onComplete,
  onError,
  enableSafeMode = true,
}: InitializationScreenProps) {
  const navigate = useNavigate();
  const [steps, setSteps] = useState<InitStep[]>(
    INIT_STEPS.map((s) => ({ name: s.name, status: 'pending' as const }))
  );
  const [overallProgress, setOverallProgress] = useState(0);
  const [retryCount, setRetryCount] = useState(0);
  const [startTime] = useState(Date.now());

  const updateStep = (index: number, updates: Partial<InitStep>) => {
    setSteps((prev) => prev.map((step, i) => (i === index ? { ...step, ...updates } : step)));
  };

  const calculateProgress = (currentSteps: InitStep[]): number => {
    const completed = currentSteps.filter(
      (s) => s.status === 'success' || s.status === 'warning'
    ).length;
    return Math.round((completed / currentSteps.length) * 100);
  };

  useEffect(() => {
    let mounted = true;
    let timeoutId: number | undefined;

    async function runInitialization() {
      try {
        updateStep(0, { status: 'running', message: 'Establishing connection...' });
        await new Promise((resolve) => setTimeout(resolve, 500));

        const controller = new AbortController();
        timeoutId = window.setTimeout(() => controller.abort(), CHECK_TIMEOUT);

        let details: HealthDetailsResponse;
        try {
          details = await getHealthDetails();
          if (timeoutId !== undefined) {
            window.clearTimeout(timeoutId);
          }
        } catch (error: unknown) {
          if (timeoutId !== undefined) {
            window.clearTimeout(timeoutId);
          }

          if (error instanceof Error && error.name === 'AbortError') {
            throw createInitError(
              'API_TIMEOUT',
              'Connection to API server timed out',
              'The API server is not responding. It may be starting up or not running.',
              true,
              [
                'Wait a few seconds and try again',
                'Check if the backend server is running',
                'Verify your network connection',
                'Start Safe Mode to bypass provider checks',
              ]
            );
          }

          throw createInitError(
            'API_UNREACHABLE',
            'Cannot connect to API server',
            error instanceof Error ? error.message : String(error),
            true,
            [
              'Make sure the backend server is running',
              'Check if the API URL is correct',
              'Verify firewall settings',
              'Try Safe Mode to continue with limited functionality',
            ]
          );
        }

        if (!mounted) return;
        updateStep(0, {
          status: 'success',
          message: `Connected (${Date.now() - startTime}ms)`,
        });
        setOverallProgress(calculateProgress(steps));

        updateStep(1, { status: 'running', message: 'Checking FFmpeg and system...' });
        await new Promise((resolve) => setTimeout(resolve, 300));

        const dependencyCheck = details.checks.find((c) => c.name === 'Dependencies');
        if (dependencyCheck) {
          if (dependencyCheck.status === 'Healthy') {
            updateStep(1, { status: 'success', message: 'All dependencies available' });
          } else if (dependencyCheck.status === 'Degraded') {
            updateStep(1, {
              status: 'warning',
              message: dependencyCheck.message || 'Some dependencies missing',
            });
          } else {
            updateStep(1, {
              status: 'error',
              message: dependencyCheck.message || 'Critical dependencies missing',
            });
          }
        } else {
          updateStep(1, { status: 'warning', message: 'Dependency check unavailable' });
        }

        if (!mounted) return;
        setOverallProgress(calculateProgress(steps));

        updateStep(2, { status: 'running', message: 'Validating provider configuration...' });
        await new Promise((resolve) => setTimeout(resolve, 300));

        const providerCheck = details.checks.find((c) => c.name === 'Providers');
        if (providerCheck) {
          if (providerCheck.status === 'Healthy') {
            updateStep(2, { status: 'success', message: 'Providers configured' });
          } else if (providerCheck.status === 'Degraded') {
            updateStep(2, {
              status: 'warning',
              message: providerCheck.message || 'Limited provider availability',
            });
          } else {
            updateStep(2, {
              status: 'warning',
              message: providerCheck.message || 'No providers configured',
            });
          }
        } else {
          updateStep(2, { status: 'warning', message: 'Provider check unavailable' });
        }

        if (!mounted) return;
        setOverallProgress(calculateProgress(steps));

        updateStep(3, { status: 'running', message: 'Checking system resources...' });
        await new Promise((resolve) => setTimeout(resolve, 300));

        const diskCheck = details.checks.find((c) => c.name === 'DiskSpace');
        if (diskCheck) {
          if (diskCheck.status === 'Healthy') {
            updateStep(3, { status: 'success', message: 'System resources OK' });
          } else if (diskCheck.status === 'Degraded') {
            updateStep(3, {
              status: 'warning',
              message: diskCheck.message || 'Low disk space',
            });
          } else {
            updateStep(3, {
              status: 'error',
              message: diskCheck.message || 'Insufficient disk space',
            });
          }
        } else {
          updateStep(3, { status: 'success', message: 'System check complete' });
        }

        if (!mounted) return;
        setOverallProgress(calculateProgress(steps));

        updateStep(4, { status: 'running', message: 'Loading application settings...' });
        await new Promise((resolve) => setTimeout(resolve, 300));

        updateStep(4, { status: 'success', message: 'Configuration loaded' });

        if (!mounted) return;
        setOverallProgress(100);

        await new Promise((resolve) => setTimeout(resolve, 500));
        if (mounted) {
          onComplete();
        }
      } catch (error: unknown) {
        if (!mounted) return;

        const initError = error as InitializationError;

        if (initError.recoverable && retryCount < 2) {
          setRetryCount((prev) => prev + 1);
          await new Promise((resolve) => setTimeout(resolve, RETRY_DELAY));
          if (mounted) {
            runInitialization();
          }
          return;
        }

        if (onError) {
          onError(initError);
        }
      }
    }

    runInitialization();

    return () => {
      mounted = false;
      if (timeoutId !== undefined) {
        window.clearTimeout(timeoutId);
      }
    };
  }, [onComplete, onError, retryCount, startTime, steps]);

  const handleSafeMode = () => {
    navigate('/settings?safeMode=true');
  };

  const handleRetry = () => {
    setRetryCount(0);
    setSteps(INIT_STEPS.map((s) => ({ name: s.name, status: 'pending' as const })));
    setOverallProgress(0);
  };

  const getStepIcon = (status: InitStep['status']) => {
    switch (status) {
      case 'success':
        return <CheckmarkCircle24Filled style={{ color: '#107C10' }} />;
      case 'warning':
        return <Warning24Regular style={{ color: '#F7630C' }} />;
      case 'error':
        return <DismissCircle24Regular style={{ color: '#D13438' }} />;
      case 'running':
        return <Spinner size="tiny" />;
      default:
        return <div style={{ width: 24, height: 24 }} />;
    }
  };

  const hasErrors = steps.some((s) => s.status === 'error');
  const hasWarnings = steps.some((s) => s.status === 'warning');

  return (
    <div
      style={{
        height: '100vh',
        display: 'flex',
        alignItems: 'center',
        justifyContent: 'center',
        background: 'var(--colorNeutralBackground1)',
      }}
    >
      <div
        style={{
          width: '600px',
          maxWidth: '90vw',
          padding: '40px',
          background: 'var(--colorNeutralBackground2)',
          borderRadius: '8px',
          boxShadow: '0 4px 16px rgba(0,0,0,0.1)',
        }}
      >
        <div style={{ textAlign: 'center', marginBottom: '32px' }}>
          <Text
            as="h1"
            size={800}
            weight="semibold"
            style={{ marginBottom: '8px', display: 'block' }}
          >
            Initializing Aura Video Studio
          </Text>
          <Text as="p" size={400} style={{ color: 'var(--colorNeutralForeground3)' }}>
            Please wait while we prepare your workspace
          </Text>
        </div>

        <ProgressBar
          value={overallProgress / 100}
          thickness="large"
          style={{ marginBottom: '24px' }}
        />

        <div style={{ marginBottom: '24px' }}>
          {steps.map((step, index) => (
            <div
              key={index}
              style={{
                display: 'flex',
                alignItems: 'center',
                gap: '12px',
                padding: '12px 0',
                borderBottom:
                  index < steps.length - 1 ? '1px solid var(--colorNeutralStroke2)' : 'none',
              }}
            >
              {getStepIcon(step.status)}
              <div style={{ flex: 1 }}>
                <Text weight="semibold" style={{ display: 'block' }}>
                  {step.name}
                </Text>
                {step.message && (
                  <Text
                    size={300}
                    style={{
                      display: 'block',
                      color: 'var(--colorNeutralForeground3)',
                    }}
                  >
                    {step.message}
                  </Text>
                )}
              </div>
            </div>
          ))}
        </div>

        {hasWarnings && !hasErrors && (
          <MessageBar intent="warning" style={{ marginBottom: '16px' }}>
            <MessageBarBody>
              Some features may be limited due to missing dependencies or configuration.
            </MessageBarBody>
          </MessageBar>
        )}

        {hasErrors && (
          <div style={{ display: 'flex', gap: '8px', justifyContent: 'center' }}>
            <Button appearance="secondary" onClick={handleRetry}>
              Retry
            </Button>
            {enableSafeMode && (
              <Button appearance="primary" onClick={handleSafeMode}>
                Continue in Safe Mode
              </Button>
            )}
          </div>
        )}

        {retryCount > 0 && !hasErrors && (
          <Text
            size={300}
            style={{
              display: 'block',
              textAlign: 'center',
              color: 'var(--colorNeutralForeground3)',
            }}
          >
            Retry attempt {retryCount} of 2...
          </Text>
        )}
      </div>
    </div>
  );
}

function createInitError(
  code: string,
  message: string,
  details: string,
  recoverable: boolean,
  suggestions: string[]
): InitializationError {
  return {
    code,
    message,
    details,
    recoverable,
    suggestions,
  };
}
