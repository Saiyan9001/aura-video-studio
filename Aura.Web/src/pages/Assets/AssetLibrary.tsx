import {
  Button,
  Input,
  Text,
  Card,
  Spinner,
  makeStyles,
  shorthands,
  tokens,
} from '@fluentui/react-components';
import {
  ArrowUpload24Regular,
  Grid24Regular,
  List24Regular,
  Search24Regular,
  ImageAdd24Regular,
  Image24Regular,
} from '@fluentui/react-icons';
import React, { useState, useEffect, useCallback } from 'react';
import { CollectionsPanel } from '../../components/Assets/CollectionsPanel';
import { StockImageSearch } from '../../components/Assets/StockImageSearch';
import { assetService } from '../../services/assetService';
import { Asset, AssetType } from '../../types/assets';

const useStyles = makeStyles({
  container: {
    display: 'flex',
    flexDirection: 'column',
    height: '100vh',
    backgroundColor: tokens.colorNeutralBackground1,
  },
  header: {
    display: 'flex',
    justifyContent: 'space-between',
    alignItems: 'center',
    ...shorthands.padding(tokens.spacingVerticalL, tokens.spacingHorizontalXL),
    ...shorthands.borderBottom('1px', 'solid', tokens.colorNeutralStroke1),
  },
  headerActions: {
    display: 'flex',
    ...shorthands.gap(tokens.spacingHorizontalM),
  },
  content: {
    display: 'flex',
    flexGrow: 1,
    ...shorthands.overflow('hidden'),
  },
  sidebar: {
    width: '20%',
    minWidth: '200px',
    backgroundColor: tokens.colorNeutralBackground2,
    ...shorthands.padding(tokens.spacingVerticalL),
    overflowY: 'auto',
    ...shorthands.borderRight('1px', 'solid', tokens.colorNeutralStroke1),
  },
  mainContent: {
    width: '60%',
    display: 'flex',
    flexDirection: 'column',
    ...shorthands.overflow('hidden'),
  },
  searchBar: {
    ...shorthands.padding(tokens.spacingVerticalL, tokens.spacingHorizontalXL),
    ...shorthands.borderBottom('1px', 'solid', tokens.colorNeutralStroke1),
  },
  assetsGrid: {
    flexGrow: 1,
    overflowY: 'auto',
    ...shorthands.padding(tokens.spacingVerticalL, tokens.spacingHorizontalXL),
    display: 'grid',
    gridTemplateColumns: 'repeat(auto-fill, minmax(200px, 1fr))',
    ...shorthands.gap(tokens.spacingHorizontalL),
  },
  assetCard: {
    cursor: 'pointer',
    ':hover': {
      boxShadow: tokens.shadow8,
    },
  },
  assetThumbnail: {
    width: '100%',
    height: '150px',
    objectFit: 'cover',
    backgroundColor: tokens.colorNeutralBackground3,
  },
  assetInfo: {
    ...shorthands.padding(tokens.spacingVerticalM),
  },
  previewPanel: {
    width: '20%',
    minWidth: '250px',
    backgroundColor: tokens.colorNeutralBackground2,
    ...shorthands.padding(tokens.spacingVerticalL),
    overflowY: 'auto',
    ...shorthands.borderLeft('1px', 'solid', tokens.colorNeutralStroke1),
  },
  emptyState: {
    display: 'flex',
    flexDirection: 'column',
    alignItems: 'center',
    justifyContent: 'center',
    height: '100%',
    ...shorthands.gap(tokens.spacingVerticalL),
  },
  filterSection: {
    marginBottom: tokens.spacingVerticalL,
  },
  filterButton: {
    width: '100%',
    justifyContent: 'flex-start',
    marginBottom: tokens.spacingVerticalS,
  },
  previewImage: {
    width: '100%',
    height: 'auto',
    marginBottom: tokens.spacingVerticalM,
  },
  tagsList: {
    display: 'flex',
    flexWrap: 'wrap',
    ...shorthands.gap(tokens.spacingHorizontalS),
    marginTop: tokens.spacingVerticalS,
  },
  tag: {
    ...shorthands.padding(tokens.spacingVerticalXS, tokens.spacingHorizontalS),
    backgroundColor: tokens.colorBrandBackground2,
    ...shorthands.borderRadius(tokens.borderRadiusMedium),
    fontSize: tokens.fontSizeBase200,
  },
});

export const AssetLibrary: React.FC = () => {
  const styles = useStyles();
  const [assets, setAssets] = useState<Asset[]>([]);
  const [selectedAsset, setSelectedAsset] = useState<Asset | null>(null);
  const [loading, setLoading] = useState(false);
  const [searchQuery, setSearchQuery] = useState('');
  const [viewMode, setViewMode] = useState<'grid' | 'list'>('grid');
  const [filterType, setFilterType] = useState<AssetType | undefined>();
  const [showStockSearch, setShowStockSearch] = useState(false);
  const [selectedCollectionId, setSelectedCollectionId] = useState<string | undefined>();

  const loadAssets = useCallback(async () => {
    setLoading(true);
    try {
      const result = await assetService.getAssets(
        searchQuery || undefined,
        filterType,
        undefined,
        1,
        50
      );
      setAssets(result.assets);
    } catch (error) {
      console.error('Failed to load assets:', error);
    } finally {
      setLoading(false);
    }
  }, [searchQuery, filterType]);

  useEffect(() => {
    loadAssets();
  }, [loadAssets]);

  const handleUpload = () => {
    const input = document.createElement('input');
    input.type = 'file';
    input.multiple = true;
    input.accept = 'image/*,video/*,audio/*';
    input.onchange = async (e) => {
      const files = (e.target as HTMLInputElement).files;
      if (!files) return;

      for (let i = 0; i < files.length; i++) {
        try {
          await assetService.uploadAsset(files[i]);
        } catch (error) {
          console.error('Failed to upload file:', error);
        }
      }
      loadAssets();
    };
    input.click();
  };

  const handleAssetClick = (asset: Asset) => {
    setSelectedAsset(asset);
  };

  return (
    <div className={styles.container}>
      <div className={styles.header}>
        <Text size={600} weight="semibold">
          Asset Library
        </Text>
        <div className={styles.headerActions}>
          <Button
            appearance="subtle"
            icon={viewMode === 'grid' ? <List24Regular /> : <Grid24Regular />}
            onClick={() => setViewMode(viewMode === 'grid' ? 'list' : 'grid')}
          >
            {viewMode === 'grid' ? 'List' : 'Grid'} View
          </Button>
          <Button
            appearance="subtle"
            icon={<Image24Regular />}
            onClick={() => setShowStockSearch(true)}
          >
            Stock Images
          </Button>
          <Button appearance="primary" icon={<ArrowUpload24Regular />} onClick={handleUpload}>
            Import Assets
          </Button>
        </div>
      </div>

      <div className={styles.content}>
        {/* Sidebar */}
        <div className={styles.sidebar}>
          <div className={styles.filterSection}>
            <Text size={400} weight="semibold">
              Type
            </Text>
            <Button
              className={styles.filterButton}
              appearance={!filterType ? 'primary' : 'subtle'}
              onClick={() => setFilterType(undefined)}
            >
              All Assets ({assets.length})
            </Button>
            <Button
              className={styles.filterButton}
              appearance={filterType === AssetType.Image ? 'primary' : 'subtle'}
              onClick={() => setFilterType(AssetType.Image)}
            >
              Images
            </Button>
            <Button
              className={styles.filterButton}
              appearance={filterType === AssetType.Video ? 'primary' : 'subtle'}
              onClick={() => setFilterType(AssetType.Video)}
            >
              Videos
            </Button>
            <Button
              className={styles.filterButton}
              appearance={filterType === AssetType.Audio ? 'primary' : 'subtle'}
              onClick={() => setFilterType(AssetType.Audio)}
            >
              Audio
            </Button>
          </div>

          <div className={styles.filterSection}>
            <CollectionsPanel
              selectedCollectionId={selectedCollectionId}
              onCollectionSelect={setSelectedCollectionId}
            />
          </div>
        </div>

        {/* Main content */}
        <div className={styles.mainContent}>
          <div className={styles.searchBar}>
            <Input
              placeholder="Search assets..."
              value={searchQuery}
              onChange={(e) => setSearchQuery(e.target.value)}
              contentBefore={<Search24Regular />}
              style={{ width: '100%' }}
            />
          </div>

          {loading ? (
            <div className={styles.emptyState}>
              <Spinner size="large" />
              <Text>Loading assets...</Text>
            </div>
          ) : assets.length === 0 ? (
            <div className={styles.emptyState}>
              <ImageAdd24Regular
                style={{ fontSize: '64px', color: tokens.colorNeutralForeground3 }}
              />
              <Text size={500}>No assets found</Text>
              <Text>Upload assets to get started</Text>
              <Button appearance="primary" icon={<ArrowUpload24Regular />} onClick={handleUpload}>
                Import Assets
              </Button>
            </div>
          ) : (
            <div className={styles.assetsGrid}>
              {assets.map((asset) => (
                <Card
                  key={asset.id}
                  className={styles.assetCard}
                  onClick={() => handleAssetClick(asset)}
                >
                  <div
                    className={styles.assetThumbnail}
                    style={{
                      display: 'flex',
                      alignItems: 'center',
                      justifyContent: 'center',
                    }}
                  >
                    {asset.type === AssetType.Image && '🖼️'}
                    {asset.type === AssetType.Video && '🎬'}
                    {asset.type === AssetType.Audio && '🔊'}
                  </div>
                  <div className={styles.assetInfo}>
                    <Text weight="semibold" size={300} truncate>
                      {asset.title}
                    </Text>
                    <Text size={200} style={{ color: tokens.colorNeutralForeground3 }}>
                      {asset.type}
                    </Text>
                  </div>
                </Card>
              ))}
            </div>
          )}
        </div>

        {/* Preview panel */}
        {selectedAsset && (
          <div className={styles.previewPanel}>
            <Text
              size={500}
              weight="semibold"
              block
              style={{ marginBottom: tokens.spacingVerticalM }}
            >
              {selectedAsset.title}
            </Text>

            <div
              className={styles.previewImage}
              style={{
                display: 'flex',
                alignItems: 'center',
                justifyContent: 'center',
                height: '200px',
                backgroundColor: tokens.colorNeutralBackground3,
                borderRadius: tokens.borderRadiusMedium,
              }}
            >
              {selectedAsset.type === AssetType.Image && '🖼️'}
              {selectedAsset.type === AssetType.Video && '🎬'}
              {selectedAsset.type === AssetType.Audio && '🔊'}
            </div>

            {selectedAsset.description && (
              <Text size={300} block style={{ marginBottom: tokens.spacingVerticalM }}>
                {selectedAsset.description}
              </Text>
            )}

            <Text size={300} weight="semibold" block>
              Metadata
            </Text>
            <Text size={200} style={{ color: tokens.colorNeutralForeground3 }}>
              Type: {selectedAsset.type}
            </Text>
            <Text size={200} style={{ color: tokens.colorNeutralForeground3 }}>
              Source: {selectedAsset.source}
            </Text>
            {selectedAsset.metadata.width && selectedAsset.metadata.height && (
              <Text size={200} style={{ color: tokens.colorNeutralForeground3 }}>
                Resolution: {selectedAsset.metadata.width}x{selectedAsset.metadata.height}
              </Text>
            )}
            {selectedAsset.metadata.fileSizeBytes && (
              <Text size={200} style={{ color: tokens.colorNeutralForeground3 }}>
                Size: {(selectedAsset.metadata.fileSizeBytes / 1024 / 1024).toFixed(2)} MB
              </Text>
            )}

            {selectedAsset.tags.length > 0 && (
              <>
                <Text
                  size={300}
                  weight="semibold"
                  block
                  style={{ marginTop: tokens.spacingVerticalM }}
                >
                  Tags
                </Text>
                <div className={styles.tagsList}>
                  {selectedAsset.tags.slice(0, 10).map((tag, idx) => (
                    <span key={idx} className={styles.tag}>
                      {tag.name}
                    </span>
                  ))}
                </div>
              </>
            )}

            <Button
              appearance="primary"
              style={{ marginTop: tokens.spacingVerticalL, width: '100%' }}
            >
              Add to Timeline
            </Button>
          </div>
        )}
      </div>

      <StockImageSearch
        isOpen={showStockSearch}
        onClose={() => setShowStockSearch(false)}
        onImageAdded={loadAssets}
      />
    </div>
  );
};

export default AssetLibrary;
