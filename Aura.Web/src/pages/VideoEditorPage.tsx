import { useState, useEffect, useRef } from 'react';
import { EditorLayout } from '../components/EditorLayout/EditorLayout';
import { VideoPreviewPanel } from '../components/EditorLayout/VideoPreviewPanel';
import { TimelinePanel } from '../components/EditorLayout/TimelinePanel';
import { PropertiesPanel } from '../components/EditorLayout/PropertiesPanel';
import { MediaLibraryPanel } from '../components/EditorLayout/MediaLibraryPanel';

export interface TimelineClip {
  id: string;
  trackId: string;
  startTime: number;
  duration: number;
  label: string;
  type: 'video' | 'audio' | 'image';
  prompt?: string;
  effects?: string[];
  transform?: {
    x?: number;
    y?: number;
    scale?: number;
    rotation?: number;
  };
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
  visible: boolean;
  locked: boolean;
}

export function VideoEditorPage() {
  const [currentTime, setCurrentTime] = useState(0);
  const [selectedClipId, setSelectedClipId] = useState<string | null>(null);
  const [clips, setClips] = useState<TimelineClip[]>([]);
  const [, setShowKeyboardShortcuts] = useState(false);
  const [isPlaying, setIsPlaying] = useState(false);
  const [tracks, setTracks] = useState<TimelineTrack[]>([
    { id: 'video1', label: 'Video 1', type: 'video', visible: true, locked: false },
    { id: 'video2', label: 'Video 2', type: 'video', visible: true, locked: false },
    { id: 'audio1', label: 'Audio 1', type: 'audio', visible: true, locked: false },
    { id: 'audio2', label: 'Audio 2', type: 'audio', visible: true, locked: false },
  ]);
  
  // Ref to track video preview controls
  const videoPreviewRef = useRef<{
    play: () => void;
    pause: () => void;
    stepForward: () => void;
    stepBackward: () => void;
  } | null>(null);

  // Ref to media library panel for triggering file picker
  const mediaLibraryRef = useRef<{ openFilePicker: () => void } | null>(null);

  // Keyboard shortcuts
  useEffect(() => {
    const handleKeyDown = (e: KeyboardEvent) => {
      // Don't trigger shortcuts if user is typing in an input
      if (e.target instanceof HTMLInputElement || e.target instanceof HTMLTextAreaElement) {
        return;
      }

      // Space for play/pause
      if (e.code === 'Space' && !e.ctrlKey && !e.shiftKey) {
        e.preventDefault();
        setIsPlaying((prev) => !prev);
      }
      // J for reverse shuttle (step backward)
      else if (e.key === 'j' && !e.ctrlKey && !e.shiftKey) {
        e.preventDefault();
        videoPreviewRef.current?.stepBackward();
      }
      // K for pause
      else if (e.key === 'k' && !e.ctrlKey && !e.shiftKey) {
        e.preventDefault();
        setIsPlaying(false);
      }
      // L for forward shuttle (step forward)
      else if (e.key === 'l' && !e.ctrlKey && !e.shiftKey) {
        e.preventDefault();
        videoPreviewRef.current?.stepForward();
      }
      // Arrow left for frame step backward
      else if (e.key === 'ArrowLeft' && !e.ctrlKey && !e.shiftKey) {
        e.preventDefault();
        videoPreviewRef.current?.stepBackward();
      }
      // Arrow right for frame step forward
      else if (e.key === 'ArrowRight' && !e.ctrlKey && !e.shiftKey) {
        e.preventDefault();
        videoPreviewRef.current?.stepForward();
      }
      // Ctrl+K for keyboard shortcuts
      else if ((e.ctrlKey || e.metaKey) && e.key === 'k') {
        e.preventDefault();
        setShowKeyboardShortcuts(true);
      }
      // Ctrl+S for save (prevent browser save)
      else if ((e.ctrlKey || e.metaKey) && e.key === 's') {
        e.preventDefault();
        // TODO: Implement project save
      }
      // Ctrl+E for export
      else if ((e.ctrlKey || e.metaKey) && e.key === 'e') {
        e.preventDefault();
        handleExportVideo();
      }
      // Delete key to delete selected clip
      else if (e.key === 'Delete' && selectedClipId) {
        e.preventDefault();
        handleDeleteClip();
      }
    };

    window.addEventListener('keydown', handleKeyDown);
    return () => window.removeEventListener('keydown', handleKeyDown);
    // handleDeleteClip and handleExportVideo are stable functions and don't need to be in deps
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [selectedClipId, isPlaying]);

  const handleUpdateClip = (updates: Partial<TimelineClip>) => {
    if (!selectedClipId) return;

    setClips((prevClips) =>
      prevClips.map((clip) => (clip.id === selectedClipId ? { ...clip, ...updates } : clip))
    );
  };

  const handleDeleteClip = () => {
    if (!selectedClipId) return;

    setClips((prevClips) => prevClips.filter((clip) => clip.id !== selectedClipId));
    setSelectedClipId(null);
  };

  const handleAddClip = (_trackId: string, clip: TimelineClip) => {
    setClips((prevClips) => [...prevClips, clip]);
  };

  const handleTrackToggleVisibility = (trackId: string) => {
    setTracks((prevTracks) =>
      prevTracks.map((track) =>
        track.id === trackId ? { ...track, visible: !track.visible } : track
      )
    );
  };

  const handleTrackToggleLock = (trackId: string) => {
    setTracks((prevTracks) =>
      prevTracks.map((track) =>
        track.id === trackId ? { ...track, locked: !track.locked } : track
      )
    );
  };

  const selectedClip = clips.find((clip) => clip.id === selectedClipId);

  const handleImportMedia = () => {
    // Trigger file picker in MediaLibraryPanel
    mediaLibraryRef.current?.openFilePicker();
  };

  const handleExportVideo = () => {
    // TODO: Implement video export
  };

  return (
    <EditorLayout
      mediaLibrary={<MediaLibraryPanel ref={mediaLibraryRef} />}
      preview={
        <VideoPreviewPanel
          currentTime={currentTime}
          onTimeUpdate={setCurrentTime}
          onPlay={() => setIsPlaying(true)}
          onPause={() => setIsPlaying(false)}
        />
      }
      timeline={
        <TimelinePanel
          clips={clips}
          tracks={tracks}
          currentTime={currentTime}
          onTimeChange={setCurrentTime}
          onClipSelect={setSelectedClipId}
          selectedClipId={selectedClipId}
          onClipAdd={handleAddClip}
          onTrackToggleVisibility={handleTrackToggleVisibility}
          onTrackToggleLock={handleTrackToggleLock}
        />
      }
      properties={
        <PropertiesPanel
          selectedClip={selectedClip}
          onUpdateClip={handleUpdateClip}
          onDeleteClip={handleDeleteClip}
        />
      }
      onImportMedia={handleImportMedia}
      onExportVideo={handleExportVideo}
      onShowKeyboardShortcuts={() => setShowKeyboardShortcuts(true)}
    />
  );
}
