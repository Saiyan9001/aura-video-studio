# Script Generation Flow - User Guide

## Overview

The Script Generation Flow enables users to create AI-powered video scripts with full editing capabilities, quality indicators, and export options.

## Features

### 1. Script Generation

**Endpoint**: `POST /api/scripts/generate`

Generate a complete video script from a brief:

```typescript
const script = await generateScript({
  topic: 'Introduction to Machine Learning',
  audience: 'Beginners in tech',
  goal: 'Educate and inspire',
  tone: 'Conversational',
  targetDurationSeconds: 120,
  preferredProvider: 'RuleBased' // optional
});
```

**Response includes**:
- Script ID for future reference
- List of scenes with narration and visual prompts
- Metadata (provider, model, cost, generation time)
- Correlation ID for tracking

### 2. Provider Selection

**Endpoint**: `GET /api/scripts/providers`

Fetch available LLM providers:

```typescript
const { providers } = await listProviders();
// Returns list with:
// - name, tier (Free/Pro)
// - isAvailable status
// - capabilities
// - cost estimates
```

**Available Providers**:
- **RuleBased** (Free, Offline) - Template-based generation
- **Ollama** (Free, Local) - Local LLM models
- **OpenAI** (Pro) - GPT-4 and GPT-3.5
- **Gemini** (Pro) - Google's language model

### 3. Scene Editing

**Auto-save**: Changes are automatically saved 2 seconds after typing stops

**Endpoint**: `PUT /api/scripts/{id}/scenes/{sceneNumber}`

Update scene content:

```typescript
await updateScene('script-123', 1, {
  narration: 'Updated narration text',
  visualPrompt: 'New visual description', // optional
  durationSeconds: 6.5 // optional
});
```

### 4. Script Regeneration

#### Full Script Regeneration

**Endpoint**: `POST /api/scripts/{id}/regenerate`

```typescript
await regenerateScript('script-123', {
  preferredProvider: 'OpenAI' // optional
});
```

#### Single Scene Regeneration

**Endpoint**: `POST /api/scripts/{id}/scenes/{sceneNumber}/regenerate`

```typescript
await regenerateScene('script-123', 2);
```

### 5. Quality Indicators

The UI displays several quality metrics:

**Word Count**: Total words across all scenes

**Reading Speed (WPM)**: Words per minute - ideal range 120-180 WPM
- **Slow**: < 120 WPM
- **Good**: 120-180 WPM
- **Fast**: > 180 WPM

**Scene Pacing Badges**:
- **Too Short**: Scene has too few words for its duration
- **Too Long**: Scene has too many words for its duration
- **Good**: Scene has appropriate pacing

### 6. Script Export

**Endpoint**: `GET /api/scripts/{id}/export?format=text|markdown`

Export formats:

**Text Format** (`.txt`):
```
Title
=====

Scene 1 (5.0s)

Narration text here...

Visual: Visual prompt here...
---
```

**Markdown Format** (`.md`):
```markdown
# Title

## Scene 1
**Duration**: 5.0s | **Transition**: Cut

### Narration
Narration text here...

### Visual
> Visual prompt here...
```

## User Workflow

### Step 1: Navigate to Script Review (Wizard Step 3)

1. Complete Brief Input (Step 1)
2. Configure Style (Step 2)
3. Reach Script Review (Step 3)

### Step 2: Generate Script

1. Click **"Generate Script"** button
2. Wait for generation (shows loading state)
3. Review generated scenes

### Step 3: Edit and Refine

**Edit Scene Narration**:
- Click in the narration text field
- Type your changes
- Changes auto-save after 2 seconds

**Regenerate Scene**:
- Click **"Regenerate"** button on specific scene
- Scene will be replaced with fresh content

**Regenerate Entire Script**:
- Click **"Regenerate"** button in header
- Entire script will be regenerated

### Step 4: Review Quality

Check the statistics bar:
- Total Duration
- Word Count
- Reading Speed (with Good/Slow/Fast indicator)
- Scene Count
- Provider Used

Look for quality badges on scenes:
- **Too Short**: Consider adding more content
- **Too Long**: Consider breaking into multiple scenes

### Step 5: Export

1. Click **"Export Text"** for plain text format
2. Click **"Export Markdown"** for markdown format
3. File downloads automatically with timestamp

### Step 6: Continue to Next Step

Once satisfied with the script, click **"Next"** to proceed to voice configuration.

## Technical Implementation

### Frontend Component Structure

```
ScriptReview.tsx
├── Header
│   ├── Title
│   └── Actions (Export, Regenerate)
├── Statistics Bar
│   ├── Duration
│   ├── Word Count
│   ├── Reading Speed
│   ├── Scene Count
│   └── Provider Badge
└── Scenes List
    └── Scene Card (per scene)
        ├── Scene Header
        │   ├── Scene Number Badge
        │   ├── Quality Badges
        │   └── Regenerate Button
        ├── Narration Field (auto-save)
        ├── Metadata Row
        │   ├── Duration
        │   ├── Word Count
        │   └── Transition
        └── Visual Prompt (read-only)
```

### State Management

**Local State**:
- `isGenerating`: Loading state during generation
- `generatedScript`: Current script data
- `editingScenes`: Scenes being edited (for optimistic updates)
- `regeneratingScenes`: Scenes being regenerated (loading states)
- `providers`: Available LLM providers
- `selectedProvider`: Currently selected provider

**Auto-save Implementation**:
```typescript
const autoSaveTimeouts = useRef<Record<number, ReturnType<typeof setTimeout>>>({});

const handleSceneEdit = (sceneNumber: number, newText: string) => {
  // Clear existing timeout
  if (autoSaveTimeouts.current[sceneNumber]) {
    clearTimeout(autoSaveTimeouts.current[sceneNumber]);
  }
  
  // Set new timeout for 2 seconds
  autoSaveTimeouts.current[sceneNumber] = setTimeout(async () => {
    await updateScene(scriptId, sceneNumber, { narration: newText });
  }, 2000);
};
```

## API Error Handling

All API methods include proper error handling:

```typescript
try {
  const script = await generateScript(request);
  // Success
} catch (error) {
  const apiError = parseApiError(error);
  console.error('Generation failed:', apiError.message);
  // Show user-friendly error message
}
```

**Common Error Scenarios**:
- **404**: Script/Scene not found
- **500**: Generation failed (provider issue, network)
- **501**: Feature not implemented (shouldn't happen now)

## Testing

### Unit Tests
Run: `npm test -- scriptApi.test.ts`

Tests cover:
- Script generation
- Scene updates
- Script regeneration
- Scene regeneration
- Provider listing
- Export functionality

### E2E Tests
Run: `npm run playwright -- script-generation.spec.ts`

Tests cover:
- Empty state display
- Script generation flow
- Scene editing with auto-save
- Export options
- Quality indicators
- Provider selection

## Performance Considerations

**Auto-save Debouncing**: 2-second delay prevents excessive API calls

**Provider Caching**: Providers loaded once and cached

**Optimistic Updates**: UI updates immediately, syncs with backend afterward

**Loading States**: Clear feedback during async operations

## Future Enhancements

- [ ] Script version history with undo/redo
- [ ] Scene reordering via drag-and-drop
- [ ] Scene splitting and merging
- [ ] Template library for common video types
- [ ] AI-powered script improvement suggestions
- [ ] Collaborative editing with real-time sync
- [ ] PDF export with formatting
- [ ] Integration with video templates

## Troubleshooting

**Script won't generate**:
1. Check brief is filled out (Step 1)
2. Verify provider is available
3. Check browser console for errors
4. Try different provider if current one fails

**Auto-save not working**:
1. Wait full 2 seconds after editing
2. Check network tab for PUT request
3. Verify script ID is valid

**Export not downloading**:
1. Check browser's download permissions
2. Try different format
3. Verify script has content

## Support

For issues or questions:
- Check GitHub Issues
- Review API logs in backend
- Enable browser DevTools for debugging
- Check correlation IDs for request tracking
