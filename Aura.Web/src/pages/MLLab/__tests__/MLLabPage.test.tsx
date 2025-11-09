import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { MemoryRouter } from 'react-router-dom';
import { describe, it, expect, beforeEach, vi } from 'vitest';
import { useMLLabStore } from '../../../state/mlLab';
import { MLLabPage } from '../MLLabPage';

// Mock the store
vi.mock('../../../state/mlLab', () => ({
  useMLLabStore: vi.fn(),
}));

// Mock child components
vi.mock('../AnnotationTab', () => ({
  AnnotationTab: () => <div data-testid="annotation-tab">Annotation Tab</div>,
}));

vi.mock('../TrainingTab', () => ({
  TrainingTab: () => <div data-testid="training-tab">Training Tab</div>,
}));

describe('MLLabPage', () => {
  const mockCheckSystemCapabilities = vi.fn();
  const mockSetCurrentTab = vi.fn();

  beforeEach(() => {
    vi.clearAllMocks();
    (useMLLabStore as unknown as ReturnType<typeof vi.fn>).mockReturnValue({
      currentTab: 'annotation',
      setCurrentTab: mockSetCurrentTab,
      checkSystemCapabilities: mockCheckSystemCapabilities,
      systemCapabilities: undefined,
    });
  });

  it('should render ML Lab page with title', () => {
    render(
      <MemoryRouter>
        <MLLabPage />
      </MemoryRouter>
    );

    expect(screen.getByText('ML Lab (Advanced)')).toBeInTheDocument();
    expect(
      screen.getByText(/Annotate video frames and retrain the frame importance model/)
    ).toBeInTheDocument();
  });

  it('should check system capabilities on mount', async () => {
    render(
      <MemoryRouter>
        <MLLabPage />
      </MemoryRouter>
    );

    await waitFor(() => {
      expect(mockCheckSystemCapabilities).toHaveBeenCalledTimes(1);
    });
  });

  it('should render both tabs', () => {
    render(
      <MemoryRouter>
        <MLLabPage />
      </MemoryRouter>
    );

    expect(screen.getByRole('tab', { name: 'Annotate Frames' })).toBeInTheDocument();
    expect(screen.getByRole('tab', { name: 'Train Model' })).toBeInTheDocument();
  });

  it('should display annotation tab by default', () => {
    render(
      <MemoryRouter>
        <MLLabPage />
      </MemoryRouter>
    );

    expect(screen.getByTestId('annotation-tab')).toBeInTheDocument();
    expect(screen.queryByTestId('training-tab')).not.toBeInTheDocument();
  });

  it('should switch to training tab when clicked', async () => {
    const user = userEvent.setup();

    // First render with annotation tab
    const { rerender } = render(
      <MemoryRouter>
        <MLLabPage />
      </MemoryRouter>
    );

    // Click training tab - use getByRole to target the button specifically
    const trainingTab = screen.getByRole('tab', { name: 'Train Model' });
    await user.click(trainingTab);

    // Update mock to reflect state change
    (useMLLabStore as unknown as ReturnType<typeof vi.fn>).mockReturnValue({
      currentTab: 'training',
      setCurrentTab: mockSetCurrentTab,
      checkSystemCapabilities: mockCheckSystemCapabilities,
      systemCapabilities: undefined,
    });

    // Re-render with new state
    rerender(
      <MemoryRouter>
        <MLLabPage />
      </MemoryRouter>
    );

    // Verify setCurrentTab was called
    await waitFor(() => {
      expect(mockSetCurrentTab).toHaveBeenCalledWith('training');
    });
  });

  it('should display warning banner when system has warnings', () => {
    (useMLLabStore as unknown as ReturnType<typeof vi.fn>).mockReturnValue({
      currentTab: 'annotation',
      setCurrentTab: mockSetCurrentTab,
      checkSystemCapabilities: mockCheckSystemCapabilities,
      systemCapabilities: {
        hasGPU: false,
        totalRAM: 8,
        availableRAM: 4,
        availableDiskSpace: 10,
        meetsMinimumRequirements: true,
        warnings: ['No GPU detected - training will use CPU (slower)'],
      },
    });

    render(
      <MemoryRouter>
        <MLLabPage />
      </MemoryRouter>
    );

    expect(screen.getByText('System Warnings')).toBeInTheDocument();
    expect(
      screen.getByText(/No GPU detected - training will use CPU \(slower\)/)
    ).toBeInTheDocument();
  });

  it('should display multiple warnings', () => {
    (useMLLabStore as unknown as ReturnType<typeof vi.fn>).mockReturnValue({
      currentTab: 'annotation',
      setCurrentTab: mockSetCurrentTab,
      checkSystemCapabilities: mockCheckSystemCapabilities,
      systemCapabilities: {
        hasGPU: false,
        totalRAM: 6,
        availableRAM: 2,
        availableDiskSpace: 3,
        meetsMinimumRequirements: false,
        warnings: [
          'Less than 8GB RAM - training may be slow or fail',
          'Less than 5GB disk space available - may not be sufficient',
          'No GPU detected - training will use CPU (slower)',
        ],
      },
    });

    render(
      <MemoryRouter>
        <MLLabPage />
      </MemoryRouter>
    );

    expect(screen.getByText('System Warnings')).toBeInTheDocument();
    expect(screen.getByText(/Less than 8GB RAM/)).toBeInTheDocument();
    expect(screen.getByText(/Less than 5GB disk space/)).toBeInTheDocument();
    expect(screen.getByText(/No GPU detected/)).toBeInTheDocument();
  });

  it('should not display warning banner when no warnings', () => {
    (useMLLabStore as unknown as ReturnType<typeof vi.fn>).mockReturnValue({
      currentTab: 'annotation',
      setCurrentTab: mockSetCurrentTab,
      checkSystemCapabilities: mockCheckSystemCapabilities,
      systemCapabilities: {
        hasGPU: true,
        gpuName: 'NVIDIA RTX 3080',
        totalRAM: 32,
        availableRAM: 16,
        availableDiskSpace: 100,
        meetsMinimumRequirements: true,
        warnings: [],
      },
    });

    render(
      <MemoryRouter>
        <MLLabPage />
      </MemoryRouter>
    );

    expect(screen.queryByText('System Warnings')).not.toBeInTheDocument();
  });

  it('should display info banner about training requirements', () => {
    render(
      <MemoryRouter>
        <MLLabPage />
      </MemoryRouter>
    );

    expect(screen.getByText('Important: Training Requirements')).toBeInTheDocument();
    expect(
      screen.getByText(/Training a custom model requires significant computational resources/)
    ).toBeInTheDocument();
  });
});
