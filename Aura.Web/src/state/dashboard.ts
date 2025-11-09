import { create } from 'zustand';
import { persist } from 'zustand/middleware';

export interface ProjectSummary {
  id: string;
  name: string;
  description?: string;
  thumbnail?: string;
  videoUrl?: string;
  status: 'draft' | 'processing' | 'complete' | 'failed';
  createdAt: string;
  lastModifiedAt: string;
  duration: number;
  viewCount?: number;
  progress?: number;
  order: number;
}

export interface DashboardStats {
  videosToday: number;
  totalStorage: string;
  apiCredits: number;
}

export interface ProviderHealth {
  name: string;
  status: 'healthy' | 'degraded' | 'down';
  responseTime: number;
  errorRate: number;
}

export interface UsageData {
  date: string;
  apiCalls: number;
  cost: number;
}

export interface QuickInsights {
  mostUsedTemplate: string;
  averageVideoDuration: string;
  peakUsageHours: string;
  favoriteVoice: string;
}

export interface RecentBrief {
  id: string;
  title: string;
  topic: string;
  audience?: string;
  goal?: string;
  tone?: string;
  createdAt: string;
}

export interface DashboardLayout {
  showStats: boolean;
  showProjects: boolean;
  showAnalytics: boolean;
  showProviderHealth: boolean;
  showQuickInsights: boolean;
  showRecentBriefs: boolean;
  projectGridColumns: number;
  view: 'default' | 'compact' | 'analytics';
}

export interface DashboardFilter {
  status?: 'draft' | 'processing' | 'complete' | 'failed';
  dateFrom?: string;
  dateTo?: string;
  template?: string;
}

interface DashboardState {
  projects: ProjectSummary[];
  stats: DashboardStats;
  providerHealth: ProviderHealth[];
  usageData: UsageData[];
  quickInsights: QuickInsights;
  recentBriefs: RecentBrief[];
  layout: DashboardLayout;
  filter: DashboardFilter;
  loading: boolean;

  // Actions
  setProjects: (projects: ProjectSummary[]) => void;
  addProject: (project: ProjectSummary) => void;
  updateProject: (id: string, updates: Partial<ProjectSummary>) => void;
  removeProject: (id: string) => void;
  reorderProjects: (fromIndex: number, toIndex: number) => void;
  setStats: (stats: DashboardStats) => void;
  setProviderHealth: (health: ProviderHealth[]) => void;
  setUsageData: (data: UsageData[]) => void;
  setQuickInsights: (insights: QuickInsights) => void;
  setRecentBriefs: (briefs: RecentBrief[]) => void;
  addRecentBrief: (brief: RecentBrief) => void;
  updateLayout: (updates: Partial<DashboardLayout>) => void;
  setFilter: (filter: DashboardFilter) => void;
  clearFilter: () => void;
  fetchDashboardData: () => Promise<void>;
}

const defaultLayout: DashboardLayout = {
  showStats: true,
  showProjects: true,
  showAnalytics: true,
  showProviderHealth: true,
  showQuickInsights: true,
  showRecentBriefs: true,
  projectGridColumns: 3,
  view: 'default',
};

const defaultStats: DashboardStats = {
  videosToday: 0,
  totalStorage: '0 MB',
  apiCredits: 0,
};

const defaultInsights: QuickInsights = {
  mostUsedTemplate: 'N/A',
  averageVideoDuration: '0:00',
  peakUsageHours: 'N/A',
  favoriteVoice: 'N/A',
};

export const useDashboardStore = create<DashboardState>()(
  persist(
    (set) => ({
      projects: [],
      stats: defaultStats,
      providerHealth: [],
      usageData: [],
      quickInsights: defaultInsights,
      recentBriefs: [],
      layout: defaultLayout,
      filter: {},
      loading: false,

      setProjects: (projects) => set({ projects }),

      addProject: (project) =>
        set((state) => ({
          projects: [...state.projects, project],
        })),

      updateProject: (id, updates) =>
        set((state) => ({
          projects: state.projects.map((p) => (p.id === id ? { ...p, ...updates } : p)),
        })),

      removeProject: (id) =>
        set((state) => ({
          projects: state.projects.filter((p) => p.id !== id),
        })),

      reorderProjects: (fromIndex, toIndex) =>
        set((state) => {
          const projects = [...state.projects];
          const [removed] = projects.splice(fromIndex, 1);
          projects.splice(toIndex, 0, removed);
          return {
            projects: projects.map((p, i) => ({ ...p, order: i })),
          };
        }),

      setStats: (stats) => set({ stats }),

      setProviderHealth: (health) => set({ providerHealth: health }),

      setUsageData: (data) => set({ usageData: data }),

      setQuickInsights: (insights) => set({ quickInsights: insights }),

      setRecentBriefs: (briefs) => set({ recentBriefs: briefs }),

      addRecentBrief: (brief) =>
        set((state) => ({
          recentBriefs: [brief, ...state.recentBriefs].slice(0, 5),
        })),

      updateLayout: (updates) =>
        set((state) => ({
          layout: { ...state.layout, ...updates },
        })),

      setFilter: (filter) => set({ filter }),

      clearFilter: () => set({ filter: {} }),

      fetchDashboardData: async () => {
        set({ loading: true });
        try {
          // For now, generate mock data
          // This will be replaced with actual API calls
          const mockProjects: ProjectSummary[] = [];
          const mockStats: DashboardStats = {
            videosToday: 0,
            totalStorage: '0 MB',
            apiCredits: 1000,
          };
          const mockProviderHealth: ProviderHealth[] = [
            { name: 'OpenAI', status: 'healthy', responseTime: 120, errorRate: 0 },
            { name: 'ElevenLabs', status: 'healthy', responseTime: 250, errorRate: 0 },
            { name: 'FFmpeg', status: 'healthy', responseTime: 0, errorRate: 0 },
          ];
          const mockUsageData: UsageData[] = Array.from({ length: 7 }, (_, i) => ({
            date: new Date(Date.now() - (6 - i) * 24 * 60 * 60 * 1000).toISOString().split('T')[0],
            apiCalls: Math.floor(Math.random() * 100),
            cost: Math.random() * 10,
          }));
          const mockInsights: QuickInsights = {
            mostUsedTemplate: 'N/A',
            averageVideoDuration: '0:00',
            peakUsageHours: 'N/A',
            favoriteVoice: 'N/A',
          };

          set({
            projects: mockProjects,
            stats: mockStats,
            providerHealth: mockProviderHealth,
            usageData: mockUsageData,
            quickInsights: mockInsights,
          });
        } catch (error: unknown) {
          const errorMessage = error instanceof Error ? error.message : String(error);
          set({ loading: false });
          throw new Error(`Failed to fetch dashboard data: ${errorMessage}`);
        } finally {
          set({ loading: false });
        }
      },
    }),
    {
      name: 'dashboard-storage',
      partialize: (state) => ({
        layout: state.layout,
        filter: state.filter,
        projects: state.projects.map((p) => ({ ...p, order: p.order })),
      }),
    }
  )
);
