import {
  makeStyles,
  tokens,
  Title2,
  Title3,
  Text,
  Button,
  Card,
  Spinner,
  ProgressBar,
  Badge,
  Tooltip,
  Dropdown,
  Option,
  Field,
  Slider,
  Dialog,
  DialogSurface,
  DialogTitle,
  DialogBody,
  DialogContent,
  DialogActions,
  Menu,
  MenuItem,
  MenuList,
  MenuPopover,
  MenuTrigger,
} from '@fluentui/react-components';
import {
  Play24Regular,
  ArrowClockwise24Regular,
  Warning24Regular,
  Image24Regular,
  Speaker224Regular,
  Settings24Regular,
  CheckmarkCircle24Regular,
  ErrorCircle24Regular,
  ArrowUpload24Regular,
  MoreHorizontal24Regular,
  ImageEdit24Regular,
  Search24Regular,
} from '@fluentui/react-icons';
import { useState, useEffect, useCallback, useMemo } from 'react';
import type { FC } from 'react';
import type {
  PreviewData,
  ScriptData,
  StyleData,
  StepValidation,
  ScriptScene,
  PreviewThumbnail,
} from '../types';
import { getVisualsClient } from '@/api/visualsClient';
import type { VisualProvider, BatchGenerateProgress } from '@/api/visualsClient';

const useStyles = makeStyles({
  container: {
    display: 'flex',
    flexDirection: 'column',
    gap: tokens.spacingVerticalXL,
  },
  header: {
    marginBottom: tokens.spacingVerticalM,
  },
  generationCard: {
    padding: tokens.spacingVerticalXL,
    backgroundColor: tokens.colorNeutralBackground2,
    borderRadius: tokens.borderRadiusMedium,
    display: 'flex',
    flexDirection: 'column',
    alignItems: 'center',
    gap: tokens.spacingVerticalL,
    textAlign: 'center',
  },
  previewGrid: {
    display: 'grid',
    gridTemplateColumns: 'repeat(auto-fill, minmax(280px, 1fr))',
    gap: tokens.spacingHorizontalL,
    marginTop: tokens.spacingVerticalL,
  },
  sceneCard: {
    padding: tokens.spacingVerticalL,
    cursor: 'pointer',
    transition: 'all 0.2s ease',
    ':hover': {
      transform: 'translateY(-2px)',
      boxShadow: tokens.shadow8,
    },
  },
  scenePreview: {
    width: '100%',
    height: '160px',
    backgroundColor: tokens.colorNeutralBackground3,
    borderRadius: tokens.borderRadiusMedium,
    display: 'flex',
    alignItems: 'center',
    justifyContent: 'center',
    marginBottom: tokens.spacingVerticalM,
    position: 'relative',
    overflow: 'hidden',
  },
  sceneImage: {
    width: '100%',
    height: '100%',
    objectFit: 'cover',
  },
  sceneDetails: {
    display: 'flex',
    flexDirection: 'column',
    gap: tokens.spacingVerticalS,
  },
  sceneActions: {
    display: 'flex',
    gap: tokens.spacingHorizontalS,
    marginTop: tokens.spacingVerticalM,
    justifyContent: 'space-between',
  },
  progressSection: {
    width: '100%',
    maxWidth: '500px',
  },
  statsRow: {
    display: 'flex',
    justifyContent: 'space-around',
    padding: tokens.spacingVerticalL,
    gap: tokens.spacingHorizontalL,
  },
  statItem: {
    display: 'flex',
    flexDirection: 'column',
    alignItems: 'center',
    gap: tokens.spacingVerticalS,
  },
  audioPreview: {
    width: '100%',
    marginTop: tokens.spacingVerticalM,
    height: '40px',
    backgroundColor: tokens.colorNeutralBackground2,
    borderRadius: tokens.borderRadiusSmall,
    display: 'flex',
    alignItems: 'center',
    justifyContent: 'center',
  },
  providerCard: {
    padding: tokens.spacingVerticalL,
    backgroundColor: tokens.colorNeutralBackground2,
    borderRadius: tokens.borderRadiusMedium,
    marginBottom: tokens.spacingVerticalL,
  },
  providerGrid: {
    display: 'grid',
    gridTemplateColumns: 'repeat(auto-fit, minmax(200px, 1fr))',
    gap: tokens.spacingHorizontalM,
    marginTop: tokens.spacingVerticalM,
  },
  providerOption: {
    padding: tokens.spacingVerticalM,
    border: `1px solid ${tokens.colorNeutralStroke2}`,
    borderRadius: tokens.borderRadiusMedium,
    cursor: 'pointer',
    transition: 'all 0.2s ease',
    ':hover': {
      transform: 'translateY(-2px)',
      boxShadow: tokens.shadow8,
    },
  },
  selectedProvider: {
    border: `2px solid ${tokens.colorBrandStroke1}`,
  },
  settingsRow: {
    display: 'flex',
    gap: tokens.spacingHorizontalL,
    marginTop: tokens.spacingVerticalL,
    flexWrap: 'wrap',
  },
  settingItem: {
    flex: 1,
    minWidth: '200px',
  },
  imageOverlay: {
    position: 'absolute',
    top: 0,
    left: 0,
    right: 0,
    bottom: 0,
    backgroundColor: 'rgba(0, 0, 0, 0.7)',
    display: 'flex',
    alignItems: 'center',
    justifyContent: 'center',
    opacity: 0,
    transition: 'opacity 0.2s ease',
  },
  sceneCardHover: {
    ':hover .image-overlay': {
      opacity: 1,
    },
  },
  badgeGroup: {
    display: 'flex',
    gap: tokens.spacingHorizontalXS,
    position: 'absolute',
    top: tokens.spacingVerticalS,
    left: tokens.spacingHorizontalS,
    flexWrap: 'wrap',
  },
  fullscreenDialog: {
    maxWidth: '90vw',
    maxHeight: '90vh',
  },
  fullscreenImage: {
    width: '100%',
    height: 'auto',
    maxHeight: '70vh',
    objectFit: 'contain',
  },
  imageDetails: {
    marginTop: tokens.spacingVerticalL,
    display: 'flex',
    flexDirection: 'column',
    gap: tokens.spacingVerticalS,
  },
});

interface PreviewGenerationProps {
  data: PreviewData;
  scriptData: ScriptData;
  styleData: StyleData;
  advancedMode: boolean;
  onChange: (data: PreviewData) => void;
  onValidationChange: (validation: StepValidation) => void;
}

type GenerationStatus = 'idle' | 'generating' | 'completed' | 'error';

export const PreviewGeneration: FC<PreviewGenerationProps> = ({
  data,
  scriptData,
  styleData,
  advancedMode,
  onChange,
  onValidationChange,
}) => {
  const styles = useStyles();
  const visualsClient = getVisualsClient();
  const [status, setStatus] = useState<GenerationStatus>('idle');
  const [progress, setProgress] = useState(0);
  const [currentStage, setCurrentStage] = useState('');
  const [regeneratingScene, setRegeneratingScene] = useState<string | null>(null);
  const [providers, setProviders] = useState<VisualProvider[]>([]);
  const [selectedProvider, setSelectedProvider] = useState<string>('');
  const [imageStyle, setImageStyle] = useState<string>(styleData.imageStyle || 'photorealistic');
  const [imageQuality, setImageQuality] = useState<number>(styleData.imageQuality || 80);
  const [aspectRatio, setAspectRatio] = useState<string>(styleData.imageAspectRatio || '16:9');
  const [showProviderSettings, setShowProviderSettings] = useState(false);
  const [availableStyles, setAvailableStyles] = useState<string[]>([]);
  const [fullscreenImage, setFullscreenImage] = useState<{
    url: string;
    scene: ScriptScene;
    thumbnail: any;
  } | null>(null);

  const hasPreviewData = useMemo(() => {
    return data.thumbnails.length > 0 && data.audioSamples.length > 0;
  }, [data]);

  useEffect(() => {
    if (hasPreviewData) {
      setStatus('completed');
      onValidationChange({ isValid: true, errors: [] });
    } else {
      onValidationChange({ isValid: false, errors: ['Preview generation required'] });
    }
  }, [hasPreviewData, onValidationChange]);

  useEffect(() => {
    loadProviders();
    loadStyles();
  }, []);

  const loadProviders = useCallback(async () => {
    try {
      const response = await visualsClient.getProviders();
      setProviders(response.providers);

      const availableProvider = response.providers.find((p) => p.isAvailable);
      if (availableProvider && !selectedProvider) {
        setSelectedProvider(availableProvider.name);
      }
    } catch (error) {
      console.error('Failed to load providers:', error);
    }
  }, [visualsClient, selectedProvider]);

  const loadStyles = useCallback(async () => {
    try {
      const response = await visualsClient.getStyles();
      setAvailableStyles(response.allStyles);
    } catch (error) {
      console.error('Failed to load styles:', error);
      setAvailableStyles(['photorealistic', 'artistic', 'cinematic', 'minimalist']);
    }
  }, [visualsClient]);

  const generatePreviews = useCallback(async () => {
    setStatus('generating');
    setProgress(0);
    setCurrentStage('Initializing preview generation...');

    try {
      const prompts = scriptData.scenes.map((scene) => {
        const visualDesc = scene.visualDescription || scene.text.substring(0, 100);
        return `${styleData.visualStyle} style: ${visualDesc}`;
      });

      setCurrentStage('Generating images with AI...');

      const response = await visualsClient.batchGenerate(
        {
          prompts,
          style: imageStyle,
          aspectRatio,
          quality: imageQuality,
        },
        (batchProgress: BatchGenerateProgress) => {
          setProgress(batchProgress.progressPercentage * 0.8);
          setCurrentStage(
            `Generating image ${batchProgress.completedCount} of ${batchProgress.totalCount}...`
          );
        }
      );

      const thumbnails: PreviewThumbnail[] = response.images.map((img, index) => ({
        sceneId: scriptData.scenes[index].id,
        imageUrl: img.imagePath,
        caption:
          scriptData.scenes[index].visualDescription ||
          scriptData.scenes[index].text.substring(0, 50),
        provider: response.provider,
        generatedAt: img.generatedAt,
        quality: img.quality,
        clipScore: img.clipScore,
        isPlaceholder: false,
      }));

      for (let i = thumbnails.length; i < scriptData.scenes.length; i++) {
        const fallbackThumbnail: PreviewThumbnail = {
          sceneId: scriptData.scenes[i].id,
          imageUrl: `https://via.placeholder.com/400x300/6264a7/ffffff?text=Scene+${i + 1}`,
          caption: scriptData.scenes[i].text.substring(0, 50),
          isPlaceholder: true,
          failureReason: 'Generation failed - using placeholder',
        };
        thumbnails.push(fallbackThumbnail);
      }

      setCurrentStage('Generating audio previews...');
      setProgress(80);

      const audioSamples = [];
      for (let i = 0; i < scriptData.scenes.length; i++) {
        const scene = scriptData.scenes[i];
        await new Promise((resolve) => setTimeout(resolve, 300));

        audioSamples.push({
          sceneId: scene.id,
          audioUrl: `https://example.com/audio/${scene.id}.mp3`,
          duration: scene.duration,
          waveformData: Array.from({ length: 50 }, () => Math.random() * 100),
        });

        setProgress(80 + ((i + 1) / scriptData.scenes.length) * 20);
      }

      onChange({
        thumbnails,
        audioSamples,
        imageProvider: response.provider,
      });

      setProgress(100);
      setCurrentStage('Preview generation completed!');
      setStatus('completed');
    } catch (error) {
      console.error('Preview generation failed:', error);
      setStatus('error');
      setCurrentStage('Preview generation failed. Using placeholder images.');

      const fallbackThumbnails = scriptData.scenes.map((scene, index) => ({
        sceneId: scene.id,
        imageUrl: `https://via.placeholder.com/400x300/6264a7/ffffff?text=Scene+${index + 1}`,
        caption: scene.text.substring(0, 50),
        isPlaceholder: true,
        failureReason: error instanceof Error ? error.message : 'Unknown error',
      }));

      onChange({
        thumbnails: fallbackThumbnails,
        audioSamples: data.audioSamples,
      });

      onValidationChange({ isValid: false, errors: ['Preview generation failed'] });
    }
  }, [
    scriptData.scenes,
    styleData.visualStyle,
    imageStyle,
    aspectRatio,
    imageQuality,
    visualsClient,
    onChange,
    onValidationChange,
    data.audioSamples,
  ]);

  const regenerateScene = useCallback(
    async (sceneId: string) => {
      setRegeneratingScene(sceneId);

      try {
        const scene = scriptData.scenes.find((s) => s.id === sceneId);
        if (!scene) return;

        const visualDesc = scene.visualDescription || scene.text.substring(0, 100);
        const prompt = `${styleData.visualStyle} style: ${visualDesc}`;

        const response = await visualsClient.generateImage({
          prompt,
          style: imageStyle,
          aspectRatio,
          quality: imageQuality,
        });

        const updatedThumbnails = data.thumbnails.map((thumb) =>
          thumb.sceneId === sceneId
            ? {
                ...thumb,
                imageUrl: response.imagePath,
                provider: response.provider,
                generatedAt: response.generatedAt,
                isPlaceholder: false,
                failureReason: undefined,
              }
            : thumb
        );

        onChange({
          thumbnails: updatedThumbnails,
          audioSamples: data.audioSamples,
          imageProvider: response.provider,
        });
      } catch (error) {
        console.error('Scene regeneration failed:', error);

        const updatedThumbnails = data.thumbnails.map((thumb) =>
          thumb.sceneId === sceneId
            ? {
                ...thumb,
                failureReason: error instanceof Error ? error.message : 'Regeneration failed',
              }
            : thumb
        );

        onChange({
          thumbnails: updatedThumbnails,
          audioSamples: data.audioSamples,
        });
      } finally {
        setRegeneratingScene(null);
      }
    },
    [
      scriptData.scenes,
      styleData.visualStyle,
      imageStyle,
      aspectRatio,
      imageQuality,
      data,
      onChange,
      visualsClient,
    ]
  );

  const handleManualUpload = useCallback(
    (sceneId: string) => {
      const input = document.createElement('input');
      input.type = 'file';
      input.accept = 'image/*';
      input.onchange = (e) => {
        const file = (e.target as HTMLInputElement).files?.[0];
        if (file) {
          const reader = new FileReader();
          reader.onload = (event) => {
            const imageUrl = event.target?.result as string;
            const updatedThumbnails = data.thumbnails.map((thumb) =>
              thumb.sceneId === sceneId
                ? {
                    ...thumb,
                    imageUrl,
                    provider: 'Manual Upload',
                    isPlaceholder: false,
                  }
                : thumb
            );

            onChange({
              thumbnails: updatedThumbnails,
              audioSamples: data.audioSamples,
            });
          };
          reader.readAsDataURL(file);
        }
      };
      input.click();
    },
    [data, onChange]
  );

  const handleSearchFallback = useCallback(async (sceneId: string) => {
    console.log('Search fallback for scene:', sceneId);
  }, []);

  const playScenePreview = useCallback((sceneId: string) => {
    console.log('Playing preview for scene:', sceneId);
  }, []);

  const renderProviderSettings = () => (
    <Card className={styles.providerCard}>
      <div
        style={{
          display: 'flex',
          justifyContent: 'space-between',
          alignItems: 'center',
          marginBottom: tokens.spacingVerticalM,
        }}
      >
        <Title3>Image Generation Settings</Title3>
        <Button
          appearance="subtle"
          icon={<Settings24Regular />}
          onClick={() => setShowProviderSettings(!showProviderSettings)}
        >
          {showProviderSettings ? 'Hide' : 'Show'} Settings
        </Button>
      </div>

      {showProviderSettings && (
        <>
          <Text weight="semibold" style={{ marginBottom: tokens.spacingVerticalS }}>
            Select Image Provider
          </Text>
          <div className={styles.providerGrid}>
            {providers.map((provider) => (
              <div
                key={provider.name}
                className={`${styles.providerOption} ${
                  selectedProvider === provider.name ? styles.selectedProvider : ''
                }`}
                onClick={() => provider.isAvailable && setSelectedProvider(provider.name)}
                style={{
                  opacity: provider.isAvailable ? 1 : 0.5,
                  cursor: provider.isAvailable ? 'pointer' : 'not-allowed',
                }}
              >
                <Text weight="semibold">{provider.name}</Text>
                <div
                  style={{
                    display: 'flex',
                    alignItems: 'center',
                    gap: tokens.spacingHorizontalXS,
                    marginTop: tokens.spacingVerticalXS,
                  }}
                >
                  {provider.isAvailable ? (
                    <CheckmarkCircle24Regular
                      style={{ color: tokens.colorPaletteGreenForeground1, fontSize: '14px' }}
                    />
                  ) : (
                    <ErrorCircle24Regular
                      style={{ color: tokens.colorPaletteRedForeground1, fontSize: '14px' }}
                    />
                  )}
                  <Text size={200}>{provider.isAvailable ? 'Available' : 'Not Available'}</Text>
                </div>
                {provider.capabilities && (
                  <Text
                    size={200}
                    style={{
                      marginTop: tokens.spacingVerticalXS,
                      color: tokens.colorNeutralForeground3,
                    }}
                  >
                    {provider.capabilities.tier} •{' '}
                    {provider.capabilities.isFree
                      ? 'Free'
                      : `$${provider.capabilities.costPerImage}/image`}
                  </Text>
                )}
              </div>
            ))}
          </div>

          <div className={styles.settingsRow}>
            <Field label="Visual Style" className={styles.settingItem}>
              <Dropdown
                value={imageStyle}
                selectedOptions={[imageStyle]}
                onOptionSelect={(_, data) => setImageStyle(data.optionValue as string)}
              >
                {availableStyles.map((style) => (
                  <Option key={style} value={style}>
                    {style.charAt(0).toUpperCase() + style.slice(1)}
                  </Option>
                ))}
              </Dropdown>
            </Field>

            <Field label="Aspect Ratio" className={styles.settingItem}>
              <Dropdown
                value={aspectRatio}
                selectedOptions={[aspectRatio]}
                onOptionSelect={(_, data) => setAspectRatio(data.optionValue as string)}
              >
                <Option value="16:9">16:9 (Widescreen)</Option>
                <Option value="9:16">9:16 (Portrait)</Option>
                <Option value="1:1">1:1 (Square)</Option>
                <Option value="4:3">4:3 (Standard)</Option>
              </Dropdown>
            </Field>

            <Field label={`Quality: ${imageQuality}%`} className={styles.settingItem}>
              <Slider
                value={imageQuality}
                min={50}
                max={100}
                step={10}
                onChange={(_, data) => setImageQuality(data.value)}
              />
            </Field>
          </div>
        </>
      )}
    </Card>
  );

  const renderGenerationView = () => (
    <div className={styles.container}>
      {renderProviderSettings()}

      <div className={styles.generationCard}>
        <Title3>Generate Scene Previews</Title3>
        <Text>
          Create preview thumbnails and audio samples for each scene to review before final
          generation.
        </Text>

        <div className={styles.statsRow}>
          <div className={styles.statItem}>
            <Image24Regular style={{ fontSize: '32px', color: tokens.colorBrandForeground1 }} />
            <Text weight="semibold">{scriptData.scenes.length}</Text>
            <Text size={200}>Scenes</Text>
          </div>
          <div className={styles.statItem}>
            <Speaker224Regular style={{ fontSize: '32px', color: tokens.colorBrandForeground1 }} />
            <Text weight="semibold">{styleData.voiceProvider}</Text>
            <Text size={200}>Voice</Text>
          </div>
          {selectedProvider && (
            <div className={styles.statItem}>
              <Image24Regular style={{ fontSize: '32px', color: tokens.colorBrandForeground1 }} />
              <Text weight="semibold">{selectedProvider}</Text>
              <Text size={200}>Image Provider</Text>
            </div>
          )}
        </div>

        <Button
          appearance="primary"
          size="large"
          onClick={generatePreviews}
          disabled={!selectedProvider || providers.length === 0}
        >
          Generate Previews
        </Button>

        {!selectedProvider && providers.length > 0 && (
          <Text size={200} style={{ color: tokens.colorPaletteRedForeground1 }}>
            Please select an image provider
          </Text>
        )}
      </div>
    </div>
  );

  const renderGeneratingView = () => (
    <div className={styles.generationCard}>
      <Spinner size="large" />
      <Title3>Generating Previews...</Title3>
      <Text>{currentStage}</Text>
      <div className={styles.progressSection}>
        <ProgressBar value={progress / 100} />
        <Text size={200} style={{ marginTop: tokens.spacingVerticalS }}>
          {Math.round(progress)}% complete
        </Text>
      </div>
    </div>
  );

  const renderCompletedView = () => (
    <div className={styles.container}>
      {renderProviderSettings()}

      <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
        <div>
          <Title3>Scene Previews</Title3>
          <Text>Review and regenerate individual scenes as needed</Text>
        </div>
        <Tooltip content="Regenerate all previews" relationship="label">
          <Button
            appearance="secondary"
            icon={<ArrowClockwise24Regular />}
            onClick={generatePreviews}
          >
            Regenerate All
          </Button>
        </Tooltip>
      </div>

      <div className={styles.previewGrid}>
        {scriptData.scenes.map((scene: ScriptScene, index: number) => {
          const thumbnail = data.thumbnails.find((t) => t.sceneId === scene.id);
          const audioSample = data.audioSamples.find((a) => a.sceneId === scene.id);
          const isRegenerating = regeneratingScene === scene.id;

          return (
            <Card key={scene.id} className={`${styles.sceneCard} ${styles.sceneCardHover}`}>
              <div
                className={styles.scenePreview}
                onClick={() =>
                  thumbnail &&
                  !isRegenerating &&
                  setFullscreenImage({
                    url: thumbnail.imageUrl,
                    scene,
                    thumbnail,
                  })
                }
                style={{ cursor: thumbnail && !isRegenerating ? 'pointer' : 'default' }}
              >
                {isRegenerating ? (
                  <Spinner size="large" />
                ) : thumbnail ? (
                  <>
                    <img
                      src={thumbnail.imageUrl}
                      alt={thumbnail.caption}
                      className={styles.sceneImage}
                    />
                    <div
                      className="image-overlay"
                      style={styles.imageOverlay as React.CSSProperties}
                    >
                      <ImageEdit24Regular style={{ fontSize: '48px', color: 'white' }} />
                    </div>
                  </>
                ) : (
                  <Image24Regular style={{ fontSize: '48px' }} />
                )}
                <div className={styles.badgeGroup}>
                  <Badge appearance="filled">Scene {index + 1}</Badge>
                  {thumbnail?.isPlaceholder && (
                    <Badge appearance="filled" color="warning">
                      Placeholder
                    </Badge>
                  )}
                  {thumbnail?.provider && !thumbnail.isPlaceholder && (
                    <Badge appearance="filled" color="success">
                      {thumbnail.provider}
                    </Badge>
                  )}
                </div>
              </div>

              <div className={styles.sceneDetails}>
                <Text weight="semibold" size={300}>
                  {scene.text.substring(0, 60)}
                  {scene.text.length > 60 ? '...' : ''}
                </Text>
                <Text size={200} style={{ color: tokens.colorNeutralForeground3 }}>
                  Duration: {scene.duration}s
                </Text>

                {thumbnail?.quality && (
                  <Text size={200} style={{ color: tokens.colorNeutralForeground3 }}>
                    Quality: {thumbnail.quality}%
                  </Text>
                )}

                {thumbnail?.failureReason && (
                  <Text size={200} style={{ color: tokens.colorPaletteRedForeground1 }}>
                    {thumbnail.failureReason}
                  </Text>
                )}

                {audioSample && (
                  <div className={styles.audioPreview}>
                    <Speaker224Regular style={{ marginRight: tokens.spacingHorizontalS }} />
                    <Text size={200}>Audio ready</Text>
                  </div>
                )}

                <div className={styles.sceneActions}>
                  <Button
                    appearance="secondary"
                    icon={<Play24Regular />}
                    onClick={() => playScenePreview(scene.id)}
                    disabled={!thumbnail || !audioSample}
                  >
                    Preview
                  </Button>

                  <Menu>
                    <MenuTrigger>
                      <Button
                        appearance="subtle"
                        icon={<MoreHorizontal24Regular />}
                        disabled={isRegenerating}
                      />
                    </MenuTrigger>
                    <MenuPopover>
                      <MenuList>
                        <MenuItem
                          icon={<ArrowClockwise24Regular />}
                          onClick={() => regenerateScene(scene.id)}
                        >
                          Regenerate
                        </MenuItem>
                        <MenuItem
                          icon={<ArrowUpload24Regular />}
                          onClick={() => handleManualUpload(scene.id)}
                        >
                          Upload Image
                        </MenuItem>
                        <MenuItem
                          icon={<Search24Regular />}
                          onClick={() => handleSearchFallback(scene.id)}
                        >
                          Search Stock Images
                        </MenuItem>
                      </MenuList>
                    </MenuPopover>
                  </Menu>
                </div>
              </div>
            </Card>
          );
        })}
      </div>

      {advancedMode && (
        <Card style={{ padding: tokens.spacingVerticalL }}>
          <Title3 style={{ marginBottom: tokens.spacingVerticalM }}>Advanced Options</Title3>
          <Text size={300}>
            Previews use lower quality settings for faster generation. Final video will use full
            quality settings.
          </Text>
        </Card>
      )}

      {fullscreenImage && (
        <Dialog
          open={!!fullscreenImage}
          onOpenChange={(_, data) => !data.open && setFullscreenImage(null)}
        >
          <DialogSurface className={styles.fullscreenDialog}>
            <DialogTitle>
              Scene {scriptData.scenes.findIndex((s) => s.id === fullscreenImage.scene.id) + 1}{' '}
              Preview
            </DialogTitle>
            <DialogBody>
              <DialogContent>
                <img
                  src={fullscreenImage.url}
                  alt="Fullscreen preview"
                  className={styles.fullscreenImage}
                />
                <div className={styles.imageDetails}>
                  <Text weight="semibold">Scene Text:</Text>
                  <Text>{fullscreenImage.scene.text}</Text>

                  {fullscreenImage.thumbnail.provider && (
                    <>
                      <Text weight="semibold" style={{ marginTop: tokens.spacingVerticalM }}>
                        Provider:
                      </Text>
                      <Text>{fullscreenImage.thumbnail.provider}</Text>
                    </>
                  )}

                  {fullscreenImage.thumbnail.quality && (
                    <>
                      <Text weight="semibold" style={{ marginTop: tokens.spacingVerticalM }}>
                        Quality Score:
                      </Text>
                      <Text>{fullscreenImage.thumbnail.quality}%</Text>
                    </>
                  )}

                  {fullscreenImage.scene.visualDescription && (
                    <>
                      <Text weight="semibold" style={{ marginTop: tokens.spacingVerticalM }}>
                        Visual Description:
                      </Text>
                      <Text>{fullscreenImage.scene.visualDescription}</Text>
                    </>
                  )}
                </div>
              </DialogContent>
              <DialogActions>
                <Button appearance="secondary" onClick={() => setFullscreenImage(null)}>
                  Close
                </Button>
                <Button
                  appearance="primary"
                  onClick={() => {
                    regenerateScene(fullscreenImage.scene.id);
                    setFullscreenImage(null);
                  }}
                >
                  Regenerate
                </Button>
              </DialogActions>
            </DialogBody>
          </DialogSurface>
        </Dialog>
      )}
    </div>
  );

  const renderErrorView = () => (
    <div className={styles.generationCard}>
      <Warning24Regular style={{ fontSize: '48px', color: tokens.colorPaletteRedForeground1 }} />
      <Title3>Preview Generation Failed</Title3>
      <Text>{currentStage}</Text>
      <Button appearance="primary" onClick={generatePreviews}>
        Try Again
      </Button>
    </div>
  );

  return (
    <div className={styles.container}>
      <div className={styles.header}>
        <Title2>Preview Generation</Title2>
        <Text>
          Generate preview thumbnails and audio samples to review your video before final rendering.
        </Text>
      </div>

      {status === 'idle' && renderGenerationView()}
      {status === 'generating' && renderGeneratingView()}
      {status === 'completed' && renderCompletedView()}
      {status === 'error' && renderErrorView()}
    </div>
  );
};
