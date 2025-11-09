import { render, screen, fireEvent } from '@testing-library/react';
import { describe, it, expect, vi } from 'vitest';
import { SettingsSearch } from '../SettingsSearch';
import type { SearchableItem } from '../SettingsSearch';

describe('SettingsSearch', () => {
  const mockItems: SearchableItem[] = [
    {
      id: 'resolution',
      title: 'Resolution',
      description: 'Default video resolution',
      category: 'Video',
      keywords: ['quality', '1080p', '4k'],
    },
    {
      id: 'theme',
      title: 'Theme',
      description: 'Color scheme preference',
      category: 'UI',
      keywords: ['dark', 'light'],
    },
  ];

  const mockOnSearch = vi.fn();
  const mockOnClear = vi.fn();

  it('renders search input', () => {
    render(<SettingsSearch items={mockItems} onSearch={mockOnSearch} onClear={mockOnClear} />);

    const searchInput = screen.getByPlaceholderText('Search settings...');
    expect(searchInput).toBeInTheDocument();
  });

  it('filters items based on search query', () => {
    render(<SettingsSearch items={mockItems} onSearch={mockOnSearch} onClear={mockOnClear} />);

    const searchInput = screen.getByPlaceholderText('Search settings...');
    fireEvent.change(searchInput, { target: { value: 'resolution' } });

    expect(mockOnSearch).toHaveBeenCalledWith('resolution', [mockItems[0]]);
  });

  it('calls onClear when clear button is clicked', () => {
    render(<SettingsSearch items={mockItems} onSearch={mockOnSearch} onClear={mockOnClear} />);

    const searchInput = screen.getByPlaceholderText('Search settings...');
    fireEvent.change(searchInput, { target: { value: 'test' } });

    const buttons = screen.getAllByRole('button');
    const clearButton = buttons[0]; // The clear button in the input
    fireEvent.click(clearButton);

    expect(mockOnClear).toHaveBeenCalled();
  });

  it('searches across title, description, and keywords', () => {
    render(<SettingsSearch items={mockItems} onSearch={mockOnSearch} onClear={mockOnClear} />);

    const searchInput = screen.getByPlaceholderText('Search settings...');

    fireEvent.change(searchInput, { target: { value: 'dark' } });
    expect(mockOnSearch).toHaveBeenCalledWith('dark', [mockItems[1]]);

    fireEvent.change(searchInput, { target: { value: 'quality' } });
    expect(mockOnSearch).toHaveBeenCalledWith('quality', [mockItems[0]]);
  });

  it('shows no results when query does not match', () => {
    render(<SettingsSearch items={mockItems} onSearch={mockOnSearch} onClear={mockOnClear} />);

    const searchInput = screen.getByPlaceholderText('Search settings...');
    fireEvent.change(searchInput, { target: { value: 'nonexistent' } });

    expect(mockOnSearch).toHaveBeenCalledWith('nonexistent', []);
  });
});
