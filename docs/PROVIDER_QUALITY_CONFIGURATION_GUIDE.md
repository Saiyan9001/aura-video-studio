# Provider and Quality Configuration UI

## Overview

Aura Video Studio now includes comprehensive configuration UI that allows you to configure all providers and quality settings without editing any code. This guide explains how to use the new configuration panels.

## Accessing Configuration

1. Open Aura Video Studio
2. Navigate to **Settings**
3. The configuration options are prominently placed at the top:
   - **Provider Configuration** - Configure API keys, priorities, and cost limits
   - **Quality Settings** - Configure video, audio, and subtitle quality

## Provider Configuration

### Overview

The Provider Configuration panel lets you manage 10 different providers across three categories:
- **LLM Providers**: OpenAI, Anthropic (Claude), Google (Gemini), Ollama
- **TTS Providers**: ElevenLabs, PlayHT, Windows SAPI
- **Image Providers**: StabilityAI, Stable Diffusion, Stock Images

### Configuring a Provider

For each provider, you can configure:

1. **Enable/Disable Toggle**
   - Use the switch to enable or disable a provider
   - Disabled providers will not be used for generation

2. **API Key**
   - Enter the provider's API key in the text field
   - Click the eye icon to show/hide the key
   - Click **Test Connection** to verify the key works
   - Success/failure messages appear below the field

3. **Priority**
   - Use the up/down arrows to reorder providers
   - Lower priority number = higher preference
   - The system tries providers in priority order

4. **Cost Limit**
   - Set a monthly cost limit in USD (optional)
   - Leave empty for no limit
   - The system will stop using this provider if the limit is reached

### Saving Configuration

- Click **Save Configuration** to persist your changes
- Changes apply immediately without restart
- API keys are stored securely using encryption

### Testing Connections

- Each provider has a **Test Connection** button
- Only enabled when an API key is entered
- Shows success ✓ or failure ✗ with error message
- Validates the key with the actual provider API

## Quality Configuration

### Video Settings

Configure the video output quality:

1. **Resolution**
   - Choose from: 480p, 720p HD, 1080p Full HD, 1440p QHD, 4K UHD
   - Automatically sets width and height

2. **Frame Rate**
   - 24 fps - Cinematic look
   - 30 fps - Standard (recommended)
   - 60 fps - Smooth motion

3. **Codec**
   - H.264 - Universal compatibility (recommended)
   - H.265/HEVC - Better compression, smaller files
   - VP9 - Open format, web-friendly
   - AV1 - Modern, efficient (requires newer devices)

4. **Bitrate Preset**
   - Low (2000 Kbps)
   - Medium (3500 Kbps)
   - High (5000 Kbps) - recommended
   - Very High (8000 Kbps)
   - Custom - set your own bitrate

### Audio Settings

Configure the audio output quality:

1. **Bitrate**
   - 128 Kbps - Good quality
   - 192 Kbps - High quality (recommended)
   - 256 Kbps - Very high quality
   - 320 Kbps - Maximum quality

2. **Sample Rate**
   - 44.1 kHz - CD quality
   - 48 kHz - Professional standard (recommended)

3. **Channels**
   - Mono - Single channel
   - Stereo - Two channels (recommended)

### Subtitle Style

Customize the appearance of subtitles:

1. **Font**
   - Family: Enter any font name (e.g., "Arial", "Helvetica")
   - Size: Font size in pixels (recommended: 20-28)

2. **Colors**
   - Font Color: Text color (default: white)
   - Background Color: Background color (default: black)
   - Background Opacity: 0.0 (transparent) to 1.0 (opaque)

3. **Position**
   - Top - Subtitles at top of video
   - Middle - Subtitles in center
   - Bottom - Subtitles at bottom (recommended)

4. **Outline**
   - Width: Outline thickness in pixels (0-5)
   - Color: Outline color (default: black)

### Saving Quality Settings

- Click **Save Configuration** to persist changes
- Settings apply to all future video generation
- Changes take effect immediately

## Configuration Management

### Export Configuration

1. Navigate to **Settings** → **Import/Export**
2. Click **Export Configuration**
3. Downloads a JSON file with:
   - All provider configurations (without API keys)
   - Quality settings
   - All saved profiles

### Import Configuration

1. Navigate to **Settings** → **Import/Export**
2. Click **Import Configuration**
3. Select your JSON export file
4. Choose whether to overwrite existing settings
5. Click **Import**

### Configuration Profiles

Create and manage configuration profiles for different use cases:

1. Configure providers and quality as desired
2. Navigate to **Settings** → **Import/Export**
3. Click **Save as Profile**
4. Enter a name and description
5. Click **Save**

To load a profile:
1. Navigate to **Settings** → **Import/Export**
2. Select a profile from the list
3. Click **Load Profile**

### Reset to Defaults

If you want to start fresh:

1. Navigate to **Provider Configuration** or **Quality Settings**
2. Scroll to the bottom
3. Click **Reset to Defaults**
4. Confirm the action

This restores all settings to their default values.

## Best Practices

### Provider Configuration

1. **Test connections** after entering API keys to verify they work
2. **Set cost limits** for cloud providers to avoid unexpected charges
3. **Order by quality**: Place premium providers (OpenAI, Claude, ElevenLabs) first for best results
4. **Keep free fallbacks**: Always enable at least one free provider (Ollama, Windows SAPI) as fallback

### Quality Settings

1. **Start with defaults**: The default settings (1080p, 30fps, High bitrate) work well for most use cases
2. **Balance quality and size**: Higher quality = larger file sizes
3. **Match your hardware**: If rendering is slow, reduce resolution or framerate
4. **Test subtitle styles**: Preview a short video to ensure subtitles are readable

### Security

1. **Never share API keys**: Keep your configuration exports secure
2. **Test in safe environment**: Use test API keys when experimenting
3. **Set cost limits**: Protect against accidental overage charges
4. **Regular backups**: Export your configuration regularly

## Troubleshooting

### Provider Connection Fails

1. Verify the API key is correct (check for extra spaces)
2. Ensure you have an active subscription with the provider
3. Check your internet connection
4. Try the test connection again after a few minutes

### Settings Not Saving

1. Check browser console for error messages
2. Verify the Aura API backend is running
3. Try refreshing the page and saving again
4. Check disk space on your system

### Video Quality Issues

1. **Blurry video**: Increase bitrate or resolution
2. **Large file size**: Reduce bitrate, resolution, or switch to H.265 codec
3. **Choppy playback**: Reduce framerate or use a simpler codec (H.264)
4. **Poor audio**: Increase audio bitrate

## API Reference

For developers integrating with the configuration API:

### Provider Configuration Endpoints

- `GET /api/providerconfiguration/providers` - Get all provider configurations
- `POST /api/providerconfiguration/providers` - Save provider configurations
- `GET /api/providerconfiguration/models/{providerName}` - Get available models for provider
- `POST /api/providerconfiguration/validate` - Validate configuration

### Quality Configuration Endpoints

- `GET /api/providerconfiguration/quality` - Get quality configuration
- `POST /api/providerconfiguration/quality` - Save quality configuration

### Profile Management Endpoints

- `GET /api/providerconfiguration/profiles` - Get all profiles
- `POST /api/providerconfiguration/profiles` - Create new profile
- `GET /api/providerconfiguration/export` - Export configuration
- `POST /api/providerconfiguration/import` - Import configuration
- `POST /api/providerconfiguration/reset` - Reset to defaults

## Support

For additional help or to report issues:
- Check the main documentation at `/docs`
- Visit the project repository
- Check existing issues or create a new one

---

**Version**: 1.0.0  
**Last Updated**: November 2024
