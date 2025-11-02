import { FluentProvider, webLightTheme } from '@fluentui/react-components';
import { render, screen, fireEvent, waitFor } from '@testing-library/react';
import { describe, it, expect, vi } from 'vitest';
import { PathSelector } from '../../components/common/PathSelector';

describe('PathSelector Component', () => {
  const mockOnChange = vi.fn();
  const mockOnValidate = vi.fn();

  it('should render with label and input', () => {
    render(
      <FluentProvider theme={webLightTheme}>
        <PathSelector
          label="Test Path"
          value=""
          onChange={mockOnChange}
          placeholder="Select path"
        />
      </FluentProvider>
    );

    expect(screen.getByText('Test Path')).toBeInTheDocument();
    expect(screen.getByPlaceholderText('Select path')).toBeInTheDocument();
  });

  it('should render browse button', () => {
    render(
      <FluentProvider theme={webLightTheme}>
        <PathSelector label="Test Path" value="" onChange={mockOnChange} />
      </FluentProvider>
    );

    expect(screen.getByText('Browse...')).toBeInTheDocument();
  });

  it('should render auto-detect button when autoDetect prop is provided', () => {
    const mockAutoDetect = vi.fn();

    render(
      <FluentProvider theme={webLightTheme}>
        <PathSelector
          label="Test Path"
          value=""
          onChange={mockOnChange}
          autoDetect={mockAutoDetect}
        />
      </FluentProvider>
    );

    expect(screen.getByText('Auto-Detect')).toBeInTheDocument();
  });

  it('should call onChange when input value changes', () => {
    render(
      <FluentProvider theme={webLightTheme}>
        <PathSelector label="Test Path" value="" onChange={mockOnChange} />
      </FluentProvider>
    );

    const input = screen.getByPlaceholderText('Click Browse to select file');
    fireEvent.change(input, { target: { value: 'C:\\test\\path.exe' } });

    expect(mockOnChange).toHaveBeenCalledWith('C:\\test\\path.exe');
  });

  it('should show validation result when path is valid', async () => {
    mockOnValidate.mockResolvedValue({
      isValid: true,
      message: 'Valid path',
      version: '1.0.0',
    });

    render(
      <FluentProvider theme={webLightTheme}>
        <PathSelector
          label="Test Path"
          value="C:\\test\\path.exe"
          onChange={mockOnChange}
          onValidate={mockOnValidate}
        />
      </FluentProvider>
    );

    await waitFor(
      () => {
        expect(screen.getByText('Valid path')).toBeInTheDocument();
      },
      { timeout: 1000 }
    );
  });

  it('should show error message when path is invalid', async () => {
    mockOnValidate.mockResolvedValue({
      isValid: false,
      message: 'File does not exist',
    });

    render(
      <FluentProvider theme={webLightTheme}>
        <PathSelector
          label="Test Path"
          value="C:\\invalid\\path.exe"
          onChange={mockOnChange}
          onValidate={mockOnValidate}
        />
      </FluentProvider>
    );

    await waitFor(
      () => {
        expect(screen.getByText('File does not exist')).toBeInTheDocument();
      },
      { timeout: 1000 }
    );
  });

  it('should display help text when provided', () => {
    const { container } = render(
      <FluentProvider theme={webLightTheme}>
        <PathSelector
          label="Test Path"
          value=""
          onChange={mockOnChange}
          helpText="This is helpful information"
        />
      </FluentProvider>
    );

    const svg = container.querySelector('svg[aria-describedby]');
    expect(svg).toBeInTheDocument();
  });

  it('should display default path when provided', () => {
    render(
      <FluentProvider theme={webLightTheme}>
        <PathSelector
          label="Test Path"
          value=""
          onChange={mockOnChange}
          defaultPath="C:\\default\\path.exe"
        />
      </FluentProvider>
    );

    expect(screen.getByText('Default:', { exact: false })).toBeInTheDocument();
  });

  it('should disable input and buttons when disabled prop is true', () => {
    render(
      <FluentProvider theme={webLightTheme}>
        <PathSelector label="Test Path" value="" onChange={mockOnChange} disabled={true} />
      </FluentProvider>
    );

    const input = screen.getByPlaceholderText('Click Browse to select file');
    const browseButton = screen.getByText('Browse...');

    expect(input).toBeDisabled();
    expect(browseButton).toBeDisabled();
  });

  it('should call autoDetect when Auto-Detect button is clicked', async () => {
    const mockAutoDetect = vi.fn().mockResolvedValue('C:\\detected\\path.exe');

    render(
      <FluentProvider theme={webLightTheme}>
        <PathSelector
          label="Test Path"
          value=""
          onChange={mockOnChange}
          autoDetect={mockAutoDetect}
        />
      </FluentProvider>
    );

    const autoDetectButton = screen.getByText('Auto-Detect');
    fireEvent.click(autoDetectButton);

    await waitFor(() => {
      expect(mockAutoDetect).toHaveBeenCalled();
      expect(mockOnChange).toHaveBeenCalledWith('C:\\detected\\path.exe');
    });
  });

  it('should render clear button when value is set and showClearButton is true', () => {
    render(
      <FluentProvider theme={webLightTheme}>
        <PathSelector
          label="Test Path"
          value="C:\\test\\path.exe"
          onChange={mockOnChange}
          showClearButton={true}
        />
      </FluentProvider>
    );

    const clearButton = screen.getByTitle('Clear path');
    expect(clearButton).toBeInTheDocument();
  });

  it('should not render clear button when value is empty', () => {
    render(
      <FluentProvider theme={webLightTheme}>
        <PathSelector label="Test Path" value="" onChange={mockOnChange} showClearButton={true} />
      </FluentProvider>
    );

    const clearButton = screen.queryByTitle('Clear path');
    expect(clearButton).not.toBeInTheDocument();
  });

  it('should call onChange with empty string when clear button is clicked', () => {
    render(
      <FluentProvider theme={webLightTheme}>
        <PathSelector
          label="Test Path"
          value="C:\\test\\path.exe"
          onChange={mockOnChange}
          showClearButton={true}
        />
      </FluentProvider>
    );

    const clearButton = screen.getByTitle('Clear path');
    fireEvent.click(clearButton);

    expect(mockOnChange).toHaveBeenCalledWith('');
  });

  it('should render open button when showOpenButton is true and path is valid', async () => {
    mockOnValidate.mockResolvedValue({
      isValid: true,
      message: 'Valid path',
    });

    render(
      <FluentProvider theme={webLightTheme}>
        <PathSelector
          label="Test Path"
          value="C:\\test\\path.exe"
          onChange={mockOnChange}
          onValidate={mockOnValidate}
          showOpenButton={true}
        />
      </FluentProvider>
    );

    await waitFor(
      () => {
        const openButton = screen.getByTitle('Open in file explorer');
        expect(openButton).toBeInTheDocument();
      },
      { timeout: 1000 }
    );
  });

  it('should use correct placeholder for directory type', () => {
    render(
      <FluentProvider theme={webLightTheme}>
        <PathSelector label="Test Path" value="" onChange={mockOnChange} type="directory" />
      </FluentProvider>
    );

    expect(screen.getByPlaceholderText('Click Browse to select folder')).toBeInTheDocument();
  });

  it('should use correct placeholder for file type', () => {
    render(
      <FluentProvider theme={webLightTheme}>
        <PathSelector label="Test Path" value="" onChange={mockOnChange} type="file" />
      </FluentProvider>
    );

    expect(screen.getByPlaceholderText('Click Browse to select file')).toBeInTheDocument();
  });

  it('should use custom placeholder when provided', () => {
    render(
      <FluentProvider theme={webLightTheme}>
        <PathSelector
          label="Test Path"
          value=""
          onChange={mockOnChange}
          placeholder="Custom placeholder"
        />
      </FluentProvider>
    );

    expect(screen.getByPlaceholderText('Custom placeholder')).toBeInTheDocument();
  });

  it('should accept custom accept attribute', () => {
    render(
      <FluentProvider theme={webLightTheme}>
        <PathSelector
          label="Test Path"
          value=""
          onChange={mockOnChange}
          type="file"
          accept=".wav,.mp3"
        />
      </FluentProvider>
    );

    expect(screen.getByText('Browse...')).toBeInTheDocument();
  });
});
