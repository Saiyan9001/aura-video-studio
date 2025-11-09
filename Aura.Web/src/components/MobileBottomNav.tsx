import { makeStyles, tokens, Button, Tooltip } from '@fluentui/react-components';
import {
  Home24Regular,
  VideoClip24Regular,
  Lightbulb24Regular,
  VideoClipMultiple24Regular,
  Settings24Regular,
} from '@fluentui/react-icons';
import { useNavigate, useLocation } from 'react-router-dom';

const useStyles = makeStyles({
  bottomNav: {
    position: 'fixed',
    bottom: 0,
    left: 0,
    right: 0,
    height: '64px',
    backgroundColor: tokens.colorNeutralBackground2,
    borderTop: `1px solid ${tokens.colorNeutralStroke1}`,
    display: 'none',
    alignItems: 'center',
    justifyContent: 'space-around',
    padding: `0 ${tokens.spacingHorizontalM}`,
    boxShadow: '0 -2px 8px rgba(0, 0, 0, 0.1)',
    zIndex: 1000,
    '@media (max-width: 768px)': {
      display: 'flex',
    },
  },
  navButton: {
    flex: 1,
    display: 'flex',
    flexDirection: 'column',
    alignItems: 'center',
    justifyContent: 'center',
    minWidth: 'auto',
    height: '100%',
    borderRadius: '0',
    gap: tokens.spacingVerticalXXS,
    fontSize: '10px',
    padding: tokens.spacingVerticalXS,
  },
  navContent: {
    display: 'flex',
    flexDirection: 'column',
    alignItems: 'center',
    gap: '2px',
  },
  navIcon: {
    fontSize: '24px',
  },
  navLabel: {
    fontSize: tokens.fontSizeBase200,
    lineHeight: '1',
  },
});

interface NavItem {
  key: string;
  label: string;
  icon: React.ComponentType;
  path: string;
}

export function MobileBottomNav() {
  const styles = useStyles();
  const navigate = useNavigate();
  const location = useLocation();

  const navItems: NavItem[] = [
    { key: 'home', label: 'Home', icon: Home24Regular, path: '/' },
    { key: 'ideation', label: 'Ideation', icon: Lightbulb24Regular, path: '/ideation' },
    { key: 'create', label: 'Create', icon: VideoClip24Regular, path: '/create' },
    { key: 'editor', label: 'Editor', icon: VideoClipMultiple24Regular, path: '/editor' },
    { key: 'settings', label: 'Settings', icon: Settings24Regular, path: '/settings' },
  ];

  return (
    <nav className={styles.bottomNav} role="navigation" aria-label="Mobile bottom navigation">
      {navItems.map((item) => {
        const Icon = item.icon;
        const isActive = location.pathname === item.path;
        return (
          <Tooltip key={item.key} content={item.label} relationship="label">
            <Button
              appearance={isActive ? 'primary' : 'subtle'}
              className={styles.navButton}
              onClick={() => navigate(item.path)}
              aria-label={item.label}
              aria-current={isActive ? 'page' : undefined}
            >
              <div className={styles.navContent}>
                <Icon />
                <span className={styles.navLabel}>{item.label}</span>
              </div>
            </Button>
          </Tooltip>
        );
      })}
    </nav>
  );
}
