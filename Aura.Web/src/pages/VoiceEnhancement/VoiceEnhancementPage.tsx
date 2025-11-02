import {
  Button,
  Card,
  Text,
  Title1,
  Title2,
  Title3,
  Spinner,
  makeStyles,
  tokens,
  Tab,
  TabList,
  Field,
  Textarea,
  Slider,
  Dropdown,
  Option,
  Checkbox,
} from '@fluentui/react-components';
import {
  Speaker224Regular,
  DataArea24Regular,
  MusicNote224Regular,
  MicSparkle24Regular,
} from '@fluentui/react-icons';
import { useState, useCallback } from 'react';
import { PathSelector } from '../../components/common/PathSelector';
import { ErrorState } from '../../components/Loading';

const useStyles = makeStyles({
  container: {
    padding: tokens.spacingVerticalXXL,
    maxWidth: '1400px',
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
  content: {
    display: 'flex',
    flexDirection: 'column',
    gap: tokens.spacingVerticalL,
  },
  toolCard: {
    padding: tokens.spacingVerticalXL,
  },
  toolHeader: {
    display: 'flex',
    alignItems: 'center',
    gap: tokens.spacingHorizontalM,
    marginBottom: tokens.spacingVerticalL,
  },
  toolIcon: {
    fontSize: '24px',
    color: tokens.colorBrandForeground1,
  },
  form: {
    display: 'flex',
    flexDirection: 'column',
    gap: tokens.spacingVerticalL,
    maxWidth: '600px',
  },
  actions: {
    display: 'flex',
    gap: tokens.spacingHorizontalM,
  },
  resultsSection: {
    marginTop: tokens.spacingVerticalL,
    padding: tokens.spacingVerticalL,
    backgroundColor: tokens.colorNeutralBackground2,
    borderRadius: tokens.borderRadiusMedium,
  },
});

type TabValue = 'enhance' | 'analyze' | 'emotion' | 'batch';

interface EnhancementResult {
  outputPath: string;
  processingTimeMs: number;
  qualityMetrics?: Record<string, unknown>;
  messages?: string[];
}

interface QualityMetrics {
  snr?: number;
  clarity?: number;
  loudness?: number;
}

interface EmotionResult {
  emotion: string;
  confidence: number;
  features?: Record<string, unknown>;
}

const VoiceEnhancementPage: React.FC = () => {
  const styles = useStyles();
  const [selectedTab, setSelectedTab] = useState<TabValue>('enhance');
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const [inputPath, setInputPath] = useState('');
  const [noiseReduction, setNoiseReduction] = useState(true);
  const [noiseStrength, setNoiseStrength] = useState(0.7);
  const [equalization, setEqualization] = useState(true);
  const [eqPreset, setEqPreset] = useState('Balanced');
  const [enhancementResult, setEnhancementResult] = useState<EnhancementResult | null>(null);

  const [analyzeInputPath, setAnalyzeInputPath] = useState('');
  const [qualityMetrics, setQualityMetrics] = useState<QualityMetrics | null>(null);

  const [emotionAudioPath, setEmotionAudioPath] = useState('');
  const [emotionResult, setEmotionResult] = useState<EmotionResult | null>(null);

  const [batchPaths, setBatchPaths] = useState('');
  const [batchResults, setBatchResults] = useState<EnhancementResult[]>([]);

  const handleEnhance = useCallback(async () => {
    setLoading(true);
    setError(null);
    setEnhancementResult(null);

    try {
      const response = await fetch('/api/voice-enhancement/enhance', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({
          inputPath,
          enableNoiseReduction: noiseReduction,
          noiseReductionStrength: noiseStrength,
          enableEqualization: equalization,
          equalizationPreset: eqPreset,
        }),
      });

      if (!response.ok) {
        const errorData = await response.json();
        throw new Error(errorData.error || 'Enhancement failed');
      }

      const data = await response.json();
      setEnhancementResult(data);
    } catch (err: unknown) {
      const errorMessage = err instanceof Error ? err.message : 'An error occurred';
      setError(errorMessage);
    } finally {
      setLoading(false);
    }
  }, [inputPath, noiseReduction, noiseStrength, equalization, eqPreset]);

  const handleAnalyze = useCallback(async () => {
    setLoading(true);
    setError(null);
    setQualityMetrics(null);

    try {
      const response = await fetch('/api/voice-enhancement/analyze-quality', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ inputPath: analyzeInputPath }),
      });

      if (!response.ok) {
        const errorData = await response.json();
        throw new Error(errorData.error || 'Analysis failed');
      }

      const data = await response.json();
      setQualityMetrics(data.metrics);
    } catch (err: unknown) {
      const errorMessage = err instanceof Error ? err.message : 'An error occurred';
      setError(errorMessage);
    } finally {
      setLoading(false);
    }
  }, [analyzeInputPath]);

  const handleDetectEmotion = useCallback(async () => {
    setLoading(true);
    setError(null);
    setEmotionResult(null);

    try {
      const response = await fetch('/api/voice-enhancement/detect-emotion', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ audioPath: emotionAudioPath }),
      });

      if (!response.ok) {
        const errorData = await response.json();
        throw new Error(errorData.error || 'Emotion detection failed');
      }

      const data = await response.json();
      setEmotionResult(data);
    } catch (err: unknown) {
      const errorMessage = err instanceof Error ? err.message : 'An error occurred';
      setError(errorMessage);
    } finally {
      setLoading(false);
    }
  }, [emotionAudioPath]);

  const handleBatchEnhance = useCallback(async () => {
    setLoading(true);
    setError(null);
    setBatchResults([]);

    try {
      const paths = batchPaths.split('\n').filter((p) => p.trim());
      const response = await fetch('/api/voice-enhancement/batch-enhance', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({
          inputPaths: paths,
          enableNoiseReduction: noiseReduction,
          noiseReductionStrength: noiseStrength,
          enableEqualization: equalization,
          equalizationPreset: eqPreset,
        }),
      });

      if (!response.ok) {
        const errorData = await response.json();
        throw new Error(errorData.error || 'Batch enhancement failed');
      }

      const data = await response.json();
      setBatchResults(data.results || []);
    } catch (err: unknown) {
      const errorMessage = err instanceof Error ? err.message : 'An error occurred';
      setError(errorMessage);
    } finally {
      setLoading(false);
    }
  }, [batchPaths, noiseReduction, noiseStrength, equalization, eqPreset]);

  return (
    <div className={styles.container}>
      <div className={styles.header}>
        <Speaker224Regular className={styles.headerIcon} />
        <div>
          <Title1>Voice Enhancement</Title1>
          <Text className={styles.subtitle}>
            Professional audio processing tools for voice enhancement, quality analysis, and emotion
            detection
          </Text>
        </div>
      </div>

      <TabList
        selectedValue={selectedTab}
        onTabSelect={(_, data) => setSelectedTab(data.value as TabValue)}
        className={styles.tabs}
      >
        <Tab value="enhance" icon={<MicSparkle24Regular />}>
          Enhance Voice
        </Tab>
        <Tab value="analyze" icon={<DataArea24Regular />}>
          Analyze Quality
        </Tab>
        <Tab value="emotion" icon={<Speaker224Regular />}>
          Detect Emotion
        </Tab>
        <Tab value="batch" icon={<MusicNote224Regular />}>
          Batch Processing
        </Tab>
      </TabList>

      <div className={styles.content}>
        {error && <ErrorState message={error} />}

        {selectedTab === 'enhance' && (
          <Card className={styles.toolCard}>
            <div className={styles.toolHeader}>
              <MicSparkle24Regular className={styles.toolIcon} />
              <Title2>Voice Enhancement</Title2>
            </div>
            <div className={styles.form}>
              <PathSelector
                label="Audio File Path"
                placeholder="Select audio file to enhance"
                value={inputPath}
                onChange={setInputPath}
                type="file"
                accept=".wav,.mp3,.m4a,.flac,.ogg"
                helpText="Select the audio file you want to enhance (WAV, MP3, M4A, FLAC, or OGG format)"
                showClearButton={true}
                showOpenButton={false}
              />
              <Field>
                <Checkbox
                  label="Enable Noise Reduction"
                  checked={noiseReduction}
                  onChange={(_, data) => setNoiseReduction(data.checked === true)}
                />
              </Field>
              {noiseReduction && (
                <Field label={`Noise Reduction Strength: ${noiseStrength.toFixed(2)}`}>
                  <Slider
                    min={0}
                    max={1}
                    step={0.1}
                    value={noiseStrength}
                    onChange={(_, data) => setNoiseStrength(data.value)}
                  />
                </Field>
              )}
              <Field>
                <Checkbox
                  label="Enable Equalization"
                  checked={equalization}
                  onChange={(_, data) => setEqualization(data.checked === true)}
                />
              </Field>
              {equalization && (
                <Field label="EQ Preset">
                  <Dropdown
                    value={eqPreset}
                    onOptionSelect={(_, data) => setEqPreset(data.optionText || 'Balanced')}
                  >
                    <Option>Balanced</Option>
                    <Option>BassBoost</Option>
                    <Option>TrebleBoost</Option>
                    <Option>Vocal</Option>
                  </Dropdown>
                </Field>
              )}
              <div className={styles.actions}>
                <Button
                  appearance="primary"
                  onClick={handleEnhance}
                  disabled={loading || !inputPath}
                >
                  {loading ? <Spinner size="tiny" /> : 'Enhance Voice'}
                </Button>
              </div>
            </div>
            {enhancementResult && (
              <div className={styles.resultsSection}>
                <Title3>Enhancement Results</Title3>
                <Text>Output Path: {enhancementResult.outputPath}</Text>
                <Text>Processing Time: {enhancementResult.processingTimeMs}ms</Text>
                {enhancementResult.messages && enhancementResult.messages.length > 0 && (
                  <div>
                    <Text>Messages:</Text>
                    {enhancementResult.messages.map((msg, i) => (
                      <Text key={i}>{msg}</Text>
                    ))}
                  </div>
                )}
              </div>
            )}
          </Card>
        )}

        {selectedTab === 'analyze' && (
          <Card className={styles.toolCard}>
            <div className={styles.toolHeader}>
              <DataArea24Regular className={styles.toolIcon} />
              <Title2>Audio Quality Analysis</Title2>
            </div>
            <div className={styles.form}>
              <PathSelector
                label="Audio File Path"
                placeholder="Select audio file to analyze"
                value={analyzeInputPath}
                onChange={setAnalyzeInputPath}
                type="file"
                accept=".wav,.mp3,.m4a,.flac,.ogg"
                helpText="Select the audio file to analyze quality metrics"
                showClearButton={true}
                showOpenButton={false}
              />
              <div className={styles.actions}>
                <Button
                  appearance="primary"
                  onClick={handleAnalyze}
                  disabled={loading || !analyzeInputPath}
                >
                  {loading ? <Spinner size="tiny" /> : 'Analyze Quality'}
                </Button>
              </div>
            </div>
            {qualityMetrics && (
              <div className={styles.resultsSection}>
                <Title3>Quality Metrics</Title3>
                <Text>SNR: {qualityMetrics.snr}</Text>
                <Text>Clarity: {qualityMetrics.clarity}</Text>
                <Text>Loudness: {qualityMetrics.loudness}</Text>
              </div>
            )}
          </Card>
        )}

        {selectedTab === 'emotion' && (
          <Card className={styles.toolCard}>
            <div className={styles.toolHeader}>
              <Speaker224Regular className={styles.toolIcon} />
              <Title2>Emotion Detection</Title2>
            </div>
            <div className={styles.form}>
              <PathSelector
                label="Audio File Path"
                placeholder="Select audio file for emotion detection"
                value={emotionAudioPath}
                onChange={setEmotionAudioPath}
                type="file"
                accept=".wav,.mp3,.m4a,.flac,.ogg"
                helpText="Select the audio file to detect emotion from voice"
                showClearButton={true}
                showOpenButton={false}
              />
              <div className={styles.actions}>
                <Button
                  appearance="primary"
                  onClick={handleDetectEmotion}
                  disabled={loading || !emotionAudioPath}
                >
                  {loading ? <Spinner size="tiny" /> : 'Detect Emotion'}
                </Button>
              </div>
            </div>
            {emotionResult && (
              <div className={styles.resultsSection}>
                <Title3>Emotion Detection Result</Title3>
                <Text>Emotion: {emotionResult.emotion}</Text>
                <Text>Confidence: {(emotionResult.confidence * 100).toFixed(1)}%</Text>
              </div>
            )}
          </Card>
        )}

        {selectedTab === 'batch' && (
          <Card className={styles.toolCard}>
            <div className={styles.toolHeader}>
              <MusicNote224Regular className={styles.toolIcon} />
              <Title2>Batch Processing</Title2>
            </div>
            <div className={styles.form}>
              <Field label="Audio File Paths (one per line)" required>
                <Textarea
                  value={batchPaths}
                  onChange={(_, data) => setBatchPaths(data.value)}
                  placeholder="/path/to/audio1.wav&#10;/path/to/audio2.wav"
                  style={{ minHeight: '120px' }}
                />
              </Field>
              <div className={styles.actions}>
                <Button
                  appearance="primary"
                  onClick={handleBatchEnhance}
                  disabled={loading || !batchPaths}
                >
                  {loading ? <Spinner size="tiny" /> : 'Process Batch'}
                </Button>
              </div>
            </div>
            {batchResults.length > 0 && (
              <div className={styles.resultsSection}>
                <Title3>Batch Processing Results</Title3>
                <Text>Processed {batchResults.length} files</Text>
                {batchResults.map((result, i) => (
                  <div key={i}>
                    <Text>
                      File {i + 1}: {result.outputPath} ({result.processingTimeMs}ms)
                    </Text>
                  </div>
                ))}
              </div>
            )}
          </Card>
        )}
      </div>
    </div>
  );
};

export default VoiceEnhancementPage;
