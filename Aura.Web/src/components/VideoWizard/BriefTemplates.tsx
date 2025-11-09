import {
  makeStyles,
  tokens,
  Card,
  CardHeader,
  CardPreview,
  Button,
  Text,
  Badge,
  Spinner,
  Title3,
  Body1,
} from '@fluentui/react-components';
import {
  Lightbulb24Regular,
  ShoppingBag24Regular,
  BookOpen24Regular,
  ShareAndroid24Regular,
  Briefcase24Regular,
  BeakerSettingsRegular,
} from '@fluentui/react-icons';
import { useState, useEffect } from 'react';
import type { FC } from 'react';
import { apiUrl } from '../../config/api';

const useStyles = makeStyles({
  container: {
    display: 'flex',
    flexDirection: 'column',
    gap: tokens.spacingVerticalL,
  },
  header: {
    marginBottom: tokens.spacingVerticalM,
  },
  grid: {
    display: 'grid',
    gridTemplateColumns: 'repeat(auto-fill, minmax(280px, 1fr))',
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
  cardContent: {
    padding: tokens.spacingVerticalL,
    display: 'flex',
    flexDirection: 'column',
    gap: tokens.spacingVerticalM,
  },
  cardHeader: {
    display: 'flex',
    alignItems: 'center',
    gap: tokens.spacingHorizontalM,
  },
  icon: {
    fontSize: '32px',
    color: tokens.colorBrandForeground1,
  },
  categoryBadge: {
    marginTop: tokens.spacingVerticalS,
  },
  description: {
    color: tokens.colorNeutralForeground3,
  },
  loadingContainer: {
    display: 'flex',
    justifyContent: 'center',
    alignItems: 'center',
    padding: tokens.spacingVerticalXXL,
  },
  errorText: {
    color: tokens.colorPaletteRedForeground1,
  },
});

interface BriefTemplate {
  id: string;
  name: string;
  category: string;
  description: string;
  icon: string;
  brief: {
    topic: string;
    audience: string;
    goal: string;
    tone: string;
    language: string;
    duration: number;
    keyPoints: string[];
  };
  settings: {
    aspect: string;
    quality: string;
    useLocalResourcesOnly?: boolean;
  };
}

interface BriefTemplatesProps {
  onSelectTemplate: (template: BriefTemplate) => void;
  selectedTemplateId?: string;
}

const iconMap: Record<string, JSX.Element> = {
  Lightbulb: <Lightbulb24Regular />,
  ShoppingBag: <ShoppingBag24Regular />,
  BookOpen: <BookOpen24Regular />,
  ShareAndroid: <ShareAndroid24Regular />,
  Briefcase: <Briefcase24Regular />,
  BeakerSettings: <BeakerSettingsRegular />,
};

export const BriefTemplates: FC<BriefTemplatesProps> = ({
  onSelectTemplate,
  selectedTemplateId,
}) => {
  const styles = useStyles();
  const [templates, setTemplates] = useState<BriefTemplate[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    loadTemplates();
  }, []);

  const loadTemplates = async () => {
    try {
      setLoading(true);
      setError(null);

      const response = await fetch(`${apiUrl}/api/assets/samples/templates/briefs`);

      if (!response.ok) {
        throw new Error('Failed to load brief templates');
      }

      const data = await response.json();
      setTemplates(data);
    } catch (err) {
      const errorMessage = err instanceof Error ? err.message : 'Failed to load templates';
      setError(errorMessage);
      console.error('Error loading brief templates:', err);
    } finally {
      setLoading(false);
    }
  };

  if (loading) {
    return (
      <div className={styles.loadingContainer}>
        <Spinner label="Loading templates..." />
      </div>
    );
  }

  if (error) {
    return (
      <div className={styles.container}>
        <Text className={styles.errorText}>{error}</Text>
        <Button onClick={loadTemplates}>Retry</Button>
      </div>
    );
  }

  return (
    <div className={styles.container}>
      <div className={styles.header}>
        <Title3>Choose a Template</Title3>
        <Body1>Start with a pre-configured brief template or create your own</Body1>
      </div>

      <div className={styles.grid}>
        {templates.map((template) => (
          <Card
            key={template.id}
            className={styles.card}
            onClick={() => onSelectTemplate(template)}
            selected={selectedTemplateId === template.id}
          >
            <CardHeader
              header={
                <div className={styles.cardHeader}>
                  <div className={styles.icon}>
                    {iconMap[template.icon] || <Lightbulb24Regular />}
                  </div>
                  <div>
                    <Text weight="semibold">{template.name}</Text>
                    <Badge className={styles.categoryBadge} appearance="outline">
                      {template.category}
                    </Badge>
                  </div>
                </div>
              }
            />
            <CardPreview>
              <div className={styles.cardContent}>
                <Text className={styles.description}>{template.description}</Text>
                <Text size={200}>
                  Duration: {template.brief.duration}s | Aspect: {template.settings.aspect}
                </Text>
              </div>
            </CardPreview>
          </Card>
        ))}
      </div>
    </div>
  );
};

export default BriefTemplates;
