import {
  makeStyles,
  tokens,
  Text,
  Badge,
  Button,
  Card,
  Spinner,
  Select,
  Input,
} from '@fluentui/react-components';
import {
  CheckmarkCircle24Filled,
  ErrorCircle24Filled,
  Clock24Regular,
  ArrowClockwise24Regular,
  Video24Regular,
  Folder24Regular,
  Search24Regular,
  DocumentBulletList24Regular,
  Open24Regular,
} from '@fluentui/react-icons';
import { useState, useEffect } from 'react';
import { useNavigate } from 'react-router-dom';
import { retryJob } from '../features/render/api/jobs';
import { openLogsFolder } from '../utils/apiErrorHandler';
import { openFile, openFolder } from '../utils/fileSystemUtils';

const useStyles = makeStyles({
  container: {
    padding: tokens.spacingVerticalXL,
    maxWidth: '1400px',
    margin: '0 auto',
  },
  header: {
    display: 'flex',
    justifyContent: 'space-between',
    alignItems: 'center',
    marginBottom: tokens.spacingVerticalL,
  },
  title: {
    fontSize: tokens.fontSizeHero700,
    fontWeight: tokens.fontWeightSemibold,
  },
  filters: {
    display: 'flex',
    gap: tokens.spacingHorizontalM,
    marginBottom: tokens.spacingVerticalL,
    flexWrap: 'wrap',
  },
  jobsList: {
    display: 'flex',
    flexDirection: 'column',
    gap: tokens.spacingVerticalM,
  },
  jobCard: {
    padding: tokens.spacingVerticalL,
  },
  jobHeader: {
    display: 'flex',
    justifyContent: 'space-between',
    alignItems: 'flex-start',
    marginBottom: tokens.spacingVerticalM,
  },
  jobInfo: {
    flex: 1,
  },
  jobTitle: {
    display: 'flex',
    alignItems: 'center',
    gap: tokens.spacingHorizontalS,
    marginBottom: tokens.spacingVerticalS,
  },
  jobMeta: {
    display: 'flex',
    gap: tokens.spacingHorizontalM,
    fontSize: tokens.fontSizeBase200,
    color: tokens.colorNeutralForeground2,
  },
  jobActions: {
    display: 'flex',
    gap: tokens.spacingHorizontalS,
  },
  artifactsSection: {
    marginTop: tokens.spacingVerticalM,
    paddingTop: tokens.spacingVerticalM,
    borderTop: `1px solid ${tokens.colorNeutralStroke2}`,
  },
  artifactsList: {
    display: 'flex',
    flexDirection: 'column',
    gap: tokens.spacingVerticalS,
    marginTop: tokens.spacingVerticalS,
  },
  artifactItem: {
    display: 'flex',
    alignItems: 'center',
    justifyContent: 'space-between',
    padding: tokens.spacingVerticalS,
    backgroundColor: tokens.colorNeutralBackground2,
    borderRadius: tokens.borderRadiusMedium,
  },
  artifactInfo: {
    display: 'flex',
    alignItems: 'center',
    gap: tokens.spacingHorizontalS,
  },
  emptyState: {
    display: 'flex',
    flexDirection: 'column',
    alignItems: 'center',
    justifyContent: 'center',
    padding: tokens.spacingVerticalXXXL,
    textAlign: 'center',
    color: tokens.colorNeutralForeground2,
  },
  outputSection: {
    marginTop: tokens.spacingVerticalM,
    paddingTop: tokens.spacingVerticalM,
    borderTop: `1px solid ${tokens.colorNeutralStroke2}`,
  },
  outputPath: {
    display: 'flex',
    flexDirection: 'column',
    gap: tokens.spacingVerticalS,
    marginTop: tokens.spacingVerticalS,
  },
  pathText: {
    fontFamily: 'monospace',
    fontSize: tokens.fontSizeBase200,
    color: tokens.colorNeutralForeground2,
    wordBreak: 'break-all',
  },
  outputActions: {
    display: 'flex',
    gap: tokens.spacingHorizontalS,
    marginTop: tokens.spacingVerticalS,
  },
});

interface JobArtifact {
  type: string;
  name: string;
  path: string;
  sizeBytes: number;
  url?: string;
}

interface Job {
  id: string;
  status: string;
  startedAt: string;
  finishedAt?: string;
  correlationId?: string;
  errorMessage?: string;
  artifacts?: JobArtifact[];
  outputPath?: string;
  subtitlePath?: string;
}

export function RecentJobsPage() {
  const styles = useStyles();
  const navigate = useNavigate();
  const [jobs, setJobs] = useState<Job[]>([]);
  const [loading, setLoading] = useState(true);
  const [filterStatus, setFilterStatus] = useState<string>('all');
  const [searchQuery, setSearchQuery] = useState('');

  useEffect(() => {
    loadJobs();
  }, []);

  const loadJobs = async () => {
    setLoading(true);
    try {
      const response = await fetch('/api/jobs?limit=50');
      const data = await response.json();
      setJobs(data.jobs || []);
    } catch (error) {
      console.error('Failed to load jobs:', error);
    } finally {
      setLoading(false);
    }
  };

  const handleRetry = async (jobId: string) => {
    try {
      await retryJob(jobId);
      await loadJobs();
    } catch (error) {
      console.error('Failed to retry job:', error);
    }
  };

  const getStatusBadge = (status: string) => {
    switch (status) {
      case 'Done':
      case 'Succeeded':
        return (
          <Badge appearance="filled" color="success">
            Completed
          </Badge>
        );
      case 'Failed':
        return (
          <Badge appearance="filled" color="danger">
            Failed
          </Badge>
        );
      case 'Running':
        return (
          <Badge appearance="filled" color="informative">
            Running
          </Badge>
        );
      case 'Queued':
        return (
          <Badge appearance="filled" color="warning">
            Queued
          </Badge>
        );
      case 'Canceled':
        return (
          <Badge appearance="outline" color="subtle">
            Canceled
          </Badge>
        );
      default:
        return <Badge appearance="outline">{status}</Badge>;
    }
  };

  const getStatusIcon = (status: string) => {
    switch (status) {
      case 'Done':
      case 'Succeeded':
        return <CheckmarkCircle24Filled style={{ color: tokens.colorPaletteGreenForeground1 }} />;
      case 'Failed':
        return <ErrorCircle24Filled style={{ color: tokens.colorPaletteRedForeground1 }} />;
      default:
        return <Clock24Regular />;
    }
  };

  const formatDate = (dateStr: string) => {
    const date = new Date(dateStr);
    return date.toLocaleString();
  };

  const formatSize = (bytes: number) => {
    if (bytes < 1024) return `${bytes} B`;
    if (bytes < 1024 * 1024) return `${(bytes / 1024).toFixed(1)} KB`;
    return `${(bytes / (1024 * 1024)).toFixed(1)} MB`;
  };

  const handleOpenFile = async (path: string) => {
    const success = await openFile(path);
    if (!success) {
      alert('Failed to open file. Please ensure the backend API is running.');
    }
  };

  const handleOpenFolder = async (path: string) => {
    const success = await openFolder(path);
    if (!success) {
      alert('Failed to open folder. Please ensure the backend API is running.');
    }
  };

  const filteredJobs = jobs.filter((job) => {
    if (filterStatus !== 'all' && job.status !== filterStatus) {
      return false;
    }
    if (searchQuery && !job.id.toLowerCase().includes(searchQuery.toLowerCase())) {
      return false;
    }
    return true;
  });

  if (loading) {
    return (
      <div className={styles.emptyState}>
        <Spinner size="large" label="Loading jobs..." />
      </div>
    );
  }

  return (
    <div className={styles.container}>
      <div className={styles.header}>
        <Text className={styles.title}>Recent Jobs & Artifacts</Text>
        <Button appearance="primary" icon={<ArrowClockwise24Regular />} onClick={loadJobs}>
          Refresh
        </Button>
      </div>

      <div className={styles.filters}>
        <Input
          placeholder="Search by job ID..."
          contentBefore={<Search24Regular />}
          value={searchQuery}
          onChange={(_e, data) => setSearchQuery(data.value)}
        />
        <Select value={filterStatus} onChange={(_e, data) => setFilterStatus(data.value)}>
          <option value="all">All Status</option>
          <option value="Done">Completed</option>
          <option value="Failed">Failed</option>
          <option value="Running">Running</option>
          <option value="Queued">Queued</option>
        </Select>
      </div>

      {filteredJobs.length === 0 ? (
        <div className={styles.emptyState}>
          <Video24Regular style={{ fontSize: '48px', marginBottom: tokens.spacingVerticalL }} />
          <Text size={500}>No jobs found</Text>
          <Text size={300}>Create a new video to get started</Text>
        </div>
      ) : (
        <div className={styles.jobsList}>
          {filteredJobs.map((job) => (
            <Card key={job.id} className={styles.jobCard}>
              <div className={styles.jobHeader}>
                <div className={styles.jobInfo}>
                  <div className={styles.jobTitle}>
                    {getStatusIcon(job.status)}
                    <Text weight="semibold">Job {job.id}</Text>
                    {getStatusBadge(job.status)}
                  </div>
                  <div className={styles.jobMeta}>
                    <span>Started: {formatDate(job.startedAt)}</span>
                    {job.finishedAt && <span>Finished: {formatDate(job.finishedAt)}</span>}
                    {job.correlationId && <span>Correlation: {job.correlationId}</span>}
                  </div>
                  {job.errorMessage && (
                    <Text
                      style={{
                        color: tokens.colorPaletteRedForeground1,
                        marginTop: tokens.spacingVerticalS,
                      }}
                    >
                      {job.errorMessage}
                    </Text>
                  )}
                </div>
                <div className={styles.jobActions}>
                  {(job.status === 'Done' ||
                    job.status === 'Failed' ||
                    job.status === 'Canceled') && (
                    <Button
                      appearance="subtle"
                      icon={<DocumentBulletList24Regular />}
                      onClick={() => navigate(`/jobs/${job.id}/telemetry`)}
                    >
                      View Details
                    </Button>
                  )}
                  {job.status === 'Done' && (
                    <Button
                      appearance="primary"
                      onClick={() => (window.location.href = `/editor/${job.id}`)}
                    >
                      Edit Video
                    </Button>
                  )}
                  {job.status === 'Failed' && (
                    <Button
                      appearance="subtle"
                      icon={<ArrowClockwise24Regular />}
                      onClick={() => handleRetry(job.id)}
                    >
                      Retry
                    </Button>
                  )}
                </div>
              </div>

              {job.status === 'Done' && job.outputPath && (
                <div className={styles.outputSection}>
                  <Text weight="semibold">Final Output</Text>
                  <div className={styles.outputPath}>
                    <Text className={styles.pathText}>{job.outputPath}</Text>
                    <div className={styles.outputActions}>
                      <Button
                        appearance="primary"
                        icon={<Open24Regular />}
                        onClick={() => handleOpenFile(job.outputPath!)}
                      >
                        Open File
                      </Button>
                      <Button
                        appearance="subtle"
                        icon={<Folder24Regular />}
                        onClick={() => handleOpenFolder(job.outputPath!)}
                      >
                        Open Folder
                      </Button>
                    </div>
                  </div>
                </div>
              )}

              {job.status === 'Done' && !job.outputPath && (
                <div className={styles.outputSection}>
                  <Text weight="semibold">Final Output</Text>
                  <div className={styles.outputPath}>
                    <Text
                      size={200}
                      style={{ color: tokens.colorNeutralForeground3, fontStyle: 'italic' }}
                    >
                      Output path not available
                    </Text>
                    <Button
                      appearance="subtle"
                      icon={<DocumentBulletList24Regular />}
                      onClick={() => openLogsFolder()}
                    >
                      View Logs
                    </Button>
                  </div>
                </div>
              )}

              {job.artifacts && job.artifacts.length > 0 && (
                <div className={styles.artifactsSection}>
                  <Text weight="semibold">Artifacts ({job.artifacts.length})</Text>
                  <div className={styles.artifactsList}>
                    {job.artifacts.map((artifact: JobArtifact, index: number) => (
                      <div key={index} className={styles.artifactItem}>
                        <div className={styles.artifactInfo}>
                          <Video24Regular />
                          <div>
                            <Text weight="semibold">{artifact.name}</Text>
                            <Text
                              size={200}
                              style={{ display: 'block', color: tokens.colorNeutralForeground2 }}
                            >
                              {artifact.type} • {formatSize(artifact.sizeBytes)}
                            </Text>
                          </div>
                        </div>
                        <Button
                          appearance="subtle"
                          icon={<Folder24Regular />}
                          onClick={() => handleOpenFolder(artifact.path)}
                        >
                          Open Folder
                        </Button>
                      </div>
                    ))}
                  </div>
                </div>
              )}
            </Card>
          ))}
        </div>
      )}
    </div>
  );
}
