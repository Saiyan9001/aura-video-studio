import {
  makeStyles,
  tokens,
  Dialog,
  DialogSurface,
  DialogBody,
  DialogTitle,
  DialogContent,
  DialogActions,
  Button,
  Card,
  Text,
  Badge,
  Input,
  Field,
  Title3,
  Spinner,
} from '@fluentui/react-components';
import {
  Delete24Regular,
  DocumentMultiple24Regular,
  Calendar24Regular,
  Checkmark24Regular,
} from '@fluentui/react-icons';
import { useState, useEffect, useCallback } from 'react';
import type { FC } from 'react';
import type { WizardData, WizardDraft, DraftMetadata } from './types';

const useStyles = makeStyles({
  container: {
    display: 'flex',
    flexDirection: 'column',
    gap: tokens.spacingVerticalL,
    padding: tokens.spacingVerticalL,
  },
  draftGrid: {
    display: 'grid',
    gridTemplateColumns: 'repeat(auto-fill, minmax(300px, 1fr))',
    gap: tokens.spacingHorizontalL,
    marginTop: tokens.spacingVerticalM,
  },
  draftCard: {
    padding: tokens.spacingVerticalL,
    cursor: 'pointer',
    transition: 'all 0.2s ease',
    ':hover': {
      transform: 'translateY(-2px)',
      boxShadow: tokens.shadow8,
    },
  },
  draftHeader: {
    display: 'flex',
    justifyContent: 'space-between',
    alignItems: 'flex-start',
    marginBottom: tokens.spacingVerticalM,
  },
  draftDetails: {
    display: 'flex',
    flexDirection: 'column',
    gap: tokens.spacingVerticalS,
  },
  draftActions: {
    display: 'flex',
    gap: tokens.spacingHorizontalS,
    marginTop: tokens.spacingVerticalM,
    justifyContent: 'flex-end',
  },
  emptyState: {
    display: 'flex',
    flexDirection: 'column',
    alignItems: 'center',
    justifyContent: 'center',
    gap: tokens.spacingVerticalL,
    padding: tokens.spacingVerticalXXXL,
    textAlign: 'center',
  },
  saveForm: {
    display: 'flex',
    flexDirection: 'column',
    gap: tokens.spacingVerticalL,
  },
});

interface DraftManagerProps {
  open: boolean;
  onClose: () => void;
  onLoadDraft: (draft: WizardDraft) => void;
  currentData?: WizardData;
  currentStep?: number;
}

const DRAFTS_STORAGE_KEY = 'aura-wizard-drafts';

export const DraftManager: FC<DraftManagerProps> = ({
  open,
  onClose,
  onLoadDraft,
  currentData,
  currentStep = 0,
}) => {
  const styles = useStyles();
  const [drafts, setDrafts] = useState<WizardDraft[]>([]);
  const [showSaveDialog, setShowSaveDialog] = useState(false);
  const [draftName, setDraftName] = useState('');
  const [loading, setLoading] = useState(false);

  const loadDrafts = useCallback(() => {
    setLoading(true);
    try {
      const stored = localStorage.getItem(DRAFTS_STORAGE_KEY);
      if (stored) {
        const parsed = JSON.parse(stored) as WizardDraft[];
        const sorted = parsed.sort(
          (a, b) => new Date(b.updatedAt).getTime() - new Date(a.updatedAt).getTime()
        );
        setDrafts(sorted);
      }
    } catch (error) {
      console.error('Failed to load drafts:', error);
    } finally {
      setLoading(false);
    }
  }, []);

  useEffect(() => {
    if (open) {
      loadDrafts();
    }
  }, [open, loadDrafts]);

  const saveDraft = useCallback(() => {
    if (!currentData || !draftName.trim()) return;

    const newDraft: WizardDraft = {
      id: `draft-${Date.now()}`,
      name: draftName.trim(),
      data: currentData,
      createdAt: new Date(),
      updatedAt: new Date(),
      currentStep,
      progress: ((currentStep + 1) / 5) * 100,
    };

    try {
      const stored = localStorage.getItem(DRAFTS_STORAGE_KEY);
      const existing = stored ? (JSON.parse(stored) as WizardDraft[]) : [];
      existing.push(newDraft);
      localStorage.setItem(DRAFTS_STORAGE_KEY, JSON.stringify(existing));
      setDrafts(existing);
      setShowSaveDialog(false);
      setDraftName('');
    } catch (error) {
      console.error('Failed to save draft:', error);
    }
  }, [currentData, currentStep, draftName]);

  const deleteDraft = useCallback((draftId: string) => {
    try {
      const stored = localStorage.getItem(DRAFTS_STORAGE_KEY);
      if (stored) {
        const existing = JSON.parse(stored) as WizardDraft[];
        const filtered = existing.filter((d) => d.id !== draftId);
        localStorage.setItem(DRAFTS_STORAGE_KEY, JSON.stringify(filtered));
        setDrafts(filtered);
      }
    } catch (error) {
      console.error('Failed to delete draft:', error);
    }
  }, []);

  const handleLoadDraft = useCallback(
    (draft: WizardDraft) => {
      onLoadDraft(draft);
      onClose();
    },
    [onLoadDraft, onClose]
  );

  const formatDate = (date: Date | string) => {
    return new Date(date).toLocaleDateString('en-US', {
      month: 'short',
      day: 'numeric',
      year: 'numeric',
      hour: 'numeric',
      minute: '2-digit',
    });
  };

  const getDraftMetadata = (draft: WizardDraft): DraftMetadata => {
    return {
      id: draft.id,
      name: draft.name,
      createdAt: draft.createdAt,
      updatedAt: draft.updatedAt,
      currentStep: draft.currentStep,
      progress: draft.progress,
      briefSummary: draft.data.brief.topic.substring(0, 100) || 'Untitled video',
    };
  };

  return (
    <>
      <Dialog open={open} onOpenChange={(_, data) => !data.open && onClose()}>
        <DialogSurface style={{ maxWidth: '900px', maxHeight: '80vh', overflow: 'auto' }}>
          <DialogBody>
            <DialogTitle>Manage Drafts</DialogTitle>
            <DialogContent>
              <div className={styles.container}>
                <div style={{ display: 'flex', justifyContent: 'space-between' }}>
                  <Title3>Saved Drafts</Title3>
                  {currentData && (
                    <Button
                      appearance="primary"
                      onClick={() => setShowSaveDialog(true)}
                      icon={<DocumentMultiple24Regular />}
                    >
                      Save Current as Draft
                    </Button>
                  )}
                </div>

                {loading ? (
                  <div className={styles.emptyState}>
                    <Spinner size="large" />
                    <Text>Loading drafts...</Text>
                  </div>
                ) : drafts.length === 0 ? (
                  <div className={styles.emptyState}>
                    <DocumentMultiple24Regular style={{ fontSize: '64px' }} />
                    <Text size={400}>No drafts saved yet</Text>
                    <Text>Save your work in progress to continue later</Text>
                  </div>
                ) : (
                  <div className={styles.draftGrid}>
                    {drafts.map((draft) => {
                      const metadata = getDraftMetadata(draft);
                      return (
                        <Card key={draft.id} className={styles.draftCard}>
                          <div className={styles.draftHeader}>
                            <div style={{ flex: 1 }}>
                              <Text weight="semibold" size={400}>
                                {draft.name}
                              </Text>
                            </div>
                            <Badge appearance="tint">Step {draft.currentStep + 1}/5</Badge>
                          </div>

                          <div className={styles.draftDetails}>
                            <Text size={200} style={{ color: tokens.colorNeutralForeground3 }}>
                              {metadata.briefSummary}
                            </Text>

                            <div
                              style={{
                                display: 'flex',
                                alignItems: 'center',
                                gap: tokens.spacingHorizontalXS,
                              }}
                            >
                              <Calendar24Regular
                                style={{ fontSize: '16px', color: tokens.colorNeutralForeground3 }}
                              />
                              <Text size={200} style={{ color: tokens.colorNeutralForeground3 }}>
                                Updated {formatDate(draft.updatedAt)}
                              </Text>
                            </div>

                            <div
                              style={{
                                marginTop: tokens.spacingVerticalS,
                                backgroundColor: tokens.colorNeutralBackground2,
                                borderRadius: tokens.borderRadiusMedium,
                                padding: tokens.spacingVerticalXS,
                              }}
                            >
                              <div
                                style={{
                                  width: `${draft.progress}%`,
                                  height: '4px',
                                  backgroundColor: tokens.colorBrandBackground,
                                  borderRadius: tokens.borderRadiusSmall,
                                }}
                              />
                            </div>
                          </div>

                          <div className={styles.draftActions}>
                            <Button
                              appearance="subtle"
                              icon={<Delete24Regular />}
                              onClick={(e) => {
                                e.stopPropagation();
                                deleteDraft(draft.id);
                              }}
                            >
                              Delete
                            </Button>
                            <Button
                              appearance="primary"
                              onClick={() => handleLoadDraft(draft)}
                              icon={<Checkmark24Regular />}
                            >
                              Load
                            </Button>
                          </div>
                        </Card>
                      );
                    })}
                  </div>
                )}
              </div>
            </DialogContent>
            <DialogActions>
              <Button appearance="secondary" onClick={onClose}>
                Close
              </Button>
            </DialogActions>
          </DialogBody>
        </DialogSurface>
      </Dialog>

      <Dialog
        open={showSaveDialog}
        onOpenChange={(_, data) => !data.open && setShowSaveDialog(false)}
      >
        <DialogSurface>
          <DialogBody>
            <DialogTitle>Save Draft</DialogTitle>
            <DialogContent>
              <div className={styles.saveForm}>
                <Text>Give your draft a name so you can easily find it later.</Text>
                <Field label="Draft Name" required>
                  <Input
                    value={draftName}
                    onChange={(_, data) => setDraftName(data.value)}
                    placeholder="e.g., Product Launch Video - WIP"
                  />
                </Field>
              </div>
            </DialogContent>
            <DialogActions>
              <Button appearance="secondary" onClick={() => setShowSaveDialog(false)}>
                Cancel
              </Button>
              <Button
                appearance="primary"
                onClick={saveDraft}
                disabled={!draftName.trim()}
                icon={<DocumentMultiple24Regular />}
              >
                Save Draft
              </Button>
            </DialogActions>
          </DialogBody>
        </DialogSurface>
      </Dialog>
    </>
  );
};
