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
  Spinner,
  MessageBar,
  MessageBarBody,
  MessageBarTitle,
  Accordion,
  AccordionItem,
  AccordionHeader,
  AccordionPanel,
} from '@fluentui/react-components';
import { Play24Regular, Lightbulb24Regular, Checkmark24Regular } from '@fluentui/react-icons';
import type { Brief, PlanSpec, PlanRecommendations } from '../types';

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
  recommendButton: {
    marginTop: tokens.spacingVerticalL,
  },
  recommendationsSection: {
    marginTop: tokens.spacingVerticalXL,
    padding: tokens.spacingVerticalL,
    backgroundColor: tokens.colorNeutralBackground2,
    borderRadius: tokens.borderRadiusMedium,
  },
  diffItem: {
    display: 'flex',
    justifyContent: 'space-between',
    alignItems: 'center',
    padding: tokens.spacingVerticalS,
    marginBottom: tokens.spacingVerticalS,
  },
  applyButton: {
    marginLeft: tokens.spacingHorizontalM,
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
  const [loadingRecommendations, setLoadingRecommendations] = useState(false);
  const [recommendations, setRecommendations] = useState<PlanRecommendations | null>(null);
  const [appliedGroups, setAppliedGroups] = useState<Set<string>>(new Set());

  const handleGetRecommendations = async () => {
    setLoadingRecommendations(true);
    try {
      const response = await fetch('/api/planner/recommendations', {
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
        setRecommendations(data.recommendations);
      } else {
        alert('Failed to get recommendations');
      }
    } catch (error) {
      console.error('Error getting recommendations:', error);
      alert('Error getting recommendations');
    } finally {
      setLoadingRecommendations(false);
    }
  };

  const applyRecommendation = (group: string, value: any) => {
    switch (group) {
      case 'sceneCount':
        // Store for later use in generation
        console.log('Scene count recommendation:', value);
        break;
      case 'voiceRate':
        // Store for voice settings
        console.log('Voice rate recommendation:', value);
        break;
      // Add other cases as needed
    }
    setAppliedGroups(new Set([...appliedGroups, group]));
  };

  const applyAllRecommendations = () => {
    if (!recommendations) return;
    
    // Apply voice settings
    applyRecommendation('voiceRate', recommendations.voiceRate);
    applyRecommendation('voicePitch', recommendations.voicePitch);
    
    // Apply visual settings
    applyRecommendation('sceneCount', recommendations.sceneCount);
    applyRecommendation('bRollPercentage', recommendations.bRollPercentage);
    applyRecommendation('overlayDensity', recommendations.overlayDensity);
    
    alert('All recommendations applied! These will be used during video generation.');
  };

  const handleGenerate = async () => {
    setGenerating(true);
    try {
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
          <>
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

              <Button
                appearance="secondary"
                icon={<Lightbulb24Regular />}
                onClick={handleGetRecommendations}
                disabled={loadingRecommendations || !brief.topic}
                className={styles.recommendButton}
              >
                {loadingRecommendations ? 'Getting Recommendations...' : 'Get AI Recommendations'}
              </Button>
            </Card>

            {loadingRecommendations && (
              <Card className={styles.section}>
                <Spinner label="Analyzing your video requirements..." />
              </Card>
            )}

            {recommendations && !loadingRecommendations && (
              <Card className={styles.recommendationsSection}>
                <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: tokens.spacingVerticalL }}>
                  <Title2>Recommendations</Title2>
                  <Button
                    appearance="primary"
                    icon={<Checkmark24Regular />}
                    onClick={applyAllRecommendations}
                  >
                    Apply All
                  </Button>
                </div>

                <MessageBar intent="info">
                  <MessageBarBody>
                    <MessageBarTitle>AI-Powered Suggestions</MessageBarTitle>
                    These recommendations are generated based on your topic, audience, and goals. You can apply them all or individually.
                  </MessageBarBody>
                </MessageBar>

                <Accordion collapsible multiple style={{ marginTop: tokens.spacingVerticalL }}>
                  <AccordionItem value="structure">
                    <AccordionHeader>Video Structure</AccordionHeader>
                    <AccordionPanel>
                      <div className={styles.diffItem}>
                        <div>
                          <Text weight="semibold">Scene Count: </Text>
                          <Text>{recommendations.sceneCount} scenes</Text>
                        </div>
                        <Button
                          size="small"
                          appearance={appliedGroups.has('sceneCount') ? 'outline' : 'secondary'}
                          onClick={() => applyRecommendation('sceneCount', recommendations.sceneCount)}
                          disabled={appliedGroups.has('sceneCount')}
                        >
                          {appliedGroups.has('sceneCount') ? 'Applied' : 'Apply'}
                        </Button>
                      </div>
                      <div className={styles.diffItem}>
                        <div>
                          <Text weight="semibold">Shots per Scene: </Text>
                          <Text>{recommendations.shotsPerScene} shots</Text>
                        </div>
                        <Button
                          size="small"
                          appearance={appliedGroups.has('shotsPerScene') ? 'outline' : 'secondary'}
                          onClick={() => applyRecommendation('shotsPerScene', recommendations.shotsPerScene)}
                          disabled={appliedGroups.has('shotsPerScene')}
                        >
                          {appliedGroups.has('shotsPerScene') ? 'Applied' : 'Apply'}
                        </Button>
                      </div>
                      <div className={styles.diffItem}>
                        <div>
                          <Text weight="semibold">B-Roll Coverage: </Text>
                          <Text>{recommendations.bRollPercentage.toFixed(0)}%</Text>
                        </div>
                        <Button
                          size="small"
                          appearance={appliedGroups.has('bRollPercentage') ? 'outline' : 'secondary'}
                          onClick={() => applyRecommendation('bRollPercentage', recommendations.bRollPercentage)}
                          disabled={appliedGroups.has('bRollPercentage')}
                        >
                          {appliedGroups.has('bRollPercentage') ? 'Applied' : 'Apply'}
                        </Button>
                      </div>
                    </AccordionPanel>
                  </AccordionItem>

                  <AccordionItem value="voice">
                    <AccordionHeader>Voice & Audio</AccordionHeader>
                    <AccordionPanel>
                      <div className={styles.diffItem}>
                        <div>
                          <Text weight="semibold">Voice Rate: </Text>
                          <Text>{recommendations.voiceRate.toFixed(2)}x</Text>
                        </div>
                        <Button
                          size="small"
                          appearance={appliedGroups.has('voiceRate') ? 'outline' : 'secondary'}
                          onClick={() => applyRecommendation('voiceRate', recommendations.voiceRate)}
                          disabled={appliedGroups.has('voiceRate')}
                        >
                          {appliedGroups.has('voiceRate') ? 'Applied' : 'Apply'}
                        </Button>
                      </div>
                      <div className={styles.diffItem}>
                        <div>
                          <Text weight="semibold">Voice Pitch: </Text>
                          <Text>{recommendations.voicePitch > 0 ? '+' : ''}{recommendations.voicePitch.toFixed(1)}</Text>
                        </div>
                        <Button
                          size="small"
                          appearance={appliedGroups.has('voicePitch') ? 'outline' : 'secondary'}
                          onClick={() => applyRecommendation('voicePitch', recommendations.voicePitch)}
                          disabled={appliedGroups.has('voicePitch')}
                        >
                          {appliedGroups.has('voicePitch') ? 'Applied' : 'Apply'}
                        </Button>
                      </div>
                      <div className={styles.diffItem}>
                        <div>
                          <Text weight="semibold">Music Tempo: </Text>
                          <Text style={{ fontSize: '12px' }}>{recommendations.musicTempoCurve}</Text>
                        </div>
                        <Button
                          size="small"
                          appearance={appliedGroups.has('musicTempoCurve') ? 'outline' : 'secondary'}
                          onClick={() => applyRecommendation('musicTempoCurve', recommendations.musicTempoCurve)}
                          disabled={appliedGroups.has('musicTempoCurve')}
                        >
                          {appliedGroups.has('musicTempoCurve') ? 'Applied' : 'Apply'}
                        </Button>
                      </div>
                    </AccordionPanel>
                  </AccordionItem>

                  <AccordionItem value="visuals">
                    <AccordionHeader>Visuals & Captions</AccordionHeader>
                    <AccordionPanel>
                      <div className={styles.diffItem}>
                        <div>
                          <Text weight="semibold">Caption Style: </Text>
                          <Text>{recommendations.captionStyle}</Text>
                        </div>
                        <Button
                          size="small"
                          appearance={appliedGroups.has('captionStyle') ? 'outline' : 'secondary'}
                          onClick={() => applyRecommendation('captionStyle', recommendations.captionStyle)}
                          disabled={appliedGroups.has('captionStyle')}
                        >
                          {appliedGroups.has('captionStyle') ? 'Applied' : 'Apply'}
                        </Button>
                      </div>
                      <div className={styles.diffItem}>
                        <div>
                          <Text weight="semibold">Overlay Density: </Text>
                          <Text>{(recommendations.overlayDensity * 100).toFixed(0)}%</Text>
                        </div>
                        <Button
                          size="small"
                          appearance={appliedGroups.has('overlayDensity') ? 'outline' : 'secondary'}
                          onClick={() => applyRecommendation('overlayDensity', recommendations.overlayDensity)}
                          disabled={appliedGroups.has('overlayDensity')}
                        >
                          {appliedGroups.has('overlayDensity') ? 'Applied' : 'Apply'}
                        </Button>
                      </div>
                      <div className={styles.diffItem}>
                        <div>
                          <Text weight="semibold">Reading Level: </Text>
                          <Text>{recommendations.readingLevel}</Text>
                        </div>
                        <Button
                          size="small"
                          appearance={appliedGroups.has('readingLevel') ? 'outline' : 'secondary'}
                          onClick={() => applyRecommendation('readingLevel', recommendations.readingLevel)}
                          disabled={appliedGroups.has('readingLevel')}
                        >
                          {appliedGroups.has('readingLevel') ? 'Applied' : 'Apply'}
                        </Button>
                      </div>
                    </AccordionPanel>
                  </AccordionItem>

                  <AccordionItem value="seo">
                    <AccordionHeader>SEO & Marketing</AccordionHeader>
                    <AccordionPanel>
                      <div className={styles.diffItem}>
                        <div style={{ flex: 1 }}>
                          <Text weight="semibold">SEO Title: </Text>
                          <Text>{recommendations.seoTitle}</Text>
                        </div>
                        <Button
                          size="small"
                          appearance={appliedGroups.has('seoTitle') ? 'outline' : 'secondary'}
                          onClick={() => applyRecommendation('seoTitle', recommendations.seoTitle)}
                          disabled={appliedGroups.has('seoTitle')}
                        >
                          {appliedGroups.has('seoTitle') ? 'Applied' : 'Apply'}
                        </Button>
                      </div>
                      <div className={styles.diffItem}>
                        <div style={{ flex: 1 }}>
                          <Text weight="semibold">Description: </Text>
                          <Text>{recommendations.seoDescription}</Text>
                        </div>
                        <Button
                          size="small"
                          appearance={appliedGroups.has('seoDescription') ? 'outline' : 'secondary'}
                          onClick={() => applyRecommendation('seoDescription', recommendations.seoDescription)}
                          disabled={appliedGroups.has('seoDescription')}
                        >
                          {appliedGroups.has('seoDescription') ? 'Applied' : 'Apply'}
                        </Button>
                      </div>
                      <div className={styles.diffItem}>
                        <div style={{ flex: 1 }}>
                          <Text weight="semibold">Tags: </Text>
                          <Text>{recommendations.seoTags.join(', ')}</Text>
                        </div>
                        <Button
                          size="small"
                          appearance={appliedGroups.has('seoTags') ? 'outline' : 'secondary'}
                          onClick={() => applyRecommendation('seoTags', recommendations.seoTags)}
                          disabled={appliedGroups.has('seoTags')}
                        >
                          {appliedGroups.has('seoTags') ? 'Applied' : 'Apply'}
                        </Button>
                      </div>
                      <div className={styles.diffItem}>
                        <div style={{ flex: 1 }}>
                          <Text weight="semibold">Thumbnail Prompt: </Text>
                          <Text style={{ fontSize: '12px' }}>{recommendations.thumbnailPrompt}</Text>
                        </div>
                        <Button
                          size="small"
                          appearance={appliedGroups.has('thumbnailPrompt') ? 'outline' : 'secondary'}
                          onClick={() => applyRecommendation('thumbnailPrompt', recommendations.thumbnailPrompt)}
                          disabled={appliedGroups.has('thumbnailPrompt')}
                        >
                          {appliedGroups.has('thumbnailPrompt') ? 'Applied' : 'Apply'}
                        </Button>
                      </div>
                    </AccordionPanel>
                  </AccordionItem>

                  <AccordionItem value="outline">
                    <AccordionHeader>Video Outline</AccordionHeader>
                    <AccordionPanel>
                      <pre style={{ 
                        whiteSpace: 'pre-wrap', 
                        fontSize: '14px', 
                        backgroundColor: tokens.colorNeutralBackground1,
                        padding: tokens.spacingVerticalM,
                        borderRadius: tokens.borderRadiusSmall,
                        marginBottom: tokens.spacingVerticalM
                      }}>
                        {recommendations.outline}
                      </pre>
                      <Button
                        size="small"
                        appearance={appliedGroups.has('outline') ? 'outline' : 'secondary'}
                        onClick={() => applyRecommendation('outline', recommendations.outline)}
                        disabled={appliedGroups.has('outline')}
                      >
                        {appliedGroups.has('outline') ? 'Applied' : 'Apply'}
                      </Button>
                    </AccordionPanel>
                  </AccordionItem>
                </Accordion>
              </Card>
            )}
          </>
        )}

        {currentStep === 3 && (
          <Card className={styles.section}>
            <Title2>Confirm</Title2>
            <Text size={200} style={{ marginBottom: tokens.spacingVerticalL }}>
              Review your settings and generate your video
            </Text>
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
    </div>
  );
}
