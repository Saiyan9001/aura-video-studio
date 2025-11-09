/**
 * File System Utilities
 * Cross-platform utilities for opening files and folders
 */

import { apiUrl } from '../config/api';
import { loggingService as logger } from '../services/loggingService';

/**
 * Open a file in the system's default application
 * Uses backend API to handle platform-specific file opening
 */
export async function openFile(filePath: string): Promise<boolean> {
  try {
    const response = await fetch(apiUrl('/api/v1/files/open-file'), {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ path: filePath }),
    });

    if (!response.ok) {
      const errorText = await response.text();
      logger.error(
        'Failed to open file',
        new Error(errorText || 'Unknown error'),
        'fileSystemUtils',
        'openFile',
        { filePath, status: response.status }
      );
      return false;
    }

    logger.debug('File opened successfully', 'fileSystemUtils', 'openFile', { filePath });
    return true;
  } catch (error) {
    logger.error(
      'Error opening file',
      error instanceof Error ? error : new Error(String(error)),
      'fileSystemUtils',
      'openFile',
      { filePath }
    );
    return false;
  }
}

/**
 * Open a folder in the system's file explorer
 * Uses backend API to handle platform-specific folder opening
 */
export async function openFolder(filePath: string): Promise<boolean> {
  try {
    const response = await fetch(apiUrl('/api/v1/files/open-folder'), {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ path: filePath }),
    });

    if (!response.ok) {
      const errorText = await response.text();
      logger.error(
        'Failed to open folder',
        new Error(errorText || 'Unknown error'),
        'fileSystemUtils',
        'openFolder',
        { filePath, status: response.status }
      );
      return false;
    }

    logger.debug('Folder opened successfully', 'fileSystemUtils', 'openFolder', { filePath });
    return true;
  } catch (error) {
    logger.error(
      'Error opening folder',
      error instanceof Error ? error : new Error(String(error)),
      'fileSystemUtils',
      'openFolder',
      { filePath }
    );
    return false;
  }
}

/**
 * Get the directory path from a file path
 */
export function getDirectoryPath(filePath: string): string {
  const lastSlash = Math.max(filePath.lastIndexOf('/'), filePath.lastIndexOf('\\'));
  return lastSlash >= 0 ? filePath.substring(0, lastSlash) : filePath;
}

/**
 * Get the filename from a file path
 */
export function getFileName(filePath: string): string {
  const lastSlash = Math.max(filePath.lastIndexOf('/'), filePath.lastIndexOf('\\'));
  return lastSlash >= 0 ? filePath.substring(lastSlash + 1) : filePath;
}
