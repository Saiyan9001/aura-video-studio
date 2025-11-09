import {
  makeStyles,
  tokens,
  Title1,
  Text,
  Button,
  Card,
  Switch,
  Tooltip,
  Dialog,
  DialogSurface,
  DialogTitle,
  DialogBody,
  DialogContent,
  DialogActions,
} from '@fluentui/react-components';
import {
  ArrowLeft24Regular,
  ArrowRight24Regular,
  Save24Regular,
  Settings24Regular,
  DismissCircle24Regular,
  Lightbulb24Regular,
  DocumentMultiple24Regular,
  Clock24Regular,
} from '@fluentui/react-icons';
import { useState, useEffect, useCallback, useRef } from 'react';
import type { FC } from 'react';
import { useNavigate } from 'react-router-dom';
import { WizardProgress } from '../WizardProgress';
import { CostEstimator } from './CostEstimator';
import { DraftManager } from './DraftManager';
import { BriefInput } from './steps/BriefInput';
import { FinalExport } from './steps/FinalExport';
import { PreviewGeneration } from './steps/PreviewGeneration';
import { ScriptReview } from './steps/ScriptReview';
import { StyleSelection } from './steps/StyleSelection';
import type { WizardData, StepValidation, VideoTemplate, WizardDraft } from './types';
import { VideoTemplates } from './VideoTemplates';

const useStyles = makeStyles({
  container: {
    maxWidth: '1200px',
    margin: '0 auto',
    padding: tokens.spacingVerticalXL,
    display: 'flex',
    flexDirection: 'column',
    gap: tokens.spacingVerticalXL,
    minHeight: '100vh',
  },
  header: {
    display: 'flex',
    justifyContent: 'space-between',
    alignItems: 'center',
    marginBottom: tokens.spacingVerticalL,
  },
  headerLeft: {
    display: 'flex',
    flexDirection: 'column',
    gap: tokens.spacingVerticalXS,
  },
  headerRight: {
    display: 'flex',
    alignItems: 'center',
    gap: tokens.spacingHorizontalM,
  },
  progressSection: {
    marginBottom: tokens.spacingVerticalXL,
  },
  contentCard: {
    padding: tokens.spacingVerticalXXL,
    flex: 1,
  },
  navigationBar: {
    display: 'flex',
    justifyContent: 'space-between',
    alignItems: 'center',
    paddingTop: tokens.spacingVerticalL,
    borderTop: `1px solid ${tokens.colorNeutralStroke2}`,
  },
  navigationButtons: {
    display: 'flex',
    gap: tokens.spacingHorizontalM,
  },
  advancedToggle: {
    display: 'flex',
    alignItems: 'center',
    gap: tokens.spacingHorizontalS,
  },
  keyboardHint: {
    fontSize: tokens.fontSizeBase200,
    color: tokens.colorNeutralForeground3,
  },
  timeEstimate: {
    display: 'flex',
    alignItems: 'center',
    gap: tokens.spacingHorizontalS,
    color: tokens.colorNeutralForeground2,
  },
  autoSaveIndicator: {
    display: 'flex',
    alignItems: 'center',
    gap: tokens.spacingHorizontalXS,
    padding: `${tokens.spacingVerticalXS} ${tokens.spacingHorizontalS}`,
    backgroundColor: tokens.colorNeutralBackground2,
    borderRadius: tokens.borderRadiusMedium,
  },
});

const STEP_LABELS = ['Brief', 'Style', 'Script', 'Preview', 'Export'];
const STEP_TIME_ESTIMATES = ['2 min', '3 min', '5 min', '3 min', '2 min'];
const STORAGE_KEY = 'aura-wizard-data';
const ADVANCED_MODE_KEY = 'aura-wizard-advanced-mode';
const AUTO_SAVE_INTERVAL = 30000;

export const VideoCreationWizard: FC = () => {
  const styles = useStyles();
  const navigate = useNavigate();
  const [currentStep, setCurrentStep] = useState(0);
  const [advancedMode, setAdvancedMode] = useState(() => {
    return localStorage.getItem(ADVANCED_MODE_KEY) === 'true';
  });
  const [showSaveDialog, setShowSaveDialog] = useState(false);
  const [showDraftManager, setShowDraftManager] = useState(false);
  const [lastSaved, setLastSaved] = useState<Date | null>(null);
  const autoSaveTimerRef = useRef<number | null>(null);
  const [showTemplates, setShowTemplates] = useState(false);
  const [wizardData, setWizardData] = useState<WizardData>(() => {
    const saved = localStorage.getItem(STORAGE_KEY);
    if (saved) {
      try {
        return JSON.parse(saved);
      } catch (error) {
        console.error('Failed to parse saved wizard data:', error);
      }
    }
    return {
      brief: {
        topic: '',
        videoType: 'educational',
        targetAudience: '',
        keyMessage: '',
        duration: 60,
      },
      style: {
        voiceProvider: 'ElevenLabs',
        voiceName: '',
        visualStyle: 'modern',
        musicGenre: 'ambient',
        musicEnabled: true,
      },
      script: {
        content: '',
        scenes: [],
        generatedAt: null,
      },
      preview: {
        thumbnails: [],
        audioSamples: [],
      },
      export: {
        quality: 'high',
        format: 'mp4',
        resolution: '1080p',
        includeCaptions: true,
      },
      advanced: {
        seoKeywords: [],
        targetPlatform: 'youtube',
        customTransitions: false,
      },
    };
  });

  const [stepValidation, setStepValidation] = useState<StepValidation[]>([
    { isValid: false, errors: [] },
    { isValid: false, errors: [] },
    { isValid: false, errors: [] },
    { isValid: false, errors: [] },
    { isValid: false, errors: [] },
  ]);

  // Save wizard data to localStorage whenever it changes
  useEffect(() => {
    localStorage.setItem(STORAGE_KEY, JSON.stringify(wizardData));
    setLastSaved(new Date());
  }, [wizardData]);

  // Auto-save every 30 seconds
  useEffect(() => {
    autoSaveTimerRef.current = window.setInterval(() => {
      localStorage.setItem(STORAGE_KEY, JSON.stringify(wizardData));
      setLastSaved(new Date());
    }, AUTO_SAVE_INTERVAL);

    return () => {
      if (autoSaveTimerRef.current) {
        window.clearInterval(autoSaveTimerRef.current);
      }
    };
  }, [wizardData]);

  // Save advanced mode preference
  useEffect(() => {
    localStorage.setItem(ADVANCED_MODE_KEY, String(advancedMode));
  }, [advancedMode]);

  // Keyboard navigation
  useEffect(() => {
    const handleKeyDown = (e: KeyboardEvent) => {
      if (e.ctrlKey && e.key === 'Enter' && !e.shiftKey) {
        e.preventDefault();
        if (currentStep < STEP_LABELS.length - 1 && stepValidation[currentStep].isValid) {
          setCurrentStep((prev) => prev + 1);
          window.scrollTo({ top: 0, behavior: 'smooth' });
        }
      } else if (e.ctrlKey && e.shiftKey && e.key === 'Enter') {
        e.preventDefault();
        if (currentStep > 0) {
          setCurrentStep((prev) => prev - 1);
          window.scrollTo({ top: 0, behavior: 'smooth' });
        }
      } else if (e.key === 'Escape') {
        e.preventDefault();
        setShowSaveDialog(true);
      }
    };

    window.addEventListener('keydown', handleKeyDown);
    return () => window.removeEventListener('keydown', handleKeyDown);
  }, [currentStep, stepValidation]);

  const updateWizardData = useCallback((updates: Partial<WizardData>) => {
    setWizardData((prev) => ({ ...prev, ...updates }));
  }, []);

  const updateStepValidation = useCallback((step: number, validation: StepValidation) => {
    setStepValidation((prev) => {
      const updated = [...prev];
      updated[step] = validation;
      return updated;
    });
  }, []);

  const handleNext = useCallback(() => {
    if (currentStep < STEP_LABELS.length - 1 && stepValidation[currentStep].isValid) {
      setCurrentStep((prev) => prev + 1);
      window.scrollTo({ top: 0, behavior: 'smooth' });
    }
  }, [currentStep, stepValidation]);

  const handlePrevious = useCallback(() => {
    if (currentStep > 0) {
      setCurrentStep((prev) => prev - 1);
      window.scrollTo({ top: 0, behavior: 'smooth' });
    }
  }, [currentStep]);

  const handleStepClick = useCallback((step: number) => {
    setCurrentStep(step);
    window.scrollTo({ top: 0, behavior: 'smooth' });
  }, []);

  const handleSaveAndExit = useCallback(() => {
    localStorage.setItem(STORAGE_KEY, JSON.stringify(wizardData));
    navigate('/');
  }, [wizardData, navigate]);

  const handleClearAndExit = useCallback(() => {
    localStorage.removeItem(STORAGE_KEY);
    setShowSaveDialog(false);
    navigate('/');
  }, [navigate]);

  const handleSelectTemplate = useCallback((template: VideoTemplate) => {
    setWizardData((prev) => ({
      ...prev,
      ...template.defaultData,
      brief: {
        ...prev.brief,
        ...template.defaultData.brief,
      },
      style: {
        ...prev.style,
        ...template.defaultData.style,
      },
      advanced: {
        ...prev.advanced,
        ...template.defaultData.advanced,
      },
    }));
    setShowTemplates(false);
  }, []);

  const handleLoadDraft = useCallback((draft: WizardDraft) => {
    setWizardData(draft.data);
    setCurrentStep(draft.currentStep);
    window.scrollTo({ top: 0, behavior: 'smooth' });
  }, []);

  const getTimeSinceLastSave = useCallback(() => {
    if (!lastSaved) return '';
    const seconds = Math.floor((Date.now() - lastSaved.getTime()) / 1000);
    if (seconds < 60) return 'Just now';
    const minutes = Math.floor(seconds / 60);
    if (minutes === 1) return '1 minute ago';
    return `${minutes} minutes ago`;
  }, [lastSaved]);

  const renderStepContent = () => {
    switch (currentStep) {
      case 0:
        return (
          <BriefInput
            data={wizardData.brief}
            advancedMode={advancedMode}
            advancedData={wizardData.advanced}
            onChange={(brief) => updateWizardData({ brief })}
            onAdvancedChange={(advanced) => updateWizardData({ advanced })}
            onValidationChange={(validation) => updateStepValidation(0, validation)}
          />
        );
      case 1:
        return (
          <StyleSelection
            data={wizardData.style}
            briefData={wizardData.brief}
            advancedMode={advancedMode}
            onChange={(style) => updateWizardData({ style })}
            onValidationChange={(validation) => updateStepValidation(1, validation)}
          />
        );
      case 2:
        return (
          <ScriptReview
            data={wizardData.script}
            briefData={wizardData.brief}
            styleData={wizardData.style}
            advancedMode={advancedMode}
            onChange={(script) => updateWizardData({ script })}
            onValidationChange={(validation) => updateStepValidation(2, validation)}
          />
        );
      case 3:
        return (
          <PreviewGeneration
            data={wizardData.preview}
            scriptData={wizardData.script}
            styleData={wizardData.style}
            advancedMode={advancedMode}
            onChange={(preview) => updateWizardData({ preview })}
            onValidationChange={(validation) => updateStepValidation(3, validation)}
          />
        );
      case 4:
        return (
          <FinalExport
            data={wizardData.export}
            wizardData={wizardData}
            advancedMode={advancedMode}
            onChange={(exportData) => updateWizardData({ export: exportData })}
            onValidationChange={(validation) => updateStepValidation(4, validation)}
          />
        );
      default:
        return null;
    }
  };

  return (
    <div className={styles.container}>
      <div className={styles.header}>
        <div className={styles.headerLeft}>
          <Title1>Create Video</Title1>
          <Text className={styles.keyboardHint}>
            Use Tab to navigate, Ctrl+Enter to continue, Escape to save and exit
          </Text>
          {lastSaved && (
            <div className={styles.autoSaveIndicator}>
              <Clock24Regular style={{ fontSize: '14px', color: tokens.colorBrandForeground1 }} />
              <Text size={200}>Auto-saved {getTimeSinceLastSave()}</Text>
            </div>
          )}
        </div>
        <div className={styles.headerRight}>
          <Tooltip content="Browse video templates" relationship="label">
            <Button
              appearance="secondary"
              icon={<Lightbulb24Regular />}
              onClick={() => setShowTemplates(true)}
            >
              Templates
            </Button>
          </Tooltip>
          <Tooltip content="Manage drafts" relationship="label">
            <Button
              appearance="secondary"
              icon={<DocumentMultiple24Regular />}
              onClick={() => setShowDraftManager(true)}
            >
              Drafts
            </Button>
          </Tooltip>
          <div className={styles.advancedToggle}>
            <Settings24Regular />
            <Switch
              checked={advancedMode}
              onChange={(_, data) => setAdvancedMode(data.checked)}
              label="Advanced Mode"
            />
          </div>
          <CostEstimator wizardData={wizardData} />
          <Tooltip content="Save progress and exit" relationship="label">
            <Button
              appearance="subtle"
              icon={<Save24Regular />}
              onClick={() => setShowSaveDialog(true)}
            >
              Save & Exit
            </Button>
          </Tooltip>
        </div>
      </div>

      <div className={styles.progressSection}>
        <WizardProgress
          currentStep={currentStep}
          totalSteps={STEP_LABELS.length}
          stepLabels={STEP_LABELS}
          onStepClick={handleStepClick}
          onSaveAndExit={() => setShowSaveDialog(true)}
        />
        <div
          className={styles.timeEstimate}
          style={{ marginTop: tokens.spacingVerticalM, textAlign: 'center' }}
        >
          <Text size={200}>Estimated time for this step: {STEP_TIME_ESTIMATES[currentStep]}</Text>
        </div>
      </div>

      <Card className={styles.contentCard}>{renderStepContent()}</Card>

      <div className={styles.navigationBar}>
        <div className={styles.navigationButtons}>
          {currentStep > 0 && (
            <Button appearance="secondary" icon={<ArrowLeft24Regular />} onClick={handlePrevious}>
              Previous
            </Button>
          )}
        </div>
        <div className={styles.navigationButtons}>
          {currentStep < STEP_LABELS.length - 1 ? (
            <Tooltip
              content={
                !stepValidation[currentStep].isValid
                  ? `Please complete all required fields: ${stepValidation[currentStep].errors.join(', ')}`
                  : ''
              }
              relationship="label"
            >
              <Button
                appearance="primary"
                icon={<ArrowRight24Regular />}
                iconPosition="after"
                onClick={handleNext}
                disabled={!stepValidation[currentStep].isValid}
              >
                Next
              </Button>
            </Tooltip>
          ) : (
            <Tooltip
              content={
                !stepValidation[currentStep].isValid
                  ? 'Please complete all required fields before generating'
                  : 'Ready to generate your video!'
              }
              relationship="label"
            >
              <Button
                appearance="primary"
                onClick={handleSaveAndExit}
                disabled={!stepValidation[currentStep].isValid}
              >
                Generate Video
              </Button>
            </Tooltip>
          )}
        </div>
      </div>

      <Dialog open={showSaveDialog} onOpenChange={(_, data) => setShowSaveDialog(data.open)}>
        <DialogSurface>
          <DialogBody>
            <DialogTitle>Save Progress?</DialogTitle>
            <DialogContent>
              <Text>Do you want to save your progress? You can resume this wizard later.</Text>
            </DialogContent>
            <DialogActions>
              <Button appearance="secondary" onClick={() => setShowSaveDialog(false)}>
                Cancel
              </Button>
              <Button
                appearance="secondary"
                icon={<DismissCircle24Regular />}
                onClick={handleClearAndExit}
              >
                Discard Progress
              </Button>
              <Button appearance="primary" icon={<Save24Regular />} onClick={handleSaveAndExit}>
                Save & Exit
              </Button>
            </DialogActions>
          </DialogBody>
        </DialogSurface>
      </Dialog>

      <Dialog open={showTemplates} onOpenChange={(_, data) => setShowTemplates(data.open)}>
        <DialogSurface style={{ maxWidth: '1200px', maxHeight: '90vh' }}>
          <DialogBody>
            <VideoTemplates
              onSelectTemplate={handleSelectTemplate}
              onClose={() => setShowTemplates(false)}
            />
          </DialogBody>
        </DialogSurface>
      </Dialog>

      <DraftManager
        open={showDraftManager}
        onClose={() => setShowDraftManager(false)}
        onLoadDraft={handleLoadDraft}
        currentData={wizardData}
        currentStep={currentStep}
      />
    </div>
  );
};
