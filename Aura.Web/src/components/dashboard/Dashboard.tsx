import {
  makeStyles,
  tokens,
  Title1,
  Title2,
  Text,
  Button,
  Card,
  Spinner,
  mergeClasses,
} from '@fluentui/react-components';
import {
  Add24Regular,
  DocumentText24Regular,
  DocumentCopy24Regular,
  ArrowUpload24Regular,
} from '@fluentui/react-icons';
import { useEffect, useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { useDashboardStore } from '../../state/dashboard';
import type { ProjectSummary } from '../../state/dashboard';
import RecentProjectsList from '../RecentProjectsList';
import { DashboardCustomizationButton } from './DashboardCustomization';
import { DashboardWidgets } from './DashboardWidgets';
import { NotificationToaster } from './NotificationToaster';
import { ProjectCard } from './ProjectCard';

const useStyles = makeStyles({
  container: {
    maxWidth: '1400px',
    margin: '0 auto',
    padding: tokens.spacingVerticalXL,
  },
  hero: {
    marginBottom: tokens.spacingVerticalXXL,
  },
  greeting: {
    marginBottom: tokens.spacingVerticalS,
  },
  subtitle: {
    color: tokens.colorNeutralForeground3,
    marginBottom: tokens.spacingVerticalL,
  },
  ctaSection: {
    display: 'flex',
    gap: tokens.spacingHorizontalM,
    marginBottom: tokens.spacingVerticalL,
    flexWrap: 'wrap',
  },
  statsBar: {
    display: 'flex',
    gap: tokens.spacingHorizontalL,
    padding: tokens.spacingVerticalL,
    backgroundColor: tokens.colorNeutralBackground2,
    borderRadius: tokens.borderRadiusMedium,
    flexWrap: 'wrap',
  },
  statItem: {
    display: 'flex',
    flexDirection: 'column',
    gap: tokens.spacingVerticalXS,
  },
  statLabel: {
    color: tokens.colorNeutralForeground3,
    fontSize: tokens.fontSizeBase200,
  },
  statValue: {
    fontSize: tokens.fontSizeBase500,
    fontWeight: tokens.fontWeightSemibold,
  },
  mainContent: {
    display: 'grid',
    gridTemplateColumns: '1fr 350px',
    gap: tokens.spacingHorizontalXL,
    '@media (max-width: 1024px)': {
      gridTemplateColumns: '1fr',
    },
  },
  projectsSection: {
    display: 'flex',
    flexDirection: 'column',
    gap: tokens.spacingVerticalL,
  },
  sectionHeader: {
    display: 'flex',
    justifyContent: 'space-between',
    alignItems: 'center',
    marginBottom: tokens.spacingVerticalM,
  },
  projectGrid: {
    display: 'grid',
    gap: tokens.spacingHorizontalL,
    '@media (max-width: 768px)': {
      gridTemplateColumns: '1fr',
    },
  },
  projectGrid2Cols: {
    gridTemplateColumns: 'repeat(auto-fill, minmax(400px, 1fr))',
  },
  projectGrid3Cols: {
    gridTemplateColumns: 'repeat(auto-fill, minmax(300px, 1fr))',
  },
  projectGrid4Cols: {
    gridTemplateColumns: 'repeat(auto-fill, minmax(250px, 1fr))',
  },
  quickStartSection: {
    marginTop: tokens.spacingVerticalXL,
  },
  quickStartGrid: {
    display: 'grid',
    gridTemplateColumns: 'repeat(auto-fit, minmax(200px, 1fr))',
    gap: tokens.spacingHorizontalM,
  },
  quickStartCard: {
    padding: tokens.spacingVerticalL,
    textAlign: 'center',
    cursor: 'pointer',
    transition: 'transform 0.2s ease, box-shadow 0.2s ease',
    ':hover': {
      transform: 'translateY(-2px)',
      boxShadow: tokens.shadow8,
    },
  },
  quickStartIcon: {
    fontSize: '48px',
    marginBottom: tokens.spacingVerticalM,
    color: tokens.colorBrandBackground,
  },
  emptyState: {
    textAlign: 'center',
    padding: tokens.spacingVerticalXXXL,
    display: 'flex',
    flexDirection: 'column',
    alignItems: 'center',
    gap: tokens.spacingVerticalL,
  },
  sidebar: {
    display: 'flex',
    flexDirection: 'column',
    gap: tokens.spacingVerticalL,
  },
  recentBriefsSection: {
    marginTop: tokens.spacingVerticalL,
  },
  briefButton: {
    width: '100%',
    marginBottom: tokens.spacingVerticalS,
    justifyContent: 'flex-start',
  },
});

export function Dashboard() {
  const styles = useStyles();
  const navigate = useNavigate();
  const [draggedIndex, setDraggedIndex] = useState<number | null>(null);

  const {
    projects,
    stats,
    providerHealth,
    usageData,
    quickInsights,
    recentBriefs,
    layout,
    loading,
    fetchDashboardData,
    reorderProjects,
  } = useDashboardStore();

  useEffect(() => {
    fetchDashboardData();
  }, [fetchDashboardData]);

  const getGreeting = () => {
    const hour = new Date().getHours();
    if (hour < 12) return 'Good morning';
    if (hour < 18) return 'Good afternoon';
    return 'Good evening';
  };

  const handleProjectPreview = (project: ProjectSummary) => {
    // Preview functionality to be implemented
    navigate(`/preview/${project.id}`);
  };

  const handleProjectEdit = (project: ProjectSummary) => {
    navigate(`/editor/${project.id}`);
  };

  const handleProjectDuplicate = (project: ProjectSummary) => {
    // Duplicate functionality to be implemented
    navigate(`/duplicate/${project.id}`);
  };

  const handleProjectShare = (project: ProjectSummary) => {
    // Share functionality to be implemented
    navigate(`/share/${project.id}`);
  };

  const handleProjectDelete = (project: ProjectSummary) => {
    // Delete functionality to be implemented
    if (window.confirm(`Are you sure you want to delete "${project.name}"?`)) {
      // Will be connected to actual delete API
    }
  };

  const handleDragStart = (index: number) => (e: React.DragEvent) => {
    setDraggedIndex(index);
    e.dataTransfer.effectAllowed = 'move';
  };

  const handleDragOver = (index: number) => (e: React.DragEvent) => {
    e.preventDefault();
    if (draggedIndex !== null && draggedIndex !== index) {
      reorderProjects(draggedIndex, index);
      setDraggedIndex(index);
    }
  };

  const handleDragEnd = () => {
    setDraggedIndex(null);
  };

  const quickStartOptions = [
    {
      icon: <DocumentText24Regular />,
      title: 'From Template',
      description: 'Start with a template',
      onClick: () => navigate('/templates'),
    },
    {
      icon: <DocumentText24Regular />,
      title: 'From Script',
      description: 'Upload your script',
      onClick: () => navigate('/create'),
    },
    {
      icon: <DocumentCopy24Regular />,
      title: 'Batch Create',
      description: 'Upload CSV',
      onClick: () => navigate('/batch'),
    },
    {
      icon: <ArrowUpload24Regular />,
      title: 'Import Project',
      description: 'Upload JSON',
      onClick: () => navigate('/import'),
    },
  ];

  if (loading && projects.length === 0) {
    return (
      <div className={styles.container}>
        <div
          style={{ display: 'flex', justifyContent: 'center', padding: tokens.spacingVerticalXXXL }}
        >
          <Spinner size="huge" label="Loading dashboard..." />
        </div>
      </div>
    );
  }

  return (
    <div className={styles.container}>
      <NotificationToaster />
      {/* Hero Section */}
      <div className={styles.hero}>
        <div
          style={{
            display: 'flex',
            justifyContent: 'space-between',
            alignItems: 'center',
            marginBottom: tokens.spacingVerticalS,
          }}
        >
          <Title1 className={styles.greeting}>{getGreeting()}, welcome back!</Title1>
          <DashboardCustomizationButton />
        </div>
        <Text className={styles.subtitle}>
          Create professional videos with AI-powered automation
        </Text>

        <div className={styles.ctaSection}>
          <Button
            appearance="primary"
            icon={<Add24Regular />}
            onClick={() => navigate('/create')}
            size="large"
          >
            Create New Video
          </Button>
        </div>

        {/* Quick Stats Bar */}
        {layout.showStats && (
          <div className={styles.statsBar}>
            <div className={styles.statItem}>
              <Text className={styles.statLabel}>Videos Today</Text>
              <Text className={styles.statValue}>{stats.videosToday}</Text>
            </div>
            <div className={styles.statItem}>
              <Text className={styles.statLabel}>Total Storage</Text>
              <Text className={styles.statValue}>{stats.totalStorage}</Text>
            </div>
            <div className={styles.statItem}>
              <Text className={styles.statLabel}>API Credits</Text>
              <Text className={styles.statValue}>{stats.apiCredits.toLocaleString()}</Text>
            </div>
          </div>
        )}
      </div>

      {/* Main Content */}
      <div className={styles.mainContent}>
        {/* Projects Section */}
        {layout.showProjects && (
          <div className={styles.projectsSection}>
            <div className={styles.sectionHeader}>
              <Title2>Recent Projects</Title2>
              {projects.length > 0 && (
                <Button appearance="subtle" onClick={() => navigate('/projects')}>
                  View All
                </Button>
              )}
            </div>

            {projects.length === 0 ? (
              <Card className={styles.emptyState}>
                <Text size={500}>No projects yet</Text>
                <Text>Get started by creating your first video</Text>
                <Button
                  appearance="primary"
                  icon={<Add24Regular />}
                  onClick={() => navigate('/create')}
                >
                  Create Your First Video
                </Button>
              </Card>
            ) : (
              <div
                className={mergeClasses(
                  styles.projectGrid,
                  layout.projectGridColumns === 2 && styles.projectGrid2Cols,
                  layout.projectGridColumns === 3 && styles.projectGrid3Cols,
                  layout.projectGridColumns === 4 && styles.projectGrid4Cols
                )}
              >
                {projects.slice(0, 6).map((project, index) => (
                  <ProjectCard
                    key={project.id}
                    project={project}
                    onPreview={handleProjectPreview}
                    onEdit={handleProjectEdit}
                    onDuplicate={handleProjectDuplicate}
                    onShare={handleProjectShare}
                    onDelete={handleProjectDelete}
                    draggable
                    onDragStart={handleDragStart(index)}
                    onDragOver={handleDragOver(index)}
                    onDragEnd={handleDragEnd}
                  />
                ))}
              </div>
            )}

            {/* Quick Start Section */}
            <div className={styles.quickStartSection}>
              <Title2 style={{ marginBottom: tokens.spacingVerticalM }}>Quick Start</Title2>
              <div className={styles.quickStartGrid}>
                {quickStartOptions.map((option) => (
                  <Card
                    key={option.title}
                    className={styles.quickStartCard}
                    onClick={option.onClick}
                  >
                    <div className={styles.quickStartIcon}>{option.icon}</div>
                    <Text weight="semibold">{option.title}</Text>
                    <Text size={200} style={{ color: tokens.colorNeutralForeground3 }}>
                      {option.description}
                    </Text>
                  </Card>
                ))}
              </div>
            </div>

            {/* Recent Projects Section */}
            <div className={styles.recentBriefsSection}>
              <Title2 style={{ marginBottom: tokens.spacingVerticalM }}>Recent Projects</Title2>
              <RecentProjectsList
                maxItems={5}
                onOpenProject={(project) => {
                  navigate(`/create?projectId=${project.id}`);
                }}
              />
            </div>

            {/* Recent Briefs Section */}
            {layout.showRecentBriefs && recentBriefs.length > 0 && (
              <div className={styles.recentBriefsSection}>
                <Title2 style={{ marginBottom: tokens.spacingVerticalM }}>Recent Briefs</Title2>
                {recentBriefs.map((brief) => (
                  <Button
                    key={brief.id}
                    className={styles.briefButton}
                    appearance="outline"
                    onClick={() => navigate(`/create?brief=${brief.id}`)}
                  >
                    <div style={{ textAlign: 'left', width: '100%' }}>
                      <Text weight="semibold" block>
                        {brief.title}
                      </Text>
                      <Text size={200} style={{ color: tokens.colorNeutralForeground3 }} block>
                        {brief.topic}
                      </Text>
                    </div>
                  </Button>
                ))}
              </div>
            )}
          </div>
        )}

        {/* Sidebar Widgets */}
        <div className={styles.sidebar}>
          <DashboardWidgets
            usageData={usageData}
            providerHealth={providerHealth}
            quickInsights={quickInsights}
          />
        </div>
      </div>
    </div>
  );
}
