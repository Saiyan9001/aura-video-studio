export interface BriefData {
  topic: string;
  videoType: 'educational' | 'marketing' | 'social' | 'story' | 'tutorial' | 'explainer';
  targetAudience: string;
  keyMessage: string;
  duration: number;
}

export interface StyleData {
  voiceProvider: 'ElevenLabs' | 'PlayHT' | 'Windows' | 'Piper';
  voiceName: string;
  visualStyle: 'modern' | 'minimal' | 'cinematic' | 'playful' | 'professional';
  musicGenre: 'ambient' | 'upbeat' | 'dramatic' | 'none';
  musicEnabled: boolean;
  imageProvider?: string;
  imageStyle?: string;
  imageQuality?: number;
  imageAspectRatio?: string;
}

export interface ScriptScene {
  id: string;
  text: string;
  duration: number;
  visualDescription: string;
  timestamp: number;
}

export interface ScriptData {
  content: string;
  scenes: ScriptScene[];
  generatedAt: Date | null;
}

export interface PreviewThumbnail {
  sceneId: string;
  imageUrl: string;
  caption: string;
  provider?: string;
  generatedAt?: string;
  quality?: number;
  clipScore?: number;
  variations?: PreviewThumbnail[];
  isPlaceholder?: boolean;
  failureReason?: string;
}

export interface AudioSample {
  sceneId: string;
  audioUrl: string;
  duration: number;
  waveformData: number[];
}

export interface PreviewData {
  thumbnails: PreviewThumbnail[];
  audioSamples: AudioSample[];
  imageGenerationProgress?: number;
  imageProvider?: string;
}

export interface ExportData {
  quality: 'low' | 'medium' | 'high' | 'ultra';
  format: 'mp4' | 'webm' | 'mov';
  resolution: '480p' | '720p' | '1080p' | '4k';
  includeCaptions: boolean;
}

export interface AdvancedData {
  seoKeywords: string[];
  targetPlatform: 'youtube' | 'tiktok' | 'instagram' | 'twitter' | 'linkedin';
  customTransitions: boolean;
  sceneTimingOverrides?: Record<string, number>;
}

export interface WizardData {
  brief: BriefData;
  style: StyleData;
  script: ScriptData;
  preview: PreviewData;
  export: ExportData;
  advanced: AdvancedData;
}

export interface StepValidation {
  isValid: boolean;
  errors: string[];
}

export interface VideoTemplate {
  id: string;
  name: string;
  category: 'Educational' | 'Marketing' | 'Social' | 'Story';
  description: string;
  thumbnailUrl?: string;
  previewVideoUrl?: string;
  isTrending?: boolean;
  isFeatured?: boolean;
  estimatedDuration: number;
  requiredInputs: string[];
  defaultData: Partial<WizardData>;
}

export interface CostBreakdown {
  llmCost: number;
  ttsCost: number;
  imageGenerationCost: number;
  totalCost: number;
  breakdown: {
    provider: string;
    service: string;
    units: number;
    costPerUnit: number;
    subtotal: number;
  }[];
}

export interface WizardDraft {
  id: string;
  name: string;
  data: WizardData;
  createdAt: Date;
  updatedAt: Date;
  currentStep: number;
  progress: number;
}

export interface DraftMetadata {
  id: string;
  name: string;
  createdAt: Date;
  updatedAt: Date;
  currentStep: number;
  progress: number;
  briefSummary: string;
}
