import { render, screen, fireEvent, waitFor } from '@testing-library/react';
import { describe, it, expect, vi, beforeEach } from 'vitest';
import { ttsService } from '../../../../services/ttsService';
import type { ScriptData, BriefData, StyleData, ScriptScene } from '../../types';
import { ScriptReview } from '../ScriptReview';

vi.mock('../../../../services/ttsService', () => ({
  ttsService: {
    regenerateAudio: vi.fn(),
  },
}));

describe('ScriptReview', () => {
  const mockBriefData: BriefData = {
    topic: 'AI Video Generation',
    videoType: 'educational',
    targetAudience: 'Content creators',
    keyMessage: 'Create videos easily with AI',
    duration: 60,
  };

  const mockStyleData: StyleData = {
    voiceProvider: 'ElevenLabs',
    voiceName: 'Adam',
    visualStyle: 'modern',
    musicGenre: 'ambient',
    musicEnabled: true,
  };

  const mockScenes: ScriptScene[] = [
    {
      id: 'scene-1',
      text: 'Welcome to our tutorial on AI video generation.',
      duration: 5,
      visualDescription: 'Modern office with AI graphics',
      timestamp: 0,
    },
    {
      id: 'scene-2',
      text: 'Creating professional videos has never been easier.',
      duration: 5,
      visualDescription: 'Dashboard showing video editing',
      timestamp: 5,
    },
  ];

  const mockScriptData: ScriptData = {
    content: 'Full script content',
    scenes: mockScenes,
    generatedAt: new Date(),
  };

  const mockOnChange = vi.fn();
  const mockOnValidationChange = vi.fn();

  beforeEach(() => {
    vi.clearAllMocks();
  });

  it('renders script review header', () => {
    render(
      <ScriptReview
        data={mockScriptData}
        briefData={mockBriefData}
        styleData={mockStyleData}
        advancedMode={false}
        onChange={mockOnChange}
        onValidationChange={mockOnValidationChange}
      />
    );

    expect(screen.getByText('Script Review')).toBeInTheDocument();
    expect(screen.getByText(/Review and edit the AI-generated script/i)).toBeInTheDocument();
  });

  it('displays all script scenes', () => {
    render(
      <ScriptReview
        data={mockScriptData}
        briefData={mockBriefData}
        styleData={mockStyleData}
        advancedMode={false}
        onChange={mockOnChange}
        onValidationChange={mockOnValidationChange}
      />
    );

    expect(screen.getByText('Scene 1')).toBeInTheDocument();
    expect(screen.getByText('Scene 2')).toBeInTheDocument();
    expect(screen.getByDisplayValue(mockScenes[0].text)).toBeInTheDocument();
    expect(screen.getByDisplayValue(mockScenes[1].text)).toBeInTheDocument();
  });

  it('shows scene timing information', () => {
    render(
      <ScriptReview
        data={mockScriptData}
        briefData={mockBriefData}
        styleData={mockStyleData}
        advancedMode={false}
        onChange={mockOnChange}
        onValidationChange={mockOnValidationChange}
      />
    );

    expect(screen.getByText(/0:00 - 0:05/)).toBeInTheDocument();
    expect(screen.getByText(/0:05 - 0:10/)).toBeInTheDocument();
  });

  it('allows editing scene text', async () => {
    render(
      <ScriptReview
        data={mockScriptData}
        briefData={mockBriefData}
        styleData={mockStyleData}
        advancedMode={false}
        onChange={mockOnChange}
        onValidationChange={mockOnValidationChange}
      />
    );

    const textarea = screen.getAllByRole('textbox')[0];
    fireEvent.change(textarea, { target: { value: 'Updated scene text' } });

    await waitFor(() => {
      expect(mockOnChange).toHaveBeenCalledWith({
        ...mockScriptData,
        scenes: [{ ...mockScenes[0], text: 'Updated scene text' }, mockScenes[1]],
      });
    });
  });

  it('shows regenerate audio button for each scene', () => {
    render(
      <ScriptReview
        data={mockScriptData}
        briefData={mockBriefData}
        styleData={mockStyleData}
        advancedMode={false}
        onChange={mockOnChange}
        onValidationChange={mockOnValidationChange}
      />
    );

    const regenerateButtons = screen.getAllByText('Regenerate Audio');
    expect(regenerateButtons).toHaveLength(2);
  });

  it('calls ttsService when regenerate button is clicked', async () => {
    const mockRegenerateResponse = {
      success: true,
      sceneIndex: 0,
      audioPath: '/path/to/audio.wav',
      duration: 5,
      correlationId: 'test-123',
    };

    vi.mocked(ttsService.regenerateAudio).mockResolvedValue(mockRegenerateResponse);

    render(
      <ScriptReview
        data={mockScriptData}
        briefData={mockBriefData}
        styleData={mockStyleData}
        advancedMode={false}
        onChange={mockOnChange}
        onValidationChange={mockOnValidationChange}
      />
    );

    const regenerateButtons = screen.getAllByText('Regenerate Audio');
    fireEvent.click(regenerateButtons[0]);

    await waitFor(() => {
      expect(ttsService.regenerateAudio).toHaveBeenCalledWith({
        sceneIndex: 0,
        text: mockScenes[0].text,
        startSeconds: mockScenes[0].timestamp,
        durationSeconds: mockScenes[0].duration,
        provider: mockStyleData.voiceProvider,
        voiceName: mockStyleData.voiceName,
      });
    });
  });

  it('shows success message after successful audio regeneration', async () => {
    const mockRegenerateResponse = {
      success: true,
      sceneIndex: 0,
      audioPath: '/path/to/audio.wav',
      duration: 5,
      correlationId: 'test-123',
    };

    vi.mocked(ttsService.regenerateAudio).mockResolvedValue(mockRegenerateResponse);

    render(
      <ScriptReview
        data={mockScriptData}
        briefData={mockBriefData}
        styleData={mockStyleData}
        advancedMode={false}
        onChange={mockOnChange}
        onValidationChange={mockOnValidationChange}
      />
    );

    const regenerateButtons = screen.getAllByText('Regenerate Audio');
    fireEvent.click(regenerateButtons[0]);

    await waitFor(() => {
      expect(screen.getByText('Audio generated successfully')).toBeInTheDocument();
    });
  });

  it('shows error message when audio regeneration fails', async () => {
    const errorMessage = 'Failed to generate audio';
    vi.mocked(ttsService.regenerateAudio).mockRejectedValue(new Error(errorMessage));

    render(
      <ScriptReview
        data={mockScriptData}
        briefData={mockBriefData}
        styleData={mockStyleData}
        advancedMode={false}
        onChange={mockOnChange}
        onValidationChange={mockOnValidationChange}
      />
    );

    const regenerateButtons = screen.getAllByText('Regenerate Audio');
    fireEvent.click(regenerateButtons[0]);

    await waitFor(() => {
      expect(screen.getByText(/Failed:/)).toBeInTheDocument();
    });
  });

  it('validates script data correctly', () => {
    render(
      <ScriptReview
        data={mockScriptData}
        briefData={mockBriefData}
        styleData={mockStyleData}
        advancedMode={false}
        onChange={mockOnChange}
        onValidationChange={mockOnValidationChange}
      />
    );

    expect(mockOnValidationChange).toHaveBeenCalledWith({
      isValid: true,
      errors: [],
    });
  });

  it('shows validation error when no scenes available', () => {
    const emptyScriptData: ScriptData = {
      content: '',
      scenes: [],
      generatedAt: null,
    };

    render(
      <ScriptReview
        data={emptyScriptData}
        briefData={mockBriefData}
        styleData={mockStyleData}
        advancedMode={false}
        onChange={mockOnChange}
        onValidationChange={mockOnValidationChange}
      />
    );

    expect(mockOnValidationChange).toHaveBeenCalledWith({
      isValid: false,
      errors: ['No script scenes available'],
    });
  });

  it('shows validation error when scene has empty text', () => {
    const scriptWithEmptyScene: ScriptData = {
      ...mockScriptData,
      scenes: [{ ...mockScenes[0], text: '' }, mockScenes[1]],
    };

    render(
      <ScriptReview
        data={scriptWithEmptyScene}
        briefData={mockBriefData}
        styleData={mockStyleData}
        advancedMode={false}
        onChange={mockOnChange}
        onValidationChange={mockOnValidationChange}
      />
    );

    expect(mockOnValidationChange).toHaveBeenCalledWith({
      isValid: false,
      errors: ['All scenes must have text'],
    });
  });

  it('displays visual descriptions when available', () => {
    render(
      <ScriptReview
        data={mockScriptData}
        briefData={mockBriefData}
        styleData={mockStyleData}
        advancedMode={false}
        onChange={mockOnChange}
        onValidationChange={mockOnValidationChange}
      />
    );

    expect(screen.getByText(mockScenes[0].visualDescription)).toBeInTheDocument();
    expect(screen.getByText(mockScenes[1].visualDescription)).toBeInTheDocument();
  });

  it('disables regenerate button when scene text is empty', async () => {
    const scriptWithEmptyScene: ScriptData = {
      ...mockScriptData,
      scenes: [{ ...mockScenes[0], text: '' }],
    };

    render(
      <ScriptReview
        data={scriptWithEmptyScene}
        briefData={mockBriefData}
        styleData={mockStyleData}
        advancedMode={false}
        onChange={mockOnChange}
        onValidationChange={mockOnValidationChange}
      />
    );

    const regenerateButton = screen.getByText('Regenerate Audio');
    expect(regenerateButton).toBeDisabled();
  });
});
