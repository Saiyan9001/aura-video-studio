import { useState, useRef, useCallback } from 'react';
import {
  makeStyles,
  tokens,
  Button,
  Slider,
  Label,
  Text,
  Switch,
  Tooltip,
} from '@fluentui/react-components';
import {
  Add24Regular,
  Subtract24Regular,
  Cut24Regular,
  Eye24Regular,
  EyeOff24Regular,
  LockClosed24Regular,
  LockOpen24Regular,
} from '@fluentui/react-icons';

const useStyles = makeStyles({
  container: {
    display: 'flex',
    flexDirection: 'column',
    height: '100%',
    backgroundColor: tokens.colorNeutralBackground2,
  },
  toolbar: {
    display: 'flex',
    alignItems: 'center',
    gap: tokens.spacingHorizontalM,
    padding: tokens.spacingVerticalM,
    borderBottom: `1px solid ${tokens.colorNeutralStroke1}`,
    backgroundColor: tokens.colorNeutralBackground1,
  },
  toolbarGroup: {
    display: 'flex',
    alignItems: 'center',
    gap: tokens.spacingHorizontalS,
  },
  timelineContainer: {
    flex: 1,
    overflow: 'auto',
    position: 'relative',
  },
  ruler: {
    height: '30px',
    borderBottom: `1px solid ${tokens.colorNeutralStroke1}`,
    position: 'sticky',
    top: 0,
    backgroundColor: tokens.colorNeutralBackground1,
    zIndex: 10,
    display: 'flex',
  },
  rulerMarker: {
    position: 'absolute',
    fontSize: tokens.fontSizeBase200,
    color: tokens.colorNeutralForeground2,
    userSelect: 'none',
  },
  tracksContainer: {
    position: 'relative',
    minHeight: '200px',
  },
  track: {
    height: '60px',
    borderBottom: `1px solid ${tokens.colorNeutralStroke1}`,
    position: 'relative',
    display: 'flex',
  },
  trackDragOver: {
    backgroundColor: tokens.colorBrandBackground2,
  },
  trackLabel: {
    width: '100px',
    padding: `${tokens.spacingVerticalXS} ${tokens.spacingVerticalS}`,
    borderRight: `1px solid ${tokens.colorNeutralStroke1}`,
    backgroundColor: tokens.colorNeutralBackground3,
    fontWeight: tokens.fontWeightSemibold,
    fontSize: tokens.fontSizeBase200,
    display: 'flex',
    flexDirection: 'column',
    gap: tokens.spacingVerticalXXS,
    position: 'sticky',
    left: 0,
    zIndex: 5,
  },
  trackLabelText: {
    fontSize: tokens.fontSizeBase200,
    fontWeight: tokens.fontWeightSemibold,
  },
  trackControls: {
    display: 'flex',
    gap: tokens.spacingHorizontalXXS,
  },
  trackContent: {
    flex: 1,
    position: 'relative',
    minWidth: '2000px',
  },
  clip: {
    position: 'absolute',
    top: '8px',
    height: '44px',
    backgroundColor: tokens.colorBrandBackground,
    borderRadius: tokens.borderRadiusSmall,
    border: `1px solid ${tokens.colorBrandStroke1}`,
    cursor: 'pointer',
    padding: `${tokens.spacingVerticalXS} ${tokens.spacingHorizontalS}`,
    color: tokens.colorNeutralForegroundOnBrand,
    fontSize: tokens.fontSizeBase200,
    overflow: 'hidden',
    whiteSpace: 'nowrap',
    textOverflow: 'ellipsis',
    display: 'flex',
    alignItems: 'center',
    userSelect: 'none',
    '&:hover': {
      backgroundColor: tokens.colorBrandBackgroundHover,
    },
  },
  clipSelected: {
    border: `2px solid ${tokens.colorBrandForeground1}`,
    backgroundColor: tokens.colorBrandBackgroundHover,
  },
  clipThumbnails: {
    position: 'absolute',
    top: 0,
    left: 0,
    right: 0,
    bottom: 0,
    display: 'flex',
    opacity: 0.6,
    pointerEvents: 'none',
  },
  clipThumbnail: {
    height: '100%',
    objectFit: 'cover',
    borderRight: `1px solid ${tokens.colorNeutralStroke2}`,
  },
  clipWaveform: {
    position: 'absolute',
    top: 0,
    left: 0,
    right: 0,
    bottom: 0,
    display: 'flex',
    alignItems: 'center',
    gap: '1px',
    padding: '4px',
    opacity: 0.7,
    pointerEvents: 'none',
  },
  waveformBar: {
    flex: 1,
    backgroundColor: tokens.colorNeutralForegroundOnBrand,
    minWidth: '2px',
    borderRadius: '1px',
  },
  clipLabel: {
    position: 'relative',
    zIndex: 1,
    textShadow: '0 1px 2px rgba(0,0,0,0.5)',
  },
  playhead: {
    position: 'absolute',
    top: 0,
    bottom: 0,
    width: '2px',
    backgroundColor: tokens.colorPaletteRedBackground3,
    zIndex: 20,
    pointerEvents: 'none',
  },
  playheadHandle: {
    position: 'absolute',
    top: '-6px',
    left: '-6px',
    width: '14px',
    height: '14px',
    backgroundColor: tokens.colorPaletteRedBackground3,
    borderRadius: '50%',
    cursor: 'ew-resize',
    pointerEvents: 'auto',
  },
  zoomSlider: {
    width: '120px',
  },
});

interface TimelineClip {
  id: string;
  trackId: string;
  startTime: number;
  duration: number;
  label: string;
  type: 'video' | 'audio' | 'image';
  // Media source reference
  mediaId?: string;
  file?: File;
  // Visual data
  thumbnails?: Array<{ dataUrl: string; timestamp: number }>;
  waveform?: { peaks: number[]; duration: number };
  preview?: string;
}

interface TimelineTrack {
  id: string;
  label: string;
  type: 'video' | 'audio';
  visible?: boolean;
  locked?: boolean;
}

interface TimelinePanelProps {
  clips?: TimelineClip[];
  tracks?: TimelineTrack[];
  currentTime?: number;
  onTimeChange?: (time: number) => void;
  onClipSelect?: (clipId: string | null) => void;
  selectedClipId?: string | null;
  onClipAdd?: (trackId: string, clip: TimelineClip) => void;
  onTrackToggleVisibility?: (trackId: string) => void;
  onTrackToggleLock?: (trackId: string) => void;
}

export function TimelinePanel({
  clips = [],
  tracks = [
    { id: 'video1', label: 'Video 1', type: 'video', visible: true, locked: false },
    { id: 'video2', label: 'Video 2', type: 'video', visible: true, locked: false },
    { id: 'audio1', label: 'Audio 1', type: 'audio', visible: true, locked: false },
    { id: 'audio2', label: 'Audio 2', type: 'audio', visible: true, locked: false },
  ],
  currentTime = 0,
  onTimeChange,
  onClipSelect,
  selectedClipId = null,
  onClipAdd,
  onTrackToggleVisibility,
  onTrackToggleLock,
}: TimelinePanelProps) {
  const styles = useStyles();
  const [zoom, setZoom] = useState(50); // pixels per second
  const [snapping, setSnapping] = useState(true);
  const [dragOverTrack, setDragOverTrack] = useState<string | null>(null);
  const timelineRef = useRef<HTMLDivElement>(null);
  const isDraggingPlayhead = useRef(false);

  // Calculate timeline width based on clips
  const maxTime = Math.max(
    10,
    ...clips.map((clip) => clip.startTime + clip.duration),
    currentTime + 5
  );

  const pixelsPerSecond = zoom;
  const timelineWidth = maxTime * pixelsPerSecond;

  const handlePlayheadDrag = useCallback(
    (_e: React.MouseEvent) => {
      if (!timelineRef.current) return;
      isDraggingPlayhead.current = true;

      const handleMouseMove = (moveEvent: MouseEvent) => {
        if (!timelineRef.current || !isDraggingPlayhead.current) return;

        const rect = timelineRef.current.getBoundingClientRect();
        const x = moveEvent.clientX - rect.left - 100; // Account for track label width
        let time = Math.max(0, x / pixelsPerSecond);

        if (snapping) {
          time = Math.round(time * 2) / 2; // Snap to 0.5 second intervals
        }

        onTimeChange?.(time);
      };

      const handleMouseUp = () => {
        isDraggingPlayhead.current = false;
        document.removeEventListener('mousemove', handleMouseMove);
        document.removeEventListener('mouseup', handleMouseUp);
      };

      document.addEventListener('mousemove', handleMouseMove);
      document.addEventListener('mouseup', handleMouseUp);
    },
    [pixelsPerSecond, snapping, onTimeChange]
  );

  const handleTimelineClick = (e: React.MouseEvent) => {
    if (!timelineRef.current || isDraggingPlayhead.current) return;

    const rect = timelineRef.current.getBoundingClientRect();
    const x = e.clientX - rect.left - 100; // Account for track label width
    let time = Math.max(0, x / pixelsPerSecond);

    if (snapping) {
      time = Math.round(time * 2) / 2;
    }

    onTimeChange?.(time);
  };

  const formatTime = (seconds: number) => {
    const mins = Math.floor(seconds / 60);
    const secs = Math.floor(seconds % 60);
    const frames = Math.floor((seconds % 1) * 30);
    return `${mins.toString().padStart(2, '0')}:${secs.toString().padStart(2, '0')}:${frames.toString().padStart(2, '0')}`;
  };

  const handleTrackDragOver = (e: React.DragEvent, trackId: string) => {
    e.preventDefault();
    e.stopPropagation();
    setDragOverTrack(trackId);
  };

  const handleTrackDragLeave = (e: React.DragEvent) => {
    e.preventDefault();
    e.stopPropagation();
    setDragOverTrack(null);
  };

  const handleTrackDrop = (e: React.DragEvent, trackId: string) => {
    e.preventDefault();
    e.stopPropagation();
    setDragOverTrack(null);

    try {
      const data = e.dataTransfer.getData('application/json');
      if (!data) return;

      const mediaClip = JSON.parse(data);
      
      // Calculate drop position based on mouse position
      const rect = (e.target as HTMLElement).getBoundingClientRect();
      const x = e.clientX - rect.left;
      let dropTime = Math.max(0, (x - 100) / pixelsPerSecond); // Account for track label width

      if (snapping) {
        dropTime = Math.round(dropTime * 2) / 2; // Snap to 0.5 second intervals
      }

      // Create a new timeline clip from the media clip
      const newClip: TimelineClip = {
        id: `clip-${Date.now()}-${Math.random().toString(36).substr(2, 9)}`,
        trackId,
        startTime: dropTime,
        duration: mediaClip.duration || 3, // Use media duration or default to 3
        label: mediaClip.name,
        type: mediaClip.type,
        mediaId: mediaClip.id,
        file: mediaClip.file,
        thumbnails: mediaClip.thumbnails,
        waveform: mediaClip.waveform,
        preview: mediaClip.preview,
      };

      onClipAdd?.(trackId, newClip);
    } catch (error) {
      console.error('Failed to parse dropped data:', error);
    }
  };

  const renderRuler = () => {
    const markers = [];
    const interval = zoom < 30 ? 5 : zoom < 60 ? 2 : 1;

    for (let i = 0; i <= maxTime; i += interval) {
      const left = i * pixelsPerSecond + 100;
      markers.push(
        <div key={i} className={styles.rulerMarker} style={{ left: `${left}px`, top: '8px' }}>
          {formatTime(i)}
        </div>
      );
    }

    return markers;
  };

  return (
    <div className={styles.container}>
      <div className={styles.toolbar}>
        <div className={styles.toolbarGroup}>
          <Button appearance="subtle" icon={<Cut24Regular />} disabled>
            Split
          </Button>
          <Button appearance="subtle" icon={<Cut24Regular />} disabled>
            Delete
          </Button>
        </div>

        <div className={styles.toolbarGroup}>
          <Label>Snapping</Label>
          <Switch checked={snapping} onChange={(_, data) => setSnapping(data.checked)} />
        </div>

        <div className={styles.toolbarGroup}>
          <Label>Zoom</Label>
          <Button
            appearance="subtle"
            icon={<Subtract24Regular />}
            onClick={() => setZoom(Math.max(10, zoom - 10))}
            aria-label="Zoom out"
          />
          <Slider
            className={styles.zoomSlider}
            min={10}
            max={100}
            value={zoom}
            onChange={(_, data) => setZoom(data.value)}
          />
          <Button
            appearance="subtle"
            icon={<Add24Regular />}
            onClick={() => setZoom(Math.min(100, zoom + 10))}
            aria-label="Zoom in"
          />
          <Text>{Math.round((zoom / 50) * 100)}%</Text>
        </div>
      </div>

      <div className={styles.timelineContainer} ref={timelineRef}>
        <div className={styles.ruler} style={{ width: `${timelineWidth + 100}px` }}>
          {renderRuler()}
        </div>

        <div 
          className={styles.tracksContainer}
          role="region" 
          aria-label="Timeline tracks"
        >
          {tracks.map((track) => (
            /* Timeline track - intentionally clickable for seeking playhead */
            /* eslint-disable-next-line jsx-a11y/click-events-have-key-events, jsx-a11y/no-static-element-interactions */
            <div 
              key={track.id} 
              className={`${styles.track} ${dragOverTrack === track.id ? styles.trackDragOver : ''}`}
              onDragOver={(e) => handleTrackDragOver(e, track.id)}
              onDragLeave={handleTrackDragLeave}
              onDrop={(e) => handleTrackDrop(e, track.id)}
              onClick={handleTimelineClick}
            >
              <div className={styles.trackLabel}>
                <Text className={styles.trackLabelText}>{track.label}</Text>
                <div className={styles.trackControls}>
                  <Tooltip content={track.visible ? 'Hide track' : 'Show track'} relationship="label">
                    <Button
                      appearance="subtle"
                      size="small"
                      icon={track.visible ? <Eye24Regular /> : <EyeOff24Regular />}
                      onClick={() => onTrackToggleVisibility?.(track.id)}
                      aria-label={track.visible ? 'Hide track' : 'Show track'}
                      style={{ minWidth: '20px', minHeight: '20px', padding: '2px' }}
                    />
                  </Tooltip>
                  <Tooltip content={track.locked ? 'Unlock track' : 'Lock track'} relationship="label">
                    <Button
                      appearance="subtle"
                      size="small"
                      icon={track.locked ? <LockClosed24Regular /> : <LockOpen24Regular />}
                      onClick={() => onTrackToggleLock?.(track.id)}
                      aria-label={track.locked ? 'Unlock track' : 'Lock track'}
                      style={{ minWidth: '20px', minHeight: '20px', padding: '2px' }}
                    />
                  </Tooltip>
                </div>
              </div>
              <div className={styles.trackContent} style={{ width: `${timelineWidth}px` }}>
                {clips
                  .filter((clip) => clip.trackId === track.id)
                  .map((clip) => (
                    <div
                      key={clip.id}
                      className={`${styles.clip} ${selectedClipId === clip.id ? styles.clipSelected : ''}`}
                      style={{
                        left: `${clip.startTime * pixelsPerSecond}px`,
                        width: `${clip.duration * pixelsPerSecond}px`,
                      }}
                      onClick={(e) => {
                        e.stopPropagation();
                        onClipSelect?.(clip.id);
                      }}
                      onKeyDown={(e) => {
                        if (e.key === 'Enter' || e.key === ' ') {
                          e.preventDefault();
                          e.stopPropagation();
                          onClipSelect?.(clip.id);
                        }
                      }}
                      role="button"
                      tabIndex={0}
                      aria-label={`${clip.label} clip`}
                    >
                      {/* Render thumbnails for video clips */}
                      {clip.type === 'video' && clip.thumbnails && clip.thumbnails.length > 0 && (
                        <div className={styles.clipThumbnails}>
                          {clip.thumbnails.map((thumbnail, idx) => (
                            <img
                              key={idx}
                              src={thumbnail.dataUrl}
                              alt=""
                              className={styles.clipThumbnail}
                              style={{ flex: 1 }}
                            />
                          ))}
                        </div>
                      )}
                      
                      {/* Render waveform for audio/video clips */}
                      {(clip.type === 'audio' || (clip.type === 'video' && !clip.thumbnails)) && 
                        clip.waveform && 
                        clip.waveform.peaks.length > 0 && (
                        <div className={styles.clipWaveform}>
                          {clip.waveform.peaks.map((peak, idx) => (
                            <div
                              key={idx}
                              className={styles.waveformBar}
                              style={{ height: `${Math.max(2, peak * 100)}%` }}
                            />
                          ))}
                        </div>
                      )}
                      
                      <span className={styles.clipLabel}>{clip.label}</span>
                    </div>
                  ))}
              </div>
            </div>
          ))}

          {/* Playhead */}
          <div className={styles.playhead} style={{ left: `${currentTime * pixelsPerSecond + 100}px` }}>
            <div 
              className={styles.playheadHandle} 
              onMouseDown={handlePlayheadDrag} 
              role="slider" 
              aria-label="Playhead" 
              aria-valuenow={currentTime}
              aria-valuemin={0}
              aria-valuemax={maxTime}
              tabIndex={0}
            />
          </div>
        </div>
      </div>
    </div>
  );
}
