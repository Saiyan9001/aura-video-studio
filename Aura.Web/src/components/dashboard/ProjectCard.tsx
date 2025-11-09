import {
  Card,
  CardHeader,
  CardPreview,
  makeStyles,
  tokens,
  Text,
  Badge,
  Menu,
  MenuTrigger,
  MenuPopover,
  MenuList,
  MenuItem,
  Button,
  mergeClasses,
} from '@fluentui/react-components';
import {
  Play24Regular,
  MoreVertical24Regular,
  Edit24Regular,
  Copy24Regular,
  Share24Regular,
  Delete24Regular,
} from '@fluentui/react-icons';
import { useState, useCallback, useRef, useEffect } from 'react';
import type { ProjectSummary } from '../../state/dashboard';

const useStyles = makeStyles({
  card: {
    width: '100%',
    height: '100%',
    cursor: 'pointer',
    transition: 'transform 0.2s ease, box-shadow 0.2s ease',
    ':hover': {
      transform: 'translateY(-4px)',
      boxShadow: tokens.shadow16,
    },
  },
  cardDragging: {
    opacity: 0.5,
  },
  preview: {
    position: 'relative',
    aspectRatio: '16 / 9',
    backgroundColor: tokens.colorNeutralBackground3,
    overflow: 'hidden',
  },
  thumbnail: {
    width: '100%',
    height: '100%',
    objectFit: 'cover',
  },
  playOverlay: {
    position: 'absolute',
    top: '50%',
    left: '50%',
    transform: 'translate(-50%, -50%)',
    opacity: 0,
    transition: 'opacity 0.2s ease',
    backgroundColor: 'rgba(0, 0, 0, 0.6)',
    borderRadius: '50%',
    padding: tokens.spacingVerticalM,
    display: 'flex',
    alignItems: 'center',
    justifyContent: 'center',
    pointerEvents: 'none',
  },
  previewHover: {
    ':hover .play-overlay': {
      opacity: 1,
    },
  },
  videoPreview: {
    position: 'absolute',
    top: 0,
    left: 0,
    width: '100%',
    height: '100%',
    objectFit: 'cover',
    opacity: 0,
    transition: 'opacity 0.3s ease',
  },
  videoPreviewVisible: {
    opacity: 1,
  },
  header: {
    display: 'flex',
    justifyContent: 'space-between',
    alignItems: 'flex-start',
    gap: tokens.spacingHorizontalS,
  },
  titleSection: {
    flex: 1,
    minWidth: 0,
  },
  title: {
    fontWeight: tokens.fontWeightSemibold,
    fontSize: tokens.fontSizeBase300,
    overflow: 'hidden',
    textOverflow: 'ellipsis',
    whiteSpace: 'nowrap',
    marginBottom: tokens.spacingVerticalXS,
  },
  metadata: {
    display: 'flex',
    alignItems: 'center',
    gap: tokens.spacingHorizontalS,
    flexWrap: 'wrap',
    fontSize: tokens.fontSizeBase200,
    color: tokens.colorNeutralForeground3,
  },
  badges: {
    display: 'flex',
    gap: tokens.spacingHorizontalXS,
    marginTop: tokens.spacingVerticalXS,
  },
  progressBar: {
    width: '100%',
    height: '4px',
    backgroundColor: tokens.colorNeutralBackground3,
    borderRadius: tokens.borderRadiusMedium,
    overflow: 'hidden',
    marginTop: tokens.spacingVerticalXS,
  },
  progressFill: {
    height: '100%',
    backgroundColor: tokens.colorBrandBackground,
    transition: 'width 0.3s ease',
  },
  skeletonCard: {
    width: '100%',
    height: '100%',
  },
  skeletonPreview: {
    aspectRatio: '16 / 9',
    backgroundColor: tokens.colorNeutralBackground3,
    animation: 'pulse 1.5s ease-in-out infinite',
  },
  '@keyframes pulse': {
    from: {
      opacity: '1',
    },
    '50%': {
      opacity: '0.5',
    },
    to: {
      opacity: '1',
    },
  },
  errorCard: {
    borderLeftWidth: '4px',
    borderLeftStyle: 'solid' as const,
    borderLeftColor: tokens.colorPaletteRedForeground1,
  },
  errorMessage: {
    color: tokens.colorPaletteRedForeground1,
    fontSize: tokens.fontSizeBase200,
    marginTop: tokens.spacingVerticalXS,
  },
});

interface ProjectCardProps {
  project: ProjectSummary;
  loading?: boolean;
  error?: string;
  onPreview?: (project: ProjectSummary) => void;
  onEdit?: (project: ProjectSummary) => void;
  onDuplicate?: (project: ProjectSummary) => void;
  onShare?: (project: ProjectSummary) => void;
  onDelete?: (project: ProjectSummary) => void;
  onRetry?: () => void;
  draggable?: boolean;
  onDragStart?: (e: React.DragEvent<HTMLDivElement>) => void;
  onDragEnd?: (e: React.DragEvent<HTMLDivElement>) => void;
  onDragOver?: (e: React.DragEvent<HTMLDivElement>) => void;
  onDrop?: (e: React.DragEvent<HTMLDivElement>) => void;
}

export function ProjectCard({
  project,
  loading = false,
  error,
  onPreview,
  onEdit,
  onDuplicate,
  onShare,
  onDelete,
  onRetry,
  draggable = false,
  onDragStart,
  onDragEnd,
  onDragOver,
  onDrop,
}: ProjectCardProps) {
  const styles = useStyles();
  const [isDragging, setIsDragging] = useState(false);
  const [showVideoPreview, setShowVideoPreview] = useState(false);
  const videoRef = useRef<HTMLVideoElement>(null);
  const previewTimeoutRef = useRef<ReturnType<typeof setTimeout>>();

  useEffect(() => {
    return () => {
      if (previewTimeoutRef.current) {
        clearTimeout(previewTimeoutRef.current);
      }
    };
  }, []);

  const handleMouseEnter = useCallback(() => {
    if (project.videoUrl && project.status === 'complete') {
      previewTimeoutRef.current = setTimeout(() => {
        setShowVideoPreview(true);
        if (videoRef.current) {
          videoRef.current.currentTime = 0;
          videoRef.current.play().catch(() => {
            // Ignore play errors (autoplay restrictions)
          });
        }
      }, 500);
    }
  }, [project.videoUrl, project.status]);

  const handleMouseLeave = useCallback(() => {
    setShowVideoPreview(false);
    if (previewTimeoutRef.current) {
      clearTimeout(previewTimeoutRef.current);
    }
    if (videoRef.current) {
      videoRef.current.pause();
      videoRef.current.currentTime = 0;
    }
  }, []);

  const handleDragStart = useCallback(
    (e: React.DragEvent<HTMLDivElement>) => {
      setIsDragging(true);
      if (onDragStart) {
        onDragStart(e);
      }
    },
    [onDragStart]
  );

  const handleDragEnd = useCallback(
    (e: React.DragEvent<HTMLDivElement>) => {
      setIsDragging(false);
      if (onDragEnd) {
        onDragEnd(e);
      }
    },
    [onDragEnd]
  );

  const getStatusBadge = () => {
    switch (project.status) {
      case 'draft':
        return (
          <Badge appearance="outline" color="informative">
            Draft
          </Badge>
        );
      case 'processing':
        return (
          <Badge appearance="filled" color="warning">
            Processing
          </Badge>
        );
      case 'complete':
        return (
          <Badge appearance="filled" color="success">
            Complete
          </Badge>
        );
      case 'failed':
        return (
          <Badge appearance="filled" color="danger">
            Failed
          </Badge>
        );
      default:
        return null;
    }
  };

  const formatDuration = (seconds: number) => {
    const mins = Math.floor(seconds / 60);
    const secs = Math.floor(seconds % 60);
    return `${mins}:${secs.toString().padStart(2, '0')}`;
  };

  const formatRelativeTime = (dateString: string) => {
    const date = new Date(dateString);
    const now = new Date();
    const diffInSeconds = Math.floor((now.getTime() - date.getTime()) / 1000);

    if (diffInSeconds < 60) return 'Just now';
    if (diffInSeconds < 3600) return `${Math.floor(diffInSeconds / 60)}m ago`;
    if (diffInSeconds < 86400) return `${Math.floor(diffInSeconds / 3600)}h ago`;
    if (diffInSeconds < 604800) return `${Math.floor(diffInSeconds / 86400)}d ago`;
    return date.toLocaleDateString();
  };

  if (loading) {
    return (
      <Card className={styles.skeletonCard}>
        <div className={styles.skeletonPreview} />
        <CardHeader
          header={
            <div
              style={{
                width: '80%',
                height: '16px',
                backgroundColor: tokens.colorNeutralBackground3,
              }}
            />
          }
          description={
            <div
              style={{
                width: '60%',
                height: '12px',
                backgroundColor: tokens.colorNeutralBackground3,
                marginTop: '8px',
              }}
            />
          }
        />
      </Card>
    );
  }

  return (
    <Card
      className={mergeClasses(
        styles.card,
        isDragging && styles.cardDragging,
        error && styles.errorCard
      )}
      draggable={draggable}
      onDragStart={handleDragStart}
      onDragEnd={handleDragEnd}
      onDragOver={onDragOver}
      onDrop={onDrop}
    >
      <CardPreview
        className={mergeClasses(styles.preview, styles.previewHover)}
        onClick={() => onPreview?.(project)}
        onMouseEnter={handleMouseEnter}
        onMouseLeave={handleMouseLeave}
      >
        {project.thumbnail ? (
          <img src={project.thumbnail} alt={project.name} className={styles.thumbnail} />
        ) : (
          <div
            style={{
              display: 'flex',
              alignItems: 'center',
              justifyContent: 'center',
              height: '100%',
              backgroundColor: tokens.colorNeutralBackground3,
            }}
          >
            <Text>No Preview</Text>
          </div>
        )}
        {project.videoUrl && project.status === 'complete' && (
          <video
            ref={videoRef}
            src={project.videoUrl}
            className={mergeClasses(
              styles.videoPreview,
              showVideoPreview && styles.videoPreviewVisible
            )}
            muted
            loop
            playsInline
            onEnded={() => {
              if (videoRef.current) {
                videoRef.current.currentTime = 0;
                videoRef.current.play().catch(() => {
                  // Ignore play errors
                });
              }
            }}
          />
        )}
        <div className={mergeClasses(styles.playOverlay, 'play-overlay')}>
          <Play24Regular style={{ color: 'white', fontSize: '32px' }} />
        </div>
      </CardPreview>

      <CardHeader
        header={
          <div className={styles.header}>
            <div className={styles.titleSection}>
              <div className={styles.title} onClick={() => onEdit?.(project)} title={project.name}>
                {project.name}
              </div>
              <div className={styles.metadata}>
                <Text size={200}>{formatRelativeTime(project.createdAt)}</Text>
                <Text size={200}>•</Text>
                <Text size={200}>{formatDuration(project.duration)}</Text>
                {project.viewCount !== undefined && (
                  <>
                    <Text size={200}>•</Text>
                    <Text size={200}>{project.viewCount} views</Text>
                  </>
                )}
              </div>
              {error && (
                <div className={styles.errorMessage}>
                  {error}
                  {onRetry && (
                    <Button size="small" appearance="transparent" onClick={onRetry}>
                      Retry
                    </Button>
                  )}
                </div>
              )}
              {project.status === 'processing' && project.progress !== undefined && (
                <div className={styles.progressBar}>
                  <div className={styles.progressFill} style={{ width: `${project.progress}%` }} />
                </div>
              )}
              <div className={styles.badges}>{getStatusBadge()}</div>
            </div>
            <Menu>
              <MenuTrigger disableButtonEnhancement>
                <Button
                  appearance="transparent"
                  icon={<MoreVertical24Regular />}
                  size="small"
                  aria-label="More options"
                />
              </MenuTrigger>
              <MenuPopover>
                <MenuList>
                  <MenuItem icon={<Edit24Regular />} onClick={() => onEdit?.(project)}>
                    Edit
                  </MenuItem>
                  <MenuItem icon={<Copy24Regular />} onClick={() => onDuplicate?.(project)}>
                    Duplicate
                  </MenuItem>
                  <MenuItem icon={<Share24Regular />} onClick={() => onShare?.(project)}>
                    Share
                  </MenuItem>
                  <MenuItem icon={<Delete24Regular />} onClick={() => onDelete?.(project)}>
                    Delete
                  </MenuItem>
                </MenuList>
              </MenuPopover>
            </Menu>
          </div>
        }
      />
    </Card>
  );
}

export function ProjectCardSkeleton() {
  const styles = useStyles();
  return (
    <Card className={styles.skeletonCard}>
      <div className={styles.skeletonPreview} />
      <CardHeader
        header={
          <div
            style={{
              width: '80%',
              height: '16px',
              backgroundColor: tokens.colorNeutralBackground3,
            }}
          />
        }
        description={
          <div
            style={{
              width: '60%',
              height: '12px',
              backgroundColor: tokens.colorNeutralBackground3,
              marginTop: '8px',
            }}
          />
        }
      />
    </Card>
  );
}
