import {
  Card,
  Text,
  Button,
  Input,
  Dropdown,
  Option,
  makeStyles,
  tokens,
  Badge,
  Spinner,
  OptionOnSelectData,
} from '@fluentui/react-components';
import { Search24Regular, MusicNote224Regular, Play24Regular } from '@fluentui/react-icons';
import React, { useState, useEffect, useCallback } from 'react';
import {
  audioIntelligenceService,
  MusicTrack,
  MusicMood,
  MusicGenre,
  EnergyLevel,
  MusicSearchParams,
} from '../../services/audioIntelligenceService';

const useStyles = makeStyles({
  container: {
    display: 'flex',
    flexDirection: 'column',
    gap: tokens.spacingVerticalM,
    padding: tokens.spacingVerticalM,
  },
  header: {
    display: 'flex',
    justifyContent: 'space-between',
    alignItems: 'center',
    marginBottom: tokens.spacingVerticalM,
  },
  filters: {
    display: 'grid',
    gridTemplateColumns: 'repeat(auto-fit, minmax(200px, 1fr))',
    gap: tokens.spacingHorizontalM,
    marginBottom: tokens.spacingVerticalM,
  },
  trackList: {
    display: 'flex',
    flexDirection: 'column',
    gap: tokens.spacingVerticalS,
  },
  trackCard: {
    display: 'flex',
    alignItems: 'center',
    gap: tokens.spacingHorizontalM,
    padding: tokens.spacingVerticalM,
    cursor: 'pointer',
    transition: 'all 0.2s ease',
    '&:hover': {
      backgroundColor: tokens.colorNeutralBackground1Hover,
    },
  },
  trackInfo: {
    flex: 1,
    display: 'flex',
    flexDirection: 'column',
    gap: tokens.spacingVerticalXXS,
  },
  trackMeta: {
    display: 'flex',
    gap: tokens.spacingHorizontalS,
    flexWrap: 'wrap',
  },
  emptyState: {
    display: 'flex',
    flexDirection: 'column',
    alignItems: 'center',
    justifyContent: 'center',
    padding: tokens.spacingVerticalXXXL,
    gap: tokens.spacingVerticalM,
  },
  loadingState: {
    display: 'flex',
    justifyContent: 'center',
    padding: tokens.spacingVerticalXXL,
  },
});

interface MusicSelectorProps {
  onSelect?: (track: MusicTrack) => void;
  selectedTrack?: MusicTrack;
}

export const MusicSelector: React.FC<MusicSelectorProps> = ({ onSelect, selectedTrack }) => {
  const styles = useStyles();
  const [tracks, setTracks] = useState<MusicTrack[]>([]);
  const [loading, setLoading] = useState(false);
  const [searchParams, setSearchParams] = useState<MusicSearchParams>({});

  const loadMusicLibrary = useCallback(async () => {
    setLoading(true);
    try {
      const { tracks } = await audioIntelligenceService.getMusicLibrary(searchParams);
      setTracks(tracks);
    } catch (error) {
      console.error('Failed to load music library:', error);
    } finally {
      setLoading(false);
    }
  }, [searchParams]);

  useEffect(() => {
    loadMusicLibrary();
  }, [loadMusicLibrary]);

  const handleMoodChange = (_: unknown, data: OptionOnSelectData) => {
    setSearchParams({ ...searchParams, mood: data.value as MusicMood });
  };

  const handleGenreChange = (_: unknown, data: OptionOnSelectData) => {
    setSearchParams({ ...searchParams, genre: data.value as MusicGenre });
  };

  const handleEnergyChange = (_: unknown, data: OptionOnSelectData) => {
    setSearchParams({ ...searchParams, energy: data.value as EnergyLevel });
  };

  const handleSearchChange = (e: React.ChangeEvent<HTMLInputElement>) => {
    setSearchParams({ ...searchParams, searchQuery: e.target.value });
  };

  const handleTrackClick = (track: MusicTrack) => {
    if (onSelect) {
      onSelect(track);
    }
  };

  const formatDuration = (isoDuration: string): string => {
    // Simple ISO 8601 duration parser for display
    const match = isoDuration.match(/PT(\d+)M/);
    if (match) {
      return `${match[1]}:00`;
    }
    return isoDuration;
  };

  return (
    <div className={styles.container}>
      <div className={styles.header}>
        <Text size={500} weight="semibold">
          <MusicNote224Regular /> Music Library
        </Text>
        <Button appearance="secondary" onClick={loadMusicLibrary}>
          Refresh
        </Button>
      </div>

      <div className={styles.filters}>
        <Input
          placeholder="Search music..."
          contentBefore={<Search24Regular />}
          onChange={handleSearchChange}
        />
        <Dropdown placeholder="Select mood" onOptionSelect={handleMoodChange}>
          {Object.values(MusicMood).map((mood) => (
            <Option key={mood} value={mood}>
              {mood}
            </Option>
          ))}
        </Dropdown>
        <Dropdown placeholder="Select genre" onOptionSelect={handleGenreChange}>
          {Object.values(MusicGenre).map((genre) => (
            <Option key={genre} value={genre}>
              {genre}
            </Option>
          ))}
        </Dropdown>
        <Dropdown placeholder="Select energy" onOptionSelect={handleEnergyChange}>
          {Object.values(EnergyLevel).map((energy) => (
            <Option key={energy} value={energy}>
              {energy}
            </Option>
          ))}
        </Dropdown>
      </div>

      {loading ? (
        <div className={styles.loadingState}>
          <Spinner label="Loading music library..." />
        </div>
      ) : tracks.length === 0 ? (
        <div className={styles.emptyState}>
          <MusicNote224Regular
            style={{ fontSize: '48px', color: tokens.colorNeutralForeground3 }}
          />
          <Text size={400} weight="semibold">
            No music tracks found
          </Text>
          <Text size={300}>Try adjusting your filters or search query</Text>
        </div>
      ) : (
        <div className={styles.trackList}>
          {tracks.map((track) => (
            <Card
              key={track.trackId}
              className={styles.trackCard}
              onClick={() => handleTrackClick(track)}
              appearance={selectedTrack?.trackId === track.trackId ? 'filled' : 'subtle'}
            >
              <Play24Regular />
              <div className={styles.trackInfo}>
                <Text size={400} weight="semibold">
                  {track.title}
                </Text>
                {track.artist && (
                  <Text size={300} style={{ color: tokens.colorNeutralForeground3 }}>
                    {track.artist}
                  </Text>
                )}
                <div className={styles.trackMeta}>
                  <Badge appearance="tint" color="brand">
                    {track.mood}
                  </Badge>
                  <Badge appearance="tint">{track.genre}</Badge>
                  <Badge appearance="tint">{track.energy}</Badge>
                  <Text size={200}>{track.bpm} BPM</Text>
                  <Text size={200}>{formatDuration(track.duration)}</Text>
                </div>
              </div>
            </Card>
          ))}
        </div>
      )}
    </div>
  );
};

export default MusicSelector;
