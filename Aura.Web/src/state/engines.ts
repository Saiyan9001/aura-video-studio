import { create } from 'zustand';
import type { EngineManifestEntry, EngineStatus } from '../types/engines';

interface EnginesState {
  engines: EngineManifestEntry[];
  engineStatuses: Map<string, EngineStatus>;
  isLoading: boolean;
  error: string | null;
  
  // Actions
  fetchEngines: () => Promise<void>;
  fetchEngineStatus: (engineId: string) => Promise<void>;
  installEngine: (engineId: string, version?: string, port?: number, customUrl?: string, localFilePath?: string) => Promise<void>;
  verifyEngine: (engineId: string) => Promise<any>;
  repairEngine: (engineId: string) => Promise<void>;
  removeEngine: (engineId: string) => Promise<void>;
  startEngine: (engineId: string, port?: number, args?: string) => Promise<void>;
  stopEngine: (engineId: string) => Promise<void>;
  refreshStatus: (engineId: string) => Promise<void>;
  getDiagnostics: (engineId: string) => Promise<any>;
  getProvenance: (engineId: string) => Promise<any>;
  openFolder: (engineId: string) => Promise<void>;
}

export const useEnginesStore = create<EnginesState>((set, get) => ({
  engines: [],
  engineStatuses: new Map(),
  isLoading: false,
  error: null,

  fetchEngines: async () => {
    set({ isLoading: true, error: null });
    try {
      const response = await fetch('http://127.0.0.1:5005/api/engines/list');
      if (!response.ok) {
        throw new Error(`Failed to fetch engines: ${response.statusText}`);
      }
      const data = await response.json();
      set({ engines: data.engines, isLoading: false });
    } catch (error) {
      set({ 
        error: error instanceof Error ? error.message : 'Failed to fetch engines',
        isLoading: false 
      });
    }
  },

  fetchEngineStatus: async (engineId: string) => {
    try {
      const response = await fetch(`http://127.0.0.1:5005/api/engines/status?engineId=${engineId}`);
      if (!response.ok) {
        throw new Error(`Failed to fetch status: ${response.statusText}`);
      }
      const data = await response.json();
      
      // Transform API response to match EngineStatus interface
      const status: EngineStatus = {
        engineId: data.engineId,
        name: data.name,
        status: data.status,
        installedVersion: data.installedVersion,
        isInstalled: data.status !== 'not_installed',
        isRunning: data.isRunning,
        isHealthy: data.health === 'healthy',
        port: data.port,
        health: data.health,
        processId: data.processId,
        logsPath: data.logsPath,
        messages: data.messages || [],
      };
      
      const newStatuses = new Map(get().engineStatuses);
      newStatuses.set(engineId, status);
      set({ engineStatuses: newStatuses });
    } catch (error) {
      console.error(`Failed to fetch status for ${engineId}:`, error);
    }
  },

  installEngine: async (engineId: string, version?: string, port?: number, customUrl?: string, localFilePath?: string) => {
    set({ isLoading: true, error: null });
    try {
      const response = await fetch('http://127.0.0.1:5005/api/engines/install', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ engineId, version, port, customUrl, localFilePath }),
      });
      
      if (!response.ok) {
        const errorData = await response.json();
        throw new Error(errorData.error || 'Installation failed');
      }
      
      await get().fetchEngines();
      await get().fetchEngineStatus(engineId);
      set({ isLoading: false });
    } catch (error) {
      set({ 
        error: error instanceof Error ? error.message : 'Installation failed',
        isLoading: false 
      });
      throw error;
    }
  },

  verifyEngine: async (engineId: string) => {
    try {
      const response = await fetch('http://127.0.0.1:5005/api/engines/verify', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ engineId }),
      });
      
      if (!response.ok) {
        const errorData = await response.json();
        throw new Error(errorData.error || 'Verification failed');
      }
      
      const result = await response.json();
      return result;
    } catch (error) {
      console.error(`Failed to verify ${engineId}:`, error);
      throw error;
    }
  },

  repairEngine: async (engineId: string) => {
    set({ isLoading: true, error: null });
    try {
      const response = await fetch('http://127.0.0.1:5005/api/engines/repair', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ engineId }),
      });
      
      if (!response.ok) {
        const errorData = await response.json();
        throw new Error(errorData.error || 'Repair failed');
      }
      
      await get().fetchEngines();
      await get().fetchEngineStatus(engineId);
      set({ isLoading: false });
    } catch (error) {
      set({ 
        error: error instanceof Error ? error.message : 'Repair failed',
        isLoading: false 
      });
      throw error;
    }
  },

  removeEngine: async (engineId: string) => {
    set({ isLoading: true, error: null });
    try {
      const response = await fetch('http://127.0.0.1:5005/api/engines/remove', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ engineId }),
      });
      
      if (!response.ok) {
        const errorData = await response.json();
        throw new Error(errorData.error || 'Removal failed');
      }
      
      await get().fetchEngines();
      const newStatuses = new Map(get().engineStatuses);
      newStatuses.delete(engineId);
      set({ engineStatuses: newStatuses, isLoading: false });
    } catch (error) {
      set({ 
        error: error instanceof Error ? error.message : 'Removal failed',
        isLoading: false 
      });
      throw error;
    }
  },

  startEngine: async (engineId: string, port?: number, args?: string) => {
    set({ isLoading: true, error: null });
    try {
      const response = await fetch('http://127.0.0.1:5005/api/engines/start', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ engineId, port, args }),
      });
      
      if (!response.ok) {
        const errorData = await response.json();
        throw new Error(errorData.error || 'Failed to start engine');
      }
      
      await get().fetchEngineStatus(engineId);
      set({ isLoading: false });
    } catch (error) {
      set({ 
        error: error instanceof Error ? error.message : 'Failed to start engine',
        isLoading: false 
      });
      throw error;
    }
  },

  stopEngine: async (engineId: string) => {
    set({ isLoading: true, error: null });
    try {
      const response = await fetch('http://127.0.0.1:5005/api/engines/stop', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ engineId }),
      });
      
      if (!response.ok) {
        const errorData = await response.json();
        throw new Error(errorData.error || 'Failed to stop engine');
      }
      
      await get().fetchEngineStatus(engineId);
      set({ isLoading: false });
    } catch (error) {
      set({ 
        error: error instanceof Error ? error.message : 'Failed to stop engine',
        isLoading: false 
      });
      throw error;
    }
  },

  refreshStatus: async (engineId: string) => {
    await get().fetchEngineStatus(engineId);
  },

  getDiagnostics: async (engineId: string) => {
    try {
      const response = await fetch(`http://127.0.0.1:5005/api/engines/diagnostics/engine?engineId=${engineId}`);
      
      if (!response.ok) {
        const errorData = await response.json();
        throw new Error(errorData.error || 'Failed to get diagnostics');
      }
      
      const result = await response.json();
      return result;
    } catch (error) {
      console.error(`Failed to get diagnostics for ${engineId}:`, error);
      throw error;
    }
  },

  getProvenance: async (engineId: string) => {
    try {
      const response = await fetch(`http://127.0.0.1:5005/api/engines/provenance?engineId=${engineId}`);
      
      if (!response.ok) {
        const errorData = await response.json();
        throw new Error(errorData.error || 'Failed to get provenance');
      }
      
      const result = await response.json();
      return result;
    } catch (error) {
      console.error(`Failed to get provenance for ${engineId}:`, error);
      throw error;
    }
  },

  openFolder: async (engineId: string) => {
    try {
      const response = await fetch('http://127.0.0.1:5005/api/engines/open-folder', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ engineId }),
      });
      
      if (!response.ok) {
        const errorData = await response.json();
        throw new Error(errorData.error || 'Failed to open folder');
      }
    } catch (error) {
      console.error(`Failed to open folder for ${engineId}:`, error);
      throw error;
    }
  },
}));
