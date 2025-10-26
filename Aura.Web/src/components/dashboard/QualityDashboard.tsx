import {
  Card,
  Title2,
  Body1,
  Tab,
  TabList,
  Button,
  makeStyles,
  tokens,
  Spinner,
} from '@fluentui/react-components';
import {
  DataTrending24Regular,
  ChartMultiple24Regular,
  DataBarVertical24Regular,
  Lightbulb24Regular,
  ArrowClockwise24Regular,
} from '@fluentui/react-icons';
import React, { useEffect, useState, useCallback } from 'react';
import { useQualityDashboardStore } from '../../state/qualityDashboard';
import { ExportControls } from './ExportControls';
import { HistoricalTrendsGraph } from './HistoricalTrendsGraph';
import { MetricsOverview } from './MetricsOverview';
import { PlatformComplianceGrid } from './PlatformComplianceGrid';
import { QualityRecommendations } from './QualityRecommendations';

const useStyles = makeStyles({
  container: {
    padding: tokens.spacingVerticalXXL,
    maxWidth: '1600px',
    margin: '0 auto',
  },
  header: {
    display: 'flex',
    justifyContent: 'space-between',
    alignItems: 'center',
    marginBottom: tokens.spacingVerticalXXL,
  },
  headerContent: {
    display: 'flex',
    flexDirection: 'column',
    gap: tokens.spacingVerticalS,
  },
  actions: {
    display: 'flex',
    gap: tokens.spacingHorizontalM,
    alignItems: 'center',
  },
  tabContent: {
    marginTop: tokens.spacingVerticalL,
  },
  loadingContainer: {
    display: 'flex',
    justifyContent: 'center',
    alignItems: 'center',
    minHeight: '400px',
  },
  errorCard: {
    padding: tokens.spacingVerticalL,
    backgroundColor: tokens.colorPaletteRedBackground2,
    color: tokens.colorPaletteRedForeground1,
  },
});

export const QualityDashboard: React.FC = () => {
  const styles = useStyles();
  const [selectedTab, setSelectedTab] = useState<string>('overview');
  const { isLoading, error, refreshAll } = useQualityDashboardStore();

  const refreshData = useCallback(() => {
    refreshAll();
  }, [refreshAll]);

  useEffect(() => {
    refreshData();
  }, [refreshData]);

  const handleRefresh = () => {
    refreshAll();
  };

  if (isLoading && !useQualityDashboardStore.getState().metrics) {
    return (
      <div className={styles.container}>
        <div className={styles.loadingContainer}>
          <Spinner label="Loading dashboard data..." size="large" />
        </div>
      </div>
    );
  }

  return (
    <div className={styles.container}>
      <div className={styles.header}>
        <div className={styles.headerContent}>
          <Title2>Quality Dashboard</Title2>
          <Body1>
            Monitor video production quality metrics, platform compliance, and AI-driven
            recommendations
          </Body1>
        </div>
        <div className={styles.actions}>
          <Button
            appearance="subtle"
            icon={<ArrowClockwise24Regular />}
            onClick={handleRefresh}
            disabled={isLoading}
          >
            Refresh
          </Button>
          <ExportControls />
        </div>
      </div>

      {error && (
        <Card className={styles.errorCard}>
          <Body1>Error: {error}</Body1>
        </Card>
      )}

      <TabList
        selectedValue={selectedTab}
        onTabSelect={(_, data) => setSelectedTab(data.value as string)}
      >
        <Tab value="overview" icon={<DataTrending24Regular />}>
          Overview
        </Tab>
        <Tab value="trends" icon={<ChartMultiple24Regular />}>
          Historical Trends
        </Tab>
        <Tab value="compliance" icon={<DataBarVertical24Regular />}>
          Platform Compliance
        </Tab>
        <Tab value="recommendations" icon={<Lightbulb24Regular />}>
          Recommendations
        </Tab>
      </TabList>

      <div className={styles.tabContent}>
        {selectedTab === 'overview' && <MetricsOverview />}
        {selectedTab === 'trends' && <HistoricalTrendsGraph />}
        {selectedTab === 'compliance' && <PlatformComplianceGrid />}
        {selectedTab === 'recommendations' && <QualityRecommendations />}
      </div>
    </div>
  );
};
