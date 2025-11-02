import { render, screen } from '@testing-library/react';
import { BrowserRouter } from 'react-router-dom';
import { describe, it, expect, vi } from 'vitest';
import { DependenciesEmptyState } from '../DependenciesEmptyState';

describe('DependenciesEmptyState', () => {
  it('should render backend down state', () => {
    render(
      <BrowserRouter>
        <DependenciesEmptyState isBackendDown={true} />
      </BrowserRouter>
    );

    expect(screen.getByText('Backend Not Available')).toBeInTheDocument();
    expect(screen.getByText(/backend server is not running/i)).toBeInTheDocument();
  });

  it('should render not configured state', () => {
    render(
      <BrowserRouter>
        <DependenciesEmptyState isBackendDown={false} />
      </BrowserRouter>
    );

    expect(screen.getByText('Dependencies Not Configured Yet')).toBeInTheDocument();
    expect(screen.getByText(/Complete the onboarding process/i)).toBeInTheDocument();
  });

  it('should call onRetry when retry button is clicked', () => {
    const onRetry = vi.fn();

    render(
      <BrowserRouter>
        <DependenciesEmptyState isBackendDown={true} onRetry={onRetry} />
      </BrowserRouter>
    );

    const retryButton = screen.getByRole('button', { name: /retry connection/i });
    retryButton.click();

    expect(onRetry).toHaveBeenCalledTimes(1);
  });

  it('should show onboarding button when not configured', () => {
    render(
      <BrowserRouter>
        <DependenciesEmptyState isBackendDown={false} />
      </BrowserRouter>
    );

    const onboardingButton = screen.getByRole('button', { name: /start onboarding/i });
    expect(onboardingButton).toBeInTheDocument();
  });
});
