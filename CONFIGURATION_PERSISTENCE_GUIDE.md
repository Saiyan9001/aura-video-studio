# Configuration Persistence Implementation Guide

## Overview

This document describes the configuration persistence system implemented for Aura Video Studio. The system provides database-backed configuration storage with caching, versioning, and immediate persistence suitable for an Adobe Suite type application.

## Architecture

### Components

1. **ConfigurationEntity** (`Aura.Core/Data/ConfigurationEntity.cs`)
   - Database entity for storing configuration key-value pairs
   - Supports versioning, categorization, and sensitive data flagging
   - Tracks creation, updates, and modifications

2. **ConfigurationRepository** (`Aura.Core/Data/ConfigurationRepository.cs`)
   - CRUD operations for configuration data
   - Transaction support for bulk operations
   - Soft delete functionality
   - History tracking

3. **ConfigurationManager** (`Aura.Core/Services/ConfigurationManager.cs`)
   - Singleton service for configuration management
   - In-memory caching with 10-minute expiration
   - Thread-safe operations with SemaphoreSlim
   - Automatic default initialization
   - Type-safe get/set methods

4. **DatabaseInitializationService** (`Aura.Core/Services/DatabaseInitializationService.cs`)
   - Database creation and migration
   - Health checking and integrity verification
   - Automatic repair for corrupted databases
   - WAL mode configuration

5. **ConfigurationManagementController** (`Aura.Api/Controllers/ConfigurationManagementController.cs`)
   - RESTful API endpoints
   - Configuration CRUD operations
   - Debugging and diagnostics
   - Health monitoring

## Database Schema

```sql
CREATE TABLE Configurations (
    Key TEXT PRIMARY KEY NOT NULL,
    Value TEXT NOT NULL,
    Category TEXT NOT NULL,
    ValueType TEXT NOT NULL,
    Description TEXT,
    IsSensitive INTEGER NOT NULL DEFAULT 0,
    Version INTEGER NOT NULL DEFAULT 1,
    CreatedAt TEXT NOT NULL,
    UpdatedAt TEXT NOT NULL,
    ModifiedBy TEXT,
    IsActive INTEGER NOT NULL DEFAULT 1
);

CREATE INDEX IX_Configurations_Category ON Configurations(Category);
CREATE INDEX IX_Configurations_Category_IsActive ON Configurations(Category, IsActive);
CREATE INDEX IX_Configurations_Category_UpdatedAt ON Configurations(Category, UpdatedAt);
CREATE INDEX IX_Configurations_IsActive ON Configurations(IsActive);
CREATE INDEX IX_Configurations_IsSensitive ON Configurations(IsSensitive);
CREATE INDEX IX_Configurations_UpdatedAt ON Configurations(UpdatedAt);
```

## API Endpoints

### Get Configuration
```http
GET /api/configuration/{key}
```
Returns a single configuration value with metadata.

**Response:**
```json
{
  "key": "General.AutosaveEnabled",
  "value": "true",
  "source": "database"
}
```

### Get Category
```http
GET /api/configuration/category/{category}
```
Returns all configurations in a category.

**Response:**
```json
{
  "category": "General",
  "count": 5,
  "configurations": {
    "General.AutosaveEnabled": "true",
    "General.AutosaveIntervalSeconds": "300",
    ...
  },
  "source": "database"
}
```

### Set Configuration
```http
POST /api/configuration/{key}
Content-Type: application/json

{
  "value": "1920x1080",
  "category": "VideoDefaults",
  "description": "Default video resolution",
  "isSensitive": false
}
```

**Response:**
```json
{
  "success": true,
  "message": "Configuration saved successfully",
  "key": "VideoDefaults.DefaultResolution",
  "category": "VideoDefaults",
  "persisted": true
}
```

### Set Bulk Configurations
```http
POST /api/configuration/bulk
Content-Type: application/json

{
  "configurations": [
    {
      "key": "Setting1",
      "value": "Value1",
      "category": "Category1",
      "description": "Description 1"
    },
    ...
  ]
}
```

### Delete Configuration
```http
DELETE /api/configuration/{key}
```
Performs soft delete (sets IsActive = false).

### Debug Endpoints

**Dump All Configurations:**
```http
GET /api/configuration/debug/dump?includeInactive=false
```

**Reset to Defaults:**
```http
POST /api/configuration/reset
```

**Database Health:**
```http
GET /api/configuration/health/database
```

**Clear Cache:**
```http
POST /api/configuration/cache/clear
```

## Usage Examples

### C# Service Usage

```csharp
public class MyService
{
    private readonly ConfigurationManager _configManager;

    public MyService(ConfigurationManager configManager)
    {
        _configManager = configManager;
    }

    public async Task DoWork()
    {
        // Get string value
        var resolution = await _configManager.GetStringAsync(
            "VideoDefaults.DefaultResolution", 
            "1920x1080");

        // Get int value
        var fps = await _configManager.GetIntAsync(
            "VideoDefaults.DefaultFrameRate", 
            30);

        // Get bool value
        var autosave = await _configManager.GetBoolAsync(
            "General.AutosaveEnabled", 
            true);

        // Set value
        await _configManager.SetAsync(
            "MyService.LastRun",
            DateTime.UtcNow.ToString("O"),
            "ServiceTracking",
            "Last execution timestamp");

        // Get category
        var generalSettings = await _configManager.GetCategoryAsync("General");
    }
}
```

### TypeScript/React Usage

```typescript
import { apiClient } from '@/services/api/apiClient';

// Get configuration
async function getConfig(key: string): Promise<string | null> {
  try {
    const response = await apiClient.get(`/api/configuration/${key}`);
    return response.data.value;
  } catch (error) {
    console.error('Failed to get configuration:', error);
    return null;
  }
}

// Set configuration
async function setConfig(
  key: string, 
  value: string, 
  category: string
): Promise<boolean> {
  try {
    await apiClient.post(`/api/configuration/${key}`, {
      value,
      category,
      description: 'User setting'
    });
    return true;
  } catch (error) {
    console.error('Failed to set configuration:', error);
    return false;
  }
}

// Get category
async function getCategory(category: string): Promise<Record<string, string>> {
  try {
    const response = await apiClient.get(`/api/configuration/category/${category}`);
    return response.data.configurations;
  } catch (error) {
    console.error('Failed to get category:', error);
    return {};
  }
}
```

## Default Configurations

The system automatically creates these default configurations on first run:

### General
- `General.DefaultProjectSaveLocation`: "" (empty, user must configure)
- `General.AutosaveIntervalSeconds`: "300" (5 minutes)
- `General.AutosaveEnabled`: "true"
- `General.Language`: "en-US"
- `General.Theme`: "Auto"
- `General.CheckForUpdatesOnStartup`: "true"

### File Locations
- `FileLocations.OutputDirectory`: ""
- `FileLocations.TempDirectory`: ""
- `FileLocations.ProjectsDirectory`: ""

### Video Defaults
- `VideoDefaults.DefaultResolution`: "1920x1080"
- `VideoDefaults.DefaultFrameRate`: "30"
- `VideoDefaults.DefaultCodec`: "libx264"
- `VideoDefaults.DefaultBitrate`: "5M"

### Advanced
- `Advanced.OfflineMode`: "false"
- `Advanced.StableDiffusionUrl`: "http://127.0.0.1:7860"
- `Advanced.OllamaUrl`: "http://127.0.0.1:11434"
- `Advanced.EnableTelemetry`: "false"

### System
- `System.DatabaseVersion`: "1"
- `System.LastBackupDate`: (current timestamp)

## Performance Characteristics

- **First access**: ~10-50ms (database query)
- **Cached access**: <1ms (memory lookup)
- **Set operation**: ~10-30ms (database write + cache invalidation)
- **Bulk set**: ~50-200ms for 10 items (transaction)
- **Cache expiration**: 10 minutes
- **Database**: SQLite with WAL mode enabled

## Thread Safety

The ConfigurationManager uses:
- `SemaphoreSlim` for key-level locking during set operations
- `IMemoryCache` which is thread-safe
- Scoped `DbContext` instances per operation

## Logging

All operations are logged with structured logging:

```
[Information] Configuration TestKey set in category TestCategory by JohnDoe
[Information] Bulk updated 5 configurations
[Warning] Configuration cache cleared
[Error] Error setting configuration TestKey: {ErrorMessage}
```

Correlation IDs from `HttpContext.TraceIdentifier` are included in all API operations.

## Testing

Run the manual test script:

```bash
./test-configuration-persistence.sh
```

Or use curl directly:

```bash
# Health check
curl http://localhost:5005/api/configuration/health/database

# Get config
curl http://localhost:5005/api/configuration/General.Theme

# Set config
curl -X POST http://localhost:5005/api/configuration/MyKey \
  -H "Content-Type: application/json" \
  -d '{"value":"MyValue","category":"MyCategory"}'
```

## Troubleshooting

### Database not created
- Check permissions on application directory
- Review logs for "Database path is not writable" errors
- Ensure directory exists and is writable

### Configuration not persisting
- Check database health: `GET /api/configuration/health/database`
- Verify migrations ran successfully in application logs
- Check for SQLite file locks

### Cache issues
- Clear cache: `POST /api/configuration/cache/clear`
- Restart application to reset all caches
- Check memory cache service is registered

### Performance issues
- Verify database indexes exist
- Check WAL mode is enabled
- Monitor cache hit rate in logs
- Consider increasing cache expiration time

## Migration

The system uses Entity Framework Core migrations. The configuration table is created by migration `20251109150500_AddConfigurationPersistence`.

To apply migrations manually:
```bash
cd Aura.Api
dotnet ef database update
```

## Future Enhancements

Potential improvements (not currently implemented):

1. **Configuration validation** - JSON schema validation for complex values
2. **Configuration encryption** - Encrypt sensitive values at rest
3. **Configuration audit log** - Track all changes with full history
4. **Configuration import/export** - JSON file import/export for profiles
5. **Configuration sync** - Multi-instance synchronization
6. **Configuration UI** - Web-based configuration editor
7. **Configuration hot reload** - Detect changes without restart

## Related Documentation

- Database Schema: See `Aura.Core/Data/AuraDbContext.cs`
- API Reference: See Swagger UI at `/swagger`
- Migration History: See `Aura.Api/Migrations/`
- Tests: See `Aura.Tests/Configuration/`
