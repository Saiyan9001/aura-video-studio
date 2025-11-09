/**
 * Application routes configuration
 * Centralized route paths for type-safe navigation
 */

export const ROUTES = {
  HOME: '/',
  SETUP: '/setup',
  ONBOARDING: '/onboarding', // Legacy route, redirects to SETUP
  DASHBOARD: '/dashboard',
  IDEATION: '/ideation',
  TRENDING: '/trending',
  CREATE: '/create',
  CREATE_LEGACY: '/create/legacy',
  TIMELINE: '/timeline',
  EDITOR: '/editor/:jobId',
  PACING: '/pacing',
  RENDER: '/render',

  PROJECTS: '/projects',
  ASSETS: '/assets',
  JOBS: '/jobs',
  DOWNLOADS: '/downloads',
  HEALTH: '/health',
  LOGS: '/logs',
  SETTINGS: '/settings',
} as const;

/**
 * Helper function to build editor route with job ID
 */
export function getEditorRoute(jobId: string): string {
  return `/editor/${jobId}`;
}

/**
 * Helper function to check if route is active
 */
export function isActiveRoute(currentPath: string, routePath: string): boolean {
  if (routePath === ROUTES.HOME) {
    return currentPath === routePath;
  }
  return currentPath.startsWith(routePath);
}

/**
 * Route metadata for navigation
 */
export interface RouteMetadata {
  path: string;
  title: string;
  description?: string;
  icon?: string;
  requiresAuth?: boolean;
}

export const ROUTE_METADATA: Record<string, RouteMetadata> = {
  HOME: {
    path: ROUTES.HOME,
    title: 'Welcome',
    description: 'Getting started with Aura Video Studio',
  },
  DASHBOARD: {
    path: ROUTES.DASHBOARD,
    title: 'Dashboard',
    description: 'Overview of your projects and recent activity',
  },
  IDEATION: {
    path: ROUTES.IDEATION,
    title: 'Ideation',
    description: 'AI-powered brainstorming and concept generation',
  },
  TRENDING: {
    path: ROUTES.TRENDING,
    title: 'Trending Topics',
    description: 'Explore trending topics and content ideas',
  },
  CREATE: {
    path: ROUTES.CREATE,
    title: 'Create',
    description: 'Create a new video project',
  },
  EDITOR: {
    path: ROUTES.EDITOR,
    title: 'Timeline Editor',
    description: 'Edit your video timeline',
  },
  PROJECTS: {
    path: ROUTES.PROJECTS,
    title: 'Projects',
    description: 'Manage your video projects',
  },
  ASSETS: {
    path: ROUTES.ASSETS,
    title: 'Asset Library',
    description: 'Manage media assets and stock content',
  },
  SETTINGS: {
    path: ROUTES.SETTINGS,
    title: 'Settings',
    description: 'Configure application settings',
  },
};
