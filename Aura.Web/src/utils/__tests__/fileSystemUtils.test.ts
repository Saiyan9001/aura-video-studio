/**
 * Tests for file system utilities
 */

import { describe, it, expect, vi, beforeEach, afterEach } from 'vitest';
import { openFile, openFolder, getDirectoryPath, getFileName } from '../fileSystemUtils';

// Mock fetch globally
const mockFetch = vi.fn();
global.fetch = mockFetch;

describe('fileSystemUtils', () => {
  beforeEach(() => {
    mockFetch.mockClear();
  });

  afterEach(() => {
    vi.restoreAllMocks();
  });

  describe('openFile', () => {
    it('should call API to open file and return true on success', async () => {
      mockFetch.mockResolvedValueOnce({
        ok: true,
      });

      const result = await openFile('/path/to/video.mp4');

      expect(mockFetch).toHaveBeenCalledWith(
        expect.stringContaining('/api/v1/files/open-file'),
        expect.objectContaining({
          method: 'POST',
          headers: { 'Content-Type': 'application/json' },
          body: JSON.stringify({ path: '/path/to/video.mp4' }),
        })
      );
      expect(result).toBe(true);
    });

    it('should return false on API error', async () => {
      mockFetch.mockResolvedValueOnce({
        ok: false,
        status: 500,
        text: async () => 'Server error',
      });

      const result = await openFile('/path/to/video.mp4');

      expect(result).toBe(false);
    });

    it('should return false on network error', async () => {
      mockFetch.mockRejectedValueOnce(new Error('Network error'));

      const result = await openFile('/path/to/video.mp4');

      expect(result).toBe(false);
    });
  });

  describe('openFolder', () => {
    it('should call API to open folder and return true on success', async () => {
      mockFetch.mockResolvedValueOnce({
        ok: true,
      });

      const result = await openFolder('/path/to/folder');

      expect(mockFetch).toHaveBeenCalledWith(
        expect.stringContaining('/api/v1/files/open-folder'),
        expect.objectContaining({
          method: 'POST',
          headers: { 'Content-Type': 'application/json' },
          body: JSON.stringify({ path: '/path/to/folder' }),
        })
      );
      expect(result).toBe(true);
    });

    it('should return false on API error', async () => {
      mockFetch.mockResolvedValueOnce({
        ok: false,
        status: 404,
        text: async () => 'Not found',
      });

      const result = await openFolder('/path/to/folder');

      expect(result).toBe(false);
    });

    it('should return false on network error', async () => {
      mockFetch.mockRejectedValueOnce(new Error('Network error'));

      const result = await openFolder('/path/to/folder');

      expect(result).toBe(false);
    });
  });

  describe('getDirectoryPath', () => {
    it('should extract directory from Unix path', () => {
      expect(getDirectoryPath('/home/user/videos/output.mp4')).toBe('/home/user/videos');
    });

    it('should extract directory from Windows path', () => {
      expect(getDirectoryPath('C:\\Users\\user\\videos\\output.mp4')).toBe(
        'C:\\Users\\user\\videos'
      );
    });

    it('should handle file in root directory (Unix)', () => {
      expect(getDirectoryPath('/file.txt')).toBe('');
    });

    it('should handle file in root directory (Windows)', () => {
      expect(getDirectoryPath('C:\\file.txt')).toBe('C:');
    });

    it('should handle path with no directory separator', () => {
      expect(getDirectoryPath('output.mp4')).toBe('output.mp4');
    });

    it('should handle empty path', () => {
      expect(getDirectoryPath('')).toBe('');
    });
  });

  describe('getFileName', () => {
    it('should extract filename from Unix path', () => {
      expect(getFileName('/home/user/videos/output.mp4')).toBe('output.mp4');
    });

    it('should extract filename from Windows path', () => {
      expect(getFileName('C:\\Users\\user\\videos\\output.mp4')).toBe('output.mp4');
    });

    it('should handle path with no directory separator', () => {
      expect(getFileName('output.mp4')).toBe('output.mp4');
    });

    it('should handle empty path', () => {
      expect(getFileName('')).toBe('');
    });
  });
});
