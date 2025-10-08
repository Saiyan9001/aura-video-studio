// Type definitions for Aura Video Studio

export interface HardwareCapabilities {
  tier: string;
  cpu: {
    cores: number;
    threads: number;
  };
  ram: {
    gb: number;
  };
  gpu?: {
    model: string;
    vramGB: number;
    vendor: string;
  };
  enableNVENC: boolean;
  enableSD: boolean;
  offlineOnly: boolean;
}

export interface RenderJob {
  id: string;
  status: string;
  progress: number;
  outputPath: string | null;
  createdAt: string;
}

export interface Profile {
  name: string;
  description: string;
}

export interface DownloadItem {
  name: string;
  version: string;
  url: string;
  sha256: string;
  sizeBytes: number;
  installPath: string;
  required: boolean;
}

export interface Brief {
  topic: string;
  audience: string;
  goal: string;
  tone: string;
  language: string;
  aspect: 'Widescreen16x9' | 'Vertical9x16' | 'Square1x1';
}

export interface PlanSpec {
  targetDurationMinutes: number;
  pacing: 'Chill' | 'Conversational' | 'Fast';
  density: 'Sparse' | 'Balanced' | 'Dense';
  style: string;
}

export interface VoiceSpec {
  voiceName: string;
  rate: number;
  pitch: number;
  pauseStyle: 'Auto' | 'None' | 'Breathier';
}

// Enum normalization utilities for backward compatibility with legacy values

/**
 * Normalize Density enum values, accepting both canonical and legacy aliases.
 * Canonical: 'Sparse', 'Balanced', 'Dense'
 * Legacy alias: 'Normal' -> 'Balanced'
 */
export function normalizeDensity(value: string): 'Sparse' | 'Balanced' | 'Dense' {
  const normalized = value.toLowerCase();
  
  if (normalized === 'normal') {
    console.warn(`Legacy density value "${value}" detected. Use "Balanced" instead. Automatically normalizing to "Balanced".`);
    return 'Balanced';
  }
  
  // Return canonical value (case-insensitive match)
  if (normalized === 'sparse') return 'Sparse';
  if (normalized === 'balanced') return 'Balanced';
  if (normalized === 'dense') return 'Dense';
  
  console.warn(`Unknown density value "${value}". Defaulting to "Balanced".`);
  return 'Balanced';
}

/**
 * Normalize Aspect enum values, accepting both canonical and legacy aliases.
 * Canonical: 'Widescreen16x9', 'Vertical9x16', 'Square1x1'
 * Legacy aliases: '16:9' -> 'Widescreen16x9', '9:16' -> 'Vertical9x16', '1:1' -> 'Square1x1'
 */
export function normalizeAspect(value: string): 'Widescreen16x9' | 'Vertical9x16' | 'Square1x1' {
  const normalized = value.toLowerCase();
  
  // Handle legacy aliases
  if (value === '16:9') {
    console.warn(`Legacy aspect value "${value}" detected. Use "Widescreen16x9" instead. Automatically normalizing to "Widescreen16x9".`);
    return 'Widescreen16x9';
  }
  if (value === '9:16') {
    console.warn(`Legacy aspect value "${value}" detected. Use "Vertical9x16" instead. Automatically normalizing to "Vertical9x16".`);
    return 'Vertical9x16';
  }
  if (value === '1:1') {
    console.warn(`Legacy aspect value "${value}" detected. Use "Square1x1" instead. Automatically normalizing to "Square1x1".`);
    return 'Square1x1';
  }
  
  // Return canonical value (case-insensitive match)
  if (normalized === 'widescreen16x9') return 'Widescreen16x9';
  if (normalized === 'vertical9x16') return 'Vertical9x16';
  if (normalized === 'square1x1') return 'Square1x1';
  
  console.warn(`Unknown aspect value "${value}". Defaulting to "Widescreen16x9".`);
  return 'Widescreen16x9';
}
