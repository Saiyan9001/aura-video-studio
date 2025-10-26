import {
  makeStyles,
  tokens,
  Title3,
  Text,
  Button,
  Card,
  Spinner,
  Badge,
  Image,
} from '@fluentui/react-components';
import { Search24Regular } from '@fluentui/react-icons';
import { useState, useEffect, useCallback } from 'react';
import { apiUrl } from '../../config/api';

const useStyles = makeStyles({
  container: {
    display: 'flex',
    flexDirection: 'column',
    gap: tokens.spacingVerticalM,
  },
  header: {
    display: 'flex',
    alignItems: 'center',
    justifyContent: 'space-between',
    marginBottom: tokens.spacingVerticalS,
  },
  grid: {
    display: 'grid',
    gridTemplateColumns: 'repeat(auto-fill, minmax(150px, 1fr))',
    gap: tokens.spacingHorizontalM,
  },
  thumbnailCard: {
    position: 'relative',
    cursor: 'pointer',
    overflow: 'hidden',
    transition: 'transform 0.2s',
    ':hover': {
      transform: 'scale(1.05)',
    },
  },
  thumbnail: {
    width: '100%',
    height: '120px',
    objectFit: 'cover',
  },
  thumbnailOverlay: {
    position: 'absolute',
    bottom: 0,
    left: 0,
    right: 0,
    padding: tokens.spacingVerticalXS,
    backgroundColor: 'rgba(0, 0, 0, 0.7)',
    color: tokens.colorNeutralForegroundOnBrand,
  },
  loadingState: {
    display: 'flex',
    flexDirection: 'column',
    alignItems: 'center',
    justifyContent: 'center',
    padding: tokens.spacingVerticalXXL,
    gap: tokens.spacingVerticalM,
  },
  emptyState: {
    textAlign: 'center',
    padding: tokens.spacingVerticalXXL,
    color: tokens.colorNeutralForeground3,
  },
});

interface AssetMatch {
  filePath: string;
  url: string;
  relevanceScore: number;
  thumbnailUrl: string;
}

interface AssetSuggestion {
  keyword: string;
  description: string;
  matches: AssetMatch[];
}

interface AssetSuggestionsProps {
  sceneHeading: string;
  sceneScript: string;
  onSelectAsset?: (asset: AssetMatch) => void;
}

export function AssetSuggestions({
  sceneHeading,
  sceneScript,
  onSelectAsset,
}: AssetSuggestionsProps) {
  const styles = useStyles();
  const [suggestions, setSuggestions] = useState<AssetSuggestion[]>([]);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const loadSuggestions = useCallback(async () => {
    setLoading(true);
    setError(null);
    try {
      const response = await fetch(`${apiUrl}/content/suggest-assets`, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({
          sceneHeading,
          sceneScript,
        }),
      });

      if (!response.ok) {
        throw new Error('Failed to load asset suggestions');
      }

      const data = await response.json();
      setSuggestions(data.suggestions || []);
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Unknown error');
      console.error('Failed to load asset suggestions:', err);
    } finally {
      setLoading(false);
    }
  }, [sceneHeading, sceneScript]);

  useEffect(() => {
    loadSuggestions();
  }, [loadSuggestions]);

  const handleAssetClick = (asset: AssetMatch) => {
    if (onSelectAsset) {
      onSelectAsset(asset);
    }
  };

  if (loading) {
    return (
      <Card>
        <div className={styles.loadingState}>
          <Spinner size="large" />
          <Text>Loading asset suggestions...</Text>
        </div>
      </Card>
    );
  }

  if (error) {
    return (
      <Card>
        <div className={styles.emptyState}>
          <Text>Failed to load suggestions: {error}</Text>
          <Button
            appearance="primary"
            icon={<Search24Regular />}
            onClick={loadSuggestions}
            style={{ marginTop: tokens.spacingVerticalM }}
          >
            Retry
          </Button>
        </div>
      </Card>
    );
  }

  if (suggestions.length === 0) {
    return (
      <Card>
        <div className={styles.emptyState}>
          <Text>No asset suggestions available for this scene.</Text>
          <Button
            appearance="primary"
            icon={<Search24Regular />}
            onClick={loadSuggestions}
            style={{ marginTop: tokens.spacingVerticalM }}
          >
            Generate Suggestions
          </Button>
        </div>
      </Card>
    );
  }

  return (
    <div className={styles.container}>
      <div className={styles.header}>
        <Title3>Suggested Assets</Title3>
        <Button size="small" icon={<Search24Regular />} onClick={loadSuggestions}>
          More Suggestions
        </Button>
      </div>

      {suggestions.map((suggestion, idx) => (
        <Card key={idx}>
          <Text weight="semibold" block style={{ marginBottom: tokens.spacingVerticalXS }}>
            {suggestion.keyword}
          </Text>
          <Text
            size={200}
            block
            style={{ marginBottom: tokens.spacingVerticalS, color: tokens.colorNeutralForeground3 }}
          >
            {suggestion.description}
          </Text>

          {suggestion.matches.length > 0 ? (
            <div className={styles.grid}>
              {suggestion.matches.map((match, matchIdx) => (
                <Card
                  key={matchIdx}
                  className={styles.thumbnailCard}
                  onClick={() => handleAssetClick(match)}
                >
                  <Image
                    className={styles.thumbnail}
                    src={match.thumbnailUrl}
                    alt={suggestion.keyword}
                    fit="cover"
                  />
                  <div className={styles.thumbnailOverlay}>
                    <Badge appearance="tint" color="brand" size="small">
                      {match.relevanceScore.toFixed(0)}% match
                    </Badge>
                  </div>
                </Card>
              ))}
            </div>
          ) : (
            <Text size={200} style={{ color: tokens.colorNeutralForeground3 }}>
              No assets found for this keyword. Try generating more suggestions.
            </Text>
          )}
        </Card>
      ))}
    </div>
  );
}
