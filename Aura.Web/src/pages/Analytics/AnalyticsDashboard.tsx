import {
  Button,
  Card,
  Text,
  Title1,
  Title2,
  Title3,
  Spinner,
  Badge,
  makeStyles,
  tokens,
  Tab,
  TabList,
} from '@fluentui/react-components';
import {
  ArrowUpload24Regular,
  VideoClip24Regular,
  ChartMultiple24Regular,
  Lightbulb24Regular,
} from '@fluentui/react-icons';
import { useState, useEffect, useCallback } from 'react';
import {
  performanceAnalyticsService,
  type PerformanceInsights,
  type VideoPerformance,
} from '../../services/analytics/PerformanceAnalyticsService';

const useStyles = makeStyles({
  container: {
    padding: tokens.spacingVerticalXXL,
    maxWidth: '1400px',
    margin: '0 auto',
  },
  header: {
    marginBottom: tokens.spacingVerticalXL,
  },
  tabs: {
    marginBottom: tokens.spacingVerticalL,
  },
  section: {
    marginBottom: tokens.spacingVerticalXL,
  },
  statsGrid: {
    display: 'grid',
    gridTemplateColumns: 'repeat(auto-fit, minmax(250px, 1fr))',
    gap: tokens.spacingHorizontalL,
    marginBottom: tokens.spacingVerticalXL,
  },
  statCard: {
    padding: tokens.spacingVerticalL,
    textAlign: 'center',
  },
  statValue: {
    fontSize: '32px',
    fontWeight: 'bold',
    color: tokens.colorBrandForeground1,
  },
  statLabel: {
    color: tokens.colorNeutralForeground3,
    marginTop: tokens.spacingVerticalS,
  },
  videoList: {
    display: 'flex',
    flexDirection: 'column',
    gap: tokens.spacingVerticalM,
  },
  videoCard: {
    padding: tokens.spacingVerticalL,
    display: 'flex',
    justifyContent: 'space-between',
    alignItems: 'center',
    ':hover': {
      backgroundColor: tokens.colorNeutralBackground1Hover,
    },
  },
  videoInfo: {
    flex: 1,
  },
  videoTitle: {
    fontWeight: 'bold',
    marginBottom: tokens.spacingVerticalXS,
  },
  videoMetrics: {
    display: 'flex',
    gap: tokens.spacingHorizontalL,
    marginTop: tokens.spacingVerticalS,
  },
  metric: {
    display: 'flex',
    flexDirection: 'column',
  },
  metricValue: {
    fontWeight: 'bold',
  },
  metricLabel: {
    fontSize: '12px',
    color: tokens.colorNeutralForeground3,
  },
  insights: {
    display: 'flex',
    flexDirection: 'column',
    gap: tokens.spacingVerticalM,
  },
  insightCard: {
    padding: tokens.spacingVerticalL,
  },
  insightText: {
    marginBottom: tokens.spacingVerticalS,
  },
  patterns: {
    display: 'flex',
    flexDirection: 'column',
    gap: tokens.spacingVerticalM,
  },
  patternCard: {
    padding: tokens.spacingVerticalL,
    borderLeft: `4px solid`,
  },
  successPattern: {
    borderLeftColor: tokens.colorPaletteGreenBorder2,
    backgroundColor: tokens.colorPaletteGreenBackground1,
  },
  failurePattern: {
    borderLeftColor: tokens.colorPaletteRedBorder2,
    backgroundColor: tokens.colorPaletteRedBackground1,
  },
  patternHeader: {
    display: 'flex',
    justifyContent: 'space-between',
    alignItems: 'center',
    marginBottom: tokens.spacingVerticalS,
  },
  patternDescription: {
    marginBottom: tokens.spacingVerticalS,
  },
  patternImpact: {
    display: 'flex',
    gap: tokens.spacingHorizontalM,
    fontSize: '12px',
  },
  emptyState: {
    textAlign: 'center',
    padding: tokens.spacingVerticalXXXL,
    color: tokens.colorNeutralForeground3,
  },
  importSection: {
    padding: tokens.spacingVerticalL,
    textAlign: 'center',
  },
  importButtons: {
    display: 'flex',
    gap: tokens.spacingHorizontalM,
    justifyContent: 'center',
    marginTop: tokens.spacingVerticalL,
  },
});

export function AnalyticsDashboard() {
  const styles = useStyles();
  const [selectedTab, setSelectedTab] = useState<'overview' | 'videos' | 'insights' | 'patterns'>(
    'overview'
  );
  const [loading, setLoading] = useState(true);
  const [insights, setInsights] = useState<PerformanceInsights | null>(null);
  const [videos, setVideos] = useState<VideoPerformance[]>([]);
  // Use a default profile ID - in a real app, this would come from user context/auth
  const [profileId] = useState('default');

  const loadData = useCallback(async () => {
    setLoading(true);
    try {
      const [insightsData, videosData] = await Promise.all([
        performanceAnalyticsService.getInsights(profileId),
        performanceAnalyticsService.getVideos(profileId),
      ]);
      setInsights(insightsData);
      setVideos(videosData);
    } catch (error) {
      console.error('Failed to load analytics data:', error);
    } finally {
      setLoading(false);
    }
  }, [profileId]);

  useEffect(() => {
    loadData();
  }, [loadData]);

  const handleImportCSV = () => {
    // Create a file input element and trigger it
    const input = document.createElement('input');
    input.type = 'file';
    input.accept = '.csv';
    input.onchange = async (e) => {
      const file = (e.target as HTMLInputElement).files?.[0];
      if (file) {
        // Note: CSV parsing and data import API integration is not yet implemented
        alert(`CSV import would process ${file.name}. API integration pending.`);
      }
    };
    input.click();
  };

  const handleImportJSON = () => {
    // Create a file input element and trigger it
    const input = document.createElement('input');
    input.type = 'file';
    input.accept = '.json';
    input.onchange = async (e) => {
      const file = (e.target as HTMLInputElement).files?.[0];
      if (file) {
        // Note: JSON validation and data import API integration is not yet implemented
        const reader = new FileReader();
        reader.onload = async (event) => {
          try {
            JSON.parse(event.target?.result as string);
            alert(`JSON import would process ${file.name}. API integration pending.`);
          } catch (error) {
            alert('Invalid JSON file');
          }
        };
        reader.readAsText(file);
      }
    };
    input.click();
  };

  const formatNumber = (num: number) => {
    if (num >= 1000000) {
      return `${(num / 1000000).toFixed(1)}M`;
    }
    if (num >= 1000) {
      return `${(num / 1000).toFixed(1)}K`;
    }
    return num.toString();
  };

  const formatPercentage = (num: number) => {
    return `${(num * 100).toFixed(2)}%`;
  };

  if (loading) {
    return (
      <div className={styles.container}>
        <div style={{ textAlign: 'center', padding: tokens.spacingVerticalXXXL }}>
          <Spinner size="large" label="Loading analytics..." />
        </div>
      </div>
    );
  }

  return (
    <div className={styles.container}>
      <div className={styles.header}>
        <Title1>Performance Analytics</Title1>
        <Text>Track your video performance and discover what works</Text>
      </div>

      <TabList
        className={styles.tabs}
        selectedValue={selectedTab}
        onTabSelect={(_, data) => setSelectedTab(data.value as string)}
      >
        <Tab value="overview" icon={<ChartMultiple24Regular />}>
          Overview
        </Tab>
        <Tab value="videos" icon={<VideoClip24Regular />}>
          Videos
        </Tab>
        <Tab value="insights" icon={<Lightbulb24Regular />}>
          Insights
        </Tab>
        <Tab value="patterns" icon={<ChartMultiple24Regular />}>
          Patterns
        </Tab>
      </TabList>

      {selectedTab === 'overview' && (
        <>
          {insights && insights.totalVideos > 0 ? (
            <>
              <div className={styles.statsGrid}>
                <Card className={styles.statCard}>
                  <div className={styles.statValue}>{insights.totalVideos}</div>
                  <div className={styles.statLabel}>Total Videos</div>
                </Card>
                <Card className={styles.statCard}>
                  <div className={styles.statValue}>{formatNumber(insights.averageViews)}</div>
                  <div className={styles.statLabel}>Avg Views</div>
                </Card>
                <Card className={styles.statCard}>
                  <div className={styles.statValue}>
                    {formatPercentage(insights.averageEngagementRate)}
                  </div>
                  <div className={styles.statLabel}>Avg Engagement</div>
                </Card>
                <Card className={styles.statCard}>
                  <div className={styles.statValue}>
                    <Badge
                      appearance={
                        insights.overallTrend === 'improving'
                          ? 'filled'
                          : insights.overallTrend === 'declining'
                            ? 'tint'
                            : 'outline'
                      }
                      color={
                        insights.overallTrend === 'improving'
                          ? 'success'
                          : insights.overallTrend === 'declining'
                            ? 'danger'
                            : 'brand'
                      }
                    >
                      {insights.overallTrend.replace('_', ' ').toUpperCase()}
                    </Badge>
                  </div>
                  <div className={styles.statLabel}>Overall Trend</div>
                </Card>
              </div>

              <div className={styles.section}>
                <Title2>Quick Insights</Title2>
                <div className={styles.insights}>
                  {insights.actionableInsights.map((insight, idx) => (
                    <Card key={idx} className={styles.insightCard}>
                      <Text className={styles.insightText}>{insight}</Text>
                    </Card>
                  ))}
                </div>
              </div>
            </>
          ) : (
            <Card className={styles.importSection}>
              <ArrowUpload24Regular
                style={{ fontSize: '48px', marginBottom: tokens.spacingVerticalL }}
              />
              <Title2>Import Your Analytics</Title2>
              <Text>Get started by importing your video performance data</Text>
              <div className={styles.importButtons}>
                <Button appearance="primary" onClick={handleImportCSV}>
                  Import from CSV
                </Button>
                <Button onClick={handleImportJSON}>Import from JSON</Button>
              </div>
            </Card>
          )}
        </>
      )}

      {selectedTab === 'videos' && (
        <div className={styles.section}>
          <div
            style={{
              display: 'flex',
              justifyContent: 'space-between',
              alignItems: 'center',
              marginBottom: tokens.spacingVerticalL,
            }}
          >
            <Title2>Your Videos</Title2>
            <Button appearance="primary" icon={<ArrowUpload24Regular />} onClick={handleImportCSV}>
              Import More
            </Button>
          </div>
          {videos.length > 0 ? (
            <div className={styles.videoList}>
              {videos.map((video) => (
                <Card key={video.videoId} className={styles.videoCard}>
                  <div className={styles.videoInfo}>
                    <div className={styles.videoTitle}>{video.title}</div>
                    <Text size={200}>
                      {video.platform} • {new Date(video.publishedAt).toLocaleDateString()}
                    </Text>
                    <div className={styles.videoMetrics}>
                      <div className={styles.metric}>
                        <span className={styles.metricValue}>
                          {formatNumber(video.metrics.views)}
                        </span>
                        <span className={styles.metricLabel}>Views</span>
                      </div>
                      <div className={styles.metric}>
                        <span className={styles.metricValue}>
                          {formatNumber(video.metrics.engagement.likes)}
                        </span>
                        <span className={styles.metricLabel}>Likes</span>
                      </div>
                      <div className={styles.metric}>
                        <span className={styles.metricValue}>
                          {formatNumber(video.metrics.engagement.comments)}
                        </span>
                        <span className={styles.metricLabel}>Comments</span>
                      </div>
                      <div className={styles.metric}>
                        <span className={styles.metricValue}>
                          {formatPercentage(video.metrics.engagement.engagementRate)}
                        </span>
                        <span className={styles.metricLabel}>Engagement</span>
                      </div>
                    </div>
                  </div>
                  {video.projectId && <Badge color="brand">Linked to Project</Badge>}
                </Card>
              ))}
            </div>
          ) : (
            <div className={styles.emptyState}>
              <VideoClip24Regular
                style={{ fontSize: '48px', marginBottom: tokens.spacingVerticalL }}
              />
              <Title3>No Videos Yet</Title3>
              <Text>Import your analytics to see your videos here</Text>
            </div>
          )}
        </div>
      )}

      {selectedTab === 'insights' && insights && (
        <div className={styles.section}>
          <Title2>Performance Insights</Title2>
          <div className={styles.insights}>
            {insights.actionableInsights.map((insight, idx) => (
              <Card key={idx} className={styles.insightCard}>
                <Lightbulb24Regular style={{ marginRight: tokens.spacingHorizontalS }} />
                <Text>{insight}</Text>
              </Card>
            ))}
          </div>
        </div>
      )}

      {selectedTab === 'patterns' && insights && (
        <div className={styles.section}>
          <Title2>Success Patterns</Title2>
          <div className={styles.patterns}>
            {insights.topSuccessPatterns.map((pattern) => (
              <Card
                key={pattern.patternId}
                className={`${styles.patternCard} ${styles.successPattern}`}
              >
                <div className={styles.patternHeader}>
                  <Title3>{pattern.patternType.replace('_', ' ').toUpperCase()}</Title3>
                  <Badge color="success">{(pattern.strength * 100).toFixed(0)}% confidence</Badge>
                </div>
                <Text className={styles.patternDescription}>{pattern.description}</Text>
                <div className={styles.patternImpact}>
                  <Text>Impact: +{pattern.impact.overallImpact.toFixed(0)}%</Text>
                  <Text>•</Text>
                  <Text>{pattern.occurrences} occurrences</Text>
                </div>
              </Card>
            ))}
          </div>

          <Title2 style={{ marginTop: tokens.spacingVerticalXXL }}>Areas to Improve</Title2>
          <div className={styles.patterns}>
            {insights.topFailurePatterns.map((pattern) => (
              <Card
                key={pattern.patternId}
                className={`${styles.patternCard} ${styles.failurePattern}`}
              >
                <div className={styles.patternHeader}>
                  <Title3>{pattern.patternType.replace('_', ' ').toUpperCase()}</Title3>
                  <Badge color="danger">{(pattern.strength * 100).toFixed(0)}% confidence</Badge>
                </div>
                <Text className={styles.patternDescription}>{pattern.description}</Text>
                <div className={styles.patternImpact}>
                  <Text>Impact: {pattern.impact.overallImpact.toFixed(0)}%</Text>
                  <Text>•</Text>
                  <Text>{pattern.occurrences} occurrences</Text>
                </div>
              </Card>
            ))}
          </div>
        </div>
      )}
    </div>
  );
}
