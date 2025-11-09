/**
 * API V1 Type Definitions
 *
 * These types match Aura.Api.Models.ApiModels.V1 exactly.
 * Keep in sync with backend types when making changes.
 */

// ============================================================================
// ENUMS
// ============================================================================

/**
 * Video pacing style - affects narration speed and scene transitions
 * Canonical values: "Chill", "Conversational", "Fast"
 */
export enum Pacing {
  Chill = 'Chill',
  Conversational = 'Conversational',
  Fast = 'Fast',
}

/**
 * Content density - affects information per scene
 * Canonical values: "Sparse", "Balanced", "Dense"
 * Legacy alias: "Normal" -> "Balanced"
 */
export enum Density {
  Sparse = 'Sparse',
  Balanced = 'Balanced',
  Dense = 'Dense',
}

/**
 * Video aspect ratio
 * Canonical values: "Widescreen16x9", "Vertical9x16", "Square1x1"
 * Legacy aliases: "16:9" -> "Widescreen16x9", "9:16" -> "Vertical9x16", "1:1" -> "Square1x1"
 */
export enum Aspect {
  Widescreen16x9 = 'Widescreen16x9',
  Vertical9x16 = 'Vertical9x16',
  Square1x1 = 'Square1x1',
}

/**
 * TTS pause style between sentences
 * Canonical values: "Natural", "Short", "Long", "Dramatic"
 */
export enum PauseStyle {
  Natural = 'Natural',
  Short = 'Short',
  Long = 'Long',
  Dramatic = 'Dramatic',
}

/**
 * Provider mode selection
 */
export enum ProviderMode {
  Free = 'Free',
  Pro = 'Pro',
}

/**
 * Hardware tier classification
 */
export enum HardwareTier {
  A = 'A', // High (≥12GB VRAM or NVIDIA 40/50-series)
  B = 'B', // Upper-mid (8-12GB VRAM)
  C = 'C', // Mid (6-8GB VRAM)
  D = 'D', // Entry (≤4-6GB VRAM or no GPU)
}

// ============================================================================
// REQUEST DTOS
// ============================================================================

export interface PlanRequest {
  targetDurationMinutes: number;
  pacing: Pacing;
  density: Density;
  style: string;
}

export interface ScriptRequest {
  topic: string;
  audience: string;
  goal: string;
  tone: string;
  language: string;
  aspect: Aspect;
  targetDurationMinutes: number;
  pacing: Pacing;
  density: Density;
  style: string;
  providerTier?: string | null;
  audienceProfileId?: string | null;
}

export interface LineDto {
  sceneIndex: number;
  text: string;
  startSeconds: number;
  durationSeconds: number;
}

export interface TtsRequest {
  lines: LineDto[];
  voiceName: string;
  rate: number;
  pitch: number;
  pauseStyle: PauseStyle;
}

export interface ComposeRequest {
  timelineJson: string;
}

export interface RenderRequest {
  timelineJson: string;
  presetName: string;
}

export interface ApplyProfileRequest {
  profileName: string;
}

export interface ApiKeysRequest {
  openAiKey?: string | null;
  elevenLabsKey?: string | null;
  pexelsKey?: string | null;
  stabilityAiKey?: string | null;
}

export interface ProviderPathsRequest {
  stableDiffusionUrl?: string | null;
  ollamaUrl?: string | null;
  ffmpegPath?: string | null;
  ffprobePath?: string | null;
  outputDirectory?: string | null;
}

export interface ProviderTestRequest {
  url?: string | null;
  path?: string | null;
}

export interface ConstraintsDto {
  maxSceneCount?: number | null;
  minSceneCount?: number | null;
  maxBRollPercentage?: number | null;
  maxReadingLevel?: number | null;
}

export interface RecommendationsRequestDto {
  topic: string;
  audience?: string | null;
  goal?: string | null;
  tone?: string | null;
  language?: string | null;
  aspect?: Aspect | null;
  targetDurationMinutes: number;
  pacing?: Pacing | null;
  density?: Density | null;
  style?: string | null;
  audiencePersona?: string | null;
  constraints?: ConstraintsDto | null;
}

export interface AssetSearchRequest {
  provider: string;
  query: string;
  count: number;
  apiKey?: string | null;
  localDirectory?: string | null;
}

export interface AssetGenerateRequest {
  prompt: string;
  style?: string | null;
  aspect?: Aspect | null;
}

// ============================================================================
// RESPONSE DTOS
// ============================================================================

export interface RenderJobDto {
  id: string;
  status: string;
  progress: number;
  outputPath?: string | null;
  createdAt: string;
}

// ============================================================================
// HELPER TYPES
// ============================================================================

/**
 * Type-safe enum value type
 */
export type PacingValue = `${Pacing}`;
export type DensityValue = `${Density}`;
export type AspectValue = `${Aspect}`;
export type PauseStyleValue = `${PauseStyle}`;

/**
 * Enum value arrays for validation and UI dropdowns
 */
export const PacingValues = Object.values(Pacing);
export const DensityValues = Object.values(Density);
export const AspectValues = Object.values(Aspect);
export const PauseStyleValues = Object.values(PauseStyle);

// ============================================================================
// OLLAMA TYPES
// ============================================================================

export interface OllamaModel {
  name: string;
  size: number;
  sizeGB: number;
  modifiedAt: string | null;
  digest: string | null;
}

export interface OllamaModelsResponse {
  models: OllamaModel[];
  baseUrl: string;
}

// ============================================================================
// AUDIENCE PROFILE TYPES
// ============================================================================

export interface AudienceProfileDto {
  id: string | null;
  name: string;
  description: string | null;
  ageRange: AgeRangeDto | null;
  educationLevel: string | null;
  profession: string | null;
  industry: string | null;
  expertiseLevel: string | null;
  incomeBracket: string | null;
  geographicRegion: string | null;
  languageFluency: LanguageFluencyDto | null;
  interests: string[] | null;
  painPoints: string[] | null;
  motivations: string[] | null;
  culturalBackground: CulturalBackgroundDto | null;
  preferredLearningStyle: string | null;
  attentionSpan: AttentionSpanDto | null;
  technicalComfort: string | null;
  accessibilityNeeds: AccessibilityNeedsDto | null;
  isTemplate: boolean;
  tags: string[] | null;
  version: number;
  createdAt: string | null;
  updatedAt: string | null;
  isFavorite: boolean;
  folderPath: string | null;
  usageCount: number;
  lastUsedAt: string | null;
}

export interface AgeRangeDto {
  minAge: number;
  maxAge: number;
  displayName: string;
  contentRating: string;
}

export interface LanguageFluencyDto {
  language: string;
  level: string;
}

export interface CulturalBackgroundDto {
  sensitivities: string[] | null;
  tabooTopics: string[] | null;
  preferredCommunicationStyle: string;
}

export interface AttentionSpanDto {
  preferredDurationMinutes: number;
  displayName: string;
}

export interface AccessibilityNeedsDto {
  requiresCaptions: boolean;
  requiresAudioDescriptions: boolean;
  requiresHighContrast: boolean;
  requiresSimplifiedLanguage: boolean;
  requiresLargeText: boolean;
}

export interface CreateAudienceProfileRequest {
  profile: AudienceProfileDto;
}

export interface UpdateAudienceProfileRequest {
  profile: AudienceProfileDto;
}

export interface AudienceProfileResponse {
  profile: AudienceProfileDto;
  validation: ValidationResultDto | null;
}

export interface AudienceProfileListResponse {
  profiles: AudienceProfileDto[];
  totalCount: number;
  page: number;
  pageSize: number;
}

export interface ValidationResultDto {
  isValid: boolean;
  errors: ValidationIssueDto[];
  warnings: ValidationIssueDto[];
  infos: ValidationIssueDto[];
}

export interface ValidationIssueDto {
  severity: string;
  field: string;
  message: string;
  suggestedFix: string | null;
}

export interface AnalyzeAudienceRequest {
  scriptText: string;
}

export interface AnalyzeAudienceResponse {
  inferredProfile: AudienceProfileDto;
  confidenceScore: number;
  reasoningFactors: string[];
}

export interface MoveToFolderRequest {
  folderPath: string | null;
}

export interface FolderListResponse {
  folders: string[];
}

export interface ExportProfileResponse {
  json: string;
}

export interface ImportProfileRequest {
  json: string;
}

export interface RecommendProfilesRequest {
  topic: string;
  goal?: string | null;
  maxResults?: number;
}

// Enum values for audience profile fields
export const EducationLevels = [
  'HighSchool',
  'SomeCollege',
  'AssociateDegree',
  'BachelorDegree',
  'MasterDegree',
  'Doctorate',
  'Vocational',
  'SelfTaught',
  'InProgress',
] as const;

export const ExpertiseLevels = [
  'CompleteBeginner',
  'Novice',
  'Intermediate',
  'Advanced',
  'Expert',
  'Professional',
] as const;

export const IncomeBrackets = [
  'NotSpecified',
  'LowIncome',
  'MiddleIncome',
  'UpperMiddleIncome',
  'HighIncome',
] as const;

export const GeographicRegions = [
  'Global',
  'NorthAmerica',
  'Europe',
  'Asia',
  'LatinAmerica',
  'MiddleEast',
  'Africa',
  'Oceania',
] as const;

export const FluencyLevels = ['Native', 'Fluent', 'Intermediate', 'Beginner'] as const;

export const CommunicationStyles = [
  'Direct',
  'Indirect',
  'Formal',
  'Casual',
  'Humorous',
  'Professional',
] as const;

export const LearningStyles = [
  'Visual',
  'Auditory',
  'Kinesthetic',
  'ReadingWriting',
  'Multimodal',
] as const;

export const TechnicalComfortLevels = [
  'NonTechnical',
  'BasicUser',
  'Moderate',
  'TechSavvy',
  'Expert',
] as const;

export const ContentRatings = ['ChildSafe', 'TeenAppropriate', 'Adult'] as const;

// ============================================================================
// TRANSLATION AND LOCALIZATION TYPES
// ============================================================================

/**
 * Translation mode selection
 */
export enum TranslationMode {
  Literal = 'Literal',
  Localized = 'Localized',
  Transcreation = 'Transcreation',
}

/**
 * Formality level for translation
 */
export enum FormalityLevel {
  Informal = 'Informal',
  Neutral = 'Neutral',
  Formal = 'Formal',
}

/**
 * Age rating for content
 */
export enum AgeRating {
  ChildSafe = 'ChildSafe',
  TeenAppropriate = 'TeenAppropriate',
  Adult = 'Adult',
}

/**
 * Script line for translation
 */
export interface ScriptLineDto {
  text: string;
  startSeconds: number;
  durationSeconds: number;
}

/**
 * Cultural context for translation
 */
export interface CulturalContextDto {
  targetRegion: string;
  targetFormality: string;
  preferredStyle: string;
  sensitivities: string[];
  tabooTopics: string[];
  contentRating: string;
}

/**
 * Translation options
 */
export interface TranslationOptionsDto {
  mode?: string;
  enableBackTranslation?: boolean;
  enableQualityScoring?: boolean;
  adjustTimings?: boolean;
  maxTimingVariance?: number;
  preserveNames?: boolean;
  preserveBrands?: boolean;
  adaptMeasurements?: boolean;
}

/**
 * Translation request
 */
export interface TranslateScriptRequest {
  sourceLanguage: string;
  targetLanguage: string;
  sourceText?: string;
  scriptLines?: ScriptLineDto[];
  culturalContext?: CulturalContextDto;
  options?: TranslationOptionsDto;
  glossary?: Record<string, string>;
  audienceProfileId?: string;
}

/**
 * Translated script line with timing adjustments
 */
export interface TranslatedScriptLineDto {
  sceneIndex: number;
  sourceText: string;
  translatedText: string;
  originalStartSeconds: number;
  originalDurationSeconds: number;
  adjustedStartSeconds: number;
  adjustedDurationSeconds: number;
  timingVariance: number;
  adaptationNotes: string[];
}

/**
 * Quality issue
 */
export interface QualityIssueDto {
  severity: string;
  category: string;
  description: string;
  suggestion?: string;
  lineNumber?: number;
}

/**
 * Translation quality metrics
 */
export interface TranslationQualityDto {
  overallScore: number;
  fluencyScore: number;
  accuracyScore: number;
  culturalAppropriatenessScore: number;
  terminologyConsistencyScore: number;
  backTranslationScore: number;
  backTranslatedText?: string;
  issues: QualityIssueDto[];
}

/**
 * Cultural adaptation
 */
export interface CulturalAdaptationDto {
  category: string;
  sourcePhrase: string;
  adaptedPhrase: string;
  reasoning: string;
  lineNumber?: number;
}

/**
 * Timing warning
 */
export interface TimingWarningDto {
  severity: string;
  message: string;
  lineNumber?: number;
}

/**
 * Timing adjustment information
 */
export interface TimingAdjustmentDto {
  originalTotalDuration: number;
  adjustedTotalDuration: number;
  expansionFactor: number;
  requiresCompression: boolean;
  compressionSuggestions: string[];
  warnings: TimingWarningDto[];
}

/**
 * Visual localization recommendation
 */
export interface VisualLocalizationRecommendationDto {
  elementType: string;
  description: string;
  recommendation: string;
  priority: string;
  sceneIndex?: number;
}

/**
 * Translation result
 */
export interface TranslationResultDto {
  sourceLanguage: string;
  targetLanguage: string;
  sourceText: string;
  translatedText: string;
  translatedLines: TranslatedScriptLineDto[];
  quality: TranslationQualityDto;
  culturalAdaptations: CulturalAdaptationDto[];
  timingAdjustment: TimingAdjustmentDto;
  visualRecommendations: VisualLocalizationRecommendationDto[];
  translationTimeSeconds: number;
}

/**
 * Batch translation request
 */
export interface BatchTranslateRequest {
  sourceLanguage: string;
  targetLanguages: string[];
  sourceText?: string;
  scriptLines?: ScriptLineDto[];
  culturalContext?: CulturalContextDto;
  options?: TranslationOptionsDto;
  glossary?: Record<string, string>;
}

/**
 * Batch translation result
 */
export interface BatchTranslationResultDto {
  sourceLanguage: string;
  translations: Record<string, TranslationResultDto>;
  successfulLanguages: string[];
  failedLanguages: string[];
  totalTimeSeconds: number;
}

/**
 * Cultural analysis request
 */
export interface CulturalAnalysisRequest {
  targetLanguage: string;
  targetRegion: string;
  content: string;
  audienceProfileId?: string;
}

/**
 * Cultural issue
 */
export interface CulturalIssueDto {
  severity: string;
  category: string;
  issue: string;
  context: string;
  suggestion?: string;
}

/**
 * Cultural recommendation
 */
export interface CulturalRecommendationDto {
  category: string;
  recommendation: string;
  reasoning: string;
  priority: string;
}

/**
 * Cultural analysis result
 */
export interface CulturalAnalysisResultDto {
  targetLanguage: string;
  targetRegion: string;
  culturalSensitivityScore: number;
  issues: CulturalIssueDto[];
  recommendations: CulturalRecommendationDto[];
}

/**
 * Language information
 */
export interface LanguageInfoDto {
  code: string;
  name: string;
  nativeName: string;
  region: string;
  isRightToLeft: boolean;
  defaultFormality: string;
  typicalExpansionFactor: number;
}

/**
 * Glossary entry
 */
export interface GlossaryEntryDto {
  id: string;
  term: string;
  translations: Record<string, string>;
  context?: string;
  industry?: string;
}

/**
 * Project glossary
 */
export interface ProjectGlossaryDto {
  id: string;
  name: string;
  description?: string;
  entries: GlossaryEntryDto[];
  createdAt: string;
  updatedAt: string;
}

/**
 * Create glossary request
 */
export interface CreateGlossaryRequest {
  name: string;
  description?: string;
}

/**
 * Add glossary entry request
 */
export interface AddGlossaryEntryRequest {
  term: string;
  translations: Record<string, string>;
  context?: string;
  industry?: string;
}

// ============================================================================
// HEALTH CHECK TYPES
// ============================================================================

/**
 * High-level summary of system health status
 */
export interface HealthSummaryResponse {
  overallStatus: string;
  isReady: boolean;
  totalChecks: number;
  passedChecks: number;
  warningChecks: number;
  failedChecks: number;
  timestamp: string;
}

/**
 * Detailed health check information with per-check results
 */
export interface HealthDetailsResponse {
  overallStatus: string;
  isReady: boolean;
  checks: HealthCheckDetail[];
  timestamp: string;
}

/**
 * Individual health check detail with remediation information
 */
export interface HealthCheckDetail {
  id: string;
  name: string;
  category: string;
  status: string;
  isRequired: boolean;
  message?: string;
  data?: Record<string, unknown>;
  remediationHint?: string;
  remediationActions?: RemediationAction[];
}

/**
 * Actionable remediation step for a failed health check
 */
export interface RemediationAction {
  type: string;
  label: string;
  description: string;
  navigateTo?: string;
  externalUrl?: string;
  parameters?: Record<string, string>;
}

/**
 * Health check status values
 */
export const HealthCheckStatus = {
  Pass: 'pass',
  Warning: 'warning',
  Fail: 'fail',
} as const;

/**
 * Health check categories
 */
export const HealthCheckCategory = {
  System: 'System',
  Configuration: 'Configuration',
  LLM: 'LLM',
  TTS: 'TTS',
  Image: 'Image',
  Video: 'Video',
} as const;

/**
 * Circuit breaker states
 */
export enum CircuitBreakerState {
  Closed = 'Closed',
  Open = 'Open',
  HalfOpen = 'HalfOpen',
}

/**
 * Provider health check DTO with circuit breaker info
 */
export interface ProviderHealthCheckDto {
  providerName: string;
  isHealthy: boolean;
  lastCheckTime: string;
  responseTimeMs: number;
  consecutiveFailures: number;
  lastError?: string | null;
  successRate: number;
  averageResponseTimeMs: number;
  circuitState: string;
  failureRate: number;
  circuitOpenedAt?: string | null;
}

/**
 * Provider type health status (e.g., all LLM providers)
 */
export interface ProviderTypeHealthDto {
  providerType: string;
  providers: ProviderHealthCheckDto[];
  isHealthy: boolean;
  healthyCount: number;
  totalCount: number;
}

/**
 * System health check DTO
 */
export interface SystemHealthDto {
  ffmpegAvailable: boolean;
  ffmpegVersion?: string | null;
  diskSpaceGB: number;
  memoryUsagePercent: number;
  isHealthy: boolean;
  issues: string[];
}

/**
 * Provider health summary DTO
 */
export interface ProviderHealthSummaryDto {
  totalProviders: number;
  healthyProviders: number;
  degradedProviders: number;
  offlineProviders: number;
  lastUpdateTime: string;
  providersByType: Record<string, ProviderTypeHealth>;
}

/**
 * Health status for a specific provider type
 */
export interface ProviderTypeHealth {
  total: number;
  healthy: number;
  degraded: number;
  offline: number;
}

/**
 * Remediation action types
 */
export const RemediationActionType = {
  OpenSettings: 'open_settings',
  Install: 'install',
  Configure: 'configure',
  Start: 'start',
  OpenHelp: 'open_help',
  SwitchProvider: 'switch_provider',
} as const;

// Ollama Process Control Types

/**
 * Ollama service status response
 */
export interface OllamaStatusResponse {
  running: boolean;
  pid?: number;
  managedByApp: boolean;
  model?: string;
  error?: string;
}

/**
 * Ollama start operation response
 */
export interface OllamaStartResponse {
  success: boolean;
  message: string;
  pid?: number;
}

/**
 * Ollama stop operation response
 */
export interface OllamaStopResponse {
  success: boolean;
  message: string;
}

/**
 * Ollama logs response
 */
export interface OllamaLogsResponse {
  logs: string[];
  totalLines: number;
}

// ============================================================================
// ACTION LOG / UNDO-REDO TYPES
// ============================================================================

/**
 * Request to record a new action in the action log
 */
export interface RecordActionRequest {
  userId?: string;
  actionType: string;
  description: string;
  affectedResourceIds?: string;
  payloadJson?: string;
  inverseActionType?: string;
  inversePayloadJson?: string;
  canBatch?: boolean;
  isPersistent?: boolean;
  correlationId?: string;
  retentionDays?: number;
}

/**
 * Response after recording an action
 */
export interface RecordActionResponse {
  actionId: string; // Guid as string
  timestamp: string; // DateTime as ISO string
  status: string;
  expiresAt?: string; // DateTime as ISO string
}

/**
 * Response for an undo operation
 */
export interface UndoActionResponse {
  actionId: string;
  success: boolean;
  undoneAt: string;
  errorMessage?: string;
  status: string;
}

/**
 * Query parameters for action history
 */
export interface ActionHistoryQuery {
  userId?: string;
  actionType?: string;
  status?: string;
  startDate?: string;
  endDate?: string;
  page?: number;
  pageSize?: number;
}

/**
 * Single action in history
 */
export interface ActionHistoryItem {
  id: string;
  userId: string;
  actionType: string;
  description: string;
  timestamp: string;
  status: string;
  canUndo: boolean;
  affectedResourceIds?: string;
  undoneAt?: string;
  undoneByUserId?: string;
}

/**
 * Paginated action history response
 */
export interface ActionHistoryResponse {
  actions: ActionHistoryItem[];
  page: number;
  pageSize: number;
  totalCount: number;
  totalPages: number;
}

/**
 * Detailed action information
 */
export interface ActionDetailResponse {
  id: string;
  userId: string;
  actionType: string;
  description: string;
  timestamp: string;
  status: string;
  affectedResourceIds?: string;
  payloadJson?: string;
  inverseActionType?: string;
  inversePayloadJson?: string;
  canBatch: boolean;
  isPersistent: boolean;
  undoneAt?: string;
  undoneByUserId?: string;
  expiresAt?: string;
  errorMessage?: string;
  correlationId?: string;
}

// ============================================================================
// ML TRAINING TYPES
// ============================================================================

/**
 * Single frame annotation with rating
 */
export interface AnnotationItemDto {
  framePath: string;
  rating: number;
  metadata?: Record<string, string>;
}

/**
 * Batch of annotations for upload
 */
export interface AnnotationBatchDto {
  userId: string;
  annotations: AnnotationItemDto[];
  timestamp: Date;
}

/**
 * Statistics about stored annotations
 */
export interface AnnotationStatsDto {
  userId: string;
  totalAnnotations: number;
  averageRating: number;
  oldestAnnotation?: string;
  newestAnnotation?: string;
}

/**
 * Metrics from a training job
 */
export interface TrainingMetricsDto {
  loss: number;
  samples: number;
  duration: string;
  additionalMetrics?: Record<string, number>;
}

/**
 * Status of a training job
 */
export interface TrainingJobStatusDto {
  jobId: string;
  state: 'Queued' | 'Running' | 'Completed' | 'Failed' | 'Cancelled';
  progress: number;
  metrics?: TrainingMetricsDto;
  modelPath?: string;
  error?: string;
  createdAt: string;
  completedAt?: string;
}

/**
 * Request to upload frame annotations
 */
export interface UploadAnnotationsRequest {
  annotations: AnnotationItemDto[];
}

/**
 * Request to start a training job
 */
export interface StartTrainingRequest {
  modelName?: string;
  pipelineConfig?: Record<string, string>;
}

/**
 * Response after starting a training job
 */
export interface StartTrainingResponse {
  jobId: string;
  message: string;
}

// ============================================================================
// GUIDED MODE AND EXPLAIN/ITERATE TYPES
// ============================================================================

/**
 * Request to explain an artifact (script, plan, brief)
 */
export interface ExplainArtifactRequest {
  artifactType: string;
  artifactContent: string;
  specificQuestion?: string | null;
}

/**
 * Response with AI explanation of an artifact
 */
export interface ExplainArtifactResponse {
  success: boolean;
  explanation?: string | null;
  keyPoints?: string[] | null;
  errorMessage?: string | null;
}

/**
 * Request to improve an artifact with specific action
 */
export interface ImproveArtifactRequest {
  artifactType: string;
  artifactContent: string;
  improvementAction: string;
  targetAudience?: string | null;
  lockedSections?: LockedSectionDto[] | null;
}

/**
 * Response with improved artifact
 */
export interface ImproveArtifactResponse {
  success: boolean;
  improvedContent?: string | null;
  changesSummary?: string | null;
  promptDiff?: PromptDiffDto | null;
  errorMessage?: string | null;
}

/**
 * Locked section to preserve during regeneration
 */
export interface LockedSectionDto {
  startIndex: number;
  endIndex: number;
  content: string;
  reason: string;
}

/**
 * Prompt diff preview showing changes
 */
export interface PromptDiffDto {
  originalPrompt: string;
  modifiedPrompt: string;
  intendedOutcome: string;
  changes: PromptChangeDto[];
}

/**
 * Individual prompt change detail
 */
export interface PromptChangeDto {
  type: string;
  description: string;
  oldValue?: string | null;
  newValue?: string | null;
}

/**
 * Request for constrained regeneration
 */
export interface ConstrainedRegenerateRequest {
  artifactType: string;
  currentContent: string;
  regenerationType: string;
  lockedSections?: LockedSectionDto[] | null;
  promptModifiers?: PromptModifiersDto | null;
}

/**
 * Response with regenerated content
 */
export interface ConstrainedRegenerateResponse {
  success: boolean;
  regeneratedContent?: string | null;
  promptDiff?: PromptDiffDto | null;
  requiresConfirmation: boolean;
  errorMessage?: string | null;
}

/**
 * Guided mode configuration
 */
export interface GuidedModeConfigDto {
  enabled: boolean;
  experienceLevel: string;
  showTooltips: boolean;
  showWhyLinks: boolean;
  requirePromptDiffConfirmation: boolean;
}

/**
 * Telemetry for guided mode feature usage
 */
export interface GuidedModeTelemetryDto {
  featureUsed: string;
  artifactType: string;
  durationMs: number;
  success: boolean;
  feedbackRating?: string | null;
  metadata?: Record<string, string> | null;
}

/**
 * Prompt modifiers DTO
 */
export interface PromptModifiersDto {
  additionalInstructions?: string | null;
  exampleStyle?: string | null;
  enableChainOfThought?: boolean;
  promptVersion?: string | null;
}

// ============================================================================
// PROJECT VERSIONING TYPES
// ============================================================================

/**
 * Version type enum
 */
export type VersionType = 'Manual' | 'Autosave' | 'RestorePoint';

/**
 * Request to create a manual snapshot
 */
export interface CreateSnapshotRequest {
  projectId: string;
  name?: string | null;
  description?: string | null;
}

/**
 * Request to restore a version
 */
export interface RestoreVersionRequest {
  projectId: string;
  versionId: string;
}

/**
 * Request to update version metadata
 */
export interface UpdateVersionRequest {
  name?: string | null;
  description?: string | null;
  isMarkedImportant?: boolean | null;
}

/**
 * Version response
 */
export interface VersionResponse {
  id: string;
  projectId: string;
  versionNumber: number;
  name?: string | null;
  description?: string | null;
  versionType: VersionType;
  trigger?: string | null;
  createdAt: string;
  createdByUserId?: string | null;
  storageSizeBytes: number;
  isMarkedImportant: boolean;
}

/**
 * Detailed version response
 */
export interface VersionDetailResponse extends VersionResponse {
  briefJson?: string | null;
  planSpecJson?: string | null;
  voiceSpecJson?: string | null;
  renderSpecJson?: string | null;
  timelineJson?: string | null;
}

/**
 * Version list response
 */
export interface VersionListResponse {
  versions: VersionResponse[];
  totalCount: number;
  totalStorageBytes: number;
}

/**
 * Version comparison response
 */
export interface VersionComparisonResponse {
  version1Id: string;
  version2Id: string;
  version1Number: number;
  version2Number: number;
  briefChanged: boolean;
  planChanged: boolean;
  voiceChanged: boolean;
  renderChanged: boolean;
  timelineChanged: boolean;
  version1Data: VersionDataDto;
  version2Data: VersionDataDto;
}

/**
 * Version data for comparison
 */
export interface VersionDataDto {
  briefJson?: string | null;
  planSpecJson?: string | null;
  voiceSpecJson?: string | null;
  renderSpecJson?: string | null;
  timelineJson?: string | null;
}

/**
 * Storage usage response
 */
export interface StorageUsageResponse {
  totalBytes: number;
  versionCount: number;
  autosaveCount: number;
  manualCount: number;
  restorePointCount: number;
  formattedSize: string;
}

// ============================================================================
// PROVIDER PROFILES
// ============================================================================

/**
 * Provider profile tier
 */
export enum ProfileTier {
  FreeOnly = 'FreeOnly',
  BalancedMix = 'BalancedMix',
  ProMax = 'ProMax',
}

/**
 * Provider profile DTO
 */
export interface ProviderProfileDto {
  id: string;
  name: string;
  description: string;
  tier: string;
  stages: Record<string, string>;
  requiredApiKeys: string[];
  usageNotes: string;
  lastValidatedAt: string | null;
}

/**
 * Profile validation result DTO
 */
export interface ProfileValidationResultDto {
  isValid: boolean;
  message: string;
  errors: string[];
  missingKeys: string[];
  warnings: string[];
}

/**
 * Provider test result DTO
 */
export interface ProviderTestResultDto {
  provider: string;
  success: boolean;
  message: string;
  testedAt: string;
}

/**
 * Request to test a provider API key
 */
export interface TestProviderRequest {
  provider: string;
  apiKey?: string | null;
}

/**
 * Request to save API keys
 */
export interface SaveApiKeysRequest {
  keys: Record<string, string>;
}

/**
 * Request to set active profile
 */
export interface SetActiveProfileRequest {
  profileId: string;
}

/**
 * Provider profile recommendation response
 */
export interface ProfileRecommendationDto {
  recommendedProfileId: string;
  recommendedProfileName: string;
  reason: string;
  availableKeys: string[];
  missingKeysForProMax: string[];
}

// ============================================================================
// TRANSLATION AND SUBTITLE INTEGRATION TYPES
// ============================================================================

/**
 * Voice specification DTO
 */
export interface VoiceSpecDto {
  voiceName: string;
  rate: number;
  pitch: number;
  volume: number;
}

/**
 * SSML Planning Result DTO
 */
export interface SSMLPlanningResultDto {
  segments: SSMLSegmentResultDto[];
  stats: DurationFittingStatsDto;
  warnings: string[];
  planningDurationMs: number;
}

/**
 * SSML Segment Result DTO
 */
export interface SSMLSegmentResultDto {
  sceneIndex: number;
  originalText: string;
  ssmlMarkup: string;
  estimatedDurationMs: number;
  targetDurationMs: number;
  deviationPercent: number;
  adjustments: ProsodyAdjustmentsDto;
  timingMarkers: TimingMarkerDto[];
}

/**
 * Prosody Adjustments DTO
 */
export interface ProsodyAdjustmentsDto {
  rate: number;
  pitch: number;
  volume: number;
  pauses: Record<number, number>;
  emphasis: EmphasisSpanDto[];
  iterations: number;
}

/**
 * Emphasis Span DTO
 */
export interface EmphasisSpanDto {
  startPosition: number;
  length: number;
  level: string;
}

/**
 * Timing Marker DTO
 */
export interface TimingMarkerDto {
  offsetMs: number;
  name: string;
  metadata?: string;
}

/**
 * Duration Fitting Stats DTO
 */
export interface DurationFittingStatsDto {
  segmentsAdjusted: number;
  averageFitIterations: number;
  maxFitIterations: number;
  withinTolerancePercent: number;
  averageDeviation: number;
  maxDeviation: number;
  targetDurationSeconds: number;
  actualDurationSeconds: number;
}

/**
 * Request for translation with SSML planning and subtitle generation
 */
export interface TranslateAndPlanSSMLRequest {
  sourceLanguage: string;
  targetLanguage: string;
  scriptLines: LineDto[];
  targetProvider: string;
  voiceSpec: VoiceSpecDto;
  culturalContext?: CulturalContextDto;
  translationOptions?: TranslationOptionsDto;
  glossary?: Record<string, string>;
  audienceProfileId?: string;
  durationTolerance?: number;
  maxFittingIterations?: number;
  enableAggressiveAdjustments?: boolean;
  subtitleFormat?: string;
}

/**
 * Result of translation with SSML planning
 */
export interface TranslatedSSMLResultDto {
  translation: TranslationResultDto;
  ssmlPlanning: SSMLPlanningResultDto;
  translatedScriptLines: LineDto[];
  subtitles: SubtitleOutputDto;
}

/**
 * Subtitle output
 */
export interface SubtitleOutputDto {
  format: string;
  content: string;
  lineCount: number;
}

/**
 * Request for voice recommendation
 */
export interface VoiceRecommendationRequest {
  targetLanguage: string;
  provider: string;
  preferredGender?: string;
  preferredStyle?: string;
}

/**
 * Voice recommendation result
 */
export interface VoiceRecommendationDto {
  targetLanguage: string;
  provider: string;
  isRTL: boolean;
  recommendedVoices: RecommendedVoiceDto[];
}

/**
 * Recommended voice option
 */
export interface RecommendedVoiceDto {
  voiceName: string;
  gender: string;
  style: string;
  quality: string;
}

/**
 * Font configuration for subtitles
 */
export interface SubtitleFontConfigDto {
  fontFamily: string;
  fontSize: number;
  primaryColor: string;
  outlineColor: string;
  outlineWidth: number;
  alignment: string;
  isRTL: boolean;
}

/**
 * Request to generate subtitles with custom font
 */
export interface GenerateSubtitlesRequest {
  scriptLines: LineDto[];
  format: string;
  fontConfig?: SubtitleFontConfigDto;
}

// ============================================================================
// PROVIDER VALIDATION
// ============================================================================

/**
 * Request to validate OpenAI API key
 */
export interface ValidateOpenAIKeyRequest {
  apiKey: string;
  baseUrl?: string;
  organizationId?: string;
  projectId?: string;
}

/**
 * Response from provider validation with detailed status
 */
export interface ProviderValidationResponse {
  isValid: boolean;
  status: string;
  message?: string;
  correlationId?: string;
  details?: ValidationDetails;
}

/**
 * Detailed validation information
 */
export interface ValidationDetails {
  provider: string;
  keyFormat: string;
  formatValid: boolean;
  networkCheckPassed?: boolean;
  httpStatusCode?: number;
  errorType?: string;
  responseTimeMs?: number;
}

// VERSION INFO
// ============================================================================

/**
 * Application version information
 */
export interface VersionInfo {
  semanticVersion: string;
  buildDate: string;
  informationalVersion: string;
  assemblyVersion: string;
  runtimeVersion: string;
  description: string;
}

// ============================================================================
// PROVIDER CONFIGURATION TYPES
// ============================================================================

/**
 * Provider configuration with API keys, priorities, and cost limits
 */
export interface ProviderConfigDto {
  name: string;
  type: string;
  enabled: boolean;
  priority: number;
  apiKey: string | null;
  additionalSettings: Record<string, string> | null;
  costLimit: number | null;
  status: string | null;
}

/**
 * Request to save provider configuration
 */
export interface SaveProviderConfigRequest {
  providers: ProviderConfigDto[];
}

/**
 * Available model information
 */
export interface AvailableModelDto {
  id: string;
  name: string;
  description: string | null;
  capabilities: string[];
  estimatedCostPer1kTokens: number | null;
  isAvailable: boolean;
  requiredApiKey: string | null;
}

/**
 * Model selection response
 */
export interface ModelSelectionResponse {
  providerName: string;
  providerType: string;
  selectedModel: string | null;
  availableModels: AvailableModelDto[];
}

/**
 * Video quality settings
 */
export interface VideoQualityDto {
  resolution: string;
  width: number;
  height: number;
  framerate: number;
  bitratePreset: string;
  bitrateKbps: number;
  codec: string;
  container: string;
}

/**
 * Audio quality settings
 */
export interface AudioQualityDto {
  bitrate: number;
  sampleRate: number;
  channels: number;
  codec: string;
}

/**
 * Subtitle style configuration
 */
export interface SubtitleStyleDto {
  fontFamily: string;
  fontSize: number;
  fontColor: string;
  backgroundColor: string;
  backgroundOpacity: number;
  position: string;
  outlineWidth: number;
  outlineColor: string;
}

/**
 * Quality configuration for video and audio
 */
export interface QualityConfigDto {
  video: VideoQualityDto;
  audio: AudioQualityDto;
  subtitles: SubtitleStyleDto | null;
}

/**
 * Configuration validation result
 */
export interface ConfigValidationResultDto {
  isValid: boolean;
  issues: ValidationIssueDto[];
  warnings: ValidationIssueDto[];
}

/**
 * Configuration profile
 */
export interface ConfigurationProfileDto {
  id: string;
  name: string;
  description: string;
  providerConfig: SaveProviderConfigRequest;
  qualityConfig: QualityConfigDto;
  created: string;
  lastModified: string;
  isDefault: boolean;
  version: string;
}

/**
 * Request to save a configuration profile
 */
export interface SaveConfigProfileRequest {
  name: string;
  description: string;
  providerConfig: SaveProviderConfigRequest;
  qualityConfig: QualityConfigDto;
}

/**
 * Export/Import configuration container
 */
export interface ConfigurationExportDto {
  version: string;
  exportedAt: string;
  profiles: ConfigurationProfileDto[];
  currentProfile: ConfigurationProfileDto;
}

/**
 * Request to import configuration
 */
export interface ImportConfigurationRequest {
  configuration: ConfigurationExportDto;
  overwriteExisting: boolean;
}

/**
 * Resolution presets
 */
export enum ResolutionPreset {
  SD_480p = 'SD_480p',
  HD_720p = 'HD_720p',
  FullHD_1080p = 'FullHD_1080p',
  QHD_1440p = 'QHD_1440p',
  UHD_4K = 'UHD_4K',
}

/**
 * Bitrate presets
 */
export enum BitratePreset {
  Low = 'Low',
  Medium = 'Medium',
  High = 'High',
  VeryHigh = 'VeryHigh',
  Custom = 'Custom',
}
