import {
  makeStyles,
  tokens,
  Card,
  CardHeader,
  Title3,
  Text,
  Button,
  Spinner,
  Badge,
} from '@fluentui/react-components';
import {
  CheckmarkCircle24Regular,
  Warning24Regular,
  ErrorCircle24Regular,
  Info24Regular,
  ArrowRight24Regular,
} from '@fluentui/react-icons';
import { useEffect, useState, useCallback } from 'react';
import { apiUrl } from '../config/api';

const useStyles = makeStyles({
  card: {
    marginBottom: tokens.spacingVerticalL,
  },
  statusBadge: {
    marginLeft: tokens.spacingHorizontalS,
  },
  issuesList: {
    display: 'flex',
    flexDirection: 'column',
    gap: tokens.spacingVerticalM,
    marginTop: tokens.spacingVerticalM,
  },
  issueCard: {
    padding: tokens.spacingVerticalM,
    borderLeft: `4px solid`,
    backgroundColor: tokens.colorNeutralBackground2,
  },
  issueCardError: {
    borderLeftColor: tokens.colorPaletteRedBorder2,
    backgroundColor: tokens.colorPaletteRedBackground1,
  },
  issueCardWarning: {
    borderLeftColor: tokens.colorPaletteYellowBorder2,
    backgroundColor: tokens.colorPaletteYellowBackground1,
  },
  issueCardInfo: {
    borderLeftColor: tokens.colorPaletteBlueBorderActive,
    backgroundColor: tokens.colorPaletteBlueBackground2,
  },
  issueHeader: {
    display: 'flex',
    alignItems: 'center',
    gap: tokens.spacingHorizontalS,
    marginBottom: tokens.spacingVerticalS,
  },
  issueContent: {
    display: 'flex',
    flexDirection: 'column',
    gap: tokens.spacingVerticalS,
  },
  causesList: {
    paddingLeft: tokens.spacingHorizontalXL,
    margin: 0,
  },
  fixActions: {
    display: 'flex',
    flexWrap: 'wrap',
    gap: tokens.spacingHorizontalS,
    marginTop: tokens.spacingVerticalS,
  },
  recommendations: {
    marginTop: tokens.spacingVerticalL,
    padding: tokens.spacingVerticalM,
    backgroundColor: tokens.colorBrandBackground2,
    borderRadius: tokens.borderRadiusMedium,
  },
  recommendationsList: {
    paddingLeft: tokens.spacingHorizontalXL,
    margin: `${tokens.spacingVerticalS} 0 0 0`,
  },
});

interface DiagnosticIssue {
  code: string;
  title: string;
  description: string;
  severity: 'info' | 'warning' | 'error' | 'critical';
  causes: string[];
  fixActions: {
    label: string;
    description: string;
    actionType: string;
    actionUrl?: string;
    actionData?: Record<string, unknown>;
  }[];
  autoFixable: boolean;
}

interface FirstRunDiagnosticsResult {
  ready: boolean;
  status: 'ready' | 'needs-setup' | 'has-errors' | 'unknown';
  issues: DiagnosticIssue[];
  recommendations: string[];
  systemInfo: Record<string, unknown>;
}

interface FirstRunDiagnosticsProps {
  onReady?: () => void;
  onNeedsSetup?: () => void;
  autoRun?: boolean;
}

export function FirstRunDiagnostics({
  onReady,
  onNeedsSetup,
  autoRun = true,
}: FirstRunDiagnosticsProps) {
  const styles = useStyles();
  const [loading, setLoading] = useState(false);
  const [result, setResult] = useState<FirstRunDiagnosticsResult | null>(null);
  const [error, setError] = useState<string | null>(null);

  const runDiagnostics = useCallback(async () => {
    setLoading(true);
    setError(null);

    try {
      const response = await fetch(apiUrl('/api/health/first-run'));
      if (!response.ok) {
        throw new Error(`HTTP ${response.status}: ${response.statusText}`);
      }

      const data = await response.json();
      setResult(data);

      // Call callbacks based on status
      if (data.ready && onReady) {
        onReady();
      } else if (!data.ready && onNeedsSetup) {
        onNeedsSetup();
      }
    } catch (err) {
      const message = err instanceof Error ? err.message : 'Unknown error';
      setError(`Failed to run diagnostics: ${message}`);
    } finally {
      setLoading(false);
    }
  }, [onReady, onNeedsSetup]);

  useEffect(() => {
    if (autoRun) {
      runDiagnostics();
    }
  }, [autoRun, runDiagnostics]);

  const getSeverityIcon = (severity: string) => {
    switch (severity) {
      case 'critical':
      case 'error':
        return <ErrorCircle24Regular />;
      case 'warning':
        return <Warning24Regular />;
      default:
        return <Info24Regular />;
    }
  };

  const getSeverityColor = (severity: string): 'danger' | 'warning' | 'informative' => {
    switch (severity) {
      case 'critical':
      case 'error':
        return 'danger';
      case 'warning':
        return 'warning';
      default:
        return 'informative';
    }
  };

  const getIssueCardStyle = (severity: string) => {
    switch (severity) {
      case 'critical':
      case 'error':
        return `${styles.issueCard} ${styles.issueCardError}`;
      case 'warning':
        return `${styles.issueCard} ${styles.issueCardWarning}`;
      default:
        return `${styles.issueCard} ${styles.issueCardInfo}`;
    }
  };

  const isFirstRunOnboarding = result?.status === 'needs-setup' && result?.issues?.length === 0;

  const handleFixAction = (action: DiagnosticIssue['fixActions'][0]) => {
    switch (action.actionType) {
      case 'navigate':
        if (action.actionUrl) {
          window.location.href = action.actionUrl;
        }
        break;
      case 'retry':
        runDiagnostics();
        break;
      case 'download':
      case 'install':
      case 'configure':
        // These would be handled by specific logic
        break;
    }
  };

  const getStatusBadge = () => {
    if (!result) return null;

    switch (result.status) {
      case 'ready':
        return (
          <Badge appearance="filled" color="success" className={styles.statusBadge}>
            Ready
          </Badge>
        );
      case 'needs-setup':
        return (
          <Badge appearance="filled" color="warning" className={styles.statusBadge}>
            Needs Setup
          </Badge>
        );
      case 'has-errors':
        return (
          <Badge appearance="filled" color="danger" className={styles.statusBadge}>
            Has Errors
          </Badge>
        );
      default:
        return (
          <Badge appearance="outline" className={styles.statusBadge}>
            Unknown
          </Badge>
        );
    }
  };

  return (
    <Card className={styles.card}>
      <CardHeader
        header={
          <div style={{ display: 'flex', alignItems: 'center' }}>
            <Title3>System Diagnostics</Title3>
            {getStatusBadge()}
          </div>
        }
        description={
          loading ? (
            <Spinner size="tiny" label="Running diagnostics..." />
          ) : error ? (
            <Text style={{ color: tokens.colorPaletteRedForeground1 }}>{error}</Text>
          ) : result ? (
            <Text>
              {result.ready ? (
                <>
                  <CheckmarkCircle24Regular
                    style={{
                      verticalAlign: 'middle',
                      marginRight: '4px',
                      color: tokens.colorPaletteGreenForeground1,
                    }}
                  />
                  All systems ready! You can start creating videos.
                </>
              ) : isFirstRunOnboarding ? (
                <>
                  <Info24Regular
                    style={{
                      verticalAlign: 'middle',
                      marginRight: '4px',
                      color: tokens.colorBrandForeground1,
                    }}
                  />
                  First-time run detected. Setup required.
                </>
              ) : (
                <>
                  <Warning24Regular
                    style={{
                      verticalAlign: 'middle',
                      marginRight: '4px',
                      color: tokens.colorPaletteYellowForeground1,
                    }}
                  />
                  {result.status === 'has-errors'
                    ? 'Critical issues detected. Please resolve them before continuing.'
                    : 'Some setup is required before you can start creating videos.'}
                </>
              )}
            </Text>
          ) : (
            <Text>Click &quot;Run Diagnostics&quot; to check your system.</Text>
          )
        }
        action={
          <Button appearance="secondary" onClick={runDiagnostics} disabled={loading}>
            {loading ? 'Running...' : 'Run Diagnostics'}
          </Button>
        }
      />

      {result && result.issues && result.issues.length > 0 && (
        <div className={styles.issuesList}>
          {result.issues.map((issue, index) => (
            <div key={index} className={getIssueCardStyle(issue.severity)}>
              <div className={styles.issueHeader}>
                {getSeverityIcon(issue.severity)}
                <Title3>{issue.title}</Title3>
                <Badge appearance="tint" color={getSeverityColor(issue.severity)}>
                  {issue.severity}
                </Badge>
                {issue.autoFixable && (
                  <Badge appearance="tint" color="success">
                    Auto-fixable
                  </Badge>
                )}
              </div>

              <div className={styles.issueContent}>
                <Text>{issue.description}</Text>

                {issue.causes && issue.causes.length > 0 && (
                  <>
                    <Text weight="semibold">Possible causes:</Text>
                    <ul className={styles.causesList}>
                      {issue.causes.map((cause, causeIndex) => (
                        <li key={causeIndex}>
                          <Text>{cause}</Text>
                        </li>
                      ))}
                    </ul>
                  </>
                )}

                {issue.fixActions && issue.fixActions.length > 0 && (
                  <div className={styles.fixActions}>
                    {issue.fixActions.map((action, actionIndex) => (
                      <Button
                        key={actionIndex}
                        appearance="primary"
                        size="small"
                        icon={<ArrowRight24Regular />}
                        onClick={() => handleFixAction(action)}
                        title={action.description}
                      >
                        {action.label}
                      </Button>
                    ))}
                  </div>
                )}
              </div>
            </div>
          ))}
        </div>
      )}

      {result && result.recommendations && result.recommendations.length > 0 && (
        <div className={styles.recommendations}>
          <Text weight="semibold">Recommendations:</Text>
          <ul className={styles.recommendationsList}>
            {result.recommendations.map((recommendation, index) => (
              <li key={index}>
                <Text>{recommendation}</Text>
              </li>
            ))}
          </ul>
        </div>
      )}
    </Card>
  );
}
