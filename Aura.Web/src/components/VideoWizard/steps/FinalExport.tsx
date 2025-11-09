import {
  makeStyles,
  tokens,
  Title2,
  Title3,
  Text,
  Button,
  Card,
  Field,
  Radio,
  RadioGroup,
  Checkbox,
  Dropdown,
  Option,
  ProgressBar,
  Spinner,
  Tooltip,
} from '@fluentui/react-components';
import {
  ArrowDownload24Regular,
  CheckmarkCircle24Regular,
  Dismiss24Regular,
  DocumentMultiple24Regular,
  Info24Regular,
} from '@fluentui/react-icons';
import { useState, useEffect, useCallback, useMemo } from 'react';
import type { FC } from 'react';
import type { ExportData, WizardData, StepValidation } from '../types';

const useStyles = makeStyles({
  container: {
    display: 'flex',
    flexDirection: 'column',
    gap: tokens.spacingVerticalXL,
  },
  header: {
    marginBottom: tokens.spacingVerticalM,
  },
  settingsGrid: {
    display: 'grid',
    gridTemplateColumns: 'repeat(auto-fit, minmax(300px, 1fr))',
    gap: tokens.spacingHorizontalL,
  },
  settingsCard: {
    padding: tokens.spacingVerticalXL,
  },
  batchExportSection: {
    padding: tokens.spacingVerticalL,
    backgroundColor: tokens.colorNeutralBackground2,
    borderRadius: tokens.borderRadiusMedium,
  },
  formatCheckboxes: {
    display: 'flex',
    flexDirection: 'column',
    gap: tokens.spacingVerticalM,
    marginTop: tokens.spacingVerticalM,
  },
  estimateCard: {
    padding: tokens.spacingVerticalL,
    backgroundColor: tokens.colorBrandBackground2,
    borderRadius: tokens.borderRadiusMedium,
    display: 'flex',
    flexDirection: 'column',
    gap: tokens.spacingVerticalS,
  },
  estimateRow: {
    display: 'flex',
    justifyContent: 'space-between',
    alignItems: 'center',
  },
  exportProgress: {
    padding: tokens.spacingVerticalXL,
    backgroundColor: tokens.colorNeutralBackground2,
    borderRadius: tokens.borderRadiusMedium,
    display: 'flex',
    flexDirection: 'column',
    alignItems: 'center',
    gap: tokens.spacingVerticalL,
    textAlign: 'center',
  },
  exportActions: {
    display: 'flex',
    gap: tokens.spacingHorizontalM,
    marginTop: tokens.spacingVerticalL,
  },
  completedSection: {
    padding: tokens.spacingVerticalXXL,
    backgroundColor: tokens.colorBrandBackground2,
    borderRadius: tokens.borderRadiusMedium,
    display: 'flex',
    flexDirection: 'column',
    alignItems: 'center',
    gap: tokens.spacingVerticalL,
    textAlign: 'center',
  },
  downloadList: {
    width: '100%',
    maxWidth: '500px',
    display: 'flex',
    flexDirection: 'column',
    gap: tokens.spacingVerticalM,
  },
  downloadItem: {
    display: 'flex',
    justifyContent: 'space-between',
    alignItems: 'center',
    padding: tokens.spacingVerticalM,
    backgroundColor: tokens.colorNeutralBackground1,
    borderRadius: tokens.borderRadiusMedium,
  },
});

interface FinalExportProps {
  data: ExportData;
  wizardData: WizardData;
  advancedMode: boolean;
  onChange: (data: ExportData) => void;
  onValidationChange: (validation: StepValidation) => void;
}

type ExportStatus = 'idle' | 'exporting' | 'completed' | 'error';

interface ExportResult {
  format: string;
  resolution: string;
  filePath: string;
  fileSize: number;
}

const QUALITY_OPTIONS = [
  { label: 'Draft (480p)', value: 'low', bitrate: 1500 },
  { label: 'Standard (720p)', value: 'medium', bitrate: 2500 },
  { label: 'High (1080p)', value: 'high', bitrate: 5000 },
  { label: 'Ultra (4K)', value: 'ultra', bitrate: 15000 },
];

const RESOLUTION_OPTIONS = [
  { label: '480p (854x480)', value: '480p', width: 854, height: 480 },
  { label: '720p (1280x720)', value: '720p', width: 1280, height: 720 },
  { label: '1080p (1920x1080)', value: '1080p', width: 1920, height: 1080 },
  { label: '4K (3840x2160)', value: '4k', width: 3840, height: 2160 },
];

const FORMAT_OPTIONS = [
  { label: 'MP4 (H.264)', value: 'mp4', extension: '.mp4', description: 'Best compatibility' },
  { label: 'WebM (VP9)', value: 'webm', extension: '.webm', description: 'Web optimized' },
  { label: 'MOV (ProRes)', value: 'mov', extension: '.mov', description: 'Professional editing' },
];

export const FinalExport: FC<FinalExportProps> = ({
  data,
  wizardData,
  advancedMode,
  onChange,
  onValidationChange,
}) => {
  const styles = useStyles();
  const [exportStatus, setExportStatus] = useState<ExportStatus>('idle');
  const [exportProgress, setExportProgress] = useState(0);
  const [exportStage, setExportStage] = useState('');
  const [batchExport, setBatchExport] = useState(false);
  const [selectedFormats, setSelectedFormats] = useState<string[]>([data.format]);
  const [exportResults, setExportResults] = useState<ExportResult[]>([]);

  useEffect(() => {
    onValidationChange({ isValid: true, errors: [] });
  }, [onValidationChange]);

  const estimatedFileSize = useMemo(() => {
    const duration = wizardData.brief.duration;
    const quality = QUALITY_OPTIONS.find((q) => q.value === data.quality);
    const bitrate = quality?.bitrate || 2500;

    const sizeKB = (bitrate * duration) / 8;
    return sizeKB / 1024;
  }, [data.quality, wizardData.brief.duration]);

  const estimatedDiskSpace = useMemo(() => {
    const formats = batchExport ? selectedFormats : [data.format];
    const baseSize = estimatedFileSize;

    return formats.reduce((total, format) => {
      const multiplier = format === 'mov' ? 3 : format === 'webm' ? 0.7 : 1;
      return total + baseSize * multiplier;
    }, 0);
  }, [batchExport, selectedFormats, data.format, estimatedFileSize]);

  const handleQualityChange = useCallback(
    (quality: string) => {
      const resolution =
        quality === 'low'
          ? '480p'
          : quality === 'medium'
            ? '720p'
            : quality === 'high'
              ? '1080p'
              : '4k';
      onChange({ ...data, quality: quality as ExportData['quality'], resolution });
    },
    [data, onChange]
  );

  const handleFormatToggle = useCallback((format: string, checked: boolean) => {
    if (checked) {
      setSelectedFormats((prev) => [...prev, format]);
    } else {
      setSelectedFormats((prev) => prev.filter((f) => f !== format));
    }
  }, []);

  const startExport = useCallback(async () => {
    setExportStatus('exporting');
    setExportProgress(0);
    setExportResults([]);

    try {
      const formatsToExport = batchExport ? selectedFormats : [data.format];

      for (let i = 0; i < formatsToExport.length; i++) {
        const format = formatsToExport[i];
        setExportStage(
          `Exporting ${format.toUpperCase()} format (${i + 1} of ${formatsToExport.length})...`
        );

        for (let progress = 0; progress <= 100; progress += 5) {
          await new Promise((resolve) => setTimeout(resolve, 100));
          setExportProgress((i * 100 + progress) / formatsToExport.length);
        }

        const fileSize = estimatedFileSize * (format === 'mov' ? 3 : format === 'webm' ? 0.7 : 1);

        exportResults.push({
          format: format.toUpperCase(),
          resolution: data.resolution,
          filePath: `/exports/video-${Date.now()}.${format}`,
          fileSize: Math.round(fileSize * 1024),
        });

        setExportResults([...exportResults]);
      }

      setExportProgress(100);
      setExportStatus('completed');
      setExportStage('Export completed successfully!');
    } catch (error) {
      console.error('Export failed:', error);
      setExportStatus('error');
      setExportStage('Export failed. Please try again.');
    }
  }, [
    batchExport,
    selectedFormats,
    data.format,
    data.resolution,
    estimatedFileSize,
    exportResults,
  ]);

  const cancelExport = useCallback(() => {
    setExportStatus('idle');
    setExportProgress(0);
    setExportStage('');
  }, []);

  const downloadFile = useCallback((filePath: string) => {
    console.log('Downloading file:', filePath);
    window.open(filePath, '_blank');
  }, []);

  const renderSettingsView = () => (
    <div className={styles.container}>
      <div className={styles.settingsGrid}>
        <Card className={styles.settingsCard}>
          <Field label="Quality Preset">
            <RadioGroup
              value={data.quality}
              onChange={(_, { value }) => handleQualityChange(value)}
            >
              {QUALITY_OPTIONS.map((option) => (
                <Radio
                  key={option.value}
                  value={option.value}
                  label={
                    <div>
                      <Text weight="semibold">{option.label}</Text>
                      <Text size={200} style={{ color: tokens.colorNeutralForeground3 }}>
                        ~{option.bitrate} kbps
                      </Text>
                    </div>
                  }
                />
              ))}
            </RadioGroup>
          </Field>
        </Card>

        <Card className={styles.settingsCard}>
          <Field label="Resolution">
            <Dropdown
              value={data.resolution}
              selectedOptions={[data.resolution]}
              onOptionSelect={(_, { optionValue }) => {
                if (optionValue) {
                  onChange({ ...data, resolution: optionValue as ExportData['resolution'] });
                }
              }}
            >
              {RESOLUTION_OPTIONS.map((option) => (
                <Option key={option.value} value={option.value}>
                  {option.label}
                </Option>
              ))}
            </Dropdown>
          </Field>

          <Field label="Output Format" style={{ marginTop: tokens.spacingVerticalL }}>
            <Dropdown
              value={data.format}
              selectedOptions={[data.format]}
              onOptionSelect={(_, { optionValue }) => {
                if (optionValue) {
                  onChange({ ...data, format: optionValue as ExportData['format'] });
                  setSelectedFormats([optionValue]);
                }
              }}
              disabled={batchExport}
            >
              {FORMAT_OPTIONS.map((option) => (
                <Option key={option.value} value={option.value}>
                  {option.label}
                </Option>
              ))}
            </Dropdown>
          </Field>
        </Card>
      </div>

      <Card className={styles.settingsCard}>
        <Checkbox
          label="Include captions/subtitles"
          checked={data.includeCaptions}
          onChange={(_, { checked }) => onChange({ ...data, includeCaptions: !!checked })}
        />
      </Card>

      {advancedMode && (
        <div className={styles.batchExportSection}>
          <Title3 style={{ marginBottom: tokens.spacingVerticalM }}>Batch Export</Title3>
          <Checkbox
            label="Export to multiple formats"
            checked={batchExport}
            onChange={(_, { checked }) => setBatchExport(!!checked)}
          />

          {batchExport && (
            <div className={styles.formatCheckboxes}>
              {FORMAT_OPTIONS.map((option) => (
                <div key={option.value}>
                  <Checkbox
                    label={option.label}
                    checked={selectedFormats.includes(option.value)}
                    onChange={(_, { checked }) => handleFormatToggle(option.value, !!checked)}
                  />
                  <Text size={200} style={{ marginLeft: tokens.spacingHorizontalXL }}>
                    {option.description}
                  </Text>
                </div>
              ))}
            </div>
          )}
        </div>
      )}

      <div className={styles.estimateCard}>
        <div className={styles.estimateRow}>
          <Text weight="semibold">Estimated File Size:</Text>
          <Text weight="bold">{estimatedFileSize.toFixed(1)} MB</Text>
        </div>

        {batchExport && (
          <div className={styles.estimateRow}>
            <Text weight="semibold">Total Disk Space Required:</Text>
            <Text weight="bold">{estimatedDiskSpace.toFixed(1)} MB</Text>
          </div>
        )}

        <div className={styles.estimateRow}>
          <Text weight="semibold">Estimated Export Time:</Text>
          <Text weight="bold">~{Math.ceil((wizardData.brief.duration / 60) * 2)} minutes</Text>
        </div>

        <Tooltip
          content="Estimates are based on your selected quality and duration settings"
          relationship="label"
        >
          <div style={{ display: 'flex', alignItems: 'center', gap: tokens.spacingHorizontalXS }}>
            <Info24Regular style={{ fontSize: '16px' }} />
            <Text size={200}>These are estimates and may vary</Text>
          </div>
        </Tooltip>
      </div>

      <div className={styles.exportActions}>
        <Button appearance="primary" size="large" onClick={startExport}>
          Start Export
        </Button>
      </div>
    </div>
  );

  const renderExportingView = () => (
    <div className={styles.exportProgress}>
      <Spinner size="extra-large" />
      <Title3>Exporting Video...</Title3>
      <Text>{exportStage}</Text>
      <div style={{ width: '100%', maxWidth: '500px' }}>
        <ProgressBar value={exportProgress / 100} />
        <Text size={200} style={{ marginTop: tokens.spacingVerticalS }}>
          {Math.round(exportProgress)}% complete
        </Text>
      </div>
      <Button appearance="secondary" icon={<Dismiss24Regular />} onClick={cancelExport}>
        Cancel Export
      </Button>
    </div>
  );

  const renderCompletedView = () => (
    <div className={styles.completedSection}>
      <CheckmarkCircle24Regular
        style={{ fontSize: '64px', color: tokens.colorPaletteGreenForeground1 }}
      />
      <Title2>Export Completed!</Title2>
      <Text>Your video has been successfully exported and is ready to download.</Text>

      <div className={styles.downloadList}>
        {exportResults.map((result, index) => (
          <div key={index} className={styles.downloadItem}>
            <div style={{ textAlign: 'left' }}>
              <Text weight="semibold">
                {result.format} - {result.resolution}
              </Text>
              <Text size={200} style={{ color: tokens.colorNeutralForeground3 }}>
                {(result.fileSize / 1024).toFixed(1)} MB
              </Text>
            </div>
            <Button
              appearance="primary"
              icon={<ArrowDownload24Regular />}
              onClick={() => downloadFile(result.filePath)}
            >
              Download
            </Button>
          </div>
        ))}
      </div>

      <div className={styles.exportActions}>
        <Button
          appearance="secondary"
          icon={<DocumentMultiple24Regular />}
          onClick={() => setExportStatus('idle')}
        >
          Export Another Version
        </Button>
      </div>
    </div>
  );

  return (
    <div className={styles.container}>
      <div className={styles.header}>
        <Title2>Final Export</Title2>
        <Text>Configure your video export settings and download the final result.</Text>
      </div>

      {exportStatus === 'idle' && renderSettingsView()}
      {exportStatus === 'exporting' && renderExportingView()}
      {exportStatus === 'completed' && renderCompletedView()}
    </div>
  );
};
