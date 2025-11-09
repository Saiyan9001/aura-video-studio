import {
  makeStyles,
  tokens,
  Title3,
  Text,
  Button,
  Switch,
  Card,
  Field,
  Textarea,
} from '@fluentui/react-components';
import { ArrowDownloadRegular } from '@fluentui/react-icons';
import type { FC } from 'react';
import { useState } from 'react';
import type { SubtitleCue, Subtitle } from '../../services/subtitleService';
import { subtitleService } from '../../services/subtitleService';

interface SubtitleEditorProps {
  scenes: Subtitle[];
  onToggleSubtitles?: (enabled: boolean) => void;
}

const useStyles = makeStyles({
  container: {
    display: 'flex',
    flexDirection: 'column',
    gap: tokens.spacingVerticalL,
  },
  header: {
    display: 'flex',
    justifyContent: 'space-between',
    alignItems: 'center',
  },
  controls: {
    display: 'flex',
    gap: tokens.spacingHorizontalM,
    alignItems: 'center',
  },
  sceneList: {
    display: 'flex',
    flexDirection: 'column',
    gap: tokens.spacingVerticalM,
    maxHeight: '400px',
    overflowY: 'auto',
  },
  sceneCard: {
    padding: tokens.spacingVerticalM,
  },
  sceneHeader: {
    display: 'flex',
    justifyContent: 'space-between',
    alignItems: 'center',
    marginBottom: tokens.spacingVerticalS,
  },
  timeInfo: {
    fontSize: tokens.fontSizeBase200,
    color: tokens.colorNeutralForeground3,
  },
});

export const SubtitleEditor: FC<SubtitleEditorProps> = ({ scenes, onToggleSubtitles }) => {
  const styles = useStyles();
  const [subtitlesEnabled, setSubtitlesEnabled] = useState(true);
  const [editedScenes, setEditedScenes] = useState<Subtitle[]>(scenes);

  const handleToggleSubtitles = (checked: boolean) => {
    setSubtitlesEnabled(checked);
    if (onToggleSubtitles) {
      onToggleSubtitles(checked);
    }
  };

  const handleSceneTextChange = (index: number, newText: string) => {
    const updated = [...editedScenes];
    updated[index] = { ...updated[index], text: newText };
    setEditedScenes(updated);
  };

  const handleExportSRT = () => {
    const cues: SubtitleCue[] = subtitleService.generateSubtitles(editedScenes);
    const srtContent = subtitleService.exportToSRT(cues);
    subtitleService.downloadSubtitles(srtContent, 'subtitles.srt');
  };

  const handleExportVTT = () => {
    const cues: SubtitleCue[] = subtitleService.generateSubtitles(editedScenes);
    const vttContent = subtitleService.exportToVTT(cues);
    subtitleService.downloadSubtitles(vttContent, 'subtitles.vtt');
  };

  const formatTime = (seconds: number): string => {
    const mins = Math.floor(seconds / 60);
    const secs = Math.floor(seconds % 60);
    return `${mins}:${secs.toString().padStart(2, '0')}`;
  };

  return (
    <div className={styles.container}>
      <div className={styles.header}>
        <Title3>Subtitles</Title3>
        <div className={styles.controls}>
          <Switch
            label="Show Subtitles"
            checked={subtitlesEnabled}
            onChange={(_ev, data) => handleToggleSubtitles(data.checked)}
          />
          <Button appearance="secondary" icon={<ArrowDownloadRegular />} onClick={handleExportSRT}>
            Export SRT
          </Button>
          <Button appearance="secondary" icon={<ArrowDownloadRegular />} onClick={handleExportVTT}>
            Export VTT
          </Button>
        </div>
      </div>

      <Text>Edit subtitle text and timing for each scene:</Text>

      <div className={styles.sceneList}>
        {editedScenes.map((scene, index) => (
          <Card key={scene.sceneIndex} className={styles.sceneCard}>
            <div className={styles.sceneHeader}>
              <Text weight="semibold">Scene {scene.sceneIndex + 1}</Text>
              <Text className={styles.timeInfo}>
                {formatTime(scene.startTime)} - {formatTime(scene.startTime + scene.duration)} (
                {scene.duration.toFixed(1)}s)
              </Text>
            </div>
            <Field>
              <Textarea
                value={scene.text}
                onChange={(_ev, data) => handleSceneTextChange(index, data.value)}
                rows={3}
                resize="vertical"
              />
            </Field>
          </Card>
        ))}
      </div>
    </div>
  );
};
