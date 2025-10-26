/**
 * Enhanced Timeline Editor with advanced editing features
 * This demonstrates how to integrate the new Timeline component with advanced features
 */

import { makeStyles, tokens, Button, Spinner, Text, Title3 } from '@fluentui/react-components';
import { Save24Regular, Play24Regular, ArrowLeft24Regular } from '@fluentui/react-icons';
import { useEffect, useState, useCallback } from 'react';
import { useParams, useNavigate } from 'react-router-dom';
import { ScenePropertiesPanel } from '../../components/Editor/ScenePropertiesPanel';
import { Timeline } from '../../components/Editor/Timeline/Timeline';
import { VideoPreviewPlayer } from '../../components/Editor/VideoPreviewPlayer';
import type { EditableTimeline, TimelineScene, TimelineAsset } from '../../types/timeline';

const useStyles = makeStyles({
  container: {
    display: 'flex',
    flexDirection: 'column',
    height: '100vh',
    backgroundColor: tokens.colorNeutralBackground1,
  },
  header: {
    display: 'flex',
    alignItems: 'center',
    justifyContent: 'space-between',
    padding: tokens.spacingVerticalM,
    borderBottom: `1px solid ${tokens.colorNeutralStroke2}`,
    backgroundColor: tokens.colorNeutralBackground2,
  },
  headerLeft: {
    display: 'flex',
    alignItems: 'center',
    gap: tokens.spacingHorizontalM,
  },
  headerRight: {
    display: 'flex',
    alignItems: 'center',
    gap: tokens.spacingHorizontalS,
  },
  saveIndicator: {
    fontSize: tokens.fontSizeBase200,
    color: tokens.colorNeutralForeground3,
  },
  content: {
    display: 'flex',
    flex: 1,
    overflow: 'hidden',
  },
  leftPanel: {
    width: '60%',
    display: 'flex',
    flexDirection: 'column',
    borderRight: `1px solid ${tokens.colorNeutralStroke2}`,
  },
  rightPanel: {
    width: '40%',
    display: 'flex',
    flexDirection: 'column',
  },
  previewSection: {
    height: '50%',
    borderBottom: `1px solid ${tokens.colorNeutralStroke2}`,
  },
  timelineSection: {
    height: '50%',
  },
  loadingContainer: {
    display: 'flex',
    flexDirection: 'column',
    alignItems: 'center',
    justifyContent: 'center',
    height: '100%',
    gap: tokens.spacingVerticalM,
  },
  errorContainer: {
    display: 'flex',
    flexDirection: 'column',
    alignItems: 'center',
    justifyContent: 'center',
    height: '100%',
    gap: tokens.spacingVerticalM,
    color: tokens.colorPaletteRedForeground1,
  },
});

export function EnhancedTimelineEditor() {
  const styles = useStyles();
  const { jobId } = useParams<{ jobId: string }>();
  const navigate = useNavigate();

  const [timeline, setTimeline] = useState<EditableTimeline | null>(null);
  const [selectedSceneIndex] = useState<number | null>(null);
  const [selectedAssetId, setSelectedAssetId] = useState<string | null>(null);
  const [currentTime, setCurrentTime] = useState(0);
  const [previewUrl, setPreviewUrl] = useState<string | null>(null);
  const [isDirty, setIsDirty] = useState(false);
  const [isSaving, setIsSaving] = useState(false);
  const [lastSaved, setLastSaved] = useState<Date | null>(null);
  const [isLoading, setIsLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [isGeneratingPreview, setIsGeneratingPreview] = useState(false);

  // Load timeline
  useEffect(() => {
    if (!jobId) return;

    const loadTimeline = async () => {
      try {
        setIsLoading(true);
        const response = await fetch(`/api/editor/timeline/${jobId}`);

        if (!response.ok) {
          throw new Error('Failed to load timeline');
        }

        const data = await response.json();
        setTimeline(data);

        // Check if preview exists
        try {
          const previewResponse = await fetch(`/api/editor/preview/${jobId}`, { method: 'HEAD' });
          if (previewResponse.ok) {
            setPreviewUrl(`/api/editor/preview/${jobId}`);
          }
        } catch {
          // Preview doesn&apos;t exist yet
        }
      } catch (err) {
        setError(err instanceof Error ? err.message : 'Failed to load timeline');
      } finally {
        setIsLoading(false);
      }
    };

    loadTimeline();
  }, [jobId]);

  // Auto-save every 5 seconds when dirty
  useEffect(() => {
    if (!isDirty || !timeline || !jobId) return;

    const timer = setTimeout(() => {
      saveTimeline();
    }, 5000);

    return () => clearTimeout(timer);
  }, [isDirty, timeline, jobId, saveTimeline]);

  // Warn before leaving with unsaved changes
  useEffect(() => {
    const handleBeforeUnload = (e: BeforeUnloadEvent) => {
      if (isDirty) {
        e.preventDefault();
        e.returnValue = '';
      }
    };

    window.addEventListener('beforeunload', handleBeforeUnload);
    return () => window.removeEventListener('beforeunload', handleBeforeUnload);
  }, [isDirty]);

  const saveTimeline = useCallback(async () => {
    if (!timeline || !jobId) return;

    try {
      setIsSaving(true);
      const response = await fetch(`/api/editor/timeline/${jobId}`, {
        method: 'PUT',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify(timeline),
      });

      if (!response.ok) {
        throw new Error('Failed to save timeline');
      }

      setIsDirty(false);
      setLastSaved(new Date());
    } catch (err) {
      console.error('Failed to save timeline:', err);
    } finally {
      setIsSaving(false);
    }
  }, [timeline, jobId]);

  const handleGeneratePreview = async () => {
    if (!jobId) return;

    try {
      setIsGeneratingPreview(true);

      // Save timeline first
      await saveTimeline();

      const response = await fetch(`/api/editor/timeline/${jobId}/render-preview`, {
        method: 'POST',
      });

      if (!response.ok) {
        throw new Error('Failed to generate preview');
      }

      const data = await response.json();
      setPreviewUrl(data.previewPath);
    } catch (err) {
      console.error('Failed to generate preview:', err);
      alert('Failed to generate preview');
    } finally {
      setIsGeneratingPreview(false);
    }
  };

  const handleSceneUpdate = useCallback(
    (index: number, updates: Partial<TimelineScene>) => {
      if (!timeline) return;

      const newScenes = [...timeline.scenes];
      newScenes[index] = { ...newScenes[index], ...updates };

      setTimeline({ ...timeline, scenes: newScenes });
      setIsDirty(true);
    },
    [timeline]
  );

  const handleAssetUpdate = useCallback(
    (sceneIndex: number, assetId: string, updates: Partial<TimelineAsset>) => {
      if (!timeline) return;

      const newScenes = [...timeline.scenes];
      const scene = newScenes[sceneIndex];
      const assetIndex = scene.visualAssets.findIndex((a) => a.id === assetId);

      if (assetIndex >= 0) {
        scene.visualAssets[assetIndex] = { ...scene.visualAssets[assetIndex], ...updates };
        setTimeline({ ...timeline, scenes: newScenes });
        setIsDirty(true);
      }
    },
    [timeline]
  );

  const handleDeleteAsset = useCallback(
    (sceneIndex: number, assetId: string) => {
      if (!timeline) return;

      const newScenes = [...timeline.scenes];
      const scene = newScenes[sceneIndex];
      scene.visualAssets = scene.visualAssets.filter((a) => a.id !== assetId);

      setTimeline({ ...timeline, scenes: newScenes });
      setIsDirty(true);

      if (selectedAssetId === assetId) {
        setSelectedAssetId(null);
      }
    },
    [timeline, selectedAssetId]
  );

  // Calculate total duration
  const totalDuration = timeline?.scenes.reduce((sum, scene) => sum + scene.duration, 0) || 120;

  if (isLoading) {
    return (
      <div className={styles.container}>
        <div className={styles.loadingContainer}>
          <Spinner size="large" />
          <Text>Loading timeline...</Text>
        </div>
      </div>
    );
  }

  if (error) {
    return (
      <div className={styles.container}>
        <div className={styles.errorContainer}>
          <Text weight="semibold">Error loading timeline</Text>
          <Text>{error}</Text>
          <Button onClick={() => navigate('/jobs')}>Back to Jobs</Button>
        </div>
      </div>
    );
  }

  if (!timeline) {
    return null;
  }

  const selectedScene =
    selectedSceneIndex !== null ? timeline.scenes[selectedSceneIndex] : undefined;

  return (
    <div className={styles.container}>
      {/* Header */}
      <div className={styles.header}>
        <div className={styles.headerLeft}>
          <Button
            appearance="subtle"
            icon={<ArrowLeft24Regular />}
            onClick={() => navigate('/jobs')}
          >
            Back
          </Button>
          <Title3>Enhanced Timeline Editor</Title3>
        </div>
        <div className={styles.headerRight}>
          <Text className={styles.saveIndicator}>
            {isSaving
              ? 'Saving...'
              : isDirty
                ? 'Unsaved changes'
                : lastSaved
                  ? `Saved at ${lastSaved.toLocaleTimeString()}`
                  : ''}
          </Text>
          <Button
            appearance="subtle"
            icon={<Save24Regular />}
            onClick={saveTimeline}
            disabled={!isDirty || isSaving}
          >
            Save
          </Button>
          <Button
            appearance="primary"
            icon={<Play24Regular />}
            onClick={handleGeneratePreview}
            disabled={isGeneratingPreview}
          >
            {isGeneratingPreview ? 'Generating...' : 'Generate Preview'}
          </Button>
        </div>
      </div>

      {/* Content */}
      <div className={styles.content}>
        {/* Left Panel: Preview and Timeline */}
        <div className={styles.leftPanel}>
          {/* Video Preview */}
          <div className={styles.previewSection}>
            <VideoPreviewPlayer
              videoUrl={previewUrl || undefined}
              currentTime={currentTime}
              onTimeUpdate={setCurrentTime}
            />
          </div>

          {/* Enhanced Timeline with Advanced Features */}
          <div className={styles.timelineSection}>
            <Timeline duration={totalDuration} onSave={saveTimeline} />
          </div>
        </div>

        {/* Right Panel: Properties */}
        <div className={styles.rightPanel}>
          <ScenePropertiesPanel
            scene={selectedScene}
            selectedAssetId={selectedAssetId ?? undefined}
            onUpdateScene={
              selectedSceneIndex !== null
                ? (updates) => handleSceneUpdate(selectedSceneIndex, updates)
                : undefined
            }
            onUpdateAsset={
              selectedSceneIndex !== null
                ? (assetId, updates) => handleAssetUpdate(selectedSceneIndex, assetId, updates)
                : undefined
            }
            onDeleteAsset={
              selectedSceneIndex !== null
                ? (assetId) => handleDeleteAsset(selectedSceneIndex, assetId)
                : undefined
            }
            onSelectAsset={setSelectedAssetId}
          />
        </div>
      </div>
    </div>
  );
}
