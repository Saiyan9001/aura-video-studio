import { makeStyles, tokens, Input, Button, Dropdown, Option } from '@fluentui/react-components';
import { Search24Regular, Dismiss24Regular, History24Regular } from '@fluentui/react-icons';
import { useState, useEffect, useCallback } from 'react';

const useStyles = makeStyles({
  searchContainer: {
    position: 'sticky',
    top: 0,
    zIndex: 100,
    backgroundColor: tokens.colorNeutralBackground1,
    padding: tokens.spacingVerticalM,
    marginBottom: tokens.spacingVerticalL,
    borderBottom: `1px solid ${tokens.colorNeutralStroke1}`,
    boxShadow: tokens.shadow4,
  },
  searchWrapper: {
    display: 'flex',
    gap: tokens.spacingHorizontalS,
    alignItems: 'center',
    maxWidth: '600px',
    margin: '0 auto',
  },
  searchInput: {
    flex: 1,
  },
  historyButton: {
    minWidth: 'auto',
  },
  historyDropdown: {
    width: '300px',
  },
  noResults: {
    padding: tokens.spacingVerticalM,
    color: tokens.colorNeutralForeground3,
    textAlign: 'center',
  },
});

export interface SearchableItem {
  id: string;
  title: string;
  description: string;
  keywords?: string[];
  category: string;
  element?: HTMLElement;
}

interface SettingsSearchProps {
  items: SearchableItem[];
  onSearch: (query: string, results: SearchableItem[]) => void;
  onClear: () => void;
}

const MAX_HISTORY = 10;
const STORAGE_KEY = 'aura-settings-search-history';

export function SettingsSearch({ items, onSearch, onClear }: SettingsSearchProps) {
  const styles = useStyles();
  const [query, setQuery] = useState('');
  const [searchHistory, setSearchHistory] = useState<string[]>([]);
  const [showHistory, setShowHistory] = useState(false);

  useEffect(() => {
    const stored = localStorage.getItem(STORAGE_KEY);
    if (stored) {
      try {
        setSearchHistory(JSON.parse(stored));
      } catch {
        // Ignore invalid JSON
      }
    }
  }, []);

  const saveToHistory = useCallback((searchQuery: string) => {
    if (!searchQuery.trim()) return;

    setSearchHistory((prev) => {
      const updated = [searchQuery, ...prev.filter((q) => q !== searchQuery)].slice(0, MAX_HISTORY);
      localStorage.setItem(STORAGE_KEY, JSON.stringify(updated));
      return updated;
    });
  }, []);

  const performSearch = useCallback(
    (searchQuery: string) => {
      if (!searchQuery.trim()) {
        onClear();
        return;
      }

      const lowerQuery = searchQuery.toLowerCase();
      const results = items.filter((item) => {
        const searchText = [item.title, item.description, item.category, ...(item.keywords || [])]
          .join(' ')
          .toLowerCase();
        return searchText.includes(lowerQuery);
      });

      onSearch(searchQuery, results);
      saveToHistory(searchQuery);

      if (results.length > 0 && results[0].element) {
        results[0].element.scrollIntoView({ behavior: 'smooth', block: 'center' });
      }
    },
    [items, onSearch, onClear, saveToHistory]
  );

  const handleInputChange = (value: string) => {
    setQuery(value);
    performSearch(value);
  };

  const handleClear = () => {
    setQuery('');
    onClear();
  };

  const handleHistorySelect = (selectedQuery: string) => {
    setQuery(selectedQuery);
    performSearch(selectedQuery);
    setShowHistory(false);
  };

  const clearHistory = () => {
    setSearchHistory([]);
    localStorage.removeItem(STORAGE_KEY);
  };

  return (
    <div className={styles.searchContainer}>
      <div className={styles.searchWrapper}>
        <Input
          className={styles.searchInput}
          contentBefore={<Search24Regular />}
          contentAfter={
            query && (
              <Button
                appearance="subtle"
                size="small"
                icon={<Dismiss24Regular />}
                onClick={handleClear}
              />
            )
          }
          placeholder="Search settings..."
          value={query}
          onChange={(e) => handleInputChange(e.target.value)}
        />
        {searchHistory.length > 0 && (
          <Dropdown
            button={
              <Button
                className={styles.historyButton}
                appearance="subtle"
                icon={<History24Regular />}
                onClick={() => setShowHistory(!showHistory)}
              />
            }
            value={showHistory ? 'Recent searches' : ''}
            onOptionSelect={(_, data) => {
              if (data.optionValue && data.optionValue !== 'clear') {
                handleHistorySelect(data.optionValue);
              } else if (data.optionValue === 'clear') {
                clearHistory();
              }
            }}
            className={styles.historyDropdown}
          >
            {searchHistory.map((historyItem, index) => (
              <Option key={index} value={historyItem}>
                {historyItem}
              </Option>
            ))}
            {searchHistory.length > 0 && (
              <>
                <Option disabled value="divider">
                  ─────────────────
                </Option>
                <Option value="clear">Clear history</Option>
              </>
            )}
          </Dropdown>
        )}
      </div>
    </div>
  );
}
