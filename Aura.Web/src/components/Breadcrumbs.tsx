import {
  makeStyles,
  tokens,
  Button,
  Menu,
  MenuTrigger,
  MenuPopover,
  MenuList,
  MenuItem,
  Badge,
  mergeClasses,
} from '@fluentui/react-components';
import { ChevronRight20Regular, ChevronDown20Regular } from '@fluentui/react-icons';
import { useMemo } from 'react';
import { useNavigate, useLocation } from 'react-router-dom';

const useStyles = makeStyles({
  breadcrumbs: {
    display: 'flex',
    alignItems: 'center',
    gap: tokens.spacingHorizontalXS,
    padding: `${tokens.spacingVerticalS} ${tokens.spacingHorizontalM}`,
    backgroundColor: tokens.colorNeutralBackground1,
    borderBottom: `1px solid ${tokens.colorNeutralStroke2}`,
    overflow: 'hidden',
  },
  breadcrumbItem: {
    display: 'flex',
    alignItems: 'center',
    gap: tokens.spacingHorizontalXS,
  },
  breadcrumbButton: {
    minWidth: 'auto',
    padding: `${tokens.spacingVerticalXS} ${tokens.spacingHorizontalS}`,
    fontSize: tokens.fontSizeBase300,
    color: tokens.colorNeutralForeground2,
    ':hover': {
      color: tokens.colorBrandForeground1,
      textDecoration: 'underline',
    },
  },
  currentBreadcrumb: {
    color: tokens.colorNeutralForeground1,
    fontWeight: tokens.fontWeightSemibold,
    ':hover': {
      textDecoration: 'none',
      cursor: 'default',
    },
  },
  separator: {
    color: tokens.colorNeutralForeground4,
    fontSize: '16px',
  },
  statusBadge: {
    marginLeft: tokens.spacingHorizontalS,
  },
  ellipsis: {
    padding: `${tokens.spacingVerticalXS} ${tokens.spacingHorizontalS}`,
  },
});

interface BreadcrumbSegment {
  label: string;
  path: string;
}

interface BreadcrumbsProps {
  maxVisible?: number;
  statusBadge?: {
    text: string;
    appearance?: 'filled' | 'outline' | 'tint' | 'ghost';
    color?:
      | 'brand'
      | 'danger'
      | 'important'
      | 'informative'
      | 'severe'
      | 'subtle'
      | 'success'
      | 'warning';
  };
}

// Map paths to user-friendly labels
const pathLabels: { [key: string]: string } = {
  '/': 'Home',
  '/dashboard': 'Dashboard',
  '/create': 'Create',
  '/projects': 'Projects',
  '/assets': 'Assets',
  '/learning': 'Learn',
  '/settings': 'Settings',
  '/render': 'Render',
  '/timeline': 'Timeline',
  '/editor': 'Editor',
  '/ideation': 'Ideation',
  '/health': 'Health',
  '/downloads': 'Downloads',
  '/templates': 'Templates',
  '/models': 'Models',
  '/jobs': 'Jobs',
  '/logs': 'Logs',
  '/platform': 'Platform',
  '/quality': 'Quality',
  '/trending': 'Trending',
  '/content-planning': 'Content Planning',
  '/pacing': 'Pacing',
  '/ai-editing': 'AI Editing',
  '/aesthetics': 'Aesthetics',
  '/localization': 'Localization',
  '/prompt-management': 'Prompts',
  '/rag': 'RAG Documents',
  '/voice-enhancement': 'Voice',
  '/performance-analytics': 'Analytics',
  '/ml-lab': 'ML Lab',
  '/quality-validation': 'Validation',
  '/verification': 'Verification',
};

export function Breadcrumbs({ maxVisible = 3, statusBadge }: BreadcrumbsProps) {
  const styles = useStyles();
  const navigate = useNavigate();
  const location = useLocation();

  // Parse the current path into breadcrumb segments
  const segments = useMemo((): BreadcrumbSegment[] => {
    const pathParts = location.pathname.split('/').filter(Boolean);

    // Always start with Home
    const breadcrumbs: BreadcrumbSegment[] = [{ label: 'Home', path: '/' }];

    // Build up the path progressively
    let currentPath = '';
    pathParts.forEach((part) => {
      currentPath += `/${part}`;
      const label = pathLabels[currentPath] || part.charAt(0).toUpperCase() + part.slice(1);
      breadcrumbs.push({ label, path: currentPath });
    });

    return breadcrumbs;
  }, [location.pathname]);

  // Handle long paths with ellipsis dropdown
  const { visibleSegments, hiddenSegments } = useMemo(() => {
    if (segments.length <= maxVisible) {
      return { visibleSegments: segments, hiddenSegments: [] };
    }

    // Always show first (Home) and last segment
    const first = segments[0];
    const last = segments[segments.length - 1];
    const hidden = segments.slice(1, segments.length - 1);

    return {
      visibleSegments: [first, last],
      hiddenSegments: hidden,
    };
  }, [segments, maxVisible]);

  const handleNavigate = (path: string) => {
    if (path !== location.pathname) {
      navigate(path);
    }
  };

  return (
    <nav className={styles.breadcrumbs} aria-label="Breadcrumb navigation">
      {visibleSegments.map((segment, index) => {
        const isLast = index === visibleSegments.length - 1;
        const isFirst = index === 0;

        return (
          <div key={segment.path} className={styles.breadcrumbItem}>
            {/* Show ellipsis menu for hidden segments after first item */}
            {isFirst && hiddenSegments.length > 0 && (
              <>
                <Button
                  appearance="transparent"
                  className={styles.breadcrumbButton}
                  onClick={() => handleNavigate(segment.path)}
                >
                  {segment.label}
                </Button>
                <ChevronRight20Regular className={styles.separator} />
                <Menu>
                  <MenuTrigger disableButtonEnhancement>
                    <Button
                      appearance="transparent"
                      className={styles.ellipsis}
                      icon={<ChevronDown20Regular />}
                    >
                      ...
                    </Button>
                  </MenuTrigger>
                  <MenuPopover>
                    <MenuList>
                      {hiddenSegments.map((hiddenSegment) => (
                        <MenuItem
                          key={hiddenSegment.path}
                          onClick={() => handleNavigate(hiddenSegment.path)}
                        >
                          {hiddenSegment.label}
                        </MenuItem>
                      ))}
                    </MenuList>
                  </MenuPopover>
                </Menu>
                {!isLast && <ChevronRight20Regular className={styles.separator} />}
              </>
            )}

            {/* Regular breadcrumb item (skip first if hidden segments exist) */}
            {!(isFirst && hiddenSegments.length > 0) && (
              <>
                {index > 0 && <ChevronRight20Regular className={styles.separator} />}
                <Button
                  appearance="transparent"
                  className={mergeClasses(
                    styles.breadcrumbButton,
                    isLast && styles.currentBreadcrumb
                  )}
                  onClick={() => !isLast && handleNavigate(segment.path)}
                  disabled={isLast}
                >
                  {segment.label}
                </Button>
              </>
            )}
          </div>
        );
      })}

      {/* Status badge */}
      {statusBadge && (
        <Badge
          className={styles.statusBadge}
          appearance={statusBadge.appearance || 'filled'}
          color={statusBadge.color || 'informative'}
        >
          {statusBadge.text}
        </Badge>
      )}
    </nav>
  );
}
