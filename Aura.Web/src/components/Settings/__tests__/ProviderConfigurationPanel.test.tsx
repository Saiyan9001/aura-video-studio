/**
 * Tests for Provider Configuration Panel
 */

import { render, screen, fireEvent, waitFor } from '@testing-library/react';
import { describe, it, expect, vi, beforeEach } from 'vitest';
import apiClient from '../../../services/api/apiClient';
import { ProviderConfigurationPanel } from '../ProviderConfigurationPanel';

vi.mock('../../../services/api/apiClient');

const mockProviders = {
  providers: [
    {
      name: 'OpenAI',
      type: 'LLM',
      enabled: true,
      priority: 1,
      apiKey: null,
      additionalSettings: {},
      costLimit: null,
      status: 'Not configured',
    },
    {
      name: 'ElevenLabs',
      type: 'TTS',
      enabled: false,
      priority: 2,
      apiKey: null,
      additionalSettings: {},
      costLimit: 100,
      status: 'Not configured',
    },
  ],
};

describe('ProviderConfigurationPanel', () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  it('renders provider configuration panel', async () => {
    vi.mocked(apiClient.get).mockResolvedValue({ data: mockProviders });

    render(<ProviderConfigurationPanel />);

    await waitFor(() => {
      expect(screen.getByText('Provider Configuration')).toBeInTheDocument();
    });

    expect(
      screen.getByText('Configure API keys, priorities, and cost limits for all providers')
    ).toBeInTheDocument();
  });

  it('loads providers on mount', async () => {
    vi.mocked(apiClient.get).mockResolvedValue({ data: mockProviders });

    render(<ProviderConfigurationPanel />);

    await waitFor(() => {
      expect(apiClient.get).toHaveBeenCalledWith('/api/providerconfiguration/providers');
    });

    await waitFor(() => {
      expect(screen.getByText('OpenAI')).toBeInTheDocument();
      expect(screen.getByText('ElevenLabs')).toBeInTheDocument();
    });
  });

  it('displays provider status badges', async () => {
    vi.mocked(apiClient.get).mockResolvedValue({ data: mockProviders });

    render(<ProviderConfigurationPanel />);

    await waitFor(() => {
      expect(screen.getByText('Enabled')).toBeInTheDocument();
      expect(screen.getByText('Disabled')).toBeInTheDocument();
    });
  });

  it('saves configuration when save button clicked', async () => {
    vi.mocked(apiClient.get).mockResolvedValue({ data: mockProviders });
    vi.mocked(apiClient.post).mockResolvedValue({ data: { success: true } });

    render(<ProviderConfigurationPanel />);

    await waitFor(() => {
      expect(screen.getByText('OpenAI')).toBeInTheDocument();
    });

    const saveButton = screen.getByRole('button', { name: /save configuration/i });
    fireEvent.click(saveButton);

    await waitFor(() => {
      expect(apiClient.post).toHaveBeenCalledWith('/api/providerconfiguration/providers', {
        providers: expect.arrayContaining([
          expect.objectContaining({ name: 'OpenAI' }),
          expect.objectContaining({ name: 'ElevenLabs' }),
        ]),
      });
    });
  });

  it('shows error message on load failure', async () => {
    vi.mocked(apiClient.get).mockRejectedValue(new Error('Network error'));

    render(<ProviderConfigurationPanel />);

    await waitFor(() => {
      expect(screen.getByText(/failed to load provider configuration/i)).toBeInTheDocument();
    });
  });

  it('displays priority controls for providers', async () => {
    vi.mocked(apiClient.get).mockResolvedValue({ data: mockProviders });

    render(<ProviderConfigurationPanel />);

    await waitFor(() => {
      expect(screen.getAllByRole('button', { name: /move up/i })).toHaveLength(2);
      expect(screen.getAllByRole('button', { name: /move down/i })).toHaveLength(2);
    });
  });

  it('tests provider connection with API key', async () => {
    const providersWithKey = {
      providers: [
        {
          ...mockProviders.providers[0],
          apiKey: 'test-key-123',
        },
      ],
    };

    vi.mocked(apiClient.get).mockResolvedValue({ data: providersWithKey });
    vi.mocked(apiClient.post).mockResolvedValue({
      data: { success: true, message: 'Connection successful', responseTimeMs: 150 },
    });

    render(<ProviderConfigurationPanel />);

    await waitFor(() => {
      expect(screen.getByText('OpenAI')).toBeInTheDocument();
    });

    const testButton = screen.getByRole('button', { name: /test connection/i });
    fireEvent.click(testButton);

    await waitFor(() => {
      expect(apiClient.post).toHaveBeenCalledWith('/api/providers/test-connection', {
        providerName: 'OpenAI',
        apiKey: 'test-key-123',
      });
    });

    await waitFor(() => {
      expect(screen.getByText(/connection successful/i)).toBeInTheDocument();
    });
  });

  it('shows model selection dropdown for LLM and TTS providers', async () => {
    vi.mocked(apiClient.get).mockResolvedValue({ data: mockProviders });

    render(<ProviderConfigurationPanel />);

    await waitFor(() => {
      expect(screen.getByText('OpenAI')).toBeInTheDocument();
      expect(screen.getByText('ElevenLabs')).toBeInTheDocument();
    });

    const modelFields = screen.getAllByText('Model Selection');
    expect(modelFields).toHaveLength(2);
  });

  it('clears API key with confirmation', async () => {
    const providersWithKey = {
      providers: [
        {
          ...mockProviders.providers[0],
          apiKey: 'test-key-123',
        },
      ],
    };

    vi.mocked(apiClient.get).mockResolvedValue({ data: providersWithKey });

    render(<ProviderConfigurationPanel />);

    await waitFor(() => {
      expect(screen.getByText('OpenAI')).toBeInTheDocument();
    });

    const clearButtons = screen.getAllByRole('button', { name: /clear api key/i });
    expect(clearButtons.length).toBeGreaterThan(0);

    fireEvent.click(clearButtons[0]);

    await waitFor(() => {
      expect(screen.getByText(/are you sure you want to clear the api key/i)).toBeInTheDocument();
    });

    const confirmButton = screen.getByRole('button', { name: /^clear key$/i });
    fireEvent.click(confirmButton);

    await waitFor(() => {
      expect(
        screen.queryByText(/are you sure you want to clear the api key/i)
      ).not.toBeInTheDocument();
    });
  });
});
