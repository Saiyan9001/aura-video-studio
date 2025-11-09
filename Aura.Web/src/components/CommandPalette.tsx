import {
  makeStyles,
  tokens,
  Dialog,
  DialogSurface,
  DialogBody,
  Input,
  Text,
  mergeClasses,
  Badge,
} from '@fluentui/react-components';
import {
  Search24Regular,
  Settings24Regular,
  VideoClip24Regular,
  Play24Regular,
  CloudArrowDown24Regular,
  Document24Regular,
  Folder24Regular,
  Timeline24Regular,
  Image24Regular,
  Lightbulb24Regular,
  HeartPulse24Regular,
  Keyboard24Regular,
  PanelLeft24Regular,
  WeatherMoon24Regular,
  DatabaseLink24Regular,
  CloudSync24Regular,
} from '@fluentui/react-icons';
import Fuse from 'fuse.js';
import { useState, useEffect, useCallback, useMemo } from 'react';
import { useNavigate } from 'react-router-dom';
import { useTheme } from '../App';

const useStyles = makeStyles({
  surface: {
    maxWidth: '600px',
    width: '90vw',
    maxHeight: '70vh',
    display: 'flex',
    flexDirection: 'column',
    backdropFilter: 'blur(20px)',
  },
  searchBox: {
    padding: tokens.spacingVerticalL,
    borderBottom: `1px solid ${tokens.colorNeutralStroke2}`,
  },
  results: {
    flex: 1,
    overflowY: 'auto',
    padding: 0,
  },
  categoryHeader: {
    padding: `${tokens.spacingVerticalS} ${tokens.spacingHorizontalL}`,
    backgroundColor: tokens.colorNeutralBackground3,
    fontSize: tokens.fontSizeBase200,
    fontWeight: tokens.fontWeightSemibold,
    color: tokens.colorNeutralForeground3,
    position: 'sticky',
    top: 0,
    zIndex: 1,
  },
  commandItem: {
    padding: `${tokens.spacingVerticalM} ${tokens.spacingHorizontalL}`,
    cursor: 'pointer',
    display: 'flex',
    alignItems: 'center',
    gap: tokens.spacingHorizontalM,
    borderBottom: `1px solid ${tokens.colorNeutralStroke2}`,
    transition: 'background-color 0.2s',
    ':hover': {
      backgroundColor: tokens.colorNeutralBackground3,
    },
  },
  selectedItem: {
    backgroundColor: tokens.colorNeutralBackground3,
  },
  icon: {
    fontSize: '20px',
    color: tokens.colorNeutralForeground2,
  },
  commandContent: {
    flex: 1,
    display: 'flex',
    flexDirection: 'column',
    gap: tokens.spacingVerticalXXS,
  },
  commandName: {
    fontSize: tokens.fontSizeBase300,
    fontWeight: tokens.fontWeightSemibold,
  },
  commandDescription: {
    fontSize: tokens.fontSizeBase200,
    color: tokens.colorNeutralForeground3,
  },
  shortcut: {
    fontSize: tokens.fontSizeBase200,
    color: tokens.colorNeutralForeground3,
    fontFamily: 'monospace',
    padding: `${tokens.spacingVerticalXXS} ${tokens.spacingHorizontalXS}`,
    backgroundColor: tokens.colorNeutralBackground4,
    borderRadius: tokens.borderRadiusSmall,
  },
  emptyState: {
    textAlign: 'center',
    padding: tokens.spacingVerticalXXL,
    color: tokens.colorNeutralForeground3,
  },
});

interface Command {
  id: string;
  name: string;
  description: string;
  category: string;
  icon?: React.ComponentType;
  shortcut?: string;
  action: () => void;
  weight?: number; // For ranking
}

interface CommandPaletteProps {
  isOpen: boolean;
  onClose: () => void;
}

// Get recent projects from localStorage
function getRecentProjects(): Command[] {
  try {
    const recent = localStorage.getItem('aura-recent-projects');
    if (!recent) return [];
    const projects = JSON.parse(recent) as Array<{ id: string; name: string }>;
    return projects.slice(0, 5).map((project) => ({
      id: `project-${project.id}`,
      name: `Open: ${project.name}`,
      description: 'Recent project',
      category: 'Recent Projects',
      icon: Folder24Regular,
      action: () => {
        window.location.href = `/projects/${project.id}`;
      },
      weight: 3, // Higher weight for recent items
    }));
  } catch {
    return [];
  }
}

// Track command usage
function trackCommandUsage(commandId: string) {
  try {
    const usage = localStorage.getItem('aura-command-usage');
    const usageMap = usage ? JSON.parse(usage) : {};
    usageMap[commandId] = (usageMap[commandId] || 0) + 1;
    localStorage.setItem('aura-command-usage', JSON.stringify(usageMap));
  } catch {
    // Ignore errors
  }
}

// Get command weight based on usage
function getCommandWeight(commandId: string): number {
  try {
    const usage = localStorage.getItem('aura-command-usage');
    if (!usage) return 1;
    const usageMap = JSON.parse(usage);
    const count = usageMap[commandId] || 0;
    return 1 + Math.min(count / 10, 2); // Max weight of 3
  } catch {
    return 1;
  }
}

export function CommandPalette({ isOpen, onClose }: CommandPaletteProps) {
  const styles = useStyles();
  const navigate = useNavigate();
  const { toggleTheme } = useTheme();
  const [searchQuery, setSearchQuery] = useState('');
  const [selectedIndex, setSelectedIndex] = useState(0);

  // Define all available commands
  const baseCommands: Command[] = useMemo(
    () => [
      // Navigation
      {
        id: 'nav-create',
        name: 'New Video',
        description: 'Start creating a new video',
        category: 'Actions',
        icon: VideoClip24Regular,
        shortcut: '⌘N',
        action: () => navigate('/create'),
        weight: getCommandWeight('nav-create'),
      },
      {
        id: 'action-open-project',
        name: 'Open Project',
        description: 'Open an existing project',
        category: 'Actions',
        icon: Folder24Regular,
        shortcut: '⌘O',
        action: () => navigate('/projects'),
        weight: getCommandWeight('action-open-project'),
      },
      {
        id: 'action-export',
        name: 'Export Video',
        description: 'Render and export current video',
        category: 'Actions',
        icon: Play24Regular,
        shortcut: '⌘M',
        action: () => navigate('/render'),
        weight: getCommandWeight('action-export'),
      },
      {
        id: 'action-toggle-sidebar',
        name: 'Toggle Sidebar',
        description: 'Show or hide the sidebar',
        category: 'Actions',
        icon: PanelLeft24Regular,
        action: () => {
          const collapsed = localStorage.getItem('aura-sidebar-collapsed');
          localStorage.setItem('aura-sidebar-collapsed', collapsed === 'true' ? 'false' : 'true');
          window.location.reload();
        },
        weight: getCommandWeight('action-toggle-sidebar'),
      },
      {
        id: 'action-change-theme',
        name: 'Change Theme',
        description: 'Toggle between light and dark mode',
        category: 'Actions',
        icon: WeatherMoon24Regular,
        action: () => toggleTheme(),
        weight: getCommandWeight('action-change-theme'),
      },
      {
        id: 'action-view-shortcuts',
        name: 'View Shortcuts',
        description: 'Show keyboard shortcuts',
        category: 'Actions',
        icon: Keyboard24Regular,
        shortcut: '?',
        action: () => {
          onClose();
          window.dispatchEvent(new KeyboardEvent('keydown', { key: '?' }));
        },
        weight: getCommandWeight('action-view-shortcuts'),
      },
      {
        id: 'nav-home',
        name: 'Home',
        description: 'Go to home dashboard',
        category: 'Navigation',
        icon: Document24Regular,
        action: () => navigate('/'),
        weight: getCommandWeight('nav-home'),
      },
      {
        id: 'nav-projects',
        name: 'Projects',
        description: 'View all projects',
        category: 'Navigation',
        icon: Folder24Regular,
        action: () => navigate('/projects'),
        weight: getCommandWeight('nav-projects'),
      },
      {
        id: 'nav-timeline',
        name: 'Timeline Editor',
        description: 'Open timeline editor',
        category: 'Navigation',
        icon: Timeline24Regular,
        action: () => navigate('/timeline'),
        weight: getCommandWeight('nav-timeline'),
      },
      {
        id: 'nav-render',
        name: 'Render & Export',
        description: 'Configure and render videos',
        category: 'Navigation',
        icon: Play24Regular,
        action: () => navigate('/render'),
        weight: getCommandWeight('nav-render'),
      },
      {
        id: 'nav-assets',
        name: 'Asset Library',
        description: 'Manage video assets',
        category: 'Navigation',
        icon: Image24Regular,
        action: () => navigate('/assets'),
        weight: getCommandWeight('nav-assets'),
      },
      {
        id: 'nav-downloads',
        name: 'Program Dependencies',
        description: 'Manage program dependencies and engines',
        category: 'Navigation',
        icon: CloudArrowDown24Regular,
        action: () => navigate('/downloads'),
        weight: getCommandWeight('nav-downloads'),
      },
      {
        id: 'nav-health',
        name: 'Provider Health',
        description: 'Check provider status',
        category: 'Navigation',
        icon: HeartPulse24Regular,
        action: () => navigate('/health'),
        weight: getCommandWeight('nav-health'),
      },
      {
        id: 'nav-ideation',
        name: 'Ideation',
        description: 'Generate video ideas',
        category: 'Navigation',
        icon: Lightbulb24Regular,
        action: () => navigate('/ideation'),
        weight: getCommandWeight('nav-ideation'),
      },
      {
        id: 'nav-settings',
        name: 'Settings',
        description: 'Open settings',
        category: 'Navigation',
        icon: Settings24Regular,
        shortcut: '⌘,',
        action: () => navigate('/settings'),
        weight: getCommandWeight('nav-settings'),
      },

      // Provider Actions
      {
        id: 'provider-ollama',
        name: 'Use Ollama',
        description: 'Switch to Ollama as LLM provider',
        category: 'Providers',
        icon: DatabaseLink24Regular,
        action: async () => {
          try {
            await fetch('/api/settings/provider', {
              method: 'POST',
              headers: { 'Content-Type': 'application/json' },
              body: JSON.stringify({ provider: 'ollama' }),
            });
            alert('Switched to Ollama');
          } catch {
            alert('Failed to switch provider');
          }
        },
        weight: getCommandWeight('provider-ollama'),
      },
      {
        id: 'provider-openai',
        name: 'Use OpenAI',
        description: 'Switch to OpenAI as LLM provider',
        category: 'Providers',
        icon: CloudSync24Regular,
        action: async () => {
          try {
            await fetch('/api/settings/provider', {
              method: 'POST',
              headers: { 'Content-Type': 'application/json' },
              body: JSON.stringify({ provider: 'openai' }),
            });
            alert('Switched to OpenAI');
          } catch {
            alert('Failed to switch provider');
          }
        },
        weight: getCommandWeight('provider-openai'),
      },

      // Settings
      {
        id: 'settings-output',
        name: 'Output Settings',
        description: 'Configure video output parameters',
        category: 'Settings',
        icon: Settings24Regular,
        action: () => {
          navigate('/settings');
          setTimeout(() => {
            const tab = document.querySelector('[value="output"]') as HTMLElement;
            tab?.click();
          }, 100);
        },
        weight: getCommandWeight('settings-output'),
      },
      {
        id: 'settings-performance',
        name: 'Performance Settings',
        description: 'Optimize rendering performance',
        category: 'Settings',
        icon: Settings24Regular,
        action: () => {
          navigate('/settings');
          setTimeout(() => {
            const tab = document.querySelector('[value="performance"]') as HTMLElement;
            tab?.click();
          }, 100);
        },
        weight: getCommandWeight('settings-performance'),
      },
      {
        id: 'settings-shortcuts',
        name: 'Keyboard Shortcuts',
        description: 'Customize keyboard shortcuts',
        category: 'Settings',
        icon: Keyboard24Regular,
        action: () => {
          navigate('/settings');
          setTimeout(() => {
            const tab = document.querySelector('[value="shortcuts"]') as HTMLElement;
            tab?.click();
          }, 100);
        },
        weight: getCommandWeight('settings-shortcuts'),
      },
      {
        id: 'settings-providers',
        name: 'Provider Settings',
        description: 'Configure AI providers',
        category: 'Settings',
        icon: Settings24Regular,
        action: () => {
          navigate('/settings');
          setTimeout(() => {
            const tab = document.querySelector('[value="providers"]') as HTMLElement;
            tab?.click();
          }, 100);
        },
        weight: getCommandWeight('settings-providers'),
      },

      // Quick Actions
      {
        id: 'action-validate-providers',
        name: 'Validate Providers',
        description: 'Test all provider connections',
        category: 'Quick Actions',
        action: async () => {
          await fetch('/api/providers/validate', { method: 'POST' });
          alert('Provider validation complete');
        },
        weight: getCommandWeight('action-validate-providers'),
      },
      {
        id: 'action-clear-cache',
        name: 'Clear Cache',
        description: 'Clear preview and render cache',
        category: 'Quick Actions',
        action: async () => {
          if (confirm('Clear all cache?')) {
            await fetch('/api/cache/clear', { method: 'POST' });
            alert('Cache cleared');
          }
        },
        weight: getCommandWeight('action-clear-cache'),
      },
    ],
    [navigate, toggleTheme, onClose]
  );

  // Combine base commands with recent projects
  const allCommands = useMemo(() => {
    return [...baseCommands, ...getRecentProjects()];
  }, [baseCommands]);

  // Configure Fuse.js for fuzzy search
  const fuse = useMemo(() => {
    return new Fuse(allCommands, {
      keys: [
        { name: 'name', weight: 2 },
        { name: 'description', weight: 1 },
        { name: 'category', weight: 0.5 },
      ],
      threshold: 0.4,
      includeScore: true,
      shouldSort: true,
    });
  }, [allCommands]);

  // Filter commands based on search query with fuzzy search
  const filteredCommands = useMemo(() => {
    if (!searchQuery.trim()) {
      // Sort by weight when no search query
      return allCommands.sort((a, b) => (b.weight || 1) - (a.weight || 1));
    }

    // Use Fuse.js for fuzzy search
    const results = fuse.search(searchQuery);
    return results
      .map((result) => result.item)
      .sort((a, b) => {
        // Sort by weight to prioritize frequent/recent commands
        return (b.weight || 1) - (a.weight || 1);
      });
  }, [searchQuery, allCommands, fuse]);

  // Group commands by category
  const groupedCommands = useMemo(() => {
    const groups: { [key: string]: Command[] } = {};
    filteredCommands.forEach((command) => {
      if (!groups[command.category]) {
        groups[command.category] = [];
      }
      groups[command.category].push(command);
    });
    return groups;
  }, [filteredCommands]);

  // Reset selected index when search query changes
  useEffect(() => {
    setSelectedIndex(0);
  }, [searchQuery]);

  // Handle keyboard navigation
  const handleKeyDown = useCallback(
    (e: KeyboardEvent) => {
      if (!isOpen) return;

      const totalCommands = filteredCommands.length;

      switch (e.key) {
        case 'ArrowDown':
          e.preventDefault();
          setSelectedIndex((prev) => Math.min(prev + 1, totalCommands - 1));
          break;
        case 'ArrowUp':
          e.preventDefault();
          setSelectedIndex((prev) => Math.max(prev - 1, 0));
          break;
        case 'Enter':
          e.preventDefault();
          if (filteredCommands[selectedIndex]) {
            const command = filteredCommands[selectedIndex];
            trackCommandUsage(command.id);
            command.action();
            onClose();
            setSearchQuery('');
          }
          break;
        case 'Escape':
          e.preventDefault();
          onClose();
          setSearchQuery('');
          break;
      }
    },
    [isOpen, selectedIndex, filteredCommands, onClose]
  );

  useEffect(() => {
    window.addEventListener('keydown', handleKeyDown);
    return () => window.removeEventListener('keydown', handleKeyDown);
  }, [handleKeyDown]);

  // Clear search when dialog closes
  useEffect(() => {
    if (!isOpen) {
      setSearchQuery('');
      setSelectedIndex(0);
    }
  }, [isOpen]);

  return (
    <Dialog open={isOpen} onOpenChange={(_, data) => !data.open && onClose()}>
      <DialogSurface className={styles.surface}>
        <DialogBody>
          <div className={styles.searchBox}>
            <Input
              appearance="filled-lighter"
              size="large"
              placeholder="Type a command or search..."
              contentBefore={<Search24Regular />}
              value={searchQuery}
              onChange={(e) => setSearchQuery(e.target.value)}
              // autoFocus is intentional for command palette UX
              // eslint-disable-next-line jsx-a11y/no-autofocus
              autoFocus
            />
          </div>
          <div className={styles.results}>
            {filteredCommands.length === 0 ? (
              <div className={styles.emptyState}>
                <Text>No commands found for &quot;{searchQuery}&quot;</Text>
              </div>
            ) : (
              Object.entries(groupedCommands).map(([category, commands]) => {
                // Calculate global indices for commands
                let commandIndex = 0;
                for (const cat of Object.keys(groupedCommands)) {
                  if (cat === category) break;
                  commandIndex += groupedCommands[cat].length;
                }

                return (
                  <div key={category}>
                    <div className={styles.categoryHeader}>
                      <Text>{category}</Text>
                    </div>
                    {commands.map((command, localIndex) => {
                      const globalIndex = commandIndex + localIndex;
                      const Icon = command.icon || Document24Regular;
                      return (
                        <div
                          key={command.id}
                          className={mergeClasses(
                            styles.commandItem,
                            globalIndex === selectedIndex && styles.selectedItem
                          )}
                          role="button"
                          tabIndex={0}
                          onClick={() => {
                            trackCommandUsage(command.id);
                            command.action();
                            onClose();
                            setSearchQuery('');
                          }}
                          onKeyDown={(e) => {
                            if (e.key === 'Enter' || e.key === ' ') {
                              e.preventDefault();
                              trackCommandUsage(command.id);
                              command.action();
                              onClose();
                              setSearchQuery('');
                            }
                          }}
                          onMouseEnter={() => setSelectedIndex(globalIndex)}
                        >
                          <Icon className={styles.icon} />
                          <div className={styles.commandContent}>
                            <Text className={styles.commandName}>{command.name}</Text>
                            <Text className={styles.commandDescription}>{command.description}</Text>
                          </div>
                          {command.shortcut && (
                            <Badge appearance="outline" className={styles.shortcut}>
                              {command.shortcut}
                            </Badge>
                          )}
                        </div>
                      );
                    })}
                  </div>
                );
              })
            )}
          </div>
        </DialogBody>
      </DialogSurface>
    </Dialog>
  );
}
