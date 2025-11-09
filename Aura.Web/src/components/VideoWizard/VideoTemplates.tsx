import {
  makeStyles,
  tokens,
  Title2,
  Title3,
  Text,
  Card,
  CardHeader,
  CardPreview,
  Button,
  Badge,
  Input,
  Field,
  Dialog,
  DialogSurface,
  DialogBody,
  DialogTitle,
  DialogContent,
  DialogActions,
} from '@fluentui/react-components';
import {
  Video24Regular,
  Play24Regular,
  Star24Filled,
  DataTrendingRegular,
  Search24Regular,
  Dismiss24Regular,
} from '@fluentui/react-icons';
import { useState, useMemo } from 'react';
import type { FC } from 'react';
import type { VideoTemplate } from './types';

const useStyles = makeStyles({
  container: {
    display: 'flex',
    flexDirection: 'column',
    gap: tokens.spacingVerticalXL,
  },
  header: {
    display: 'flex',
    justifyContent: 'space-between',
    alignItems: 'center',
    marginBottom: tokens.spacingVerticalM,
  },
  searchBar: {
    marginBottom: tokens.spacingVerticalL,
  },
  categoryTabs: {
    display: 'flex',
    gap: tokens.spacingHorizontalM,
    marginBottom: tokens.spacingVerticalL,
    overflowX: 'auto',
    paddingBottom: tokens.spacingVerticalS,
  },
  categoryButton: {
    whiteSpace: 'nowrap',
  },
  grid: {
    display: 'grid',
    gridTemplateColumns: 'repeat(auto-fill, minmax(300px, 1fr))',
    gap: tokens.spacingHorizontalL,
  },
  card: {
    cursor: 'pointer',
    transition: 'all 0.2s ease',
    ':hover': {
      transform: 'translateY(-4px)',
      boxShadow: tokens.shadow16,
    },
  },
  cardPreview: {
    height: '180px',
    display: 'flex',
    alignItems: 'center',
    justifyContent: 'center',
    backgroundColor: tokens.colorNeutralBackground3,
    position: 'relative',
  },
  cardContent: {
    padding: tokens.spacingVerticalL,
    display: 'flex',
    flexDirection: 'column',
    gap: tokens.spacingVerticalM,
  },
  badges: {
    display: 'flex',
    gap: tokens.spacingHorizontalS,
    flexWrap: 'wrap',
  },
  playIcon: {
    fontSize: '48px',
    color: tokens.colorBrandForeground1,
    opacity: 0.8,
  },
  templateDetails: {
    display: 'flex',
    flexDirection: 'column',
    gap: tokens.spacingVerticalL,
  },
  requiredInputs: {
    padding: tokens.spacingVerticalM,
    backgroundColor: tokens.colorNeutralBackground2,
    borderRadius: tokens.borderRadiusMedium,
  },
  loadingContainer: {
    display: 'flex',
    justifyContent: 'center',
    alignItems: 'center',
    padding: tokens.spacingVerticalXXXL,
  },
});

interface VideoTemplatesProps {
  onSelectTemplate: (template: VideoTemplate) => void;
  onClose?: () => void;
}

const MOCK_TEMPLATES: VideoTemplate[] = [
  {
    id: 'edu-intro',
    name: 'Educational Introduction',
    category: 'Educational',
    description: 'Perfect for introducing complex topics in a simple, engaging way',
    isTrending: true,
    isFeatured: true,
    estimatedDuration: 120,
    requiredInputs: ['Topic', 'Target Audience', 'Key Concepts'],
    defaultData: {
      brief: {
        videoType: 'educational',
        targetAudience: 'Students and learners',
        duration: 120,
        topic: '',
        keyMessage: '',
      },
      style: {
        voiceProvider: 'ElevenLabs',
        voiceName: 'Professional',
        visualStyle: 'modern',
        musicGenre: 'ambient',
        musicEnabled: true,
      },
    },
  },
  {
    id: 'marketing-product',
    name: 'Product Showcase',
    category: 'Marketing',
    description: 'Highlight your product features and benefits professionally',
    isFeatured: true,
    estimatedDuration: 60,
    requiredInputs: ['Product Name', 'Key Features', 'Call to Action'],
    defaultData: {
      brief: {
        videoType: 'marketing',
        targetAudience: 'Potential customers',
        duration: 60,
        topic: '',
        keyMessage: '',
      },
      style: {
        voiceProvider: 'ElevenLabs',
        voiceName: 'Energetic',
        visualStyle: 'cinematic',
        musicGenre: 'upbeat',
        musicEnabled: true,
      },
    },
  },
  {
    id: 'social-tips',
    name: 'Quick Tips Video',
    category: 'Social',
    description: 'Fast-paced tips and tricks perfect for social media',
    isTrending: true,
    estimatedDuration: 30,
    requiredInputs: ['Topic', 'Number of Tips', 'Target Platform'],
    defaultData: {
      brief: {
        videoType: 'social',
        targetAudience: 'Social media followers',
        duration: 30,
        topic: '',
        keyMessage: '',
      },
      style: {
        voiceProvider: 'PlayHT',
        voiceName: 'Casual',
        visualStyle: 'playful',
        musicGenre: 'upbeat',
        musicEnabled: true,
      },
      advanced: {
        targetPlatform: 'tiktok',
        seoKeywords: [],
        customTransitions: true,
      },
    },
  },
  {
    id: 'story-narrative',
    name: 'Story Narrative',
    category: 'Story',
    description: 'Tell compelling stories with emotional depth',
    estimatedDuration: 180,
    requiredInputs: ['Story Theme', 'Characters', 'Message'],
    defaultData: {
      brief: {
        videoType: 'story',
        targetAudience: 'General audience',
        duration: 180,
        topic: '',
        keyMessage: '',
      },
      style: {
        voiceProvider: 'ElevenLabs',
        voiceName: 'Narrative',
        visualStyle: 'cinematic',
        musicGenre: 'dramatic',
        musicEnabled: true,
      },
    },
  },
  {
    id: 'marketing-testimonial',
    name: 'Customer Testimonial',
    category: 'Marketing',
    description: 'Showcase customer success stories and reviews',
    estimatedDuration: 90,
    requiredInputs: ['Customer Quote', 'Product/Service', 'Results'],
    defaultData: {
      brief: {
        videoType: 'marketing',
        targetAudience: 'Prospective customers',
        duration: 90,
        topic: '',
        keyMessage: '',
      },
      style: {
        voiceProvider: 'ElevenLabs',
        voiceName: 'Friendly',
        visualStyle: 'professional',
        musicGenre: 'ambient',
        musicEnabled: true,
      },
    },
  },
  {
    id: 'edu-tutorial',
    name: 'Step-by-Step Tutorial',
    category: 'Educational',
    description: 'Guide viewers through a process or skill',
    estimatedDuration: 240,
    requiredInputs: ['Process/Skill', 'Prerequisites', 'Steps'],
    defaultData: {
      brief: {
        videoType: 'tutorial',
        targetAudience: 'Learners',
        duration: 240,
        topic: '',
        keyMessage: '',
      },
      style: {
        voiceProvider: 'Windows',
        voiceName: 'Instructional',
        visualStyle: 'minimal',
        musicGenre: 'none',
        musicEnabled: false,
      },
    },
  },
];

export const VideoTemplates: FC<VideoTemplatesProps> = ({ onSelectTemplate, onClose }) => {
  const styles = useStyles();
  const [selectedCategory, setSelectedCategory] = useState<string>('All');
  const [searchQuery, setSearchQuery] = useState('');
  const [selectedTemplate, setSelectedTemplate] = useState<VideoTemplate | null>(null);

  const categories = useMemo(() => {
    return ['All', ...new Set(MOCK_TEMPLATES.map((t) => t.category))];
  }, []);

  const filteredTemplates = useMemo(() => {
    let templates = MOCK_TEMPLATES;

    if (selectedCategory !== 'All') {
      templates = templates.filter((t) => t.category === selectedCategory);
    }

    if (searchQuery) {
      const query = searchQuery.toLowerCase();
      templates = templates.filter(
        (t) =>
          t.name.toLowerCase().includes(query) ||
          t.description.toLowerCase().includes(query) ||
          t.category.toLowerCase().includes(query)
      );
    }

    return templates;
  }, [selectedCategory, searchQuery]);

  const trendingTemplates = useMemo(() => {
    return MOCK_TEMPLATES.filter((t) => t.isTrending);
  }, []);

  const featuredTemplates = useMemo(() => {
    return MOCK_TEMPLATES.filter((t) => t.isFeatured);
  }, []);

  const handleTemplateClick = (template: VideoTemplate) => {
    setSelectedTemplate(template);
  };

  const handleApplyTemplate = () => {
    if (selectedTemplate) {
      onSelectTemplate(selectedTemplate);
      setSelectedTemplate(null);
    }
  };

  return (
    <div className={styles.container}>
      <div className={styles.header}>
        <div>
          <Title2>Video Templates</Title2>
          <Text size={300} style={{ color: tokens.colorNeutralForeground3 }}>
            Start with a professional template and customize to your needs
          </Text>
        </div>
        {onClose && (
          <Button appearance="subtle" icon={<Dismiss24Regular />} onClick={onClose}>
            Close
          </Button>
        )}
      </div>

      <div className={styles.searchBar}>
        <Field>
          <Input
            placeholder="Search templates..."
            value={searchQuery}
            onChange={(_, data) => setSearchQuery(data.value)}
            contentBefore={<Search24Regular />}
            size="large"
          />
        </Field>
      </div>

      <div className={styles.categoryTabs}>
        {categories.map((category) => (
          <Button
            key={category}
            appearance={selectedCategory === category ? 'primary' : 'secondary'}
            className={styles.categoryButton}
            onClick={() => setSelectedCategory(category)}
          >
            {category}
          </Button>
        ))}
      </div>

      {trendingTemplates.length > 0 && selectedCategory === 'All' && !searchQuery && (
        <div>
          <Title3
            style={{
              marginBottom: tokens.spacingVerticalM,
              display: 'flex',
              alignItems: 'center',
              gap: tokens.spacingHorizontalS,
            }}
          >
            <DataTrendingRegular /> Trending Templates
          </Title3>
          <div className={styles.grid}>
            {trendingTemplates.map((template) => (
              <TemplateCard
                key={template.id}
                template={template}
                onClick={() => handleTemplateClick(template)}
              />
            ))}
          </div>
        </div>
      )}

      {featuredTemplates.length > 0 && selectedCategory === 'All' && !searchQuery && (
        <div>
          <Title3
            style={{
              marginBottom: tokens.spacingVerticalM,
              display: 'flex',
              alignItems: 'center',
              gap: tokens.spacingHorizontalS,
            }}
          >
            <Star24Filled style={{ color: tokens.colorPaletteYellowForeground1 }} /> Featured
          </Title3>
          <div className={styles.grid}>
            {featuredTemplates.map((template) => (
              <TemplateCard
                key={template.id}
                template={template}
                onClick={() => handleTemplateClick(template)}
              />
            ))}
          </div>
        </div>
      )}

      <div>
        <Title3 style={{ marginBottom: tokens.spacingVerticalM }}>
          {selectedCategory === 'All' ? 'All Templates' : selectedCategory}
        </Title3>
        <div className={styles.grid}>
          {filteredTemplates.map((template) => (
            <TemplateCard
              key={template.id}
              template={template}
              onClick={() => handleTemplateClick(template)}
            />
          ))}
        </div>
      </div>

      <Dialog
        open={selectedTemplate !== null}
        onOpenChange={(_, data) => !data.open && setSelectedTemplate(null)}
      >
        <DialogSurface style={{ maxWidth: '600px' }}>
          <DialogBody>
            <DialogTitle>{selectedTemplate?.name}</DialogTitle>
            <DialogContent>
              {selectedTemplate && (
                <div className={styles.templateDetails}>
                  <div>
                    <Text weight="semibold" size={300}>
                      Description
                    </Text>
                    <Text size={300} style={{ marginTop: tokens.spacingVerticalS }}>
                      {selectedTemplate.description}
                    </Text>
                  </div>

                  <div>
                    <Text weight="semibold" size={300}>
                      Category
                    </Text>
                    <Badge appearance="tint" style={{ marginTop: tokens.spacingVerticalS }}>
                      {selectedTemplate.category}
                    </Badge>
                  </div>

                  <div>
                    <Text weight="semibold" size={300}>
                      Estimated Duration
                    </Text>
                    <Text size={300} style={{ marginTop: tokens.spacingVerticalS }}>
                      {Math.floor(selectedTemplate.estimatedDuration / 60)} minutes{' '}
                      {selectedTemplate.estimatedDuration % 60} seconds
                    </Text>
                  </div>

                  <div className={styles.requiredInputs}>
                    <Text weight="semibold" size={300}>
                      Required Inputs
                    </Text>
                    <ul style={{ marginTop: tokens.spacingVerticalS, marginBottom: 0 }}>
                      {selectedTemplate.requiredInputs.map((input, index) => (
                        <li key={index}>
                          <Text size={200}>{input}</Text>
                        </li>
                      ))}
                    </ul>
                  </div>

                  {selectedTemplate.previewVideoUrl && (
                    <div>
                      <Text weight="semibold" size={300}>
                        Example Output
                      </Text>
                      <div
                        style={{
                          marginTop: tokens.spacingVerticalS,
                          padding: tokens.spacingVerticalL,
                          backgroundColor: tokens.colorNeutralBackground3,
                          borderRadius: tokens.borderRadiusMedium,
                          textAlign: 'center',
                        }}
                      >
                        <Video24Regular style={{ fontSize: '48px' }} />
                        <Text
                          size={200}
                          style={{ display: 'block', marginTop: tokens.spacingVerticalS }}
                        >
                          Preview video available
                        </Text>
                      </div>
                    </div>
                  )}
                </div>
              )}
            </DialogContent>
            <DialogActions>
              <Button appearance="secondary" onClick={() => setSelectedTemplate(null)}>
                Cancel
              </Button>
              <Button appearance="primary" onClick={handleApplyTemplate}>
                Apply Template
              </Button>
            </DialogActions>
          </DialogBody>
        </DialogSurface>
      </Dialog>
    </div>
  );
};

const TemplateCard: FC<{ template: VideoTemplate; onClick: () => void }> = ({
  template,
  onClick,
}) => {
  const styles = useStyles();

  return (
    <Card className={styles.card} onClick={onClick}>
      <CardPreview className={styles.cardPreview}>
        <Play24Regular className={styles.playIcon} />
        {template.isTrending && (
          <Badge
            appearance="filled"
            color="informative"
            style={{
              position: 'absolute',
              top: tokens.spacingVerticalS,
              right: tokens.spacingHorizontalS,
            }}
          >
            Trending
          </Badge>
        )}
        {template.isFeatured && (
          <Badge
            appearance="filled"
            color="warning"
            style={{
              position: 'absolute',
              top: tokens.spacingVerticalS,
              left: tokens.spacingHorizontalS,
            }}
            icon={<Star24Filled />}
          >
            Featured
          </Badge>
        )}
      </CardPreview>
      <div className={styles.cardContent}>
        <CardHeader
          header={
            <Text weight="semibold" size={400}>
              {template.name}
            </Text>
          }
          description={
            <Text size={200} style={{ color: tokens.colorNeutralForeground3 }}>
              {template.description}
            </Text>
          }
        />
        <div className={styles.badges}>
          <Badge appearance="tint">{template.category}</Badge>
          <Badge appearance="outline">
            ~{Math.floor(template.estimatedDuration / 60)}:
            {String(template.estimatedDuration % 60).padStart(2, '0')}
          </Badge>
        </div>
      </div>
    </Card>
  );
};
