import { useState } from 'react';
import {
  makeStyles,
  tokens,
  Text,
  Button,
  Tab,
  TabList,
} from '@fluentui/react-components';
import {
  ChevronDown24Regular,
  ChevronUp24Regular,
  Dismiss24Regular,
} from '@fluentui/react-icons';
import { useActivity } from '../../state/activityContext';
import { OperationProgress } from './OperationProgress';
import { OperationHistory } from './OperationHistory';

const useStyles = makeStyles({
  drawer: {
    position: 'fixed',
    bottom: 0,
    left: 0,
    right: 0,
    backgroundColor: tokens.colorNeutralBackground1,
    borderTop: `1px solid ${tokens.colorNeutralStroke1}`,
    boxShadow: '0 -4px 12px rgba(0, 0, 0, 0.15)',
    zIndex: 1000,
    transition: 'transform 0.3s ease-in-out',
  },
  drawerCollapsed: {
    transform: 'translateY(100%)',
  },
  drawerExpanded: {
    transform: 'translateY(0)',
  },
  header: {
    display: 'flex',
    alignItems: 'center',
    justifyContent: 'space-between',
    padding: '12px 20px',
    borderBottom: `1px solid ${tokens.colorNeutralStroke1}`,
    cursor: 'pointer',
    ':hover': {
      backgroundColor: tokens.colorNeutralBackground1Hover,
    },
  },
  headerLeft: {
    display: 'flex',
    alignItems: 'center',
    gap: '12px',
  },
  content: {
    maxHeight: '60vh',
    overflowY: 'auto',
  },
  tabContent: {
    padding: '16px 20px',
  },
  operationsList: {
    display: 'flex',
    flexDirection: 'column',
    gap: '12px',
  },
  emptyState: {
    padding: '40px 20px',
    textAlign: 'center',
    color: tokens.colorNeutralForeground3,
  },
});

export interface ActivityDrawerProps {
  isExpanded: boolean;
  onToggle: () => void;
}

export function ActivityDrawer({ isExpanded, onToggle }: ActivityDrawerProps) {
  const styles = useStyles();
  const [selectedTab, setSelectedTab] = useState<'active' | 'queued' | 'completed' | 'history'>('active');
  const { activeActivities, queuedActivities, completedActivities, clearCompleted } = useActivity();

  const renderTabContent = () => {
    switch (selectedTab) {
      case 'active':
        return (
          <div className={styles.operationsList}>
            {activeActivities.length === 0 ? (
              <div className={styles.emptyState}>
                <Text>No active operations</Text>
              </div>
            ) : (
              activeActivities.map(activity => (
                <OperationProgress key={activity.id} activity={activity} />
              ))
            )}
          </div>
        );
      case 'queued':
        return (
          <div className={styles.operationsList}>
            {queuedActivities.length === 0 ? (
              <div className={styles.emptyState}>
                <Text>No queued operations</Text>
              </div>
            ) : (
              queuedActivities.map(activity => (
                <OperationProgress key={activity.id} activity={activity} showPriorityControl />
              ))
            )}
          </div>
        );
      case 'completed':
        return (
          <div className={styles.operationsList}>
            {completedActivities.length === 0 ? (
              <div className={styles.emptyState}>
                <Text>No completed operations</Text>
              </div>
            ) : (
              <>
                <div style={{ display: 'flex', justifyContent: 'flex-end', marginBottom: '8px' }}>
                  <Button
                    appearance="subtle"
                    size="small"
                    onClick={clearCompleted}
                  >
                    Clear All
                  </Button>
                </div>
                {completedActivities.map(activity => (
                  <OperationProgress key={activity.id} activity={activity} />
                ))}
              </>
            )}
          </div>
        );
      case 'history':
        return <OperationHistory />;
      default:
        return null;
    }
  };

  const getTotalCount = () => {
    switch (selectedTab) {
      case 'active':
        return activeActivities.length;
      case 'queued':
        return queuedActivities.length;
      case 'completed':
        return completedActivities.length;
      default:
        return 0;
    }
  };

  return (
    <div className={`${styles.drawer} ${isExpanded ? styles.drawerExpanded : styles.drawerCollapsed}`}>
      <div 
        className={styles.header}
        onClick={onToggle}
        role="button"
        tabIndex={0}
        onKeyDown={(e) => {
          if (e.key === 'Enter' || e.key === ' ') {
            e.preventDefault();
            onToggle();
          }
        }}
      >
        <div className={styles.headerLeft}>
          {isExpanded ? <ChevronDown24Regular /> : <ChevronUp24Regular />}
          <Text weight="semibold">Activity Details</Text>
          <Text size={200} style={{ color: tokens.colorNeutralForeground3 }}>
            {getTotalCount()} {selectedTab}
          </Text>
        </div>
        <Button
          appearance="subtle"
          size="small"
          icon={<Dismiss24Regular />}
          onClick={(e) => {
            e.stopPropagation();
            onToggle();
          }}
          aria-label="Close activity drawer"
        />
      </div>

      {isExpanded && (
        <>
          <TabList
            selectedValue={selectedTab}
            onTabSelect={(_, data) => setSelectedTab(data.value as typeof selectedTab)}
            style={{ padding: '0 20px', borderBottom: `1px solid ${tokens.colorNeutralStroke1}` }}
          >
            <Tab value="active">
              Active ({activeActivities.length})
            </Tab>
            <Tab value="queued">
              Queued ({queuedActivities.length})
            </Tab>
            <Tab value="completed">
              Completed ({completedActivities.length})
            </Tab>
            <Tab value="history">
              History
            </Tab>
          </TabList>
          <div className={styles.content}>
            <div className={styles.tabContent}>
              {renderTabContent()}
            </div>
          </div>
        </>
      )}
    </div>
  );
}
