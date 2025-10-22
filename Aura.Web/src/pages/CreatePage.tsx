import { useState } from 'react';
import { apiUrl } from '../config/api';
import {
  makeStyles,
  tokens,
  Title1,
  Title2,
  Title3,
  Text,
  Button,
  Input,
  Dropdown,
  Option,
  Slider,
  Card,
  Field,
  Spinner,
  Badge,
  Checkbox,
  Tooltip,
} from '@fluentui/react-components';
import { Play24Regular, Lightbulb24Regular, Checkmark24Regular } from '@fluentui/react-icons';
import type { Brief, PlanSpec, PlannerRecommendations } from '../types';
import type { PreflightReport } from '../state/providers';
import { normalizeEnumsForApi, validateAndWarnEnums } from '../utils/enumNormalizer';
import { PreflightPanel } from '../components/PreflightPanel';

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
  const [loadingRecommendations, setLoadingRecommendations] = useState(false);
  const [recommendations, setRecommendations] = useState<PlannerRecommendations | null>(null);
  const [showRecommendations, setShowRecommendations] = useState(false);

  // Preflight state
  const [selectedProfile, setSelectedProfile] = useState('Free-Only');
  const [preflightReport, setPreflightReport] = useState<PreflightReport | null>(null);
  const [isRunningPreflight, setIsRunningPreflight] = useState(false);
  const [overridePreflightGate, setOverridePreflightGate] = useState(false);

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
        setShowRecommendations(true);
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

  const applyAllRecommendations = () => {
    if (!recommendations) return;
    // Apply recommendations that map to planSpec
    // Additional fields can be mapped as needed
    alert('Recommendations applied successfully!');
  };

  const handleRunPreflight = async () => {
    setIsRunningPreflight(true);
    try {
      const response = await fetch(`/api/preflight?profile=${encodeURIComponent(selectedProfile)}`);
      
      if (response.ok) {
        const data = await response.json();
        setPreflightReport(data);
      } else {
        alert('Failed to run preflight check');
      }
    } catch (error) {
      console.error('Error running preflight check:', error);
      alert('Error running preflight check');
    } finally {
      setIsRunningPreflight(false);
    }
  };

  const handleApplySafeDefaults = async () => {
    try {
      const response = await fetch(apiUrl('/api/preflight/safe-defaults'));
      
      if (response.ok) {
        // Apply safe defaults
        setSelectedProfile('Free-Only');
        
        // Re-run preflight check
        await handleRunPreflight();
        
        alert('Applied safe defaults: Free-Only mode.');
      } else {
        alert('Failed to get safe defaults');
      }
    } catch (error) {
      console.error('Error applying safe defaults:', error);
      alert('Error applying safe defaults');
    }
  };

  const handleGenerate = async () => {
    console.log('[GENERATE VIDEO] Button clicked (CreatePage)');
    console.log('[GENERATE VIDEO] Current form data:', { brief, planSpec });
    
    setGenerating(true);
    try {
      console.log('[GENERATE VIDEO] Starting video generation...');
      
      // Validate and warn about legacy enum values
      validateAndWarnEnums(brief, planSpec);
      
      // Normalize enums to canonical values before sending to API
      const { brief: normalizedBrief, planSpec: normalizedPlanSpec } = normalizeEnumsForApi(brief, planSpec);
      
      console.log('[GENERATE VIDEO] Normalized brief:', normalizedBrief);
      console.log('[GENERATE VIDEO] Normalized planSpec:', normalizedPlanSpec);
      
      // Create voice spec with defaults
      const voiceSpec = {
        voiceName: 'en-US-Standard-A',
        rate: 1.0,
        pitch: 0.0,
        pause: 'Medium',
      };
      
      // Create render spec with defaults
      const renderSpec = {
        res: { width: 1920, height: 1080 },
        container: 'mp4',
        videoBitrateK: 5000,
        audioBitrateK: 192,
        fps: 30,
        codec: 'H264',
        qualityLevel: 75,
        enableSceneCut: true,
      };
      
      const requestData = {
        brief: {
          topic: normalizedBrief.topic,
          audience: normalizedBrief.audience || 'General',
          goal: normalizedBrief.goal || 'Inform',
          tone: normalizedBrief.tone || 'Informative',
          language: normalizedBrief.language || 'en-US',
          aspect: normalizedBrief.aspect || 'Widescreen16x9',
        },
        planSpec: {
          targetDuration: `00:${String(Math.floor(normalizedPlanSpec.targetDurationMinutes || 3)).padStart(2, '0')}:00`,
          pacing: normalizedPlanSpec.pacing || 'Conversational',
          density: normalizedPlanSpec.density || 'Balanced',
          style: normalizedPlanSpec.style || 'Standard',
        },
        voiceSpec,
        renderSpec,
      };
      
      console.log('[GENERATE VIDEO] Request data:', requestData);
      console.log('[GENERATE VIDEO] Calling API endpoint /api/jobs...');
      
      // Create a full video generation job via JobsController
      const response = await fetch(apiUrl('/api/jobs'), {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify(requestData),
      });

      console.log('[GENERATE VIDEO] API response status:', response.status);

      if (response.ok) {
        const data = await response.json();
        console.log('[GENERATE VIDEO] Job created successfully:', data);
        alert(`Video generation started! Job ID: ${data.jobId}\n\nYou can track progress in the jobs panel.`);
        
        // TODO: Navigate to jobs page or open generation panel
        // For now, just show success message
      } else {
        const errorText = await response.text();
        console.error('[GENERATE VIDEO] Failed to create job:', response.status, errorText);
        alert(`Failed to start video generation: ${response.status} ${response.statusText}\n\nCheck console for details.`);
      }
    } catch (error) {
      console.error('[GENERATE VIDEO] Error creating video generation job:', error);
      console.error('[GENERATE VIDEO] Error details:', {
        message: error instanceof Error ? error.message : 'Unknown error',
        stack: error instanceof Error ? error.stack : undefined,
      });
      alert(`Error starting video generation: ${error instanceof Error ? error.message : 'Unknown error'}\n\nCheck console for details.`);
    } finally {
      console.log('[GENERATE VIDEO] Resetting generating state');
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

                <div style={{ marginTop: tokens.spacingVerticalL }}>
                  <Button
                    appearance="secondary"
                    icon={<Lightbulb24Regular />}
                    onClick={handleGetRecommendations}
                    disabled={loadingRecommendations || !brief.topic}
                  >
                    {loadingRecommendations ? (
                      <>
                        <Spinner size="tiny" /> Getting Recommendations...
                      </>
                    ) : (
                      'Get Recommendations'
                    )}
                  </Button>
                </div>
              </div>
            </Card>

            {showRecommendations && recommendations && (
              <Card className={styles.section} style={{ backgroundColor: tokens.colorNeutralBackground2 }}>
                <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: tokens.spacingVerticalM }}>
                  <Title3>AI Recommendations</Title3>
                  <Button
                    appearance="primary"
                    icon={<Checkmark24Regular />}
                    onClick={applyAllRecommendations}
                  >
                    Apply All
                  </Button>
                </div>

                <div style={{ display: 'flex', flexDirection: 'column', gap: tokens.spacingVerticalL }}>
                  <div>
                    <Text weight="semibold">Scene Count:</Text>{' '}
                    <Badge appearance="tint" color="informative">{recommendations.sceneCount} scenes</Badge>
                  </div>

                  <div>
                    <Text weight="semibold">Shots per Scene:</Text>{' '}
                    <Badge appearance="tint" color="informative">{recommendations.shotsPerScene} shots</Badge>
                  </div>

                  <div>
                    <Text weight="semibold">B-Roll:</Text>{' '}
                    <Badge appearance="tint" color="informative">{recommendations.bRollPercentage}%</Badge>
                  </div>

                  <div>
                    <Text weight="semibold">Overlay Density:</Text>{' '}
                    <Badge appearance="tint" color="informative">{recommendations.overlayDensity} overlays/scene</Badge>
                  </div>

                  <div>
                    <Text weight="semibold">Reading Level:</Text>{' '}
                    <Badge appearance="tint" color="informative">Grade {recommendations.readingLevel}</Badge>
                  </div>

                  <div>
                    <Text weight="semibold">Voice:</Text>{' '}
                    <Text>Rate: {recommendations.voice.rate}x, Pitch: {recommendations.voice.pitch}x, Style: {recommendations.voice.style}</Text>
                  </div>

                  <div>
                    <Text weight="semibold">Music:</Text>{' '}
                    <Text>{recommendations.music.genre} - {recommendations.music.tempo}</Text>
                  </div>

                  <div>
                    <Text weight="semibold">Captions:</Text>{' '}
                    <Text>{recommendations.captions.position}, {recommendations.captions.fontSize}</Text>
                  </div>

                  <div>
                    <Text weight="semibold">SEO Title:</Text>{' '}
                    <Text>{recommendations.seo.title}</Text>
                  </div>

                  <div>
                    <Text weight="semibold">Tags:</Text>{' '}
                    <Text>{recommendations.seo.tags.join(', ')}</Text>
                  </div>

                  <div>
                    <Text weight="semibold">Thumbnail Prompt:</Text>
                    <div style={{ 
                      marginTop: tokens.spacingVerticalXS, 
                      padding: tokens.spacingVerticalM,
                      backgroundColor: tokens.colorNeutralBackground1,
                      borderRadius: tokens.borderRadiusMedium
                    }}>
                      <Text size={200}>{recommendations.thumbnailPrompt}</Text>
                    </div>
                  </div>

                  <div>
                    <Text weight="semibold">Outline:</Text>
                    <div style={{ 
                      marginTop: tokens.spacingVerticalXS, 
                      padding: tokens.spacingVerticalM,
                      backgroundColor: tokens.colorNeutralBackground1,
                      borderRadius: tokens.borderRadiusMedium,
                      whiteSpace: 'pre-wrap'
                    }}>
                      <Text size={200} style={{ fontFamily: 'monospace' }}>{recommendations.outline}</Text>
                    </div>
                  </div>
                </div>
              </Card>
            )}
          </>
        )}

        {currentStep === 3 && (
          <>
            <Card className={styles.section}>
              <Title2>Review Settings</Title2>
              <Text size={200} style={{ marginBottom: tokens.spacingVerticalL }}>
                Review your settings before generating your video
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

            <Card className={styles.section}>
              <div style={{ marginBottom: tokens.spacingVerticalL }}>
                <Field label="Profile">
                  <Dropdown
                    value={selectedProfile}
                    onOptionSelect={(_, data) => {
                      setSelectedProfile(data.optionValue as string);
                      setPreflightReport(null); // Clear report when profile changes
                    }}
                  >
                    <Option value="Free-Only">Free-Only (Ollama + Windows TTS + Stock)</Option>
                    <Option value="Balanced Mix">Balanced Mix (Pro with fallbacks)</Option>
                    <Option value="Pro-Max">Pro-Max (OpenAI + ElevenLabs + Cloud)</Option>
                  </Dropdown>
                </Field>
              </div>

              <PreflightPanel
                profile={selectedProfile}
                report={preflightReport}
                isRunning={isRunningPreflight}
                onRunPreflight={handleRunPreflight}
                onApplySafeDefaults={handleApplySafeDefaults}
              />

              {preflightReport && !preflightReport.ok && (
                <div style={{ marginTop: tokens.spacingVerticalL }}>
                  <Checkbox
                    checked={overridePreflightGate}
                    onChange={(_, data) => setOverridePreflightGate(data.checked === true)}
                    label={
                      <Tooltip content="Some preflight checks failed, but you can still proceed at your own risk" relationship="label">
                        <Text>Override and proceed anyway</Text>
                      </Tooltip>
                    }
                  />
                </div>
              )}
            </Card>
          </>
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
            <Tooltip
              content={
                !preflightReport
                  ? 'Run preflight check before generating'
                  : !preflightReport.ok && !overridePreflightGate
                  ? 'Preflight checks failed. Enable override to proceed anyway.'
                  : ''
              }
              relationship="label"
            >
              <Button
                appearance="primary"
                icon={<Play24Regular />}
                onClick={handleGenerate}
                disabled={generating || !preflightReport || (!preflightReport.ok && !overridePreflightGate)}
              >
                {generating ? 'Generating...' : 'Generate Video'}
              </Button>
            </Tooltip>
          )}
        </div>
      </div>
    </div>
  );
}
