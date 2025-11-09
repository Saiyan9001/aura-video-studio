/**
 * Example: Connecting Wizard Steps to API
 *
 * This file demonstrates how to integrate wizard steps with the backend API
 * using the new wizardService and proper error handling.
 */

import { Button, Spinner, MessageBar } from '@fluentui/react-components';
import { useState, useCallback } from 'react';
import type { FC } from 'react';
import { loggingService as logger } from '@/services/loggingService';
import {
  storeBrief,
  fetchAvailableVoices,
  generateScript,
  startFinalRendering,
  type WizardBriefData,
  type WizardStyleData,
  type WizardScriptData,
} from '@/services/wizardService';

/**
 * Example: Step 1 - Storing Brief Data
 */
export const BriefStepExample: FC = () => {
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [success, setSuccess] = useState(false);

  const handleStoreBrief = useCallback(async (briefData: WizardBriefData) => {
    setLoading(true);
    setError(null);
    setSuccess(false);

    try {
      const result = await storeBrief(briefData);

      logger.info('Brief stored successfully', 'BriefStep', 'handleStoreBrief', {
        briefId: result.briefId,
      });

      setSuccess(true);

      // Navigate to next step or update wizard state
      // moveToNextStep();
    } catch (error: unknown) {
      const errorObj = error instanceof Error ? error : new Error(String(error));
      const errorMessage = errorObj.message || 'Failed to store brief';

      logger.error('Failed to store brief', errorObj, 'BriefStep', 'handleStoreBrief');
      setError(errorMessage);
    } finally {
      setLoading(false);
    }
  }, []);

  return (
    <div>
      {error && <MessageBar intent="error">{error}</MessageBar>}

      {success && <MessageBar intent="success">Brief saved successfully!</MessageBar>}

      <Button
        onClick={() =>
          handleStoreBrief({
            topic: 'Example Topic',
            audience: 'General',
            goal: 'Educate',
            tone: 'Professional',
            language: 'English',
            duration: 60,
            videoType: 'educational',
          })
        }
        disabled={loading}
        icon={loading ? <Spinner size="tiny" /> : undefined}
      >
        Save Brief
      </Button>
    </div>
  );
};

/**
 * Example: Step 2 - Fetching Available Voices
 */
export const StyleStepExample: FC = () => {
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [voices, setVoices] = useState<
    Array<{
      id: string;
      name: string;
      provider: string;
    }>
  >([]);

  const loadVoices = useCallback(async (provider?: string) => {
    setLoading(true);
    setError(null);

    try {
      const result = await fetchAvailableVoices(provider);

      logger.info('Voices loaded', 'StyleStep', 'loadVoices', {
        count: result.voices.length,
        provider,
      });

      setVoices(result.voices);
    } catch (error: unknown) {
      const errorObj = error instanceof Error ? error : new Error(String(error));
      const errorMessage = errorObj.message || 'Failed to load voices';

      logger.error('Failed to load voices', errorObj, 'StyleStep', 'loadVoices');
      setError(errorMessage);
    } finally {
      setLoading(false);
    }
  }, []);

  return (
    <div>
      {error && <MessageBar intent="error">{error}</MessageBar>}

      <Button
        onClick={() => loadVoices('ElevenLabs')}
        disabled={loading}
        icon={loading ? <Spinner size="tiny" /> : undefined}
      >
        Load Voices
      </Button>

      {voices.length > 0 && (
        <ul>
          {voices.map((voice) => (
            <li key={voice.id}>
              {voice.name} ({voice.provider})
            </li>
          ))}
        </ul>
      )}
    </div>
  );
};

/**
 * Example: Step 3 - Generating Script
 */
export const ScriptStepExample: FC = () => {
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [script, setScript] = useState<string | null>(null);

  const handleGenerateScript = useCallback(
    async (briefData: WizardBriefData, styleData: WizardStyleData) => {
      setLoading(true);
      setError(null);
      setScript(null);

      try {
        const result = await generateScript(briefData, styleData);

        logger.info('Script generated', 'ScriptStep', 'handleGenerateScript', {
          jobId: result.jobId,
          sceneCount: result.scenes.length,
        });

        setScript(result.script);

        // Optionally start SSE streaming for script generation progress
        // startStreaming(result.jobId);
      } catch (error: unknown) {
        const errorObj = error instanceof Error ? error : new Error(String(error));
        const errorMessage = errorObj.message || 'Failed to generate script';

        logger.error('Failed to generate script', errorObj, 'ScriptStep', 'handleGenerateScript');
        setError(errorMessage);
      } finally {
        setLoading(false);
      }
    },
    []
  );

  return (
    <div>
      {error && <MessageBar intent="error">{error}</MessageBar>}

      <Button
        onClick={() =>
          handleGenerateScript(
            {
              topic: 'Example Topic',
              audience: 'General',
              goal: 'Educate',
              tone: 'Professional',
              language: 'English',
              duration: 60,
              videoType: 'educational',
            },
            {
              voiceProvider: 'ElevenLabs',
              voiceName: 'Rachel',
              visualStyle: 'modern',
              musicEnabled: true,
            }
          )
        }
        disabled={loading}
        icon={loading ? <Spinner size="tiny" /> : undefined}
      >
        Generate Script
      </Button>

      {script && (
        <div>
          <h3>Generated Script:</h3>
          <pre>{script}</pre>
        </div>
      )}
    </div>
  );
};

/**
 * Example: Step 5 - Starting Final Rendering
 */
export const ExportStepExample: FC = () => {
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [jobId, setJobId] = useState<string | null>(null);

  const handleStartRendering = useCallback(
    async (
      briefData: WizardBriefData,
      styleData: WizardStyleData,
      scriptData: WizardScriptData,
      exportConfig: {
        resolution: string;
        fps: number;
        codec: string;
        quality: number;
        includeSubs: boolean;
        outputFormat: string;
      }
    ) => {
      setLoading(true);
      setError(null);
      setJobId(null);

      try {
        const result = await startFinalRendering(briefData, styleData, scriptData, exportConfig);

        logger.info('Rendering started', 'ExportStep', 'handleStartRendering', {
          jobId: result.jobId,
        });

        setJobId(result.jobId);

        // Start SSE streaming for real-time progress
        // useJobsStore.getState().startStreaming(result.jobId);
      } catch (error: unknown) {
        const errorObj = error instanceof Error ? error : new Error(String(error));
        const errorMessage = errorObj.message || 'Failed to start rendering';

        logger.error('Failed to start rendering', errorObj, 'ExportStep', 'handleStartRendering');
        setError(errorMessage);
      } finally {
        setLoading(false);
      }
    },
    []
  );

  return (
    <div>
      {error && <MessageBar intent="error">{error}</MessageBar>}

      <Button
        onClick={() =>
          handleStartRendering(
            {
              topic: 'Example Topic',
              audience: 'General',
              goal: 'Educate',
              tone: 'Professional',
              language: 'English',
              duration: 60,
              videoType: 'educational',
            },
            {
              voiceProvider: 'ElevenLabs',
              voiceName: 'Rachel',
              visualStyle: 'modern',
              musicEnabled: true,
            },
            {
              generatedScript: 'Script content...',
              scenes: [
                {
                  id: 'scene-1',
                  text: 'Scene content',
                  duration: 10,
                  visualDescription: 'Visual description',
                },
              ],
              totalDuration: 60,
            },
            {
              resolution: '1920x1080',
              fps: 30,
              codec: 'H264',
              quality: 75,
              includeSubs: true,
              outputFormat: 'mp4',
            }
          )
        }
        disabled={loading}
        icon={loading ? <Spinner size="tiny" /> : undefined}
      >
        Start Rendering
      </Button>

      {jobId && (
        <MessageBar intent="success">
          Rendering started! Job ID: {jobId}
          <br />
          Track progress in the Jobs page.
        </MessageBar>
      )}
    </div>
  );
};

/**
 * Best Practices Summary:
 *
 * 1. Always use try-catch for async operations
 * 2. Set loading states before and after API calls
 * 3. Display user-friendly error messages
 * 4. Log all operations for debugging
 * 5. Use typed error handling (error: unknown)
 * 6. Clear errors when retrying operations
 * 7. Show success feedback to users
 * 8. Use proper TypeScript types from services
 * 9. Handle both success and error cases
 * 10. Clean up resources (SSE connections) when done
 */
