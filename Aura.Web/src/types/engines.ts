export interface EngineManifestEntry {
  id: string;
  name: string;
  version: string;
  description?: string;
  sizeBytes: number;
  defaultPort?: number;
  licenseUrl?: string;
  requiredVRAMGB?: number;
  isInstalled: boolean;
  installPath: string;
  // Gating information
  isGated?: boolean;
  canInstall?: boolean;
  canAutoStart?: boolean; // New: can the engine auto-start with current hardware
  gatingReason?: string;
  vramTooltip?: string;
  icon?: string;
  tags?: string[];
}

export interface EngineStatus {
  engineId: string;
  name: string;
  status: 'not_installed' | 'installed' | 'running';
  installedVersion?: string;
  isInstalled: boolean;
  isRunning: boolean;
  isHealthy: boolean;
  port?: number;
  health?: 'healthy' | 'unreachable' | null;
  processId?: number;
  logsPath?: string;
  messages: string[];
}

export interface EngineInstallProgress {
  engineId: string;
  phase: 'downloading' | 'extracting' | 'verifying' | 'complete' | 'error';
  bytesProcessed: number;
  totalBytes: number;
  percentComplete: number;
  message?: string;
}

export interface EngineVerificationResult {
  engineId: string;
  isValid: boolean;
  status: string;
  missingFiles: string[];
  issues: string[];
}

export interface InstallRequest {
  engineId: string;
  version?: string;
  port?: number;
  customUrl?: string;
  localFilePath?: string;
}

export interface InstallProvenance {
  engineId: string;
  version: string;
  installedAt: string;
  installPath: string;
  source: 'Mirror' | 'CustomUrl' | 'LocalFile';
  url: string;
  sha256: string;
}

export interface StartRequest {
  engineId: string;
  port?: number;
  args?: string;
}

export interface EngineActionRequest {
  engineId: string;
}
