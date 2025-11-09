/**
 * Tests for Ollama Provider Configuration Component
 */

import { render, screen, waitFor } from '@testing-library/react';
import { describe, it, expect, vi, beforeEach } from 'vitest';
import { OllamaProviderConfig } from '../OllamaProviderConfig';

global.fetch = vi.fn();

describe('OllamaProviderConfig', () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  it('renders ollama configuration with status check', async () => {
    vi.mocked(global.fetch).mockResolvedValueOnce({
      ok: true,
      json: async () => ({
        isAvailable: false,
        modelsCount: 0,
        message: 'Ollama service not running',
      }),
    } as Response);

    const mockOnModelChange = vi.fn();

    render(<OllamaProviderConfig selectedModel="" onModelChange={mockOnModelChange} />);

    await waitFor(() => {
      expect(screen.getByText('Ollama Service Status')).toBeInTheDocument();
    });
  });

  it('displays connected status when ollama is available with models', async () => {
    vi.mocked(global.fetch)
      .mockResolvedValueOnce({
        ok: true,
        json: async () => ({
          isAvailable: true,
          modelsCount: 2,
          version: '0.1.0',
          message: 'Ollama running with 2 models',
        }),
      } as Response)
      .mockResolvedValueOnce({
        ok: true,
        json: async () => ({
          success: true,
          models: [
            {
              name: 'llama3.1:8b',
              size: 4661211648,
              sizeFormatted: '4.34 GB',
              modified: '2024-01-15T10:30:00',
              modifiedFormatted: '2024-01-15 10:30:00',
            },
          ],
        }),
      } as Response);

    const mockOnModelChange = vi.fn();

    render(<OllamaProviderConfig selectedModel="" onModelChange={mockOnModelChange} />);

    await waitFor(() => {
      expect(screen.getByText('Connected')).toBeInTheDocument();
    });

    await waitFor(() => {
      expect(screen.getByText('2 models available')).toBeInTheDocument();
    });
  });

  it('displays not running status when ollama is unavailable', async () => {
    vi.mocked(global.fetch).mockResolvedValueOnce({
      ok: true,
      json: async () => ({
        isAvailable: false,
        modelsCount: 0,
        message: 'Ollama service not running',
      }),
    } as Response);

    const mockOnModelChange = vi.fn();

    render(<OllamaProviderConfig selectedModel="" onModelChange={mockOnModelChange} />);

    await waitFor(() => {
      expect(screen.getByText('Not Running')).toBeInTheDocument();
    });

    await waitFor(() => {
      expect(screen.getByText('Ollama Not Running')).toBeInTheDocument();
    });
  });

  it('displays no models warning when ollama is running but no models installed', async () => {
    vi.mocked(global.fetch)
      .mockResolvedValueOnce({
        ok: true,
        json: async () => ({
          isAvailable: true,
          modelsCount: 0,
          version: '0.1.0',
          message: 'Ollama running with 0 models',
        }),
      } as Response)
      .mockResolvedValueOnce({
        ok: true,
        json: async () => ({
          success: true,
          models: [],
        }),
      } as Response);

    const mockOnModelChange = vi.fn();

    render(<OllamaProviderConfig selectedModel="" onModelChange={mockOnModelChange} />);

    await waitFor(() => {
      expect(screen.getByText('No Models')).toBeInTheDocument();
    });

    await waitFor(() => {
      expect(screen.getByText('No Models Installed')).toBeInTheDocument();
    });
  });
});
