import {
  makeStyles,
  tokens,
  Title2,
  Title3,
  Text,
  Button,
  Switch,
  Card,
  Field,
  Dropdown,
  Option,
  Divider,
} from '@fluentui/react-components';
import { useState, useEffect } from 'react';
import type { GeneralSettings, UISettings, AdvancedSettings } from '../../types/settings';

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
  themePreview: {
    padding: tokens.spacingVerticalL,
    borderRadius: tokens.borderRadiusMedium,
    border: `1px solid ${tokens.colorNeutralStroke1}`,
    marginTop: tokens.spacingVerticalM,
    transition: 'all 0.3s',
  },
  shortcutsList: {
    display: 'flex',
    flexDirection: 'column',
    gap: tokens.spacingVerticalS,
  },
  shortcutItem: {
    padding: tokens.spacingVerticalS,
    backgroundColor: tokens.colorNeutralBackground2,
    borderRadius: tokens.borderRadiusMedium,
    display: 'flex',
    justifyContent: 'space-between',
    alignItems: 'center',
  },
  keyBadge: {
    padding: `${tokens.spacingVerticalXXS} ${tokens.spacingHorizontalXS}`,
    borderRadius: tokens.borderRadiusSmall,
    backgroundColor: tokens.colorNeutralBackground3,
    border: `1px solid ${tokens.colorNeutralStroke1}`,
    fontFamily: 'monospace',
    fontSize: tokens.fontSizeBase200,
  },
});

interface PreferencesTabProps {
  general: GeneralSettings;
  ui: UISettings;
  advanced: AdvancedSettings;
  onGeneralChange: (settings: GeneralSettings) => void;
  onUIChange: (settings: UISettings) => void;
  onAdvancedChange: (settings: AdvancedSettings) => void;
}

interface KeyboardShortcut {
  action: string;
  description: string;
  defaultKeys: string;
  customizable: boolean;
}

const DEFAULT_SHORTCUTS: KeyboardShortcut[] = [
  { action: 'play', description: 'Play/Pause', defaultKeys: 'Space', customizable: true },
  { action: 'save', description: 'Save Project', defaultKeys: 'Ctrl+S', customizable: true },
  { action: 'undo', description: 'Undo', defaultKeys: 'Ctrl+Z', customizable: true },
  { action: 'redo', description: 'Redo', defaultKeys: 'Ctrl+Y', customizable: true },
  { action: 'delete', description: 'Delete Selection', defaultKeys: 'Delete', customizable: true },
  { action: 'split', description: 'Split Clip', defaultKeys: 'S', customizable: true },
  { action: 'export', description: 'Export Video', defaultKeys: 'Ctrl+E', customizable: true },
];

export function PreferencesTab({
  general,
  ui,
  advanced,
  onGeneralChange,
  onUIChange,
  onAdvancedChange,
}: PreferencesTabProps) {
  const styles = useStyles();
  const [previewTheme, setPreviewTheme] = useState(general.theme);

  useEffect(() => {
    setPreviewTheme(general.theme);
  }, [general.theme]);

  const updateGeneral = <K extends keyof GeneralSettings>(key: K, value: GeneralSettings[K]) => {
    onGeneralChange({ ...general, [key]: value });
  };

  const updateUI = <K extends keyof UISettings>(key: K, value: UISettings[K]) => {
    onUIChange({ ...ui, [key]: value });
  };

  const updateAdvanced = <K extends keyof AdvancedSettings>(key: K, value: AdvancedSettings[K]) => {
    onAdvancedChange({ ...advanced, [key]: value });
  };

  const getThemePreviewStyles = () => {
    const isDark =
      previewTheme === 'Dark' ||
      (previewTheme === 'Auto' && window.matchMedia('(prefers-color-scheme: dark)').matches);
    return {
      backgroundColor: isDark ? '#1f1f1f' : '#ffffff',
      color: isDark ? '#ffffff' : '#1f1f1f',
    };
  };

  return (
    <Card className={styles.section}>
      <Title2>Preferences</Title2>
      <Text size={200} style={{ marginBottom: tokens.spacingVerticalL }}>
        Customize theme, language, keyboard shortcuts, and accessibility options
      </Text>

      <div className={styles.form}>
        <div className={styles.subsection}>
          <Title3>Theme</Title3>
          <Field label="Color Theme" hint="Choose your preferred color scheme">
            <Dropdown
              value={general.theme}
              onOptionSelect={(_, data) => {
                if (data.optionValue) {
                  updateGeneral('theme', data.optionValue as GeneralSettings['theme']);
                }
              }}
            >
              <Option value="Light">Light</Option>
              <Option value="Dark">Dark</Option>
              <Option value="Auto">Auto (System)</Option>
            </Dropdown>
          </Field>

          <div className={styles.themePreview} style={getThemePreviewStyles()}>
            <Text weight="semibold">Theme Preview</Text>
            <br />
            <Text size={200}>This is how your interface will look with the selected theme.</Text>
          </div>
        </div>

        <Divider />

        <div className={styles.subsection}>
          <Title3>Language and Region</Title3>
          <div className={styles.infoBox}>
            <Text size={200}>
              🌍 <strong>Coming soon:</strong> Full internationalization support. Currently
              preparing for multi-language support.
            </Text>
          </div>

          <Field label="Language" hint="Interface language (English only for now)">
            <Dropdown value={general.language} disabled>
              <Option value="en-US">English (US)</Option>
            </Dropdown>
          </Field>

          <Field label="Locale" hint="Date and number formatting">
            <Dropdown
              value={general.locale}
              onOptionSelect={(_, data) => {
                if (data.optionValue) {
                  updateGeneral('locale', data.optionValue);
                }
              }}
            >
              <Option value="en-US">English (US)</Option>
              <Option value="en-GB">English (UK)</Option>
              <Option value="fr-FR">Français (France)</Option>
              <Option value="de-DE">Deutsch (Deutschland)</Option>
              <Option value="es-ES">Español (España)</Option>
              <Option value="ja-JP">日本語 (Japan)</Option>
            </Dropdown>
          </Field>
        </div>

        <Divider />

        <div className={styles.subsection}>
          <Title3>Keyboard Shortcuts</Title3>
          <div className={styles.infoBox}>
            <Text size={200}>
              ⌨️ Customize keyboard shortcuts to match your workflow. Default shortcuts are shown
              below.
            </Text>
          </div>

          <div className={styles.shortcutsList}>
            {DEFAULT_SHORTCUTS.map((shortcut) => (
              <div key={shortcut.action} className={styles.shortcutItem}>
                <div>
                  <Text weight="semibold">{shortcut.description}</Text>
                  <br />
                  <Text size={200} style={{ color: tokens.colorNeutralForeground3 }}>
                    {shortcut.action}
                  </Text>
                </div>
                <div
                  style={{ display: 'flex', alignItems: 'center', gap: tokens.spacingHorizontalS }}
                >
                  <span className={styles.keyBadge}>{shortcut.defaultKeys}</span>
                  {shortcut.customizable && (
                    <Button size="small" appearance="subtle" disabled>
                      Edit
                    </Button>
                  )}
                </div>
              </div>
            ))}
          </div>
        </div>

        <Divider />

        <div className={styles.subsection}>
          <Title3>Accessibility</Title3>
          <Field label="Reduced Motion">
            <Switch
              checked={false}
              onChange={() => {
                // Will be implemented
              }}
            />
            <Text size={200}>
              Minimize animations and transitions for users sensitive to motion
            </Text>
          </Field>

          <Field label="High Contrast">
            <Switch
              checked={false}
              onChange={() => {
                // Will be implemented
              }}
            />
            <Text size={200}>Increase contrast for better visibility</Text>
          </Field>

          <Field label="Font Size" hint="Adjust interface font size">
            <Dropdown
              value={String(ui.scale)}
              onOptionSelect={(_, data) => {
                if (data.optionValue) {
                  updateUI('scale', Number(data.optionValue));
                }
              }}
            >
              <Option value="75">75% (Smaller)</Option>
              <Option value="100">100% (Default)</Option>
              <Option value="125">125% (Larger)</Option>
              <Option value="150">150% (Extra Large)</Option>
            </Dropdown>
          </Field>

          <Field label="Compact Mode">
            <Switch
              checked={ui.compactMode}
              onChange={(_, data) => updateUI('compactMode', data.checked)}
            />
            <Text size={200}>Reduce spacing and padding for a more compact layout</Text>
          </Field>
        </div>

        <Divider />

        <div className={styles.subsection}>
          <Title3>Privacy</Title3>
          <Field label="Telemetry">
            <Switch
              checked={advanced.enableTelemetry}
              onChange={(_, data) => updateAdvanced('enableTelemetry', data.checked)}
            />
            <Text size={200}>Send anonymous usage data to help improve the application</Text>
          </Field>

          <Field label="Crash Reports">
            <Switch
              checked={advanced.enableCrashReports}
              onChange={(_, data) => updateAdvanced('enableCrashReports', data.checked)}
            />
            <Text size={200}>Automatically send crash reports to help diagnose issues</Text>
          </Field>

          <div className={styles.infoBox}>
            <Text size={200}>
              🔒 <strong>Privacy First:</strong> All data collection is optional and can be
              disabled. We never collect personal information or API keys.
            </Text>
          </div>
        </div>
      </div>
    </Card>
  );
}
