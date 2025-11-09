import {
  Dialog,
  DialogSurface,
  DialogTitle,
  DialogBody,
  DialogActions,
  DialogContent,
  Button,
  makeStyles,
  tokens,
  Text,
  Switch,
  Label,
  Radio,
  RadioGroup,
  Divider,
} from '@fluentui/react-components';
import { Dismiss24Regular, Settings24Regular } from '@fluentui/react-icons';
import { useDashboardStore } from '../../state/dashboard';

const useStyles = makeStyles({
  content: {
    display: 'flex',
    flexDirection: 'column',
    gap: tokens.spacingVerticalL,
  },
  section: {
    display: 'flex',
    flexDirection: 'column',
    gap: tokens.spacingVerticalM,
  },
  switchRow: {
    display: 'flex',
    justifyContent: 'space-between',
    alignItems: 'center',
    padding: tokens.spacingVerticalS,
  },
  label: {
    fontWeight: tokens.fontWeightSemibold,
    marginBottom: tokens.spacingVerticalS,
  },
});

interface DashboardCustomizationProps {
  open: boolean;
  onClose: () => void;
}

export function DashboardCustomization({ open, onClose }: DashboardCustomizationProps) {
  const styles = useStyles();
  const { layout, updateLayout } = useDashboardStore();

  const handleViewChange = (_e: unknown, data: { value: string }) => {
    updateLayout({ view: data.value as 'default' | 'compact' | 'analytics' });
  };

  return (
    <Dialog open={open} onOpenChange={(_e, data) => !data.open && onClose()}>
      <DialogSurface>
        <DialogBody>
          <DialogTitle
            action={
              <Button
                appearance="subtle"
                aria-label="close"
                icon={<Dismiss24Regular />}
                onClick={onClose}
              />
            }
          >
            Dashboard Customization
          </DialogTitle>
          <DialogContent className={styles.content}>
            <div className={styles.section}>
              <Text className={styles.label}>Layout View</Text>
              <RadioGroup value={layout.view} onChange={handleViewChange}>
                <Radio value="default" label="Default - Balanced view with all sections" />
                <Radio value="compact" label="Compact - Minimal view with essential info" />
                <Radio value="analytics" label="Analytics Focus - Emphasize charts and metrics" />
              </RadioGroup>
            </div>

            <Divider />

            <div className={styles.section}>
              <Text className={styles.label}>Visible Sections</Text>

              <div className={styles.switchRow}>
                <Label>Quick Stats Bar</Label>
                <Switch
                  checked={layout.showStats}
                  onChange={(_e, data) => updateLayout({ showStats: data.checked })}
                />
              </div>

              <div className={styles.switchRow}>
                <Label>Projects Grid</Label>
                <Switch
                  checked={layout.showProjects}
                  onChange={(_e, data) => updateLayout({ showProjects: data.checked })}
                />
              </div>

              <div className={styles.switchRow}>
                <Label>Usage Analytics</Label>
                <Switch
                  checked={layout.showAnalytics}
                  onChange={(_e, data) => updateLayout({ showAnalytics: data.checked })}
                />
              </div>

              <div className={styles.switchRow}>
                <Label>Provider Health</Label>
                <Switch
                  checked={layout.showProviderHealth}
                  onChange={(_e, data) => updateLayout({ showProviderHealth: data.checked })}
                />
              </div>

              <div className={styles.switchRow}>
                <Label>Quick Insights</Label>
                <Switch
                  checked={layout.showQuickInsights}
                  onChange={(_e, data) => updateLayout({ showQuickInsights: data.checked })}
                />
              </div>

              <div className={styles.switchRow}>
                <Label>Recent Briefs</Label>
                <Switch
                  checked={layout.showRecentBriefs}
                  onChange={(_e, data) => updateLayout({ showRecentBriefs: data.checked })}
                />
              </div>
            </div>

            <Divider />

            <div className={styles.section}>
              <Text className={styles.label}>Project Grid Columns</Text>
              <RadioGroup
                value={layout.projectGridColumns.toString()}
                onChange={(_e, data) => updateLayout({ projectGridColumns: parseInt(data.value) })}
              >
                <Radio value="2" label="2 columns - Larger cards" />
                <Radio value="3" label="3 columns - Balanced (default)" />
                <Radio value="4" label="4 columns - More compact" />
              </RadioGroup>
            </div>
          </DialogContent>
          <DialogActions>
            <Button
              appearance="secondary"
              onClick={() => {
                updateLayout({
                  showStats: true,
                  showProjects: true,
                  showAnalytics: true,
                  showProviderHealth: true,
                  showQuickInsights: true,
                  showRecentBriefs: true,
                  projectGridColumns: 3,
                  view: 'default',
                });
              }}
            >
              Reset to Default
            </Button>
            <Button appearance="primary" onClick={onClose}>
              Done
            </Button>
          </DialogActions>
        </DialogBody>
      </DialogSurface>
    </Dialog>
  );
}

export function DashboardCustomizationButton() {
  const [open, setOpen] = React.useState(false);

  return (
    <>
      <Button appearance="subtle" icon={<Settings24Regular />} onClick={() => setOpen(true)}>
        Customize
      </Button>
      <DashboardCustomization open={open} onClose={() => setOpen(false)} />
    </>
  );
}

import React from 'react';
