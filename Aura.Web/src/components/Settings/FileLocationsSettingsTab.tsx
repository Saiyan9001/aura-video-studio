import { makeStyles, tokens, Title2, Text, Button, Card } from '@fluentui/react-components';
import type { FileLocationsSettings } from '../../types/settings';
import { PathSelector } from '../common/PathSelector';

const useStyles = makeStyles({
  section: {
    padding: tokens.spacingVerticalXL,
  },
  form: {
    display: 'flex',
    flexDirection: 'column',
    gap: tokens.spacingVerticalL,
  },
  infoBox: {
    padding: tokens.spacingVerticalM,
    backgroundColor: tokens.colorNeutralBackground3,
    borderRadius: tokens.borderRadiusMedium,
    marginBottom: tokens.spacingVerticalL,
  },
  inputWithButton: {
    display: 'flex',
    gap: tokens.spacingHorizontalS,
    alignItems: 'flex-start',
  },
});

interface FileLocationsSettingsTabProps {
  settings: FileLocationsSettings;
  onChange: (settings: FileLocationsSettings) => void;
  onSave: () => void;
  onValidatePath: (path: string) => Promise<{ valid: boolean; message: string }>;
  hasChanges: boolean;
}

export function FileLocationsSettingsTab({
  settings,
  onChange,
  onSave,
  onValidatePath,
  hasChanges,
}: FileLocationsSettingsTabProps) {
  const styles = useStyles();

  const updateSetting = <K extends keyof FileLocationsSettings>(
    key: K,
    value: FileLocationsSettings[K]
  ) => {
    onChange({ ...settings, [key]: value });
  };

  const createValidator = (pathType: string) => {
    return async (path: string) => {
      if (!path) {
        return { isValid: true, message: `Using default ${pathType}` };
      }
      const result = await onValidatePath(path);
      return { isValid: result.valid, message: result.message };
    };
  };

  return (
    <Card className={styles.section}>
      <Title2>File Locations</Title2>
      <Text size={200} style={{ marginBottom: tokens.spacingVerticalL }}>
        Configure paths for tools and directories used by the application
      </Text>

      <div className={styles.infoBox}>
        <Text weight="semibold" size={300}>
          💡 Tip
        </Text>
        <Text size={200} style={{ marginTop: tokens.spacingVerticalXS }}>
          Leave paths empty to use system defaults or portable installation paths. Visit the
          Downloads page to automatically install and configure tools like FFmpeg.
        </Text>
      </div>

      <div className={styles.form}>
        <PathSelector
          label="FFmpeg Path"
          placeholder="C:\path\to\ffmpeg.exe or leave empty for system PATH"
          value={settings.ffmpegPath}
          onChange={(value) => updateSetting('ffmpegPath', value)}
          onValidate={createValidator('FFmpeg executable')}
          type="file"
          accept=".exe"
          helpText="Select the ffmpeg.exe executable. Leave empty to use system PATH or portable installation."
          showClearButton={true}
          showOpenButton={true}
        />

        <PathSelector
          label="FFprobe Path"
          placeholder="C:\path\to\ffprobe.exe or leave empty"
          value={settings.ffprobePath}
          onChange={(value) => updateSetting('ffprobePath', value)}
          onValidate={createValidator('FFprobe executable')}
          type="file"
          accept=".exe"
          helpText="Select the ffprobe.exe executable (usually in same folder as FFmpeg). Leave empty for auto-detection."
          showClearButton={true}
          showOpenButton={true}
        />

        <PathSelector
          label="Output Directory"
          placeholder="C:\Users\YourName\Videos\AuraOutput"
          value={settings.outputDirectory}
          onChange={(value) => updateSetting('outputDirectory', value)}
          onValidate={createValidator('output directory')}
          type="directory"
          helpText="Select the default directory for rendered videos. Leave empty for Documents\AuraVideoStudio."
          showClearButton={true}
          showOpenButton={true}
        />

        <PathSelector
          label="Temporary Directory"
          placeholder="Leave empty to use system temp folder"
          value={settings.tempDirectory}
          onChange={(value) => updateSetting('tempDirectory', value)}
          onValidate={createValidator('temporary directory')}
          type="directory"
          helpText="Select the directory for temporary files during video generation. Leave empty for system temp."
          showClearButton={true}
          showOpenButton={true}
        />

        <PathSelector
          label="Media Library Location"
          placeholder="Leave empty to use default location"
          value={settings.mediaLibraryLocation}
          onChange={(value) => updateSetting('mediaLibraryLocation', value)}
          onValidate={createValidator('media library')}
          type="directory"
          helpText="Select the directory where media assets are stored. Leave empty for default location."
          showClearButton={true}
          showOpenButton={true}
        />

        <PathSelector
          label="Projects Directory"
          placeholder="Leave empty to use default location"
          value={settings.projectsDirectory}
          onChange={(value) => updateSetting('projectsDirectory', value)}
          onValidate={createValidator('projects directory')}
          type="directory"
          helpText="Select the directory where project files are saved. Leave empty for default location."
          showClearButton={true}
          showOpenButton={true}
        />

        {hasChanges && (
          <div className={styles.infoBox}>
            <Text weight="semibold" style={{ color: tokens.colorPaletteYellowForeground1 }}>
              ⚠️ You have unsaved changes
            </Text>
          </div>
        )}

        <div>
          <Button appearance="primary" onClick={onSave} disabled={!hasChanges}>
            Save File Locations
          </Button>
        </div>
      </div>
    </Card>
  );
}
