import axios from 'axios';
import { env } from '../config/env';

const API_BASE_URL = env.apiBaseUrl;

export interface ImageCandidate {
  imageUrl: string;
  source: string;
  aestheticScore: number;
  keywordCoverageScore: number;
  qualityScore: number;
  overallScore: number;
  reasoning: string;
  width: number;
  height: number;
  rejectionReasons: string[];
  generationLatencyMs: number;
  licensing?: {
    licenseType: string;
    commercialUseAllowed: boolean;
    attributionRequired: boolean;
    creatorName?: string;
    creatorUrl?: string;
    sourcePlatform: string;
    licenseUrl?: string;
    attribution?: string;
  };
}

export interface SceneVisualSelection {
  id: string;
  jobId: string;
  sceneIndex: number;
  selectedCandidate?: ImageCandidate;
  candidates: ImageCandidate[];
  state: 'Pending' | 'Accepted' | 'Rejected' | 'Replaced';
  rejectionReason?: string;
  selectedAt: string;
  selectedBy?: string;
  metadata?: {
    totalGenerationTimeMs: number;
    regenerationCount: number;
    autoSelected: boolean;
    autoSelectionConfidence?: number;
    llmAssistedRefinement: boolean;
    originalPrompt?: string;
    traceId?: string;
  };
}

export interface LicensingSummary {
  totalScenes: number;
  scenesWithSelection: number;
  commercialUseAllowed: boolean;
  requiresAttribution: boolean;
  licenseTypes: Array<{
    licenseType: string;
    count: number;
  }>;
  sources: Array<{
    source: string;
    count: number;
  }>;
  warnings: string[];
}

export interface PromptRefinementRequest {
  currentPrompt: unknown;
  currentCandidates: ImageCandidate[];
  issuesDetected: string[];
  userFeedback?: string;
}

export interface PromptRefinementResult {
  refinedPrompt: unknown;
  explanation: string;
  improvements: string[];
  expectedImprovement: number;
  confidence: number;
}

export interface AutoSelectionDecision {
  shouldAutoSelect: boolean;
  selectedCandidate?: ImageCandidate;
  confidence: number;
  reasoning: string;
  thresholdUsed: number;
}

class VisualSelectionService {
  private readonly baseUrl: string;

  constructor(baseUrl: string = API_BASE_URL) {
    this.baseUrl = baseUrl;
  }

  async getSelection(jobId: string, sceneIndex: number): Promise<SceneVisualSelection | null> {
    try {
      const response = await axios.get<SceneVisualSelection>(
        `${this.baseUrl}/api/visual-selection/${jobId}/scene/${sceneIndex}`
      );
      return response.data;
    } catch (error: unknown) {
      if (axios.isAxiosError(error) && error.response?.status === 404) {
        return null;
      }
      throw error;
    }
  }

  async getSelections(jobId: string): Promise<SceneVisualSelection[]> {
    const response = await axios.get<SceneVisualSelection[]>(
      `${this.baseUrl}/api/visual-selection/${jobId}`
    );
    return response.data;
  }

  async acceptCandidate(
    jobId: string,
    sceneIndex: number,
    candidate: ImageCandidate,
    userId?: string
  ): Promise<SceneVisualSelection> {
    const response = await axios.post<SceneVisualSelection>(
      `${this.baseUrl}/api/visual-selection/${jobId}/scene/${sceneIndex}/accept`,
      { candidate, userId }
    );
    return response.data;
  }

  async rejectSelection(
    jobId: string,
    sceneIndex: number,
    reason: string,
    userId?: string
  ): Promise<SceneVisualSelection> {
    const response = await axios.post<SceneVisualSelection>(
      `${this.baseUrl}/api/visual-selection/${jobId}/scene/${sceneIndex}/reject`,
      { reason, userId }
    );
    return response.data;
  }

  async replaceSelection(
    jobId: string,
    sceneIndex: number,
    newCandidate: ImageCandidate,
    userId?: string
  ): Promise<SceneVisualSelection> {
    const response = await axios.post<SceneVisualSelection>(
      `${this.baseUrl}/api/visual-selection/${jobId}/scene/${sceneIndex}/replace`,
      { newCandidate, userId }
    );
    return response.data;
  }

  async removeSelection(
    jobId: string,
    sceneIndex: number,
    userId?: string
  ): Promise<SceneVisualSelection> {
    const response = await axios.post<SceneVisualSelection>(
      `${this.baseUrl}/api/visual-selection/${jobId}/scene/${sceneIndex}/remove`,
      { userId }
    );
    return response.data;
  }

  async regenerateCandidates(
    jobId: string,
    sceneIndex: number,
    refinedPrompt?: unknown,
    config?: unknown,
    userId?: string
  ): Promise<SceneVisualSelection> {
    const response = await axios.post<SceneVisualSelection>(
      `${this.baseUrl}/api/visual-selection/${jobId}/scene/${sceneIndex}/regenerate`,
      { refinedPrompt, config, userId }
    );
    return response.data;
  }

  async suggestRefinement(
    jobId: string,
    sceneIndex: number,
    request: PromptRefinementRequest
  ): Promise<PromptRefinementResult> {
    const response = await axios.post<PromptRefinementResult>(
      `${this.baseUrl}/api/visual-selection/${jobId}/scene/${sceneIndex}/suggest-refinement`,
      request
    );
    return response.data;
  }

  async getSuggestions(
    jobId: string,
    sceneIndex: number,
    prompt: unknown,
    candidates: ImageCandidate[]
  ): Promise<string[]> {
    const response = await axios.post<string[]>(
      `${this.baseUrl}/api/visual-selection/${jobId}/scene/${sceneIndex}/suggestions`,
      { prompt, candidates }
    );
    return response.data;
  }

  async evaluateAutoSelection(
    jobId: string,
    sceneIndex: number,
    candidates: ImageCandidate[],
    confidenceThreshold: number = 85.0
  ): Promise<AutoSelectionDecision> {
    const response = await axios.post<AutoSelectionDecision>(
      `${this.baseUrl}/api/visual-selection/${jobId}/scene/${sceneIndex}/evaluate-auto-select`,
      { candidates, confidenceThreshold }
    );
    return response.data;
  }

  async getLicensingSummary(jobId: string): Promise<LicensingSummary> {
    const response = await axios.get<LicensingSummary>(
      `${this.baseUrl}/api/visual-selection/${jobId}/licensing/summary`
    );
    return response.data;
  }

  async exportLicensingCsv(jobId: string): Promise<Blob> {
    const response = await axios.get(
      `${this.baseUrl}/api/visual-selection/${jobId}/export/licensing/csv`,
      {
        responseType: 'blob',
      }
    );
    return response.data;
  }

  async exportLicensingJson(jobId: string): Promise<Blob> {
    const response = await axios.get(
      `${this.baseUrl}/api/visual-selection/${jobId}/export/licensing/json`,
      {
        responseType: 'blob',
      }
    );
    return response.data;
  }

  async exportAttributionText(jobId: string): Promise<string> {
    const response = await axios.get<string>(
      `${this.baseUrl}/api/visual-selection/${jobId}/export/attribution`,
      {
        responseType: 'text' as 'json',
      }
    );
    return response.data;
  }

  async getCandidates(request: GetCandidatesRequest): Promise<GetCandidatesResponse> {
    const response = await axios.post<GetCandidatesResponse>(
      `${this.baseUrl}/api/VisualSelection/candidates`,
      request
    );
    return response.data;
  }

  async regenerateCandidatesForScene(
    jobId: string,
    sceneIndex: number,
    refinedPrompt?: GetCandidatesRequest,
    config?: unknown,
    userId?: string
  ): Promise<GetCandidatesResponse> {
    const response = await axios.post<GetCandidatesResponse>(
      `${this.baseUrl}/api/VisualSelection/regenerate`,
      {
        jobId,
        sceneIndex,
        refinedPrompt,
        config,
        userId,
      }
    );
    return response.data;
  }

  downloadFile(blob: Blob, filename: string): void {
    const url = window.URL.createObjectURL(blob);
    const link = document.createElement('a');
    link.href = url;
    link.download = filename;
    document.body.appendChild(link);
    link.click();
    document.body.removeChild(link);
    window.URL.revokeObjectURL(url);
  }
}

export interface GetCandidatesRequest {
  sceneIndex: number;
  detailedDescription: string;
  subject: string;
  framing: string;
  narrativeKeywords: string[];
  style: string;
  qualityTier: string;
  config?: {
    minimumAestheticThreshold: number;
    candidatesPerScene: number;
    aestheticWeight: number;
    keywordWeight: number;
    qualityWeight: number;
    preferGeneratedImages: boolean;
    maxGenerationTimeSeconds: number;
  };
  useCache: boolean;
}

export interface GetCandidatesResponse {
  requestId: string;
  result: {
    sceneIndex: number;
    selectedImage?: ImageCandidate;
    candidates: ImageCandidate[];
    minimumAestheticThreshold: number;
    narrativeKeywords: string[];
    selectionTimeMs: number;
    meetsCriteria: boolean;
    warnings: string[];
  };
  fromCache: boolean;
  cachedAt?: string;
  expiresAt?: string;
}

export const visualSelectionService = new VisualSelectionService();
export default visualSelectionService;
