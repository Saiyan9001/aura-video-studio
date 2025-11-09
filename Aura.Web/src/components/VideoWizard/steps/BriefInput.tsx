import {
  makeStyles,
  tokens,
  Title2,
  Title3,
  Text,
  Field,
  Textarea,
  Dropdown,
  Option,
  Slider,
  Button,
  Badge,
  Card,
  Accordion,
  AccordionItem,
  AccordionHeader,
  AccordionPanel,
  Tooltip,
} from '@fluentui/react-components';
import {
  Lightbulb24Regular,
  Mic24Regular,
  ChevronDown24Regular,
  Info24Regular,
} from '@fluentui/react-icons';
import { useState, useEffect, useCallback } from 'react';
import type { FC } from 'react';
import type { BriefData, AdvancedData, StepValidation } from '../types';

const useStyles = makeStyles({
  container: {
    display: 'flex',
    flexDirection: 'column',
    gap: tokens.spacingVerticalXL,
  },
  header: {
    marginBottom: tokens.spacingVerticalM,
  },
  fieldGroup: {
    display: 'flex',
    flexDirection: 'column',
    gap: tokens.spacingVerticalL,
  },
  textareaContainer: {
    position: 'relative',
  },
  charCounter: {
    display: 'flex',
    justifyContent: 'space-between',
    alignItems: 'center',
    marginTop: tokens.spacingVerticalXS,
  },
  actionButtons: {
    display: 'flex',
    gap: tokens.spacingHorizontalM,
    marginTop: tokens.spacingVerticalM,
  },
  exampleCard: {
    padding: tokens.spacingVerticalL,
    backgroundColor: tokens.colorNeutralBackground2,
    borderRadius: tokens.borderRadiusMedium,
    cursor: 'pointer',
    transition: 'all 0.2s ease',
    ':hover': {
      backgroundColor: tokens.colorNeutralBackground3,
      transform: 'translateY(-2px)',
    },
  },
  exampleGrid: {
    display: 'grid',
    gridTemplateColumns: 'repeat(auto-fill, minmax(250px, 1fr))',
    gap: tokens.spacingHorizontalM,
    marginTop: tokens.spacingVerticalL,
  },
  advancedSection: {
    marginTop: tokens.spacingVerticalL,
  },
});

interface BriefInputProps {
  data: BriefData;
  advancedMode: boolean;
  advancedData: AdvancedData;
  onChange: (data: BriefData) => void;
  onAdvancedChange: (data: AdvancedData) => void;
  onValidationChange: (validation: StepValidation) => void;
}

const EXAMPLE_PROMPTS = [
  {
    videoType: 'educational' as const,
    title: 'Educational: AI Basics',
    prompt:
      'Introduction to Artificial Intelligence for beginners - explaining neural networks, machine learning, and real-world applications',
  },
  {
    videoType: 'marketing' as const,
    title: 'Marketing: Product Launch',
    prompt:
      'Exciting product launch announcement for our new eco-friendly water bottle - highlighting sustainability features and health benefits',
  },
  {
    videoType: 'social' as const,
    title: 'Social: Travel Tips',
    prompt: 'Top 10 travel tips for budget-conscious backpackers exploring Southeast Asia',
  },
  {
    videoType: 'story' as const,
    title: 'Story: Success Journey',
    prompt: 'A motivational story about overcoming challenges to achieve entrepreneurial success',
  },
];

const PLACEHOLDER_BY_TYPE = {
  educational: 'e.g., "How photosynthesis works - a step-by-step explanation for students"',
  marketing: 'e.g., "Introducing our revolutionary fitness app that helps you reach your goals"',
  social: 'e.g., "5 life hacks that will save you time every day"',
  story: 'e.g., "The inspiring journey from a small startup to a thriving business"',
  tutorial: 'e.g., "Complete beginners guide to using Adobe Photoshop"',
  explainer: 'e.g., "How blockchain technology is changing the financial industry"',
};

export const BriefInput: FC<BriefInputProps> = ({
  data,
  advancedMode,
  advancedData,
  onChange,
  onAdvancedChange,
  onValidationChange,
}) => {
  const styles = useStyles();
  const [isListening, setIsListening] = useState(false);

  const validateBrief = useCallback((briefData: BriefData): StepValidation => {
    const errors: string[] = [];

    if (!briefData.topic || briefData.topic.trim().length < 10) {
      errors.push('Topic must be at least 10 characters');
    }
    if (briefData.topic.length > 500) {
      errors.push('Topic must be no more than 500 characters');
    }
    if (!briefData.targetAudience || briefData.targetAudience.trim().length === 0) {
      errors.push('Target audience is required');
    }
    if (!briefData.keyMessage || briefData.keyMessage.trim().length === 0) {
      errors.push('Key message is required');
    }
    if (briefData.duration < 15 || briefData.duration > 600) {
      errors.push('Duration must be between 15 seconds and 10 minutes');
    }

    return {
      isValid: errors.length === 0,
      errors,
    };
  }, []);

  useEffect(() => {
    const validation = validateBrief(data);
    onValidationChange(validation);
  }, [data, validateBrief, onValidationChange]);

  const handleTopicChange = (value: string) => {
    onChange({ ...data, topic: value });
  };

  const handleInspireMe = () => {
    const randomExample = EXAMPLE_PROMPTS[Math.floor(Math.random() * EXAMPLE_PROMPTS.length)];
    onChange({
      ...data,
      topic: randomExample.prompt,
      videoType: randomExample.videoType,
    });
  };

  const handleVoiceInput = async () => {
    if (!('webkitSpeechRecognition' in window) && !('SpeechRecognition' in window)) {
      alert('Speech recognition is not supported in your browser. Please use Chrome or Edge.');
      return;
    }

    const SpeechRecognition =
      (
        window as {
          webkitSpeechRecognition?: new () => SpeechRecognitionInstance;
          SpeechRecognition?: new () => SpeechRecognitionInstance;
        }
      ).webkitSpeechRecognition ||
      (
        window as {
          webkitSpeechRecognition?: new () => SpeechRecognitionInstance;
          SpeechRecognition?: new () => SpeechRecognitionInstance;
        }
      ).SpeechRecognition;

    if (!SpeechRecognition) {
      alert('Speech recognition is not available');
      return;
    }

    const recognition = new SpeechRecognition();

    recognition.onresult = (event: SpeechRecognitionEvent) => {
      const transcript = event.results[0][0].transcript;
      onChange({ ...data, topic: data.topic + ' ' + transcript });
    };

    recognition.onerror = (event: SpeechRecognitionErrorEvent) => {
      console.error('Speech recognition error:', event.error);
      setIsListening(false);
    };

    recognition.onend = () => {
      setIsListening(false);
    };

    setIsListening(true);
    recognition.start();
  };

  const handleExampleClick = (example: (typeof EXAMPLE_PROMPTS)[0]) => {
    onChange({
      ...data,
      topic: example.prompt,
      videoType: example.videoType,
    });
  };

  const charCount = data.topic.length;
  const isOptimalLength = charCount >= 50 && charCount <= 500;
  const charCountColor =
    charCount < 10
      ? tokens.colorPaletteRedForeground1
      : charCount > 500
        ? tokens.colorPaletteRedForeground1
        : isOptimalLength
          ? tokens.colorPaletteGreenForeground1
          : tokens.colorNeutralForeground3;

  return (
    <div className={styles.container}>
      <div className={styles.header}>
        <Title2>What&apos;s your video about?</Title2>
        <Text
          size={300}
          style={{ marginTop: tokens.spacingVerticalS, color: tokens.colorNeutralForeground3 }}
        >
          Describe your video idea in detail. The more specific you are, the better the result will
          be.
        </Text>
      </div>

      <div className={styles.fieldGroup}>
        <Field label="Video Topic" required hint="Be specific about what you want to create">
          <div className={styles.textareaContainer}>
            <Textarea
              value={data.topic}
              onChange={(_, newData) => handleTopicChange(newData.value)}
              placeholder={PLACEHOLDER_BY_TYPE[data.videoType]}
              rows={6}
              resize="vertical"
              style={{ width: '100%' }}
            />
            <div className={styles.charCounter}>
              <Text size={200} style={{ color: charCountColor }}>
                {charCount} / 500 characters
                {isOptimalLength && (
                  <Badge
                    appearance="tint"
                    color="success"
                    style={{ marginLeft: tokens.spacingHorizontalS }}
                  >
                    Optimal length
                  </Badge>
                )}
              </Text>
            </div>
          </div>
        </Field>

        <div className={styles.actionButtons}>
          <Button appearance="secondary" icon={<Lightbulb24Regular />} onClick={handleInspireMe}>
            Inspire Me
          </Button>
          <Tooltip content="Use your microphone to dictate the topic" relationship="label">
            <Button
              appearance="secondary"
              icon={<Mic24Regular />}
              onClick={handleVoiceInput}
              disabled={isListening}
            >
              {isListening ? 'Listening...' : 'Voice Input'}
            </Button>
          </Tooltip>
        </div>

        <Field label="Video Type" required>
          <Dropdown
            value={data.videoType}
            onOptionSelect={(_, newData) =>
              onChange({ ...data, videoType: newData.optionValue as BriefData['videoType'] })
            }
          >
            <Option value="educational">Educational - Teach a concept or skill</Option>
            <Option value="marketing">Marketing - Promote a product or service</Option>
            <Option value="social">Social - Engage and entertain</Option>
            <Option value="story">Story - Tell a narrative</Option>
            <Option value="tutorial">Tutorial - Step-by-step guide</Option>
            <Option value="explainer">Explainer - Explain complex topics</Option>
          </Dropdown>
        </Field>

        <Field label="Target Audience" required hint="Who is this video for?">
          <Textarea
            value={data.targetAudience}
            onChange={(_, newData) => onChange({ ...data, targetAudience: newData.value })}
            placeholder="e.g., College students studying biology, Small business owners, Tech enthusiasts"
            rows={2}
          />
        </Field>

        <Field label="Key Message" required hint="What's the main takeaway?">
          <Textarea
            value={data.keyMessage}
            onChange={(_, newData) => onChange({ ...data, keyMessage: newData.value })}
            placeholder="e.g., Understanding photosynthesis is key to understanding all life on Earth"
            rows={2}
          />
        </Field>

        <Field
          label={`Target Duration: ${data.duration} seconds`}
          hint="Shorter videos typically have higher engagement"
        >
          <Slider
            min={15}
            max={600}
            step={15}
            value={data.duration}
            onChange={(_, newData) => onChange({ ...data, duration: newData.value })}
          />
          <div
            style={{
              display: 'flex',
              justifyContent: 'space-between',
              marginTop: tokens.spacingVerticalXS,
            }}
          >
            <Text size={200}>15s</Text>
            <Text size={200}>10min</Text>
          </div>
        </Field>
      </div>

      {advancedMode && (
        <div className={styles.advancedSection}>
          <Accordion collapsible>
            <AccordionItem value="advanced">
              <AccordionHeader icon={<ChevronDown24Regular />} expandIconPosition="end">
                <Title3>Advanced Options</Title3>
              </AccordionHeader>
              <AccordionPanel>
                <div className={styles.fieldGroup}>
                  <Field
                    label={
                      <div
                        style={{
                          display: 'flex',
                          alignItems: 'center',
                          gap: tokens.spacingHorizontalXS,
                        }}
                      >
                        <Text>SEO Keywords</Text>
                        <Tooltip
                          content="Keywords to optimize for search engines. Separate with commas."
                          relationship="label"
                        >
                          <Info24Regular
                            style={{ fontSize: '16px', color: tokens.colorNeutralForeground3 }}
                          />
                        </Tooltip>
                      </div>
                    }
                    hint="Comma-separated keywords for SEO optimization"
                  >
                    <Textarea
                      value={advancedData.seoKeywords.join(', ')}
                      onChange={(_, newData) =>
                        onAdvancedChange({
                          ...advancedData,
                          seoKeywords: newData.value
                            .split(',')
                            .map((k) => k.trim())
                            .filter(Boolean),
                        })
                      }
                      placeholder="e.g., artificial intelligence, machine learning, AI basics"
                      rows={2}
                    />
                  </Field>

                  <Field label="Target Platform" hint="Optimize for specific platform requirements">
                    <Dropdown
                      value={advancedData.targetPlatform}
                      onOptionSelect={(_, newData) =>
                        onAdvancedChange({
                          ...advancedData,
                          targetPlatform: newData.optionValue as AdvancedData['targetPlatform'],
                        })
                      }
                    >
                      <Option value="youtube">YouTube (16:9, detailed)</Option>
                      <Option value="tiktok">TikTok (9:16, short & punchy)</Option>
                      <Option value="instagram">Instagram (1:1 or 9:16)</Option>
                      <Option value="twitter">Twitter (16:9, concise)</Option>
                      <Option value="linkedin">LinkedIn (16:9, professional)</Option>
                    </Dropdown>
                  </Field>
                </div>
              </AccordionPanel>
            </AccordionItem>
          </Accordion>
        </div>
      )}

      <div>
        <Title3>Need Inspiration? Try These Examples</Title3>
        <div className={styles.exampleGrid}>
          {EXAMPLE_PROMPTS.map((example, index) => (
            <Card
              key={index}
              className={styles.exampleCard}
              onClick={() => handleExampleClick(example)}
            >
              <Text weight="semibold" size={300}>
                {example.title}
              </Text>
              <Text
                size={200}
                style={{
                  marginTop: tokens.spacingVerticalS,
                  color: tokens.colorNeutralForeground3,
                }}
              >
                {example.prompt}
              </Text>
            </Card>
          ))}
        </div>
      </div>
    </div>
  );
};

interface SpeechRecognitionInstance {
  start: () => void;
  stop: () => void;
  onresult: (event: SpeechRecognitionEvent) => void;
  onerror: (event: SpeechRecognitionErrorEvent) => void;
  onend: () => void;
}

interface SpeechRecognitionEvent {
  results: {
    [index: number]: {
      [index: number]: {
        transcript: string;
      };
    };
  };
}

interface SpeechRecognitionErrorEvent {
  error: string;
}
