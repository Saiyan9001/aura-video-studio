import { render, screen } from '@testing-library/react';
import { describe, it, expect, vi } from 'vitest';
import { SafeFFmpegCard } from '../SafeFFmpegCard';

// Mock FFmpegCard to simulate errors
vi.mock('../FFmpegCard', () => ({
  FFmpegCard: () => {
    throw new Error('FFmpeg test error');
  },
}));

describe('SafeFFmpegCard', () => {
  it('should catch and display error when FFmpegCard throws', () => {
    render(<SafeFFmpegCard />);

    expect(screen.getByText(/FFmpeg configuration unavailable/i)).toBeInTheDocument();
    expect(screen.getByText(/backend may not be running/i)).toBeInTheDocument();
  });

  it('should show retry button when error occurs', () => {
    render(<SafeFFmpegCard />);

    const retryButton = screen.getByRole('button', { name: /retry/i });
    expect(retryButton).toBeInTheDocument();
  });
});
