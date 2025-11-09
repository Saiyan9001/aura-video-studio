/**
 * Tests for wizard project API client
 */

import { describe, it, expect, vi, beforeEach, type MockedFunction } from 'vitest';
import apiClient from '../../services/api/apiClient';
import { generateDefaultProjectName } from '../../state/wizardProject';
import type {
  SaveWizardProjectRequest,
  SaveWizardProjectResponse,
  WizardProjectDetails,
  WizardProjectListItem,
} from '../../types/wizardProject';
import {
  saveWizardProject,
  getWizardProject,
  getAllWizardProjects,
  getRecentWizardProjects,
  duplicateWizardProject,
  deleteWizardProject,
  exportWizardProject,
  importWizardProject,
  clearGeneratedContent,
} from '../wizardProjects';

vi.mock('../../services/api/apiClient', () => ({
  default: {
    get: vi.fn(),
    post: vi.fn(),
    delete: vi.fn(),
  },
}));

const mockedApiClient = apiClient as {
  get: MockedFunction<typeof apiClient.get>;
  post: MockedFunction<typeof apiClient.post>;
  delete: MockedFunction<typeof apiClient.delete>;
};

describe('Wizard Projects API Client', () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  describe('saveWizardProject', () => {
    it('should save a new project', async () => {
      const request: SaveWizardProjectRequest = {
        name: 'Test Project',
        currentStep: 0,
        briefJson: '{"topic":"test"}',
      };

      const mockResponse: SaveWizardProjectResponse = {
        id: 'project-123',
        name: 'Test Project',
        lastModifiedAt: '2025-01-01T00:00:00Z',
      };

      mockedApiClient.post.mockResolvedValue({ data: mockResponse } as never);

      const result = await saveWizardProject(request);

      expect(mockedApiClient.post).toHaveBeenCalledWith('/api/wizard-projects', request);
      expect(result).toEqual(mockResponse);
    });

    it('should update an existing project', async () => {
      const request: SaveWizardProjectRequest = {
        id: 'project-123',
        name: 'Updated Project',
        currentStep: 1,
        briefJson: '{"topic":"test"}',
        planSpecJson: '{"scenes":[]}',
      };

      const mockResponse: SaveWizardProjectResponse = {
        id: 'project-123',
        name: 'Updated Project',
        lastModifiedAt: '2025-01-01T00:00:00Z',
      };

      mockedApiClient.post.mockResolvedValue({ data: mockResponse } as never);

      const result = await saveWizardProject(request);

      expect(mockedApiClient.post).toHaveBeenCalledWith('/api/wizard-projects', request);
      expect(result).toEqual(mockResponse);
    });
  });

  describe('getWizardProject', () => {
    it('should get a project by ID', async () => {
      const mockProject: WizardProjectDetails = {
        id: 'project-123',
        name: 'Test Project',
        status: 'Draft',
        progressPercent: 50,
        currentStep: 1,
        createdAt: '2025-01-01T00:00:00Z',
        updatedAt: '2025-01-01T00:00:00Z',
        briefJson: '{"topic":"test"}',
        generatedAssets: [],
      };

      mockedApiClient.get.mockResolvedValue({ data: mockProject } as never);

      const result = await getWizardProject('project-123');

      expect(mockedApiClient.get).toHaveBeenCalledWith('/api/wizard-projects/project-123');
      expect(result).toEqual(mockProject);
    });
  });

  describe('getAllWizardProjects', () => {
    it('should get all projects', async () => {
      const mockProjects: WizardProjectListItem[] = [
        {
          id: 'project-1',
          name: 'Project 1',
          status: 'Draft',
          progressPercent: 25,
          currentStep: 0,
          createdAt: '2025-01-01T00:00:00Z',
          updatedAt: '2025-01-01T00:00:00Z',
          hasGeneratedContent: false,
        },
        {
          id: 'project-2',
          name: 'Project 2',
          status: 'InProgress',
          progressPercent: 75,
          currentStep: 2,
          createdAt: '2025-01-01T00:00:00Z',
          updatedAt: '2025-01-01T00:00:00Z',
          hasGeneratedContent: true,
        },
      ];

      mockedApiClient.get.mockResolvedValue({ data: mockProjects } as never);

      const result = await getAllWizardProjects();

      expect(mockedApiClient.get).toHaveBeenCalledWith('/api/wizard-projects');
      expect(result).toEqual(mockProjects);
    });
  });

  describe('getRecentWizardProjects', () => {
    it('should get recent projects with default count', async () => {
      const mockProjects: WizardProjectListItem[] = [];

      mockedApiClient.get.mockResolvedValue({ data: mockProjects } as never);

      await getRecentWizardProjects();

      expect(mockedApiClient.get).toHaveBeenCalledWith('/api/wizard-projects/recent', {
        params: { count: 10 },
      });
    });

    it('should get recent projects with custom count', async () => {
      const mockProjects: WizardProjectListItem[] = [];

      mockedApiClient.get.mockResolvedValue({ data: mockProjects } as never);

      await getRecentWizardProjects(5);

      expect(mockedApiClient.get).toHaveBeenCalledWith('/api/wizard-projects/recent', {
        params: { count: 5 },
      });
    });
  });

  describe('duplicateWizardProject', () => {
    it('should duplicate a project', async () => {
      const mockResponse: SaveWizardProjectResponse = {
        id: 'project-456',
        name: 'Test Project (Copy)',
        lastModifiedAt: '2025-01-01T00:00:00Z',
      };

      mockedApiClient.post.mockResolvedValue({ data: mockResponse } as never);

      const result = await duplicateWizardProject('project-123', {
        newName: 'Test Project (Copy)',
      });

      expect(mockedApiClient.post).toHaveBeenCalledWith(
        '/api/wizard-projects/project-123/duplicate',
        { newName: 'Test Project (Copy)' }
      );
      expect(result).toEqual(mockResponse);
    });
  });

  describe('deleteWizardProject', () => {
    it('should delete a project', async () => {
      mockedApiClient.delete.mockResolvedValue({ data: undefined } as never);

      await deleteWizardProject('project-123');

      expect(mockedApiClient.delete).toHaveBeenCalledWith('/api/wizard-projects/project-123');
    });
  });

  describe('exportWizardProject', () => {
    it('should export a project as JSON', async () => {
      const mockJson = JSON.stringify({ version: '1.0.0', project: {} });

      mockedApiClient.get.mockResolvedValue({ data: mockJson } as never);

      const result = await exportWizardProject('project-123');

      expect(mockedApiClient.get).toHaveBeenCalledWith('/api/wizard-projects/project-123/export', {
        responseType: 'text',
      });
      expect(result).toEqual(mockJson);
    });
  });

  describe('importWizardProject', () => {
    it('should import a project from JSON', async () => {
      const mockResponse: SaveWizardProjectResponse = {
        id: 'project-789',
        name: 'Imported Project',
        lastModifiedAt: '2025-01-01T00:00:00Z',
      };

      mockedApiClient.post.mockResolvedValue({ data: mockResponse } as never);

      const result = await importWizardProject({
        projectJson: '{"version":"1.0.0","project":{}}',
        newName: 'Imported Project',
      });

      expect(mockedApiClient.post).toHaveBeenCalledWith('/api/wizard-projects/import', {
        projectJson: '{"version":"1.0.0","project":{}}',
        newName: 'Imported Project',
      });
      expect(result).toEqual(mockResponse);
    });
  });

  describe('clearGeneratedContent', () => {
    it('should clear generated content', async () => {
      const mockResponse = {
        message: 'Generated content cleared successfully',
        projectId: 'project-123',
      };

      mockedApiClient.post.mockResolvedValue({ data: mockResponse } as never);

      const result = await clearGeneratedContent('project-123', {
        keepScript: false,
        keepAudio: false,
        keepImages: false,
        keepVideo: false,
      });

      expect(mockedApiClient.post).toHaveBeenCalledWith(
        '/api/wizard-projects/project-123/clear-content',
        {
          keepScript: false,
          keepAudio: false,
          keepImages: false,
          keepVideo: false,
        }
      );
      expect(result).toEqual(mockResponse);
    });
  });

  describe('generateDefaultProjectName', () => {
    it('should generate a default name with timestamp', () => {
      const name = generateDefaultProjectName();

      expect(name).toMatch(/^Project \d{2}\/\d{2}\/\d{4} \d{2}:\d{2}$/);
    });
  });
});
