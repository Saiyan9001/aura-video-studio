/**
 * Fallback language list for offline use
 * Used when backend is unavailable or language loading fails
 * Subset of most common languages from LanguageRegistry
 */

import type { LanguageInfoDto } from '../types/api-v1';

/**
 * Common languages for fallback (top 30 most used globally)
 */
export const FALLBACK_LANGUAGES: LanguageInfoDto[] = [
  // English
  {
    code: 'en',
    name: 'English',
    nativeName: 'English',
    region: 'Global',
    isRightToLeft: false,
    defaultFormality: 'Neutral',
    typicalExpansionFactor: 1.0,
  },
  {
    code: 'en-US',
    name: 'English (US)',
    nativeName: 'English (United States)',
    region: 'North America',
    isRightToLeft: false,
    defaultFormality: 'Neutral',
    typicalExpansionFactor: 1.0,
  },
  {
    code: 'en-GB',
    name: 'English (UK)',
    nativeName: 'English (United Kingdom)',
    region: 'Europe',
    isRightToLeft: false,
    defaultFormality: 'Neutral',
    typicalExpansionFactor: 1.0,
  },

  // Spanish
  {
    code: 'es',
    name: 'Spanish',
    nativeName: 'Español',
    region: 'Global',
    isRightToLeft: false,
    defaultFormality: 'Neutral',
    typicalExpansionFactor: 1.15,
  },
  {
    code: 'es-ES',
    name: 'Spanish (Spain)',
    nativeName: 'Español (España)',
    region: 'Europe',
    isRightToLeft: false,
    defaultFormality: 'Formal',
    typicalExpansionFactor: 1.15,
  },
  {
    code: 'es-MX',
    name: 'Spanish (Mexico)',
    nativeName: 'Español (México)',
    region: 'Latin America',
    isRightToLeft: false,
    defaultFormality: 'Neutral',
    typicalExpansionFactor: 1.15,
  },

  // French
  {
    code: 'fr',
    name: 'French',
    nativeName: 'Français',
    region: 'Global',
    isRightToLeft: false,
    defaultFormality: 'Formal',
    typicalExpansionFactor: 1.15,
  },
  {
    code: 'fr-FR',
    name: 'French (France)',
    nativeName: 'Français (France)',
    region: 'Europe',
    isRightToLeft: false,
    defaultFormality: 'Formal',
    typicalExpansionFactor: 1.15,
  },
  {
    code: 'fr-CA',
    name: 'French (Canada)',
    nativeName: 'Français (Canada)',
    region: 'North America',
    isRightToLeft: false,
    defaultFormality: 'Neutral',
    typicalExpansionFactor: 1.15,
  },

  // German
  {
    code: 'de',
    name: 'German',
    nativeName: 'Deutsch',
    region: 'Europe',
    isRightToLeft: false,
    defaultFormality: 'Formal',
    typicalExpansionFactor: 1.3,
  },

  // Italian
  {
    code: 'it',
    name: 'Italian',
    nativeName: 'Italiano',
    region: 'Europe',
    isRightToLeft: false,
    defaultFormality: 'Neutral',
    typicalExpansionFactor: 1.2,
  },

  // Portuguese
  {
    code: 'pt',
    name: 'Portuguese',
    nativeName: 'Português',
    region: 'Global',
    isRightToLeft: false,
    defaultFormality: 'Neutral',
    typicalExpansionFactor: 1.15,
  },
  {
    code: 'pt-BR',
    name: 'Portuguese (Brazil)',
    nativeName: 'Português (Brasil)',
    region: 'Latin America',
    isRightToLeft: false,
    defaultFormality: 'Informal',
    typicalExpansionFactor: 1.15,
  },
  {
    code: 'pt-PT',
    name: 'Portuguese (Portugal)',
    nativeName: 'Português (Portugal)',
    region: 'Europe',
    isRightToLeft: false,
    defaultFormality: 'Formal',
    typicalExpansionFactor: 1.15,
  },

  // Russian
  {
    code: 'ru',
    name: 'Russian',
    nativeName: 'Русский',
    region: 'Eastern Europe',
    isRightToLeft: false,
    defaultFormality: 'Formal',
    typicalExpansionFactor: 1.15,
  },

  // Chinese
  {
    code: 'zh',
    name: 'Chinese (Simplified)',
    nativeName: '简体中文',
    region: 'Asia',
    isRightToLeft: false,
    defaultFormality: 'Formal',
    typicalExpansionFactor: 0.7,
  },
  {
    code: 'zh-CN',
    name: 'Chinese (Mainland)',
    nativeName: '简体中文 (中国)',
    region: 'Asia',
    isRightToLeft: false,
    defaultFormality: 'Formal',
    typicalExpansionFactor: 0.7,
  },
  {
    code: 'zh-TW',
    name: 'Chinese (Traditional)',
    nativeName: '繁體中文 (台灣)',
    region: 'Asia',
    isRightToLeft: false,
    defaultFormality: 'Formal',
    typicalExpansionFactor: 0.7,
  },

  // Japanese
  {
    code: 'ja',
    name: 'Japanese',
    nativeName: '日本語',
    region: 'Asia',
    isRightToLeft: false,
    defaultFormality: 'VeryFormal',
    typicalExpansionFactor: 0.8,
  },

  // Korean
  {
    code: 'ko',
    name: 'Korean',
    nativeName: '한국어',
    region: 'Asia',
    isRightToLeft: false,
    defaultFormality: 'VeryFormal',
    typicalExpansionFactor: 0.9,
  },

  // Arabic
  {
    code: 'ar',
    name: 'Arabic',
    nativeName: 'العربية',
    region: 'Middle East',
    isRightToLeft: true,
    defaultFormality: 'VeryFormal',
    typicalExpansionFactor: 1.25,
  },

  // Hindi
  {
    code: 'hi',
    name: 'Hindi',
    nativeName: 'हिन्दी',
    region: 'Asia',
    isRightToLeft: false,
    defaultFormality: 'Formal',
    typicalExpansionFactor: 1.15,
  },

  // Turkish
  {
    code: 'tr',
    name: 'Turkish',
    nativeName: 'Türkçe',
    region: 'Middle East',
    isRightToLeft: false,
    defaultFormality: 'Formal',
    typicalExpansionFactor: 1.15,
  },

  // Dutch
  {
    code: 'nl',
    name: 'Dutch',
    nativeName: 'Nederlands',
    region: 'Europe',
    isRightToLeft: false,
    defaultFormality: 'Neutral',
    typicalExpansionFactor: 1.1,
  },

  // Polish
  {
    code: 'pl',
    name: 'Polish',
    nativeName: 'Polski',
    region: 'Europe',
    isRightToLeft: false,
    defaultFormality: 'Formal',
    typicalExpansionFactor: 1.2,
  },

  // Swedish
  {
    code: 'sv',
    name: 'Swedish',
    nativeName: 'Svenska',
    region: 'Europe',
    isRightToLeft: false,
    defaultFormality: 'Neutral',
    typicalExpansionFactor: 1.1,
  },

  // Indonesian
  {
    code: 'id',
    name: 'Indonesian',
    nativeName: 'Bahasa Indonesia',
    region: 'Asia',
    isRightToLeft: false,
    defaultFormality: 'Neutral',
    typicalExpansionFactor: 1.1,
  },

  // Thai
  {
    code: 'th',
    name: 'Thai',
    nativeName: 'ไทย',
    region: 'Asia',
    isRightToLeft: false,
    defaultFormality: 'Formal',
    typicalExpansionFactor: 1.0,
  },

  // Vietnamese
  {
    code: 'vi',
    name: 'Vietnamese',
    nativeName: 'Tiếng Việt',
    region: 'Asia',
    isRightToLeft: false,
    defaultFormality: 'Formal',
    typicalExpansionFactor: 1.0,
  },

  // Greek
  {
    code: 'el',
    name: 'Greek',
    nativeName: 'Ελληνικά',
    region: 'Europe',
    isRightToLeft: false,
    defaultFormality: 'Neutral',
    typicalExpansionFactor: 1.2,
  },
];

/**
 * Get cached languages from localStorage
 */
export function getCachedLanguages(): LanguageInfoDto[] | null {
  try {
    const cached = localStorage.getItem('aura_cached_languages');
    if (cached) {
      const parsed = JSON.parse(cached);
      if (Array.isArray(parsed) && parsed.length > 0) {
        return parsed;
      }
    }
  } catch (error) {
    console.warn('Failed to load cached languages:', error);
  }
  return null;
}

/**
 * Cache languages in localStorage
 */
export function cacheLanguages(languages: LanguageInfoDto[]): void {
  try {
    localStorage.setItem('aura_cached_languages', JSON.stringify(languages));
  } catch (error) {
    console.warn('Failed to cache languages:', error);
  }
}
