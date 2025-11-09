# Sample Assets and Demo Content Guide

This guide covers the built-in sample assets system that provides ready-to-use templates, voices, and media for immediate testing and video generation.

## Overview

Aura Video Studio includes a comprehensive set of sample assets to help users:
- Start creating videos immediately without external resources
- Test the system with known-good configurations
- Learn how to structure briefs and configure settings
- Develop and test new features without dependencies

## Components

### 1. Brief Templates

Pre-configured video brief templates for common use cases.

**Location**: `Assets/Samples/Templates/brief-templates.json`

**Available Templates**:
- **Welcome Tutorial**: Friendly introduction for new users (30s)
- **Product Demo**: Showcase products or services (45s)
- **Educational Explainer**: Explain concepts clearly (60s)
- **Social Media Short**: Quick engaging content (15s, 9:16)
- **Corporate Presentation**: Professional business communication (90s)
- **Storytelling Narrative**: Compelling story format (120s)
- **Quick Demo**: Minimal test template (10s)

**Accessing via API**:
```bash
# Get all templates
GET /api/assets/samples/templates/briefs

# Get specific template
GET /api/assets/samples/templates/briefs/{templateId}
```

**Using in Frontend**:
```typescript
import { BriefTemplates } from '@/components/VideoWizard/BriefTemplates';

<BriefTemplates
  onSelectTemplate={(template) => {
    // Use template.brief properties to populate form
    setBrief({
      topic: template.brief.topic,
      audience: template.brief.audience,
      goal: template.brief.goal,
      tone: template.brief.tone,
      duration: template.brief.duration,
    });
  }}
  selectedTemplateId={selectedId}
/>
```

### 2. Voice Configurations

Pre-configured TTS voice settings for all supported providers.

**Location**: `Assets/Samples/Templates/voice-configs.json`

**Supported Providers**:
- **ElevenLabs**: Premium male/female voices (requires API key)
- **PlayHT**: Natural male/female voices (requires API key)
- **Windows SAPI**: Built-in Windows voices (free, no installation)
- **Piper**: Neural TTS offline voices (free)
- **Mimic3**: Open source offline voices (free)

**Accessing via API**:
```bash
# Get all voice configurations
GET /api/assets/samples/voice-configs

# Get configurations for specific provider
GET /api/assets/samples/voice-configs?provider=Piper
```

**Using Voice Configs**:
```typescript
// Load voice configurations
const response = await fetch('/api/assets/samples/voice-configs?provider=WindowsSAPI');
const configs = await response.json();

// Apply configuration
const voiceSettings = {
  provider: configs[0].provider,
  voiceId: configs[0].voiceId,
  ...configs[0].settings
};
```

### 3. Sample Images

Auto-generated gradient placeholder images for testing.

**Location**: `Assets/Samples/Images/`

**Available Images**:
- `sample-gradient-01.jpg` - Blue gradient (1920x1080)
- `sample-abstract-01.jpg` - Purple/violet gradient (1920x1080)
- `sample-nature-01.jpg` - Green/blue gradient (1920x1080)
- `sample-tech-01.jpg` - Cyan/gray gradient (1920x1080)
- `sample-portrait-01.jpg` - Red/orange gradient (1080x1920)
- `sample-minimal-01.jpg` - Light gray/white gradient (1920x1080)

**Auto-Generation**: Images are automatically generated on first initialization if the directory is empty.

**Accessing via API**:
```bash
# Get all sample images
GET /api/assets/samples/images
```

### 4. Sample Audio

Placeholder for background music (to be implemented with actual CC0 audio files).

**Location**: `Assets/Samples/Audio/`

**Accessing via API**:
```bash
# Get all sample audio
GET /api/assets/samples/audio
```

## Backend Implementation

### Registering Sample Assets Service

In your dependency injection setup (e.g., `Program.cs` or `Startup.cs`):

```csharp
using Aura.Core.Services.Assets;

// Register services
services.AddSingleton<ILogger<SampleImageGenerator>>(provider => 
    provider.GetRequiredService<ILoggerFactory>().CreateLogger<SampleImageGenerator>());
services.AddSingleton<SampleImageGenerator>();

services.AddSingleton<ILogger<AssetLibraryService>>(provider => 
    provider.GetRequiredService<ILoggerFactory>().CreateLogger<AssetLibraryService>());
services.AddSingleton<AssetLibraryService>(provider => 
    new AssetLibraryService(
        provider.GetRequiredService<ILogger<AssetLibraryService>>(),
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Aura", "Library"),
        provider.GetRequiredService<ThumbnailGenerator>()));

services.AddSingleton<ILogger<SampleAssetsService>>(provider => 
    provider.GetRequiredService<ILoggerFactory>().CreateLogger<SampleAssetsService>());
services.AddSingleton<SampleAssetsService>(provider => 
    new SampleAssetsService(
        provider.GetRequiredService<ILogger<SampleAssetsService>>(),
        Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets", "Samples"),
        provider.GetRequiredService<AssetLibraryService>(),
        provider.GetRequiredService<SampleImageGenerator>()));
```

### Initialize on Startup

```csharp
// In your startup code or hosted service
var sampleAssets = app.Services.GetRequiredService<SampleAssetsService>();
await sampleAssets.InitializeAsync();
```

### Using in Controllers

```csharp
public class MyController : ControllerBase
{
    private readonly SampleAssetsService _sampleAssets;
    
    public MyController(SampleAssetsService sampleAssets)
    {
        _sampleAssets = sampleAssets;
    }
    
    [HttpGet("templates")]
    public async Task<IActionResult> GetTemplates()
    {
        var templates = await _sampleAssets.GetBriefTemplatesAsync();
        return Ok(templates);
    }
}
```

## Frontend Integration

### Using Brief Templates Component

```typescript
import { useState } from 'react';
import { BriefTemplates } from '@/components/VideoWizard/BriefTemplates';

function CreateVideoWizard() {
  const [selectedTemplate, setSelectedTemplate] = useState<string>();
  
  return (
    <div>
      <h2>Choose a Starting Point</h2>
      <BriefTemplates
        onSelectTemplate={(template) => {
          setSelectedTemplate(template.id);
          // Apply template values to your form
          populateFormFromTemplate(template);
        }}
        selectedTemplateId={selectedTemplate}
      />
    </div>
  );
}
```

### Loading Voice Configurations

```typescript
import { useEffect, useState } from 'react';
import { apiUrl } from '@/config/api';

function VoiceSelector({ provider }: { provider: string }) {
  const [voices, setVoices] = useState([]);
  
  useEffect(() => {
    async function loadVoices() {
      const response = await fetch(
        `${apiUrl}/api/assets/samples/voice-configs?provider=${provider}`
      );
      const data = await response.json();
      setVoices(data);
    }
    
    loadVoices();
  }, [provider]);
  
  return (
    <select>
      {voices.map(voice => (
        <option key={voice.voiceId} value={voice.voiceId}>
          {voice.name} - {voice.description}
        </option>
      ))}
    </select>
  );
}
```

## Testing with Sample Assets

### Unit Testing

Sample assets service includes comprehensive tests:

```bash
# Run sample assets tests
dotnet test --filter "FullyQualifiedName~SampleAssetsServiceTests"
```

### Integration Testing

Use sample assets in integration tests:

```csharp
[Fact]
public async Task GenerateVideo_WithSampleTemplate_Succeeds()
{
    // Arrange
    var template = await _sampleAssets.GetBriefTemplateAsync("quick-demo");
    var images = await _sampleAssets.GetSampleImagesAsync();
    
    // Act
    var job = await _videoService.GenerateVideoAsync(template.Brief, images.First());
    
    // Assert
    Assert.Equal(JobStatus.Completed, job.Status);
}
```

## Adding New Sample Assets

### Adding a Brief Template

Edit `Assets/Samples/Templates/brief-templates.json`:

```json
{
  "id": "my-custom-template",
  "name": "My Custom Template",
  "category": "Custom",
  "description": "Description of the template",
  "icon": "Lightbulb",
  "brief": {
    "topic": "Your topic here",
    "audience": "Target audience",
    "goal": "Video goal",
    "tone": "Tone of voice",
    "language": "English",
    "duration": 30,
    "keyPoints": ["Point 1", "Point 2"]
  },
  "settings": {
    "aspect": "16:9",
    "quality": "high"
  }
}
```

### Adding a Voice Configuration

Edit `Assets/Samples/Templates/voice-configs.json`:

```json
{
  "provider": "ProviderName",
  "name": "Voice Name",
  "description": "Voice description",
  "voiceId": "provider-specific-voice-id",
  "settings": {
    "speed": 1.0,
    "pitch": 1.0
  },
  "sampleText": "Sample text for preview",
  "tags": ["male", "friendly"],
  "requiresInstallation": false
}
```

### Adding Sample Images

Place image files in `Assets/Samples/Images/` with these naming conventions:
- `sample-{theme}-{number}.jpg` for 16:9 images
- `sample-{theme}-portrait-{number}.jpg` for 9:16 images

Update `Assets/Samples/Images/MANIFEST.md` with details about each image.

### Adding Sample Audio

Place audio files in `Assets/Samples/Audio/` as MP3 or WAV files:
- Use loopable tracks for background music
- Target 128kbps or higher for MP3
- Include licensing information in filename or metadata

Update `Assets/Samples/Audio/MANIFEST.md` with track details.

## Best Practices

1. **Always use sample assets for testing**: Don't rely on external APIs during development
2. **Provide fallbacks**: Sample assets should work offline without API keys
3. **Keep templates simple**: Templates should be minimal and demonstrate core functionality
4. **Document licensing**: Always include license information for sample content
5. **Test with samples first**: Verify new features work with sample assets before testing with real content

## Troubleshooting

### Templates not loading

Check that `Assets/Samples/Templates/brief-templates.json` exists and is valid JSON.

```bash
# Validate JSON
cat Assets/Samples/Templates/brief-templates.json | python -m json.tool
```

### Images not generating

Ensure SampleImageGenerator is registered in DI and has System.Drawing.Common dependency:

```bash
# Check installed packages
dotnet list Aura.Core/Aura.Core.csproj package | grep Drawing
```

### API returns 404

Verify SampleAssetsService is registered in DI and initialized on startup. Check logs for initialization errors.

## API Reference

### Brief Templates

- `GET /api/assets/samples/templates/briefs` - Get all brief templates
- `GET /api/assets/samples/templates/briefs/{id}` - Get specific template

### Voice Configurations

- `GET /api/assets/samples/voice-configs` - Get all voice configurations
- `GET /api/assets/samples/voice-configs?provider={name}` - Filter by provider

### Sample Media

- `GET /api/assets/samples/images` - Get all sample images
- `GET /api/assets/samples/audio` - Get all sample audio

## License

All built-in sample assets are CC0 (Public Domain) or specifically created for this project and may be used freely for any purpose.
