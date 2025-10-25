import { useState, useRef, DragEvent, forwardRef, useImperativeHandle } from 'react';
import {
  makeStyles,
  tokens,
  Text,
  Button,
  Spinner,
  Card,
  CardHeader,
  CardPreview,
  Menu,
  MenuTrigger,
  MenuList,
  MenuItem,
  MenuPopover,
  ProgressBar,
} from '@fluentui/react-components';
import {
  Add24Regular,
  VideoClip24Regular,
  MusicNote224Regular,
  Image24Regular,
  Dismiss24Regular,
  Delete24Regular,
  Rename24Regular,
} from '@fluentui/react-icons';
import {
  generateVideoThumbnails,
  generateWaveform,
  getMediaDuration,
  isSupportedMediaType,
  getMediaPreview,
} from '../../utils/mediaProcessing';

const useStyles = makeStyles({
  container: {
    display: 'flex',
    flexDirection: 'column',
    height: '100%',
    backgroundColor: tokens.colorNeutralBackground2,
    overflow: 'hidden',
  },
  header: {
    padding: tokens.spacingVerticalM,
    borderBottom: `1px solid ${tokens.colorNeutralStroke1}`,
    backgroundColor: tokens.colorNeutralBackground1,
  },
  title: {
    fontWeight: tokens.fontWeightSemibold,
    fontSize: tokens.fontSizeBase300,
    marginBottom: tokens.spacingVerticalXS,
  },
  subtitle: {
    fontSize: tokens.fontSizeBase200,
    color: tokens.colorNeutralForeground3,
  },
  uploadArea: {
    margin: tokens.spacingVerticalM,
    padding: tokens.spacingVerticalXXL,
    border: `2px dashed ${tokens.colorNeutralStroke1}`,
    borderRadius: tokens.borderRadiusMedium,
    textAlign: 'center',
    cursor: 'pointer',
    backgroundColor: tokens.colorNeutralBackground3,
    transitionProperty: 'all',
    transitionDuration: '0.2s',
    transitionTimingFunction: 'ease',
    '&:hover': {
      border: `2px dashed ${tokens.colorBrandStroke1}`,
      backgroundColor: tokens.colorNeutralBackground1Hover,
    },
  },
  uploadAreaDragging: {
    border: `2px dashed ${tokens.colorBrandStroke1}`,
    backgroundColor: tokens.colorBrandBackground2,
  },
  uploadIcon: {
    fontSize: '48px',
    color: tokens.colorNeutralForeground3,
    marginBottom: tokens.spacingVerticalM,
  },
  uploadText: {
    fontSize: tokens.fontSizeBase300,
    color: tokens.colorNeutralForeground2,
    marginBottom: tokens.spacingVerticalS,
  },
  uploadHint: {
    fontSize: tokens.fontSizeBase200,
    color: tokens.colorNeutralForeground3,
  },
  libraryContent: {
    flex: 1,
    overflow: 'auto',
    padding: tokens.spacingVerticalM,
  },
  clipGrid: {
    display: 'grid',
    gridTemplateColumns: 'repeat(auto-fill, minmax(120px, 1fr))',
    gap: tokens.spacingHorizontalM,
  },
  clipCard: {
    cursor: 'grab',
    position: 'relative',
    '&:hover': {
      boxShadow: tokens.shadow8,
    },
    '&:active': {
      cursor: 'grabbing',
    },
  },
  clipPreview: {
    height: '80px',
    display: 'flex',
    alignItems: 'center',
    justifyContent: 'center',
    backgroundColor: tokens.colorNeutralBackground4,
    fontSize: '32px',
  },
  clipHeader: {
    padding: tokens.spacingVerticalXS,
  },
  clipName: {
    fontSize: tokens.fontSizeBase200,
    overflow: 'hidden',
    textOverflow: 'ellipsis',
    whiteSpace: 'nowrap',
  },
  clipType: {
    fontSize: tokens.fontSizeBase100,
    color: tokens.colorNeutralForeground3,
  },
  clipMetadata: {
    fontSize: tokens.fontSizeBase100,
    color: tokens.colorNeutralForeground3,
    marginTop: '2px',
  },
  removeButton: {
    position: 'absolute',
    top: '4px',
    right: '4px',
    minWidth: '24px',
    minHeight: '24px',
    padding: '4px',
  },
  loadingContainer: {
    display: 'flex',
    flexDirection: 'column',
    alignItems: 'center',
    justifyContent: 'center',
    padding: tokens.spacingVerticalXXL,
    gap: tokens.spacingVerticalM,
  },
  emptyState: {
    display: 'flex',
    flexDirection: 'column',
    alignItems: 'center',
    justifyContent: 'center',
    padding: tokens.spacingVerticalXXL,
    color: tokens.colorNeutralForeground3,
    textAlign: 'center',
  },
});

export interface MediaClip {
  id: string;
  name: string;
  type: 'video' | 'audio' | 'image';
  file: File;
  duration?: number;
  fileSize?: number;
  resolution?: string;
  thumbnails?: Array<{ dataUrl: string; timestamp: number }>;
  waveform?: { peaks: number[]; duration: number };
  preview?: string;
  uploadProgress?: number;
}

interface MediaLibraryPanelProps {
  onClipDragStart?: (clip: MediaClip) => void;
  onClipDragEnd?: () => void;
}

export interface MediaLibraryPanelRef {
  openFilePicker: () => void;
}

export const MediaLibraryPanel = forwardRef<MediaLibraryPanelRef, MediaLibraryPanelProps>(
  ({ onClipDragStart, onClipDragEnd }, ref) => {
  const styles = useStyles();
  const fileInputRef = useRef<HTMLInputElement>(null);
  const [clips, setClips] = useState<MediaClip[]>([]);
  const [isDraggingOver, setIsDraggingOver] = useState(false);
  const [isUploading, setIsUploading] = useState(false);

  // Expose methods to parent component
  useImperativeHandle(ref, () => ({
    openFilePicker: () => {
      fileInputRef.current?.click();
    },
  }));

  const formatFileSize = (bytes: number): string => {
    if (bytes < 1024) return `${bytes} B`;
    if (bytes < 1024 * 1024) return `${(bytes / 1024).toFixed(1)} KB`;
    return `${(bytes / (1024 * 1024)).toFixed(1)} MB`;
  };

  const getClipType = (file: File): 'video' | 'audio' | 'image' => {
    if (file.type.startsWith('video/')) return 'video';
    if (file.type.startsWith('audio/')) return 'audio';
    if (file.type.startsWith('image/')) return 'image';
    return 'video'; // default
  };

  const getClipIcon = (type: 'video' | 'audio' | 'image') => {
    switch (type) {
      case 'video':
        return <VideoClip24Regular />;
      case 'audio':
        return <MusicNote224Regular />;
      case 'image':
        return <Image24Regular />;
    }
  };

  const processMediaFile = async (file: File, clipId: string, type: 'video' | 'audio' | 'image') => {
    try {
      // Update progress
      setClips((prev) =>
        prev.map((clip) =>
          clip.id === clipId ? { ...clip, uploadProgress: 10 } : clip
        )
      );

      // Get duration for audio/video files
      let duration: number | undefined;
      if (type === 'video' || type === 'audio') {
        try {
          duration = await getMediaDuration(file);
        } catch (error) {
          console.warn('Failed to get media duration:', error);
        }
      }

      setClips((prev) =>
        prev.map((clip) =>
          clip.id === clipId ? { ...clip, uploadProgress: 30, duration } : clip
        )
      );

      // Generate thumbnails for video files
      let thumbnails: Array<{ dataUrl: string; timestamp: number }> | undefined;
      if (type === 'video') {
        try {
          thumbnails = await generateVideoThumbnails(file, 5);
        } catch (error) {
          console.warn('Failed to generate thumbnails:', error);
        }
      }

      setClips((prev) =>
        prev.map((clip) =>
          clip.id === clipId ? { ...clip, uploadProgress: 60, thumbnails } : clip
        )
      );

      // Generate waveform for audio/video files
      let waveform: { peaks: number[]; duration: number } | undefined;
      if (type === 'video' || type === 'audio') {
        try {
          waveform = await generateWaveform(file, 100);
        } catch (error) {
          console.warn('Failed to generate waveform:', error);
        }
      }

      setClips((prev) =>
        prev.map((clip) =>
          clip.id === clipId ? { ...clip, uploadProgress: 80, waveform } : clip
        )
      );

      // Get preview image
      let preview: string | undefined;
      try {
        const previewUrl = await getMediaPreview(file, type);
        preview = previewUrl || undefined;
      } catch (error) {
        console.warn('Failed to generate preview:', error);
      }

      // Mark as complete
      setClips((prev) =>
        prev.map((clip) =>
          clip.id === clipId ? { ...clip, uploadProgress: 100, preview } : clip
        )
      );

      // Remove progress after a short delay
      setTimeout(() => {
        setClips((prev) =>
          prev.map((clip) =>
            clip.id === clipId ? { ...clip, uploadProgress: undefined } : clip
          )
        );
      }, 1000);
    } catch (error) {
      console.error('Error processing media file:', error);
      // Remove failed clip
      setClips((prev) => prev.filter((clip) => clip.id !== clipId));
    }
  };

  const handleFiles = async (files: FileList | null) => {
    if (!files || files.length === 0) return;

    setIsUploading(true);

    const validFiles = Array.from(files).filter((file) => {
      if (!isSupportedMediaType(file)) {
        alert(`Unsupported file type: ${file.name}`);
        return false;
      }
      return true;
    });

    if (validFiles.length === 0) {
      setIsUploading(false);
      return;
    }

    const newClips: MediaClip[] = validFiles.map((file) => ({
      id: `${Date.now()}-${Math.random().toString(36).substr(2, 9)}`,
      name: file.name,
      type: getClipType(file),
      file,
      fileSize: file.size,
      uploadProgress: 0,
    }));

    setClips((prev) => [...prev, ...newClips]);

    // Process each file
    for (const clip of newClips) {
      processMediaFile(clip.file, clip.id, clip.type);
    }

    setIsUploading(false);
    setIsUploading(false);
  };

  const handleFileInputChange = (e: React.ChangeEvent<HTMLInputElement>) => {
    handleFiles(e.target.files);
    if (fileInputRef.current) {
      fileInputRef.current.value = '';
    }
  };

  const handleDragOver = (e: DragEvent<HTMLDivElement>) => {
    e.preventDefault();
    e.stopPropagation();
    setIsDraggingOver(true);
  };

  const handleDragLeave = (e: DragEvent<HTMLDivElement>) => {
    e.preventDefault();
    e.stopPropagation();
    setIsDraggingOver(false);
  };

  const handleDrop = (e: DragEvent<HTMLDivElement>) => {
    e.preventDefault();
    e.stopPropagation();
    setIsDraggingOver(false);

    const files = e.dataTransfer.files;
    handleFiles(files);
  };

  const handleUploadAreaClick = () => {
    fileInputRef.current?.click();
  };

  const handleClipDragStart = (e: DragEvent<HTMLDivElement>, clip: MediaClip) => {
    e.dataTransfer.effectAllowed = 'copy';
    e.dataTransfer.setData('application/json', JSON.stringify(clip));
    onClipDragStart?.(clip);
  };

  const handleClipDragEnd = () => {
    onClipDragEnd?.();
  };

  const handleRemoveClip = (clipId: string) => {
    setClips((prev) => prev.filter((c) => c.id !== clipId));
  };

  const handleRenameClip = (clipId: string, newName: string) => {
    setClips((prev) =>
      prev.map((c) => (c.id === clipId ? { ...c, name: newName } : c))
    );
  };

  return (
    <div className={styles.container}>
      <div className={styles.header}>
        <Text className={styles.title}>Media Library</Text>
        <Text className={styles.subtitle}>Drag clips to timeline</Text>
      </div>

      <input
        ref={fileInputRef}
        type="file"
        accept="video/*,audio/*,image/*"
        multiple
        style={{ display: 'none' }}
        onChange={handleFileInputChange}
      />

      <div
        className={`${styles.uploadArea} ${isDraggingOver ? styles.uploadAreaDragging : ''}`}
        onDragOver={handleDragOver}
        onDragLeave={handleDragLeave}
        onDrop={handleDrop}
        onClick={handleUploadAreaClick}
        role="button"
        tabIndex={0}
        onKeyDown={(e) => {
          if (e.key === 'Enter' || e.key === ' ') {
            e.preventDefault();
            handleUploadAreaClick();
          }
        }}
        aria-label="Upload media files"
      >
        <div className={styles.uploadIcon}>
          <Add24Regular />
        </div>
        <Text className={styles.uploadText}>Drop files here or click to upload</Text>
        <Text className={styles.uploadHint}>Supports video, audio, and image files</Text>
      </div>

      <div className={styles.libraryContent}>
        {isUploading && (
          <div className={styles.loadingContainer}>
            <Spinner size="large" label="Processing files..." />
          </div>
        )}

        {!isUploading && clips.length === 0 && (
          <div className={styles.emptyState}>
            <Text>No clips in library</Text>
            <Text size={200}>Upload files to get started</Text>
          </div>
        )}

        {!isUploading && clips.length > 0 && (
          <div className={styles.clipGrid}>
            {clips.map((clip) => (
              <Menu key={clip.id}>
                <MenuTrigger disableButtonEnhancement>
                  <Card
                    className={styles.clipCard}
                    draggable
                    onDragStart={(e) => handleClipDragStart(e, clip)}
                    onDragEnd={handleClipDragEnd}
                  >
                    <Button
                      className={styles.removeButton}
                      appearance="subtle"
                      size="small"
                      icon={<Dismiss24Regular />}
                      onClick={(e) => {
                        e.stopPropagation();
                        handleRemoveClip(clip.id);
                      }}
                      aria-label="Remove clip"
                    />
                    <CardPreview className={styles.clipPreview}>
                      {clip.preview ? (
                        <img 
                          src={clip.preview} 
                          alt={clip.name} 
                          style={{ width: '100%', height: '100%', objectFit: 'cover' }}
                        />
                      ) : (
                        getClipIcon(clip.type)
                      )}
                    </CardPreview>
                    <CardHeader
                      className={styles.clipHeader}
                      header={
                        <div>
                          <Text className={styles.clipName} title={clip.name}>
                            {clip.name}
                          </Text>
                          <Text className={styles.clipType}>{clip.type}</Text>
                          {clip.duration && (
                            <Text className={styles.clipMetadata}>
                              {Math.floor(clip.duration)}s
                            </Text>
                          )}
                          {clip.fileSize && (
                            <Text className={styles.clipMetadata}>
                              {formatFileSize(clip.fileSize)}
                            </Text>
                          )}
                          {clip.uploadProgress !== undefined && clip.uploadProgress < 100 && (
                            <ProgressBar value={clip.uploadProgress / 100} />
                          )}
                        </div>
                      }
                    />
                  </Card>
                </MenuTrigger>
                <MenuPopover>
                  <MenuList>
                    <MenuItem
                      icon={<Rename24Regular />}
                      onClick={() => {
                        const newName = prompt('Enter new name:', clip.name);
                        if (newName && newName.trim()) {
                          handleRenameClip(clip.id, newName.trim());
                        }
                      }}
                    >
                      Rename
                    </MenuItem>
                    <MenuItem
                      icon={<Delete24Regular />}
                      onClick={() => handleRemoveClip(clip.id)}
                    >
                      Delete
                    </MenuItem>
                  </MenuList>
                </MenuPopover>
              </Menu>
            ))}
          </div>
        )}
      </div>
    </div>
  );
});

MediaLibraryPanel.displayName = 'MediaLibraryPanel';
