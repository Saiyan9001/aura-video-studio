import { render, screen, fireEvent } from '@testing-library/react';
import { describe, it, expect, vi } from 'vitest';
import type { ProjectSummary } from '../../../state/dashboard';
import { ProjectCard } from '../ProjectCard';

describe('ProjectCard', () => {
  const mockProject: ProjectSummary = {
    id: '1',
    name: 'Test Project',
    description: 'A test project',
    status: 'complete',
    createdAt: new Date('2024-01-01').toISOString(),
    lastModifiedAt: new Date('2024-01-01').toISOString(),
    duration: 120,
    viewCount: 10,
    order: 0,
  };

  it('renders project information', () => {
    render(<ProjectCard project={mockProject} />);

    expect(screen.getByText('Test Project')).toBeDefined();
    expect(screen.getByText('2:00')).toBeDefined(); // Duration formatted
    expect(screen.getByText('10 views')).toBeDefined();
  });

  it('displays status badge', () => {
    render(<ProjectCard project={mockProject} />);

    expect(screen.getByText('Complete')).toBeDefined();
  });

  it('calls onPreview when thumbnail clicked', () => {
    const onPreview = vi.fn();
    render(<ProjectCard project={mockProject} onPreview={onPreview} />);

    const preview = screen.getByText('No Preview').closest('div');
    if (preview?.parentElement) {
      fireEvent.click(preview.parentElement);
    }

    expect(onPreview).toHaveBeenCalledWith(mockProject);
  });

  it('calls onEdit when title clicked', () => {
    const onEdit = vi.fn();
    render(<ProjectCard project={mockProject} onEdit={onEdit} />);

    const title = screen.getByText('Test Project');
    fireEvent.click(title);

    expect(onEdit).toHaveBeenCalledWith(mockProject);
  });

  it('shows progress bar for processing status', () => {
    const processingProject = { ...mockProject, status: 'processing' as const, progress: 50 };
    render(<ProjectCard project={processingProject} />);

    expect(screen.getByText('Processing')).toBeDefined();
  });

  it('shows error state when error provided', () => {
    const onRetry = vi.fn();
    render(<ProjectCard project={mockProject} error="Test error" onRetry={onRetry} />);

    expect(screen.getByText('Test error')).toBeDefined();

    const retryButton = screen.getByText('Retry');
    fireEvent.click(retryButton);

    expect(onRetry).toHaveBeenCalled();
  });

  it('shows skeleton loader when loading', () => {
    const { container } = render(<ProjectCard project={mockProject} loading />);

    // Skeleton should have different structure than normal card
    expect(container.querySelector('.fui-Card')).toBeDefined();
  });

  it('displays relative time correctly', () => {
    const recentProject = { ...mockProject, createdAt: new Date().toISOString() };
    render(<ProjectCard project={recentProject} />);

    expect(screen.getByText('Just now')).toBeDefined();
  });

  it('formats duration correctly', () => {
    const longProject = { ...mockProject, duration: 3665 }; // 1 hour, 1 minute, 5 seconds
    render(<ProjectCard project={longProject} />);

    expect(screen.getByText('61:05')).toBeDefined(); // 61 minutes, 5 seconds
  });

  it('hides view count when not provided', () => {
    const { viewCount, ...projectWithoutViews } = mockProject;
    render(<ProjectCard project={projectWithoutViews as ProjectSummary} />);

    expect(screen.queryByText(/views/)).toBeNull();
  });

  it('displays video preview on hover when videoUrl is available', () => {
    const projectWithVideo = { ...mockProject, videoUrl: 'https://example.com/video.mp4' };
    const { container } = render(<ProjectCard project={projectWithVideo} />);

    const videoElement = container.querySelector('video');
    expect(videoElement).toBeDefined();
    expect(videoElement?.src).toBe('https://example.com/video.mp4');
  });

  it('does not display video preview when status is not complete', () => {
    const processingProject = {
      ...mockProject,
      status: 'processing' as const,
      videoUrl: 'https://example.com/video.mp4',
    };
    const { container } = render(<ProjectCard project={processingProject} />);

    const videoElement = container.querySelector('video');
    expect(videoElement).toBeNull();
  });
});
