> **⚠️ ARCHIVED DOCUMENT**
>
> This document is archived for historical reference only.
> It may contain outdated information. See the [Documentation Index](docs/DocsIndex.md) for current documentation.

# User Customization API Testing Guide

This guide provides examples for testing the User Preferences API endpoints using curl or similar tools.

## Prerequisites

1. Start the Aura API server:
   ```bash
   cd Aura.Api
   dotnet run
   ```

2. The API will be available at `http://localhost:5005` by default

3. All examples use JSON format

## Testing Custom Audience Profiles

### 1. Create a Custom Audience Profile

```bash
curl -X POST http://localhost:5005/api/user-preferences/audience-profiles \
  -H "Content-Type: application/json" \
  -d '{
    "id": "",
    "name": "Tech Professional Test",
    "baseProfileId": null,
    "createdAt": "2025-11-01T00:00:00Z",
    "updatedAt": "2025-11-01T00:00:00Z",
    "isCustom": true,
    "minAge": 25,
    "maxAge": 45,
    "educationLevel": "Bachelor or higher",
    "educationLevelDescription": "College educated professional",
    "culturalSensitivities": ["Inclusive language"],
    "topicsToAvoid": ["Politics"],
    "topicsToEmphasize": ["Technology", "Innovation"],
    "vocabularyLevel": 7,
    "sentenceStructurePreference": "Complex",
    "readingLevel": 12,
    "violenceThreshold": 2,
    "profanityThreshold": 1,
    "sexualContentThreshold": 1,
    "controversialTopicsThreshold": 5,
    "humorStyle": "Moderate",
    "sarcasmLevel": 4,
    "jokeTypes": ["Tech humor", "Wordplay"],
    "culturalHumorPreferences": [],
    "formalityLevel": 6,
    "attentionSpanSeconds": 240,
    "pacingPreference": "Medium",
    "informationDensity": 7,
    "technicalDepthTolerance": 8,
    "jargonAcceptability": 8,
    "familiarTechnicalTerms": ["API", "Cloud", "SaaS"],
    "emotionalTone": "Professional",
    "emotionalIntensity": 5,
    "ctaAggressiveness": 5,
    "ctaStyle": "Conversational",
    "brandVoiceGuidelines": "Professional and approachable",
    "brandToneKeywords": ["Professional", "Innovative"],
    "brandPersonality": "Expert but friendly",
    "description": "Professional tech audience profile for testing",
    "tags": ["technology", "professional", "test"],
    "isFavorite": false,
    "usageCount": 0,
    "lastUsedAt": null
  }'
```

**Expected Response** (201 Created):
```json
{
  "id": "generated-uuid",
  "name": "Tech Professional Test",
  ...
}
```

### 2. Get All Custom Audience Profiles

```bash
curl -X GET http://localhost:5005/api/user-preferences/audience-profiles
```

**Expected Response** (200 OK):
```json
[
  {
    "id": "profile-id-1",
    "name": "Tech Professional Test",
    ...
  }
]
```

### 3. Get a Specific Profile

```bash
# Replace {id} with actual profile ID
curl -X GET http://localhost:5005/api/user-preferences/audience-profiles/{id}
```

**Expected Response** (200 OK):
```json
{
  "id": "profile-id",
  "name": "Tech Professional Test",
  ...
}
```

### 4. Update a Profile

```bash
# Replace {id} with actual profile ID
curl -X PUT http://localhost:5005/api/user-preferences/audience-profiles/{id} \
  -H "Content-Type: application/json" \
  -d '{
    "id": "{id}",
    "name": "Tech Professional Updated",
    "minAge": 30,
    "maxAge": 50,
    ... (include all other fields)
  }'
```

**Expected Response** (200 OK):
```json
{
  "id": "profile-id",
  "name": "Tech Professional Updated",
  ...
}
```

### 5. Delete a Profile

```bash
# Replace {id} with actual profile ID
curl -X DELETE http://localhost:5005/api/user-preferences/audience-profiles/{id}
```

**Expected Response** (204 No Content)

## Testing Content Filtering Policies

### 1. Create a Content Filtering Policy

```bash
curl -X POST http://localhost:5005/api/user-preferences/filtering-policies \
  -H "Content-Type: application/json" \
  -d '{
    "id": "",
    "name": "Moderate Family Friendly",
    "createdAt": "2025-11-01T00:00:00Z",
    "updatedAt": "2025-11-01T00:00:00Z",
    "filteringEnabled": true,
    "allowOverrideAll": false,
    "profanityFilter": "Moderate",
    "customBannedWords": ["inappropriate"],
    "customAllowedWords": [],
    "violenceThreshold": 3,
    "blockGraphicContent": true,
    "sexualContentThreshold": 1,
    "blockExplicitContent": true,
    "bannedTopics": ["violence", "drugs"],
    "allowedControversialTopics": ["healthcare"],
    "politicalContent": "NeutralOnly",
    "politicalContentGuidelines": "Avoid partisan content",
    "religiousContent": "RespectfulOnly",
    "religiousContentGuidelines": "Respectful mentions only",
    "substanceReferences": "Moderate",
    "blockHateSpeech": true,
    "hateSpeechExceptions": [],
    "copyrightPolicy": "Strict",
    "blockedConcepts": ["weapons"],
    "allowedConcepts": [],
    "blockedPeople": [],
    "allowedPeople": [],
    "blockedBrands": [],
    "allowedBrands": [],
    "description": "Moderate filtering for general audiences",
    "isDefault": false,
    "usageCount": 0,
    "lastUsedAt": null
  }'
```

**Expected Response** (201 Created):
```json
{
  "id": "generated-uuid",
  "name": "Moderate Family Friendly",
  ...
}
```

### 2. Get All Filtering Policies

```bash
curl -X GET http://localhost:5005/api/user-preferences/filtering-policies
```

**Expected Response** (200 OK):
```json
[
  {
    "id": "policy-id-1",
    "name": "Moderate Family Friendly",
    ...
  }
]
```

### 3. Get a Specific Policy

```bash
# Replace {id} with actual policy ID
curl -X GET http://localhost:5005/api/user-preferences/filtering-policies/{id}
```

### 4. Update a Policy

```bash
# Replace {id} with actual policy ID
curl -X PUT http://localhost:5005/api/user-preferences/filtering-policies/{id} \
  -H "Content-Type: application/json" \
  -d '{
    ... (include all fields with updates)
  }'
```

### 5. Delete a Policy

```bash
# Replace {id} with actual policy ID
curl -X DELETE http://localhost:5005/api/user-preferences/filtering-policies/{id}
```

**Expected Response** (204 No Content)

## Testing Export/Import

### 1. Export All Preferences

```bash
curl -X GET http://localhost:5005/api/user-preferences/export \
  -o preferences-backup.json
```

**Expected Response** (200 OK):
```json
{
  "jsonData": "{...full export...}",
  "exportDate": "2025-11-01T15:00:00Z",
  "version": "1.0"
}
```

### 2. Import Preferences

First, extract the jsonData from the export response, then:

```bash
curl -X POST http://localhost:5005/api/user-preferences/import \
  -H "Content-Type: application/json" \
  -d '{
    "jsonData": "{...previously exported data...}"
  }'
```

**Expected Response** (200 OK):
```json
{
  "success": true,
  "message": "Preferences imported successfully"
}
```

## Testing Error Cases

### 1. Get Non-Existent Profile

```bash
curl -X GET http://localhost:5005/api/user-preferences/audience-profiles/non-existent-id
```

**Expected Response** (404 Not Found):
```json
{
  "type": "https://tools.ietf.org/html/rfc7231#section-6.5.4",
  "title": "Profile Not Found",
  "status": 404,
  "detail": "Custom audience profile non-existent-id does not exist"
}
```

### 2. Create Profile with Missing Name

```bash
curl -X POST http://localhost:5005/api/user-preferences/audience-profiles \
  -H "Content-Type: application/json" \
  -d '{
    "id": "",
    "name": "",
    ... (rest of fields)
  }'
```

**Expected Response** (400 Bad Request):
```json
{
  "type": "https://tools.ietf.org/html/rfc7231#section-6.5.1",
  "title": "Invalid Profile",
  "status": 400,
  "detail": "Profile name is required"
}
```

### 3. Import Invalid JSON

```bash
curl -X POST http://localhost:5005/api/user-preferences/import \
  -H "Content-Type: application/json" \
  -d '{
    "jsonData": "invalid json"
  }'
```

**Expected Response** (400 Bad Request):
```json
{
  "title": "Import Failed",
  "status": 400,
  "detail": "Failed to import preferences: ..."
}
```

## Automated Testing Script

Save this as `test-user-preferences-api.sh`:

```bash
#!/bin/bash

API_URL="http://localhost:5005/api/user-preferences"

echo "Testing User Preferences API..."

# Test 1: Create audience profile
echo -e "\n1. Creating audience profile..."
PROFILE_RESPONSE=$(curl -s -X POST $API_URL/audience-profiles \
  -H "Content-Type: application/json" \
  -d '{
    "name": "Test Profile",
    "minAge": 25,
    "maxAge": 35,
    "educationLevel": "Bachelor",
    "vocabularyLevel": 7,
    "sentenceStructurePreference": "Mixed",
    "readingLevel": 10,
    "violenceThreshold": 3,
    "profanityThreshold": 2,
    "sexualContentThreshold": 1,
    "controversialTopicsThreshold": 5,
    "humorStyle": "Moderate",
    "sarcasmLevel": 4,
    "formalityLevel": 5,
    "attentionSpanSeconds": 180,
    "pacingPreference": "Medium",
    "informationDensity": 6,
    "technicalDepthTolerance": 7,
    "jargonAcceptability": 6,
    "emotionalTone": "Neutral",
    "emotionalIntensity": 5,
    "ctaAggressiveness": 5,
    "ctaStyle": "Conversational",
    "culturalSensitivities": [],
    "topicsToAvoid": [],
    "topicsToEmphasize": [],
    "jokeTypes": [],
    "culturalHumorPreferences": [],
    "familiarTechnicalTerms": [],
    "brandToneKeywords": [],
    "description": "Test profile",
    "tags": ["test"],
    "isFavorite": false,
    "usageCount": 0
  }')

PROFILE_ID=$(echo $PROFILE_RESPONSE | jq -r '.id')
echo "Created profile with ID: $PROFILE_ID"

# Test 2: Get all profiles
echo -e "\n2. Getting all audience profiles..."
curl -s -X GET $API_URL/audience-profiles | jq '.[].name'

# Test 3: Get specific profile
echo -e "\n3. Getting specific profile..."
curl -s -X GET $API_URL/audience-profiles/$PROFILE_ID | jq '.name'

# Test 4: Export preferences
echo -e "\n4. Exporting preferences..."
curl -s -X GET $API_URL/export | jq '.version'

# Test 5: Delete profile
echo -e "\n5. Deleting profile..."
curl -s -X DELETE $API_URL/audience-profiles/$PROFILE_ID -w "%{http_code}"

echo -e "\n\nAll tests completed!"
```

Make it executable:
```bash
chmod +x test-user-preferences-api.sh
```

Run it:
```bash
./test-user-preferences-api.sh
```

## Troubleshooting

### Connection Refused

**Problem**: `curl: (7) Failed to connect to localhost port 5005`

**Solution**: 
1. Ensure the API is running: `dotnet run` in Aura.Api directory
2. Check the correct port in appsettings.json
3. Verify no firewall blocking the port

### 500 Internal Server Error

**Problem**: Server returns 500 error

**Solution**:
1. Check API logs in `Aura.Api/logs/`
2. Verify database/storage is accessible
3. Check AuraData directory permissions

### Invalid JSON

**Problem**: 400 Bad Request with JSON parse error

**Solution**:
1. Validate JSON with a linter (jsonlint.com)
2. Ensure all required fields are present
3. Check for trailing commas (not allowed in JSON)

### Profile Not Found After Creation

**Problem**: Created profile returns 404 on subsequent GET

**Solution**:
1. Check UserPreferences directory in AuraData folder
2. Verify write permissions
3. Look for file system errors in logs

## Performance Testing

### Load Test Example (using Apache Bench)

```bash
# Install Apache Bench if needed
# Ubuntu/Debian: apt-get install apache2-utils
# macOS: brew install httpd

# Test GET endpoint (100 requests, 10 concurrent)
ab -n 100 -c 10 http://localhost:5005/api/user-preferences/audience-profiles

# Expected: < 100ms average response time for GET operations
```

### Expected Performance Metrics

- **GET all profiles**: < 50ms for 10 profiles
- **GET single profile**: < 20ms
- **POST create profile**: < 100ms
- **PUT update profile**: < 100ms
- **DELETE profile**: < 50ms
- **Export preferences**: < 200ms for moderate data
- **Import preferences**: < 500ms for moderate data

## Integration with Video Generation

After creating custom profiles, test integration:

```bash
# 1. Create a custom audience profile (save the ID)

# 2. Use the profile in a video generation request
curl -X POST http://localhost:5005/api/quick/demo \
  -H "Content-Type: application/json" \
  -d '{
    "audienceProfileId": "your-profile-id-here"
  }'

# 3. Check job logs to verify profile was used
curl -X GET http://localhost:5005/api/jobs/{job-id}
```

## Continuous Integration Testing

Add to your CI pipeline:

```yaml
# .github/workflows/api-tests.yml
name: API Tests

on: [push, pull_request]

jobs:
  test-user-preferences-api:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v2
      
      - name: Setup .NET
        uses: actions/setup-dotnet@v1
        with:
          dotnet-version: '8.0.x'
      
      - name: Start API
        run: |
          cd Aura.Api
          dotnet run &
          sleep 10
      
      - name: Run API Tests
        run: |
          chmod +x test-user-preferences-api.sh
          ./test-user-preferences-api.sh
```

## Next Steps

1. Test all CRUD operations for each entity type
2. Verify export/import with real data
3. Load test with realistic data volumes
4. Integration test with video generation pipeline
5. Test edge cases and error handling
6. Performance benchmark and optimize if needed
