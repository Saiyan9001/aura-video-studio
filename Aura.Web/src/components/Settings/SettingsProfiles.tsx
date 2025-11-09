import {
  makeStyles,
  tokens,
  Title2,
  Text,
  Button,
  Input,
  Card,
  Field,
  Dropdown,
  Option,
  Dialog,
  DialogSurface,
  DialogTitle,
  DialogContent,
  DialogBody,
  DialogActions,
} from '@fluentui/react-components';
import {
  Add24Regular,
  Edit24Regular,
  Delete24Regular,
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
  profileHeader: {
    display: 'flex',
    justifyContent: 'space-between',
    alignItems: 'center',
    marginBottom: tokens.spacingVerticalL,
  },
  profileSelector: {
    display: 'flex',
    gap: tokens.spacingHorizontalS,
    alignItems: 'center',
  },
  profilesList: {
    display: 'flex',
    flexDirection: 'column',
    gap: tokens.spacingVerticalS,
  },
  profileItem: {
    padding: tokens.spacingVerticalM,
    backgroundColor: tokens.colorNeutralBackground2,
    borderRadius: tokens.borderRadiusMedium,
    display: 'flex',
    justifyContent: 'space-between',
    alignItems: 'center',
    transition: 'all 0.2s',
  },
  profileItemActive: {
    backgroundColor: tokens.colorBrandBackground2,
  },
  profileActions: {
    display: 'flex',
    gap: tokens.spacingHorizontalXS,
  },
  infoBox: {
    padding: tokens.spacingVerticalM,
    backgroundColor: tokens.colorNeutralBackground3,
    borderRadius: tokens.borderRadiusMedium,
    marginBottom: tokens.spacingVerticalL,
  },
});

export interface SettingsProfile {
  id: string;
  name: string;
  description: string;
  settings: UserSettings;
  createdAt: string;
  updatedAt: string;
  isActive?: boolean;
}

interface SettingsProfilesProps {
  profiles: SettingsProfile[];
  activeProfileId: string;
  onCreateProfile: (profile: SettingsProfile) => void;
  onUpdateProfile: (profile: SettingsProfile) => void;
  onDeleteProfile: (profileId: string) => void;
  onSwitchProfile: (profileId: string) => void;
  currentSettings: UserSettings;
}

const STORAGE_KEY = 'aura-settings-profiles';
const ACTIVE_PROFILE_KEY = 'aura-active-profile';

export function SettingsProfiles({
  profiles,
  activeProfileId,
  onCreateProfile,
  onUpdateProfile,
  onDeleteProfile,
  onSwitchProfile,
  currentSettings,
}: SettingsProfilesProps) {
  const styles = useStyles();
  const [showCreateDialog, setShowCreateDialog] = useState(false);
  const [showEditDialog, setShowEditDialog] = useState(false);
  const [editingProfile, setEditingProfile] = useState<SettingsProfile | null>(null);
  const [newProfileName, setNewProfileName] = useState('');
  const [newProfileDescription, setNewProfileDescription] = useState('');

  const handleCreateProfile = () => {
    if (!newProfileName.trim()) return;

    const newProfile: SettingsProfile = {
      id: `profile-${Date.now()}`,
      name: newProfileName,
      description: newProfileDescription,
      settings: currentSettings,
      createdAt: new Date().toISOString(),
      updatedAt: new Date().toISOString(),
    };

    onCreateProfile(newProfile);
    setNewProfileName('');
    setNewProfileDescription('');
    setShowCreateDialog(false);
  };

  const handleEditProfile = () => {
    if (!editingProfile || !newProfileName.trim()) return;

    const updatedProfile: SettingsProfile = {
      ...editingProfile,
      name: newProfileName,
      description: newProfileDescription,
      updatedAt: new Date().toISOString(),
    };

    onUpdateProfile(updatedProfile);
    setEditingProfile(null);
    setNewProfileName('');
    setNewProfileDescription('');
    setShowEditDialog(false);
  };

  const openEditDialog = (profile: SettingsProfile) => {
    setEditingProfile(profile);
    setNewProfileName(profile.name);
    setNewProfileDescription(profile.description);
    setShowEditDialog(true);
  };

  const handleDeleteProfile = (profileId: string) => {
    const profile = profiles.find((p) => p.id === profileId);
    if (!profile) return;

    if (window.confirm(`Delete profile "${profile.name}"? This action cannot be undone.`)) {
      onDeleteProfile(profileId);
    }
  };

  return (
    <div className={styles.container}>
      <Card>
        <div style={{ padding: tokens.spacingVerticalL }}>
          <div className={styles.profileHeader}>
            <div>
              <Title2>Settings Profiles</Title2>
              <Text size={200}>Manage multiple configuration profiles for different use cases</Text>
            </div>
            <Button
              appearance="primary"
              icon={<Add24Regular />}
              onClick={() => setShowCreateDialog(true)}
            >
              New Profile
            </Button>
          </div>

          <div className={styles.infoBox}>
            <Text size={200}>
              💡 <strong>Use profiles to:</strong>
              <br />
              • Separate work and personal configurations
              <br />
              • Switch between different client setups
              <br />
              • Test new settings without affecting your main configuration
              <br />• Quickly switch between home and office environments
            </Text>
          </div>

          <div className={styles.profileSelector}>
            <Text weight="semibold">Active Profile:</Text>
            <Dropdown
              value={profiles.find((p) => p.id === activeProfileId)?.name || 'Default'}
              onOptionSelect={(_, data) => {
                if (data.optionValue) {
                  onSwitchProfile(data.optionValue);
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

          {profiles.length === 0 ? (
            <Text
              size={200}
              style={{
                color: tokens.colorNeutralForeground3,
                marginTop: tokens.spacingVerticalL,
              }}
            >
              No profiles yet. Create one to get started.
            </Text>
          ) : (
            <div className={styles.profilesList} style={{ marginTop: tokens.spacingVerticalL }}>
              {profiles.map((profile) => (
                <div
                  key={profile.id}
                  className={`${styles.profileItem} ${profile.id === activeProfileId ? styles.profileItemActive : ''}`}
                >
                  <div>
                    <div
                      style={{
                        display: 'flex',
                        alignItems: 'center',
                        gap: tokens.spacingHorizontalXS,
                      }}
                    >
                      <Text weight="semibold">{profile.name}</Text>
                      {profile.id === activeProfileId && (
                        <Checkmark24Regular
                          style={{ color: tokens.colorPaletteGreenForeground1 }}
                        />
                      )}
                    </div>
                    <Text size={200} style={{ color: tokens.colorNeutralForeground3 }}>
                      {profile.description}
                    </Text>
                    <Text size={100} style={{ color: tokens.colorNeutralForeground4 }}>
                      Updated: {new Date(profile.updatedAt).toLocaleString()}
                    </Text>
                  </div>
                  <div className={styles.profileActions}>
                    {profile.id !== activeProfileId && (
                      <Button
                        size="small"
                        appearance="subtle"
                        onClick={() => onSwitchProfile(profile.id)}
                      >
                        Activate
                      </Button>
                    )}
                    <Button
                      size="small"
                      appearance="subtle"
                      icon={<Edit24Regular />}
                      onClick={() => openEditDialog(profile)}
                    />
                    <Button
                      size="small"
                      appearance="subtle"
                      icon={<Delete24Regular />}
                      onClick={() => handleDeleteProfile(profile.id)}
                      disabled={profiles.length === 1}
                    />
                  </div>
                </div>
              ))}
            </div>
          )}
        </div>
      </Card>

      <Dialog open={showCreateDialog} onOpenChange={(_, data) => setShowCreateDialog(data.open)}>
        <DialogSurface>
          <DialogBody>
            <DialogTitle>Create New Profile</DialogTitle>
            <DialogContent>
              <Field label="Profile Name" required>
                <Input
                  value={newProfileName}
                  onChange={(e) => setNewProfileName(e.target.value)}
                  placeholder="e.g., Home, Work, Client A"
                />
              </Field>
              <Field label="Description">
                <Input
                  value={newProfileDescription}
                  onChange={(e) => setNewProfileDescription(e.target.value)}
                  placeholder="Describe this profile..."
                />
              </Field>
              <Text size={200} style={{ color: tokens.colorNeutralForeground3 }}>
                The new profile will be created with your current settings.
              </Text>
            </DialogContent>
            <DialogActions>
              <Button appearance="secondary" onClick={() => setShowCreateDialog(false)}>
                Cancel
              </Button>
              <Button
                appearance="primary"
                onClick={handleCreateProfile}
                disabled={!newProfileName.trim()}
              >
                Create Profile
              </Button>
            </DialogActions>
          </DialogBody>
        </DialogSurface>
      </Dialog>

      <Dialog open={showEditDialog} onOpenChange={(_, data) => setShowEditDialog(data.open)}>
        <DialogSurface>
          <DialogBody>
            <DialogTitle>Edit Profile</DialogTitle>
            <DialogContent>
              <Field label="Profile Name" required>
                <Input
                  value={newProfileName}
                  onChange={(e) => setNewProfileName(e.target.value)}
                  placeholder="e.g., Home, Work, Client A"
                />
              </Field>
              <Field label="Description">
                <Input
                  value={newProfileDescription}
                  onChange={(e) => setNewProfileDescription(e.target.value)}
                  placeholder="Describe this profile..."
                />
              </Field>
            </DialogContent>
            <DialogActions>
              <Button appearance="secondary" onClick={() => setShowEditDialog(false)}>
                Cancel
              </Button>
              <Button
                appearance="primary"
                onClick={handleEditProfile}
                disabled={!newProfileName.trim()}
              >
                Save Changes
              </Button>
            </DialogActions>
          </DialogBody>
        </DialogSurface>
      </Dialog>
    </div>
  );
}

export function useSettingsProfiles() {
  const [profiles, setProfiles] = useState<SettingsProfile[]>([]);
  const [activeProfileId, setActiveProfileId] = useState<string>('default');

  useEffect(() => {
    const stored = localStorage.getItem(STORAGE_KEY);
    if (stored) {
      try {
        setProfiles(JSON.parse(stored));
      } catch {
        // Ignore invalid JSON
      }
    }

    const activeId = localStorage.getItem(ACTIVE_PROFILE_KEY);
    if (activeId) {
      setActiveProfileId(activeId);
    }
  }, []);

  const saveProfiles = (updatedProfiles: SettingsProfile[]) => {
    setProfiles(updatedProfiles);
    localStorage.setItem(STORAGE_KEY, JSON.stringify(updatedProfiles));
  };

  const handleCreateProfile = (profile: SettingsProfile) => {
    saveProfiles([...profiles, profile]);
  };

  const handleUpdateProfile = (updatedProfile: SettingsProfile) => {
    saveProfiles(profiles.map((p) => (p.id === updatedProfile.id ? updatedProfile : p)));
  };

  const handleDeleteProfile = (profileId: string) => {
    const filtered = profiles.filter((p) => p.id !== profileId);
    saveProfiles(filtered);
    if (activeProfileId === profileId && filtered.length > 0) {
      setActiveProfileId(filtered[0].id);
      localStorage.setItem(ACTIVE_PROFILE_KEY, filtered[0].id);
    }
  };

  const handleSwitchProfile = (profileId: string) => {
    setActiveProfileId(profileId);
    localStorage.setItem(ACTIVE_PROFILE_KEY, profileId);
  };

  return {
    profiles,
    activeProfileId,
    handleCreateProfile,
    handleUpdateProfile,
    handleDeleteProfile,
    handleSwitchProfile,
  };
}
