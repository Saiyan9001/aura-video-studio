/**
 * API client for wizard project management
 */

import apiClient from '../services/api/apiClient';
import type {
  WizardProjectListItem,
  WizardProjectDetails,
  SaveWizardProjectRequest,
  SaveWizardProjectResponse,
  DuplicateProjectRequest,
  ProjectImportRequest,
  ClearGeneratedContentRequest,
  ProjectExport,
} from '../types/wizardProject';

const BASE_PATH = '/api/wizard-projects';

/**
 * Save or update a wizard project
 */
export async function saveWizardProject(
  request: SaveWizardProjectRequest
): Promise<SaveWizardProjectResponse> {
  const response = await apiClient.post<SaveWizardProjectResponse>(BASE_PATH, request);
  return response.data;
}

/**
 * Get a specific wizard project by ID
 */
export async function getWizardProject(id: string): Promise<WizardProjectDetails> {
  const response = await apiClient.get<WizardProjectDetails>(`${BASE_PATH}/${id}`);
  return response.data;
}

/**
 * Get all wizard projects
 */
export async function getAllWizardProjects(): Promise<WizardProjectListItem[]> {
  const response = await apiClient.get<WizardProjectListItem[]>(BASE_PATH);
  return response.data;
}

/**
 * Get recent wizard projects
 */
export async function getRecentWizardProjects(
  count: number = 10
): Promise<WizardProjectListItem[]> {
  const response = await apiClient.get<WizardProjectListItem[]>(`${BASE_PATH}/recent`, {
    params: { count },
  });
  return response.data;
}

/**
 * Duplicate a wizard project
 */
export async function duplicateWizardProject(
  id: string,
  request: DuplicateProjectRequest
): Promise<SaveWizardProjectResponse> {
  const response = await apiClient.post<SaveWizardProjectResponse>(
    `${BASE_PATH}/${id}/duplicate`,
    request
  );
  return response.data;
}

/**
 * Delete a wizard project
 */
export async function deleteWizardProject(id: string): Promise<void> {
  await apiClient.delete(`${BASE_PATH}/${id}`);
}

/**
 * Export a wizard project as JSON
 */
export async function exportWizardProject(id: string): Promise<string> {
  const response = await apiClient.get<string>(`${BASE_PATH}/${id}/export`, {
    responseType: 'text' as 'json',
  });
  return response.data;
}

/**
 * Import a wizard project from JSON
 */
export async function importWizardProject(
  request: ProjectImportRequest
): Promise<SaveWizardProjectResponse> {
  const response = await apiClient.post<SaveWizardProjectResponse>(`${BASE_PATH}/import`, request);
  return response.data;
}

/**
 * Clear generated content from a project
 */
export async function clearGeneratedContent(
  id: string,
  request: ClearGeneratedContentRequest
): Promise<{ message: string; projectId: string }> {
  const response = await apiClient.post<{ message: string; projectId: string }>(
    `${BASE_PATH}/${id}/clear-content`,
    request
  );
  return response.data;
}

/**
 * Download project export as a file
 */
export function downloadProjectExport(projectJson: string, projectName: string): void {
  const blob = new Blob([projectJson], { type: 'application/json' });
  const url = URL.createObjectURL(blob);
  const link = document.createElement('a');
  link.href = url;
  link.download = `${projectName.replace(/[^a-zA-Z0-9-_]/g, '_')}_export.json`;
  document.body.appendChild(link);
  link.click();
  document.body.removeChild(link);
  URL.revokeObjectURL(url);
}

/**
 * Parse and validate an imported project JSON file
 */
export async function parseImportFile(file: File): Promise<ProjectExport> {
  return new Promise((resolve, reject) => {
    const reader = new FileReader();

    reader.onload = (e: ProgressEvent<FileReader>) => {
      try {
        const result = e.target?.result;
        if (typeof result !== 'string') {
          throw new Error('Invalid file content');
        }

        const projectData = JSON.parse(result) as ProjectExport;

        if (!projectData.version || !projectData.project) {
          throw new Error('Invalid project file format');
        }

        resolve(projectData);
      } catch (error) {
        reject(new Error(`Failed to parse project file: ${(error as Error).message}`));
      }
    };

    reader.onerror = () => {
      reject(new Error('Failed to read file'));
    };

    reader.readAsText(file);
  });
}
