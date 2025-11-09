import {
  makeStyles,
  tokens,
  Text,
  Button,
  Input,
  Field,
  Spinner,
  Dropdown,
  Option,
  Link,
  MessageBar,
  MessageBarBody,
  MessageBarTitle,
} from '@fluentui/react-components';
import {
  CheckmarkCircle24Filled,
  DismissCircle24Filled,
  Eye24Regular,
  EyeOff24Regular,
  ArrowClockwise24Regular,
  Info24Regular,
} from '@fluentui/react-icons';
import { useState, useCallback, useEffect } from 'react';
import { settingsService } from '../../services/settingsService';

const useStyles = makeStyles({
  container: {
    display: 'flex',
    flexDirection: 'column',
    gap: tokens.spacingVerticalM,
  },
  inputWithButtons: {
    display: 'flex',
    gap: tokens.spacingHorizontalS,
    alignItems: 'flex-start',
    width: '100%',
  },
  inputContainer: {
    flex: 1,
    minWidth: '200px',
  },
  buttonGroup: {
    display: 'flex',
    gap: tokens.spacingHorizontalXS,
  },
  statusMessage: {
    display: 'flex',
    alignItems: 'center',
    gap: tokens.spacingHorizontalXS,
    marginTop: tokens.spacingVerticalXS,
  },
  maskedKey: {
    fontFamily: 'monospace',
    fontSize: tokens.fontSizeBase300,
    color: tokens.colorNeutralForeground3,
  },
  helpLink: {
    fontSize: tokens.fontSizeBase200,
  },
  modelsDropdown: {
    marginTop: tokens.spacingVerticalS,
    width: '100%',
  },
  testGenerationResult: {
    marginTop: tokens.spacingVerticalS,
    padding: tokens.spacingVerticalM,
    backgroundColor: tokens.colorNeutralBackground3,
    borderRadius: tokens.borderRadiusMedium,
  },
  warningBox: {
    marginTop: tokens.spacingVerticalS,
  },
});

interface OpenAIProviderConfigProps {
  apiKey: string;
  onApiKeyChange: (apiKey: string) => void;
}

interface ValidationState {
  isValid: boolean;
  message: string;
  isValidating: boolean;
  responseTime?: number;
  hasAttempted: boolean;
}

interface ModelsState {
  models: string[];
  isLoading: boolean;
  error?: string;
  selectedModel?: string;
}

interface GenerationTestState {
  isLoading: boolean;
  success?: boolean;
  generatedText?: string;
  responseTime?: number;
  error?: string;
}

export function OpenAIProviderConfig({ apiKey, onApiKeyChange }: OpenAIProviderConfigProps) {
  const styles = useStyles();
  const [showKey, setShowKey] = useState(false);
  const [validationState, setValidationState] = useState<ValidationState>({
    isValid: false,
    message: '',
    isValidating: false,
    hasAttempted: false,
  });
  const [modelsState, setModelsState] = useState<ModelsState>({
    models: [],
    isLoading: false,
  });
  const [generationTest, setGenerationTest] = useState<GenerationTestState>({
    isLoading: false,
  });
  const [retryCount, setRetryCount] = useState(0);

  const maskedKey = useCallback((key: string): string => {
    if (!key) return '';
    if (key.length <= 4) return '****';
    return `${key.substring(0, 3)}...${key.substring(key.length - 4)}`;
  }, []);

  const handleValidation = useCallback(
    async (isRetry = false) => {
      if (!apiKey.trim()) {
        setValidationState({
          isValid: false,
          message: 'API key is required',
          isValidating: false,
          hasAttempted: true,
        });
        return;
      }

      if (isRetry) {
        const delay = Math.min(1000 * Math.pow(2, retryCount), 10000);
        await new Promise((resolve) => setTimeout(resolve, delay));
      }

      setValidationState((prev) => ({
        ...prev,
        isValidating: true,
        hasAttempted: true,
      }));

      try {
        const result = await settingsService.testApiKey('openai', apiKey);

        setValidationState({
          isValid: result.success,
          message: result.message,
          isValidating: false,
          responseTime: result.responseTimeMs,
          hasAttempted: true,
        });

        if (result.success) {
          setRetryCount(0);
          handleFetchModels();
        } else if (isRetry) {
          setRetryCount((prev) => prev + 1);
        }
      } catch (error: unknown) {
        setValidationState({
          isValid: false,
          message: `Validation failed: ${error instanceof Error ? error.message : String(error)}`,
          isValidating: false,
          hasAttempted: true,
        });
      }
    },
    [apiKey, retryCount]
  );

  const handleFetchModels = useCallback(async () => {
    if (!apiKey.trim()) return;

    setModelsState({
      models: [],
      isLoading: true,
    });

    try {
      const result = await settingsService.getOpenAIModels(apiKey);

      if (result.success && result.models) {
        const sortedModels = [...result.models].sort();
        const defaultModel = sortedModels.find((m) => m.includes('gpt-4o-mini')) || sortedModels[0];

        setModelsState({
          models: sortedModels,
          isLoading: false,
          selectedModel: defaultModel,
        });
      } else {
        setModelsState({
          models: [],
          isLoading: false,
          error: result.message || 'Failed to fetch models',
        });
      }
    } catch (error: unknown) {
      setModelsState({
        models: [],
        isLoading: false,
        error: `Error: ${error instanceof Error ? error.message : String(error)}`,
      });
    }
  }, [apiKey]);

  const handleTestGeneration = useCallback(async () => {
    if (!modelsState.selectedModel) {
      return;
    }

    setGenerationTest({
      isLoading: true,
    });

    try {
      const result = await settingsService.testOpenAIGeneration(apiKey, modelsState.selectedModel);

      setGenerationTest({
        isLoading: false,
        success: result.success,
        generatedText: result.generatedText,
        responseTime: result.responseTimeMs,
        error: result.message,
      });
    } catch (error: unknown) {
      setGenerationTest({
        isLoading: false,
        success: false,
        error: `Error: ${error instanceof Error ? error.message : String(error)}`,
      });
    }
  }, [apiKey, modelsState.selectedModel]);

  const handleKeyChange = useCallback(
    (newKey: string) => {
      const trimmedKey = newKey.trim();
      onApiKeyChange(trimmedKey);
      setValidationState({
        isValid: false,
        message: '',
        isValidating: false,
        hasAttempted: false,
      });
      setModelsState({
        models: [],
        isLoading: false,
      });
      setGenerationTest({
        isLoading: false,
      });
      setRetryCount(0);
    },
    [onApiKeyChange]
  );

  useEffect(() => {
    if (validationState.isValid && !modelsState.models.length && !modelsState.isLoading) {
      handleFetchModels();
    }
  }, [
    validationState.isValid,
    modelsState.models.length,
    modelsState.isLoading,
    handleFetchModels,
  ]);

  return (
    <div className={styles.container}>
      <Field
        label="OpenAI API Key"
        hint={
          <span className={styles.helpLink}>
            For GPT-4, GPT-3.5 models.{' '}
            <Link
              href="https://platform.openai.com/api-keys"
              target="_blank"
              rel="noopener noreferrer"
            >
              Get API Key →
            </Link>
          </span>
        }
      >
        <div className={styles.inputWithButtons}>
          <div className={styles.inputContainer}>
            <Input
              type={showKey ? 'text' : 'password'}
              value={apiKey}
              onChange={(e) => handleKeyChange(e.target.value)}
              placeholder="sk-..."
              disabled={validationState.isValidating}
            />
          </div>
          <div className={styles.buttonGroup}>
            <Button
              appearance="subtle"
              icon={showKey ? <EyeOff24Regular /> : <Eye24Regular />}
              onClick={() => setShowKey(!showKey)}
              disabled={validationState.isValidating}
            />
            <Button
              appearance="secondary"
              onClick={() => handleValidation(false)}
              disabled={!apiKey.trim() || validationState.isValidating}
            >
              {validationState.isValidating ? <Spinner size="tiny" /> : 'Validate'}
            </Button>
            {validationState.hasAttempted && !validationState.isValid && retryCount < 3 && (
              <Button
                appearance="subtle"
                icon={<ArrowClockwise24Regular />}
                onClick={() => handleValidation(true)}
                disabled={validationState.isValidating}
              >
                Retry
              </Button>
            )}
          </div>
        </div>

        {validationState.hasAttempted && !validationState.isValidating && (
          <div className={styles.statusMessage}>
            {validationState.isValid ? (
              <CheckmarkCircle24Filled style={{ color: tokens.colorPaletteGreenForeground1 }} />
            ) : (
              <DismissCircle24Filled style={{ color: tokens.colorPaletteRedForeground1 }} />
            )}
            <Text
              size={200}
              style={{
                color: validationState.isValid
                  ? tokens.colorPaletteGreenForeground1
                  : tokens.colorPaletteRedForeground1,
              }}
            >
              {validationState.message}
              {validationState.responseTime && ` (${validationState.responseTime}ms)`}
            </Text>
          </div>
        )}

        {validationState.isValid && apiKey && (
          <div className={styles.statusMessage}>
            <Text size={200}>
              Saved as: <span className={styles.maskedKey}>{maskedKey(apiKey)}</span>
            </Text>
          </div>
        )}
      </Field>

      {!validationState.isValid && validationState.hasAttempted && retryCount >= 3 && (
        <MessageBar intent="warning" className={styles.warningBox}>
          <MessageBarBody>
            <MessageBarTitle>Having trouble validating your key?</MessageBarTitle>
            Common issues:
            <ul
              style={{
                margin: `${tokens.spacingVerticalXS} 0`,
                paddingLeft: tokens.spacingHorizontalL,
              }}
            >
              <li>Check that the key is copied correctly without extra spaces or newlines</li>
              <li>
                Verify your OpenAI account has billing enabled at
                platform.openai.com/settings/organization/billing
              </li>
              <li>Ensure the API key has not been revoked</li>
              <li>Check your internet connection</li>
            </ul>
            <Link
              href="https://platform.openai.com/docs/api-reference/authentication"
              target="_blank"
            >
              View OpenAI API documentation →
            </Link>
          </MessageBarBody>
        </MessageBar>
      )}

      {validationState.isValid && (
        <>
          {modelsState.isLoading && (
            <div className={styles.statusMessage}>
              <Spinner size="tiny" />
              <Text size={200}>Loading available models...</Text>
            </div>
          )}

          {modelsState.error && (
            <div className={styles.statusMessage}>
              <Info24Regular style={{ color: tokens.colorPaletteYellowForeground1 }} />
              <Text size={200} style={{ color: tokens.colorPaletteYellowForeground1 }}>
                {modelsState.error}
              </Text>
            </div>
          )}

          {modelsState.models.length > 0 && (
            <div className={styles.modelsDropdown}>
              <Field label="Test with Model" hint="Select a model to test script generation">
                <div style={{ display: 'flex', gap: tokens.spacingHorizontalS }}>
                  <Dropdown
                    value={modelsState.selectedModel || ''}
                    onOptionSelect={(_e, data) => {
                      setModelsState((prev) => ({
                        ...prev,
                        selectedModel: data.optionValue as string,
                      }));
                    }}
                    style={{ flex: 1 }}
                  >
                    {modelsState.models.map((model) => (
                      <Option key={model} value={model}>
                        {model}
                      </Option>
                    ))}
                  </Dropdown>
                  <Button
                    appearance="secondary"
                    onClick={handleTestGeneration}
                    disabled={!modelsState.selectedModel || generationTest.isLoading}
                  >
                    {generationTest.isLoading ? <Spinner size="tiny" /> : 'Test Generation'}
                  </Button>
                </div>
              </Field>

              {generationTest.success !== undefined && !generationTest.isLoading && (
                <div className={styles.testGenerationResult}>
                  <div className={styles.statusMessage}>
                    {generationTest.success ? (
                      <CheckmarkCircle24Filled
                        style={{ color: tokens.colorPaletteGreenForeground1 }}
                      />
                    ) : (
                      <DismissCircle24Filled style={{ color: tokens.colorPaletteRedForeground1 }} />
                    )}
                    <Text
                      weight="semibold"
                      style={{
                        color: generationTest.success
                          ? tokens.colorPaletteGreenForeground1
                          : tokens.colorPaletteRedForeground1,
                      }}
                    >
                      {generationTest.success
                        ? 'Generation test successful!'
                        : 'Generation test failed'}
                    </Text>
                  </div>

                  {generationTest.success && generationTest.generatedText && (
                    <>
                      <Text size={200} style={{ marginTop: tokens.spacingVerticalS }}>
                        Generated text: &quot;{generationTest.generatedText}&quot;
                      </Text>
                      {generationTest.responseTime && (
                        <Text size={200} style={{ color: tokens.colorNeutralForeground3 }}>
                          Response time: {generationTest.responseTime}ms
                        </Text>
                      )}
                    </>
                  )}

                  {generationTest.error && (
                    <Text
                      size={200}
                      style={{
                        marginTop: tokens.spacingVerticalS,
                        color: tokens.colorPaletteRedForeground1,
                      }}
                    >
                      {generationTest.error}
                    </Text>
                  )}
                </div>
              )}
            </div>
          )}
        </>
      )}
    </div>
  );
}
