import {
  makeStyles,
  tokens,
  Card,
  Text,
  Title1,
  Title3,
  Button,
  Table,
  TableHeader,
  TableRow,
  TableHeaderCell,
  TableBody,
  TableCell,
  Menu,
  MenuTrigger,
  MenuPopover,
  MenuList,
  MenuItem,
  TabList,
  Tab,
} from '@fluentui/react-components';
import {
  FolderOpen24Regular,
  Video24Regular,
  ArrowClockwise24Regular,
  MoreVertical24Regular,
  Eye24Regular,
  Edit24Regular,
  Delete24Regular,
  DocumentCopy24Regular,
  Wand24Regular,
} from '@fluentui/react-icons';
import { useEffect, useState, useCallback } from 'react';
import { useNavigate } from 'react-router-dom';
import { RouteErrorBoundary } from '../../components/ErrorBoundary/RouteErrorBoundary';
import { SkeletonTable, ErrorState } from '../../components/Loading';
import { WizardProjectsTab } from '../../components/WizardProjectsTab';
import { getProjects, deleteProject, duplicateProject } from '../../services/projectService';
import { useJobsStore } from '../../state/jobs';
import { ProjectListItem } from '../../types/project';

const useStyles = makeStyles({
  container: {
    maxWidth: '1200px',
    margin: '0 auto',
    padding: tokens.spacingVerticalL,
  },
  header: {
    display: 'flex',
    justifyContent: 'space-between',
    alignItems: 'center',
    marginBottom: tokens.spacingVerticalXXL,
  },
  table: {
    width: '100%',
  },
  statusBadge: {
    padding: `${tokens.spacingVerticalXS} ${tokens.spacingHorizontalS}`,
    borderRadius: tokens.borderRadiusMedium,
    fontSize: '12px',
    fontWeight: 600,
  },
  statusDone: {
    backgroundColor: tokens.colorPaletteGreenBackground2,
    color: tokens.colorPaletteGreenForeground1,
  },
  statusRunning: {
    backgroundColor: tokens.colorPaletteBlueBackground2,
    color: tokens.colorPaletteBlueForeground2,
  },
  statusFailed: {
    backgroundColor: tokens.colorPaletteRedBackground2,
    color: tokens.colorPaletteRedForeground1,
  },
  emptyState: {
    display: 'flex',
    flexDirection: 'column',
    alignItems: 'center',
    justifyContent: 'center',
    padding: tokens.spacingVerticalXXXL,
    gap: tokens.spacingVerticalL,
    color: tokens.colorNeutralForeground3,
  },
});

function ProjectsPageContent() {
  const styles = useStyles();
  const { jobs, loading, listJobs } = useJobsStore();
  const navigate = useNavigate();
  const [selectedTab, setSelectedTab] = useState<'wizard' | 'editor' | 'generated'>('wizard');
  const [editorProjects, setEditorProjects] = useState<ProjectListItem[]>([]);
  const [loadingProjects, setLoadingProjects] = useState(false);
  const [projectsError, setProjectsError] = useState<string | null>(null);

  const loadEditorProjects = useCallback(async () => {
    setLoadingProjects(true);
    setProjectsError(null);
    try {
      const projects = await getProjects();
      setEditorProjects(Array.isArray(projects) ? projects : []);
    } catch (error: unknown) {
      const err = error instanceof Error ? error : new Error(String(error));
      setProjectsError(err.message);
      setEditorProjects([]);
    } finally {
      setLoadingProjects(false);
    }
  }, []);

  useEffect(() => {
    listJobs();
    loadEditorProjects();
  }, [listJobs, loadEditorProjects]);

  const handleOpenProject = (projectId: string) => {
    navigate(`/editor?projectId=${projectId}`);
  };

  const handleDeleteProject = async (projectId: string) => {
    if (window.confirm('Are you sure you want to delete this project?')) {
      try {
        await deleteProject(projectId);
        await loadEditorProjects();
      } catch (error) {
        console.error('Failed to delete project:', error);
      }
    }
  };

  const handleDuplicateProject = async (projectId: string) => {
    try {
      await duplicateProject(projectId);
      await loadEditorProjects();
    } catch (error) {
      console.error('Failed to duplicate project:', error);
    }
  };

  const formatDate = (dateStr: string) => {
    const date = new Date(dateStr);
    return date.toLocaleString();
  };

  const formatDuration = (startedAt: string, finishedAt?: string) => {
    if (!finishedAt) return '--';
    const start = new Date(startedAt);
    const end = new Date(finishedAt);
    const diffMs = end.getTime() - start.getTime();
    const minutes = Math.floor(diffMs / 60000);
    const seconds = Math.floor((diffMs % 60000) / 1000);
    return `${minutes}m ${seconds}s`;
  };

  const openFolder = (artifactPath: string) => {
    // Extract directory from artifact path
    const dirPath = artifactPath.substring(0, artifactPath.lastIndexOf('/'));
    window.open(`file:///${dirPath.replace(/\\/g, '/')}`);
  };

  const revealInExplorer = (artifactPath: string) => {
    // For web apps, we can only open the folder
    // In a desktop app, this would call the OS's "reveal in explorer" API
    const dirPath = artifactPath.substring(0, artifactPath.lastIndexOf('/'));
    window.open(`file:///${dirPath.replace(/\\/g, '/')}`);
  };

  const openArtifact = (artifactPath: string) => {
    window.open(`file:///${artifactPath.replace(/\\/g, '/')}`);
  };

  return (
    <div className={styles.container}>
      <div className={styles.header}>
        <Title1>Projects</Title1>
        <Button
          appearance="secondary"
          icon={<ArrowClockwise24Regular />}
          onClick={() => {
            listJobs();
            loadEditorProjects();
          }}
          disabled={loading || loadingProjects}
        >
          Refresh
        </Button>
      </div>

      {/* Tabs */}
      <TabList
        selectedValue={selectedTab}
        onTabSelect={(_, data) => setSelectedTab(data.value as 'wizard' | 'editor' | 'generated')}
        style={{ marginBottom: tokens.spacingVerticalL }}
      >
        <Tab value="wizard" icon={<Wand24Regular />}>
          Wizard Projects
        </Tab>
        <Tab value="editor" icon={<Edit24Regular />}>
          Editor Projects
        </Tab>
        <Tab value="generated" icon={<Video24Regular />}>
          Generated Videos
        </Tab>
      </TabList>

      {/* Wizard Projects Tab */}
      {selectedTab === 'wizard' && <WizardProjectsTab />}

      {/* Editor Projects Tab */}
      {selectedTab === 'editor' && (
        <>
          {loadingProjects && (
            <Card>
              <SkeletonTable
                columns={['Name', 'Last Modified', 'Duration', 'Clips', 'Actions']}
                rowCount={5}
                columnWidths={['35%', '20%', '15%', '10%', '20%']}
                ariaLabel="Loading editor projects"
              />
            </Card>
          )}

          {!loadingProjects && projectsError && (
            <ErrorState
              title="Failed to load projects"
              message={projectsError}
              onRetry={loadEditorProjects}
              withCard={true}
            />
          )}

          {!loadingProjects && !projectsError && editorProjects.length === 0 && (
            <Card>
              <div className={styles.emptyState}>
                <Edit24Regular style={{ fontSize: '64px' }} />
                <Title3>No editor projects yet</Title3>
                <Text>Create a project in the video editor to see it here</Text>
                <Button appearance="primary" onClick={() => navigate('/editor')}>
                  Open Video Editor
                </Button>
              </div>
            </Card>
          )}

          {!loadingProjects && !projectsError && editorProjects.length > 0 && (
            <Card>
              <Table className={styles.table}>
                <TableHeader>
                  <TableRow>
                    <TableHeaderCell>Name</TableHeaderCell>
                    <TableHeaderCell>Last Modified</TableHeaderCell>
                    <TableHeaderCell>Duration</TableHeaderCell>
                    <TableHeaderCell>Clips</TableHeaderCell>
                    <TableHeaderCell>Actions</TableHeaderCell>
                  </TableRow>
                </TableHeader>
                <TableBody>
                  {editorProjects.map((project) => (
                    <TableRow
                      key={project.id}
                      style={{ cursor: 'pointer' }}
                      onClick={() => handleOpenProject(project.id)}
                    >
                      <TableCell>
                        <Text weight="semibold">{project.name}</Text>
                        {project.description && (
                          <div>
                            <Text size={200} style={{ color: tokens.colorNeutralForeground3 }}>
                              {project.description}
                            </Text>
                          </div>
                        )}
                      </TableCell>
                      <TableCell>{new Date(project.lastModifiedAt).toLocaleString()}</TableCell>
                      <TableCell>{formatProjectDuration(project.duration)}</TableCell>
                      <TableCell>{project.clipCount}</TableCell>
                      <TableCell>
                        {/* eslint-disable-next-line jsx-a11y/no-static-element-interactions, jsx-a11y/click-events-have-key-events */}
                        <div
                          style={{ display: 'flex', gap: tokens.spacingHorizontalS }}
                          onClick={(e) => e.stopPropagation()}
                        >
                          <Button
                            size="small"
                            appearance="primary"
                            icon={<Edit24Regular />}
                            onClick={() => handleOpenProject(project.id)}
                          >
                            Open
                          </Button>
                          <Menu>
                            <MenuTrigger>
                              <Button
                                size="small"
                                appearance="subtle"
                                icon={<MoreVertical24Regular />}
                              />
                            </MenuTrigger>
                            <MenuPopover>
                              <MenuList>
                                <MenuItem
                                  icon={<DocumentCopy24Regular />}
                                  onClick={() => handleDuplicateProject(project.id)}
                                >
                                  Duplicate
                                </MenuItem>
                                <MenuItem
                                  icon={<Delete24Regular />}
                                  onClick={() => handleDeleteProject(project.id)}
                                >
                                  Delete
                                </MenuItem>
                              </MenuList>
                            </MenuPopover>
                          </Menu>
                        </div>
                      </TableCell>
                    </TableRow>
                  ))}
                </TableBody>
              </Table>
            </Card>
          )}
        </>
      )}

      {/* Generated Videos Tab */}
      {selectedTab === 'generated' && (
        <>
          {loading && (
            <Card>
              <SkeletonTable
                columns={['Date', 'Topic', 'Status', 'Stage', 'Duration', 'Actions']}
                rowCount={5}
                columnWidths={['18%', '20%', '15%', '15%', '12%', '20%']}
                ariaLabel="Loading generated videos"
              />
            </Card>
          )}

          {!loading && jobs.length === 0 && (
            <Card>
              <div className={styles.emptyState}>
                <Video24Regular style={{ fontSize: '64px' }} />
                <Title3>No generated videos yet</Title3>
                <Text>Create your first video to see it here</Text>
              </div>
            </Card>
          )}

          {!loading && jobs.length > 0 && (
            <Card>
              <Table className={styles.table}>
                <TableHeader>
                  <TableRow>
                    <TableHeaderCell>Date</TableHeaderCell>
                    <TableHeaderCell>Topic</TableHeaderCell>
                    <TableHeaderCell>Status</TableHeaderCell>
                    <TableHeaderCell>Stage</TableHeaderCell>
                    <TableHeaderCell>Duration</TableHeaderCell>
                    <TableHeaderCell>Actions</TableHeaderCell>
                  </TableRow>
                </TableHeader>
                <TableBody>
                  {jobs.map((job) => (
                    <TableRow key={job.id}>
                      <TableCell>{formatDate(job.startedAt)}</TableCell>
                      <TableCell>
                        <Text weight="semibold">
                          {job.correlationId?.substring(0, 8) || 'Unknown'}
                        </Text>
                      </TableCell>
                      <TableCell>
                        <span
                          className={`${styles.statusBadge} ${
                            job.status === 'Done'
                              ? styles.statusDone
                              : job.status === 'Running'
                                ? styles.statusRunning
                                : styles.statusFailed
                          }`}
                        >
                          {job.status}
                        </span>
                      </TableCell>
                      <TableCell>{job.stage}</TableCell>
                      <TableCell>{formatDuration(job.startedAt, job.finishedAt)}</TableCell>
                      <TableCell>
                        <div style={{ display: 'flex', gap: tokens.spacingHorizontalS }}>
                          {job.artifacts.length > 0 && (
                            <>
                              <Button
                                size="small"
                                appearance="primary"
                                icon={<Eye24Regular />}
                                onClick={() => {
                                  const firstArtifact = job.artifacts[0];
                                  if (firstArtifact) {
                                    openArtifact(firstArtifact.path);
                                  }
                                }}
                              >
                                Open
                              </Button>
                              <Menu>
                                <MenuTrigger>
                                  <Button
                                    size="small"
                                    appearance="subtle"
                                    icon={<MoreVertical24Regular />}
                                  />
                                </MenuTrigger>
                                <MenuPopover>
                                  <MenuList>
                                    <MenuItem
                                      icon={<FolderOpen24Regular />}
                                      onClick={() => {
                                        const firstArtifact = job.artifacts[0];
                                        if (firstArtifact) {
                                          openFolder(firstArtifact.path);
                                        }
                                      }}
                                    >
                                      Open outputs folder
                                    </MenuItem>
                                    <MenuItem
                                      icon={<FolderOpen24Regular />}
                                      onClick={() => {
                                        const firstArtifact = job.artifacts[0];
                                        if (firstArtifact) {
                                          revealInExplorer(firstArtifact.path);
                                        }
                                      }}
                                    >
                                      Reveal in Explorer
                                    </MenuItem>
                                  </MenuList>
                                </MenuPopover>
                              </Menu>
                            </>
                          )}
                        </div>
                      </TableCell>
                    </TableRow>
                  ))}
                </TableBody>
              </Table>
            </Card>
          )}
        </>
      )}
    </div>
  );
}

// Helper function to format project duration
function formatProjectDuration(seconds: number): string {
  if (seconds === 0) return '0:00';
  const mins = Math.floor(seconds / 60);
  const secs = Math.floor(seconds % 60);
  return `${mins}:${secs.toString().padStart(2, '0')}`;
}

// Main export with error boundary
export function ProjectsPage() {
  return (
    <RouteErrorBoundary>
      <ProjectsPageContent />
    </RouteErrorBoundary>
  );
}
