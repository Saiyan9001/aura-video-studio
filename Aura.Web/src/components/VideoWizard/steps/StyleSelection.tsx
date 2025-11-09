import {
  makeStyles,
  tokens,
  Title2,
  Title3,
  Text,
  Dropdown,
  Option,
  Field,
  Slider,
  Card,
  Badge,
  Spinner,
} from '@fluentui/react-components';
import { CheckmarkCircle24Regular, ErrorCircle24Regular } from '@fluentui/react-icons';
import { useEffect, useState, useCallback } from 'react';
import type { FC } from 'react';
import type { StyleData, BriefData, StepValidation } from '../types';
import { getVisualsClient } from '@/api/visualsClient';
import type { VisualProvider } from '@/api/visualsClient';

const useStyles = makeStyles({
  container: {
    display: 'flex',
    flexDirection: 'column',
    gap: tokens.spacingVerticalXL,
  },
  section: {
    display: 'flex',
    flexDirection: 'column',
    gap: tokens.spacingVerticalM,
  },
  grid: {
    display: 'grid',
    gridTemplateColumns: 'repeat(auto-fit, minmax(300px, 1fr))',
    gap: tokens.spacingHorizontalL,
  },
  providerCard: {
    padding: tokens.spacingVerticalL,
    cursor: 'pointer',
    transition: 'all 0.2s ease',
    border: `1px solid ${tokens.colorNeutralStroke2}`,
    ':hover': {
      transform: 'translateY(-2px)',
      boxShadow: tokens.shadow8,
    },
  },
  selectedCard: {
    border: `2px solid ${tokens.colorBrandStroke1}`,
  },
  providerHeader: {
    display: 'flex',
    justifyContent: 'space-between',
    alignItems: 'center',
    marginBottom: tokens.spacingVerticalS,
  },
  providerDetails: {
    display: 'flex',
    flexDirection: 'column',
    gap: tokens.spacingVerticalXS,
  },
  formRow: {
    display: 'flex',
    gap: tokens.spacingHorizontalL,
    flexWrap: 'wrap',
  },
  formField: {
    flex: 1,
    minWidth: '200px',
  },
  loadingContainer: {
    display: 'flex',
    justifyContent: 'center',
    alignItems: 'center',
    padding: tokens.spacingVerticalXXL,
  },
});

interface StyleSelectionProps {
  data: StyleData;
  briefData: BriefData;
  advancedMode: boolean;
  onChange: (data: StyleData) => void;
  onValidationChange: (validation: StepValidation) => void;
}

export const StyleSelection: FC<StyleSelectionProps> = ({
  data,
  advancedMode,
  onChange,
  onValidationChange,
}) => {
  const styles = useStyles();
  const visualsClient = getVisualsClient();
  const [providers, setProviders] = useState<VisualProvider[]>([]);
  const [availableStyles, setAvailableStyles] = useState<string[]>([]);
  const [loading, setLoading] = useState(true);

  useEffect(() => {
    loadProviders();
    loadStyles();
  }, []);

  const loadProviders = useCallback(async () => {
    try {
      const response = await visualsClient.getProviders();
      setProviders(response.providers);

      if (!data.imageProvider) {
        const availableProvider = response.providers.find((p) => p.isAvailable);
        if (availableProvider) {
          onChange({
            ...data,
            imageProvider: availableProvider.name,
          });
        }
      }
    } catch (error) {
      console.error('Failed to load providers:', error);
    } finally {
      setLoading(false);
    }
  }, [visualsClient, data, onChange]);

  const loadStyles = useCallback(async () => {
    try {
      const response = await visualsClient.getStyles();
      setAvailableStyles(response.allStyles);

      if (!data.imageStyle && response.allStyles.length > 0) {
        onChange({
          ...data,
          imageStyle: response.allStyles[0],
        });
      }
    } catch (error) {
      console.error('Failed to load styles:', error);
      setAvailableStyles(['photorealistic', 'artistic', 'cinematic', 'minimalist']);
    }
  }, [visualsClient, data, onChange]);

  useEffect(() => {
    const isValid = !!data.voiceProvider && !!data.visualStyle && !!data.imageProvider;

    onValidationChange({
      isValid,
      errors: isValid ? [] : ['Please select voice and image providers'],
    });
  }, [data, onValidationChange]);

  const handleProviderSelect = useCallback(
    (providerName: string) => {
      onChange({
        ...data,
        imageProvider: providerName,
      });
    },
    [data, onChange]
  );

  const handleStyleChange = useCallback(
    (field: keyof StyleData, value: string | number | boolean) => {
      onChange({
        ...data,
        [field]: value,
      });
    },
    [data, onChange]
  );

  if (loading) {
    return (
      <div className={styles.container}>
        <Title2>Style Selection</Title2>
        <div className={styles.loadingContainer}>
          <Spinner size="large" label="Loading providers..." />
        </div>
      </div>
    );
  }

  return (
    <div className={styles.container}>
      <div className={styles.section}>
        <Title2>Style Selection</Title2>
        <Text>Configure voice, visual style, and music preferences for your video.</Text>
      </div>

      <div className={styles.section}>
        <Title3>Voice Settings</Title3>
        <div className={styles.formRow}>
          <Field label="Voice Provider" className={styles.formField}>
            <Dropdown
              value={data.voiceProvider}
              selectedOptions={[data.voiceProvider]}
              onOptionSelect={(_, option) => {
                if (option.optionValue) {
                  handleStyleChange('voiceProvider', option.optionValue as string);
                }
              }}
            >
              <Option value="ElevenLabs">ElevenLabs</Option>
              <Option value="PlayHT">PlayHT</Option>
              <Option value="Windows">Windows</Option>
              <Option value="Piper">Piper</Option>
            </Dropdown>
          </Field>

          <Field label="Voice Name" className={styles.formField}>
            <Dropdown
              placeholder="Select a voice"
              value={data.voiceName}
              selectedOptions={data.voiceName ? [data.voiceName] : []}
              onOptionSelect={(_, option) => {
                if (option.optionValue) {
                  handleStyleChange('voiceName', option.optionValue as string);
                }
              }}
            >
              <Option value="default">Default</Option>
              <Option value="professional">Professional</Option>
              <Option value="friendly">Friendly</Option>
              <Option value="energetic">Energetic</Option>
            </Dropdown>
          </Field>
        </div>
      </div>

      <div className={styles.section}>
        <Title3>Image Generation Provider</Title3>
        <Text>Select the AI provider for generating scene images</Text>

        <div className={styles.grid}>
          {providers.map((provider) => (
            <Card
              key={provider.name}
              className={`${styles.providerCard} ${
                data.imageProvider === provider.name ? styles.selectedCard : ''
              }`}
              onClick={() => provider.isAvailable && handleProviderSelect(provider.name)}
              style={{
                opacity: provider.isAvailable ? 1 : 0.5,
                cursor: provider.isAvailable ? 'pointer' : 'not-allowed',
              }}
            >
              <div className={styles.providerHeader}>
                <Title3>{provider.name}</Title3>
                {data.imageProvider === provider.name && (
                  <Badge appearance="filled" color="success">
                    Selected
                  </Badge>
                )}
              </div>

              <div className={styles.providerDetails}>
                <div
                  style={{ display: 'flex', alignItems: 'center', gap: tokens.spacingHorizontalXS }}
                >
                  {provider.isAvailable ? (
                    <CheckmarkCircle24Regular
                      style={{ color: tokens.colorPaletteGreenForeground1, fontSize: '16px' }}
                    />
                  ) : (
                    <ErrorCircle24Regular
                      style={{ color: tokens.colorPaletteRedForeground1, fontSize: '16px' }}
                    />
                  )}
                  <Text size={200}>{provider.isAvailable ? 'Available' : 'Not Available'}</Text>
                </div>

                {provider.capabilities && (
                  <>
                    <Text size={200} style={{ color: tokens.colorNeutralForeground3 }}>
                      {provider.capabilities.tier} •{' '}
                      {provider.capabilities.isFree
                        ? 'Free'
                        : `$${provider.capabilities.costPerImage}/image`}
                    </Text>
                    <Text size={200} style={{ color: tokens.colorNeutralForeground3 }}>
                      Max: {provider.capabilities.maxWidth}x{provider.capabilities.maxHeight}
                    </Text>
                    {provider.capabilities.supportedStyles.length > 0 && (
                      <Text size={200} style={{ color: tokens.colorNeutralForeground3 }}>
                        {provider.capabilities.supportedStyles.length} styles supported
                      </Text>
                    )}
                  </>
                )}
              </div>
            </Card>
          ))}
        </div>
      </div>

      <div className={styles.section}>
        <Title3>Visual Style Settings</Title3>
        <div className={styles.formRow}>
          <Field label="Visual Style" className={styles.formField}>
            <Dropdown
              value={data.visualStyle}
              selectedOptions={[data.visualStyle]}
              onOptionSelect={(_, option) => {
                if (option.optionValue) {
                  handleStyleChange('visualStyle', option.optionValue as string);
                }
              }}
            >
              <Option value="modern">Modern</Option>
              <Option value="minimal">Minimal</Option>
              <Option value="cinematic">Cinematic</Option>
              <Option value="playful">Playful</Option>
              <Option value="professional">Professional</Option>
            </Dropdown>
          </Field>

          {availableStyles.length > 0 && (
            <Field label="Image Style" className={styles.formField}>
              <Dropdown
                placeholder="Select image style"
                value={data.imageStyle}
                selectedOptions={data.imageStyle ? [data.imageStyle] : []}
                onOptionSelect={(_, option) => {
                  if (option.optionValue) {
                    handleStyleChange('imageStyle', option.optionValue as string);
                  }
                }}
              >
                {availableStyles.map((style) => (
                  <Option key={style} value={style}>
                    {style.charAt(0).toUpperCase() + style.slice(1)}
                  </Option>
                ))}
              </Dropdown>
            </Field>
          )}
        </div>

        {advancedMode && (
          <div className={styles.formRow}>
            <Field label="Aspect Ratio" className={styles.formField}>
              <Dropdown
                value={data.imageAspectRatio || '16:9'}
                selectedOptions={[data.imageAspectRatio || '16:9']}
                onOptionSelect={(_, option) => {
                  if (option.optionValue) {
                    handleStyleChange('imageAspectRatio', option.optionValue as string);
                  }
                }}
              >
                <Option value="16:9">16:9 (Widescreen)</Option>
                <Option value="9:16">9:16 (Portrait)</Option>
                <Option value="1:1">1:1 (Square)</Option>
                <Option value="4:3">4:3 (Standard)</Option>
              </Dropdown>
            </Field>

            <Field
              label={`Image Quality: ${data.imageQuality || 80}%`}
              className={styles.formField}
            >
              <Slider
                value={data.imageQuality || 80}
                min={50}
                max={100}
                step={10}
                onChange={(_, sliderData) => handleStyleChange('imageQuality', sliderData.value)}
              />
            </Field>
          </div>
        )}
      </div>

      <div className={styles.section}>
        <Title3>Music Settings</Title3>
        <div className={styles.formRow}>
          <Field label="Music Genre" className={styles.formField}>
            <Dropdown
              value={data.musicGenre}
              selectedOptions={[data.musicGenre]}
              onOptionSelect={(_, option) => {
                if (option.optionValue) {
                  handleStyleChange('musicGenre', option.optionValue as string);
                }
              }}
            >
              <Option value="ambient">Ambient</Option>
              <Option value="upbeat">Upbeat</Option>
              <Option value="dramatic">Dramatic</Option>
              <Option value="none">None</Option>
            </Dropdown>
          </Field>
        </div>
      </div>
    </div>
  );
};
