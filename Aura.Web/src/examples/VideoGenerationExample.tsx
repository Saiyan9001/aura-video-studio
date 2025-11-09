/**
 * Example: Video Generation with Hooks
 *
 * This example demonstrates how to use the new video generation hooks
 * and API methods for a complete video generation flow.
 */

import { Button, ProgressBar, Text, MessageBar, MessageBarBody } from '@fluentui/react-components';
import { ComponentErrorBoundary } from '../components/ErrorBoundary/ComponentErrorBoundary';
import { useVideoGeneration } from '../hooks/useVideoGeneration';
import type { VideoGenerationRequest } from '../services/api/videoApi';

/**
 * Example component showing video generation with real-time progress
 */
export function VideoGenerationExample() {
  // Use the video generation hook
  const { isGenerating, progress, status, error, generate, cancel, retry, reset } =
    useVideoGeneration({
      onComplete: (finalStatus) => {
        console.log('Video generation completed:', finalStatus);
        if (finalStatus.outputPath) {
          alert(`Video ready: ${finalStatus.outputPath}`);
        }
      },
      onError: (err) => {
        console.error('Video generation failed:', err);
      },
      onProgress: (percent, message) => {
        console.log(`Progress: ${percent}% - ${message || ''}`);
      },
    });

  // Start video generation
  const handleGenerate = async () => {
    const request: VideoGenerationRequest = {
      brief: {
        topic: 'Introduction to TypeScript',
        audience: 'Web Developers',
        goal: 'Educate',
        tone: 'Professional',
        language: 'en',
        aspect: 'Widescreen16x9',
      },
      planSpec: {
        targetDuration: '00:01:30',
        pacing: 'Conversational',
        density: 'Balanced',
        style: 'Modern',
      },
      voiceSpec: {
        voiceName: 'David',
        rate: 1.0,
        pitch: 1.0,
        pause: 'Natural',
      },
      renderSpec: {
        res: '1080p',
        container: 'mp4',
        videoBitrateK: 5000,
        audioBitrateK: 192,
        fps: 30,
        codec: 'h264',
        qualityLevel: 'High',
        enableSceneCut: true,
      },
    };

    try {
      await generate(request);
    } catch (err) {
      console.error('Failed to start generation:', err);
    }
  };

  // Get stage label
  const getStageLabel = (percent: number): string => {
    if (percent < 25) return 'Script Generation';
    if (percent < 50) return 'Audio Generation';
    if (percent < 75) return 'Visual Generation';
    if (percent < 100) return 'Video Rendering';
    return 'Completed';
  };

  return (
    <ComponentErrorBoundary componentName="VideoGenerationExample">
      <div style={{ padding: '2rem', maxWidth: '600px' }}>
        <Text as="h2" size={600} weight="semibold" block style={{ marginBottom: '1rem' }}>
          Video Generation Example
        </Text>

        {!isGenerating && !status && (
          <Button appearance="primary" onClick={handleGenerate}>
            Generate Video
          </Button>
        )}

        {isGenerating && (
          <div style={{ marginTop: '1rem' }}>
            <Text block style={{ marginBottom: '0.5rem' }}>
              {getStageLabel(progress)} - {progress}%
            </Text>
            <ProgressBar value={progress / 100} />
            <Button appearance="secondary" onClick={cancel} style={{ marginTop: '1rem' }}>
              Cancel Generation
            </Button>
          </div>
        )}

        {error && (
          <MessageBar intent="error" style={{ marginTop: '1rem' }}>
            <MessageBarBody>
              <strong>Error:</strong> {error.message}
              <div style={{ marginTop: '0.5rem' }}>
                <Button size="small" onClick={retry}>
                  Retry
                </Button>
                <Button
                  size="small"
                  appearance="secondary"
                  onClick={reset}
                  style={{ marginLeft: '0.5rem' }}
                >
                  Reset
                </Button>
              </div>
            </MessageBarBody>
          </MessageBar>
        )}

        {status && status.status === 'Done' && (
          <MessageBar intent="success" style={{ marginTop: '1rem' }}>
            <MessageBarBody>
              <strong>Success!</strong> Video generated successfully.
              {status.outputPath && <div>Output: {status.outputPath}</div>}
              <Button size="small" onClick={reset} style={{ marginTop: '0.5rem' }}>
                Generate Another
              </Button>
            </MessageBarBody>
          </MessageBar>
        )}
      </div>
    </ComponentErrorBoundary>
  );
}
