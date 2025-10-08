import { useState, useEffect } from 'react';
import {
  makeStyles,
  tokens,
  Title1,
  Title2,
  Text,
  Button,
  Input,
  Switch,
  Card,
  Field,
  Tab,
  TabList,
  Slider,
} from '@fluentui/react-components';
import { Save24Regular, Checkmark20Regular } from '@fluentui/react-icons';
import type { Profile } from '../types';
import { PreflightModal } from '../components/PreflightModal';

const useStyles = makeStyles({
  container: {
    maxWidth: '900px',
    margin: '0 auto',
  },
  header: {
    marginBottom: tokens.spacingVerticalXXL,
    display: 'flex',
    flexDirection: 'column',
    gap: tokens.spacingVerticalS,
  },
  subtitle: {
    color: tokens.colorNeutralForeground3,
  },
  tabs: {
    marginBottom: tokens.spacingVerticalL,
  },
  section: {
    padding: tokens.spacingVerticalXL,
  },
  form: {
    display: 'flex',
    flexDirection: 'column',
    gap: tokens.spacingVerticalL,
  },
  actions: {
    display: 'flex',
    gap: tokens.spacingHorizontalM,
    justifyContent: 'flex-end',
    marginTop: tokens.spacingVerticalXL,
  },
  profileCard: {
    padding: tokens.spacingVerticalL,
    marginBottom: tokens.spacingVerticalM,
    cursor: 'pointer',
    borderRadius: tokens.borderRadiusMedium,
    border: `1px solid ${tokens.colorNeutralStroke1}`,
    backgroundColor: tokens.colorNeutralBackground2,
  },
});

export function SettingsPage() {
  const styles = useStyles();
  const [activeTab, setActiveTab] = useState('system');
  const [profiles, setProfiles] = useState<Profile[]>([]);
  const [offlineMode, setOfflineMode] = useState(false);
  const [settings, setSettings] = useState<any>({});
  const [preflightModalOpen, setPreflightModalOpen] = useState(false);
  
  // UI Settings state
  const [uiScale, setUiScale] = useState(100);
  const [compactMode, setCompactMode] = useState(false);
  
  // API Keys state
  const [apiKeys, setApiKeys] = useState({
    openai: '',
    elevenlabs: '',
    pexels: '',
    stabilityai: '',
  });
  const [keysModified, setKeysModified] = useState(false);
  const [savingKeys, setSavingKeys] = useState(false);
  
  // Local Provider Paths state
  const [providerPaths, setProviderPaths] = useState({
    stableDiffusionUrl: 'http://127.0.0.1:7860',
    ollamaUrl: 'http://127.0.0.1:11434',
    ffmpegPath: '',
    ffprobePath: '',
    outputDirectory: '',
  });
  const [pathsModified, setPathsModified] = useState(false);
  const [savingPaths, setSavingPaths] = useState(false);
  const [testResults, setTestResults] = useState<Record<string, { success: boolean; message: string } | null>>({});

  useEffect(() => {
    fetchSettings();
    fetchProfiles();
    fetchApiKeys();
    fetchProviderPaths();
  }, []);

  // Apply UI scale on load
  useEffect(() => {
    if (uiScale !== 100) {
      document.documentElement.style.fontSize = `${uiScale}%`;
    }
  }, [uiScale]);

  const fetchSettings = async () => {
    try {
      const response = await fetch('/api/settings/load');
      if (response.ok) {
        const data = await response.json();
        setSettings(data);
        setOfflineMode(data.offlineMode || false);
        setUiScale(data.uiScale || 100);
        setCompactMode(data.compactMode || false);
      }
    } catch (error) {
      console.error('Error fetching settings:', error);
    }
  };

  const fetchProfiles = async () => {
    try {
      const response = await fetch('/api/profiles/list');
      if (response.ok) {
        const data = await response.json();
        setProfiles(data.profiles || []);
      }
    } catch (error) {
      console.error('Error fetching profiles:', error);
    }
  };

  const fetchApiKeys = async () => {
    try {
      const response = await fetch('/api/apikeys/load');
      if (response.ok) {
        const data = await response.json();
        setApiKeys({
          openai: data.openai || '',
          elevenlabs: data.elevenlabs || '',
          pexels: data.pexels || '',
          stabilityai: data.stabilityai || '',
        });
      }
    } catch (error) {
      console.error('Error fetching API keys:', error);
    }
  };

  const saveSettings = async () => {
    try {
      const response = await fetch('/api/settings/save', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ 
          ...settings, 
          offlineMode,
          uiScale,
          compactMode
        }),
      });
      if (response.ok) {
        alert('Settings saved successfully');
        // Apply UI scale by updating document root font size
        document.documentElement.style.fontSize = `${uiScale}%`;
      }
    } catch (error) {
      console.error('Error saving settings:', error);
      alert('Error saving settings');
    }
  };

  const saveApiKeys = async () => {
    setSavingKeys(true);
    try {
      const response = await fetch('/api/apikeys/save', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({
          openAiKey: apiKeys.openai,
          elevenLabsKey: apiKeys.elevenlabs,
          pexelsKey: apiKeys.pexels,
          stabilityAiKey: apiKeys.stabilityai,
        }),
      });
      if (response.ok) {
        alert('API keys saved successfully');
        setKeysModified(false);
      } else {
        alert('Error saving API keys');
      }
    } catch (error) {
      console.error('Error saving API keys:', error);
      alert('Error saving API keys');
    } finally {
      setSavingKeys(false);
    }
  };

  const applyProfile = async (profileName: string) => {
    try {
      const response = await fetch('/api/profiles/apply', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ profileName }),
      });
      if (response.ok) {
        alert(`Profile "${profileName}" applied successfully`);
      }
    } catch (error) {
      console.error('Error applying profile:', error);
    }
  };

  const updateApiKey = (key: keyof typeof apiKeys, value: string) => {
    setApiKeys(prev => ({ ...prev, [key]: value }));
    setKeysModified(true);
  };

  const fetchProviderPaths = async () => {
    try {
      const response = await fetch('/api/providers/paths/load');
      if (response.ok) {
        const data = await response.json();
        setProviderPaths({
          stableDiffusionUrl: data.stableDiffusionUrl || 'http://127.0.0.1:7860',
          ollamaUrl: data.ollamaUrl || 'http://127.0.0.1:11434',
          ffmpegPath: data.ffmpegPath || '',
          ffprobePath: data.ffprobePath || '',
          outputDirectory: data.outputDirectory || '',
        });
      }
    } catch (error) {
      console.error('Error fetching provider paths:', error);
    }
  };

  const saveProviderPaths = async () => {
    setSavingPaths(true);
    try {
      const response = await fetch('/api/providers/paths/save', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify(providerPaths),
      });
      if (response.ok) {
        alert('Provider paths saved successfully');
        setPathsModified(false);
      } else {
        alert('Error saving provider paths');
      }
    } catch (error) {
      console.error('Error saving provider paths:', error);
      alert('Error saving provider paths');
    } finally {
      setSavingPaths(false);
    }
  };

  const updateProviderPath = (key: keyof typeof providerPaths, value: string) => {
    setProviderPaths(prev => ({ ...prev, [key]: value }));
    setPathsModified(true);
    // Clear test result when path changes
    setTestResults(prev => ({ ...prev, [key]: null }));
  };

  const testProvider = async (provider: string, url?: string, path?: string) => {
    try {
      const response = await fetch(`/api/providers/test/${provider}`, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ url, path }),
      });
      if (response.ok) {
        const data = await response.json();
        setTestResults(prev => ({ ...prev, [provider]: data }));
      } else {
        setTestResults(prev => ({ 
          ...prev, 
          [provider]: { success: false, message: 'Failed to test connection' } 
        }));
      }
    } catch (error) {
      console.error(`Error testing ${provider}:`, error);
      setTestResults(prev => ({ 
        ...prev, 
        [provider]: { success: false, message: `Network error: ${error}` } 
      }));
    }
  };

  return (
    <div className={styles.container}>
      <div className={styles.header}>
        <Title1>Settings</Title1>
        <Text className={styles.subtitle}>
          Configure system preferences, providers, and API keys
        </Text>
      </div>

      <TabList
        className={styles.tabs}
        selectedValue={activeTab}
        onTabSelect={(_, data) => setActiveTab(data.value as string)}
      >
        <Tab value="system">System</Tab>
        <Tab value="ui">UI</Tab>
        <Tab value="providers">Providers</Tab>
        <Tab value="localproviders">Local Providers</Tab>
        <Tab value="apikeys">API Keys</Tab>
        <Tab value="privacy">Privacy</Tab>
      </TabList>

      {activeTab === 'system' && (
        <Card className={styles.section}>
          <Title2>System Profile</Title2>
          <div className={styles.form}>
            <Field label="Offline Mode">
              <Switch
                checked={offlineMode}
                onChange={(_, data) => setOfflineMode(data.checked)}
              />
              <Text size={200}>
                {offlineMode ? 'Enabled' : 'Disabled'} - Blocks all network providers. Only local and stock assets are used.
              </Text>
            </Field>

            <Button
              onClick={async () => {
                try {
                  const response = await fetch('/api/probes/run', { method: 'POST' });
                  if (response.ok) {
                    alert('Hardware probes completed successfully');
                  }
                } catch (error) {
                  console.error('Error running probes:', error);
                }
              }}
            >
              Run Hardware Probes
            </Button>

            <Button
              icon={<Checkmark20Regular />}
              onClick={() => setPreflightModalOpen(true)}
            >
              Run Preflight Check
            </Button>
          </div>
        </Card>
      )}

      {activeTab === 'ui' && (
        <Card className={styles.section}>
          <Title2>UI Customization</Title2>
          <Text size={200} style={{ marginBottom: tokens.spacingVerticalL }}>
            Customize the appearance and scale of the user interface
          </Text>
          <div className={styles.form}>
            <Field label={`UI Scale: ${uiScale}%`} hint="Adjust the overall size of the interface">
              <Slider
                min={75}
                max={150}
                step={5}
                value={uiScale}
                onChange={(_, data) => setUiScale(data.value)}
              />
            </Field>
            <Field label="Compact Mode">
              <Switch
                checked={compactMode}
                onChange={(_, data) => setCompactMode(data.checked)}
              />
              <Text size={200}>
                {compactMode ? 'Enabled' : 'Disabled'} - Reduces spacing and padding for a more compact layout
              </Text>
            </Field>
            <Text size={200} style={{ fontStyle: 'italic', color: tokens.colorNeutralForeground3 }}>
              Note: Changes take effect after saving and refreshing the page
            </Text>
          </div>
        </Card>
      )}

      {activeTab === 'providers' && (
        <Card className={styles.section}>
          <Title2>Provider Profiles</Title2>
          <Text>Select a provider profile to configure which services are used</Text>
          <div style={{ marginTop: tokens.spacingVerticalL }}>
            {profiles.map((profile) => (
              <div
                key={profile.name}
                className={styles.profileCard}
                onClick={() => applyProfile(profile.name)}
              >
                <Text weight="semibold">{profile.name}</Text>
                <br />
                <Text size={200}>{profile.description}</Text>
              </div>
            ))}
          </div>
        </Card>
      )}

      {activeTab === 'localproviders' && (
        <Card className={styles.section}>
          <Title2>Local AI Providers Configuration</Title2>
          <Text size={200} style={{ marginBottom: tokens.spacingVerticalL }}>
            Configure paths and URLs for locally-installed AI tools. These tools run on your machine and don't require API keys.
          </Text>
          
          <Card style={{ marginBottom: tokens.spacingVerticalL, padding: tokens.spacingVerticalM, backgroundColor: tokens.colorNeutralBackground3 }}>
            <Text weight="semibold" size={300}>📖 Need Help?</Text>
            <Text size={200} style={{ marginTop: tokens.spacingVerticalXS }}>
              See the <strong>LOCAL_PROVIDERS_SETUP.md</strong> guide in the repository for detailed setup instructions for Stable Diffusion, Ollama, and FFmpeg.
              Visit the <strong>Downloads</strong> page to install components automatically.
            </Text>
          </Card>

          <div className={styles.form}>
            <Field 
              label="Stable Diffusion WebUI URL" 
              hint="URL where Stable Diffusion WebUI is running (requires NVIDIA GPU with 6GB+ VRAM)"
            >
              <div style={{ display: 'flex', gap: tokens.spacingHorizontalS, alignItems: 'flex-start' }}>
                <Input 
                  style={{ flex: 1 }}
                  placeholder="http://127.0.0.1:7860" 
                  value={providerPaths.stableDiffusionUrl}
                  onChange={(e) => updateProviderPath('stableDiffusionUrl', e.target.value)}
                />
                <Button 
                  size="small"
                  onClick={() => testProvider('stablediffusion', providerPaths.stableDiffusionUrl)}
                >
                  Test Connection
                </Button>
              </div>
              {testResults.stablediffusion && (
                <Text 
                  size={200} 
                  style={{ 
                    marginTop: tokens.spacingVerticalXS,
                    color: testResults.stablediffusion.success 
                      ? tokens.colorPaletteGreenForeground1 
                      : tokens.colorPaletteRedForeground1 
                  }}
                >
                  {testResults.stablediffusion.success ? '✓' : '✗'} {testResults.stablediffusion.message}
                </Text>
              )}
            </Field>
            
            <Field 
              label="Ollama URL" 
              hint="URL where Ollama is running for local LLM generation"
            >
              <div style={{ display: 'flex', gap: tokens.spacingHorizontalS, alignItems: 'flex-start' }}>
                <Input 
                  style={{ flex: 1 }}
                  placeholder="http://127.0.0.1:11434" 
                  value={providerPaths.ollamaUrl}
                  onChange={(e) => updateProviderPath('ollamaUrl', e.target.value)}
                />
                <Button 
                  size="small"
                  onClick={() => testProvider('ollama', providerPaths.ollamaUrl)}
                >
                  Test Connection
                </Button>
              </div>
              {testResults.ollama && (
                <Text 
                  size={200} 
                  style={{ 
                    marginTop: tokens.spacingVerticalXS,
                    color: testResults.ollama.success 
                      ? tokens.colorPaletteGreenForeground1 
                      : tokens.colorPaletteRedForeground1 
                  }}
                >
                  {testResults.ollama.success ? '✓' : '✗'} {testResults.ollama.message}
                </Text>
              )}
            </Field>
            
            <Field 
              label="FFmpeg Executable Path" 
              hint="Path to ffmpeg.exe (leave empty to use system PATH or download from Downloads page)"
            >
              <div style={{ display: 'flex', gap: tokens.spacingHorizontalS, alignItems: 'flex-start' }}>
                <Input 
                  style={{ flex: 1 }}
                  placeholder="C:\path\to\ffmpeg.exe or leave empty for system PATH" 
                  value={providerPaths.ffmpegPath}
                  onChange={(e) => updateProviderPath('ffmpegPath', e.target.value)}
                />
                <Button 
                  size="small"
                  onClick={() => testProvider('ffmpeg', undefined, providerPaths.ffmpegPath || 'ffmpeg')}
                >
                  Test
                </Button>
              </div>
              {testResults.ffmpeg && (
                <Text 
                  size={200} 
                  style={{ 
                    marginTop: tokens.spacingVerticalXS,
                    color: testResults.ffmpeg.success 
                      ? tokens.colorPaletteGreenForeground1 
                      : tokens.colorPaletteRedForeground1 
                  }}
                >
                  {testResults.ffmpeg.success ? '✓' : '✗'} {testResults.ffmpeg.message}
                </Text>
              )}
            </Field>
            
            <Field 
              label="FFprobe Executable Path" 
              hint="Path to ffprobe.exe (usually in the same folder as FFmpeg)"
            >
              <Input 
                placeholder="C:\path\to\ffprobe.exe or leave empty for system PATH" 
                value={providerPaths.ffprobePath}
                onChange={(e) => updateProviderPath('ffprobePath', e.target.value)}
              />
            </Field>
            
            <Field 
              label="Output Directory" 
              hint="Default directory for rendered videos (leave empty for Documents\AuraVideoStudio)"
            >
              <Input 
                placeholder="C:\Users\YourName\Videos\AuraOutput" 
                value={providerPaths.outputDirectory}
                onChange={(e) => updateProviderPath('outputDirectory', e.target.value)}
              />
            </Field>
            
            {pathsModified && (
              <Text size={200} style={{ color: tokens.colorPaletteYellowForeground1 }}>
                ⚠️ You have unsaved changes
              </Text>
            )}
            <Button 
              appearance="primary" 
              onClick={saveProviderPaths}
              disabled={!pathsModified || savingPaths}
            >
              {savingPaths ? 'Saving...' : 'Save Provider Paths'}
            </Button>
          </div>
        </Card>
      )}

      {activeTab === 'apikeys' && (
        <Card className={styles.section}>
          <Title2>API Keys</Title2>
          <Text size={200} style={{ marginBottom: tokens.spacingVerticalL }}>
            Configure API keys for external services. Keys are stored securely.
          </Text>
          <div className={styles.form}>
            <Field label="OpenAI API Key" hint="Required for GPT-based script generation">
              <Input 
                type="password" 
                placeholder="sk-..." 
                value={apiKeys.openai}
                onChange={(e) => updateApiKey('openai', e.target.value)}
              />
            </Field>
            <Field label="ElevenLabs API Key" hint="Required for high-quality voice synthesis">
              <Input 
                type="password" 
                placeholder="..." 
                value={apiKeys.elevenlabs}
                onChange={(e) => updateApiKey('elevenlabs', e.target.value)}
              />
            </Field>
            <Field label="Pexels API Key" hint="Required for stock video and images">
              <Input 
                type="password" 
                placeholder="..." 
                value={apiKeys.pexels}
                onChange={(e) => updateApiKey('pexels', e.target.value)}
              />
            </Field>
            <Field label="Stability AI API Key" hint="Optional - for AI image generation">
              <Input 
                type="password" 
                placeholder="..." 
                value={apiKeys.stabilityai}
                onChange={(e) => updateApiKey('stabilityai', e.target.value)}
              />
            </Field>
            {keysModified && (
              <Text size={200} style={{ color: tokens.colorPaletteYellowForeground1 }}>
                ⚠️ You have unsaved changes
              </Text>
            )}
            <Button 
              appearance="primary" 
              onClick={saveApiKeys}
              disabled={!keysModified || savingKeys}
            >
              {savingKeys ? 'Saving...' : 'Save API Keys'}
            </Button>
          </div>
        </Card>
      )}

      {activeTab === 'privacy' && (
        <Card className={styles.section}>
          <Title2>Privacy Settings</Title2>
          <div className={styles.form}>
            <Field label="Telemetry">
              <Switch />
              <Text size={200}>Send anonymous usage data to improve the app (disabled by default)</Text>
            </Field>
            <Field label="Crash Reports">
              <Switch />
              <Text size={200}>Send crash reports to help diagnose issues (disabled by default)</Text>
            </Field>
          </div>
        </Card>
      )}

      <div className={styles.actions}>
        <Button appearance="primary" icon={<Save24Regular />} onClick={saveSettings}>
          Save Settings
        </Button>
      </div>

      <PreflightModal
        open={preflightModalOpen}
        onOpenChange={setPreflightModalOpen}
        onAutoSwitchToFreePath={async () => {
          try {
            await fetch('/api/profiles/apply', {
              method: 'POST',
              headers: { 'Content-Type': 'application/json' },
              body: JSON.stringify({ profileName: 'Free-Only' }),
            });
            alert('Switched to Free-Only profile');
            fetchSettings(); // Refresh settings
          } catch (error) {
            console.error('Error switching profile:', error);
          }
        }}
      />
    </div>
  );
}
