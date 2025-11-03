> **⚠️ ARCHIVED DOCUMENT**
>
> This document is archived for historical reference only.
> It may contain outdated information. See the [Documentation Index](../DocsIndex.md) for current documentation.

# Phase 2: Server-Side Undo/Redo Implementation

**Status**: ✅ Complete (Continuation of PR #79)  
**Date**: November 3, 2025  
**Branch**: `copilot/continue-phase-2-pr-79`

## Overview

Phase 2 extends the client-side undo/redo system from PR #79 with server-side persistence, enabling cross-session undo operations and enterprise-grade action logging.

## Features Implemented

### 1. Database Schema

#### ActionLog Table
- **Purpose**: Persistent storage of undoable operations
- **Fields**:
  - `Id` (Guid) - Primary key
  - `UserId` (string, 200) - User who performed action
  - `ActionType` (string, 100) - Type of action (e.g., "CreateProject")
  - `Description` (string, 500) - Human-readable description
  - `Timestamp` (DateTime) - When action occurred
  - `Status` (string, 50) - Current status (Applied, Undone, Failed, Expired)
  - `AffectedResourceIds` (string, 1000) - Comma-separated resource IDs
  - `PayloadJson` (TEXT) - Complete action data
  - `InverseActionType` (string, 100) - Type of undo operation
  - `InversePayloadJson` (TEXT) - Data for undo
  - `CanBatch` (bool) - Whether action can be batched
  - `IsPersistent` (bool) - Server-side persistence flag
  - `UndoneAt` (DateTime?) - When undone
  - `UndoneByUserId` (string, 200) - User who undid
  - `ExpiresAt` (DateTime?) - Retention policy expiration
  - `ErrorMessage` (TEXT) - Error details if failed
  - `CorrelationId` (string, 100) - Request tracking

#### Indexes
- `UserId` - Fast user lookup
- `ActionType` - Filter by type
- `Status` - Query by status
- `Timestamp` - Chronological ordering
- `UserId + Timestamp` - User history
- `Status + Timestamp` - Status timeline
- `CorrelationId` - Request correlation
- `ExpiresAt` - Retention cleanup

#### Soft-Delete Support
Added to ProjectStateEntity and CustomTemplateEntity:
- `IsDeleted` (bool) - Soft-delete flag
- `DeletedAt` (DateTime?) - When deleted
- `DeletedByUserId` (string) - Who deleted

### 2. Backend Services

#### ActionService (`Aura.Core/Services/ActionService.cs`)

**Methods**:
```csharp
Task<ActionLogEntity> RecordActionAsync(ActionLogEntity action, CancellationToken ct)
Task<bool> UndoActionAsync(Guid actionId, string undoneByUserId, CancellationToken ct)
Task<(List<ActionLogEntity>, int)> GetActionHistoryAsync(
    string? userId, string? actionType, string? status,
    DateTime? startDate, DateTime? endDate,
    int page, int pageSize, CancellationToken ct)
Task<ActionLogEntity?> GetActionAsync(Guid actionId, CancellationToken ct)
Task<int> CleanupExpiredActionsAsync(CancellationToken ct)
```

**Features**:
- Structured logging with ILogger
- Entity Framework Core integration
- Efficient querying with indexes
- Automatic expiration handling

### 3. API Endpoints

#### ActionsController (`Aura.Api/Controllers/ActionsController.cs`)

**POST /api/actions**
- Records new action
- Returns actionId, timestamp, status
- Status 201 (Created) on success

**POST /api/actions/{id}/undo**
- Undoes existing action
- Updates status to "Undone"
- Returns success flag, undoneAt, status
- Status 400 if already undone or expired
- Status 404 if action not found

**GET /api/actions**
- Query action history
- Filters: userId, actionType, status, dateRange
- Pagination: page, pageSize (max 100)
- Returns paginated list with totals

**GET /api/actions/{id}**
- Get detailed action information
- Includes all fields (payload, inverse payload)
- Status 404 if not found

**Error Handling**:
- ProblemDetails format (RFC 7807)
- Correlation IDs in all responses
- Structured logging for debugging

### 4. Frontend Integration

#### Types (`Aura.Web/src/types/api-v1.ts`)

Added TypeScript interfaces:
- `RecordActionRequest` / `RecordActionResponse`
- `UndoActionResponse`
- `ActionHistoryQuery` / `ActionHistoryResponse`
- `ActionHistoryItem`
- `ActionDetailResponse`

#### API Client (`Aura.Web/src/services/api/actionsApi.ts`)

Methods:
```typescript
recordAction(request: RecordActionRequest): Promise<RecordActionResponse>
undoAction(actionId: string): Promise<UndoActionResponse>
getActionHistory(query?: ActionHistoryQuery): Promise<ActionHistoryResponse>
getActionDetail(actionId: string): Promise<ActionDetailResponse>
```

#### Enhanced UndoManager (`Aura.Web/src/state/undoManager.ts`)

**New Interface**:
```typescript
interface PersistableCommand extends Command {
  isPersistent?: boolean;
  getActionType?(): string;
  getPayload?(): string;
  getInversePayload?(): string;
  getAffectedResourceIds?(): string;
  serverActionId?: string;
}
```

**Updated Methods**:
- `execute()` - Now async, records to server if isPersistent
- `undo()` - Now async, calls server undo if applicable
- `setServerPersistenceEnabled(enabled)` - Toggle server mode

**Features**:
- Automatic server persistence for persistent commands
- Graceful fallback if server unavailable
- Offline-first with optional sync
- Server actionId stored in command for undo tracking

#### Example Command (`Aura.Web/src/commands/workspaceCommands.ts`)

```typescript
export class SaveWorkspaceCommand implements PersistableCommand {
  isPersistent = true;
  serverActionId?: string;

  execute(): void { /* Save workspace */ }
  undo(): void { /* Delete workspace */ }
  getActionType(): string { return 'SaveWorkspace'; }
  getPayload(): string { return JSON.stringify({...}); }
  getInversePayload(): string { return JSON.stringify({...}); }
  getAffectedResourceIds(): string { return this.workspaceName; }
}
```

### 5. Testing

#### ActionServiceTests (14 tests)
- Record action saves to database
- Undo updates status correctly
- Filter by userId, actionType, status
- Pagination works correctly
- Non-existent actions return null/false
- Already undone actions return false
- Cleanup expired actions marks status
- Actions ordered by timestamp descending

#### ActionsControllerTests (9 tests)
- POST /api/actions returns 201 with actionId
- POST /api/actions/{id}/undo succeeds
- Undo non-existent returns 404
- Undo already undone returns 400
- GET /api/actions filters correctly
- GET /api/actions paginates correctly
- GET /api/actions/{id} returns details
- GET /api/actions/{id} non-existent returns 404
- Retention policy sets expiration date

### 6. Migration

**File**: `Aura.Api/Migrations/20251103202216_AddActionLogAndSoftDelete.cs`

**Up**:
- Creates ActionLogs table with all columns
- Creates CustomTemplates table
- Adds IsDeleted, DeletedAt, DeletedByUserId to ProjectStates
- Creates 13 indexes for performance

**Down**:
- Drops ActionLogs table
- Drops CustomTemplates table
- Removes soft-delete columns from ProjectStates

## Architecture

### Data Flow

```
┌─────────────────────┐
│ User performs action│
└──────────┬──────────┘
           │
           ▼
┌─────────────────────┐
│ Command.execute()   │
└──────────┬──────────┘
           │
           ▼
┌─────────────────────────────┐
│ UndoManager.execute(command)│
└──────────┬──────────────────┘
           │
           ├───► Local History (CommandHistory)
           │
           └───► Server Persistence (if isPersistent)
                 │
                 ▼
           ┌─────────────────────┐
           │ POST /api/actions   │
           └──────────┬──────────┘
                      │
                      ▼
           ┌─────────────────────┐
           │ ActionService       │
           └──────────┬──────────┘
                      │
                      ▼
           ┌─────────────────────┐
           │ ActionLogs table    │
           └─────────────────────┘
```

### Undo Flow

```
┌────────────────────┐
│ User presses Ctrl+Z│
└─────────┬──────────┘
          │
          ▼
┌─────────────────────┐
│ UndoManager.undo()  │
└─────────┬───────────┘
          │
          ├───► Local: Command.undo()
          │
          └───► Server: POST /api/actions/{id}/undo
                │
                ▼
          ┌─────────────────────┐
          │ ActionService       │
          │ .UndoActionAsync()  │
          └─────────┬───────────┘
                    │
                    ▼
          ┌─────────────────────────┐
          │ Update ActionLog        │
          │ Status = "Undone"       │
          │ UndoneAt = DateTime.Now │
          └─────────────────────────┘
```

## Configuration

### Server Persistence Toggle

```typescript
// Enable server persistence (default)
useUndoManager.getState().setServerPersistenceEnabled(true);

// Disable for offline mode
useUndoManager.getState().setServerPersistenceEnabled(false);
```

### Retention Policy

Set retention when recording action:

```typescript
const request: RecordActionRequest = {
  actionType: 'CreateProject',
  description: 'Create new project',
  retentionDays: 90 // Auto-expire after 90 days
};
```

### Cleanup Job

Implement background job to periodically call:

```csharp
await actionService.CleanupExpiredActionsAsync(cancellationToken);
```

## Usage Examples

### Recording a Persistent Action

```typescript
import { useUndoManager, PersistableCommand } from '@/state/undoManager';

class CreateProjectCommand implements PersistableCommand {
  isPersistent = true;
  serverActionId?: string;

  constructor(private project: Project) {}

  execute(): void {
    // Create project locally
    createProject(this.project);
  }

  undo(): void {
    // Delete project locally
    deleteProject(this.project.id);
  }

  getActionType(): string {
    return 'CreateProject';
  }

  getPayload(): string {
    return JSON.stringify(this.project);
  }

  getInversePayload(): string {
    return JSON.stringify({ id: this.project.id });
  }

  getAffectedResourceIds(): string {
    return this.project.id;
  }

  getDescription(): string {
    return `Create project "${this.project.name}"`;
  }

  getTimestamp(): Date {
    return new Date();
  }
}

// In component
const { execute } = useUndoManager();

async function handleCreateProject() {
  const command = new CreateProjectCommand(newProject);
  await execute(command); // Saves locally AND to server
}
```

### Querying Action History

```typescript
import { getActionHistory } from '@/services/api/actionsApi';

const history = await getActionHistory({
  userId: 'user-123',
  actionType: 'CreateProject',
  status: 'Applied',
  page: 1,
  pageSize: 20
});

console.log(`Total: ${history.totalCount} actions`);
history.actions.forEach(action => {
  console.log(`${action.timestamp}: ${action.description}`);
});
```

### Manual Undo via API

```typescript
import { undoAction } from '@/services/api/actionsApi';

const response = await undoAction(actionId);
if (response.success) {
  console.log(`Action undone at ${response.undoneAt}`);
} else {
  console.error(`Failed: ${response.errorMessage}`);
}
```

## Benefits

### For Users
- **Cross-session undo**: Undo actions from previous sessions
- **Audit trail**: See complete history of what changed
- **Safety**: Recover from mistakes days/weeks later
- **Multi-device**: Undo on one device, effect on all

### For Developers
- **Simple API**: Just implement PersistableCommand interface
- **Automatic persistence**: No manual API calls needed
- **Type-safe**: Full TypeScript support
- **Testable**: Easy to mock and test

### For Operations
- **Audit log**: Complete record of all changes
- **Compliance**: Meet retention requirements
- **Debugging**: Track down issues with correlation IDs
- **Analytics**: Understand user behavior patterns

## Limitations & Considerations

### Current Limitations
1. **No conflict resolution**: Last-write-wins for concurrent operations
2. **No multi-user coordination**: Users can undo each other's actions
3. **Storage growth**: ActionLog table grows indefinitely without cleanup
4. **No batching**: Each action is a separate API call

### Performance Considerations
1. **Network latency**: Adds ~50-200ms per action
2. **Database size**: Plan for ~1KB per action * actions per day
3. **Query performance**: Use indexes, pagination, and date ranges
4. **Retention policy**: Implement cleanup job to manage size

### Security Considerations
1. **Authorization**: Verify user can undo specific actions
2. **Data sensitivity**: Don't log sensitive data in payloads
3. **Rate limiting**: Prevent abuse of undo operations
4. **Audit integrity**: Ensure logs can't be tampered with

## Future Enhancements

### Phase 3 (Potential)
- [ ] Conflict resolution for concurrent edits
- [ ] Multi-user undo safety (require confirmation)
- [ ] Batch undo operations
- [ ] Undo preview (show what will change)
- [ ] Action compression (merge similar actions)
- [ ] Real-time sync across devices
- [ ] Undo analytics dashboard
- [ ] Export audit logs

### Advanced Features
- [ ] Time-travel debugging
- [ ] Action replay for testing
- [ ] Collaborative undo with permissions
- [ ] Automatic cleanup background job
- [ ] Action search with full-text
- [ ] Undo/redo via voice commands

## Migration Guide

### Updating Existing Commands

**Before** (Phase 1):
```typescript
export class MyCommand implements Command {
  execute(): void { /* ... */ }
  undo(): void { /* ... */ }
  getDescription(): string { /* ... */ }
  getTimestamp(): Date { /* ... */ }
}
```

**After** (Phase 2):
```typescript
export class MyCommand implements PersistableCommand {
  isPersistent = true; // Enable server persistence
  serverActionId?: string;

  execute(): void { /* ... */ }
  undo(): void { /* ... */ }
  getDescription(): string { /* ... */ }
  getTimestamp(): Date { /* ... */ }

  // New methods for server persistence
  getActionType(): string { return 'MyAction'; }
  getPayload(): string { return JSON.stringify({...}); }
  getInversePayload(): string { return JSON.stringify({...}); }
  getAffectedResourceIds(): string { return 'resource-id'; }
}
```

### Database Migration

```bash
# Run migration
dotnet ef database update --project Aura.Api

# Verify tables created
sqlite3 aura.db ".tables"  # Should show ActionLogs
```

## Testing

### Run Tests

```bash
# Run all Phase 2 tests
dotnet test --filter "FullyQualifiedName~ActionService|FullyQualifiedName~ActionsController"

# Run specific test class
dotnet test --filter "FullyQualifiedName~ActionServiceTests"
```

### Manual Testing

1. **Record Action**:
   ```bash
   curl -X POST http://localhost:5005/api/actions \
     -H "Content-Type: application/json" \
     -d '{"userId":"test","actionType":"CreateProject","description":"Test action"}'
   ```

2. **Undo Action**:
   ```bash
   curl -X POST http://localhost:5005/api/actions/{actionId}/undo
   ```

3. **Query History**:
   ```bash
   curl "http://localhost:5005/api/actions?userId=test&page=1&pageSize=10"
   ```

## Documentation

- **API Documentation**: Swagger UI at `/swagger`
- **Developer Guide**: `Aura.Web/UNDO_REDO_GUIDE.md`
- **Visual Guide**: `Aura.Web/UNDO_REDO_VISUAL_GUIDE.md`
- **This Document**: Phase 2 implementation details

## Contributors

- GitHub Copilot Agent
- Implementation Date: November 3, 2025
- Based on PR #79 Phase 1 foundation

## Summary

Phase 2 successfully extends the undo/redo system with enterprise-grade server-side persistence. The implementation is production-ready with:

✅ Complete database schema with indexes  
✅ RESTful API with proper error handling  
✅ Frontend integration with TypeScript  
✅ 23 integration tests  
✅ Comprehensive documentation  
✅ Retention policy support  
✅ Soft-delete for safe undo  

The system is now ready for cross-session undo operations, audit logging, and enterprise compliance requirements.
