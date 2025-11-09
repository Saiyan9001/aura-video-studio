/**
 * Wizard Projects Tab with search, filter, and sort functionality
 */

import {
  Card,
  Table,
  TableHeader,
  TableRow,
  TableHeaderCell,
  TableBody,
  TableCell,
  Text,
  Button,
  Input,
  Dropdown,
  Option,
  Menu,
  MenuTrigger,
  MenuPopover,
  MenuList,
  MenuItem,
  Badge,
  Title3,
  tokens,
  makeStyles,
} from '@fluentui/react-components';
import {
  Search20Regular,
  MoreVertical24Regular,
  Edit24Regular,
  DocumentCopy24Regular,
  Delete24Regular,
  ArrowDownload24Regular,
  ArrowUpload24Regular,
  FolderOpen24Regular,
} from '@fluentui/react-icons';
import { useState, useEffect, useCallback, useMemo } from 'react';
import type { FC } from 'react';
import { useNavigate } from 'react-router-dom';
import {
  getAllWizardProjects,
  deleteWizardProject,
  duplicateWizardProject,
  exportWizardProject,
  importWizardProject,
  downloadProjectExport,
  parseImportFile,
} from '../api/wizardProjects';
import type { WizardProjectListItem } from '../types/wizardProject';
import { SkeletonTable, ErrorState } from './Loading';
import { useNotifications } from './Notifications/Toasts';

const useStyles = makeStyles({
  container: {
    display: 'flex',
    flexDirection: 'column',
    gap: tokens.spacingVerticalL,
  },
  toolbar: {
    display: 'flex',
    gap: tokens.spacingHorizontalM,
    alignItems: 'center',
    flexWrap: 'wrap',
  },
  searchInput: {
    minWidth: '250px',
    flex: 1,
  },
  filterDropdown: {
    minWidth: '150px',
  },
  sortDropdown: {
    minWidth: '150px',
  },
  table: {
    width: '100%',
  },
  statusBadge: {
    padding: `${tokens.spacingVerticalXS} ${tokens.spacingHorizontalS}`,
    borderRadius: tokens.borderRadiusMedium,
    fontSize: '12px',
    fontWeight: 600,
  },
  statusDraft: {
    backgroundColor: tokens.colorPaletteYellowBackground2,
    color: tokens.colorPaletteYellowForeground2,
  },
  statusInProgress: {
    backgroundColor: tokens.colorPaletteBlueBackground2,
    color: tokens.colorPaletteBlueForeground2,
  },
  statusCompleted: {
    backgroundColor: tokens.colorPaletteGreenBackground2,
    color: tokens.colorPaletteGreenForeground1,
  },
  emptyState: {
    display: 'flex',
    flexDirection: 'column',
    alignItems: 'center',
    justifyContent: 'center',
    padding: tokens.spacingVerticalXXXL,
    gap: tokens.spacingVerticalL,
    color: tokens.colorNeutralForeground3,
  },
  pagination: {
    display: 'flex',
    justifyContent: 'center',
    gap: tokens.spacingHorizontalM,
    alignItems: 'center',
  },
});

type SortField = 'name' | 'updatedAt' | 'createdAt' | 'progressPercent';
type SortDirection = 'asc' | 'desc';
type StatusFilter = 'all' | 'draft' | 'inProgress' | 'completed';

interface WizardProjectsTabProps {
  itemsPerPage?: number;
}

export const WizardProjectsTab: FC<WizardProjectsTabProps> = ({ itemsPerPage = 20 }) => {
  const styles = useStyles();
  const navigate = useNavigate();
  const { showSuccessToast, showFailureToast } = useNotifications();

  const [projects, setProjects] = useState<WizardProjectListItem[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [actionInProgress, setActionInProgress] = useState<string | null>(null);

  // Search and filter state
  const [searchQuery, setSearchQuery] = useState('');
  const [statusFilter, setStatusFilter] = useState<StatusFilter>('all');
  const [sortField, setSortField] = useState<SortField>('updatedAt');
  const [sortDirection, setSortDirection] = useState<SortDirection>('desc');
  const [currentPage, setCurrentPage] = useState(1);

  // Load projects
  const loadProjects = useCallback(async () => {
    setLoading(true);
    setError(null);
    try {
      const allProjects = await getAllWizardProjects();
      setProjects(allProjects);
    } catch (err: unknown) {
      const error = err instanceof Error ? err : new Error(String(err));
      setError(error.message);
      showFailureToast({
        title: 'Load Failed',
        message: 'Failed to load wizard projects',
      });
    } finally {
      setLoading(false);
    }
  }, [showFailureToast]);

  useEffect(() => {
    loadProjects();
  }, [loadProjects]);

  // Filter and sort projects
  const filteredAndSortedProjects = useMemo(() => {
    let filtered = projects;

    // Apply search filter
    if (searchQuery.trim()) {
      const query = searchQuery.toLowerCase();
      filtered = filtered.filter(
        (p) =>
          p.name.toLowerCase().includes(query) ||
          (p.description && p.description.toLowerCase().includes(query))
      );
    }

    // Apply status filter
    if (statusFilter !== 'all') {
      filtered = filtered.filter((p) => {
        if (statusFilter === 'draft') return p.status === 'Draft';
        if (statusFilter === 'inProgress') return p.status === 'InProgress';
        if (statusFilter === 'completed') return p.status === 'Completed';
        return true;
      });
    }

    // Sort projects
    filtered.sort((a, b) => {
      let comparison = 0;
      if (sortField === 'name') {
        comparison = a.name.localeCompare(b.name);
      } else if (sortField === 'updatedAt') {
        comparison = new Date(a.updatedAt).getTime() - new Date(b.updatedAt).getTime();
      } else if (sortField === 'createdAt') {
        comparison = new Date(a.createdAt).getTime() - new Date(b.createdAt).getTime();
      } else if (sortField === 'progressPercent') {
        comparison = a.progressPercent - b.progressPercent;
      }
      return sortDirection === 'asc' ? comparison : -comparison;
    });

    return filtered;
  }, [projects, searchQuery, statusFilter, sortField, sortDirection]);

  // Pagination
  const totalPages = Math.ceil(filteredAndSortedProjects.length / itemsPerPage);
  const paginatedProjects = useMemo(() => {
    const startIndex = (currentPage - 1) * itemsPerPage;
    return filteredAndSortedProjects.slice(startIndex, startIndex + itemsPerPage);
  }, [filteredAndSortedProjects, currentPage, itemsPerPage]);

  // Reset to first page when filters change
  useEffect(() => {
    setCurrentPage(1);
  }, [searchQuery, statusFilter, sortField, sortDirection]);

  // Handlers
  const handleOpenProject = useCallback(
    (projectId: string) => {
      navigate(`/wizard?projectId=${projectId}`);
    },
    [navigate]
  );

  const handleDuplicate = useCallback(
    async (project: WizardProjectListItem) => {
      setActionInProgress(project.id);
      try {
        await duplicateWizardProject(project.id, { newName: `${project.name} (Copy)` });
        showSuccessToast({
          title: 'Success',
          message: 'Project duplicated successfully',
        });
        await loadProjects();
      } catch (error: unknown) {
        const err = error instanceof Error ? error : new Error(String(error));
        showFailureToast({
          title: 'Duplicate Failed',
          message: err.message,
        });
      } finally {
        setActionInProgress(null);
      }
    },
    [showSuccessToast, showFailureToast, loadProjects]
  );

  const handleExport = useCallback(
    async (project: WizardProjectListItem) => {
      setActionInProgress(project.id);
      try {
        const projectJson = await exportWizardProject(project.id);
        downloadProjectExport(projectJson, project.name);
        showSuccessToast({
          title: 'Success',
          message: 'Project exported successfully',
        });
      } catch (error: unknown) {
        const err = error instanceof Error ? error : new Error(String(error));
        showFailureToast({
          title: 'Export Failed',
          message: err.message,
        });
      } finally {
        setActionInProgress(null);
      }
    },
    [showSuccessToast, showFailureToast]
  );

  const handleImport = useCallback(async () => {
    const input = document.createElement('input');
    input.type = 'file';
    input.accept = '.json';
    input.onchange = async (e) => {
      const file = (e.target as HTMLInputElement).files?.[0];
      if (!file) return;

      try {
        const projectData = await parseImportFile(file);
        await importWizardProject({ projectJson: JSON.stringify(projectData) });
        showSuccessToast({
          title: 'Success',
          message: 'Project imported successfully',
        });
        await loadProjects();
      } catch (error: unknown) {
        const err = error instanceof Error ? error : new Error(String(error));
        showFailureToast({
          title: 'Import Failed',
          message: err.message,
        });
      }
    };
    input.click();
  }, [showSuccessToast, showFailureToast, loadProjects]);

  const handleDelete = useCallback(
    async (project: WizardProjectListItem) => {
      if (!confirm(`Are you sure you want to delete "${project.name}"?`)) {
        return;
      }

      setActionInProgress(project.id);
      try {
        await deleteWizardProject(project.id);
        showSuccessToast({
          title: 'Success',
          message: 'Project deleted successfully',
        });
        await loadProjects();
      } catch (error: unknown) {
        const err = error instanceof Error ? error : new Error(String(error));
        showFailureToast({
          title: 'Delete Failed',
          message: err.message,
        });
      } finally {
        setActionInProgress(null);
      }
    },
    [showSuccessToast, showFailureToast, loadProjects]
  );

  const getStatusBadge = (status: string) => {
    const statusLower = status.toLowerCase();
    if (statusLower === 'draft') {
      return (
        <Badge appearance="filled" color="warning">
          Draft
        </Badge>
      );
    } else if (statusLower === 'inprogress') {
      return (
        <Badge appearance="filled" color="informative">
          In Progress
        </Badge>
      );
    } else if (statusLower === 'completed') {
      return (
        <Badge appearance="filled" color="success">
          Completed
        </Badge>
      );
    }
    return <Badge>{status}</Badge>;
  };

  const formatDate = (dateStr: string) => {
    const date = new Date(dateStr);
    return date.toLocaleString();
  };

  if (loading) {
    return (
      <Card>
        <SkeletonTable
          columns={['Name', 'Status', 'Progress', 'Last Modified', 'Actions']}
          rowCount={5}
          columnWidths={['30%', '15%', '15%', '20%', '20%']}
          ariaLabel="Loading wizard projects"
        />
      </Card>
    );
  }

  if (error) {
    return (
      <ErrorState
        title="Failed to load projects"
        message={error}
        onRetry={loadProjects}
        withCard={true}
      />
    );
  }

  return (
    <div className={styles.container}>
      {/* Toolbar */}
      <div className={styles.toolbar}>
        <Input
          className={styles.searchInput}
          placeholder="Search projects..."
          value={searchQuery}
          onChange={(_, data) => setSearchQuery(data.value)}
          contentBefore={<Search20Regular />}
        />
        <Dropdown
          className={styles.filterDropdown}
          placeholder="Filter by status"
          value={statusFilter}
          selectedOptions={[statusFilter]}
          onOptionSelect={(_, data) => setStatusFilter(data.optionValue as StatusFilter)}
        >
          <Option value="all">All Statuses</Option>
          <Option value="draft">Draft</Option>
          <Option value="inProgress">In Progress</Option>
          <Option value="completed">Completed</Option>
        </Dropdown>
        <Dropdown
          className={styles.sortDropdown}
          placeholder="Sort by"
          value={`${sortField}-${sortDirection}`}
          selectedOptions={[`${sortField}-${sortDirection}`]}
          onOptionSelect={(_, data) => {
            const [field, direction] = (data.optionValue as string).split('-');
            setSortField(field as SortField);
            setSortDirection(direction as SortDirection);
          }}
        >
          <Option value="updatedAt-desc">Last Modified (Newest)</Option>
          <Option value="updatedAt-asc">Last Modified (Oldest)</Option>
          <Option value="name-asc">Name (A-Z)</Option>
          <Option value="name-desc">Name (Z-A)</Option>
          <Option value="createdAt-desc">Created (Newest)</Option>
          <Option value="createdAt-asc">Created (Oldest)</Option>
          <Option value="progressPercent-desc">Progress (High to Low)</Option>
          <Option value="progressPercent-asc">Progress (Low to High)</Option>
        </Dropdown>
        <Button appearance="primary" icon={<ArrowUpload24Regular />} onClick={handleImport}>
          Import Project
        </Button>
      </div>

      {/* Projects Table */}
      {filteredAndSortedProjects.length === 0 ? (
        <Card>
          <div className={styles.emptyState}>
            <FolderOpen24Regular style={{ fontSize: '64px' }} />
            <Title3>No projects found</Title3>
            <Text>
              {searchQuery || statusFilter !== 'all'
                ? 'Try adjusting your search or filters'
                : 'Create your first wizard project to get started'}
            </Text>
          </div>
        </Card>
      ) : (
        <>
          <Card>
            <Table className={styles.table}>
              <TableHeader>
                <TableRow>
                  <TableHeaderCell>Name</TableHeaderCell>
                  <TableHeaderCell>Status</TableHeaderCell>
                  <TableHeaderCell>Progress</TableHeaderCell>
                  <TableHeaderCell>Last Modified</TableHeaderCell>
                  <TableHeaderCell>Actions</TableHeaderCell>
                </TableRow>
              </TableHeader>
              <TableBody>
                {paginatedProjects.map((project) => (
                  <TableRow
                    key={project.id}
                    style={{ cursor: 'pointer' }}
                    onClick={() => handleOpenProject(project.id)}
                  >
                    <TableCell>
                      <Text weight="semibold">{project.name}</Text>
                      {project.description && (
                        <div>
                          <Text size={200} style={{ color: tokens.colorNeutralForeground3 }}>
                            {project.description}
                          </Text>
                        </div>
                      )}
                    </TableCell>
                    <TableCell>{getStatusBadge(project.status)}</TableCell>
                    <TableCell>
                      <Text>{project.progressPercent}%</Text>
                    </TableCell>
                    <TableCell>{formatDate(project.updatedAt)}</TableCell>
                    <TableCell>
                      <div
                        style={{ display: 'flex', gap: tokens.spacingHorizontalS }}
                        onClick={(e) => e.stopPropagation()}
                      >
                        <Button
                          size="small"
                          appearance="primary"
                          icon={<Edit24Regular />}
                          onClick={() => handleOpenProject(project.id)}
                          disabled={actionInProgress === project.id}
                        >
                          Open
                        </Button>
                        <Menu>
                          <MenuTrigger>
                            <Button
                              size="small"
                              appearance="subtle"
                              icon={<MoreVertical24Regular />}
                              disabled={actionInProgress === project.id}
                            />
                          </MenuTrigger>
                          <MenuPopover>
                            <MenuList>
                              <MenuItem
                                icon={<DocumentCopy24Regular />}
                                onClick={() => handleDuplicate(project)}
                              >
                                Duplicate
                              </MenuItem>
                              <MenuItem
                                icon={<ArrowDownload24Regular />}
                                onClick={() => handleExport(project)}
                              >
                                Export
                              </MenuItem>
                              <MenuItem
                                icon={<Delete24Regular />}
                                onClick={() => handleDelete(project)}
                              >
                                Delete
                              </MenuItem>
                            </MenuList>
                          </MenuPopover>
                        </Menu>
                      </div>
                    </TableCell>
                  </TableRow>
                ))}
              </TableBody>
            </Table>
          </Card>

          {/* Pagination */}
          {totalPages > 1 && (
            <div className={styles.pagination}>
              <Button
                appearance="subtle"
                onClick={() => setCurrentPage((p) => Math.max(1, p - 1))}
                disabled={currentPage === 1}
              >
                Previous
              </Button>
              <Text>
                Page {currentPage} of {totalPages} ({filteredAndSortedProjects.length} projects)
              </Text>
              <Button
                appearance="subtle"
                onClick={() => setCurrentPage((p) => Math.min(totalPages, p + 1))}
                disabled={currentPage === totalPages}
              >
                Next
              </Button>
            </div>
          )}
        </>
      )}
    </div>
  );
};
