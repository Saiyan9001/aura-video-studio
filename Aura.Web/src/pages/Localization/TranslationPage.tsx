/**
 * Translation Page - Complete translation and localization interface
 * Supports 55+ languages with cultural adaptation, quality scoring, and glossary management
 */

import {
  Button,
  Card,
  Text,
  Title1,
  Title2,
  Spinner,
  makeStyles,
  tokens,
  Tab,
  TabList,
  Field,
  Dropdown,
  Option,
  Textarea,
  MessageBar,
  MessageBarBody,
  MessageBarTitle,
} from '@fluentui/react-components';
import {
  LocalLanguage24Regular,
  ArrowSync24Regular,
  Info24Regular,
  DocumentMultiple24Regular,
  Database24Regular,
} from '@fluentui/react-icons';
import { useState, useCallback, useEffect } from 'react';
import { ErrorState } from '../../components/Loading';
import { FALLBACK_LANGUAGES } from '../../constants/fallbackLanguages';
import {
  translateScript,
  batchTranslate,
  getSupportedLanguages,
} from '../../services/api/localizationApi';
import type {
  TranslateScriptRequest,
  TranslationResultDto,
  BatchTranslateRequest,
  LanguageInfoDto,
} from '../../types/api-v1';
import { BatchTranslationQueue } from './components/BatchTranslationQueue';
import { GlossaryManager } from './components/GlossaryManager';
import { TranslationResult } from './components/TranslationResult';

const useStyles = makeStyles({
  container: {
    padding: tokens.spacingVerticalXXL,
    maxWidth: '1600px',
    margin: '0 auto',
  },
  header: {
    marginBottom: tokens.spacingVerticalXL,
    display: 'flex',
    alignItems: 'center',
    gap: tokens.spacingHorizontalM,
  },
  headerIcon: {
    fontSize: '32px',
    color: tokens.colorBrandForeground1,
  },
  subtitle: {
    color: tokens.colorNeutralForeground3,
    marginTop: tokens.spacingVerticalS,
  },
  tabs: {
    marginBottom: tokens.spacingVerticalL,
  },
  toolCard: {
    padding: tokens.spacingVerticalXL,
  },
  form: {
    display: 'flex',
    flexDirection: 'column',
    gap: tokens.spacingVerticalL,
  },
  languageRow: {
    display: 'grid',
    gridTemplateColumns: '1fr 1fr',
    gap: tokens.spacingHorizontalL,
  },
  optionsGrid: {
    display: 'grid',
    gridTemplateColumns: 'repeat(auto-fit, minmax(250px, 1fr))',
    gap: tokens.spacingHorizontalL,
  },
  actions: {
    display: 'flex',
    gap: tokens.spacingHorizontalM,
    marginTop: tokens.spacingVerticalL,
    alignItems: 'center',
  },
  statsBar: {
    display: 'flex',
    gap: tokens.spacingHorizontalXL,
    padding: tokens.spacingVerticalM,
    backgroundColor: tokens.colorNeutralBackground2,
    borderRadius: tokens.borderRadiusMedium,
    marginTop: tokens.spacingVerticalL,
  },
  statItem: {
    display: 'flex',
    flexDirection: 'column',
    gap: tokens.spacingVerticalXS,
  },
  statLabel: {
    fontSize: '12px',
    color: tokens.colorNeutralForeground3,
  },
  statValue: {
    fontSize: '18px',
    fontWeight: tokens.fontWeightSemibold,
  },
  infoText: {
    fontSize: '13px',
    color: tokens.colorNeutralForeground3,
    fontStyle: 'italic',
  },
  warningBanner: {
    marginBottom: tokens.spacingVerticalL,
  },
});

type TabValue = 'translate' | 'batch' | 'glossary';

export const TranslationPage: React.FC = () => {
  const styles = useStyles();
  const [activeTab, setActiveTab] = useState<TabValue>('translate');
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);

  // Form state
  const [sourceText, setSourceText] = useState('');
  const [sourceLanguage, setSourceLanguage] = useState('en');
  const [targetLanguage, setTargetLanguage] = useState('es');
  const [targetLanguages, setTargetLanguages] = useState<string[]>(['es', 'fr']);
  const [translationMode, setTranslationMode] = useState<string>('Localized');
  const [enableBackTranslation, setEnableBackTranslation] = useState(true);
  const [adjustTimings, setAdjustTimings] = useState(true);

  // Results
  const [translationResult, setTranslationResult] = useState<TranslationResultDto | null>(null);
  const [batchResults, setBatchResults] = useState<Record<string, TranslationResultDto>>({});

  // Languages
  const [languages, setLanguages] = useState<LanguageInfoDto[]>([]);
  const [loadingLanguages, setLoadingLanguages] = useState(true);
  const [usingFallbackLanguages, setUsingFallbackLanguages] = useState(false);

  // Load supported languages on mount
  useEffect(() => {
    async function loadLanguages() {
      try {
        const langs = await getSupportedLanguages();
        setLanguages(langs);
        setUsingFallbackLanguages(false);
      } catch (err) {
        console.error('Failed to load languages from backend:', err);

        // Use fallback languages instead of showing error
        console.info('Using fallback language list (30 common languages)');
        setLanguages(FALLBACK_LANGUAGES as never[]);
        setUsingFallbackLanguages(true);
      } finally {
        setLoadingLanguages(false);
      }
    }
    loadLanguages();
  }, []);

  const handleTranslate = useCallback(async () => {
    if (!sourceText) {
      setError('Please enter text to translate');
      return;
    }

    setLoading(true);
    setError(null);
    setTranslationResult(null);

    try {
      const request: TranslateScriptRequest = {
        sourceLanguage,
        targetLanguage,
        sourceText,
        options: {
          mode: translationMode,
          enableBackTranslation,
          enableQualityScoring: true,
          adjustTimings,
          maxTimingVariance: 0.15,
          preserveNames: true,
          preserveBrands: true,
          adaptMeasurements: true,
        },
      };

      const result = await translateScript(request);
      setTranslationResult(result);
    } catch (err) {
      const errorMsg = err instanceof Error ? err.message : 'Translation failed';
      setError(errorMsg);
      console.error('Translation error:', err);
    } finally {
      setLoading(false);
    }
  }, [
    sourceText,
    sourceLanguage,
    targetLanguage,
    translationMode,
    enableBackTranslation,
    adjustTimings,
  ]);

  const handleBatchTranslate = useCallback(async () => {
    if (!sourceText) {
      setError('Please enter text to translate');
      return;
    }

    if (targetLanguages.length === 0) {
      setError('Please select at least one target language');
      return;
    }

    setLoading(true);
    setError(null);
    setBatchResults({});

    try {
      const request: BatchTranslateRequest = {
        sourceLanguage,
        targetLanguages,
        sourceText,
        options: {
          mode: translationMode,
          enableBackTranslation,
          enableQualityScoring: true,
          adjustTimings,
        },
      };

      const result = await batchTranslate(request);
      setBatchResults(result.translations);
    } catch (err) {
      const errorMsg = err instanceof Error ? err.message : 'Batch translation failed';
      setError(errorMsg);
      console.error('Batch translation error:', err);
    } finally {
      setLoading(false);
    }
  }, [
    sourceText,
    sourceLanguage,
    targetLanguages,
    translationMode,
    enableBackTranslation,
    adjustTimings,
  ]);

  return (
    <div className={styles.container}>
      <div className={styles.header}>
        <LocalLanguage24Regular className={styles.headerIcon} />
        <div>
          <Title1>Translation & Localization</Title1>
          <Text className={styles.subtitle}>
            AI-powered translation with cultural adaptation for {languages.length}+ languages
          </Text>
        </div>
      </div>

      <TabList
        className={styles.tabs}
        selectedValue={activeTab}
        onTabSelect={(_, data) => setActiveTab(data.value as TabValue)}
      >
        <Tab value="translate" icon={<ArrowSync24Regular />}>
          Single Translation
        </Tab>
        <Tab value="batch" icon={<DocumentMultiple24Regular />}>
          Batch Translation
        </Tab>
        <Tab value="glossary" icon={<Database24Regular />}>
          Glossary Management
        </Tab>
      </TabList>

      {usingFallbackLanguages && (
        <MessageBar intent="warning" className={styles.warningBanner}>
          <MessageBarBody>
            <MessageBarTitle>Using Offline Language List</MessageBarTitle>
            Unable to connect to backend. Showing 30 common languages. Full feature set
            (translation, cultural adaptation) requires backend connection. Check that the API
            server is running.
          </MessageBarBody>
        </MessageBar>
      )}

      {error && <ErrorState message={error} />}

      {activeTab === 'translate' && (
        <>
          <Card className={styles.toolCard}>
            <Title2>Translate Content</Title2>
            <Text>Translate text with cultural adaptation and quality assurance</Text>

            <div className={styles.form}>
              <div className={styles.languageRow}>
                <Field label="Source Language">
                  <Dropdown
                    value={languages.find((l) => l.code === sourceLanguage)?.name || sourceLanguage}
                    onOptionSelect={(_, data) => setSourceLanguage(data.optionValue as string)}
                    disabled={loadingLanguages}
                  >
                    {languages.map((lang) => (
                      <Option
                        key={lang.code}
                        value={lang.code}
                        text={`${lang.name} (${lang.nativeName})`}
                      >
                        {lang.name} ({lang.nativeName})
                      </Option>
                    ))}
                  </Dropdown>
                </Field>

                <Field label="Target Language">
                  <Dropdown
                    value={languages.find((l) => l.code === targetLanguage)?.name || targetLanguage}
                    onOptionSelect={(_, data) => setTargetLanguage(data.optionValue as string)}
                    disabled={loadingLanguages}
                  >
                    {languages.map((lang) => (
                      <Option
                        key={lang.code}
                        value={lang.code}
                        text={`${lang.name} (${lang.nativeName})${lang.isRightToLeft ? ' [RTL]' : ''}`}
                      >
                        {lang.name} ({lang.nativeName}){lang.isRightToLeft && ' [RTL]'}
                      </Option>
                    ))}
                  </Dropdown>
                </Field>
              </div>

              <Field label="Translation Mode">
                <Dropdown
                  value={translationMode}
                  onOptionSelect={(_, data) => setTranslationMode(data.optionValue as string)}
                >
                  <Option value="Literal">Literal - Direct word-for-word translation</Option>
                  <Option value="Localized">Localized - Cultural adaptation with idioms</Option>
                  <Option value="Transcreation">
                    Transcreation - Creative adaptation preserving emotional impact
                  </Option>
                </Dropdown>
              </Field>

              <Field label="Content to Translate" required>
                <Textarea
                  value={sourceText}
                  onChange={(_, data) => setSourceText(data.value)}
                  placeholder="Enter text or script to translate..."
                  rows={10}
                />
              </Field>

              <div className={styles.optionsGrid}>
                <Field label="Back-Translation QA">
                  <Dropdown
                    value={enableBackTranslation ? 'enabled' : 'disabled'}
                    onOptionSelect={(_, data) =>
                      setEnableBackTranslation(data.optionValue === 'enabled')
                    }
                  >
                    <Option value="enabled">Enabled (recommended)</Option>
                    <Option value="disabled">Disabled (faster)</Option>
                  </Dropdown>
                </Field>

                <Field label="Timing Adjustment">
                  <Dropdown
                    value={adjustTimings ? 'enabled' : 'disabled'}
                    onOptionSelect={(_, data) => setAdjustTimings(data.optionValue === 'enabled')}
                  >
                    <Option value="enabled">Enabled (for video)</Option>
                    <Option value="disabled">Disabled (text only)</Option>
                  </Dropdown>
                </Field>
              </div>

              <Text className={styles.infoText}>
                <Info24Regular /> Translation includes: Quality scoring, cultural adaptation
                analysis, idiom replacement, and visual localization recommendations
              </Text>

              <div className={styles.actions}>
                <Button
                  appearance="primary"
                  icon={loading ? <Spinner size="tiny" /> : <ArrowSync24Regular />}
                  onClick={handleTranslate}
                  disabled={loading || !sourceText || loadingLanguages}
                >
                  {loading ? 'Translating...' : 'Translate'}
                </Button>
              </div>
            </div>
          </Card>

          {translationResult && <TranslationResult result={translationResult} />}
        </>
      )}

      {activeTab === 'batch' && (
        <BatchTranslationQueue
          sourceLanguage={sourceLanguage}
          onSourceLanguageChange={setSourceLanguage}
          targetLanguages={targetLanguages}
          onTargetLanguagesChange={setTargetLanguages}
          sourceText={sourceText}
          onSourceTextChange={setSourceText}
          translationMode={translationMode}
          onTranslationModeChange={setTranslationMode}
          languages={languages}
          loadingLanguages={loadingLanguages}
          onTranslate={handleBatchTranslate}
          loading={loading}
          results={batchResults}
        />
      )}

      {activeTab === 'glossary' && <GlossaryManager />}
    </div>
  );
};

export default TranslationPage;
