import {
  makeStyles,
  tokens,
  Title2,
  Title3,
  Text,
  Button,
  Input,
  Card,
  Field,
  Dropdown,
  Option,
  Divider,
} from '@fluentui/react-components';
import { Folder24Regular } from '@fluentui/react-icons';
import type { VideoDefaultsSettings, FileLocationsSettings } from '../../types/settings';

const useStyles = makeStyles({
  section: {
    padding: tokens.spacingVerticalXL,
  },
  form: {
    display: 'flex',
    flexDirection: 'column',
    gap: tokens.spacingVerticalL,
  },
  subsection: {
    marginTop: tokens.spacingVerticalL,
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

interface GenerationSettingsTabProps {
  videoDefaults: VideoDefaultsSettings;
  fileLocations: FileLocationsSettings;
  onVideoDefaultsChange: (settings: VideoDefaultsSettings) => void;
  onFileLocationsChange: (settings: FileLocationsSettings) => void;
}

export function GenerationSettingsTab({
  videoDefaults,
  fileLocations,
  onVideoDefaultsChange,
  onFileLocationsChange,
}: GenerationSettingsTabProps) {
  const styles = useStyles();

  const updateVideoDefault = <K extends keyof VideoDefaultsSettings>(
    key: K,
    value: VideoDefaultsSettings[K]
  ) => {
    onVideoDefaultsChange({ ...videoDefaults, [key]: value });
  };

  const updateFileLocation = <K extends keyof FileLocationsSettings>(
    key: K,
    value: FileLocationsSettings[K]
  ) => {
    onFileLocationsChange({ ...fileLocations, [key]: value });
  };

  const handleBrowseFolder = async (fieldName: keyof FileLocationsSettings) => {
    try {
      const response = await fetch('/api/settings/browse-folder', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ currentPath: fileLocations[fieldName] }),
      });
      if (response.ok) {
        const data = await response.json();
        if (data.path) {
          updateFileLocation(fieldName, data.path);
        }
      }
    } catch (error) {
      console.error('Error browsing folder:', error);
    }
  };

  return (
    <Card className={styles.section}>
      <Title2>Generation Settings</Title2>
      <Text size={200} style={{ marginBottom: tokens.spacingVerticalL }}>
        Configure default video quality, output formats, and file locations
      </Text>

      <div className={styles.form}>
        <div className={styles.subsection}>
          <Title3>Default Quality</Title3>
          <div className={styles.infoBox}>
            <Text size={200}>
              These settings will be used as defaults for new video projects. You can override them
              per-project.
            </Text>
          </div>

          <Field
            label="Default Resolution"
            hint="Common resolutions: 1920x1080 (1080p), 1280x720 (720p), 3840x2160 (4K)"
          >
            <Dropdown
              value={videoDefaults.defaultResolution}
              onOptionSelect={(_, data) => {
                if (data.optionValue) {
                  updateVideoDefault('defaultResolution', data.optionValue);
                }
              }}
            >
              <Option value="1280x720">720p (1280x720)</Option>
              <Option value="1920x1080">1080p (1920x1080)</Option>
              <Option value="2560x1440">1440p (2560x1440)</Option>
              <Option value="3840x2160">4K (3840x2160)</Option>
              <Option value="1080x1920">Portrait 9:16 (1080x1920)</Option>
            </Dropdown>
          </Field>

          <Field label="Default Frame Rate" hint="Higher frame rates = smoother motion">
            <Dropdown
              value={String(videoDefaults.defaultFrameRate)}
              onOptionSelect={(_, data) => {
                if (data.optionValue) {
                  updateVideoDefault('defaultFrameRate', Number(data.optionValue));
                }
              }}
            >
              <Option value="24">24 fps (Cinematic)</Option>
              <Option value="30">30 fps (Standard)</Option>
              <Option value="60">60 fps (Smooth)</Option>
            </Dropdown>
          </Field>

          <Field label="Default Bitrate" hint="Higher bitrate = better quality, larger file size">
            <Dropdown
              value={videoDefaults.defaultBitrate}
              onOptionSelect={(_, data) => {
                if (data.optionValue) {
                  updateVideoDefault('defaultBitrate', data.optionValue);
                }
              }}
            >
              <Option value="2M">2 Mbps (Low)</Option>
              <Option value="5M">5 Mbps (Medium)</Option>
              <Option value="8M">8 Mbps (High)</Option>
              <Option value="12M">12 Mbps (Very High)</Option>
              <Option value="20M">20 Mbps (Ultra)</Option>
            </Dropdown>
          </Field>
        </div>

        <Divider />

        <div className={styles.subsection}>
          <Title3>Output Formats and Codecs</Title3>
          <Field label="Video Codec" hint="H.264 is most compatible, H.265 has better compression">
            <Dropdown
              value={videoDefaults.defaultCodec}
              onOptionSelect={(_, data) => {
                if (data.optionValue) {
                  updateVideoDefault('defaultCodec', data.optionValue);
                }
              }}
            >
              <Option value="libx264">H.264 (Most compatible)</Option>
              <Option value="libx265">H.265 (Better compression)</Option>
              <Option value="vp9">VP9 (Web optimized)</Option>
            </Dropdown>
          </Field>

          <Field label="Audio Codec">
            <Dropdown
              value={videoDefaults.defaultAudioCodec}
              onOptionSelect={(_, data) => {
                if (data.optionValue) {
                  updateVideoDefault('defaultAudioCodec', data.optionValue);
                }
              }}
            >
              <Option value="aac">AAC (Most compatible)</Option>
              <Option value="libmp3lame">MP3</Option>
              <Option value="libopus">Opus (Web optimized)</Option>
            </Dropdown>
          </Field>

          <Field label="Audio Bitrate">
            <Dropdown
              value={videoDefaults.defaultAudioBitrate}
              onOptionSelect={(_, data) => {
                if (data.optionValue) {
                  updateVideoDefault('defaultAudioBitrate', data.optionValue);
                }
              }}
            >
              <Option value="128k">128 kbps (Standard)</Option>
              <Option value="192k">192 kbps (High)</Option>
              <Option value="320k">320 kbps (Maximum)</Option>
            </Dropdown>
          </Field>

          <Field label="Audio Sample Rate">
            <Dropdown
              value={String(videoDefaults.defaultAudioSampleRate)}
              onOptionSelect={(_, data) => {
                if (data.optionValue) {
                  updateVideoDefault('defaultAudioSampleRate', Number(data.optionValue));
                }
              }}
            >
              <Option value="44100">44.1 kHz (CD Quality)</Option>
              <Option value="48000">48 kHz (Professional)</Option>
            </Dropdown>
          </Field>
        </div>

        <Divider />

        <div className={styles.subsection}>
          <Title3>File Locations</Title3>
          <Field
            label="Output Directory"
            hint="Where generated videos are saved (leave empty for default Documents folder)"
          >
            <div className={styles.inputWithButton}>
              <Input
                style={{ flex: 1 }}
                value={fileLocations.outputDirectory}
                onChange={(e) => updateFileLocation('outputDirectory', e.target.value)}
                placeholder="C:\Users\YourName\Videos\AuraOutput"
              />
              <Button
                appearance="secondary"
                icon={<Folder24Regular />}
                onClick={() => handleBrowseFolder('outputDirectory')}
              >
                Browse
              </Button>
            </div>
          </Field>

          <Field
            label="Temporary Directory"
            hint="Where temporary files are stored during generation (leave empty for system default)"
          >
            <div className={styles.inputWithButton}>
              <Input
                style={{ flex: 1 }}
                value={fileLocations.tempDirectory}
                onChange={(e) => updateFileLocation('tempDirectory', e.target.value)}
                placeholder="C:\Users\YourName\AppData\Local\Temp"
              />
              <Button
                appearance="secondary"
                icon={<Folder24Regular />}
                onClick={() => handleBrowseFolder('tempDirectory')}
              >
                Browse
              </Button>
            </div>
          </Field>

          <Field
            label="Projects Directory"
            hint="Where project files are saved (leave empty for default Documents folder)"
          >
            <div className={styles.inputWithButton}>
              <Input
                style={{ flex: 1 }}
                value={fileLocations.projectsDirectory}
                onChange={(e) => updateFileLocation('projectsDirectory', e.target.value)}
                placeholder="C:\Users\YourName\Documents\AuraProjects"
              />
              <Button
                appearance="secondary"
                icon={<Folder24Regular />}
                onClick={() => handleBrowseFolder('projectsDirectory')}
              >
                Browse
              </Button>
            </div>
          </Field>
        </div>

        <Divider />

        <div className={styles.subsection}>
          <Title3>File Naming Patterns</Title3>
          <div className={styles.infoBox}>
            <Text size={200}>
              <strong>Available tokens:</strong>
              <br />
              %title% - Project title
              <br />
              %date% - Current date (YYYY-MM-DD)
              <br />
              %time% - Current time (HH-MM-SS)
              <br />
              %resolution% - Video resolution
              <br />
              %counter% - Auto-incrementing number
            </Text>
          </div>
          <Field label="File Naming Pattern" hint="Pattern for output filenames">
            <Input
              value="%title%_%date%_%counter%"
              placeholder="%title%_%date%_%counter%"
              disabled
            />
            <Text
              size={200}
              style={{ color: tokens.colorNeutralForeground3, marginTop: tokens.spacingVerticalXS }}
            >
              Example: MyVideo_2024-01-15_001.mp4
            </Text>
          </Field>
        </div>
      </div>
    </Card>
  );
}
