/**
 * Tests for localizationApi service
 */

import MockAdapter from 'axios-mock-adapter';
import { describe, it, expect, beforeEach } from 'vitest';
import type {
  TranslateScriptRequest,
  TranslationResultDto,
  BatchTranslateRequest,
  BatchTranslationResultDto,
  CulturalAnalysisRequest,
  CulturalAnalysisResultDto,
  LanguageInfoDto,
  ProjectGlossaryDto,
  CreateGlossaryRequest,
  AddGlossaryEntryRequest,
} from '../../../types/api-v1';
import apiClient from '../apiClient';
import {
  translateScript,
  batchTranslate,
  getSupportedLanguages,
  analyzeCulturalContent,
  createGlossary,
  listGlossaries,
  getGlossary,
  addGlossaryEntry,
  deleteGlossary,
} from '../localizationApi';

describe('localizationApi', () => {
  let mock: MockAdapter;

  beforeEach(() => {
    mock = new MockAdapter(apiClient);
  });

  afterEach(() => {
    mock.restore();
  });

  describe('translateScript', () => {
    it('should translate script successfully', async () => {
      const request: TranslateScriptRequest = {
        sourceLanguage: 'en',
        targetLanguage: 'es',
        sourceText: 'Hello world',
        options: {
          mode: 'Localized',
          enableBackTranslation: true,
          enableQualityScoring: true,
        },
      };

      const mockResponse: Partial<TranslationResultDto> = {
        sourceLanguage: 'en',
        targetLanguage: 'es',
        sourceText: 'Hello world',
        translatedText: 'Hola mundo',
        translatedLines: [],
        quality: {
          overallScore: 92,
          fluencyScore: 95,
          accuracyScore: 90,
          culturalAppropriatenessScore: 88,
          terminologyConsistencyScore: 100,
          backTranslationScore: 85,
          issues: [],
        },
        culturalAdaptations: [],
        timingAdjustment: {
          originalTotalDuration: 0,
          adjustedTotalDuration: 0,
          expansionFactor: 1.0,
          requiresCompression: false,
          compressionSuggestions: [],
          warnings: [],
        },
        visualRecommendations: [],
        translationTimeSeconds: 2.5,
      };

      mock.onPost('/api/localization/translate').reply(200, mockResponse);

      const result = await translateScript(request);

      expect(result.sourceLanguage).toBe('en');
      expect(result.targetLanguage).toBe('es');
      expect(result.translatedText).toBe('Hola mundo');
      expect(result.quality.overallScore).toBe(92);
    });
  });

  describe('batchTranslate', () => {
    it('should batch translate to multiple languages', async () => {
      const request: BatchTranslateRequest = {
        sourceLanguage: 'en',
        targetLanguages: ['es', 'fr'],
        sourceText: 'Hello world',
      };

      const mockResponse: Partial<BatchTranslationResultDto> = {
        sourceLanguage: 'en',
        translations: {},
        successfulLanguages: ['es', 'fr'],
        failedLanguages: [],
        totalTimeSeconds: 5.0,
      };

      mock.onPost('/api/localization/translate/batch').reply(200, mockResponse);

      const result = await batchTranslate(request);

      expect(result.sourceLanguage).toBe('en');
      expect(result.successfulLanguages).toContain('es');
      expect(result.successfulLanguages).toContain('fr');
      expect(result.totalTimeSeconds).toBe(5.0);
    });
  });

  describe('getSupportedLanguages', () => {
    it('should retrieve list of supported languages', async () => {
      const mockLanguages: LanguageInfoDto[] = [
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
          code: 'es',
          name: 'Spanish',
          nativeName: 'Español',
          region: 'Global',
          isRightToLeft: false,
          defaultFormality: 'Formal',
          typicalExpansionFactor: 1.15,
        },
      ];

      mock.onGet('/api/localization/languages').reply(200, mockLanguages);

      const result = await getSupportedLanguages();

      expect(result).toHaveLength(2);
      expect(result[0].code).toBe('en');
      expect(result[1].code).toBe('es');
    });
  });

  describe('analyzeCulturalContent', () => {
    it('should analyze cultural appropriateness', async () => {
      const request: CulturalAnalysisRequest = {
        targetLanguage: 'es',
        targetRegion: 'MX',
        content: 'Test content',
      };

      const mockResponse: Partial<CulturalAnalysisResultDto> = {
        targetLanguage: 'es',
        targetRegion: 'MX',
        culturalSensitivityScore: 85,
        issues: [],
        recommendations: [],
      };

      mock.onPost('/api/localization/analyze-culture').reply(200, mockResponse);

      const result = await analyzeCulturalContent(request);

      expect(result.targetLanguage).toBe('es');
      expect(result.culturalSensitivityScore).toBe(85);
    });
  });

  describe('glossary management', () => {
    it('should create glossary', async () => {
      const request: CreateGlossaryRequest = {
        name: 'Test Glossary',
        description: 'Test description',
      };

      const mockResponse: Partial<ProjectGlossaryDto> = {
        id: 'glossary-1',
        name: 'Test Glossary',
        description: 'Test description',
        entries: [],
        createdAt: new Date().toISOString(),
        updatedAt: new Date().toISOString(),
      };

      mock.onPost('/api/localization/glossary').reply(201, mockResponse);

      const result = await createGlossary(request);

      expect(result.id).toBe('glossary-1');
      expect(result.name).toBe('Test Glossary');
    });

    it('should list glossaries', async () => {
      const mockGlossaries: Partial<ProjectGlossaryDto>[] = [
        {
          id: 'glossary-1',
          name: 'Glossary 1',
          entries: [],
          createdAt: new Date().toISOString(),
          updatedAt: new Date().toISOString(),
        },
      ];

      mock.onGet('/api/localization/glossary').reply(200, mockGlossaries);

      const result = await listGlossaries();

      expect(result).toHaveLength(1);
      expect(result[0].id).toBe('glossary-1');
    });

    it('should get glossary by ID', async () => {
      const glossaryId = 'glossary-1';
      const mockGlossary: Partial<ProjectGlossaryDto> = {
        id: glossaryId,
        name: 'Test Glossary',
        entries: [],
        createdAt: new Date().toISOString(),
        updatedAt: new Date().toISOString(),
      };

      mock.onGet(`/api/localization/glossary/${glossaryId}`).reply(200, mockGlossary);

      const result = await getGlossary(glossaryId);

      expect(result.id).toBe(glossaryId);
      expect(result.name).toBe('Test Glossary');
    });

    it('should add entry to glossary', async () => {
      const glossaryId = 'glossary-1';
      const request: AddGlossaryEntryRequest = {
        term: 'test term',
        translations: { es: 'término de prueba' },
      };

      const mockEntry = {
        id: 'entry-1',
        term: 'test term',
        translations: { es: 'término de prueba' },
      };

      mock.onPost(`/api/localization/glossary/${glossaryId}/entries`).reply(201, mockEntry);

      const result = await addGlossaryEntry(glossaryId, request);

      expect(result.id).toBe('entry-1');
      expect(result.term).toBe('test term');
    });

    it('should delete glossary', async () => {
      const glossaryId = 'glossary-1';

      mock.onDelete(`/api/localization/glossary/${glossaryId}`).reply(204);

      await expect(deleteGlossary(glossaryId)).resolves.toBeUndefined();
    });
  });

  describe('error handling', () => {
    it('should handle translation API errors', async () => {
      const request: TranslateScriptRequest = {
        sourceLanguage: 'en',
        targetLanguage: 'invalid',
        sourceText: 'Test',
      };

      mock.onPost('/api/localization/translate').reply(400, {
        title: 'Invalid Request',
        detail: 'Invalid language code',
      });

      await expect(translateScript(request)).rejects.toThrow();
    });

    it('should use cached/fallback languages on network error', async () => {
      // Clear any existing cache
      localStorage.removeItem('aura_cached_languages');

      mock.onGet('/api/localization/languages').networkError();

      // Should still throw (will be caught by TranslationPage which uses fallback)
      await expect(getSupportedLanguages()).rejects.toThrow();
    }, 15000); // Increase timeout to allow for retries

    it('should cache languages on successful load', async () => {
      const mockLanguages: LanguageInfoDto[] = [
        {
          code: 'en',
          name: 'English',
          nativeName: 'English',
          region: 'Global',
          isRightToLeft: false,
          defaultFormality: 'Neutral',
          typicalExpansionFactor: 1.0,
        },
      ];

      mock.onGet('/api/localization/languages').reply(200, mockLanguages);

      await getSupportedLanguages();

      // Check that languages were cached
      const cached = localStorage.getItem('aura_cached_languages');
      expect(cached).toBeTruthy();
      if (cached) {
        const parsedCache = JSON.parse(cached);
        expect(parsedCache).toHaveLength(1);
        expect(parsedCache[0].code).toBe('en');
      }
    });
  });
});
