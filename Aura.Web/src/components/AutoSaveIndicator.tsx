import { Text, Button, Spinner } from '@fluentui/react-components';
import {
  Checkmark24Regular,
  ArrowSync24Regular,
  Warning24Regular,
  Save24Regular,
} from '@fluentui/react-icons';
import React, { useState, useEffect } from 'react';

export type SaveStatus = 'saved' | 'saving' | 'error' | 'idle';

interface AutoSaveIndicatorProps {
  status: SaveStatus;
  lastSavedAt?: Date;
  error?: string;
  onRetry?: () => void;
  onManualSave?: () => void;
}

export const AutoSaveIndicator: React.FC<AutoSaveIndicatorProps> = ({
  status,
  lastSavedAt,
  error,
  onRetry,
  onManualSave,
}) => {
  const [timeAgo, setTimeAgo] = useState<string>('');

  useEffect(() => {
    if (!lastSavedAt) return;

    const updateTimeAgo = () => {
      const now = new Date();
      const diffMs = now.getTime() - lastSavedAt.getTime();
      const diffSeconds = Math.floor(diffMs / 1000);
      const diffMinutes = Math.floor(diffSeconds / 60);

      if (diffSeconds < 10) {
        setTimeAgo('just now');
      } else if (diffSeconds < 60) {
        setTimeAgo(`${diffSeconds} seconds ago`);
      } else if (diffMinutes < 60) {
        setTimeAgo(`${diffMinutes} minute${diffMinutes > 1 ? 's' : ''} ago`);
      } else {
        const diffHours = Math.floor(diffMinutes / 60);
        setTimeAgo(`${diffHours} hour${diffHours > 1 ? 's' : ''} ago`);
      }
    };

    updateTimeAgo();
    const interval = setInterval(updateTimeAgo, 10000); // Update every 10 seconds

    return () => clearInterval(interval);
  }, [lastSavedAt]);

  const renderStatusIcon = () => {
    switch (status) {
      case 'saved':
        return <Checkmark24Regular style={{ color: 'var(--colorPaletteGreenForeground1)' }} />;
      case 'saving':
        return <Spinner size="tiny" />;
      case 'error':
        return <Warning24Regular style={{ color: 'var(--colorPaletteRedForeground1)' }} />;
      default:
        return <Save24Regular />;
    }
  };

  const renderStatusText = () => {
    switch (status) {
      case 'saved':
        return lastSavedAt ? `Auto-saved ${timeAgo}` : 'Saved';
      case 'saving':
        return 'Saving...';
      case 'error':
        return 'Syncing paused';
      default:
        return 'Not saved';
    }
  };

  return (
    <div
      style={{
        display: 'flex',
        alignItems: 'center',
        gap: '8px',
        padding: '4px 12px',
        borderRadius: '4px',
        backgroundColor:
          status === 'error'
            ? 'var(--colorPaletteRedBackground2)'
            : status === 'saved'
              ? 'var(--colorNeutralBackground1)'
              : 'var(--colorNeutralBackground2)',
        border: `1px solid ${
          status === 'error' ? 'var(--colorPaletteRedBorder1)' : 'var(--colorNeutralStroke2)'
        }`,
      }}
    >
      {renderStatusIcon()}

      <Text size={200} style={{ whiteSpace: 'nowrap' }}>
        {renderStatusText()}
      </Text>

      {status === 'error' && onRetry && (
        <Button
          size="small"
          appearance="subtle"
          icon={<ArrowSync24Regular />}
          onClick={onRetry}
          title={error || 'Retry saving'}
        >
          Retry
        </Button>
      )}

      {onManualSave && status !== 'saving' && (
        <Button
          size="small"
          appearance="subtle"
          icon={<Save24Regular />}
          onClick={onManualSave}
          title="Save progress manually"
        >
          Save Now
        </Button>
      )}
    </div>
  );
};
