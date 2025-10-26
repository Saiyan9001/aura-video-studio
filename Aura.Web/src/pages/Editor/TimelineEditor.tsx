import { makeStyles, tokens, Button, Spinner, Text, Title3 } from '@fluentui/react-components';
import {
  Save24Regular,
  Play24Regular,
  ZoomIn24Regular,
  ZoomOut24Regular,
} from '@fluentui/react-icons';
import { useEffect, useState, useCallback } from 'react';
import { useParams, useNavigate } from 'react-router-dom';
import { ScenePropertiesPanel } from '../../components/Editor/ScenePropertiesPanel';
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
    flexDirection: 'column',
    flex: 1,
    overflow: 'hidden',
  },
  previewPanel: {
    height: '60%',
    borderBottom: `1px solid ${tokens.colorNeutralStroke2}`,
  },
  timelinePanel: {
    height: '30%',
    borderBottom: `1px solid ${tokens.colorNeutralStroke2}`,
    overflow: 'auto',
    backgroundColor: tokens.colorNeutralBackground3,
  },
  timelineContainer: {
    position: 'relative',
    padding: tokens.spacingVerticalM,
    minHeight: '100%',
  },
  timelineTrack: {
    display: 'flex',
    gap: tokens.spacingHorizontalXS,
    position: 'relative',
    minHeight: '80px',
  },
  sceneBlock: {
    position: 'relative',
    height: '80px',
    backgroundColor: tokens.colorNeutralBackground1,
    border: `2px solid ${tokens.colorNeutralStroke1}`,
    borderRadius: tokens.borderRadiusMedium,
    padding: tokens.spacingVerticalS,
    cursor: 'grab',
    overflow: 'hidden',
    display: 'flex',
    flexDirection: 'column',
    justifyContent: 'space-between',
  },
  sceneBlockSelected: {
    border: `3px solid ${tokens.colorBrandBackground}`,
    backgroundColor: tokens.colorBrandBackground2,
  },
  sceneBlockDragging: {
    opacity: 0.5,
    cursor: 'grabbing',
  },
  sceneHeading: {
    fontSize: tokens.fontSizeBase200,
    fontWeight: tokens.fontWeightSemibold,
    overflow: 'hidden',
    textOverflow: 'ellipsis',
    whiteSpace: 'nowrap',
  },
  sceneDuration: {
    fontSize: tokens.fontSizeBase100,
    color: tokens.colorNeutralForeground3,
  },
  resizeHandle: {
    position: 'absolute',
    right: '-4px',
    top: '0',
    bottom: '0',
    width: '8px',
    cursor: 'ew-resize',
    backgroundColor: 'transparent',
    '&:hover': {
      backgroundColor: tokens.colorBrandBackground,
    },
  },
  playhead: {
    position: 'absolute',
    top: '0',
    bottom: '0',
    width: '2px',
    backgroundColor: tokens.colorPaletteRedBackground3,
    pointerEvents: 'none',
    zIndex: 10,
  },
  propertiesPanel: {
    height: '10%',
    minHeight: '150px',
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
  timelineControls: {
    display: 'flex',
    gap: tokens.spacingHorizontalS,
    alignItems: 'center',
    marginBottom: tokens.spacingVerticalS,
  },
});

export function TimelineEditor() {
  const styles = useStyles();
  const { jobId } = useParams<{ jobId: string }>();
  const navigate = useNavigate();

  const [timeline, setTimeline] = useState<EditableTimeline | null>(null);
  const [selectedSceneIndex, setSelectedSceneIndex] = useState<number | null>(null);
  const [selectedAssetId, setSelectedAssetId] = useState<string | null>(null);
  const [currentTime, setCurrentTime] = useState(0);
  const [previewUrl, setPreviewUrl] = useState<string | null>(null);
  const [zoom, setZoom] = useState(50); // pixels per second
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

  const getSceneWidth = (scene: TimelineScene) => {
    return scene.duration * zoom;
  };

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
      <div className={styles.header}>
        <div className={styles.headerLeft}>
          <Title3>Timeline Editor</Title3>
          <Button appearance="subtle" onClick={() => navigate('/jobs')}>
            Back to Jobs
          </Button>
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

      <div className={styles.content}>
        <div className={styles.previewPanel}>
          <VideoPreviewPlayer
            videoUrl={previewUrl || undefined}
            currentTime={currentTime}
            onTimeUpdate={setCurrentTime}
          />
        </div>

        <div className={styles.timelinePanel}>
          <div className={styles.timelineContainer}>
            <div className={styles.timelineControls}>
              <Button
                appearance="subtle"
                icon={<ZoomIn24Regular />}
                onClick={() => setZoom(Math.min(200, zoom + 10))}
              >
                Zoom In
              </Button>
              <Button
                appearance="subtle"
                icon={<ZoomOut24Regular />}
                onClick={() => setZoom(Math.max(10, zoom - 10))}
              >
                Zoom Out
              </Button>
              <Text>Zoom: {zoom}px/s</Text>
            </div>

            <div className={styles.timelineTrack}>
              {timeline.scenes.map((scene, index) => (
                <div
                  key={scene.index}
                  className={`${styles.sceneBlock} ${
                    selectedSceneIndex === index ? styles.sceneBlockSelected : ''
                  }`}
                  style={{
                    width: `${getSceneWidth(scene)}px`,
                  }}
                  role="button"
                  tabIndex={0}
                  onClick={() => setSelectedSceneIndex(index)}
                  onKeyDown={(e) => {
                    if (e.key === 'Enter' || e.key === ' ') {
                      e.preventDefault();
                      setSelectedSceneIndex(index);
                    }
                  }}
                >
                  <div className={styles.sceneHeading}>
                    {scene.heading || `Scene ${scene.index + 1}`}
                  </div>
                  <div className={styles.sceneDuration}>{scene.duration.toFixed(1)}s</div>
                  <div className={styles.resizeHandle} />
                </div>
              ))}

              {/* Playhead */}
              <div className={styles.playhead} style={{ left: `${currentTime * zoom}px` }} />
            </div>
          </div>
        </div>

        <div className={styles.propertiesPanel}>
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
