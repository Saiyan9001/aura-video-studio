import { API_BASE_URL } from '../config/api';

export interface DependencyCheckResult {
  success: boolean;
  ffmpeg: {
    installed: boolean;
    version: string | null;
    installationRequired: boolean;
  };
  nodejs: {
    installed: boolean;
    version: string | null;
  };
  dotnet: {
    installed: boolean;
    version: string | null;
  };
  python: {
    installed: boolean;
    version: string | null;
  };
  ollama: {
    installed: boolean;
    version: string | null;
  };
  piperTts: {
    installed: boolean;
    path: string | null;
  };
  nvidia: {
    installed: boolean;
    version: string | null;
  };
  diskSpaceGB: number;
  internetConnected: boolean;
}

export interface ProviderAvailabilityResult {
  providerName: string;
  providerType: string;
  isAvailable: boolean;
  isReachable: boolean;
  status: string | null;
  latency: number | null;
  errorMessage: string | null;
}

export interface ProviderAvailabilityReport {
  success: boolean;
  timestamp: string;
  providers: ProviderAvailabilityResult[];
  ollamaAvailable: boolean;
  stableDiffusionAvailable: boolean;
  databaseAvailable: boolean;
  networkConnected: boolean;
}

export interface AutoConfigurationResult {
  success: boolean;
  recommendedThreadCount: number;
  recommendedMemoryLimitMB: number;
  recommendedQualityPreset: string;
  useHardwareAcceleration: boolean;
  hardwareAccelerationMethod: string | null;
  enableLocalProviders: boolean;
  recommendedTier: string;
  configuredProviders: string[];
}

export class SetupService {
  private baseUrl: string;

  constructor() {
    this.baseUrl = API_BASE_URL;
  }

  async checkDependencies(): Promise<DependencyCheckResult> {
    const response = await fetch(`${this.baseUrl}/api/dependencies/check`);
    if (!response.ok) {
      throw new Error(`Failed to check dependencies: ${response.statusText}`);
    }
    return await response.json();
  }

  async checkProviderAvailability(): Promise<ProviderAvailabilityReport> {
    const response = await fetch(`${this.baseUrl}/api/diagnostics/providers/availability`);
    if (!response.ok) {
      throw new Error(`Failed to check provider availability: ${response.statusText}`);
    }
    return await response.json();
  }

  async getAutoConfiguration(): Promise<AutoConfigurationResult> {
    const response = await fetch(`${this.baseUrl}/api/diagnostics/auto-config`);
    if (!response.ok) {
      throw new Error(`Failed to get auto-configuration: ${response.statusText}`);
    }
    return await response.json();
  }
}

export const setupService = new SetupService();
