import {
  makeStyles,
  tokens,
  Text,
  Card,
  Button,
  Input,
  Dropdown,
  Option,
  Spinner,
  Badge,
} from '@fluentui/react-components';
import { MicRegular, SearchRegular, PlayRegular } from '@fluentui/react-icons';
import { useState, useEffect, useCallback } from 'react';
import { apiUrl } from '../../config/api';

const useStyles = makeStyles({
  container: {
    display: 'flex',
    flexDirection: 'column',
    gap: tokens.spacingVerticalM,
  },
  filters: {
    display: 'flex',
    gap: tokens.spacingHorizontalM,
    flexWrap: 'wrap',
  },
  searchBar: {
    flex: 1,
    minWidth: '200px',
  },
  filterDropdown: {
    minWidth: '150px',
  },
  voiceGrid: {
    display: 'grid',
    gridTemplateColumns: 'repeat(auto-fill, minmax(280px, 1fr))',
    gap: tokens.spacingVerticalM,
    marginTop: tokens.spacingVerticalM,
  },
  voiceCard: {
    padding: tokens.spacingVerticalM,
    cursor: 'pointer',
    transition: 'all 0.2s ease',
    '&:hover': {
      backgroundColor: tokens.colorNeutralBackground1Hover,
      transform: 'translateY(-2px)',
      boxShadow: tokens.shadow8,
    },
  },
  selectedCard: {
    backgroundColor: tokens.colorBrandBackground2,
    borderColor: tokens.colorBrandForeground1 as string,
    borderWidth: '2px' as string,
  },
  voiceHeader: {
    display: 'flex',
    justifyContent: 'space-between',
    alignItems: 'center',
    marginBottom: tokens.spacingVerticalS,
  },
  voiceName: {
    fontWeight: tokens.fontWeightSemibold,
    fontSize: tokens.fontSizeBase400,
  },
  voiceInfo: {
    display: 'flex',
    flexDirection: 'column',
    gap: tokens.spacingVerticalXS,
    marginTop: tokens.spacingVerticalS,
  },
  voiceDetails: {
    display: 'flex',
    gap: tokens.spacingHorizontalS,
    fontSize: tokens.fontSizeBase200,
    color: tokens.colorNeutralForeground3,
  },
  badges: {
    display: 'flex',
    gap: tokens.spacingHorizontalXS,
    flexWrap: 'wrap',
    marginTop: tokens.spacingVerticalS,
  },
  playButton: {
    marginTop: tokens.spacingVerticalS,
  },
  loading: {
    display: 'flex',
    justifyContent: 'center',
    alignItems: 'center',
    padding: tokens.spacingVerticalXXL,
  },
  emptyState: {
    textAlign: 'center',
    padding: tokens.spacingVerticalXXL,
    color: tokens.colorNeutralForeground3,
  },
});

interface VoiceDescriptor {
  id: string;
  name: string;
  provider: string;
  locale: string;
  gender: string;
  voiceType: string;
  availableStyles?: string[];
  description?: string;
}

interface VoiceProfileSelectorProps {
  selectedVoiceId: string;
  onVoiceSelect: (voiceId: string) => void;
}

export const VoiceProfileSelector: React.FC<VoiceProfileSelectorProps> = ({
  selectedVoiceId,
  onVoiceSelect,
}) => {
  const styles = useStyles();
  const [voices, setVoices] = useState<VoiceDescriptor[]>([]);
  const [filteredVoices, setFilteredVoices] = useState<VoiceDescriptor[]>([]);
  const [loading, setLoading] = useState(true);
  const [searchQuery, setSearchQuery] = useState('');
  const [providerFilter, setProviderFilter] = useState<string>('all');
  const [genderFilter, setGenderFilter] = useState<string>('all');
  const [localeFilter, setLocaleFilter] = useState<string>('all');

  const filterVoices = useCallback(() => {
    let filtered = voices;

    if (searchQuery) {
      const query = searchQuery.toLowerCase();
      filtered = filtered.filter(
        (v) => v.name.toLowerCase().includes(query) || v.description?.toLowerCase().includes(query)
      );
    }

    if (providerFilter !== 'all') {
      filtered = filtered.filter((v) => v.provider === providerFilter);
    }

    if (genderFilter !== 'all') {
      filtered = filtered.filter((v) => v.gender === genderFilter);
    }

    if (localeFilter !== 'all') {
      filtered = filtered.filter((v) => v.locale === localeFilter);
    }

    setFilteredVoices(filtered);
  }, [searchQuery, providerFilter, genderFilter, localeFilter, voices]);

  useEffect(() => {
    loadVoices();
  }, []);

  useEffect(() => {
    filterVoices();
  }, [filterVoices]);

  const handlePlaySample = async (voiceId: string, event: React.MouseEvent) => {
    event.stopPropagation();
    try {
      // Request voice sample playback from API
      const response = await fetch(`${apiUrl}/api/v1/voices/${voiceId}/sample`, {
        method: 'POST',
      });

      if (response.ok) {
        const data = await response.json();
        if (data.audioUrl) {
          // Play the audio sample
          const audio = new Audio(data.audioUrl);
          audio.play().catch((err) => console.error('Failed to play audio:', err));
        }
      } else {
        console.warn('Voice sample not available for:', voiceId);
        alert(
          'Voice sample preview not available. Try selecting the voice to hear it in your video.'
        );
      }
    } catch (error) {
      console.error('Error playing voice sample:', error);
    }
  };

  if (loading) {
    return (
      <div className={styles.loading}>
        <Spinner label="Loading voices..." />
      </div>
    );
  }

  return (
    <div className={styles.container}>
      <div className={styles.filters}>
        <Input
          className={styles.searchBar}
          placeholder="Search voices..."
          contentBefore={<SearchRegular />}
          value={searchQuery}
          onChange={(_, data) => setSearchQuery(data.value)}
        />
        <Dropdown
          className={styles.filterDropdown}
          placeholder="Provider"
          value={providerFilter}
          onOptionSelect={(_, data) => setProviderFilter(data.optionValue as string)}
        >
          <Option value="all">All Providers</Option>
          <Option value="Azure">Azure</Option>
          <Option value="ElevenLabs">ElevenLabs</Option>
          <Option value="PlayHT">PlayHT</Option>
        </Dropdown>
        <Dropdown
          className={styles.filterDropdown}
          placeholder="Gender"
          value={genderFilter}
          onOptionSelect={(_, data) => setGenderFilter(data.optionValue as string)}
        >
          <Option value="all">All Genders</Option>
          <Option value="Male">Male</Option>
          <Option value="Female">Female</Option>
          <Option value="Neutral">Neutral</Option>
        </Dropdown>
        <Dropdown
          className={styles.filterDropdown}
          placeholder="Language"
          value={localeFilter}
          onOptionSelect={(_, data) => setLocaleFilter(data.optionValue as string)}
        >
          <Option value="all">All Languages</Option>
          <Option value="en-US">English (US)</Option>
          <Option value="en-GB">English (UK)</Option>
          <Option value="es-ES">Spanish</Option>
        </Dropdown>
      </div>

      {filteredVoices.length === 0 ? (
        <div className={styles.emptyState}>
          <Text>No voices found matching your criteria</Text>
        </div>
      ) : (
        <div className={styles.voiceGrid}>
          {filteredVoices.map((voice) => (
            <Card
              key={voice.id}
              className={`${styles.voiceCard} ${
                selectedVoiceId === voice.id ? styles.selectedCard : ''
              }`}
              onClick={() => onVoiceSelect(voice.id)}
            >
              <div className={styles.voiceHeader}>
                <Text className={styles.voiceName}>{voice.name}</Text>
                <MicRegular />
              </div>
              <div className={styles.voiceInfo}>
                <Text size={200}>{voice.description}</Text>
                <div className={styles.voiceDetails}>
                  <Text>{voice.gender}</Text>
                  <Text>•</Text>
                  <Text>{voice.locale}</Text>
                </div>
                <div className={styles.badges}>
                  <Badge appearance="tint" color="brand">
                    {voice.provider}
                  </Badge>
                  <Badge appearance="outline">{voice.voiceType}</Badge>
                  {voice.availableStyles && voice.availableStyles.length > 0 && (
                    <Badge appearance="outline">{voice.availableStyles.length} styles</Badge>
                  )}
                </div>
              </div>
              <Button
                className={styles.playButton}
                appearance="subtle"
                size="small"
                icon={<PlayRegular />}
                onClick={(e) => handlePlaySample(voice.id, e)}
              >
                Play Sample
              </Button>
            </Card>
          ))}
        </div>
      )}
    </div>
  );
};
