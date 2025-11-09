import {
  makeStyles,
  tokens,
  Button,
  Card,
  CardHeader,
  Text,
  Badge,
  Tooltip,
} from '@fluentui/react-components';
import {
  Folder24Regular,
  Document24Regular,
  Star24Regular,
  Star24Filled,
  Play24Regular,
  CheckmarkCircle24Regular,
  ErrorCircle24Regular,
  Clock24Regular,
} from '@fluentui/react-icons';
import { useState, useEffect } from 'react';
import { useNavigate } from 'react-router-dom';

const useStyles = makeStyles({
  panel: {
    position: 'fixed',
    top: '80px',
    right: tokens.spacingHorizontalL,
    width: '320px',
    maxHeight: 'calc(100vh - 100px)',
    overflowY: 'auto',
    backgroundColor: tokens.colorNeutralBackground1,
    borderRadius: tokens.borderRadiusLarge,
    boxShadow: tokens.shadow16,
    padding: tokens.spacingVerticalL,
    zIndex: 1000,
    '@media (max-width: 768px)': {
      display: 'none',
    },
  },
  section: {
    marginBottom: tokens.spacingVerticalL,
  },
  sectionTitle: {
    fontSize: tokens.fontSizeBase300,
    fontWeight: tokens.fontWeightSemibold,
    marginBottom: tokens.spacingVerticalS,
    color: tokens.colorNeutralForeground2,
  },
  projectCard: {
    marginBottom: tokens.spacingVerticalS,
    cursor: 'pointer',
    transition: 'transform 0.2s, box-shadow 0.2s',
    ':hover': {
      transform: 'translateY(-2px)',
      boxShadow: tokens.shadow8,
    },
  },
  projectHeader: {
    display: 'flex',
    alignItems: 'center',
    justifyContent: 'space-between',
  },
  projectInfo: {
    display: 'flex',
    alignItems: 'center',
    gap: tokens.spacingHorizontalS,
  },
  projectThumb: {
    width: '40px',
    height: '40px',
    borderRadius: tokens.borderRadiusSmall,
    backgroundColor: tokens.colorNeutralBackground3,
    display: 'flex',
    alignItems: 'center',
    justifyContent: 'center',
  },
  templateCard: {
    marginBottom: tokens.spacingVerticalS,
    cursor: 'pointer',
    ':hover': {
      backgroundColor: tokens.colorNeutralBackground2,
    },
  },
  resumeButton: {
    width: '100%',
    marginBottom: tokens.spacingVerticalS,
  },
  providerStatus: {
    display: 'flex',
    flexDirection: 'column',
    gap: tokens.spacingVerticalXS,
  },
  providerItem: {
    display: 'flex',
    alignItems: 'center',
    justifyContent: 'space-between',
    padding: tokens.spacingVerticalXS,
  },
  providerName: {
    fontSize: tokens.fontSizeBase200,
  },
  starButton: {
    minWidth: 'auto',
    padding: tokens.spacingVerticalXXS,
  },
});

interface Project {
  id: string;
  name: string;
  lastModified: Date;
  isPinned?: boolean;
}

interface Template {
  id: string;
  name: string;
  isPinned?: boolean;
}

interface ProviderStatus {
  name: string;
  status: 'online' | 'offline' | 'error';
}

interface QuickAccessPanelProps {
  onClose?: () => void;
}

export function QuickAccessPanel({ onClose }: QuickAccessPanelProps) {
  const styles = useStyles();
  const navigate = useNavigate();
  const [recentProjects, setRecentProjects] = useState<Project[]>([]);
  const [pinnedTemplates, setPinnedTemplates] = useState<Template[]>([]);
  const [lastSession, setLastSession] = useState<string | null>(null);
  const [providerStatuses, setProviderStatuses] = useState<ProviderStatus[]>([]);

  // Load recent projects from localStorage
  useEffect(() => {
    try {
      const saved = localStorage.getItem('aura-recent-projects');
      if (saved) {
        const projects = JSON.parse(saved);
        setRecentProjects(
          projects.slice(0, 5).map((p: Project) => ({
            ...p,
            lastModified: new Date(p.lastModified),
          }))
        );
      }
    } catch {
      // Ignore errors
    }

    // Load pinned templates
    try {
      const saved = localStorage.getItem('aura-pinned-templates');
      if (saved) {
        setPinnedTemplates(JSON.parse(saved));
      }
    } catch {
      // Ignore errors
    }

    // Load last session
    try {
      const session = localStorage.getItem('aura-last-session');
      setLastSession(session);
    } catch {
      // Ignore errors
    }

    // Mock provider statuses
    setProviderStatuses([
      { name: 'OpenAI', status: 'online' },
      { name: 'Ollama', status: 'offline' },
      { name: 'ElevenLabs', status: 'online' },
    ]);
  }, []);

  const toggleProjectPin = (projectId: string, isPinned: boolean) => {
    setRecentProjects((projects) =>
      projects.map((p) => (p.id === projectId ? { ...p, isPinned: !isPinned } : p))
    );
  };

  const toggleTemplatePin = (templateId: string) => {
    setPinnedTemplates((templates) => {
      const existing = templates.find((t) => t.id === templateId);
      if (existing) {
        return templates.filter((t) => t.id !== templateId);
      } else {
        return [...templates, { id: templateId, name: `Template ${templateId}`, isPinned: true }];
      }
    });
  };

  const handleResumeSession = () => {
    if (lastSession) {
      navigate(lastSession);
      onClose?.();
    }
  };

  const getStatusIcon = (status: string) => {
    switch (status) {
      case 'online':
        return <CheckmarkCircle24Regular style={{ color: tokens.colorPaletteGreenForeground1 }} />;
      case 'offline':
        return <Clock24Regular style={{ color: tokens.colorNeutralForeground3 }} />;
      case 'error':
        return <ErrorCircle24Regular style={{ color: tokens.colorPaletteRedForeground1 }} />;
      default:
        return null;
    }
  };

  return (
    <div className={styles.panel}>
      {/* Resume Last Session */}
      {lastSession && (
        <div className={styles.section}>
          <Text className={styles.sectionTitle}>Continue Where You Left Off</Text>
          <Button
            appearance="primary"
            className={styles.resumeButton}
            icon={<Play24Regular />}
            onClick={handleResumeSession}
          >
            Resume Last Session
          </Button>
        </div>
      )}

      {/* Recent Projects */}
      <div className={styles.section}>
        <Text className={styles.sectionTitle}>Recent Projects</Text>
        {recentProjects.length === 0 ? (
          <Text>No recent projects</Text>
        ) : (
          recentProjects.map((project) => (
            <Card
              key={project.id}
              className={styles.projectCard}
              onClick={() => {
                navigate(`/projects/${project.id}`);
                onClose?.();
              }}
            >
              <CardHeader
                image={
                  <div className={styles.projectThumb}>
                    <Folder24Regular />
                  </div>
                }
                header={<Text weight="semibold">{project.name}</Text>}
                description={<Text size={200}>{project.lastModified.toLocaleDateString()}</Text>}
                action={
                  <Tooltip content={project.isPinned ? 'Unpin' : 'Pin'} relationship="label">
                    <Button
                      appearance="transparent"
                      className={styles.starButton}
                      icon={project.isPinned ? <Star24Filled /> : <Star24Regular />}
                      onClick={(e) => {
                        e.stopPropagation();
                        toggleProjectPin(project.id, project.isPinned || false);
                      }}
                    />
                  </Tooltip>
                }
              />
            </Card>
          ))
        )}
      </div>

      {/* Pinned Templates */}
      {pinnedTemplates.length > 0 && (
        <div className={styles.section}>
          <Text className={styles.sectionTitle}>Pinned Templates</Text>
          {pinnedTemplates.map((template) => (
            <Card
              key={template.id}
              className={styles.templateCard}
              onClick={() => {
                navigate(`/templates/${template.id}`);
                onClose?.();
              }}
            >
              <CardHeader
                image={<Document24Regular />}
                header={<Text>{template.name}</Text>}
                action={
                  <Button
                    appearance="transparent"
                    icon={<Star24Filled />}
                    onClick={(e) => {
                      e.stopPropagation();
                      toggleTemplatePin(template.id);
                    }}
                  />
                }
              />
            </Card>
          ))}
        </div>
      )}

      {/* Provider Status */}
      <div className={styles.section}>
        <Text className={styles.sectionTitle}>Provider Status</Text>
        <div className={styles.providerStatus}>
          {providerStatuses.map((provider) => (
            <div key={provider.name} className={styles.providerItem}>
              <Text className={styles.providerName}>{provider.name}</Text>
              <div
                style={{ display: 'flex', alignItems: 'center', gap: tokens.spacingHorizontalXS }}
              >
                {getStatusIcon(provider.status)}
                <Badge
                  appearance="outline"
                  color={
                    provider.status === 'online'
                      ? 'success'
                      : provider.status === 'offline'
                        ? 'subtle'
                        : 'danger'
                  }
                >
                  {provider.status}
                </Badge>
              </div>
            </div>
          ))}
        </div>
      </div>
    </div>
  );
}
