import {
  makeStyles,
  tokens,
  Text,
  Badge,
  Popover,
  PopoverTrigger,
  PopoverSurface,
  Button,
  Title3,
  Divider,
} from '@fluentui/react-components';
import { Money24Regular, Warning24Regular } from '@fluentui/react-icons';
import { useMemo } from 'react';
import type { FC } from 'react';
import type { WizardData, CostBreakdown } from './types';

const useStyles = makeStyles({
  trigger: {
    display: 'flex',
    alignItems: 'center',
    gap: tokens.spacingHorizontalS,
  },
  content: {
    minWidth: '320px',
    padding: tokens.spacingVerticalL,
    display: 'flex',
    flexDirection: 'column',
    gap: tokens.spacingVerticalM,
  },
  totalRow: {
    display: 'flex',
    justifyContent: 'space-between',
    alignItems: 'center',
    paddingTop: tokens.spacingVerticalM,
    paddingBottom: tokens.spacingVerticalM,
  },
  breakdownRow: {
    display: 'flex',
    justifyContent: 'space-between',
    alignItems: 'center',
    paddingTop: tokens.spacingVerticalS,
    paddingBottom: tokens.spacingVerticalS,
  },
  breakdownLabel: {
    display: 'flex',
    flexDirection: 'column',
    gap: tokens.spacingVerticalXS,
  },
  warning: {
    display: 'flex',
    alignItems: 'center',
    gap: tokens.spacingHorizontalS,
    padding: tokens.spacingVerticalM,
    backgroundColor: tokens.colorPaletteYellowBackground2,
    borderRadius: tokens.borderRadiusMedium,
    marginTop: tokens.spacingVerticalM,
  },
  optimization: {
    display: 'flex',
    flexDirection: 'column',
    gap: tokens.spacingVerticalS,
    padding: tokens.spacingVerticalM,
    backgroundColor: tokens.colorNeutralBackground2,
    borderRadius: tokens.borderRadiusMedium,
    marginTop: tokens.spacingVerticalM,
  },
});

interface CostEstimatorProps {
  wizardData: WizardData;
  budgetLimit?: number;
}

const COST_RATES = {
  llm: {
    OpenAI: { inputTokens: 0.0015 / 1000, outputTokens: 0.002 / 1000 },
    Claude: { inputTokens: 0.008 / 1000, outputTokens: 0.024 / 1000 },
    Ollama: { inputTokens: 0, outputTokens: 0 },
  },
  tts: {
    ElevenLabs: 0.00018,
    PlayHT: 0.00012,
    Windows: 0,
    Piper: 0,
  },
  images: {
    StableDiffusion: 0,
    DALLE: 0.02,
    Midjourney: 0.04,
    Stock: 0,
  },
};

export const CostEstimator: FC<CostEstimatorProps> = ({ wizardData, budgetLimit = 5.0 }) => {
  const styles = useStyles();

  const costBreakdown = useMemo((): CostBreakdown => {
    const breakdown: CostBreakdown['breakdown'] = [];

    const scriptLength = wizardData.script.content.length || wizardData.brief.topic.length * 50;
    const estimatedTokens = Math.ceil(scriptLength / 4);
    const sceneCount = wizardData.script.scenes.length || Math.ceil(wizardData.brief.duration / 10);
    const audioCharacters = scriptLength || 500;

    const llmCost = (estimatedTokens * 0.002) / 1000;
    if (llmCost > 0) {
      breakdown.push({
        provider: 'LLM',
        service: 'Script Generation',
        units: estimatedTokens,
        costPerUnit: 0.002 / 1000,
        subtotal: llmCost,
      });
    }

    const ttsRate = COST_RATES.tts[wizardData.style.voiceProvider] || 0;
    const ttsCost = audioCharacters * ttsRate;
    if (ttsCost > 0) {
      breakdown.push({
        provider: wizardData.style.voiceProvider,
        service: 'Text-to-Speech',
        units: audioCharacters,
        costPerUnit: ttsRate,
        subtotal: ttsCost,
      });
    }

    const imageGenerationCost = sceneCount * 0.02;
    if (imageGenerationCost > 0) {
      breakdown.push({
        provider: 'Image Generation',
        service: 'Scene Visuals',
        units: sceneCount,
        costPerUnit: 0.02,
        subtotal: imageGenerationCost,
      });
    }

    const totalCost = llmCost + ttsCost + imageGenerationCost;

    return {
      llmCost,
      ttsCost,
      imageGenerationCost,
      totalCost,
      breakdown,
    };
  }, [wizardData]);

  const exceedsBudget = costBreakdown.totalCost > budgetLimit;

  const optimizationSuggestions = useMemo(() => {
    const suggestions: string[] = [];
    if (costBreakdown.ttsCost > 0) {
      suggestions.push('Use Windows TTS (free) instead of premium voices');
    }
    if (costBreakdown.imageGenerationCost > 0) {
      suggestions.push('Use stock images instead of AI generation');
    }
    if (costBreakdown.llmCost > 0) {
      suggestions.push('Use Ollama (free, local) for script generation');
    }
    return suggestions;
  }, [costBreakdown]);

  return (
    <Popover trapFocus positioning="below-end">
      <PopoverTrigger disableButtonEnhancement>
        <Button appearance="subtle" icon={<Money24Regular />} className={styles.trigger}>
          <Text weight="semibold">${costBreakdown.totalCost.toFixed(3)}</Text>
          {exceedsBudget && (
            <Badge appearance="filled" color="danger" shape="rounded">
              Over Budget
            </Badge>
          )}
        </Button>
      </PopoverTrigger>

      <PopoverSurface className={styles.content}>
        <Title3>Cost Estimate</Title3>
        <Divider />

        <div style={{ display: 'flex', flexDirection: 'column', gap: tokens.spacingVerticalS }}>
          {costBreakdown.breakdown.map((item, index) => (
            <div key={index} className={styles.breakdownRow}>
              <div className={styles.breakdownLabel}>
                <Text weight="semibold" size={300}>
                  {item.provider}
                </Text>
                <Text size={200} style={{ color: tokens.colorNeutralForeground3 }}>
                  {item.units}{' '}
                  {item.service.includes('Token')
                    ? 'tokens'
                    : item.service.includes('TTS')
                      ? 'characters'
                      : 'images'}
                </Text>
              </div>
              <Text weight="semibold">${item.subtotal.toFixed(3)}</Text>
            </div>
          ))}
        </div>

        <Divider />

        <div className={styles.totalRow}>
          <Text weight="bold" size={400}>
            Total Estimated Cost
          </Text>
          <Text weight="bold" size={400}>
            ${costBreakdown.totalCost.toFixed(3)}
          </Text>
        </div>

        <Text size={200} style={{ color: tokens.colorNeutralForeground3 }}>
          Budget limit: ${budgetLimit.toFixed(2)}
        </Text>

        {exceedsBudget && (
          <div className={styles.warning}>
            <Warning24Regular style={{ color: tokens.colorPaletteYellowForeground1 }} />
            <div>
              <Text weight="semibold" size={300}>
                Exceeds Budget
              </Text>
              <Text size={200}>
                This configuration will cost ${(costBreakdown.totalCost - budgetLimit).toFixed(3)}{' '}
                more than your budget.
              </Text>
            </div>
          </div>
        )}

        {exceedsBudget && optimizationSuggestions.length > 0 && (
          <div className={styles.optimization}>
            <Text weight="semibold" size={300}>
              Cost Optimizations
            </Text>
            {optimizationSuggestions.map((suggestion, index) => (
              <Text key={index} size={200} style={{ marginLeft: tokens.spacingHorizontalM }}>
                • {suggestion}
              </Text>
            ))}
          </div>
        )}
      </PopoverSurface>
    </Popover>
  );
};
