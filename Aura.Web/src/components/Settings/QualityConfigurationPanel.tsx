/**
 * Quality Configuration Panel
 * Comprehensive UI for configuring video, audio, and subtitle quality settings
 */

import {
  makeStyles,
  tokens,
  Title2,
  Title3,
  Text,
  Button,
  Input,
  Dropdown,
  Option,
  Card,
  Field,
  Spinner,
  MessageBar,
  MessageBarBody,
  MessageBarTitle,
} from '@fluentui/react-components';
import {
  Save24Regular,
  Video24Regular,
  Speaker224Regular,
  SubtitlesRegular,
} from '@fluentui/react-icons';
import { useState, useEffect } from 'react';
import apiClient from '../../services/api/apiClient';
import { useProviderConfigStore } from '../../state/providerConfig';
import type { QualityConfigDto } from '../../types/api-v1';

const useStyles = makeStyles({
  container: {
    display: 'flex',
    flexDirection: 'column',
    gap: tokens.spacingVerticalL,
    padding: tokens.spacingVerticalXL,
  },
  header: {
    display: 'flex',
    justifyContent: 'space-between',
    alignItems: 'center',
  },
  sections: {
    display: 'flex',
    flexDirection: 'column',
    gap: tokens.spacingVerticalL,
  },
  sectionCard: {
    padding: tokens.spacingVerticalL,
  },
  sectionHeader: {
    display: 'flex',
    alignItems: 'center',
    gap: tokens.spacingHorizontalS,
    marginBottom: tokens.spacingVerticalM,
  },
  fields: {
    display: 'grid',
    gridTemplateColumns: 'repeat(auto-fit, minmax(250px, 1fr))',
    gap: tokens.spacingVerticalM,
  },
  actions: {
    display: 'flex',
    gap: tokens.spacingHorizontalM,
    justifyContent: 'flex-end',
  },
  colorPreview: {
    width: '40px',
    height: '40px',
    borderRadius: tokens.borderRadiusMedium,
    border: `1px solid ${tokens.colorNeutralStroke1}`,
  },
  fieldWithPreview: {
    display: 'flex',
    gap: tokens.spacingHorizontalS,
    alignItems: 'flex-end',
  },
});

const RESOLUTION_OPTIONS = [
  { value: '480p', label: '480p (854x480)', width: 854, height: 480 },
  { value: '720p', label: '720p HD (1280x720)', width: 1280, height: 720 },
  { value: '1080p', label: '1080p Full HD (1920x1080)', width: 1920, height: 1080 },
  { value: '1440p', label: '1440p QHD (2560x1440)', width: 2560, height: 1440 },
  { value: '4K', label: '4K UHD (3840x2160)', width: 3840, height: 2160 },
];

const FRAMERATE_OPTIONS = [
  { value: 24, label: '24 fps (Cinematic)' },
  { value: 30, label: '30 fps (Standard)' },
  { value: 60, label: '60 fps (Smooth)' },
];

const BITRATE_PRESETS = [
  { value: 'Low', label: 'Low', kbps: 2000 },
  { value: 'Medium', label: 'Medium', kbps: 3500 },
  { value: 'High', label: 'High', kbps: 5000 },
  { value: 'VeryHigh', label: 'Very High', kbps: 8000 },
  { value: 'Custom', label: 'Custom', kbps: 0 },
];

const CODEC_OPTIONS = [
  { value: 'h264', label: 'H.264 (Universal)' },
  { value: 'h265', label: 'H.265/HEVC (Efficient)' },
  { value: 'vp9', label: 'VP9 (Open)' },
  { value: 'av1', label: 'AV1 (Modern)' },
];

const AUDIO_BITRATE_OPTIONS = [128, 192, 256, 320];
const AUDIO_SAMPLE_RATE_OPTIONS = [44100, 48000];

interface QualityConfigurationPanelProps {
  onSave?: () => void;
}

export function QualityConfigurationPanel({ onSave }: QualityConfigurationPanelProps) {
  const styles = useStyles();
  const {
    qualityConfig,
    isLoading,
    isSaving,
    error,
    setQualityConfig,
    setIsLoading,
    setIsSaving,
    setError,
  } = useProviderConfigStore();

  const [localConfig, setLocalConfig] = useState<QualityConfigDto | null>(null);

  useEffect(() => {
    loadQualityConfig();
  }, []);

  useEffect(() => {
    if (qualityConfig) {
      setLocalConfig(qualityConfig);
    }
  }, [qualityConfig]);

  const loadQualityConfig = async () => {
    setIsLoading(true);
    setError(null);
    try {
      const response = await apiClient.get<QualityConfigDto>('/api/providerconfiguration/quality');
      setQualityConfig(response.data);
    } catch (err: unknown) {
      const error = err instanceof Error ? err : new Error(String(err));
      setError('Failed to load quality configuration: ' + error.message);
    } finally {
      setIsLoading(false);
    }
  };

  const saveQualityConfig = async () => {
    if (!localConfig) return;

    setIsSaving(true);
    setError(null);
    try {
      await apiClient.post('/api/providerconfiguration/quality', localConfig);
      setQualityConfig(localConfig);
      onSave?.();
    } catch (err: unknown) {
      const error = err instanceof Error ? err : new Error(String(err));
      setError('Failed to save quality configuration: ' + error.message);
    } finally {
      setIsSaving(false);
    }
  };

  const updateVideoConfig = (updates: Partial<QualityConfigDto['video']>) => {
    if (!localConfig) return;
    setLocalConfig({
      ...localConfig,
      video: { ...localConfig.video, ...updates },
    });
  };

  const updateAudioConfig = (updates: Partial<QualityConfigDto['audio']>) => {
    if (!localConfig) return;
    setLocalConfig({
      ...localConfig,
      audio: { ...localConfig.audio, ...updates },
    });
  };

  const updateSubtitleConfig = (updates: Partial<NonNullable<QualityConfigDto['subtitles']>>) => {
    if (!localConfig) return;
    setLocalConfig({
      ...localConfig,
      subtitles: localConfig.subtitles ? { ...localConfig.subtitles, ...updates } : null,
    });
  };

  const handleResolutionChange = (resolution: string) => {
    const preset = RESOLUTION_OPTIONS.find((r) => r.value === resolution);
    if (preset) {
      updateVideoConfig({
        resolution: preset.value,
        width: preset.width,
        height: preset.height,
      });
    }
  };

  const handleBitratePresetChange = (preset: string) => {
    const presetData = BITRATE_PRESETS.find((p) => p.value === preset);
    if (presetData) {
      updateVideoConfig({
        bitratePreset: preset,
        bitrateKbps: presetData.kbps || localConfig?.video.bitrateKbps || 5000,
      });
    }
  };

  if (isLoading) {
    return (
      <div className={styles.container}>
        <Spinner label="Loading quality configuration..." />
      </div>
    );
  }

  if (!localConfig) {
    return (
      <div className={styles.container}>
        <Text>No configuration loaded</Text>
      </div>
    );
  }

  return (
    <div className={styles.container}>
      <div className={styles.header}>
        <div>
          <Title2>Quality Configuration</Title2>
          <Text>Configure video resolution, audio quality, and subtitle styles</Text>
        </div>
        <div className={styles.actions}>
          <Button
            appearance="secondary"
            onClick={loadQualityConfig}
            disabled={isLoading || isSaving}
          >
            Reload
          </Button>
          <Button
            appearance="primary"
            icon={<Save24Regular />}
            onClick={saveQualityConfig}
            disabled={isSaving}
          >
            {isSaving ? 'Saving...' : 'Save Configuration'}
          </Button>
        </div>
      </div>

      {error && (
        <MessageBar intent="error">
          <MessageBarBody>
            <MessageBarTitle>Error</MessageBarTitle>
            {error}
          </MessageBarBody>
        </MessageBar>
      )}

      <div className={styles.sections}>
        <Card className={styles.sectionCard}>
          <div className={styles.sectionHeader}>
            <Video24Regular />
            <Title3>Video Settings</Title3>
          </div>
          <div className={styles.fields}>
            <Field label="Resolution">
              <Dropdown
                value={localConfig.video.resolution}
                selectedOptions={[localConfig.video.resolution]}
                onOptionSelect={(_, data) => handleResolutionChange(data.optionValue as string)}
              >
                {RESOLUTION_OPTIONS.map((opt) => (
                  <Option key={opt.value} value={opt.value}>
                    {opt.label}
                  </Option>
                ))}
              </Dropdown>
            </Field>

            <Field label="Frame Rate">
              <Dropdown
                value={localConfig.video.framerate.toString()}
                selectedOptions={[localConfig.video.framerate.toString()]}
                onOptionSelect={(_, data) =>
                  updateVideoConfig({ framerate: parseInt(data.optionValue as string) })
                }
              >
                {FRAMERATE_OPTIONS.map((opt) => (
                  <Option key={opt.value} value={opt.value.toString()}>
                    {opt.label}
                  </Option>
                ))}
              </Dropdown>
            </Field>

            <Field label="Codec">
              <Dropdown
                value={localConfig.video.codec}
                selectedOptions={[localConfig.video.codec]}
                onOptionSelect={(_, data) =>
                  updateVideoConfig({ codec: data.optionValue as string })
                }
              >
                {CODEC_OPTIONS.map((opt) => (
                  <Option key={opt.value} value={opt.value}>
                    {opt.label}
                  </Option>
                ))}
              </Dropdown>
            </Field>

            <Field label="Bitrate Preset">
              <Dropdown
                value={localConfig.video.bitratePreset}
                selectedOptions={[localConfig.video.bitratePreset]}
                onOptionSelect={(_, data) => handleBitratePresetChange(data.optionValue as string)}
              >
                {BITRATE_PRESETS.map((opt) => (
                  <Option key={opt.value} value={opt.value}>
                    {opt.label}
                  </Option>
                ))}
              </Dropdown>
            </Field>

            {localConfig.video.bitratePreset === 'Custom' && (
              <Field label="Bitrate (Kbps)">
                <Input
                  type="number"
                  value={localConfig.video.bitrateKbps.toString()}
                  onChange={(_, data) =>
                    updateVideoConfig({ bitrateKbps: parseInt(data.value) || 5000 })
                  }
                />
              </Field>
            )}
          </div>
        </Card>

        <Card className={styles.sectionCard}>
          <div className={styles.sectionHeader}>
            <Speaker224Regular />
            <Title3>Audio Settings</Title3>
          </div>
          <div className={styles.fields}>
            <Field label="Bitrate (Kbps)">
              <Dropdown
                value={localConfig.audio.bitrate.toString()}
                selectedOptions={[localConfig.audio.bitrate.toString()]}
                onOptionSelect={(_, data) =>
                  updateAudioConfig({ bitrate: parseInt(data.optionValue as string) })
                }
              >
                {AUDIO_BITRATE_OPTIONS.map((bitrate) => (
                  <Option key={bitrate} value={bitrate.toString()} text={`${bitrate} Kbps`}>
                    {bitrate} Kbps
                  </Option>
                ))}
              </Dropdown>
            </Field>

            <Field label="Sample Rate (Hz)">
              <Dropdown
                value={localConfig.audio.sampleRate.toString()}
                selectedOptions={[localConfig.audio.sampleRate.toString()]}
                onOptionSelect={(_, data) =>
                  updateAudioConfig({ sampleRate: parseInt(data.optionValue as string) })
                }
              >
                {AUDIO_SAMPLE_RATE_OPTIONS.map((rate) => (
                  <Option key={rate} value={rate.toString()} text={`${rate} Hz`}>
                    {rate} Hz
                  </Option>
                ))}
              </Dropdown>
            </Field>

            <Field label="Channels">
              <Dropdown
                value={localConfig.audio.channels.toString()}
                selectedOptions={[localConfig.audio.channels.toString()]}
                onOptionSelect={(_, data) =>
                  updateAudioConfig({ channels: parseInt(data.optionValue as string) })
                }
              >
                <Option value="1">Mono</Option>
                <Option value="2">Stereo</Option>
              </Dropdown>
            </Field>
          </div>
        </Card>

        {localConfig.subtitles && (
          <Card className={styles.sectionCard}>
            <div className={styles.sectionHeader}>
              <SubtitlesRegular />
              <Title3>Subtitle Style</Title3>
            </div>
            <div className={styles.fields}>
              <Field label="Font Family">
                <Input
                  value={localConfig.subtitles.fontFamily}
                  onChange={(_, data) => updateSubtitleConfig({ fontFamily: data.value })}
                />
              </Field>

              <Field label="Font Size (px)">
                <Input
                  type="number"
                  value={localConfig.subtitles.fontSize.toString()}
                  onChange={(_, data) =>
                    updateSubtitleConfig({ fontSize: parseInt(data.value) || 24 })
                  }
                />
              </Field>

              <Field label="Font Color">
                <Input
                  value={localConfig.subtitles.fontColor}
                  onChange={(_, data) => updateSubtitleConfig({ fontColor: data.value })}
                  contentAfter={
                    <div
                      className={styles.colorPreview}
                      style={{ backgroundColor: localConfig.subtitles.fontColor }}
                    />
                  }
                />
              </Field>

              <Field label="Background Color">
                <Input
                  value={localConfig.subtitles.backgroundColor}
                  onChange={(_, data) => updateSubtitleConfig({ backgroundColor: data.value })}
                  contentAfter={
                    <div
                      className={styles.colorPreview}
                      style={{ backgroundColor: localConfig.subtitles.backgroundColor }}
                    />
                  }
                />
              </Field>

              <Field label="Background Opacity">
                <Input
                  type="number"
                  min="0"
                  max="1"
                  step="0.1"
                  value={localConfig.subtitles.backgroundOpacity.toString()}
                  onChange={(_, data) =>
                    updateSubtitleConfig({
                      backgroundOpacity: parseFloat(data.value) || 0.7,
                    })
                  }
                />
              </Field>

              <Field label="Position">
                <Dropdown
                  value={localConfig.subtitles.position}
                  selectedOptions={[localConfig.subtitles.position]}
                  onOptionSelect={(_, data) =>
                    updateSubtitleConfig({ position: data.optionValue as string })
                  }
                >
                  <Option value="Top">Top</Option>
                  <Option value="Middle">Middle</Option>
                  <Option value="Bottom">Bottom</Option>
                </Dropdown>
              </Field>

              <Field label="Outline Width (px)">
                <Input
                  type="number"
                  value={localConfig.subtitles.outlineWidth.toString()}
                  onChange={(_, data) =>
                    updateSubtitleConfig({ outlineWidth: parseInt(data.value) || 2 })
                  }
                />
              </Field>
            </div>
          </Card>
        )}
      </div>
    </div>
  );
}

export default QualityConfigurationPanel;
