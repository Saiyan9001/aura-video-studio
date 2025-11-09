import apiClient from './apiClient';

export interface Brief {
  topic: string;
  audience?: string;
  goal?: string;
  tone: string;
  language: string;
  aspect: string;
}

export interface PlanSpec {
  targetDuration: string;
  pacing: string;
  density: string;
  style: string;
}

export interface Scene {
  number: number;
  narration: string;
  visualPrompt: string;
  duration: number;
  transition: string;
}

export interface Script {
  title: string;
  scenes: Scene[];
  totalDuration: number;
  correlationId?: string;
}

export interface ScriptQualityMetrics {
  narrativeCoherence: number;
  pacingAppropriateness: number;
  audienceAlignment: number;
  visualClarity: number;
  engagementPotential: number;
  overallScore: number;
  issues: string[];
  suggestions: string[];
  strengths: string[];
}

export interface ScriptRefinementConfig {
  maxRefinementPasses?: number;
  qualityThreshold?: number;
  minimumImprovement?: number;
}

export interface ScriptRefinementResult {
  success: boolean;
  finalScript?: string;
  iterationMetrics: ScriptQualityMetrics[];
  totalPasses: number;
  stopReason: string;
  totalDuration: number;
  critiqueSummary?: string;
  errorMessage?: string;
}

export interface ScriptVariation {
  variationId: string;
  name: string;
  script: Script;
  qualityScore: number;
  focus: string;
}

/**
 * API service for advanced script generation and refinement
 */
export const advancedScriptApi = {
  /**
   * Generate a high-quality script with advanced prompting
   */
  async generateScript(
    brief: Brief,
    planSpec: PlanSpec,
    videoType: string = 'General',
    modelOverride?: string,
    temperatureOverride?: number
  ): Promise<{ script: Script; qualityMetrics: ScriptQualityMetrics }> {
    const response = await apiClient.post('/api/advanced-script/generate', {
      brief,
      planSpec,
      videoType,
      modelOverride,
      temperatureOverride,
    });

    return response.data;
  },

  /**
   * Analyze script quality without regeneration
   */
  async analyzeScript(
    script: Script,
    brief: Brief,
    planSpec: PlanSpec
  ): Promise<{
    metrics: ScriptQualityMetrics;
    validations: {
      readingSpeed: any;
      sceneCount: any;
      visualPrompts: any;
      narrativeFlow: any;
      contentCheck: any;
    };
  }> {
    const response = await apiClient.post('/api/advanced-script/analyze', {
      script,
      brief,
      planSpec,
    });

    return response.data;
  },

  /**
   * Auto-refine script based on quality analysis
   */
  async refineScript(
    script: Script,
    brief: Brief,
    planSpec: PlanSpec,
    videoType: string = 'General',
    config?: ScriptRefinementConfig
  ): Promise<ScriptRefinementResult> {
    const response = await apiClient.post('/api/advanced-script/refine', {
      script,
      brief,
      planSpec,
      videoType,
      config,
    });

    return response.data.result;
  },

  /**
   * Manual improvement with specific goal
   */
  async improveScript(
    script: Script,
    brief: Brief,
    planSpec: PlanSpec,
    improvementGoal: string,
    videoType: string = 'General'
  ): Promise<{ script: Script; qualityMetrics: ScriptQualityMetrics }> {
    const response = await apiClient.post('/api/advanced-script/improve', {
      script,
      brief,
      planSpec,
      videoType,
      improvementGoal,
    });

    return response.data;
  },

  /**
   * Regenerate a specific scene
   */
  async regenerateScene(
    script: Script,
    sceneNumber: number,
    improvementGoal: string,
    brief: Brief,
    planSpec: PlanSpec
  ): Promise<Scene> {
    const response = await apiClient.post('/api/advanced-script/regenerate-scene', {
      script,
      sceneNumber,
      improvementGoal,
      brief,
      planSpec,
    });

    return response.data.scene;
  },

  /**
   * Generate multiple script variations for A/B testing
   */
  async generateVariations(
    script: Script,
    brief: Brief,
    planSpec: PlanSpec,
    videoType: string = 'General',
    variationCount: number = 3
  ): Promise<ScriptVariation[]> {
    const response = await apiClient.post('/api/advanced-script/variations', {
      script,
      brief,
      planSpec,
      videoType,
      variationCount,
    });

    return response.data.variations;
  },

  /**
   * Optimize the opening hook
   */
  async optimizeHook(script: Script, brief: Brief, targetSeconds: number = 3): Promise<Script> {
    const response = await apiClient.post('/api/advanced-script/optimize-hook', {
      script,
      brief,
      targetSeconds,
    });

    return response.data.script;
  },
};

export default advancedScriptApi;
