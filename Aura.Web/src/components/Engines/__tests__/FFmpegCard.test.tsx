import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, it, expect, vi, beforeEach } from 'vitest';
import { FFmpegCard } from '../FFmpegCard';

// Mock the notifications hook
vi.mock('../../Notifications/Toasts', () => ({
  useNotifications: () => ({
    showSuccessToast: vi.fn(),
    showFailureToast: vi.fn(),
  }),
}));

// Mock the apiUrl function
vi.mock('../../../config/api', () => ({
  apiUrl: (path: string) => `http://localhost:5005${path}`,
}));

// Mock ManualInstallationModal
vi.mock('../ManualInstallationModal', () => ({
  ManualInstallationModal: () => <div>Manual Installation Modal</div>,
}));

describe('FFmpegCard', () => {
  beforeEach(() => {
    // Clear all mocks before each test
    vi.clearAllMocks();
    // Reset fetch mock
    global.fetch = vi.fn();
  });

  it('should show loading state initially', () => {
    // Mock fetch to never resolve (to keep loading state)
    global.fetch = vi.fn(() => new Promise(() => {}));

    render(<FFmpegCard />);
    expect(screen.getByText(/Loading FFmpeg status.../i)).toBeInTheDocument();
  });

  it('should NOT show "Installed" badge when version is null', async () => {
    const statusResponse = {
      installed: true,
      valid: true,
      version: null,
      path: '/usr/bin/ffmpeg',
      source: 'PATH',
      error: null,
      versionMeetsRequirement: false,
      minimumVersion: '4.0',
      hardwareAcceleration: {
        nvencSupported: false,
        amfSupported: false,
        quickSyncSupported: false,
        videoToolboxSupported: false,
        availableEncoders: [],
      },
    };

    global.fetch = vi.fn(() =>
      Promise.resolve({
        ok: true,
        json: () => Promise.resolve(statusResponse),
      } as Response)
    );

    render(<FFmpegCard />);

    await waitFor(() => {
      expect(screen.queryByText(/^Installed$/)).not.toBeInTheDocument();
    });
  });

  it('should NOT show "Installed" badge when valid is false', async () => {
    const statusResponse = {
      installed: true,
      valid: false,
      version: '4.4.2',
      path: '/usr/bin/ffmpeg',
      source: 'PATH',
      error: 'FFmpeg executable is corrupt',
      versionMeetsRequirement: true,
      minimumVersion: '4.0',
      hardwareAcceleration: {
        nvencSupported: false,
        amfSupported: false,
        quickSyncSupported: false,
        videoToolboxSupported: false,
        availableEncoders: [],
      },
    };

    global.fetch = vi.fn(() =>
      Promise.resolve({
        ok: true,
        json: () => Promise.resolve(statusResponse),
      } as Response)
    );

    render(<FFmpegCard />);

    await waitFor(() => {
      const badge = screen.getByText(/Invalid/i);
      expect(badge).toBeInTheDocument();
    });
  });

  it('should show "Installed" badge only when installed, valid, and has version', async () => {
    const statusResponse = {
      installed: true,
      valid: true,
      version: '4.4.2',
      path: '/usr/bin/ffmpeg',
      source: 'Managed',
      error: null,
      versionMeetsRequirement: true,
      minimumVersion: '4.0',
      hardwareAcceleration: {
        nvencSupported: true,
        amfSupported: false,
        quickSyncSupported: false,
        videoToolboxSupported: false,
        availableEncoders: ['h264_nvenc', 'hevc_nvenc'],
      },
    };

    global.fetch = vi.fn(() =>
      Promise.resolve({
        ok: true,
        json: () => Promise.resolve(statusResponse),
      } as Response)
    );

    render(<FFmpegCard />);

    await waitFor(() => {
      expect(screen.getByText(/^Installed$/)).toBeInTheDocument();
    });
  });

  it('should display Version, Path, and Source when FFmpeg is installed', async () => {
    const statusResponse = {
      installed: true,
      valid: true,
      version: '5.1.2',
      path: 'C:\\Program Files\\AuraVideoStudio\\ffmpeg\\5.1.2\\bin\\ffmpeg.exe',
      source: 'Managed',
      error: null,
      versionMeetsRequirement: true,
      minimumVersion: '4.0',
      hardwareAcceleration: {
        nvencSupported: false,
        amfSupported: true,
        quickSyncSupported: false,
        videoToolboxSupported: false,
        availableEncoders: ['h264_amf', 'hevc_amf'],
      },
    };

    global.fetch = vi.fn(() =>
      Promise.resolve({
        ok: true,
        json: () => Promise.resolve(statusResponse),
      } as Response)
    );

    render(<FFmpegCard />);

    await waitFor(() => {
      expect(screen.getByText(/Version: 5\.1\.2/i)).toBeInTheDocument();
      expect(screen.getByText(/✓ 4\.0\+/i)).toBeInTheDocument();
      expect(
        screen.getByText('C:\\Program Files\\AuraVideoStudio\\ffmpeg\\5.1.2\\bin\\ffmpeg.exe')
      ).toBeInTheDocument();
      expect(screen.getByText(/Managed Installation/i)).toBeInTheDocument();
    });
  });

  it('should display "Outdated" badge when version does not meet requirement', async () => {
    const statusResponse = {
      installed: true,
      valid: true,
      version: '3.4.0',
      path: '/opt/ffmpeg/bin/ffmpeg',
      source: 'Configured',
      error: null,
      versionMeetsRequirement: false,
      minimumVersion: '4.0',
      hardwareAcceleration: {
        nvencSupported: false,
        amfSupported: false,
        quickSyncSupported: false,
        videoToolboxSupported: false,
        availableEncoders: ['libx264'],
      },
    };

    global.fetch = vi.fn(() =>
      Promise.resolve({
        ok: true,
        json: () => Promise.resolve(statusResponse),
      } as Response)
    );

    render(<FFmpegCard />);

    await waitFor(() => {
      expect(screen.getByText(/Outdated/i)).toBeInTheDocument();
      expect(screen.getByText(/Requires 4\.0\+/i)).toBeInTheDocument();
    });
  });

  it('should show "Install Managed FFmpeg" button when not ready', async () => {
    const statusResponse = {
      installed: false,
      valid: false,
      version: null,
      path: null,
      source: 'None',
      error: 'FFmpeg not found',
      versionMeetsRequirement: false,
      minimumVersion: '4.0',
      hardwareAcceleration: {
        nvencSupported: false,
        amfSupported: false,
        quickSyncSupported: false,
        videoToolboxSupported: false,
        availableEncoders: [],
      },
    };

    global.fetch = vi.fn(() =>
      Promise.resolve({
        ok: true,
        json: () => Promise.resolve(statusResponse),
      } as Response)
    );

    render(<FFmpegCard />);

    await waitFor(() => {
      expect(screen.getByText(/Install Managed FFmpeg/i)).toBeInTheDocument();
      expect(screen.getByText(/Attach Existing.../i)).toBeInTheDocument();
    });
  });

  it('should display Source as "User Configured" for Configured source', async () => {
    const statusResponse = {
      installed: true,
      valid: true,
      version: '4.4.2',
      path: '/custom/path/ffmpeg',
      source: 'Configured',
      error: null,
      versionMeetsRequirement: true,
      minimumVersion: '4.0',
      hardwareAcceleration: {
        nvencSupported: false,
        amfSupported: false,
        quickSyncSupported: false,
        videoToolboxSupported: false,
        availableEncoders: [],
      },
    };

    global.fetch = vi.fn(() =>
      Promise.resolve({
        ok: true,
        json: () => Promise.resolve(statusResponse),
      } as Response)
    );

    render(<FFmpegCard />);

    await waitFor(() => {
      expect(screen.getByText(/User Configured/i)).toBeInTheDocument();
    });
  });

  it('should display Source as "System PATH" for PATH source', async () => {
    const statusResponse = {
      installed: true,
      valid: true,
      version: '4.4.2',
      path: '/usr/bin/ffmpeg',
      source: 'PATH',
      error: null,
      versionMeetsRequirement: true,
      minimumVersion: '4.0',
      hardwareAcceleration: {
        nvencSupported: false,
        amfSupported: false,
        quickSyncSupported: false,
        videoToolboxSupported: false,
        availableEncoders: [],
      },
    };

    global.fetch = vi.fn(() =>
      Promise.resolve({
        ok: true,
        json: () => Promise.resolve(statusResponse),
      } as Response)
    );

    render(<FFmpegCard />);

    await waitFor(() => {
      expect(screen.getByText(/System PATH/i)).toBeInTheDocument();
    });
  });

  it('should call correct API endpoint for status check', async () => {
    const fetchMock = vi.fn(() =>
      Promise.resolve({
        ok: true,
        json: () =>
          Promise.resolve({
            installed: false,
            valid: false,
            version: null,
            path: null,
            source: 'None',
            error: null,
            versionMeetsRequirement: false,
            minimumVersion: '4.0',
            hardwareAcceleration: {
              nvencSupported: false,
              amfSupported: false,
              quickSyncSupported: false,
              videoToolboxSupported: false,
              availableEncoders: [],
            },
          }),
      } as Response)
    );

    global.fetch = fetchMock;

    render(<FFmpegCard />);

    await waitFor(() => {
      expect(fetchMock).toHaveBeenCalledWith('http://localhost:5005/api/system/ffmpeg/status');
    });
  });
});
