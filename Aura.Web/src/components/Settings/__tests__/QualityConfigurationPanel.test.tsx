/**
 * Tests for Quality Configuration Panel
 */

import { render, screen, fireEvent, waitFor } from '@testing-library/react';
import { describe, it, expect, vi, beforeEach } from 'vitest';
import apiClient from '../../../services/api/apiClient';
import { QualityConfigurationPanel } from '../QualityConfigurationPanel';

vi.mock('../../../services/api/apiClient');

const mockQualityConfig = {
  video: {
    resolution: '1080p',
    width: 1920,
    height: 1080,
    framerate: 30,
    bitratePreset: 'High',
    bitrateKbps: 5000,
    codec: 'h264',
    container: 'mp4',
  },
  audio: {
    bitrate: 192,
    sampleRate: 48000,
    channels: 2,
    codec: 'aac',
  },
  subtitles: {
    fontFamily: 'Arial',
    fontSize: 24,
    fontColor: '#FFFFFF',
    backgroundColor: '#000000',
    backgroundOpacity: 0.7,
    position: 'Bottom',
    outlineWidth: 2,
    outlineColor: '#000000',
  },
};

describe('QualityConfigurationPanel', () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  it('renders quality configuration panel', async () => {
    vi.mocked(apiClient.get).mockResolvedValue({ data: mockQualityConfig });

    render(<QualityConfigurationPanel />);

    await waitFor(() => {
      expect(screen.getByText('Quality Configuration')).toBeInTheDocument();
    });

    expect(
      screen.getByText('Configure video resolution, audio quality, and subtitle styles')
    ).toBeInTheDocument();
  });

  it('loads quality config on mount', async () => {
    vi.mocked(apiClient.get).mockResolvedValue({ data: mockQualityConfig });

    render(<QualityConfigurationPanel />);

    await waitFor(() => {
      expect(apiClient.get).toHaveBeenCalledWith('/api/providerconfiguration/quality');
    });

    await waitFor(() => {
      expect(screen.getByText('Video Settings')).toBeInTheDocument();
      expect(screen.getByText('Audio Settings')).toBeInTheDocument();
      expect(screen.getByText('Subtitle Style')).toBeInTheDocument();
    });
  });

  it('displays video quality settings', async () => {
    vi.mocked(apiClient.get).mockResolvedValue({ data: mockQualityConfig });

    render(<QualityConfigurationPanel />);

    await waitFor(() => {
      expect(screen.getByText('Resolution')).toBeInTheDocument();
      expect(screen.getByText('Frame Rate')).toBeInTheDocument();
      expect(screen.getByText('Codec')).toBeInTheDocument();
      expect(screen.getByText('Bitrate Preset')).toBeInTheDocument();
    });
  });

  it('displays audio quality settings', async () => {
    vi.mocked(apiClient.get).mockResolvedValue({ data: mockQualityConfig });

    render(<QualityConfigurationPanel />);

    await waitFor(() => {
      expect(screen.getByText('Audio Settings')).toBeInTheDocument();
    });

    const bitrateLabels = screen.getAllByText(/Bitrate/i);
    expect(bitrateLabels.length).toBeGreaterThan(0);

    expect(screen.getByText('Sample Rate (Hz)')).toBeInTheDocument();
    expect(screen.getByText('Channels')).toBeInTheDocument();
  });

  it('displays subtitle style settings', async () => {
    vi.mocked(apiClient.get).mockResolvedValue({ data: mockQualityConfig });

    render(<QualityConfigurationPanel />);

    await waitFor(() => {
      expect(screen.getByText('Subtitle Style')).toBeInTheDocument();
      expect(screen.getByText('Font Family')).toBeInTheDocument();
      expect(screen.getByText('Font Size (px)')).toBeInTheDocument();
      expect(screen.getByText('Font Color')).toBeInTheDocument();
      expect(screen.getByText('Background Color')).toBeInTheDocument();
      expect(screen.getByText('Position')).toBeInTheDocument();
    });
  });

  it('saves configuration when save button clicked', async () => {
    vi.mocked(apiClient.get).mockResolvedValue({ data: mockQualityConfig });
    vi.mocked(apiClient.post).mockResolvedValue({ data: { success: true } });

    render(<QualityConfigurationPanel />);

    await waitFor(() => {
      expect(screen.getByText('Video Settings')).toBeInTheDocument();
    });

    const saveButton = screen.getByRole('button', { name: /save configuration/i });
    fireEvent.click(saveButton);

    await waitFor(() => {
      expect(apiClient.post).toHaveBeenCalledWith(
        '/api/providerconfiguration/quality',
        expect.objectContaining({
          video: expect.any(Object),
          audio: expect.any(Object),
          subtitles: expect.any(Object),
        })
      );
    });
  });

  it('shows error message on load failure', async () => {
    vi.mocked(apiClient.get).mockRejectedValue(new Error('Network error'));

    render(<QualityConfigurationPanel />);

    await waitFor(() => {
      expect(screen.getByText(/failed to load quality configuration/i)).toBeInTheDocument();
    });
  });

  it('handles resolution preset changes', async () => {
    vi.mocked(apiClient.get).mockResolvedValue({ data: mockQualityConfig });

    render(<QualityConfigurationPanel />);

    await waitFor(() => {
      expect(screen.getByText('Video Settings')).toBeInTheDocument();
    });

    const resolutionLabel = screen.getByText('Resolution');
    expect(resolutionLabel).toBeInTheDocument();
  });
});
