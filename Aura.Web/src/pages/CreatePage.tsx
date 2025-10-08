import { useState } from 'react';
import {
  makeStyles,
  tokens,
  Title1,
  Title2,
  Text,
  Button,
  Input,
  Dropdown,
  Option,
  Slider,
  Card,
  Field,
  MessageBar,
  MessageBarBody,
} from '@fluentui/react-components';
import { Play24Regular, Checkmark20Regular } from '@fluentui/react-icons';
import type { Brief, PlanSpec, PreflightResult } from '../types';
import { PreflightModal } from '../components/PreflightModal';

const useStyles = makeStyles({
  container: {
    maxWidth: '800px',
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
  form: {
    display: 'flex',
    flexDirection: 'column',
    gap: tokens.spacingVerticalL,
  },
  section: {
    padding: tokens.spacingVerticalXL,
  },
  actions: {
    display: 'flex',
    gap: tokens.spacingHorizontalM,
    justifyContent: 'flex-end',
    marginTop: tokens.spacingVerticalXL,
  },
  fieldGroup: {
    display: 'flex',
    flexDirection: 'column',
    gap: tokens.spacingVerticalL,
  },
});

export function CreatePage() {
  const styles = useStyles();
  const [currentStep, setCurrentStep] = useState(1);
  const [brief, setBrief] = useState<Partial<Brief>>({
    topic: '',
    audience: 'General',
    goal: 'Inform',
    tone: 'Informative',
    language: 'en-US',
    aspect: 'Widescreen16x9',
  });

  const [planSpec, setPlanSpec] = useState<Partial<PlanSpec>>({
    targetDurationMinutes: 3.0,
    pacing: 'Conversational',
    density: 'Balanced',
    style: 'Standard',
  });

  const [generating, setGenerating] = useState(false);
  const [preflightModalOpen, setPreflightModalOpen] = useState(false);
  const [preflightResult, setPreflightResult] = useState<PreflightResult | null>(null);

  const runPreflightCheck = async (): Promise<boolean> => {
    try {
      const response = await fetch('/api/preflight/run', { method: 'POST' });
      if (response.ok) {
        const result = await response.json();
        setPreflightResult(result);
        return result.ok;
      }
      return false;
    } catch (error) {
      console.error('Error running preflight check:', error);
      return false;
    }
  };

  const handleGenerate = async () => {
    setGenerating(true);
    try {
      // Run preflight check first
      const preflightPassed = await runPreflightCheck();
      
      if (!preflightPassed) {
        // Show preflight modal if checks failed
        setPreflightModalOpen(true);
        setGenerating(false);
        return;
      }

      // Call API to generate video
      const response = await fetch('/api/script', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({
          topic: brief.topic,
          audience: brief.audience,
          goal: brief.goal,
          tone: brief.tone,
          language: brief.language,
          aspect: brief.aspect,
          targetDurationMinutes: planSpec.targetDurationMinutes,
          pacing: planSpec.pacing,
          density: planSpec.density,
          style: planSpec.style,
        }),
      });

      if (response.ok) {
        const data = await response.json();
        console.log('Script generated:', data);
        alert('Script generated successfully! Check console for details.');
      } else {
        alert('Failed to generate script');
      }
    } catch (error) {
      console.error('Error generating script:', error);
      alert('Error generating script');
    } finally {
      setGenerating(false);
    }
  };

  return (
    <div className={styles.container}>
      <div className={styles.header}>
        <Title1>Create Video</Title1>
        <Text className={styles.subtitle}>Step {currentStep} of 3</Text>
      </div>

      <div className={styles.form}>
        {currentStep === 1 && (
          <Card className={styles.section}>
            <Title2>Brief</Title2>
            <Text size={200} style={{ marginBottom: tokens.spacingVerticalL }}>
              Define the core details of your video
            </Text>
            <div className={styles.fieldGroup}>
              <Field label="Topic" required hint="What is your video about?">
                <Input
                  value={brief.topic}
                  onChange={(_, data) => setBrief({ ...brief, topic: data.value })}
                  placeholder="Enter your video topic"
                />
              </Field>

              <Field label="Audience" hint="Who is this video for?">
                <Dropdown
                  value={brief.audience}
                  onOptionSelect={(_, data) => setBrief({ ...brief, audience: data.optionText })}
                >
                  <Option>General</Option>
                  <Option>Beginners</Option>
                  <Option>Advanced</Option>
                  <Option>Professionals</Option>
                </Dropdown>
              </Field>

              <Field label="Tone" hint="What style should the video have?">
                <Dropdown
                  value={brief.tone}
                  onOptionSelect={(_, data) => setBrief({ ...brief, tone: data.optionText })}
                >
                  <Option>Informative</Option>
                  <Option>Casual</Option>
                  <Option>Professional</Option>
                  <Option>Energetic</Option>
                </Dropdown>
              </Field>

              <Field label="Aspect Ratio" hint="Video dimensions">
                <Dropdown
                  value={brief.aspect}
                  onOptionSelect={(_, data) => setBrief({ ...brief, aspect: data.optionValue as any })}
                >
                  <Option value="Widescreen16x9">16:9 Widescreen</Option>
                  <Option value="Vertical9x16">9:16 Vertical</Option>
                  <Option value="Square1x1">1:1 Square</Option>
                </Dropdown>
              </Field>
            </div>
          </Card>
        )}

        {currentStep === 2 && (
          <Card className={styles.section}>
            <Title2>Length and Pacing</Title2>
            <Text size={200} style={{ marginBottom: tokens.spacingVerticalL }}>
              Configure the duration and pacing of your video
            </Text>
            <div className={styles.fieldGroup}>
              <Field label={`Duration: ${planSpec.targetDurationMinutes} minutes`} hint="How long should the video be?">
                <Slider
                  min={0.5}
                  max={20}
                  step={0.5}
                  value={planSpec.targetDurationMinutes}
                  onChange={(_, data) =>
                    setPlanSpec({ ...planSpec, targetDurationMinutes: data.value })
                  }
                />
              </Field>

              <Field label="Pacing" hint="How fast should the narration be?">
                <Dropdown
                  value={planSpec.pacing}
                  onOptionSelect={(_, data) => setPlanSpec({ ...planSpec, pacing: data.optionText as any })}
                >
                  <Option>Chill</Option>
                  <Option>Conversational</Option>
                  <Option>Fast</Option>
                </Dropdown>
              </Field>

              <Field label="Density" hint="How much content per minute?">
                <Dropdown
                  value={planSpec.density}
                  onOptionSelect={(_, data) => setPlanSpec({ ...planSpec, density: data.optionText as any })}
                >
                  <Option>Sparse</Option>
                  <Option>Balanced</Option>
                  <Option>Dense</Option>
                </Dropdown>
              </Field>
            </div>
          </Card>
        )}

        {currentStep === 3 && (
          <Card className={styles.section}>
            <Title2>Confirm</Title2>
            <Text size={200} style={{ marginBottom: tokens.spacingVerticalL }}>
              Review your settings and generate your video
            </Text>
            
            {preflightResult && !preflightResult.ok && (
              <MessageBar intent="warning" style={{ marginBottom: tokens.spacingVerticalL }}>
                <MessageBarBody>
                  Configuration issues detected. Click "Run Preflight Check" to review and fix.
                </MessageBarBody>
              </MessageBar>
            )}

            <div style={{ marginTop: tokens.spacingVerticalL, display: 'flex', flexDirection: 'column', gap: tokens.spacingVerticalM }}>
              <div>
                <Text weight="semibold">Topic:</Text> <Text>{brief.topic}</Text>
              </div>
              <div>
                <Text weight="semibold">Audience:</Text> <Text>{brief.audience}</Text>
              </div>
              <div>
                <Text weight="semibold">Duration:</Text> <Text>{planSpec.targetDurationMinutes} minutes</Text>
              </div>
              <div>
                <Text weight="semibold">Pacing:</Text> <Text>{planSpec.pacing}</Text>
              </div>
              <div>
                <Text weight="semibold">Density:</Text> <Text>{planSpec.density}</Text>
              </div>
              <div>
                <Text weight="semibold">Aspect:</Text> <Text>{brief.aspect}</Text>
              </div>
            </div>

            <div style={{ marginTop: tokens.spacingVerticalL }}>
              <Button
                icon={<Checkmark20Regular />}
                onClick={() => setPreflightModalOpen(true)}
              >
                Run Preflight Check
              </Button>
            </div>
          </Card>
        )}

        <div className={styles.actions}>
          {currentStep > 1 && (
            <Button onClick={() => setCurrentStep(currentStep - 1)}>
              Previous
            </Button>
          )}
          {currentStep < 3 ? (
            <Button 
              appearance="primary" 
              onClick={() => setCurrentStep(currentStep + 1)}
              disabled={currentStep === 1 && !brief.topic}
            >
              Next
            </Button>
          ) : (
            <Button
              appearance="primary"
              icon={<Play24Regular />}
              onClick={handleGenerate}
              disabled={generating}
            >
              {generating ? 'Generating...' : 'Generate Video'}
            </Button>
          )}
        </div>
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
          } catch (error) {
            console.error('Error switching profile:', error);
          }
        }}
      />
    </div>
  );
}
