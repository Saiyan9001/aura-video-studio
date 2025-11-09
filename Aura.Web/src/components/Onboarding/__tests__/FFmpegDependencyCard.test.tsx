import { render, screen, waitFor } from '@testing-library/react';
import { describe, expect, beforeEach, vi, it } from 'vitest';
import type { FFmpegStatus } from '../../../services/api/ffmpegClient';
import { ffmpegClient } from '../../../services/api/ffmpegClient';
import { FFmpegDependencyCard } from '../FFmpegDependencyCard';

vi.mock('../../../services/api/ffmpegClient', () => ({
  ffmpegClient: {
    getStatus: vi.fn(),
    install: vi.fn(),
  },
}));

const createStatus = (overrides: Partial<FFmpegStatus> = {}): FFmpegStatus => ({
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
  correlationId: 'test-correlation',
  ...overrides,
});

const mockedGetStatus = vi.mocked(ffmpegClient.getStatus);

describe('FFmpegDependencyCard', () => {
  beforeEach(() => {
    mockedGetStatus.mockReset();
  });

  it('shows install button when FFmpeg is not ready', async () => {
    mockedGetStatus.mockResolvedValueOnce(
      createStatus({
        installed: false,
        valid: false,
        error: 'FFmpeg not found',
      })
    );

    render(<FFmpegDependencyCard autoCheck autoExpandDetails />);

    await waitFor(() => {
      expect(screen.getByRole('button', { name: /install managed ffmpeg/i })).toBeVisible();
    });
  });

  it('invokes onInstallComplete when FFmpeg is detected without a version', async () => {
    const onInstallComplete = vi.fn();
    mockedGetStatus.mockResolvedValueOnce(
      createStatus({
        installed: true,
        valid: true,
        version: null,
        path: '/usr/bin/ffmpeg',
        source: 'PATH',
        versionMeetsRequirement: true,
      })
    );

    render(
      <FFmpegDependencyCard autoCheck autoExpandDetails onInstallComplete={onInstallComplete} />
    );

    await waitFor(() => {
      expect(onInstallComplete).toHaveBeenCalledTimes(1);
    });
  });
});
