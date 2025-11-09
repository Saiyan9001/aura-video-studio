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
  Textarea,
  Dialog,
  DialogSurface,
  DialogTitle,
  DialogContent,
  DialogBody,
  DialogActions,
} from '@fluentui/react-components';
import {
  Save24Regular,
  Share24Regular,
  ArrowDownload24Regular,
  ArrowUpload24Regular,
  Dismiss24Regular,
  Checkmark24Regular,
} from '@fluentui/react-icons';
import { useState, useEffect } from 'react';
import type { UserSettings } from '../../types/settings';

const useStyles = makeStyles({
  container: {
    display: 'flex',
    flexDirection: 'column',
    gap: tokens.spacingVerticalL,
  },
  presetsGrid: {
    display: 'grid',
    gridTemplateColumns: 'repeat(auto-fill, minmax(280px, 1fr))',
    gap: tokens.spacingVerticalM,
  },
  presetCard: {
    padding: tokens.spacingVerticalL,
    cursor: 'pointer',
    transition: 'all 0.2s',
    ':hover': {
      transform: 'translateY(-2px)',
      boxShadow: tokens.shadow8,
    },
  },
  presetCardActive: {
    backgroundColor: tokens.colorBrandBackground2,
  },
  presetHeader: {
    display: 'flex',
    justifyContent: 'space-between',
    alignItems: 'flex-start',
    marginBottom: tokens.spacingVerticalS,
  },
  presetActions: {
    display: 'flex',
    gap: tokens.spacingHorizontalXS,
  },
  customPresetsList: {
    display: 'flex',
    flexDirection: 'column',
    gap: tokens.spacingVerticalS,
  },
  customPresetItem: {
    padding: tokens.spacingVerticalM,
    backgroundColor: tokens.colorNeutralBackground2,
    borderRadius: tokens.borderRadiusMedium,
    display: 'flex',
    justifyContent: 'space-between',
    alignItems: 'center',
  },
});

export interface SettingsPreset {
  id: string;
  name: string;
  description: string;
  settings: Partial<UserSettings>;
  isBuiltIn: boolean;
  createdAt?: string;
}

const BUILT_IN_PRESETS: SettingsPreset[] = [
  {
    id: 'youtube-optimized',
    name: 'YouTube Optimized',
    description: '1080p, H.264, 8Mbps - Perfect for YouTube uploads',
    isBuiltIn: true,
    settings: {
      videoDefaults: {
        defaultResolution: '1920x1080',
        defaultFrameRate: 30,
        defaultCodec: 'libx264',
        defaultBitrate: '8M',
        defaultAudioCodec: 'aac',
        defaultAudioBitrate: '192k',
        defaultAudioSampleRate: 48000,
      },
    },
  },
  {
    id: 'tiktok-shorts',
    name: 'TikTok/Shorts',
    description: '9:16 Portrait, 1080x1920, 60fps - Mobile-first vertical video',
    isBuiltIn: true,
    settings: {
      videoDefaults: {
        defaultResolution: '1080x1920',
        defaultFrameRate: 60,
        defaultCodec: 'libx264',
        defaultBitrate: '6M',
        defaultAudioCodec: 'aac',
        defaultAudioBitrate: '192k',
        defaultAudioSampleRate: 48000,
      },
    },
  },
  {
    id: 'professional-4k',
    name: 'Professional 4K',
    description: '4K UHD, H.265, High bitrate - Maximum quality for professional use',
    isBuiltIn: true,
    settings: {
      videoDefaults: {
        defaultResolution: '3840x2160',
        defaultFrameRate: 30,
        defaultCodec: 'libx265',
        defaultBitrate: '20M',
        defaultAudioCodec: 'aac',
        defaultAudioBitrate: '320k',
        defaultAudioSampleRate: 48000,
      },
    },
  },
  {
    id: 'fast-draft',
    name: 'Fast Draft',
    description: '720p, Quick encode - Rapid preview and iteration',
    isBuiltIn: true,
    settings: {
      videoDefaults: {
        defaultResolution: '1280x720',
        defaultFrameRate: 30,
        defaultCodec: 'libx264',
        defaultBitrate: '3M',
        defaultAudioCodec: 'aac',
        defaultAudioBitrate: '128k',
        defaultAudioSampleRate: 44100,
      },
    },
  },
];

interface SettingsPresetsProps {
  currentSettings: UserSettings;
  onApplyPreset: (preset: SettingsPreset) => void;
  onSavePreset: (preset: SettingsPreset) => void;
  customPresets: SettingsPreset[];
  onDeletePreset: (presetId: string) => void;
}

export function SettingsPresets({
  currentSettings,
  onApplyPreset,
  onSavePreset,
  customPresets,
  onDeletePreset,
}: SettingsPresetsProps) {
  const styles = useStyles();
  const [showSaveDialog, setShowSaveDialog] = useState(false);
  const [newPresetName, setNewPresetName] = useState('');
  const [newPresetDescription, setNewPresetDescription] = useState('');
  const [exportUrl, setExportUrl] = useState('');
  const [showShareDialog, setShowShareDialog] = useState(false);

  const handleApplyPreset = (preset: SettingsPreset) => {
    if (window.confirm(`Apply "${preset.name}" preset? This will update your current settings.`)) {
      onApplyPreset(preset);
    }
  };

  const handleSaveCustomPreset = () => {
    if (!newPresetName.trim()) return;

    const newPreset: SettingsPreset = {
      id: `custom-${Date.now()}`,
      name: newPresetName,
      description: newPresetDescription,
      settings: currentSettings,
      isBuiltIn: false,
      createdAt: new Date().toISOString(),
    };

    onSavePreset(newPreset);
    setNewPresetName('');
    setNewPresetDescription('');
    setShowSaveDialog(false);
  };

  const handleExportPreset = (preset: SettingsPreset) => {
    const json = JSON.stringify(preset);
    const base64 = btoa(json);
    const url = `${window.location.origin}${window.location.pathname}?preset=${base64}`;
    setExportUrl(url);
    setShowShareDialog(true);
  };

  const handleImportFromUrl = () => {
    const urlParams = new URLSearchParams(window.location.search);
    const presetParam = urlParams.get('preset');
    if (presetParam) {
      try {
        const json = atob(presetParam);
        const preset: SettingsPreset = JSON.parse(json);
        if (window.confirm(`Import preset "${preset.name}"? This will update your settings.`)) {
          onApplyPreset(preset);
        }
      } catch (error) {
        alert('Failed to import preset from URL. The link may be invalid.');
        console.error('Preset import error:', error);
      }
    }
  };

  // Trigger import from URL on component mount
  useEffect(() => {
    handleImportFromUrl();
  }, []);

  const handleExportToFile = (preset: SettingsPreset) => {
    const json = JSON.stringify(preset, null, 2);
    const blob = new Blob([json], { type: 'application/json' });
    const url = URL.createObjectURL(blob);
    const a = document.createElement('a');
    a.href = url;
    a.download = `aura-preset-${preset.id}.json`;
    document.body.appendChild(a);
    a.click();
    document.body.removeChild(a);
    URL.revokeObjectURL(url);
  };

  const handleImportFromFile = () => {
    const input = document.createElement('input');
    input.type = 'file';
    input.accept = '.json';
    input.onchange = async (e) => {
      const file = (e.target as HTMLInputElement).files?.[0];
      if (file) {
        try {
          const text = await file.text();
          const preset: SettingsPreset = JSON.parse(text);
          onSavePreset(preset);
          alert(`Preset "${preset.name}" imported successfully!`);
        } catch (error) {
          alert('Failed to import preset. Please check the file format.');
          console.error('Import error:', error);
        }
      }
    };
    input.click();
  };

  const copyToClipboard = (text: string) => {
    navigator.clipboard.writeText(text).then(
      () => alert('Copied to clipboard!'),
      () => alert('Failed to copy to clipboard')
    );
  };

  return (
    <div className={styles.container}>
      <Card>
        <div style={{ padding: tokens.spacingVerticalL }}>
          <Title2>Built-in Presets</Title2>
          <Text size={200} style={{ marginBottom: tokens.spacingVerticalL }}>
            Quick-start presets optimized for common use cases
          </Text>
          <div className={styles.presetsGrid}>
            {BUILT_IN_PRESETS.map((preset) => (
              <Card
                key={preset.id}
                className={styles.presetCard}
                onClick={() => handleApplyPreset(preset)}
              >
                <div className={styles.presetHeader}>
                  <Title3>{preset.name}</Title3>
                  <div className={styles.presetActions}>
                    <Button
                      appearance="subtle"
                      size="small"
                      icon={<Share24Regular />}
                      onClick={(e) => {
                        e.stopPropagation();
                        handleExportPreset(preset);
                      }}
                    />
                  </div>
                </div>
                <Text size={200}>{preset.description}</Text>
              </Card>
            ))}
          </div>
        </div>
      </Card>

      <Card>
        <div style={{ padding: tokens.spacingVerticalL }}>
          <div
            style={{
              display: 'flex',
              justifyContent: 'space-between',
              alignItems: 'center',
              marginBottom: tokens.spacingVerticalL,
            }}
          >
            <div>
              <Title2>Custom Presets</Title2>
              <Text size={200}>Save and manage your own preset configurations</Text>
            </div>
            <div style={{ display: 'flex', gap: tokens.spacingHorizontalS }}>
              <Button
                appearance="primary"
                icon={<Save24Regular />}
                onClick={() => setShowSaveDialog(true)}
              >
                Save Current
              </Button>
              <Button
                appearance="secondary"
                icon={<ArrowUpload24Regular />}
                onClick={handleImportFromFile}
              >
                Import
              </Button>
            </div>
          </div>

          {customPresets.length === 0 ? (
            <Text size={200} style={{ color: tokens.colorNeutralForeground3 }}>
              No custom presets yet. Save your current settings to create one.
            </Text>
          ) : (
            <div className={styles.customPresetsList}>
              {customPresets.map((preset) => (
                <div key={preset.id} className={styles.customPresetItem}>
                  <div>
                    <Text weight="semibold">{preset.name}</Text>
                    <br />
                    <Text size={200} style={{ color: tokens.colorNeutralForeground3 }}>
                      {preset.description}
                    </Text>
                  </div>
                  <div style={{ display: 'flex', gap: tokens.spacingHorizontalXS }}>
                    <Button
                      size="small"
                      appearance="subtle"
                      icon={<Checkmark24Regular />}
                      onClick={() => handleApplyPreset(preset)}
                    >
                      Apply
                    </Button>
                    <Button
                      size="small"
                      appearance="subtle"
                      icon={<ArrowDownload24Regular />}
                      onClick={() => handleExportToFile(preset)}
                    >
                      Export
                    </Button>
                    <Button
                      size="small"
                      appearance="subtle"
                      icon={<Share24Regular />}
                      onClick={() => handleExportPreset(preset)}
                    >
                      Share
                    </Button>
                    <Button
                      size="small"
                      appearance="subtle"
                      icon={<Dismiss24Regular />}
                      onClick={() => {
                        if (window.confirm(`Delete preset "${preset.name}"?`)) {
                          onDeletePreset(preset.id);
                        }
                      }}
                    >
                      Delete
                    </Button>
                  </div>
                </div>
              ))}
            </div>
          )}
        </div>
      </Card>

      <Dialog open={showSaveDialog} onOpenChange={(_, data) => setShowSaveDialog(data.open)}>
        <DialogSurface>
          <DialogBody>
            <DialogTitle>Save Current Settings as Preset</DialogTitle>
            <DialogContent>
              <Field label="Preset Name" required>
                <Input
                  value={newPresetName}
                  onChange={(e) => setNewPresetName(e.target.value)}
                  placeholder="My Custom Preset"
                />
              </Field>
              <Field label="Description">
                <Textarea
                  value={newPresetDescription}
                  onChange={(e) => setNewPresetDescription(e.target.value)}
                  placeholder="Describe this preset configuration..."
                  rows={3}
                />
              </Field>
            </DialogContent>
            <DialogActions>
              <Button appearance="secondary" onClick={() => setShowSaveDialog(false)}>
                Cancel
              </Button>
              <Button
                appearance="primary"
                onClick={handleSaveCustomPreset}
                disabled={!newPresetName.trim()}
              >
                Save Preset
              </Button>
            </DialogActions>
          </DialogBody>
        </DialogSurface>
      </Dialog>

      <Dialog open={showShareDialog} onOpenChange={(_, data) => setShowShareDialog(data.open)}>
        <DialogSurface>
          <DialogBody>
            <DialogTitle>Share Preset</DialogTitle>
            <DialogContent>
              <Text size={200} style={{ marginBottom: tokens.spacingVerticalS }}>
                Copy this URL to share your preset with others:
              </Text>
              <Field>
                <Input value={exportUrl} readOnly />
              </Field>
            </DialogContent>
            <DialogActions>
              <Button
                appearance="primary"
                onClick={() => {
                  copyToClipboard(exportUrl);
                  setShowShareDialog(false);
                }}
              >
                Copy to Clipboard
              </Button>
              <Button appearance="secondary" onClick={() => setShowShareDialog(false)}>
                Close
              </Button>
            </DialogActions>
          </DialogBody>
        </DialogSurface>
      </Dialog>
    </div>
  );
}
