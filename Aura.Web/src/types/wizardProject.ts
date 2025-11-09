/**
 * Types for wizard project saving and loading
 */

export interface WizardProjectListItem {
  id: string;
  name: string;
  description?: string;
  status: string;
  progressPercent: number;
  currentStep: number;
  createdAt: string;
  updatedAt: string;
  jobId?: string;
  hasGeneratedContent: boolean;
}

export interface GeneratedAsset {
  assetType: string;
  filePath: string;
  fileSizeBytes: number;
  createdAt: string;
}

export interface WizardProjectDetails {
  id: string;
  name: string;
  description?: string;
  status: string;
  progressPercent: number;
  currentStep: number;
  createdAt: string;
  updatedAt: string;
  briefJson?: string;
  planSpecJson?: string;
  voiceSpecJson?: string;
  renderSpecJson?: string;
  jobId?: string;
  generatedAssets: GeneratedAsset[];
}

export interface SaveWizardProjectRequest {
  id?: string;
  name: string;
  description?: string;
  currentStep: number;
  briefJson?: string;
  planSpecJson?: string;
  voiceSpecJson?: string;
  renderSpecJson?: string;
}

export interface SaveWizardProjectResponse {
  id: string;
  name: string;
  lastModifiedAt: string;
}

export interface DuplicateProjectRequest {
  newName: string;
}

export interface ProjectImportRequest {
  projectJson: string;
  newName?: string;
}

export interface ClearGeneratedContentRequest {
  keepScript: boolean;
  keepAudio: boolean;
  keepImages: boolean;
  keepVideo: boolean;
}

export interface ProjectExport {
  version: string;
  project: WizardProjectDetails;
  exportedAt: string;
}
