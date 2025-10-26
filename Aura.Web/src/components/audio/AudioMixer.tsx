import {
  Text,
  Slider,
  Switch,
  Button,
  makeStyles,
  tokens,
  Badge,
  Tooltip,
} from '@fluentui/react-components';
import { Speaker224Regular, Info16Regular } from '@fluentui/react-icons';
import React, { useState, useEffect, useCallback } from 'react';
import {
  audioIntelligenceService,
  AudioMixing,
  MixingSuggestionsRequest,
} from '../../services/audioIntelligenceService';

const useStyles = makeStyles({
  container: {
    display: 'flex',
    flexDirection: 'column',
    gap: tokens.spacingVerticalM,
    padding: tokens.spacingVerticalM,
  },
  header: {
    display: 'flex',
    justifyContent: 'space-between',
    alignItems: 'center',
    marginBottom: tokens.spacingVerticalM,
  },
  section: {
    display: 'flex',
    flexDirection: 'column',
    gap: tokens.spacingVerticalS,
  },
  sliderGroup: {
    display: 'flex',
    flexDirection: 'column',
    gap: tokens.spacingVerticalXS,
    padding: tokens.spacingVerticalS,
    backgroundColor: tokens.colorNeutralBackground1,
    borderRadius: tokens.borderRadiusMedium,
  },
  sliderRow: {
    display: 'flex',
    alignItems: 'center',
    gap: tokens.spacingHorizontalM,
  },
  sliderLabel: {
    minWidth: '120px',
    fontSize: tokens.fontSizeBase300,
  },
  slider: {
    flex: 1,
  },
  valueDisplay: {
    minWidth: '50px',
    textAlign: 'right',
    fontSize: tokens.fontSizeBase300,
    color: tokens.colorBrandForeground1,
    fontWeight: tokens.fontWeightSemibold,
  },
  suggestions: {
    display: 'flex',
    flexDirection: 'column',
    gap: tokens.spacingVerticalS,
    padding: tokens.spacingVerticalM,
    backgroundColor: tokens.colorNeutralBackground3,
    borderRadius: tokens.borderRadiusMedium,
    borderLeft: `3px solid ${tokens.colorBrandBackground}`,
  },
  issuesList: {
    display: 'flex',
    flexDirection: 'column',
    gap: tokens.spacingVerticalXS,
    marginTop: tokens.spacingVerticalS,
  },
  issue: {
    padding: tokens.spacingVerticalS,
    backgroundColor: tokens.colorPaletteRedBackground2,
    borderRadius: tokens.borderRadiusSmall,
    fontSize: tokens.fontSizeBase300,
  },
  controls: {
    display: 'flex',
    gap: tokens.spacingHorizontalM,
  },
});

interface AudioMixerProps {
  contentType?: string;
  hasNarration?: boolean;
  hasMusic?: boolean;
  hasSoundEffects?: boolean;
  onMixingChange?: (mixing: AudioMixing) => void;
}

export const AudioMixer: React.FC<AudioMixerProps> = ({
  contentType = 'default',
  hasNarration = true,
  hasMusic = true,
  hasSoundEffects = false,
  onMixingChange,
}) => {
  const styles = useStyles();
  const [mixing, setMixing] = useState<AudioMixing | null>(null);
  const [validationIssues, setValidationIssues] = useState<string[]>([]);
  const [frequencyConflicts, setFrequencyConflicts] = useState<string[]>([]);
  const [loading, setLoading] = useState(false);

  const loadMixingSuggestions = useCallback(async () => {
    setLoading(true);
    try {
      const request: MixingSuggestionsRequest = {
        contentType,
        hasNarration,
        hasMusic,
        hasSoundEffects,
        targetLUFS: -14.0,
      };

      const result = await audioIntelligenceService.getMixingSuggestions(request);
      setMixing(result.mixing);
      setValidationIssues(result.validationIssues);
      setFrequencyConflicts(result.frequencyConflicts);
    } catch (error) {
      console.error('Failed to load mixing suggestions:', error);
    } finally {
      setLoading(false);
    }
  }, [contentType, hasNarration, hasMusic, hasSoundEffects]);

  useEffect(() => {
    loadMixingSuggestions();
  }, [loadMixingSuggestions]);

  const handleVolumeChange = (type: 'music' | 'narration' | 'soundEffects', value: number) => {
    if (!mixing) return;

    const updatedMixing = {
      ...mixing,
      musicVolume: type === 'music' ? value : mixing.musicVolume,
      narrationVolume: type === 'narration' ? value : mixing.narrationVolume,
      soundEffectsVolume: type === 'soundEffects' ? value : mixing.soundEffectsVolume,
    };

    setMixing(updatedMixing);
    if (onMixingChange) {
      onMixingChange(updatedMixing);
    }
  };

  const handleNormalizeChange = (checked: boolean) => {
    if (!mixing) return;

    const updatedMixing = {
      ...mixing,
      normalize: checked,
    };

    setMixing(updatedMixing);
    if (onMixingChange) {
      onMixingChange(updatedMixing);
    }
  };

  if (loading || !mixing) {
    return (
      <div className={styles.container}>
        <Text>Loading mixing suggestions...</Text>
      </div>
    );
  }

  return (
    <div className={styles.container}>
      <div className={styles.header}>
        <Text size={500} weight="semibold">
          <Speaker224Regular /> Audio Mixer
        </Text>
        <div className={styles.controls}>
          <Button appearance="secondary" onClick={loadMixingSuggestions}>
            Reset to Suggested
          </Button>
        </div>
      </div>

      <div className={styles.section}>
        <Text size={400} weight="semibold">
          Volume Levels
        </Text>

        {hasNarration && (
          <div className={styles.sliderGroup}>
            <div className={styles.sliderRow}>
              <Text className={styles.sliderLabel}>Narration</Text>
              <Slider
                className={styles.slider}
                min={0}
                max={100}
                value={mixing.narrationVolume}
                onChange={(_, data) => handleVolumeChange('narration', data.value)}
              />
              <Text className={styles.valueDisplay}>{Math.round(mixing.narrationVolume)}</Text>
            </div>
          </div>
        )}

        {hasMusic && (
          <div className={styles.sliderGroup}>
            <div className={styles.sliderRow}>
              <Text className={styles.sliderLabel}>Music</Text>
              <Slider
                className={styles.slider}
                min={0}
                max={100}
                value={mixing.musicVolume}
                onChange={(_, data) => handleVolumeChange('music', data.value)}
              />
              <Text className={styles.valueDisplay}>{Math.round(mixing.musicVolume)}</Text>
            </div>
          </div>
        )}

        {hasSoundEffects && (
          <div className={styles.sliderGroup}>
            <div className={styles.sliderRow}>
              <Text className={styles.sliderLabel}>Sound Effects</Text>
              <Slider
                className={styles.slider}
                min={0}
                max={100}
                value={mixing.soundEffectsVolume}
                onChange={(_, data) => handleVolumeChange('soundEffects', data.value)}
              />
              <Text className={styles.valueDisplay}>{Math.round(mixing.soundEffectsVolume)}</Text>
            </div>
          </div>
        )}
      </div>

      <div className={styles.section}>
        <Text size={400} weight="semibold">
          Processing
        </Text>

        <div className={styles.sliderGroup}>
          <div className={styles.sliderRow}>
            <Text className={styles.sliderLabel}>Target LUFS</Text>
            <Tooltip
              content="Loudness Units Full Scale - YouTube standard is -14 LUFS"
              relationship="description"
            >
              <Info16Regular />
            </Tooltip>
            <Badge appearance="tint" color="success">
              {mixing.targetLUFS.toFixed(1)} LUFS
            </Badge>
          </div>

          <div className={styles.sliderRow}>
            <Text className={styles.sliderLabel}>Normalize</Text>
            <Switch
              checked={mixing.normalize}
              onChange={(_, data) => handleNormalizeChange(data.checked)}
            />
          </div>

          {hasNarration && hasMusic && (
            <div className={styles.sliderRow}>
              <Text className={styles.sliderLabel}>Music Ducking</Text>
              <Tooltip
                content="Automatically reduce music volume when narration plays"
                relationship="description"
              >
                <Info16Regular />
              </Tooltip>
              <Badge appearance="tint">{mixing.ducking.duckDepthDb.toFixed(1)} dB</Badge>
            </div>
          )}
        </div>
      </div>

      {(validationIssues.length > 0 || frequencyConflicts.length > 0) && (
        <div className={styles.suggestions}>
          <Text size={400} weight="semibold">
            Recommendations
          </Text>

          {validationIssues.length > 0 && (
            <div className={styles.issuesList}>
              {validationIssues.map((issue, index) => (
                <div key={index} className={styles.issue}>
                  <Text size={300}>{issue}</Text>
                </div>
              ))}
            </div>
          )}

          {frequencyConflicts.length > 0 && (
            <div className={styles.issuesList}>
              {frequencyConflicts.map((conflict, index) => (
                <div key={index} style={{ padding: tokens.spacingVerticalS }}>
                  <Text size={300}>{conflict}</Text>
                </div>
              ))}
            </div>
          )}
        </div>
      )}
    </div>
  );
};

export default AudioMixer;
