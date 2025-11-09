import {
  makeStyles,
  tokens,
  Title1,
  Text,
  Button,
  Card,
  Tab,
  TabList,
  Dropdown,
  Option,
} from '@fluentui/react-components';
import {
  Save24Regular,
  Settings24Regular,
  Key24Regular,
  Rocket24Regular,
  Search24Regular,
  BookTemplateRegular,
  PersonAvailable24Regular,
} from '@fluentui/react-icons';
import { useState, useEffect } from 'react';
import { GenerationSettingsTab } from '../components/Settings/GenerationSettingsTab';
import { PreferencesTab } from '../components/Settings/PreferencesTab';
import { ProvidersTab } from '../components/Settings/ProvidersTab';
import { SettingsPresets, type SettingsPreset } from '../components/Settings/SettingsPresets';
import {
  SettingsProfiles,
  useSettingsProfiles,
  type SettingsProfile,
} from '../components/Settings/SettingsProfiles';
import { SettingsSearch, type SearchableItem } from '../components/Settings/SettingsSearch';
import { settingsService } from '../services/settingsService';
import type { UserSettings } from '../types/settings';
import { createDefaultSettings } from '../types/settings';

const useStyles = makeStyles({
  container: {
    maxWidth: '100%',
    width: '100%',
    margin: '0 auto',
    padding: `0 ${tokens.spacingHorizontalXL}`,
    '@media (max-width: 768px)': {
      padding: `0 ${tokens.spacingHorizontalM}`,
    },
  },
  header: {
    marginBottom: tokens.spacingVerticalXXL,
    display: 'flex',
    flexDirection: 'column',
    gap: tokens.spacingVerticalS,
  },
  headerRow: {
    display: 'flex',
    justifyContent: 'space-between',
    alignItems: 'center',
    flexWrap: 'wrap',
    gap: tokens.spacingVerticalM,
  },
  subtitle: {
    color: tokens.colorNeutralForeground3,
  },
  tabs: {
    marginBottom: tokens.spacingVerticalL,
  },
  contentArea: {
    marginTop: tokens.spacingVerticalXL,
  },
  actions: {
    display: 'flex',
    gap: tokens.spacingHorizontalM,
    justifyContent: 'flex-end',
    marginTop: tokens.spacingVerticalXL,
    flexWrap: 'wrap',
  },
  profileSelector: {
    display: 'flex',
    alignItems: 'center',
    gap: tokens.spacingHorizontalS,
  },
  tabsHeader: {
    display: 'flex',
    justifyContent: 'space-between',
    alignItems: 'center',
    marginBottom: tokens.spacingVerticalL,
    flexWrap: 'wrap',
    gap: tokens.spacingVerticalM,
  },
  quickActions: {
    display: 'flex',
    gap: tokens.spacingHorizontalS,
  },
});

const STORAGE_KEY_PRESETS = 'aura-custom-presets';

export function SettingsPageRedesigned() {
  const styles = useStyles();
  const [activeTab, setActiveTab] = useState<
    'generation' | 'providers' | 'preferences' | 'presets' | 'profiles'
  >('generation');
  const [userSettings, setUserSettings] = useState<UserSettings>(createDefaultSettings());
  const [originalSettings, setOriginalSettings] = useState<UserSettings>(createDefaultSettings());
  const [hasUnsavedChanges, setHasUnsavedChanges] = useState(false);
  const [customPresets, setCustomPresets] = useState<SettingsPreset[]>([]);
  const [searchQuery, setSearchQuery] = useState('');
  const [searchResults, setSearchResults] = useState<SearchableItem[]>([]);

  const {
    profiles,
    activeProfileId,
    handleCreateProfile,
    handleUpdateProfile,
    handleDeleteProfile,
    handleSwitchProfile,
  } = useSettingsProfiles();

  useEffect(() => {
    loadUserSettings();
    loadCustomPresets();
  }, []);

  useEffect(() => {
    const hasChanges = JSON.stringify(userSettings) !== JSON.stringify(originalSettings);
    setHasUnsavedChanges(hasChanges);
  }, [userSettings, originalSettings]);

  const loadUserSettings = async () => {
    try {
      const loaded = await settingsService.loadSettings();
      setUserSettings(loaded);
      setOriginalSettings(loaded);
    } catch (error) {
      console.error('Error loading user settings:', error);
    }
  };

  const loadCustomPresets = () => {
    try {
      const stored = localStorage.getItem(STORAGE_KEY_PRESETS);
      if (stored) {
        setCustomPresets(JSON.parse(stored));
      }
    } catch (error) {
      console.error('Error loading custom presets:', error);
    }
  };

  const saveUserSettings = async () => {
    try {
      const success = await settingsService.saveSettings(userSettings);
      if (success) {
        setOriginalSettings({ ...userSettings });
        alert('Settings saved successfully!');
      } else {
        alert('Failed to save settings to backend, but saved locally.');
      }
    } catch (error) {
      console.error('Error saving settings:', error);
      alert('Error saving settings');
    }
  };

  const handleApplyPreset = (preset: SettingsPreset) => {
    const updatedSettings = {
      ...userSettings,
      ...preset.settings,
    };
    setUserSettings(updatedSettings);
  };

  const handleSavePreset = (preset: SettingsPreset) => {
    const updated = [...customPresets.filter((p) => p.id !== preset.id), preset];
    setCustomPresets(updated);
    localStorage.setItem(STORAGE_KEY_PRESETS, JSON.stringify(updated));
  };

  const handleDeletePreset = (presetId: string) => {
    const updated = customPresets.filter((p) => p.id !== presetId);
    setCustomPresets(updated);
    localStorage.setItem(STORAGE_KEY_PRESETS, JSON.stringify(updated));
  };

  const handleProfileCreate = (profile: SettingsProfile) => {
    handleCreateProfile(profile);
  };

  const handleProfileSwitch = async (profileId: string) => {
    const profile = profiles.find((p) => p.id === profileId);
    if (profile) {
      if (hasUnsavedChanges) {
        if (
          !window.confirm(
            'You have unsaved changes. Switching profiles will discard them. Continue?'
          )
        ) {
          return;
        }
      }
      setUserSettings(profile.settings);
      setOriginalSettings(profile.settings);
      handleSwitchProfile(profileId);
    }
  };

  const searchableItems: SearchableItem[] = [
    {
      id: 'resolution',
      title: 'Resolution',
      description: 'Default video resolution for new projects',
      category: 'Generation Settings',
      keywords: ['quality', 'size', '1080p', '4k', 'video'],
    },
    {
      id: 'framerate',
      title: 'Frame Rate',
      description: 'Default frames per second',
      category: 'Generation Settings',
      keywords: ['fps', 'smooth', '30fps', '60fps'],
    },
    {
      id: 'codec',
      title: 'Video Codec',
      description: 'Default video encoding codec',
      category: 'Generation Settings',
      keywords: ['h264', 'h265', 'encoding', 'compression'],
    },
    {
      id: 'apikeys',
      title: 'API Keys',
      description: 'Configure API keys for cloud services',
      category: 'Providers',
      keywords: ['openai', 'anthropic', 'elevenlabs', 'authentication'],
    },
    {
      id: 'theme',
      title: 'Theme',
      description: 'Color scheme preference',
      category: 'Preferences',
      keywords: ['dark', 'light', 'appearance', 'ui'],
    },
    {
      id: 'language',
      title: 'Language',
      description: 'Interface language',
      category: 'Preferences',
      keywords: ['locale', 'translation', 'i18n'],
    },
    {
      id: 'shortcuts',
      title: 'Keyboard Shortcuts',
      description: 'Customize keyboard shortcuts',
      category: 'Preferences',
      keywords: ['hotkeys', 'keybindings', 'controls'],
    },
    {
      id: 'accessibility',
      title: 'Accessibility',
      description: 'Reduced motion and high contrast options',
      category: 'Preferences',
      keywords: ['a11y', 'motion', 'contrast', 'font size'],
    },
  ];

  const handleSearch = (query: string, results: SearchableItem[]) => {
    setSearchQuery(query);
    setSearchResults(results);
  };

  const handleClearSearch = () => {
    setSearchQuery('');
    setSearchResults([]);
  };

  return (
    <div className={styles.container}>
      <div className={styles.header}>
        <div className={styles.headerRow}>
          <div>
            <Title1>Settings</Title1>
            <Text className={styles.subtitle}>
              Configure your video generation preferences and providers
            </Text>
          </div>
          {profiles.length > 0 && (
            <div className={styles.profileSelector}>
              <PersonAvailable24Regular />
              <Text weight="semibold">Profile:</Text>
              <Dropdown
                value={profiles.find((p) => p.id === activeProfileId)?.name || 'Default'}
                onOptionSelect={(_, data) => {
                  if (data.optionValue) {
                    handleProfileSwitch(data.optionValue);
                  }
                }}
              >
                {profiles.map((profile) => (
                  <Option key={profile.id} value={profile.id}>
                    {profile.name}
                  </Option>
                ))}
              </Dropdown>
            </div>
          )}
        </div>
        {hasUnsavedChanges && (
          <Card
            style={{
              padding: tokens.spacingVerticalS,
              backgroundColor: tokens.colorPaletteYellowBackground2,
            }}
          >
            <Text style={{ color: tokens.colorPaletteYellowForeground1, fontWeight: 600 }}>
              ⚠️ You have unsaved changes
            </Text>
          </Card>
        )}
      </div>

      <SettingsSearch items={searchableItems} onSearch={handleSearch} onClear={handleClearSearch} />

      {searchQuery && (
        <Card style={{ padding: tokens.spacingVerticalM, marginBottom: tokens.spacingVerticalL }}>
          <Text weight="semibold">
            Search Results: {searchResults.length}{' '}
            {searchResults.length === 1 ? 'match' : 'matches'} for &ldquo;{searchQuery}&rdquo;
          </Text>
          {searchResults.length > 0 && (
            <ul style={{ marginTop: tokens.spacingVerticalS }}>
              {searchResults.map((result) => (
                <li key={result.id}>
                  <Text>
                    <strong>{result.title}</strong> - {result.description} ({result.category})
                  </Text>
                </li>
              ))}
            </ul>
          )}
        </Card>
      )}

      <div className={styles.tabsHeader}>
        <TabList
          className={styles.tabs}
          selectedValue={activeTab}
          onTabSelect={(_, data) => setActiveTab(data.value as typeof activeTab)}
        >
          <Tab value="generation" icon={<Settings24Regular />}>
            Generation
          </Tab>
          <Tab value="providers" icon={<Key24Regular />}>
            Providers
          </Tab>
          <Tab value="preferences" icon={<Rocket24Regular />}>
            Preferences
          </Tab>
          <Tab value="presets" icon={<BookTemplateRegular />}>
            Presets
          </Tab>
          <Tab value="profiles" icon={<PersonAvailable24Regular />}>
            Profiles
          </Tab>
        </TabList>

        <div className={styles.quickActions}>
          <Button
            appearance="secondary"
            size="small"
            icon={<Search24Regular />}
            onClick={() => {
              const searchInput = document.querySelector(
                'input[placeholder="Search settings..."]'
              ) as HTMLInputElement | null;
              searchInput?.focus();
            }}
          >
            Search
          </Button>
        </div>
      </div>

      <div className={styles.contentArea}>
        {activeTab === 'generation' && (
          <GenerationSettingsTab
            videoDefaults={userSettings.videoDefaults}
            fileLocations={userSettings.fileLocations}
            onVideoDefaultsChange={(videoDefaults) =>
              setUserSettings({ ...userSettings, videoDefaults })
            }
            onFileLocationsChange={(fileLocations) =>
              setUserSettings({ ...userSettings, fileLocations })
            }
          />
        )}

        {activeTab === 'providers' && (
          <ProvidersTab
            apiKeys={userSettings.apiKeys}
            onApiKeysChange={(apiKeys) => setUserSettings({ ...userSettings, apiKeys })}
            advanced={userSettings.advanced}
            onAdvancedChange={(advanced) => setUserSettings({ ...userSettings, advanced })}
            onTestApiKey={async (provider, apiKey) => settingsService.testApiKey(provider, apiKey)}
          />
        )}

        {activeTab === 'preferences' && (
          <PreferencesTab
            general={userSettings.general}
            ui={userSettings.ui}
            advanced={userSettings.advanced}
            onGeneralChange={(general) => setUserSettings({ ...userSettings, general })}
            onUIChange={(ui) => setUserSettings({ ...userSettings, ui })}
            onAdvancedChange={(advanced) => setUserSettings({ ...userSettings, advanced })}
          />
        )}

        {activeTab === 'presets' && (
          <SettingsPresets
            currentSettings={userSettings}
            onApplyPreset={handleApplyPreset}
            onSavePreset={handleSavePreset}
            customPresets={customPresets}
            onDeletePreset={handleDeletePreset}
          />
        )}

        {activeTab === 'profiles' && (
          <SettingsProfiles
            profiles={profiles}
            activeProfileId={activeProfileId}
            onCreateProfile={handleProfileCreate}
            onUpdateProfile={handleUpdateProfile}
            onDeleteProfile={handleDeleteProfile}
            onSwitchProfile={handleProfileSwitch}
            currentSettings={userSettings}
          />
        )}
      </div>

      <div className={styles.actions}>
        <Button
          appearance="primary"
          icon={<Save24Regular />}
          onClick={saveUserSettings}
          disabled={!hasUnsavedChanges}
        >
          Save All Settings
        </Button>
        {hasUnsavedChanges && (
          <Button
            appearance="subtle"
            onClick={() => {
              if (window.confirm('Discard all unsaved changes?')) {
                setUserSettings({ ...originalSettings });
              }
            }}
          >
            Discard Changes
          </Button>
        )}
      </div>
    </div>
  );
}
