import { makeStyles, tokens, Text } from '@fluentui/react-components';
import type { FC } from 'react';
import { useState, useEffect } from 'react';
import type { SubtitleCue } from '../../services/subtitleService';

interface SubtitleDisplayProps {
  cues: SubtitleCue[];
  currentTime: number;
  enabled?: boolean;
}

const useStyles = makeStyles({
  container: {
    position: 'absolute',
    bottom: '60px',
    left: '50%',
    transform: 'translateX(-50%)',
    maxWidth: '80%',
    textAlign: 'center',
    pointerEvents: 'none',
    zIndex: 10,
  },
  subtitle: {
    backgroundColor: 'rgba(0, 0, 0, 0.8)',
    color: tokens.colorNeutralForegroundInverted,
    padding: `${tokens.spacingVerticalS} ${tokens.spacingHorizontalM}`,
    borderRadius: tokens.borderRadiusMedium,
    fontSize: tokens.fontSizeBase400,
    lineHeight: tokens.lineHeightBase400,
    fontWeight: tokens.fontWeightSemibold,
  },
});

export const SubtitleDisplay: FC<SubtitleDisplayProps> = ({
  cues,
  currentTime,
  enabled = true,
}) => {
  const styles = useStyles();
  const [currentSubtitle, setCurrentSubtitle] = useState<string | null>(null);

  useEffect(() => {
    if (!enabled) {
      setCurrentSubtitle(null);
      return;
    }

    const activeCue = cues.find(
      (cue) => currentTime >= cue.startTime && currentTime <= cue.endTime
    );

    setCurrentSubtitle(activeCue?.text ?? null);
  }, [cues, currentTime, enabled]);

  if (!currentSubtitle) {
    return null;
  }

  return (
    <div className={styles.container}>
      <Text className={styles.subtitle}>{currentSubtitle}</Text>
    </div>
  );
};
