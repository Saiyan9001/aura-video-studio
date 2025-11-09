# Wizard Project Integration Guide

This guide describes how wizard project save/load functionality is integrated into the application, based on PR #81.

## Overview

The wizard project persistence feature allows users to save their work at any step in the video creation wizard, reload projects later, and manage projects from the dashboard.

## Architecture

### Backend

**Service**: `Aura.Core/Services/WizardProjectService.cs`
- Provides CRUD operations for wizard projects
- Handles JSON serialization/deserialization of wizard state
- Supports project duplication, export/import, and content clearing

**Controller**: `Aura.Api/Controllers/WizardProjectsController.cs`
- RESTful API endpoints at `/api/wizard-projects`
- Proper error handling with ProblemDetails
- Correlation ID tracking for debugging

**Database**: 
- Entity: `ProjectStateEntity` with `CurrentWizardStep` field
- Migration: `20251108184353_AddWizardProjectManagement.cs`

### Frontend

**State Management**: `Aura.Web/src/state/wizardProject.ts`
- Zustand store for project state
- Helper functions for serialization/deserialization
- Auto-save configuration support

**API Client**: `Aura.Web/src/api/wizardProjects.ts`
- Typed API client matching backend DTOs
- Full CRUD operation coverage

**Components**:
- `SaveProjectDialog.tsx` - Modal for saving projects
- `RecentProjectsList.tsx` - Card list displaying recent projects

## Integration Points

### CreateWizard (Video Creation Page)

**Location**: `Aura.Web/src/pages/Wizard/CreateWizard.tsx`

**Features**:
1. **Save Button**: Added to header (top-right) to open SaveProjectDialog
2. **Project Loading**: Reads `projectId` URL parameter to load saved projects
3. **State Restoration**: Restores brief, plan, and current step from loaded project
4. **Toast Notifications**: Success/error feedback for save and load operations

**Usage Flow**:
```
User clicks "Save Project" → SaveProjectDialog opens → 
Enter name → Save → Toast notification → Project ID stored
```

**Load Flow**:
```
Navigate to /create?projectId=xyz → useEffect detects param → 
Load project → Restore wizard state → Show success toast
```

### Dashboard

**Location**: `Aura.Web/src/components/dashboard/Dashboard.tsx`

**Features**:
1. **Recent Projects Section**: Displays up to 5 most recent projects
2. **Project Cards**: Show name, description, progress, last modified time
3. **Actions Menu**: Open, duplicate, export, delete via context menu
4. **Navigation**: Clicking "Open" navigates to `/create?projectId={id}`

**Display Location**: After "Quick Start" section, before "Recent Briefs"

## API Endpoints

| Method | Endpoint | Description |
|--------|----------|-------------|
| POST | `/api/wizard-projects` | Save/update project |
| GET | `/api/wizard-projects/{id}` | Get project details |
| GET | `/api/wizard-projects` | Get all projects |
| GET | `/api/wizard-projects/recent?count=5` | Get recent projects |
| POST | `/api/wizard-projects/{id}/duplicate` | Clone project |
| DELETE | `/api/wizard-projects/{id}` | Soft delete project |
| GET | `/api/wizard-projects/{id}/export` | Export as JSON |
| POST | `/api/wizard-projects/import` | Import from JSON |
| POST | `/api/wizard-projects/{id}/clear-content` | Clear generated assets |

## Data Flow

### Saving a Project

```
CreateWizard → User clicks "Save Project" → SaveProjectDialog
  ↓
Wizard state (brief, planSpec) serialized to JSON
  ↓
saveWizardProject() API call → POST /api/wizard-projects
  ↓
WizardProjectService.SaveProjectAsync() → Database
  ↓
Response with project ID → Success toast → Dialog closes
```

### Loading a Project

```
Dashboard → User clicks "Open" on project card
  ↓
Navigate to /create?projectId={id}
  ↓
CreateWizard useEffect detects projectId param
  ↓
getWizardProject(id) → GET /api/wizard-projects/{id}
  ↓
deserializeWizardState() → Restore brief, planSpec
  ↓
Set wizard state, current step → Success toast
```

## Testing

**Frontend Tests**: `Aura.Web/src/api/__tests__/wizardProjects.test.ts`
- 12 comprehensive unit tests
- Cover all API operations
- Mock API client for isolation

**Backend Tests**: Not yet implemented
- Add tests in `Aura.Tests/`
- Test WizardProjectService methods
- Test API endpoints with WebApplicationFactory

## Future Enhancements

1. **Auto-save**: Timer-based background saving every 2 minutes
2. **Project Templates**: Save projects as reusable templates
3. **Project Versioning**: Track changes and allow rollback
4. **Collaboration**: Share projects with other users
5. **Project Analytics**: Track which projects get generated most

## Troubleshooting

### Project won't load
- Check browser console for errors
- Verify projectId is valid GUID
- Check backend logs for API errors
- Ensure database migration was applied

### Save dialog doesn't appear
- Verify SaveProjectDialog is imported correctly
- Check saveDialogOpen state in CreateWizard
- Look for console errors in browser

### Projects don't show on dashboard
- Check RecentProjectsList is rendered
- Verify API endpoint returns data
- Check browser network tab for failed requests

## Code Quality

- ✅ Zero placeholder policy enforced
- ✅ TypeScript strict mode enabled
- ✅ All components production-ready
- ✅ Error handling implemented
- ✅ Correlation IDs for debugging
- ✅ Structured logging
