import { render, screen, fireEvent, waitFor } from '@testing-library/react';
import { BrowserRouter } from 'react-router-dom';
import { describe, it, expect, vi } from 'vitest';
import { VideoCreationWizard } from '../VideoCreationWizard';

describe('VideoCreationWizard', () => {
  const renderWizard = () => {
    return render(
      <BrowserRouter>
        <VideoCreationWizard />
      </BrowserRouter>
    );
  };

  it('renders the wizard with initial step', () => {
    renderWizard();

    expect(screen.getByText('Create Video')).toBeInTheDocument();
    expect(screen.getByText('Step 1 of 5')).toBeInTheDocument();
    expect(screen.getByText(/What.+s your video about/i)).toBeInTheDocument();
  });

  it('displays the brief input form on step 1', () => {
    renderWizard();

    expect(screen.getByLabelText(/Video Topic/i)).toBeInTheDocument();
    expect(screen.getByLabelText(/Video Type/i)).toBeInTheDocument();
    expect(screen.getByText(/Inspire Me/i)).toBeInTheDocument();
  });

  it('shows validation errors when advancing with empty form', () => {
    renderWizard();

    const nextButton = screen.getByRole('button', { name: /Next/i });
    expect(nextButton).toBeDisabled();
  });

  it('enables next button when form is valid', async () => {
    renderWizard();

    const topicInput = screen.getByLabelText(/Video Topic/i);
    const audienceInput = screen.getByLabelText(/Target Audience/i);
    const keyMessageInput = screen.getByLabelText(/Key Message/i);

    fireEvent.change(topicInput, {
      target: { value: 'A comprehensive guide to learning TypeScript' },
    });
    fireEvent.change(audienceInput, { target: { value: 'JavaScript developers' } });
    fireEvent.change(keyMessageInput, {
      target: { value: 'TypeScript makes code more maintainable' },
    });

    await waitFor(() => {
      const nextButton = screen.getByRole('button', { name: /Next/i });
      expect(nextButton).not.toBeDisabled();
    });
  });

  it('advances to next step when valid', async () => {
    renderWizard();

    const topicInput = screen.getByLabelText(/Video Topic/i);
    fireEvent.change(topicInput, {
      target: { value: 'A comprehensive guide to learning TypeScript' },
    });

    const audienceInput = screen.getByLabelText(/Target Audience/i);
    fireEvent.change(audienceInput, { target: { value: 'JavaScript developers' } });

    const keyMessageInput = screen.getByLabelText(/Key Message/i);
    fireEvent.change(keyMessageInput, {
      target: { value: 'TypeScript makes code more maintainable' },
    });

    await waitFor(() => {
      const nextButton = screen.getByRole('button', { name: /Next/i });
      expect(nextButton).not.toBeDisabled();
    });

    const nextButton = screen.getByRole('button', { name: /Next/i });
    fireEvent.click(nextButton);

    await waitFor(() => {
      expect(screen.getByText('Step 2 of 5')).toBeInTheDocument();
      expect(screen.getByText(/Style Selection/i)).toBeInTheDocument();
    });
  });

  it('shows cost estimator in header', () => {
    renderWizard();

    expect(screen.getByText(/\$/)).toBeInTheDocument();
  });

  it('shows advanced mode toggle', () => {
    renderWizard();

    expect(screen.getByRole('switch', { name: /Advanced Mode/i })).toBeInTheDocument();
  });

  it('shows templates button', () => {
    renderWizard();

    expect(screen.getByRole('button', { name: /Templates/i })).toBeInTheDocument();
  });

  it('shows save and exit button', () => {
    renderWizard();

    expect(screen.getByRole('button', { name: /Save & Exit/i })).toBeInTheDocument();
  });

  it('opens template dialog when templates button clicked', async () => {
    renderWizard();

    const templatesButton = screen.getByRole('button', { name: /Templates/i });
    fireEvent.click(templatesButton);

    await waitFor(() => {
      expect(screen.getByText(/Video Templates/i)).toBeInTheDocument();
      expect(screen.getByText(/Educational/i)).toBeInTheDocument();
      expect(screen.getByText(/Marketing/i)).toBeInTheDocument();
    });
  });

  it('persists wizard data to localStorage', async () => {
    const setItemSpy = vi.spyOn(Storage.prototype, 'setItem');

    renderWizard();

    const topicInput = screen.getByLabelText(/Video Topic/i);
    fireEvent.change(topicInput, { target: { value: 'Test topic' } });

    await waitFor(() => {
      expect(setItemSpy).toHaveBeenCalledWith(
        'aura-wizard-data',
        expect.stringContaining('Test topic')
      );
    });

    setItemSpy.mockRestore();
  });
});
