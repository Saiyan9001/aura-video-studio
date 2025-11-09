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
  Divider,
  Spinner,
} from '@fluentui/react-components';
import {
  CheckmarkCircle24Filled,
  DismissCircle24Filled,
  Eye24Regular,
  EyeOff24Regular,
  ArrowSort24Regular,
} from '@fluentui/react-icons';
import { useState } from 'react';
import type { ApiKeysSettings, AdvancedSettings } from '../../types/settings';
import { OllamaProviderConfig } from './OllamaProviderConfig';
import { OpenAIProviderConfig } from './OpenAIProviderConfig';

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
  testResult: {
    display: 'flex',
    alignItems: 'center',
    gap: tokens.spacingHorizontalXS,
    marginTop: tokens.spacingVerticalXS,
  },
  providerList: {
    display: 'flex',
    flexDirection: 'column',
    gap: tokens.spacingVerticalS,
  },
  providerItem: {
    padding: tokens.spacingVerticalM,
    backgroundColor: tokens.colorNeutralBackground2,
    borderRadius: tokens.borderRadiusMedium,
    display: 'flex',
    justifyContent: 'space-between',
    alignItems: 'center',
    cursor: 'grab',
    ':active': {
      cursor: 'grabbing',
    },
  },
  priorityBadge: {
    padding: `${tokens.spacingVerticalXXS} ${tokens.spacingHorizontalXS}`,
    borderRadius: tokens.borderRadiusSmall,
    backgroundColor: tokens.colorBrandBackground,
    color: tokens.colorNeutralForegroundOnBrand,
    fontSize: tokens.fontSizeBase200,
    fontWeight: tokens.fontWeightSemibold,
  },
});

interface ProvidersTabProps {
  apiKeys: ApiKeysSettings;
  onApiKeysChange: (settings: ApiKeysSettings) => void;
  advanced: AdvancedSettings;
  onAdvancedChange: (settings: AdvancedSettings) => void;
  onTestApiKey: (
    provider: string,
    apiKey: string
  ) => Promise<{ success: boolean; message: string; responseTime?: number }>;
}

interface TestResult {
  success: boolean;
  message: string;
  responseTime?: number;
  testing?: boolean;
}

interface ProviderPriority {
  id: string;
  name: string;
  type: 'LLM' | 'TTS' | 'Images';
  priority: number;
}

export function ProvidersTab({
  apiKeys,
  onApiKeysChange,
  advanced,
  onAdvancedChange,
  onTestApiKey,
}: ProvidersTabProps) {
  const styles = useStyles();
  const [testResults, setTestResults] = useState<Record<string, TestResult>>({});
  const [visibleKeys, setVisibleKeys] = useState<Record<string, boolean>>({});
  const [providerPriorities, setProviderPriorities] = useState<ProviderPriority[]>([
    { id: 'openai', name: 'OpenAI', type: 'LLM', priority: 1 },
    { id: 'anthropic', name: 'Anthropic', type: 'LLM', priority: 2 },
    { id: 'google', name: 'Google', type: 'LLM', priority: 3 },
    { id: 'elevenlabs', name: 'ElevenLabs', type: 'TTS', priority: 1 },
    { id: 'azure', name: 'Azure', type: 'TTS', priority: 2 },
    { id: 'stabilityai', name: 'Stability AI', type: 'Images', priority: 1 },
  ]);

  const updateApiKey = <K extends keyof ApiKeysSettings>(key: K, value: ApiKeysSettings[K]) => {
    onApiKeysChange({ ...apiKeys, [key]: value });
    setTestResults((prev) => ({ ...prev, [key]: { success: false, message: '', testing: false } }));
  };

  const toggleKeyVisibility = (key: string) => {
    setVisibleKeys((prev) => ({ ...prev, [key]: !prev[key] }));
  };

  const handleTestConnection = async (provider: string, apiKey: string) => {
    if (!apiKey.trim()) {
      setTestResults((prev) => ({
        ...prev,
        [provider]: { success: false, message: 'API key is required', testing: false },
      }));
      return;
    }

    setTestResults((prev) => ({
      ...prev,
      [provider]: { success: false, message: 'Testing...', testing: true },
    }));

    try {
      const result = await onTestApiKey(provider, apiKey);
      setTestResults((prev) => ({
        ...prev,
        [provider]: { ...result, testing: false },
      }));
    } catch (error) {
      setTestResults((prev) => ({
        ...prev,
        [provider]: {
          success: false,
          message: `Connection failed: ${error instanceof Error ? error.message : 'Unknown error'}`,
          testing: false,
        },
      }));
    }
  };

  const moveProvider = (index: number, direction: 'up' | 'down') => {
    const newPriorities = [...providerPriorities];
    const targetIndex = direction === 'up' ? index - 1 : index + 1;

    if (targetIndex < 0 || targetIndex >= newPriorities.length) return;

    [newPriorities[index], newPriorities[targetIndex]] = [
      newPriorities[targetIndex],
      newPriorities[index],
    ];

    newPriorities.forEach((p, i) => {
      p.priority = i + 1;
    });

    setProviderPriorities(newPriorities);
  };

  const renderApiKeyField = (
    key: keyof ApiKeysSettings,
    label: string,
    hint: string,
    docsUrl?: string
  ) => {
    const value = apiKeys[key];
    const isVisible = visibleKeys[key];
    const testResult = testResults[key];

    return (
      <Field
        label={label}
        hint={
          docsUrl ? (
            <span>
              {hint}{' '}
              <a href={docsUrl} target="_blank" rel="noopener noreferrer">
                Get API Key →
              </a>
            </span>
          ) : (
            hint
          )
        }
      >
        <div className={styles.inputWithButton}>
          <Input
            style={{ flex: 1 }}
            type={isVisible ? 'text' : 'password'}
            value={value}
            onChange={(e) => updateApiKey(key, e.target.value)}
            placeholder="sk-..."
          />
          <Button
            appearance="subtle"
            icon={isVisible ? <EyeOff24Regular /> : <Eye24Regular />}
            onClick={() => toggleKeyVisibility(key)}
          />
          <Button
            appearance="secondary"
            onClick={() => handleTestConnection(key, value)}
            disabled={!value.trim() || testResult?.testing}
          >
            {testResult?.testing ? <Spinner size="tiny" /> : 'Test'}
          </Button>
        </div>
        {testResult && !testResult.testing && (
          <div className={styles.testResult}>
            {testResult.success ? (
              <CheckmarkCircle24Filled style={{ color: tokens.colorPaletteGreenForeground1 }} />
            ) : (
              <DismissCircle24Filled style={{ color: tokens.colorPaletteRedForeground1 }} />
            )}
            <Text
              size={200}
              style={{
                color: testResult.success
                  ? tokens.colorPaletteGreenForeground1
                  : tokens.colorPaletteRedForeground1,
              }}
            >
              {testResult.message}
              {testResult.responseTime && ` (${testResult.responseTime}ms)`}
            </Text>
          </div>
        )}
      </Field>
    );
  };

  return (
    <Card className={styles.section}>
      <Title2>Providers</Title2>
      <Text size={200} style={{ marginBottom: tokens.spacingVerticalL }}>
        Configure API keys, provider priorities, and connection settings
      </Text>

      <div className={styles.form}>
        <div className={styles.subsection}>
          <Title3>API Keys</Title3>
          <div className={styles.infoBox}>
            <Text size={200}>
              🔒 <strong>Security:</strong> API keys are stored encrypted and never leave your
              device. Test connections verify keys without storing them on our servers.
            </Text>
          </div>

          <OpenAIProviderConfig
            apiKey={apiKeys.openAI}
            onApiKeyChange={(newKey) => updateApiKey('openAI', newKey)}
          />

          <Divider />

          <div style={{ marginTop: tokens.spacingVerticalL }}>
            <Title3>Ollama (Local LLM)</Title3>
            <Text size={200} style={{ marginBottom: tokens.spacingVerticalM }}>
              Run AI models locally on your machine without API keys
            </Text>
            <OllamaProviderConfig
              selectedModel={advanced.ollamaModel}
              onModelChange={(model) => onAdvancedChange({ ...advanced, ollamaModel: model })}
            />
          </div>

          <Divider />

          {renderApiKeyField(
            'anthropic',
            'Anthropic API Key',
            'For Claude models',
            'https://console.anthropic.com/settings/keys'
          )}

          {renderApiKeyField(
            'google',
            'Google API Key',
            'For Gemini models',
            'https://makersuite.google.com/app/apikey'
          )}

          {renderApiKeyField(
            'elevenLabs',
            'ElevenLabs API Key',
            'For premium text-to-speech',
            'https://elevenlabs.io/app/settings/api-keys'
          )}

          {renderApiKeyField(
            'stabilityAI',
            'Stability AI API Key',
            'For image generation',
            'https://platform.stability.ai/account/keys'
          )}

          {renderApiKeyField(
            'azure',
            'Azure API Key',
            'For Azure TTS services',
            'https://portal.azure.com'
          )}

          {renderApiKeyField(
            'pexels',
            'Pexels API Key',
            'For stock images (free)',
            'https://www.pexels.com/api/'
          )}

          {renderApiKeyField(
            'pixabay',
            'Pixabay API Key',
            'For stock images (free)',
            'https://pixabay.com/api/docs/'
          )}

          {renderApiKeyField(
            'unsplash',
            'Unsplash API Key',
            'For stock images (free)',
            'https://unsplash.com/developers'
          )}
        </div>

        <Divider />

        <div className={styles.subsection}>
          <Title3>Provider Priorities</Title3>
          <div className={styles.infoBox}>
            <Text size={200}>
              Drag to reorder providers. Higher priority providers are tried first. If a provider
              fails, the next one is automatically used.
            </Text>
          </div>

          <div className={styles.providerList}>
            {providerPriorities.map((provider, index) => (
              <div key={provider.id} className={styles.providerItem}>
                <div
                  style={{ display: 'flex', alignItems: 'center', gap: tokens.spacingHorizontalM }}
                >
                  <ArrowSort24Regular style={{ cursor: 'grab' }} />
                  <div>
                    <Text weight="semibold">{provider.name}</Text>
                    <Text size={200} style={{ color: tokens.colorNeutralForeground3 }}>
                      {provider.type}
                    </Text>
                  </div>
                </div>
                <div
                  style={{ display: 'flex', alignItems: 'center', gap: tokens.spacingHorizontalS }}
                >
                  <span className={styles.priorityBadge}>#{provider.priority}</span>
                  <div>
                    <Button
                      size="small"
                      appearance="subtle"
                      onClick={() => moveProvider(index, 'up')}
                      disabled={index === 0}
                    >
                      ↑
                    </Button>
                    <Button
                      size="small"
                      appearance="subtle"
                      onClick={() => moveProvider(index, 'down')}
                      disabled={index === providerPriorities.length - 1}
                    >
                      ↓
                    </Button>
                  </div>
                </div>
              </div>
            ))}
          </div>
        </div>

        <Divider />

        <div className={styles.subsection}>
          <Title3>Cost Limits and Quotas</Title3>
          <div className={styles.infoBox}>
            <Text size={200}>
              💡 <strong>Coming soon:</strong> Set monthly spending limits and usage quotas per
              provider to control costs.
            </Text>
          </div>
        </div>
      </div>
    </Card>
  );
}
