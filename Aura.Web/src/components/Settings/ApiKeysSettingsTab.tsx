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
  Tooltip,
  Divider,
} from '@fluentui/react-components';
import {
  CheckmarkCircle24Filled,
  DismissCircle24Filled,
  Info24Regular,
  Eye24Regular,
} from '@fluentui/react-icons';
import { useState } from 'react';
import type { ApiKeysSettings } from '../../types/settings';
import { TooltipContent, TooltipWithLink } from '../Tooltips';
import { ProviderStatusDashboard } from './ProviderStatusDashboard';

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
  categorySection: {
    marginTop: tokens.spacingVerticalL,
    marginBottom: tokens.spacingVerticalL,
  },
  categoryTitle: {
    marginBottom: tokens.spacingVerticalM,
    display: 'flex',
    alignItems: 'center',
    gap: tokens.spacingHorizontalS,
  },
  inputWithButton: {
    display: 'flex',
    gap: tokens.spacingHorizontalS,
    alignItems: 'flex-start',
  },
  testResult: {
    display: 'flex',
    alignItems: 'center',
    gap: tokens.spacingHorizontalXS,
    marginTop: tokens.spacingVerticalXS,
  },
  dashboardToggle: {
    marginTop: tokens.spacingVerticalL,
    marginBottom: tokens.spacingVerticalM,
  },
});

interface ApiKeysSettingsTabProps {
  settings: ApiKeysSettings;
  onChange: (settings: ApiKeysSettings) => void;
  onSave: () => void;
  onTestApiKey: (
    provider: string,
    apiKey: string
  ) => Promise<{ success: boolean; message: string }>;
  hasChanges: boolean;
}

interface TestResult {
  success: boolean;
  message: string;
}

export function ApiKeysSettingsTab({
  settings,
  onChange,
  onSave,
  onTestApiKey,
  hasChanges,
}: ApiKeysSettingsTabProps) {
  const styles = useStyles();
  const [testResults, setTestResults] = useState<Record<string, TestResult | null>>({});
  const [testing, setTesting] = useState<Record<string, boolean>>({});
  const [showDashboard, setShowDashboard] = useState(false);

  const updateSetting = <K extends keyof ApiKeysSettings>(key: K, value: ApiKeysSettings[K]) => {
    onChange({ ...settings, [key]: value });
    // Clear test result when value changes
    setTestResults((prev) => ({ ...prev, [key]: null }));
  };

  const handleTest = async (provider: string, key: keyof ApiKeysSettings) => {
    const apiKey = settings[key];
    if (!apiKey) {
      return;
    }

    setTesting((prev) => ({ ...prev, [key]: true }));
    setTestResults((prev) => ({ ...prev, [key]: null })); // Clear previous result
    try {
      const result = await onTestApiKey(provider, apiKey);
      setTestResults((prev) => ({ ...prev, [key]: result }));
    } catch (error: unknown) {
      const errorObj = error instanceof Error ? error : new Error(String(error));
      setTestResults((prev) => ({
        ...prev,
        [key]: {
          success: false,
          message: `Test failed: ${errorObj.message}`,
        },
      }));
    } finally {
      setTesting((prev) => ({ ...prev, [key]: false }));
    }
  };

  const renderTestResult = (key: string) => {
    const result = testResults[key];
    if (result === null || result === undefined) return null;

    return (
      <div className={styles.testResult}>
        {result.success ? (
          <CheckmarkCircle24Filled style={{ color: tokens.colorPaletteGreenForeground1 }} />
        ) : (
          <DismissCircle24Filled style={{ color: tokens.colorPaletteRedForeground1 }} />
        )}
        <Text size={200}>{result.message}</Text>
      </div>
    );
  };

  return (
    <Card className={styles.section}>
      <Title2>API Keys</Title2>
      <Text size={200} style={{ marginBottom: tokens.spacingVerticalL }}>
        Configure API keys for external services. All fields are optional - the app supports free
        alternatives for all features. Keys are stored securely and encrypted.
      </Text>

      <div className={styles.infoBox}>
        <Text weight="semibold" size={300}>
          🔒 Security & Optional Fields
        </Text>
        <Text size={200} style={{ marginTop: tokens.spacingVerticalXS }}>
          All API keys are <strong>optional</strong>. The app works with free alternatives (Ollama
          for LLM, Windows TTS for voice, stock images for visuals). Keys are encrypted before
          storage. Use &ldquo;Test&rdquo; buttons to verify connectivity.
        </Text>
      </div>

      <div className={styles.dashboardToggle}>
        <Button
          icon={<Eye24Regular />}
          appearance="subtle"
          onClick={() => setShowDashboard(!showDashboard)}
        >
          {showDashboard ? 'Hide' : 'Show'} Provider Status Dashboard
        </Button>
      </div>

      {showDashboard && (
        <>
          <ProviderStatusDashboard />
          <Divider
            style={{ marginTop: tokens.spacingVerticalL, marginBottom: tokens.spacingVerticalL }}
          />
        </>
      )}

      <div className={styles.form}>
        {/* LLM Providers Section */}
        <div className={styles.categorySection}>
          <div className={styles.categoryTitle}>
            <Title3>🤖 LLM Providers (Script Generation)</Title3>
          </div>
          <Text size={200} style={{ marginBottom: tokens.spacingVerticalM }}>
            Optional - Use Ollama (free, local) as an alternative.
          </Text>

          <Field
            label={
              <div style={{ display: 'flex', alignItems: 'center' }}>
                OpenAI API Key
                <Tooltip
                  content={<TooltipWithLink content={TooltipContent.apiKeyOpenAI} />}
                  relationship="label"
                >
                  <Info24Regular style={{ marginLeft: tokens.spacingHorizontalXS }} />
                </Tooltip>
              </div>
            }
            hint="For advanced AI script generation. Click Test to verify with OpenAI."
          >
            <div className={styles.inputWithButton}>
              <Input
                style={{ flex: 1 }}
                type="password"
                value={settings.openAI}
                onChange={(e) => updateSetting('openAI', e.target.value)}
                placeholder="sk-... / sk-proj-... (optional)"
              />
              <Button
                onClick={() => handleTest('openai', 'openAI')}
                disabled={!settings.openAI || testing.openAI}
              >
                {testing.openAI ? 'Testing...' : testResults.openAI ? 'Test Again' : 'Test'}
              </Button>
            </div>
            {renderTestResult('openAI')}
          </Field>

          <Field
            label={
              <div style={{ display: 'flex', alignItems: 'center' }}>
                Anthropic API Key (Claude)
                <Tooltip
                  content={<TooltipWithLink content={TooltipContent.apiKeyAnthropic} />}
                  relationship="label"
                >
                  <Info24Regular style={{ marginLeft: tokens.spacingHorizontalXS }} />
                </Tooltip>
              </div>
            }
            hint="For Claude-based AI features. Get your key from console.anthropic.com"
          >
            <div className={styles.inputWithButton}>
              <Input
                style={{ flex: 1 }}
                type="password"
                value={settings.anthropic}
                onChange={(e) => updateSetting('anthropic', e.target.value)}
                placeholder="sk-ant-... (optional)"
              />
              <Button
                onClick={() => handleTest('anthropic', 'anthropic')}
                disabled={!settings.anthropic || testing.anthropic}
              >
                {testing.anthropic ? 'Testing...' : 'Test'}
              </Button>
            </div>
            {renderTestResult('anthropic')}
          </Field>

          <Field label="Google API Key" hint="For Google Gemini services (optional)">
            <Input
              type="password"
              value={settings.google}
              onChange={(e) => updateSetting('google', e.target.value)}
              placeholder="Optional"
            />
          </Field>

          <Field label="Azure API Key" hint="For Azure OpenAI services (optional)">
            <Input
              type="password"
              value={settings.azure}
              onChange={(e) => updateSetting('azure', e.target.value)}
              placeholder="Optional"
            />
          </Field>
        </div>

        <Divider />

        {/* Text-to-Speech Section */}
        <div className={styles.categorySection}>
          <div className={styles.categoryTitle}>
            <Title3>🎤 Text-to-Speech Services</Title3>
          </div>
          <Text size={200} style={{ marginBottom: tokens.spacingVerticalM }}>
            Optional - Use Windows TTS or Piper TTS (free, local) as alternatives.
          </Text>

          <Field
            label={
              <div style={{ display: 'flex', alignItems: 'center' }}>
                ElevenLabs API Key
                <Tooltip
                  content={<TooltipWithLink content={TooltipContent.apiKeyElevenLabs} />}
                  relationship="label"
                >
                  <Info24Regular style={{ marginLeft: tokens.spacingHorizontalXS }} />
                </Tooltip>
              </div>
            }
            hint="For high-quality voice synthesis. Get your key from elevenlabs.io"
          >
            <div className={styles.inputWithButton}>
              <Input
                style={{ flex: 1 }}
                type="password"
                value={settings.elevenLabs}
                onChange={(e) => updateSetting('elevenLabs', e.target.value)}
                placeholder="Optional"
              />
              <Button
                onClick={() => handleTest('elevenlabs', 'elevenLabs')}
                disabled={!settings.elevenLabs || testing.elevenLabs}
              >
                {testing.elevenLabs ? 'Testing...' : 'Test'}
              </Button>
            </div>
            {renderTestResult('elevenLabs')}
          </Field>
        </div>

        <Divider />

        {/* Image Services Section */}
        <div className={styles.categorySection}>
          <div className={styles.categoryTitle}>
            <Title3>🖼️ Image Services</Title3>
          </div>
          <Text size={200} style={{ marginBottom: tokens.spacingVerticalM }}>
            Optional - Free stock images are available without API keys.
          </Text>

          <Field
            label="Stability AI API Key"
            hint="For AI image generation. Get your key from platform.stability.ai"
          >
            <div className={styles.inputWithButton}>
              <Input
                style={{ flex: 1 }}
                type="password"
                value={settings.stabilityAI}
                onChange={(e) => updateSetting('stabilityAI', e.target.value)}
                placeholder="sk-... (optional)"
              />
              <Button
                onClick={() => handleTest('stabilityai', 'stabilityAI')}
                disabled={!settings.stabilityAI || testing.stabilityAI}
              >
                {testing.stabilityAI ? 'Testing...' : 'Test'}
              </Button>
            </div>
            {renderTestResult('stabilityAI')}
          </Field>

          <Field
            label={
              <div style={{ display: 'flex', alignItems: 'center' }}>
                Pexels API Key
                <Tooltip
                  content={<TooltipWithLink content={TooltipContent.apiKeyPexels} />}
                  relationship="label"
                >
                  <Info24Regular style={{ marginLeft: tokens.spacingHorizontalXS }} />
                </Tooltip>
              </div>
            }
            hint="For stock video and images. Get your free key from pexels.com/api"
          >
            <div className={styles.inputWithButton}>
              <Input
                style={{ flex: 1 }}
                type="password"
                value={settings.pexels}
                onChange={(e) => updateSetting('pexels', e.target.value)}
                placeholder="Optional"
              />
              <Button
                onClick={() => handleTest('pexels', 'pexels')}
                disabled={!settings.pexels || testing.pexels}
              >
                {testing.pexels ? 'Testing...' : 'Test'}
              </Button>
            </div>
            {renderTestResult('pexels')}
          </Field>

          <Field
            label="Pixabay API Key"
            hint="For stock video and images. Get your free key from pixabay.com/api"
          >
            <Input
              type="password"
              value={settings.pixabay}
              onChange={(e) => updateSetting('pixabay', e.target.value)}
              placeholder="Optional"
            />
          </Field>

          <Field
            label="Unsplash API Key"
            hint="For high-quality stock images. Get your free key from unsplash.com/developers"
          >
            <Input
              type="password"
              value={settings.unsplash}
              onChange={(e) => updateSetting('unsplash', e.target.value)}
              placeholder="Optional"
            />
          </Field>
        </div>

        <Divider />

        {hasChanges && (
          <div className={styles.infoBox}>
            <Text weight="semibold" style={{ color: tokens.colorPaletteYellowForeground1 }}>
              ⚠️ You have unsaved changes
            </Text>
          </div>
        )}

        <div>
          <Button appearance="primary" onClick={onSave} disabled={!hasChanges}>
            Save API Keys
          </Button>
        </div>
      </div>
    </Card>
  );
}
