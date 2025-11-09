import { render, screen, waitFor } from '@testing-library/react';
import { BrowserRouter } from 'react-router-dom';
import { describe, it, expect, vi, beforeEach } from 'vitest';
import { useDashboardStore } from '../../../state/dashboard';
import { Dashboard } from '../Dashboard';

// Mock the router
vi.mock('react-router-dom', async () => {
  const actual = await vi.importActual('react-router-dom');
  return {
    ...actual,
    useNavigate: () => vi.fn(),
  };
});

// Mock recharts to avoid canvas issues in tests
vi.mock('recharts', () => ({
  LineChart: ({ children }: { children: React.ReactNode }) => (
    <div data-testid="line-chart">{children}</div>
  ),
  Line: () => <div data-testid="line" />,
  XAxis: () => <div data-testid="x-axis" />,
  YAxis: () => <div data-testid="y-axis" />,
  CartesianGrid: () => <div data-testid="cartesian-grid" />,
  Tooltip: () => <div data-testid="tooltip" />,
  ResponsiveContainer: ({ children }: { children: React.ReactNode }) => (
    <div data-testid="responsive-container">{children}</div>
  ),
  Legend: () => <div data-testid="legend" />,
}));

describe('Dashboard', () => {
  beforeEach(() => {
    // Reset store state before each test
    useDashboardStore.setState({
      projects: [],
      stats: {
        videosToday: 0,
        totalStorage: '0 MB',
        apiCredits: 1000,
      },
      providerHealth: [],
      usageData: [],
      quickInsights: {
        mostUsedTemplate: 'N/A',
        averageVideoDuration: '0:00',
        peakUsageHours: 'N/A',
        favoriteVoice: 'N/A',
      },
      recentBriefs: [],
      loading: false,
      layout: {
        showStats: true,
        showProjects: true,
        showAnalytics: true,
        showProviderHealth: true,
        showQuickInsights: true,
        showRecentBriefs: true,
        projectGridColumns: 3,
        view: 'default',
      },
      fetchDashboardData: vi.fn(), // Mock the fetch function
    });
  });

  it('renders with greeting message', () => {
    render(
      <BrowserRouter>
        <Dashboard />
      </BrowserRouter>
    );

    // Check for greeting (should be one of the three based on time)
    const greeting = screen.getByText(/Good (morning|afternoon|evening)/);
    expect(greeting).toBeDefined();
  });

  it('displays quick stats bar', () => {
    render(
      <BrowserRouter>
        <Dashboard />
      </BrowserRouter>
    );

    expect(screen.getByText('Videos Today')).toBeDefined();
    expect(screen.getByText('Total Storage')).toBeDefined();
    expect(screen.getByText('API Credits')).toBeDefined();
  });

  it('shows empty state when no projects', () => {
    render(
      <BrowserRouter>
        <Dashboard />
      </BrowserRouter>
    );

    expect(screen.getByText('No projects yet')).toBeDefined();
    expect(screen.getByText('Get started by creating your first video')).toBeDefined();
  });

  it('displays projects when available', async () => {
    const currentState = useDashboardStore.getState();
    useDashboardStore.setState({
      ...currentState,
      projects: [
        {
          id: '1',
          name: 'Test Project',
          status: 'complete' as const,
          createdAt: new Date().toISOString(),
          lastModifiedAt: new Date().toISOString(),
          duration: 120,
          order: 0,
        },
      ],
    });

    render(
      <BrowserRouter>
        <Dashboard />
      </BrowserRouter>
    );

    await waitFor(() => {
      expect(screen.getByText('Test Project')).toBeDefined();
    });
  });

  it('displays quick start cards', () => {
    render(
      <BrowserRouter>
        <Dashboard />
      </BrowserRouter>
    );

    expect(screen.getByText('From Template')).toBeDefined();
    expect(screen.getByText('From Script')).toBeDefined();
    expect(screen.getByText('Batch Create')).toBeDefined();
    expect(screen.getByText('Import Project')).toBeDefined();
  });

  it('shows loading spinner when loading', async () => {
    const currentState = useDashboardStore.getState();
    useDashboardStore.setState({ ...currentState, loading: true, projects: [] });

    render(
      <BrowserRouter>
        <Dashboard />
      </BrowserRouter>
    );

    await waitFor(() => {
      expect(screen.getByText('Loading dashboard...')).toBeDefined();
    });
  });

  it('displays analytics widgets', () => {
    useDashboardStore.setState({
      usageData: [
        { date: '2024-01-01', apiCalls: 10, cost: 5 },
        { date: '2024-01-02', apiCalls: 20, cost: 10 },
      ],
      providerHealth: [
        { name: 'OpenAI', status: 'healthy' as const, responseTime: 120, errorRate: 0 },
      ],
      layout: {
        showStats: true,
        showProjects: true,
        showAnalytics: true,
        showProviderHealth: true,
        showQuickInsights: true,
        showRecentBriefs: true,
        projectGridColumns: 3,
        view: 'default',
      },
    });

    render(
      <BrowserRouter>
        <Dashboard />
      </BrowserRouter>
    );

    expect(screen.getByText('API Usage')).toBeDefined();
    expect(screen.getByText('Provider Health')).toBeDefined();
    expect(screen.getByText('Quick Insights')).toBeDefined();
  });

  it('hides sections based on layout settings', () => {
    useDashboardStore.setState({
      layout: {
        showStats: false,
        showProjects: false,
        showAnalytics: false,
        showProviderHealth: false,
        showQuickInsights: false,
        showRecentBriefs: false,
        projectGridColumns: 3,
        view: 'default',
      },
    });

    render(
      <BrowserRouter>
        <Dashboard />
      </BrowserRouter>
    );

    expect(screen.queryByText('Videos Today')).toBeNull();
    expect(screen.queryByText('Recent Projects')).toBeNull();
  });

  it('displays recent briefs when available', () => {
    useDashboardStore.setState({
      recentBriefs: [
        {
          id: '1',
          title: 'Test Brief',
          topic: 'Test Topic',
          createdAt: new Date().toISOString(),
        },
      ],
      layout: {
        showStats: true,
        showProjects: true,
        showAnalytics: true,
        showProviderHealth: true,
        showQuickInsights: true,
        showRecentBriefs: true,
        projectGridColumns: 3,
        view: 'default',
      },
    });

    render(
      <BrowserRouter>
        <Dashboard />
      </BrowserRouter>
    );

    expect(screen.getByText('Recent Briefs')).toBeDefined();
    expect(screen.getByText('Test Brief')).toBeDefined();
  });
});
