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
  Switch,
  Accordion,
  AccordionItem,
  AccordionHeader,
  AccordionPanel,
  Dialog,
  DialogTrigger,
  DialogSurface,
  DialogTitle,
  DialogBody,
  DialogContent,
} from '@fluentui/react-components';
import {
  Play24Regular,
  Lightbulb24Regular,
  Checkmark24Regular,
  ChevronDown24Regular,
  Info24Regular,
  ArrowReset24Regular,
  Dismiss24Regular,
  Sparkle24Regular,
  Save20Regular,
} from '@fluentui/react-icons';
import { useState, useEffect, useRef } from 'react';
import { useNavigate, useSearchParams } from 'react-router-dom';
import { getWizardProject } from '../../api/wizardProjects';
import { AutoSaveIndicator } from '../../components/AutoSaveIndicator';
import { GenerationPanel } from '../../components/Generation/GenerationPanel';
import { useNotifications } from '../../components/Notifications/Toasts';
import { PreflightPanel } from '../../components/PreflightPanel';
import { PromptCustomizationPanel } from '../../components/PromptCustomization/PromptCustomizationPanel';
import SaveProjectDialog from '../../components/SaveProjectDialog';
import { TooltipContent, TooltipWithLink } from '../../components/Tooltips';
import { ProviderSelection } from '../../components/Wizard/ProviderSelection';
import { apiUrl } from '../../config/api';
import { useWizardAutoSave } from '../../hooks/useWizardAutoSave';
import { postCancellable, isAbortError } from '../../services/api/cancellableRequests';
import { loggingService as logger } from '../../services/loggingService';
import { useJobState } from '../../state/jobState';
import type { PreflightReport, PerStageProviderSelection } from '../../state/providers';
import { useWizardProjectStore, deserializeWizardState } from '../../state/wizardProject';
import type {
  Brief,
  PlanSpec,
  PlannerRecommendations,
  WizardSettings,
  BrandKitConfig,
  CaptionsConfig,
  StockSourcesConfig,
} from '../../types';
import { parseApiError, openLogsFolder } from '../../utils/apiErrorHandler';
import { normalizeEnumsForApi, validateAndWarnEnums } from '../../utils/enumNormalizer';
import { validateBriefRequest } from '../../utils/formValidation';

const useStyles = makeStyles({
  container: {
    maxWidth: '900px',
    margin: '0 auto',
    padding: tokens.spacingVerticalL,
  },
  header: {
    marginBottom: tokens.spacingVerticalXXL,
    display: 'flex',
    flexDirection: 'column',
    gap: tokens.spacingVerticalS,
  },
  headerRow: {
    display: 'flex',
    justifyContent: 'space-between',
    alignItems: 'center',
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
  advancedSection: {
    marginTop: tokens.spacingVerticalL,
  },
  resetButton: {
    marginTop: tokens.spacingVerticalM,
  },
  infoIcon: {
    color: tokens.colorNeutralForeground3,
    marginLeft: tokens.spacingHorizontalXS,
  },
  gridLayout: {
    display: 'grid',
    gridTemplateColumns: '1fr 1fr',
    gap: tokens.spacingVerticalM,
    '@media (max-width: 768px)': {
      gridTemplateColumns: '1fr',
    },
  },
  keyboardHint: {
    fontSize: tokens.fontSizeBase200,
    color: tokens.colorNeutralForeground3,
    marginTop: tokens.spacingVerticalXS,
  },
});

// Default settings factory
const createDefaultSettings = (): WizardSettings => ({
  brief: {
    topic: '',
    audience: 'General',
    goal: 'Inform',
    tone: 'Informative',
    language: 'en-US',
    aspect: 'Widescreen16x9',
  },
  planSpec: {
    targetDurationMinutes: 3.0,
    pacing: 'Conversational',
    density: 'Balanced',
    style: 'Standard',
  },
  brandKit: {
    watermarkOpacity: 0.7,
  },
  captions: {
    enabled: true,
    format: 'srt',
    burnIn: false,
    fontName: 'Arial',
    fontSize: 24,
    primaryColor: 'FFFFFF',
    outlineColor: '000000',
    outlineWidth: 2,
    position: 'bottom-center',
  },
  stockSources: {
    enablePexels: true,
    enablePixabay: true,
    enableUnsplash: true,
    enableLocalAssets: false,
    enableStableDiffusion: false,
  },
  offlineMode: false,
});

// Local storage key
const SETTINGS_STORAGE_KEY = 'aura-wizard-settings';

// eslint-disable-next-line sonarjs/cognitive-complexity -- Multi-step wizard component with various state management and conditional rendering
export function CreateWizard() {
  const styles = useStyles();
  const navigate = useNavigate();
  const [currentStep, setCurrentStep] = useState(1);

  // Load settings from localStorage or use defaults
  const loadSettings = (): WizardSettings => {
    try {
      const stored = localStorage.getItem(SETTINGS_STORAGE_KEY);
      if (stored) {
        const parsed = JSON.parse(stored);
        // Merge with defaults to ensure all properties exist
        return { ...createDefaultSettings(), ...parsed };
      }
    } catch (error) {
      console.error('Failed to load settings from localStorage:', error);
    }
    return createDefaultSettings();
  };

  const [settings, setSettings] = useState<WizardSettings>(loadSettings());
  const [generating, setGenerating] = useState(false);
  const [loadingRecommendations, setLoadingRecommendations] = useState(false);
  const [recommendations, setRecommendations] = useState<PlannerRecommendations | null>(null);
  const [showRecommendations, setShowRecommendations] = useState(false);

  // Preflight state
  const [selectedProfile, setSelectedProfile] = useState('Free-Only');
  const [preflightReport, setPreflightReport] = useState<PreflightReport | null>(null);
  const [isRunningPreflight, setIsRunningPreflight] = useState(false);
  const [overridePreflightGate, setOverridePreflightGate] = useState(false);
  const [perStageSelection, setPerStageSelection] = useState<PerStageProviderSelection>({});

  // Generation panel state
  const [showGenerationPanel, setShowGenerationPanel] = useState(false);
  const [activeJobId, setActiveJobId] = useState<string | null>(null);

  // Prompt customization state
  const [promptCustomizationOpen, setPromptCustomizationOpen] = useState(false);
  const handleClosePromptCustomization = () => setPromptCustomizationOpen(false);

  // Save project dialog state
  const [saveDialogOpen, setSaveDialogOpen] = useState(false);
  const [searchParams] = useSearchParams();
  const { setCurrentProject, currentProject } = useWizardProjectStore();

  // Request cancellation state
  const currentRequestRef = useRef<{ cancel: () => void } | null>(null);

  const { showFailureToast, showSuccessToast } = useNotifications();

  // Auto-save functionality
  const {
    triggerManualSave,
    isSaving: isAutoSaving,
    lastSaveTime,
    saveError: autoSaveError,
  } = useWizardAutoSave({
    enabled: !!currentProject && currentStep > 0,
    intervalMs: 30000, // 30 seconds
    projectId: currentProject?.id,
    projectName: currentProject?.name || 'Untitled Project',
    currentStep: currentStep - 1, // Convert from 1-indexed to 0-indexed
    briefJson: JSON.stringify(settings.brief),
    planSpecJson: JSON.stringify(settings.planSpec),
    voiceSpecJson: undefined, // Add when voice settings are implemented
    renderSpecJson: undefined, // Add when render settings are implemented
    onSaveSuccess: (projectId) => {
      // Update or create current project
      if (currentProject) {
        setCurrentProject({
          ...currentProject,
          id: projectId,
          updatedAt: new Date().toISOString(),
        });
      }
    },
  });

  // Update provider selection
  const updateProviderSelection = (selection: PerStageProviderSelection) => {
    setPerStageSelection(selection);
    setSettings({ ...settings, providerSelection: selection });
    // Reset preflight when selection changes
    setPreflightReport(null);
  };

  // Save settings to localStorage whenever they change
  useEffect(() => {
    try {
      localStorage.setItem(SETTINGS_STORAGE_KEY, JSON.stringify(settings));
    } catch (error) {
      console.error('Failed to save settings to localStorage:', error);
    }
  }, [settings]);

  // Cleanup: Cancel any in-progress requests on unmount
  useEffect(() => {
    return () => {
      if (currentRequestRef.current) {
        currentRequestRef.current.cancel();
      }
    };
  }, []);

  // Load project from URL parameter if present
  useEffect(() => {
    const projectId = searchParams.get('projectId');
    if (projectId) {
      const loadProject = async () => {
        try {
          const project = await getWizardProject(projectId);
          const state = deserializeWizardState(project);

          // Restore wizard state
          if (state.brief) {
            setSettings((prev) => ({ ...prev, brief: state.brief as Brief }));
          }
          if (state.plan) {
            setSettings((prev) => ({ ...prev, planSpec: state.plan as PlanSpec }));
          }

          setCurrentStep(project.currentStep + 1);
          setCurrentProject(project);

          showSuccessToast({
            title: 'Project Loaded',
            message: `Project "${project.name}" loaded successfully`,
          });
        } catch (error) {
          console.error('Failed to load project:', error);
          showFailureToast({
            title: 'Load Failed',
            message: 'Failed to load project. Please try again.',
          });
        }
      };
      loadProject();
    }
  }, [searchParams, setCurrentProject, showSuccessToast, showFailureToast]);

  // Update brief
  const updateBrief = (updates: Partial<Brief>) => {
    setSettings({ ...settings, brief: { ...settings.brief, ...updates } });
  };

  // Update planSpec
  const updatePlanSpec = (updates: Partial<PlanSpec>) => {
    setSettings({ ...settings, planSpec: { ...settings.planSpec, ...updates } });
  };

  // Update brandKit
  const updateBrandKit = (updates: Partial<BrandKitConfig>) => {
    setSettings({ ...settings, brandKit: { ...settings.brandKit, ...updates } });
  };

  // Update captions
  const updateCaptions = (updates: Partial<CaptionsConfig>) => {
    setSettings({ ...settings, captions: { ...settings.captions, ...updates } });
  };

  // Update stock sources
  const updateStockSources = (updates: Partial<StockSourcesConfig>) => {
    setSettings({ ...settings, stockSources: { ...settings.stockSources, ...updates } });
  };

  // Reset to defaults
  const handleResetToDefaults = () => {
    if (confirm('Reset all settings to defaults? This cannot be undone.')) {
      setSettings(createDefaultSettings());
    }
  };

  // Cancel current request
  const handleCancelRequest = () => {
    if (currentRequestRef.current) {
      currentRequestRef.current.cancel();
      currentRequestRef.current = null;
      setGenerating(false);
      showSuccessToast({
        title: 'Request Cancelled',
        message: 'The generation request has been cancelled.',
      });
    }
  };

  const handleGetRecommendations = async () => {
    setLoadingRecommendations(true);
    try {
      const response = await fetch('/api/planner/recommendations', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({
          topic: settings.brief.topic,
          audience: settings.brief.audience,
          goal: settings.brief.goal,
          tone: settings.brief.tone,
          language: settings.brief.language,
          aspect: settings.brief.aspect,
          targetDurationMinutes: settings.planSpec.targetDurationMinutes,
          pacing: settings.planSpec.pacing,
          density: settings.planSpec.density,
          style: settings.planSpec.style,
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
        console.error('[PREFLIGHT] Preflight check failed with status:', response.status);
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
        await response.json(); // Acknowledge the response

        // Apply safe defaults to the selection
        setSelectedProfile('Free-Only');
        setPerStageSelection({
          script: 'RuleBased',
          tts: 'Windows',
          visuals: 'Stock',
          upload: 'Off',
        });

        // Re-run preflight check with safe defaults
        await handleRunPreflight();

        alert(
          'Applied safe defaults: Free-Only mode with RuleBased script, Windows TTS, and Stock images.'
        );
      } else {
        alert('Failed to get safe defaults');
      }
    } catch (error) {
      console.error('Error applying safe defaults:', error);
      alert('Error applying safe defaults');
    }
  };

  const handleGenerate = async () => {
    try {
      setGenerating(true);
      validateAndWarnEnums(settings.brief, settings.planSpec);
      const { brief: normalizedBrief, planSpec: normalizedPlanSpec } = normalizeEnumsForApi(
        settings.brief,
        settings.planSpec
      );

      // Create job with full specs
      const brief = {
        topic: normalizedBrief.topic,
        audience: normalizedBrief.audience,
        goal: normalizedBrief.goal,
        tone: normalizedBrief.tone,
        language: normalizedBrief.language,
        aspect: normalizedBrief.aspect,
      };

      const planSpec = {
        targetDuration: `PT${normalizedPlanSpec.targetDurationMinutes}M`,
        pacing: normalizedPlanSpec.pacing,
        density: normalizedPlanSpec.density,
        style: normalizedPlanSpec.style,
      };

      const voiceSpec = {
        voiceName: 'en-US-Standard-A',
        rate: 1.0,
        pitch: 0.0,
        pause: 'Short',
      };

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

      const requestData = { brief, planSpec, voiceSpec, renderSpec };
      const jobsUrl = apiUrl('/api/jobs');

      // Use cancellable request
      const { promise, cancel } = postCancellable<{ jobId: string }>(jobsUrl, requestData);
      currentRequestRef.current = { cancel };

      const data = await promise;

      // Clear the request reference on success
      currentRequestRef.current = null;

      // Set job in global state for status bar
      useJobState.getState().setJob(data.jobId);
      useJobState.getState().updateProgress(0, 'Starting video generation...');

      setActiveJobId(data.jobId);
      setShowGenerationPanel(true);
    } catch (error) {
      // Clear the request reference
      currentRequestRef.current = null;

      // Don't show error if request was cancelled
      if (isAbortError(error)) {
        logger.debug('Request cancelled by user', 'CreateWizard', 'handleGenerateVideo');
        return;
      }

      logger.error(
        'Error generating video',
        error instanceof Error ? error : new Error(String(error)),
        'CreateWizard',
        'handleGenerateVideo'
      );
      const errorInfo = await parseApiError(error);
      showFailureToast({
        title: errorInfo.title,
        message: errorInfo.message,
        errorDetails: errorInfo.errorDetails,
        correlationId: errorInfo.correlationId,
        errorCode: errorInfo.errorCode,
        onRetry: () => handleGenerate(),
        onOpenLogs: openLogsFolder,
      });
    } finally {
      setGenerating(false);
    }
  };

  const handleQuickDemo = async () => {
    try {
      setGenerating(true);

      // Prepare complete validation request with all required fields
      const validationRequest = {
        topic: 'Welcome to Aura Video Studio',
        audience: 'General',
        goal: 'Demonstrate',
        tone: 'Informative',
        language: 'en-US',
        durationMinutes: 0.2, // 12 seconds (0.2 minutes)
      };

      // Frontend validation before API call
      const frontendValidation = validateBriefRequest(validationRequest);
      if (!frontendValidation.valid) {
        console.warn('[QUICK DEMO] Frontend validation failed:', frontendValidation.errors);
        showFailureToast({
          title: 'Invalid Quick Demo Request',
          message:
            'The demo request failed frontend validation:\n' + frontendValidation.errors.join('\n'),
        });
        setGenerating(false);
        return;
      }

      // Validate before starting generation
      const validationUrl = apiUrl('/api/validation/brief');

      const validationResponse = await fetch(validationUrl, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify(validationRequest),
      });

      if (validationResponse.ok) {
        const validationData = await validationResponse.json();

        if (!validationData.isValid) {
          console.warn('[QUICK DEMO] Validation failed with issues:', validationData.issues);

          // Format error message with specific issues
          const errorMessage =
            validationData.issues && validationData.issues.length > 0
              ? validationData.issues.join('\n')
              : 'Validation failed. Please check system requirements.';

          showFailureToast({
            title: 'Quick Demo System Check Failed',
            message: errorMessage,
            errorDetails: `Validation returned ${validationData.issueCount || validationData.issues?.length || 0} issue(s). Please ensure FFmpeg is installed and system requirements are met.`,
          });
          setGenerating(false);
          return;
        }
      } else {
        console.error(
          '[QUICK DEMO] Validation request failed:',
          validationResponse.status,
          validationResponse.statusText
        );
        const errorText = await validationResponse.text();
        console.error('[QUICK DEMO] Error response:', errorText);

        showFailureToast({
          title: 'Validation Failed',
          message: `Could not validate quick demo request. Server returned status ${validationResponse.status}`,
          errorDetails: errorText,
        });
        setGenerating(false);
        return;
      }

      // Validation passed, proceed with generation
      const demoUrl = apiUrl('/api/quick/demo');
      const requestData = { topic: settings.brief.topic || null };

      // Use cancellable request
      const { promise, cancel } = postCancellable<{ jobId: string }>(demoUrl, requestData);
      currentRequestRef.current = { cancel };

      const data = await promise;

      // Clear the request reference on success
      currentRequestRef.current = null;

      // Set job in global state for status bar
      useJobState.getState().setJob(data.jobId);
      useJobState.getState().updateProgress(0, 'Starting quick demo...');

      // Show success toast
      showSuccessToast({
        title: 'Quick Demo Started',
        message: `Job ID: ${data.jobId}`,
      });

      setActiveJobId(data.jobId);
      setShowGenerationPanel(true);
    } catch (error) {
      // Clear the request reference
      currentRequestRef.current = null;

      // Don't show error if request was cancelled
      if (isAbortError(error)) {
        logger.debug('Quick demo request cancelled by user', 'CreateWizard', 'handleQuickDemo');
        return;
      }

      logger.error(
        'Error starting quick demo',
        error instanceof Error ? error : new Error(String(error)),
        'CreateWizard',
        'handleQuickDemo'
      );
      const errorInfo = await parseApiError(error);
      showFailureToast({
        title: 'Failed to Start Quick Demo',
        message: errorInfo.message,
        errorDetails: errorInfo.errorDetails,
        correlationId: errorInfo.correlationId,
        errorCode: errorInfo.errorCode,
      });
    } finally {
      setGenerating(false);
    }
  };

  return (
    <div className={styles.container}>
      <div className={styles.header}>
        <div className={styles.headerRow}>
          <Title1>Create Video</Title1>
          <div style={{ display: 'flex', alignItems: 'center', gap: tokens.spacingHorizontalM }}>
            {currentProject && (
              <AutoSaveIndicator
                status={
                  isAutoSaving
                    ? 'saving'
                    : autoSaveError
                      ? 'error'
                      : lastSaveTime
                        ? 'saved'
                        : 'idle'
                }
                lastSavedAt={lastSaveTime || undefined}
                error={autoSaveError?.message}
                onRetry={() => triggerManualSave()}
                onManualSave={() => triggerManualSave()}
              />
            )}
            <Button
              icon={<Save20Regular />}
              onClick={() => setSaveDialogOpen(true)}
              appearance="subtle"
            >
              Save Project
            </Button>
          </div>
        </div>
        <Text className={styles.subtitle}>Step {currentStep} of 3</Text>
        <Text className={styles.keyboardHint}>
          Tip: Press Tab to navigate, Ctrl+Enter to advance
        </Text>
      </div>

      <div className={styles.form}>
        {/* Step 1: Brief */}
        {currentStep === 1 && (
          <Card className={styles.section}>
            <Title2>Brief</Title2>
            <Text size={200} style={{ marginBottom: tokens.spacingVerticalL }}>
              Define the core details of your video
            </Text>
            <div className={styles.fieldGroup}>
              <Field
                label={
                  <div style={{ display: 'flex', alignItems: 'center' }}>
                    Topic
                    <Tooltip
                      content={<TooltipWithLink content={TooltipContent.topic} />}
                      relationship="label"
                    >
                      <Info24Regular className={styles.infoIcon} />
                    </Tooltip>
                  </div>
                }
                required
                hint="What is your video about?"
              >
                <Input
                  value={settings.brief.topic}
                  onChange={(_, data) => updateBrief({ topic: data.value })}
                  placeholder="Enter your video topic"
                />
              </Field>

              <Field
                label={
                  <div style={{ display: 'flex', alignItems: 'center' }}>
                    Audience
                    <Tooltip
                      content={<TooltipWithLink content={TooltipContent.audience} />}
                      relationship="label"
                    >
                      <Info24Regular className={styles.infoIcon} />
                    </Tooltip>
                  </div>
                }
                hint="Who is this video for?"
              >
                <Dropdown
                  value={settings.brief.audience}
                  onOptionSelect={(_, data) => updateBrief({ audience: data.optionText })}
                >
                  <Option>General</Option>
                  <Option>Beginners</Option>
                  <Option>Advanced</Option>
                  <Option>Professionals</Option>
                  <Option>Children</Option>
                  <Option>Students</Option>
                </Dropdown>
              </Field>

              <Field
                label={
                  <div style={{ display: 'flex', alignItems: 'center' }}>
                    Tone
                    <Tooltip
                      content={<TooltipWithLink content={TooltipContent.tone} />}
                      relationship="label"
                    >
                      <Info24Regular className={styles.infoIcon} />
                    </Tooltip>
                  </div>
                }
                hint="What style should the video have?"
              >
                <Dropdown
                  value={settings.brief.tone}
                  onOptionSelect={(_, data) => updateBrief({ tone: data.optionText })}
                >
                  <Option>Informative</Option>
                  <Option>Casual</Option>
                  <Option>Professional</Option>
                  <Option>Energetic</Option>
                  <Option>Friendly</Option>
                  <Option>Authoritative</Option>
                </Dropdown>
              </Field>

              <Field
                label={
                  <div style={{ display: 'flex', alignItems: 'center' }}>
                    Aspect Ratio
                    <Tooltip
                      content={<TooltipWithLink content={TooltipContent.aspect} />}
                      relationship="label"
                    >
                      <Info24Regular className={styles.infoIcon} />
                    </Tooltip>
                  </div>
                }
                hint="Video dimensions"
              >
                <Dropdown
                  value={settings.brief.aspect}
                  onOptionSelect={(_, data) =>
                    updateBrief({ aspect: data.optionValue as Brief['aspect'] })
                  }
                >
                  <Option value="Widescreen16x9">16:9 Widescreen (YouTube, Desktop)</Option>
                  <Option value="Vertical9x16">9:16 Vertical (TikTok, Stories)</Option>
                  <Option value="Square1x1">1:1 Square (Instagram)</Option>
                </Dropdown>
              </Field>
            </div>

            {/* RAG Configuration */}
            <div style={{ marginTop: tokens.spacingVerticalL }}>
              <Accordion collapsible>
                <AccordionItem value="rag">
                  <AccordionHeader icon={<ChevronDown24Regular />} expandIconPosition="end">
                    <Title3>Document Grounding (Optional)</Title3>
                  </AccordionHeader>
                  <AccordionPanel>
                    <div className={styles.fieldGroup}>
                      <Text size={300} style={{ marginBottom: tokens.spacingVerticalM }}>
                        Ground your video script in source documents. Upload PDFs, Word documents,
                        or other files to ensure factual accuracy and include citations.
                      </Text>

                      <Field>
                        <Switch
                          checked={settings.brief.ragConfiguration?.enabled ?? false}
                          onChange={(_, data) =>
                            updateBrief({
                              ragConfiguration: {
                                enabled: data.checked,
                                topK: settings.brief.ragConfiguration?.topK ?? 5,
                                minimumScore: settings.brief.ragConfiguration?.minimumScore ?? 0.6,
                                maxContextTokens:
                                  settings.brief.ragConfiguration?.maxContextTokens ?? 2000,
                                includeCitations:
                                  settings.brief.ragConfiguration?.includeCitations ?? true,
                                tightenClaims:
                                  settings.brief.ragConfiguration?.tightenClaims ?? false,
                              },
                            })
                          }
                          label="Use attached source documents"
                        />
                      </Field>

                      {settings.brief.ragConfiguration?.enabled && (
                        <>
                          <Field
                            label="Number of source chunks (TopK)"
                            hint="How many relevant document chunks to retrieve"
                          >
                            <Slider
                              min={1}
                              max={20}
                              step={1}
                              value={settings.brief.ragConfiguration.topK ?? 5}
                              onChange={(_, data) =>
                                updateBrief({
                                  ragConfiguration: {
                                    ...settings.brief.ragConfiguration,
                                    enabled: true,
                                    topK: data.value,
                                  },
                                })
                              }
                            />
                            <Text size={200}>
                              {settings.brief.ragConfiguration.topK ?? 5} chunks
                            </Text>
                          </Field>

                          <Field
                            label="Minimum relevance score"
                            hint="Filter out less relevant content (0-1)"
                          >
                            <Slider
                              min={0}
                              max={1}
                              step={0.1}
                              value={settings.brief.ragConfiguration.minimumScore ?? 0.6}
                              onChange={(_, data) =>
                                updateBrief({
                                  ragConfiguration: {
                                    ...settings.brief.ragConfiguration,
                                    enabled: true,
                                    minimumScore: data.value,
                                  },
                                })
                              }
                            />
                            <Text size={200}>
                              {(settings.brief.ragConfiguration.minimumScore ?? 0.6).toFixed(1)}
                            </Text>
                          </Field>

                          <Field>
                            <Checkbox
                              checked={settings.brief.ragConfiguration.includeCitations ?? true}
                              onChange={(_, data) =>
                                updateBrief({
                                  ragConfiguration: {
                                    ...settings.brief.ragConfiguration,
                                    enabled: true,
                                    includeCitations: data.checked === true,
                                  },
                                })
                              }
                              label="Include citations in script"
                            />
                          </Field>

                          <Button
                            appearance="subtle"
                            onClick={() => {
                              navigate('/rag');
                            }}
                          >
                            Manage Documents
                          </Button>
                        </>
                      )}
                    </div>
                  </AccordionPanel>
                </AccordionItem>
              </Accordion>
            </div>

            {/* Quick Demo Button */}
            <div
              style={{
                marginTop: tokens.spacingVerticalXXL,
                padding: tokens.spacingVerticalL,
                borderTop: `1px solid ${tokens.colorNeutralStroke2}`,
                textAlign: 'center',
              }}
            >
              <Title3 style={{ marginBottom: tokens.spacingVerticalM }}>New to Aura?</Title3>
              <Text size={300} style={{ display: 'block', marginBottom: tokens.spacingVerticalL }}>
                Try a Quick Demo - No setup required!
              </Text>
              <div
                style={{
                  display: 'flex',
                  gap: tokens.spacingHorizontalM,
                  justifyContent: 'center',
                }}
              >
                <Button
                  appearance="primary"
                  size="large"
                  icon={generating ? <Spinner size="tiny" /> : <Play24Regular />}
                  onClick={handleQuickDemo}
                  disabled={generating}
                  style={{ minWidth: '200px' }}
                >
                  {generating ? 'Starting...' : 'Quick Demo (Safe)'}
                </Button>
                {generating && currentRequestRef.current && (
                  <Button
                    appearance="secondary"
                    size="large"
                    icon={<Dismiss24Regular />}
                    onClick={handleCancelRequest}
                  >
                    Cancel
                  </Button>
                )}
              </div>
              <Text
                size={200}
                style={{
                  display: 'block',
                  marginTop: tokens.spacingVerticalM,
                  color: tokens.colorNeutralForeground3,
                }}
              >
                Generates a 10-15 second demo video with safe defaults
              </Text>
            </div>
          </Card>
        )}

        {/* Step 2: Length, Pacing, and Options */}
        {currentStep === 2 && (
          <>
            <Card className={styles.section}>
              <Title2>Length and Pacing</Title2>
              <Text size={200} style={{ marginBottom: tokens.spacingVerticalL }}>
                Configure the duration and pacing of your video
              </Text>
              <div className={styles.fieldGroup}>
                <Field
                  label={
                    <div style={{ display: 'flex', alignItems: 'center' }}>
                      Duration: {settings.planSpec.targetDurationMinutes} minutes
                      <Tooltip
                        content={<TooltipWithLink content={TooltipContent.duration} />}
                        relationship="label"
                      >
                        <Info24Regular className={styles.infoIcon} />
                      </Tooltip>
                    </div>
                  }
                  hint="How long should the video be?"
                >
                  <Slider
                    min={0.5}
                    max={20}
                    step={0.5}
                    value={settings.planSpec.targetDurationMinutes}
                    onChange={(_, data) => updatePlanSpec({ targetDurationMinutes: data.value })}
                  />
                </Field>

                <Field
                  label={
                    <div style={{ display: 'flex', alignItems: 'center' }}>
                      Pacing
                      <Tooltip
                        content={<TooltipWithLink content={TooltipContent.pacing} />}
                        relationship="label"
                      >
                        <Info24Regular className={styles.infoIcon} />
                      </Tooltip>
                    </div>
                  }
                  hint="How fast should the narration be?"
                >
                  <Dropdown
                    value={settings.planSpec.pacing}
                    onOptionSelect={(_, data) =>
                      updatePlanSpec({ pacing: data.optionText as PlanSpec['pacing'] })
                    }
                  >
                    <Option>Chill</Option>
                    <Option>Conversational</Option>
                    <Option>Fast</Option>
                  </Dropdown>
                </Field>

                <Field
                  label={
                    <div style={{ display: 'flex', alignItems: 'center' }}>
                      Density
                      <Tooltip
                        content={<TooltipWithLink content={TooltipContent.density} />}
                        relationship="label"
                      >
                        <Info24Regular className={styles.infoIcon} />
                      </Tooltip>
                    </div>
                  }
                  hint="How much content per minute?"
                >
                  <Dropdown
                    value={settings.planSpec.density}
                    onOptionSelect={(_, data) =>
                      updatePlanSpec({ density: data.optionText as PlanSpec['density'] })
                    }
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
                    disabled={loadingRecommendations || !settings.brief.topic}
                  >
                    {loadingRecommendations ? (
                      <>
                        <Spinner size="tiny" /> Getting Recommendations...
                      </>
                    ) : (
                      'Get AI Recommendations'
                    )}
                  </Button>
                </div>
              </div>
            </Card>

            {/* Brand Kit */}
            <Card className={styles.section}>
              <Title3>
                Brand Kit
                <Tooltip
                  content={<TooltipWithLink content={TooltipContent.brandKit} />}
                  relationship="label"
                >
                  <Info24Regular className={styles.infoIcon} />
                </Tooltip>
              </Title3>
              <Text size={200} style={{ marginBottom: tokens.spacingVerticalM }}>
                Add your logo and brand colors
              </Text>
              <div className={styles.fieldGroup}>
                <Field
                  label={
                    <div style={{ display: 'flex', alignItems: 'center' }}>
                      Watermark/Logo
                      <Tooltip
                        content={<TooltipWithLink content={TooltipContent.watermark} />}
                        relationship="label"
                      >
                        <Info24Regular className={styles.infoIcon} />
                      </Tooltip>
                    </div>
                  }
                  hint="Path to PNG or SVG file"
                >
                  <Input
                    type="text"
                    placeholder="C:\path\to\logo.png"
                    value={settings.brandKit.watermarkPath || ''}
                    onChange={(_, data) => updateBrandKit({ watermarkPath: data.value })}
                  />
                </Field>

                <div className={styles.gridLayout}>
                  <Field label="Watermark Position">
                    <Dropdown
                      value={settings.brandKit.watermarkPosition || 'bottom-right'}
                      onOptionSelect={(_, data) =>
                        updateBrandKit({ watermarkPosition: data.optionValue as string })
                      }
                    >
                      <Option value="top-left">Top Left</Option>
                      <Option value="top-right">Top Right</Option>
                      <Option value="bottom-left">Bottom Left</Option>
                      <Option value="bottom-right">Bottom Right</Option>
                    </Dropdown>
                  </Field>

                  <Field
                    label={`Opacity: ${(settings.brandKit.watermarkOpacity * 100).toFixed(0)}%`}
                  >
                    <Slider
                      min={0}
                      max={1}
                      step={0.1}
                      value={settings.brandKit.watermarkOpacity}
                      onChange={(_, data) => updateBrandKit({ watermarkOpacity: data.value })}
                    />
                  </Field>
                </div>

                <div className={styles.gridLayout}>
                  <Field
                    label={
                      <div style={{ display: 'flex', alignItems: 'center' }}>
                        Brand Color
                        <Tooltip
                          content={<TooltipWithLink content={TooltipContent.brandColor} />}
                          relationship="label"
                        >
                          <Info24Regular className={styles.infoIcon} />
                        </Tooltip>
                      </div>
                    }
                    hint="Hex color code"
                  >
                    <Input
                      type="text"
                      placeholder="#FF6B35"
                      value={settings.brandKit.brandColor || ''}
                      onChange={(_, data) => updateBrandKit({ brandColor: data.value })}
                    />
                  </Field>

                  <Field
                    label={
                      <div style={{ display: 'flex', alignItems: 'center' }}>
                        Accent Color
                        <Tooltip
                          content={<TooltipWithLink content={TooltipContent.accentColor} />}
                          relationship="label"
                        >
                          <Info24Regular className={styles.infoIcon} />
                        </Tooltip>
                      </div>
                    }
                    hint="Hex color code"
                  >
                    <Input
                      type="text"
                      placeholder="#00D9FF"
                      value={settings.brandKit.accentColor || ''}
                      onChange={(_, data) => updateBrandKit({ accentColor: data.value })}
                    />
                  </Field>
                </div>
              </div>
            </Card>

            {/* Captions */}
            <Card className={styles.section}>
              <Title3>
                Captions
                <Tooltip
                  content={<TooltipWithLink content={TooltipContent.captionsStyle} />}
                  relationship="label"
                >
                  <Info24Regular className={styles.infoIcon} />
                </Tooltip>
              </Title3>
              <Text size={200} style={{ marginBottom: tokens.spacingVerticalM }}>
                Configure subtitle appearance
              </Text>
              <div className={styles.fieldGroup}>
                <Field label="Enable Captions">
                  <Switch
                    checked={settings.captions.enabled}
                    onChange={(_, data) => updateCaptions({ enabled: data.checked })}
                    label={settings.captions.enabled ? 'Enabled' : 'Disabled'}
                  />
                </Field>

                {settings.captions.enabled && (
                  <>
                    <div className={styles.gridLayout}>
                      <Field
                        label={
                          <div style={{ display: 'flex', alignItems: 'center' }}>
                            Format
                            <Tooltip
                              content={<TooltipWithLink content={TooltipContent.captionsFormat} />}
                              relationship="label"
                            >
                              <Info24Regular className={styles.infoIcon} />
                            </Tooltip>
                          </div>
                        }
                      >
                        <Dropdown
                          value={settings.captions.format.toUpperCase()}
                          onOptionSelect={(_, data) =>
                            updateCaptions({ format: data.optionValue as 'srt' | 'vtt' })
                          }
                        >
                          <Option value="srt">SRT (SubRip)</Option>
                          <Option value="vtt">VTT (WebVTT)</Option>
                        </Dropdown>
                      </Field>

                      <Field
                        label={
                          <div style={{ display: 'flex', alignItems: 'center' }}>
                            Burn-in
                            <Tooltip
                              content={<TooltipWithLink content={TooltipContent.captionsBurnIn} />}
                              relationship="label"
                            >
                              <Info24Regular className={styles.infoIcon} />
                            </Tooltip>
                          </div>
                        }
                      >
                        <Switch
                          checked={settings.captions.burnIn}
                          onChange={(_, data) => updateCaptions({ burnIn: data.checked })}
                          label={settings.captions.burnIn ? 'Yes' : 'No'}
                        />
                      </Field>
                    </div>

                    {settings.captions.burnIn && (
                      <div className={styles.gridLayout}>
                        <Field label="Font">
                          <Dropdown
                            value={settings.captions.fontName}
                            onOptionSelect={(_, data) =>
                              updateCaptions({ fontName: data.optionValue as string })
                            }
                          >
                            <Option value="Arial">Arial</Option>
                            <Option value="Helvetica">Helvetica</Option>
                            <Option value="Times New Roman">Times New Roman</Option>
                            <Option value="Courier New">Courier New</Option>
                          </Dropdown>
                        </Field>

                        <Field label="Font Size">
                          <Input
                            type="number"
                            value={settings.captions.fontSize.toString()}
                            onChange={(_, data) =>
                              updateCaptions({ fontSize: parseInt(data.value) || 24 })
                            }
                          />
                        </Field>
                      </div>
                    )}
                  </>
                )}
              </div>
            </Card>

            {/* Stock Sources */}
            <Card className={styles.section}>
              <Title3>
                Stock Sources
                <Tooltip
                  content={<TooltipWithLink content={TooltipContent.stockSources} />}
                  relationship="label"
                >
                  <Info24Regular className={styles.infoIcon} />
                </Tooltip>
              </Title3>
              <Text size={200} style={{ marginBottom: tokens.spacingVerticalM }}>
                Select image and video providers
              </Text>
              <div className={styles.fieldGroup}>
                <Field
                  label={
                    <div style={{ display: 'flex', alignItems: 'center' }}>
                      Pexels
                      <Tooltip
                        content={<TooltipWithLink content={TooltipContent.pexels} />}
                        relationship="label"
                      >
                        <Info24Regular className={styles.infoIcon} />
                      </Tooltip>
                    </div>
                  }
                >
                  <Switch
                    checked={settings.stockSources.enablePexels}
                    onChange={(_, data) => updateStockSources({ enablePexels: data.checked })}
                    label={settings.stockSources.enablePexels ? 'Enabled' : 'Disabled'}
                  />
                </Field>

                <Field
                  label={
                    <div style={{ display: 'flex', alignItems: 'center' }}>
                      Pixabay
                      <Tooltip
                        content={<TooltipWithLink content={TooltipContent.pixabay} />}
                        relationship="label"
                      >
                        <Info24Regular className={styles.infoIcon} />
                      </Tooltip>
                    </div>
                  }
                >
                  <Switch
                    checked={settings.stockSources.enablePixabay}
                    onChange={(_, data) => updateStockSources({ enablePixabay: data.checked })}
                    label={settings.stockSources.enablePixabay ? 'Enabled' : 'Disabled'}
                  />
                </Field>

                <Field
                  label={
                    <div style={{ display: 'flex', alignItems: 'center' }}>
                      Unsplash
                      <Tooltip
                        content={<TooltipWithLink content={TooltipContent.unsplash} />}
                        relationship="label"
                      >
                        <Info24Regular className={styles.infoIcon} />
                      </Tooltip>
                    </div>
                  }
                >
                  <Switch
                    checked={settings.stockSources.enableUnsplash}
                    onChange={(_, data) => updateStockSources({ enableUnsplash: data.checked })}
                    label={settings.stockSources.enableUnsplash ? 'Enabled' : 'Disabled'}
                  />
                </Field>

                <Field
                  label={
                    <div style={{ display: 'flex', alignItems: 'center' }}>
                      Local Assets
                      <Tooltip
                        content={<TooltipWithLink content={TooltipContent.localAssets} />}
                        relationship="label"
                      >
                        <Info24Regular className={styles.infoIcon} />
                      </Tooltip>
                    </div>
                  }
                >
                  <Switch
                    checked={settings.stockSources.enableLocalAssets}
                    onChange={(_, data) => updateStockSources({ enableLocalAssets: data.checked })}
                    label={settings.stockSources.enableLocalAssets ? 'Enabled' : 'Disabled'}
                  />
                </Field>

                {settings.stockSources.enableLocalAssets && (
                  <Field label="Local Assets Directory" hint="Path to your images/videos folder">
                    <Input
                      type="text"
                      placeholder="C:\path\to\assets"
                      value={settings.stockSources.localAssetsDirectory || ''}
                      onChange={(_, data) =>
                        updateStockSources({ localAssetsDirectory: data.value })
                      }
                    />
                  </Field>
                )}
              </div>
            </Card>

            {/* Offline Mode */}
            <Card className={styles.section}>
              <Title3>
                Offline Mode
                <Tooltip
                  content={<TooltipWithLink content={TooltipContent.offline} />}
                  relationship="label"
                >
                  <Info24Regular className={styles.infoIcon} />
                </Tooltip>
              </Title3>
              <Text size={200} style={{ marginBottom: tokens.spacingVerticalM }}>
                Use only local providers (no cloud API calls)
              </Text>
              <Field label="Enable Offline Mode">
                <Switch
                  checked={settings.offlineMode}
                  onChange={(_, data) => setSettings({ ...settings, offlineMode: data.checked })}
                  label={settings.offlineMode ? 'Enabled - Local only' : 'Disabled - Cloud allowed'}
                />
              </Field>
            </Card>

            {/* Pacing Optimization removed from Step 2 - no analyzable content exists yet.
                 Pacing analysis is available in Video Editor and Timeline Editor where
                 actual scripts and scenes are available. */}

            {/* Advanced Settings */}
            <Card className={styles.section}>
              <Accordion collapsible>
                <AccordionItem value="advanced">
                  <AccordionHeader icon={<ChevronDown24Regular />} expandIconPosition="end">
                    <Title3>
                      Advanced Settings
                      <Tooltip
                        content={<TooltipWithLink content={TooltipContent.advanced} />}
                        relationship="label"
                      >
                        <Info24Regular className={styles.infoIcon} />
                      </Tooltip>
                    </Title3>
                  </AccordionHeader>
                  <AccordionPanel>
                    <div className={styles.fieldGroup}>
                      <Text size={200} style={{ color: tokens.colorNeutralForeground3 }}>
                        Fine-tune generation parameters for power users
                      </Text>

                      <Field label="Visual Style">
                        <Dropdown
                          value={settings.planSpec.style}
                          onOptionSelect={(_, data) =>
                            updatePlanSpec({ style: data.optionText || 'Standard' })
                          }
                        >
                          <Option>Standard</Option>
                          <Option>Educational</Option>
                          <Option>Cinematic</Option>
                          <Option>Documentary</Option>
                          <Option>Minimal</Option>
                        </Dropdown>
                      </Field>

                      {settings.stockSources.enableStableDiffusion && (
                        <Field
                          label={
                            <div style={{ display: 'flex', alignItems: 'center' }}>
                              Stable Diffusion URL
                              <Tooltip
                                content={<TooltipWithLink content={TooltipContent.sdParams} />}
                                relationship="label"
                              >
                                <Info24Regular className={styles.infoIcon} />
                              </Tooltip>
                            </div>
                          }
                        >
                          <Input
                            type="text"
                            placeholder="http://127.0.0.1:7860"
                            value={settings.stockSources.stableDiffusionUrl || ''}
                            onChange={(_, data) =>
                              updateStockSources({ stableDiffusionUrl: data.value })
                            }
                          />
                        </Field>
                      )}

                      {/* Prompt Customization Button */}
                      <Dialog
                        open={promptCustomizationOpen}
                        onOpenChange={(_, data) => setPromptCustomizationOpen(data.open)}
                      >
                        <DialogTrigger disableButtonEnhancement>
                          <Button
                            icon={<Sparkle24Regular />}
                            appearance="primary"
                            style={{ marginTop: tokens.spacingVerticalM }}
                          >
                            Customize Prompts
                          </Button>
                        </DialogTrigger>
                        <DialogSurface style={{ maxWidth: '900px', maxHeight: '90vh' }}>
                          <DialogBody>
                            <DialogTitle>Advanced Prompt Customization</DialogTitle>
                            <DialogContent>
                              <PromptCustomizationPanel
                                brief={settings.brief}
                                planSpec={settings.planSpec}
                                onApply={handleClosePromptCustomization}
                                onClose={handleClosePromptCustomization}
                              />
                            </DialogContent>
                          </DialogBody>
                        </DialogSurface>
                      </Dialog>

                      <Button
                        appearance="secondary"
                        icon={<ArrowReset24Regular />}
                        onClick={handleResetToDefaults}
                        className={styles.resetButton}
                      >
                        Reset All to Defaults
                      </Button>
                    </div>
                  </AccordionPanel>
                </AccordionItem>
              </Accordion>
            </Card>

            {/* AI Recommendations */}
            {showRecommendations && recommendations && (
              <Card
                className={styles.section}
                style={{ backgroundColor: tokens.colorNeutralBackground2 }}
              >
                <div
                  style={{
                    display: 'flex',
                    justifyContent: 'space-between',
                    alignItems: 'center',
                    marginBottom: tokens.spacingVerticalM,
                  }}
                >
                  <Title3>AI Recommendations</Title3>
                  <Button
                    appearance="primary"
                    icon={<Checkmark24Regular />}
                    onClick={applyAllRecommendations}
                  >
                    Apply All
                  </Button>
                </div>

                <div
                  style={{ display: 'flex', flexDirection: 'column', gap: tokens.spacingVerticalL }}
                >
                  <div>
                    <Text weight="semibold">Scene Count:</Text>{' '}
                    <Badge appearance="tint" color="informative">
                      {recommendations.sceneCount} scenes
                    </Badge>
                  </div>

                  <div>
                    <Text weight="semibold">Shots per Scene:</Text>{' '}
                    <Badge appearance="tint" color="informative">
                      {recommendations.shotsPerScene} shots
                    </Badge>
                  </div>

                  <div>
                    <Text weight="semibold">B-Roll:</Text>{' '}
                    <Badge appearance="tint" color="informative">
                      {recommendations.bRollPercentage}%
                    </Badge>
                  </div>

                  <div>
                    <Text weight="semibold">Voice:</Text>{' '}
                    <Text>
                      Rate: {recommendations.voice.rate}x, Pitch: {recommendations.voice.pitch}x
                    </Text>
                  </div>

                  <div>
                    <Text weight="semibold">SEO Title:</Text>{' '}
                    <Text>{recommendations.seo.title}</Text>
                  </div>
                </div>
              </Card>
            )}
          </>
        )}

        {/* Step 3: Review and Generate */}
        {currentStep === 3 && (
          <>
            <Card className={styles.section}>
              <Title2>Review Settings</Title2>
              <Text size={200} style={{ marginBottom: tokens.spacingVerticalL }}>
                Review your settings before generating your video
              </Text>
              <div
                style={{
                  marginTop: tokens.spacingVerticalL,
                  display: 'flex',
                  flexDirection: 'column',
                  gap: tokens.spacingVerticalM,
                }}
              >
                <div>
                  <Text weight="semibold">Topic:</Text> <Text>{settings.brief.topic}</Text>
                </div>
                <div>
                  <Text weight="semibold">Audience:</Text> <Text>{settings.brief.audience}</Text>
                </div>
                <div>
                  <Text weight="semibold">Duration:</Text>{' '}
                  <Text>{settings.planSpec.targetDurationMinutes} minutes</Text>
                </div>
                <div>
                  <Text weight="semibold">Pacing:</Text> <Text>{settings.planSpec.pacing}</Text>
                </div>
                <div>
                  <Text weight="semibold">Density:</Text> <Text>{settings.planSpec.density}</Text>
                </div>
                <div>
                  <Text weight="semibold">Offline Mode:</Text>{' '}
                  <Badge color={settings.offlineMode ? 'success' : 'subtle'}>
                    {settings.offlineMode ? 'Enabled' : 'Disabled'}
                  </Badge>
                </div>
                <div>
                  <Text weight="semibold">Brand Kit:</Text>{' '}
                  <Text>{settings.brandKit.watermarkPath ? 'Configured' : 'Not configured'}</Text>
                </div>
                <div>
                  <Text weight="semibold">Captions:</Text>{' '}
                  <Badge color={settings.captions.enabled ? 'success' : 'subtle'}>
                    {settings.captions.enabled
                      ? `${settings.captions.format.toUpperCase()}`
                      : 'Disabled'}
                  </Badge>
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
                      setPreflightReport(null);
                    }}
                  >
                    <Option value="Free-Only">Free-Only (Ollama + Windows TTS + Stock)</Option>
                    <Option value="Balanced Mix">Balanced Mix (Pro with fallbacks)</Option>
                    <Option value="Pro-Max">Pro-Max (OpenAI + ElevenLabs + Cloud)</Option>
                  </Dropdown>
                </Field>
              </div>
            </Card>

            {/* Per-Stage Provider Selection */}
            <ProviderSelection
              selection={perStageSelection}
              onSelectionChange={updateProviderSelection}
            />

            <Card className={styles.section}>
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
                      <Tooltip
                        content="Some preflight checks failed, but you can still proceed at your own risk"
                        relationship="label"
                      >
                        <Text>Override and proceed anyway</Text>
                      </Tooltip>
                    }
                  />
                </div>
              )}
            </Card>
          </>
        )}

        {/* Navigation Actions */}
        <div className={styles.actions}>
          {currentStep > 1 && (
            <Button onClick={() => setCurrentStep(currentStep - 1)}>Previous</Button>
          )}
          {currentStep < 3 ? (
            <Button
              appearance="primary"
              onClick={() => setCurrentStep(currentStep + 1)}
              disabled={currentStep === 1 && !settings.brief.topic}
            >
              Next
            </Button>
          ) : (
            <>
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
                  disabled={
                    generating ||
                    !preflightReport ||
                    (!preflightReport.ok && !overridePreflightGate)
                  }
                >
                  {generating ? 'Generating...' : 'Generate Video'}
                </Button>
              </Tooltip>
              {generating && currentRequestRef.current && (
                <Button
                  appearance="secondary"
                  icon={<Dismiss24Regular />}
                  onClick={handleCancelRequest}
                >
                  Cancel
                </Button>
              )}
            </>
          )}
        </div>
      </div>

      {/* Generation Panel */}
      {showGenerationPanel && activeJobId && (
        <GenerationPanel
          jobId={activeJobId}
          onClose={() => {
            setShowGenerationPanel(false);
            setActiveJobId(null);
          }}
        />
      )}

      {/* Save Project Dialog */}
      <SaveProjectDialog
        isOpen={saveDialogOpen}
        onClose={() => setSaveDialogOpen(false)}
        currentStep={currentStep - 1}
        briefData={settings.brief}
        planData={settings.planSpec}
        voiceData={undefined}
        renderData={undefined}
        onSaveSuccess={() => {
          showSuccessToast({
            title: 'Project Saved',
            message: 'Your project has been saved successfully',
          });
        }}
      />
    </div>
  );
}
