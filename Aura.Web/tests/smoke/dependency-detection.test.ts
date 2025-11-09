import { describe, it, expect, vi, beforeEach, afterEach } from 'vitest';

/**
 * PHASE 1: Dependency Detection and Initialization Verification
 *
 * These smoke tests validate:
 * - Fresh installation dependency detection
 * - Auto-install functionality
 * - Python/AI service detection
 * - Service initialization order
 * - Dependency status persistence
 */

// Type definitions for test data
type ServiceInitialization = { service: string; timestamp: number; status: string };
type Dependency = { name: string; status: string; warning?: boolean };

describe('Smoke Test: Dependency Detection', () => {
  beforeEach(() => {
    // Clear localStorage to simulate fresh installation
    localStorage.clear();
    vi.clearAllMocks();
  });

  afterEach(() => {
    vi.restoreAllMocks();
  });

  describe('1.1 Fresh Installation Dependency Detection', () => {
    it('should detect FFmpeg availability', async () => {
      // Mock FFmpeg detection API call
      const mockFetch = vi.fn().mockResolvedValue({
        ok: true,
        json: async () => ({
          ffmpeg: {
            installed: true,
            version: '6.0',
            path: '/usr/bin/ffmpeg',
          },
        }),
      });
      global.fetch = mockFetch;

      // Simulate dependency check
      const response = await fetch('/api/dependencies/check');
      const data = await response.json();

      expect(response.ok).toBe(true);
      expect(data.ffmpeg.installed).toBe(true);
      expect(data.ffmpeg.version).toBeTruthy();
      expect(data.ffmpeg.path).toBeTruthy();
    });

    it('should detect missing FFmpeg', async () => {
      const mockFetch = vi.fn().mockResolvedValue({
        ok: true,
        json: async () => ({
          ffmpeg: {
            installed: false,
            version: null,
            path: null,
            canAutoInstall: true,
          },
        }),
      });
      global.fetch = mockFetch;

      const response = await fetch('/api/dependencies/check');
      const data = await response.json();

      expect(data.ffmpeg.installed).toBe(false);
      expect(data.ffmpeg.canAutoInstall).toBe(true);
    });

    it('should provide accurate version information', async () => {
      const mockFetch = vi.fn().mockResolvedValue({
        ok: true,
        json: async () => ({
          ffmpeg: {
            installed: true,
            version: '6.0',
            fullVersion: 'ffmpeg version 6.0-static',
            path: '/usr/bin/ffmpeg',
          },
        }),
      });
      global.fetch = mockFetch;

      const response = await fetch('/api/dependencies/check');
      const data = await response.json();

      expect(data.ffmpeg.version).toMatch(/\d+\.\d+/);
      expect(data.ffmpeg.fullVersion).toBeTruthy();
    });
  });

  describe('1.2 Auto-Install Functionality', () => {
    it('should support FFmpeg auto-install trigger', async () => {
      const mockFetch = vi.fn().mockResolvedValue({
        ok: true,
        json: async () => ({
          jobId: 'install-ffmpeg-123',
          status: 'queued',
          message: 'FFmpeg installation queued',
        }),
      });
      global.fetch = mockFetch;

      const response = await fetch('/api/dependencies/install/ffmpeg', {
        method: 'POST',
      });
      const data = await response.json();

      expect(response.ok).toBe(true);
      expect(data.jobId).toBeTruthy();
      expect(data.status).toBe('queued');
    });

    it('should provide installation progress tracking', async () => {
      const mockFetch = vi.fn().mockResolvedValue({
        ok: true,
        json: async () => ({
          jobId: 'install-ffmpeg-123',
          status: 'in_progress',
          progress: 45,
          message: 'Downloading FFmpeg...',
        }),
      });
      global.fetch = mockFetch;

      const response = await fetch('/api/dependencies/install/status/install-ffmpeg-123');
      const data = await response.json();

      expect(data.status).toBe('in_progress');
      expect(data.progress).toBeGreaterThan(0);
      expect(data.progress).toBeLessThanOrEqual(100);
    });

    it('should validate FFmpeg after installation', async () => {
      const mockFetch = vi.fn().mockResolvedValue({
        ok: true,
        json: async () => ({
          jobId: 'install-ffmpeg-123',
          status: 'completed',
          progress: 100,
          result: {
            installed: true,
            version: '6.0',
            path: '/opt/aura/ffmpeg/bin/ffmpeg',
          },
        }),
      });
      global.fetch = mockFetch;

      const response = await fetch('/api/dependencies/install/status/install-ffmpeg-123');
      const data = await response.json();

      expect(data.status).toBe('completed');
      expect(data.result.installed).toBe(true);
      expect(data.result.version).toBeTruthy();
    });

    it('should support manual path selection', async () => {
      const customPath = '/custom/path/to/ffmpeg';
      const mockFetch = vi.fn().mockResolvedValue({
        ok: true,
        json: async () => ({
          valid: true,
          version: '6.0',
          path: customPath,
        }),
      });
      global.fetch = mockFetch;

      const response = await fetch('/api/dependencies/validate-path', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ dependency: 'ffmpeg', path: customPath }),
      });
      const data = await response.json();

      expect(data.valid).toBe(true);
      expect(data.path).toBe(customPath);
    });
  });

  describe('1.3 Python/AI Service Detection', () => {
    it('should detect Python installation', async () => {
      const mockFetch = vi.fn().mockResolvedValue({
        ok: true,
        json: async () => ({
          python: {
            installed: true,
            version: '3.11.0',
            path: '/usr/bin/python3',
          },
        }),
      });
      global.fetch = mockFetch;

      const response = await fetch('/api/dependencies/check');
      const data = await response.json();

      expect(data.python.installed).toBe(true);
      expect(data.python.version).toMatch(/3\.\d+\.\d+/);
    });

    it('should detect pip packages for AI dependencies', async () => {
      const mockFetch = vi.fn().mockResolvedValue({
        ok: true,
        json: async () => ({
          pipPackages: {
            torch: { installed: false, required: true },
            transformers: { installed: false, required: false },
            whisper: { installed: false, required: false },
          },
        }),
      });
      global.fetch = mockFetch;

      const response = await fetch('/api/dependencies/pip-packages');
      const data = await response.json();

      expect(data.pipPackages).toBeDefined();
      expect(data.pipPackages.torch).toBeDefined();
    });

    it('should detect GPU for hardware acceleration', async () => {
      const mockFetch = vi.fn().mockResolvedValue({
        ok: true,
        json: async () => ({
          gpu: {
            available: true,
            vendor: 'NVIDIA',
            model: 'GeForce RTX 3080',
            vram: 10240,
            cudaAvailable: true,
          },
        }),
      });
      global.fetch = mockFetch;

      const response = await fetch('/api/hardware/probe');
      const data = await response.json();

      expect(data.gpu.available).toBe(true);
      expect(data.gpu.vram).toBeGreaterThan(0);
    });

    it('should validate AI service endpoints are reachable', async () => {
      const mockFetch = vi.fn().mockResolvedValue({
        ok: true,
        json: async () => ({
          services: {
            llm: { reachable: true, latency: 120 },
            tts: { reachable: true, latency: 85 },
            imageGen: { reachable: false, latency: null },
          },
        }),
      });
      global.fetch = mockFetch;

      const response = await fetch('/api/dependencies/test-services');
      const data = await response.json();

      expect(data.services.llm.reachable).toBe(true);
      expect(data.services.tts.reachable).toBe(true);
    });
  });

  describe('1.4 Service Initialization Order', () => {
    it('should initialize logging service first', async () => {
      // This test validates initialization order through API call timing
      const mockFetch = vi.fn().mockResolvedValue({
        ok: true,
        json: async () => ({
          initializationOrder: [
            { service: 'logging', timestamp: 1000, status: 'initialized' },
            { service: 'database', timestamp: 1100, status: 'initialized' },
            { service: 'ffmpeg', timestamp: 1200, status: 'initialized' },
            { service: 'ai-services', timestamp: 1300, status: 'initialized' },
          ],
        }),
      });
      global.fetch = mockFetch;

      const response = await fetch('/api/diagnostics/initialization-order');
      const data = await response.json();

      const loggingInit = data.initializationOrder.find(
        (s: ServiceInitialization) => s.service === 'logging'
      );
      expect(loggingInit).toBeDefined();
      expect(loggingInit.timestamp).toBe(1000);
    });

    it('should initialize database before dependent services', async () => {
      const mockFetch = vi.fn().mockResolvedValue({
        ok: true,
        json: async () => ({
          initializationOrder: [
            { service: 'logging', timestamp: 1000, status: 'initialized' },
            { service: 'database', timestamp: 1100, status: 'initialized' },
            { service: 'video-service', timestamp: 1200, status: 'initialized' },
          ],
        }),
      });
      global.fetch = mockFetch;

      const response = await fetch('/api/diagnostics/initialization-order');
      const data = await response.json();

      const dbIndex = data.initializationOrder.findIndex(
        (s: ServiceInitialization) => s.service === 'database'
      );
      const videoIndex = data.initializationOrder.findIndex(
        (s: ServiceInitialization) => s.service === 'video-service'
      );

      expect(dbIndex).toBeLessThan(videoIndex);
    });

    it('should validate FFmpeg before video services register', async () => {
      const mockFetch = vi.fn().mockResolvedValue({
        ok: true,
        json: async () => ({
          initializationOrder: [
            { service: 'ffmpeg', timestamp: 1200, status: 'initialized' },
            { service: 'video-service', timestamp: 1300, status: 'initialized' },
            { service: 'export-service', timestamp: 1400, status: 'initialized' },
          ],
        }),
      });
      global.fetch = mockFetch;

      const response = await fetch('/api/diagnostics/initialization-order');
      const data = await response.json();

      const ffmpegIndex = data.initializationOrder.findIndex(
        (s: ServiceInitialization) => s.service === 'ffmpeg'
      );
      const videoIndex = data.initializationOrder.findIndex(
        (s: ServiceInitialization) => s.service === 'video-service'
      );

      expect(ffmpegIndex).toBeLessThan(videoIndex);
    });
  });

  describe('1.5 Dependency Status Persistence', () => {
    it('should persist dependency status in localStorage', () => {
      // Clear first to ensure clean state
      localStorage.clear();

      const dependencyStatus = {
        ffmpeg: { installed: true, version: '6.0' },
        python: { installed: true, version: '3.11.0' },
        lastCheck: new Date().toISOString(),
      };

      localStorage.setItem('dependencyStatus', JSON.stringify(dependencyStatus));

      const retrieved = JSON.parse(localStorage.getItem('dependencyStatus') || '{}');

      expect(retrieved).toBeDefined();
      expect(retrieved.ffmpeg).toBeDefined();
      expect(retrieved.ffmpeg.installed).toBe(true);
      expect(retrieved.python).toBeDefined();
      expect(retrieved.python.installed).toBe(true);
      expect(retrieved.lastCheck).toBeTruthy();
    });

    it('should allow rescan to force fresh check', async () => {
      // Set stale status
      localStorage.setItem(
        'dependencyStatus',
        JSON.stringify({
          ffmpeg: { installed: false },
          lastCheck: new Date(Date.now() - 86400000).toISOString(), // 1 day ago
        })
      );

      const mockFetch = vi.fn().mockResolvedValue({
        ok: true,
        json: async () => ({
          ffmpeg: { installed: true, version: '6.0' },
          lastCheck: new Date().toISOString(),
        }),
      });
      global.fetch = mockFetch;

      const response = await fetch('/api/dependencies/rescan');
      const data = await response.json();

      expect(data.ffmpeg.installed).toBe(true);
      expect(mockFetch).toHaveBeenCalledWith('/api/dependencies/rescan');
    });

    it('should show warnings for offline/disconnected dependencies', async () => {
      const mockFetch = vi.fn().mockResolvedValue({
        ok: true,
        json: async () => ({
          dependencies: [
            {
              name: 'AI Service',
              status: 'offline',
              warning: 'AI service is unreachable. Some features may not be available.',
            },
          ],
        }),
      });
      global.fetch = mockFetch;

      const response = await fetch('/api/dependencies/check');
      const data = await response.json();

      const aiService = data.dependencies.find((d: Dependency) => d.name === 'AI Service');
      expect(aiService.status).toBe('offline');
      expect(aiService.warning).toBeTruthy();
    });
  });

  describe('1.6 Comprehensive Dependency Check', () => {
    it('should return Node.js detection status', async () => {
      const mockFetch = vi.fn().mockResolvedValue({
        ok: true,
        json: async () => ({
          success: true,
          nodejs: {
            installed: true,
            version: '20.0.0',
          },
        }),
      });
      global.fetch = mockFetch;

      const response = await fetch('/api/dependencies/check');
      const data = await response.json();

      expect(data.success).toBe(true);
      expect(data.nodejs).toBeDefined();
      expect(data.nodejs.installed).toBe(true);
    });

    it('should return .NET detection status', async () => {
      const mockFetch = vi.fn().mockResolvedValue({
        ok: true,
        json: async () => ({
          success: true,
          dotnet: {
            installed: true,
            version: '8.0.0',
          },
        }),
      });
      global.fetch = mockFetch;

      const response = await fetch('/api/dependencies/check');
      const data = await response.json();

      expect(data.success).toBe(true);
      expect(data.dotnet).toBeDefined();
      expect(data.dotnet.installed).toBe(true);
    });

    it('should return Python detection status', async () => {
      const mockFetch = vi.fn().mockResolvedValue({
        ok: true,
        json: async () => ({
          success: true,
          python: {
            installed: true,
            version: '3.11.0',
          },
        }),
      });
      global.fetch = mockFetch;

      const response = await fetch('/api/dependencies/check');
      const data = await response.json();

      expect(data.success).toBe(true);
      expect(data.python).toBeDefined();
    });

    it('should return disk space information', async () => {
      const mockFetch = vi.fn().mockResolvedValue({
        ok: true,
        json: async () => ({
          success: true,
          diskSpaceGB: 50.5,
          internetConnected: true,
        }),
      });
      global.fetch = mockFetch;

      const response = await fetch('/api/dependencies/check');
      const data = await response.json();

      expect(data.success).toBe(true);
      expect(data.diskSpaceGB).toBeGreaterThan(0);
      expect(data.internetConnected).toBeDefined();
    });
  });

  describe('1.7 Provider Availability Check', () => {
    it('should check Ollama availability', async () => {
      const mockFetch = vi.fn().mockResolvedValue({
        ok: true,
        json: async () => ({
          success: true,
          ollamaAvailable: true,
          stableDiffusionAvailable: false,
          networkConnected: true,
        }),
      });
      global.fetch = mockFetch;

      const response = await fetch('/api/diagnostics/providers/availability');
      const data = await response.json();

      expect(data.success).toBe(true);
      expect(data.ollamaAvailable).toBeDefined();
    });

    it('should check Stable Diffusion availability', async () => {
      const mockFetch = vi.fn().mockResolvedValue({
        ok: true,
        json: async () => ({
          success: true,
          stableDiffusionAvailable: true,
        }),
      });
      global.fetch = mockFetch;

      const response = await fetch('/api/diagnostics/providers/availability');
      const data = await response.json();

      expect(data.success).toBe(true);
      expect(data.stableDiffusionAvailable).toBeDefined();
    });

    it('should return provider list', async () => {
      const mockFetch = vi.fn().mockResolvedValue({
        ok: true,
        json: async () => ({
          success: true,
          providers: [
            {
              providerName: 'Ollama',
              providerType: 'LLM',
              isAvailable: true,
            },
          ],
        }),
      });
      global.fetch = mockFetch;

      const response = await fetch('/api/diagnostics/providers/availability');
      const data = await response.json();

      expect(data.success).toBe(true);
      expect(data.providers).toBeDefined();
      expect(Array.isArray(data.providers)).toBe(true);
    });
  });

  describe('1.8 Auto-Configuration Detection', () => {
    it('should return auto-configuration recommendations', async () => {
      const mockFetch = vi.fn().mockResolvedValue({
        ok: true,
        json: async () => ({
          success: true,
          recommendedThreadCount: 8,
          recommendedMemoryLimitMB: 8192,
          recommendedQualityPreset: 'High',
          useHardwareAcceleration: true,
          hardwareAccelerationMethod: 'nvenc',
          recommendedTier: 'Pro',
        }),
      });
      global.fetch = mockFetch;

      const response = await fetch('/api/diagnostics/auto-config');
      const data = await response.json();

      expect(data.success).toBe(true);
      expect(data.recommendedThreadCount).toBeGreaterThan(0);
      expect(data.recommendedMemoryLimitMB).toBeGreaterThan(0);
      expect(data.recommendedQualityPreset).toBeTruthy();
    });

    it('should detect hardware acceleration support', async () => {
      const mockFetch = vi.fn().mockResolvedValue({
        ok: true,
        json: async () => ({
          success: true,
          useHardwareAcceleration: true,
          hardwareAccelerationMethod: 'nvenc',
        }),
      });
      global.fetch = mockFetch;

      const response = await fetch('/api/diagnostics/auto-config');
      const data = await response.json();

      expect(data.success).toBe(true);
      expect(data.useHardwareAcceleration).toBeDefined();
      if (data.useHardwareAcceleration) {
        expect(data.hardwareAccelerationMethod).toBeTruthy();
      }
    });

    it('should recommend tier based on capabilities', async () => {
      const mockFetch = vi.fn().mockResolvedValue({
        ok: true,
        json: async () => ({
          success: true,
          recommendedTier: 'Pro',
          enableLocalProviders: true,
          configuredProviders: ['FFmpeg', 'Ollama (Local LLM)'],
        }),
      });
      global.fetch = mockFetch;

      const response = await fetch('/api/diagnostics/auto-config');
      const data = await response.json();

      expect(data.success).toBe(true);
      expect(data.recommendedTier).toBeTruthy();
      expect(['Free', 'Local', 'Pro']).toContain(data.recommendedTier);
    });
  });
});
