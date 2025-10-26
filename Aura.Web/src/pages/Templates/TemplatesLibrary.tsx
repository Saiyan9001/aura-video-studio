/**
 * Templates Library page for browsing and selecting project templates
 */

import {
  makeStyles,
  tokens,
  Text,
  Title1,
  Title3,
  Button,
  TabList,
  Tab,
  Input,
  Spinner,
  MessageBar,
  MessageBarBody,
  Dialog,
  DialogSurface,
  DialogTitle,
  DialogBody,
  DialogActions,
  DialogContent,
  Field,
} from '@fluentui/react-components';
import { Search24Regular, Add24Regular, VideoClip24Regular } from '@fluentui/react-icons';
import { useState, useEffect, useCallback } from 'react';
import { useNavigate } from 'react-router-dom';
import { TemplateCard } from '../../components/Templates/TemplateCard';
import { TemplatePreview } from '../../components/Templates/TemplatePreview';
import {
  getTemplates,
  createFromTemplate,
  seedSampleTemplates,
} from '../../services/templatesService';
import { TemplateListItem, TemplateCategory } from '../../types/templates';

const useStyles = makeStyles({
  container: {
    maxWidth: '1400px',
    margin: '0 auto',
    padding: tokens.spacingVerticalL,
  },
  header: {
    display: 'flex',
    justifyContent: 'space-between',
    alignItems: 'center',
    marginBottom: tokens.spacingVerticalXXL,
  },
  controls: {
    display: 'flex',
    gap: tokens.spacingHorizontalL,
    alignItems: 'center',
    marginBottom: tokens.spacingVerticalL,
  },
  searchBox: {
    width: '300px',
  },
  categoryTabs: {
    marginBottom: tokens.spacingVerticalL,
  },
  section: {
    marginBottom: tokens.spacingVerticalXXL,
  },
  sectionTitle: {
    marginBottom: tokens.spacingVerticalL,
  },
  grid: {
    display: 'grid',
    gridTemplateColumns: 'repeat(auto-fill, minmax(280px, 1fr))',
    gap: tokens.spacingVerticalL,
  },
  emptyState: {
    display: 'flex',
    flexDirection: 'column',
    alignItems: 'center',
    justifyContent: 'center',
    padding: tokens.spacingVerticalXXXL,
    gap: tokens.spacingVerticalL,
    color: tokens.colorNeutralForeground3,
    textAlign: 'center',
  },
  loadingContainer: {
    display: 'flex',
    justifyContent: 'center',
    alignItems: 'center',
    padding: tokens.spacingVerticalXXXL,
  },
  messageBar: {
    marginBottom: tokens.spacingVerticalL,
  },
});

export default function TemplatesLibrary() {
  const styles = useStyles();
  const navigate = useNavigate();

  const [templates, setTemplates] = useState<TemplateListItem[]>([]);
  const [filteredTemplates, setFilteredTemplates] = useState<TemplateListItem[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [searchQuery, setSearchQuery] = useState('');
  const [selectedCategory, setSelectedCategory] = useState<string>('all');
  const [previewTemplate, setPreviewTemplate] = useState<TemplateListItem | null>(null);
  const [showPreview, setShowPreview] = useState(false);
  const [showNewProjectDialog, setShowNewProjectDialog] = useState(false);
  const [selectedTemplate, setSelectedTemplate] = useState<TemplateListItem | null>(null);
  const [projectName, setProjectName] = useState('');
  const [creating, setCreating] = useState(false);

  const loadTemplates = async () => {
    try {
      setLoading(true);
      setError(null);
      const data = await getTemplates();

      // If no templates, seed sample templates
      if (data.length === 0) {
        await seedSampleTemplates();
        const seededData = await getTemplates();
        setTemplates(seededData);
      } else {
        setTemplates(data);
      }
    } catch (err) {
      setError('Failed to load templates');
      console.error('Error loading templates:', err);
    } finally {
      setLoading(false);
    }
  };

  const filterTemplates = useCallback(() => {
    let filtered = [...templates];

    // Filter by category
    if (selectedCategory !== 'all') {
      filtered = filtered.filter((t) => t.category === selectedCategory);
    }

    // Filter by search query
    if (searchQuery) {
      const query = searchQuery.toLowerCase();
      filtered = filtered.filter(
        (t) =>
          t.name.toLowerCase().includes(query) ||
          t.description.toLowerCase().includes(query) ||
          t.tags.some((tag) => tag.toLowerCase().includes(query))
      );
    }

    setFilteredTemplates(filtered);
  }, [templates, searchQuery, selectedCategory]);

  useEffect(() => {
    loadTemplates();
  }, []);

  useEffect(() => {
    filterTemplates();
  }, [filterTemplates]);

  const handleTemplateClick = (template: TemplateListItem) => {
    setSelectedTemplate(template);
    setProjectName(`${template.name} - ${new Date().toLocaleDateString()}`);
    setShowNewProjectDialog(true);
  };

  const handlePreview = (template: TemplateListItem) => {
    setPreviewTemplate(template);
    setShowPreview(true);
  };

  const handleUseTemplate = async () => {
    if (!selectedTemplate || !projectName.trim()) return;

    try {
      setCreating(true);
      const result = await createFromTemplate({
        templateId: selectedTemplate.id,
        projectName: projectName.trim(),
      });

      // Navigate to editor with the new project
      navigate('/editor', { state: { projectFile: result.projectFile } });
    } catch (err) {
      setError('Failed to create project from template');
      console.error('Error creating project:', err);
    } finally {
      setCreating(false);
      setShowNewProjectDialog(false);
    }
  };

  // Group templates by subcategory
  const groupedTemplates = filteredTemplates.reduce(
    (acc, template) => {
      const key = template.subCategory || 'Other';
      if (!acc[key]) {
        acc[key] = [];
      }
      acc[key].push(template);
      return acc;
    },
    {} as Record<string, TemplateListItem[]>
  );

  if (loading) {
    return (
      <div className={styles.container}>
        <div className={styles.loadingContainer}>
          <Spinner size="large" label="Loading templates..." />
        </div>
      </div>
    );
  }

  return (
    <div className={styles.container}>
      <div className={styles.header}>
        <div>
          <Title1>Templates Library</Title1>
          <Text>Get started quickly with pre-built project templates</Text>
        </div>
        <Button appearance="primary" icon={<Add24Regular />}>
          Save as Template
        </Button>
      </div>

      {error && (
        <MessageBar intent="error" className={styles.messageBar}>
          <MessageBarBody>{error}</MessageBarBody>
        </MessageBar>
      )}

      <div className={styles.controls}>
        <Input
          className={styles.searchBox}
          placeholder="Search templates..."
          value={searchQuery}
          onChange={(_, data) => setSearchQuery(data.value)}
          contentBefore={<Search24Regular />}
        />
      </div>

      <TabList
        className={styles.categoryTabs}
        selectedValue={selectedCategory}
        onTabSelect={(_, data) => setSelectedCategory(data.value as string)}
      >
        <Tab value="all">All Templates</Tab>
        <Tab value={TemplateCategory.YouTube}>YouTube</Tab>
        <Tab value={TemplateCategory.SocialMedia}>Social Media</Tab>
        <Tab value={TemplateCategory.Business}>Business</Tab>
        <Tab value={TemplateCategory.Creative}>Creative</Tab>
      </TabList>

      {filteredTemplates.length === 0 ? (
        <div className={styles.emptyState}>
          <VideoClip24Regular style={{ fontSize: '48px' }} />
          <Title3>No templates found</Title3>
          <Text>Try adjusting your search or category filter</Text>
        </div>
      ) : (
        Object.entries(groupedTemplates).map(([subcategory, items]) => (
          <div key={subcategory} className={styles.section}>
            <Title3 className={styles.sectionTitle}>{subcategory}</Title3>
            <div className={styles.grid}>
              {items.map((template) => (
                <TemplateCard
                  key={template.id}
                  template={template}
                  onClick={handleTemplateClick}
                  onPreview={handlePreview}
                />
              ))}
            </div>
          </div>
        ))
      )}

      <TemplatePreview
        template={previewTemplate}
        open={showPreview}
        onClose={() => setShowPreview(false)}
        onUseTemplate={(template) => {
          setShowPreview(false);
          handleTemplateClick(template);
        }}
      />

      <Dialog
        open={showNewProjectDialog}
        onOpenChange={(_, data) => !data.open && setShowNewProjectDialog(false)}
      >
        <DialogSurface>
          <DialogBody>
            <DialogTitle>Create Project from Template</DialogTitle>
            <DialogContent>
              <Field label="Project Name" required>
                <Input
                  value={projectName}
                  onChange={(_, data) => setProjectName(data.value)}
                  placeholder="Enter project name"
                  disabled={creating}
                />
              </Field>
              {selectedTemplate && (
                <Text>
                  Template: <strong>{selectedTemplate.name}</strong>
                </Text>
              )}
            </DialogContent>
            <DialogActions>
              <Button
                appearance="secondary"
                onClick={() => setShowNewProjectDialog(false)}
                disabled={creating}
              >
                Cancel
              </Button>
              <Button
                appearance="primary"
                onClick={handleUseTemplate}
                disabled={!projectName.trim() || creating}
              >
                {creating ? <Spinner size="tiny" /> : 'Create Project'}
              </Button>
            </DialogActions>
          </DialogBody>
        </DialogSurface>
      </Dialog>
    </div>
  );
}
