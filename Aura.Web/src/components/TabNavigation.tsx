import { makeStyles, tokens, TabList, Tab } from '@fluentui/react-components';
import { useEffect, useState } from 'react';

const useStyles = makeStyles({
  tabList: {
    backgroundColor: tokens.colorNeutralBackground1,
    borderBottom: `1px solid ${tokens.colorNeutralStroke2}`,
    padding: `0 ${tokens.spacingHorizontalL}`,
  },
});

export interface TabItem {
  key: string;
  label: string;
  icon?: React.ReactElement;
}

interface TabNavigationProps {
  tabs: TabItem[];
  activeTab: string;
  onTabChange: (tabKey: string) => void;
  storageKey?: string; // Key for persisting tab state
}

export function TabNavigation({ tabs, activeTab, onTabChange, storageKey }: TabNavigationProps) {
  const styles = useStyles();
  const [selectedTab, setSelectedTab] = useState(activeTab);

  // Load persisted tab state on mount
  useEffect(() => {
    if (storageKey) {
      try {
        const savedTab = localStorage.getItem(storageKey);
        if (savedTab && tabs.some((tab) => tab.key === savedTab)) {
          setSelectedTab(savedTab);
          onTabChange(savedTab);
        }
      } catch {
        // Ignore localStorage errors
      }
    }
  }, [storageKey, tabs, onTabChange]);

  // Sync with activeTab prop
  useEffect(() => {
    setSelectedTab(activeTab);
  }, [activeTab]);

  const handleTabSelect = (key: string) => {
    setSelectedTab(key);
    onTabChange(key);

    // Persist tab state
    if (storageKey) {
      try {
        localStorage.setItem(storageKey, key);
      } catch {
        // Ignore localStorage errors
      }
    }
  };

  return (
    <TabList
      className={styles.tabList}
      selectedValue={selectedTab}
      onTabSelect={(_, data) => handleTabSelect(data.value as string)}
    >
      {tabs.map((tab) => (
        <Tab key={tab.key} value={tab.key} icon={tab.icon}>
          {tab.label}
        </Tab>
      ))}
    </TabList>
  );
}
