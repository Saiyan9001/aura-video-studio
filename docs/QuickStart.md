# Quick Start Guide

Welcome to Aura Video Studio! This guide will walk you through creating your first video in just a few minutes.

## Prerequisites

Before you begin, make sure you have:
- Extracted the portable ZIP to a folder
- Windows 10/11 (64-bit)
- 4 GB RAM minimum (8 GB recommended)
- 2 GB free disk space
- A modern web browser (Chrome, Edge, or Firefox)

## Launch the Application

### Option 1: Using the Launcher (Recommended)
1. Double-click `Launch.bat` in the extracted folder
2. Your default browser will open to `http://127.0.0.1:5005`
3. Wait a few seconds for the application to load

### Option 2: Manual Launch
1. Navigate to the `Api` folder
2. Double-click `Aura.Api.exe`
3. Open your browser and go to `http://127.0.0.1:5005`

## Creating Your First Video

### Step 1: Project Creation
1. Click **Create** in the left navigation panel
2. Fill out the brief:
   - **Topic**: What your video is about (e.g., "How to brew pour-over coffee")
   - **Audience**: Who you're targeting (e.g., "Beginners")
   - **Goal**: What you want to achieve (e.g., "Educational")
   - **Tone**: Writing style (e.g., "Conversational")
   - **Language**: Content language (default: English)
3. Configure video settings:
   - **Aspect Ratio**: Choose based on platform
     - 16:9 Widescreen for YouTube
     - 9:16 Vertical for YouTube Shorts/TikTok
     - 1:1 Square for Instagram
   - **Duration**: Target video length (0.5 to 120 minutes)
   - **Pacing**: Speech speed
     - Slow: ~120 words/min (meditation, tutorials)
     - Conversational: ~150 words/min (standard)
     - Fast: ~190 words/min (news, recap)
   - **Density**: Information density
     - Sparse: Simple, one idea at a time
     - Balanced: Standard information flow
     - Dense: Information-rich content
   - **Style**: Video style
     - How-to: Step-by-step tutorials
     - Listicle: Numbered lists
     - Story: Narrative format
     - Educational: Teaching content
4. Click **Generate Script**

### Step 2: Preflight Check
After script generation:
1. Review the generated script in the preview panel
2. Check system capabilities:
   - The app automatically detects your hardware
   - Ensures you have required providers (FFmpeg)
   - Validates disk space for output
3. The preflight check runs automatically:
   - ✅ **Script validated**: Non-empty, correct format
   - ✅ **Providers ready**: FFmpeg available
   - ✅ **Disk space**: Sufficient storage available
   - ✅ **Output directory**: Configured and writable

If any preflight checks fail, see [Troubleshooting](./Troubleshooting.md).

### Step 3: Generate Video Assets
1. Click **Continue to Storyboard** or navigate to the **Storyboard** page
2. The storyboard view shows:
   - Timeline with scene markers
   - Script text for each scene
   - Visual placeholders (stock images or generated)
   - Audio waveform preview
3. Edit if needed:
   - Adjust scene timing by dragging markers
   - Edit script text directly
   - Replace visuals (coming soon)
4. Click **Generate Assets**:
   - **Text-to-Speech**: Converts script to voice
   - **Visuals**: Fetches stock images or generates with Stable Diffusion
   - **Subtitles**: Auto-generated with timing
   - Progress bars show real-time status

### Step 4: Render Video
1. Navigate to **Render** page
2. Select render preset:
   - **YouTube 1080p**: 1920x1080, 12 Mbps (recommended)
   - **YouTube Shorts**: 1080x1920 vertical
   - **YouTube 4K**: 3840x2160, 45 Mbps
   - **YouTube 1440p**: 2560x1440, 24 Mbps
   - **YouTube 720p**: 1280x720, 8 Mbps
   - **Instagram Square**: 1080x1080
3. Configure audio settings (optional):
   - Background music volume
   - Sound effects
   - Audio normalization (LUFS -16 default)
4. Click **Start Render**
5. Monitor progress:
   - Encoding progress bar
   - Estimated time remaining
   - Current encoder in use (software or NVENC)
6. Rendering typically takes:
   - Software encoding: 1-3x real-time
   - NVENC (NVIDIA GPU): 5-10x real-time

### Step 5: Open Output
Once rendering completes:
1. A success notification appears
2. Click **Open Output Folder** to view your video
3. Default location: `%USERPROFILE%\Videos\AuraVideoStudio\`
4. Your video file: `[project-name]-[timestamp].mp4`

## Next Steps

### Preview Your Video
- Use your default media player (Windows Media Player, VLC, etc.)
- Verify quality and content

### Publish Your Video
1. Navigate to the **Publish** page (coming soon)
2. Add metadata:
   - Title and description
   - Tags
   - Thumbnail (auto-generated or custom)
3. Optional: Direct upload to YouTube

### Manage Projects
- Navigate to **Library** to view past projects
- Re-open projects to make changes
- Export projects for backup

## Tips for Success

### Script Quality
- Keep your topic focused and specific
- Use clear, simple language for your audience
- Match tone to your content type

### Performance Optimization
- Close other applications during rendering
- Use NVENC if you have an NVIDIA GPU (6GB+ VRAM)
- Render shorter videos (< 5 min) first to test settings

### Free vs. Pro Providers
- **Free Path** (no API keys required):
  - Rule-based script generation
  - Windows TTS voices
  - Stock images (Pixabay/Pexels/Unsplash)
- **Pro Providers** (requires API keys):
  - OpenAI/Gemini for scripts
  - ElevenLabs/PlayHT for voice
  - Stable Diffusion for custom images

Configure providers in **Settings** → **API Keys** or **Local Providers**.

## Need Help?

- **Common Issues**: See [Troubleshooting](./Troubleshooting.md)
- **Keyboard Shortcuts**: See [Keyboard Shortcuts](./KeyboardShortcuts.md)
- **Provider Setup**: See [LOCAL_PROVIDERS_SETUP.md](../LOCAL_PROVIDERS_SETUP.md)
- **Portable Guide**: See [PORTABLE.md](../PORTABLE.md)

## Your First Video Checklist

- [x] Launch the application
- [x] Fill out project brief
- [x] Generate script
- [x] Pass preflight checks
- [x] Generate assets (TTS, visuals, subtitles)
- [x] Configure render settings
- [x] Start render
- [x] Open output folder
- [x] Preview your video

Congratulations! You've created your first video with Aura Video Studio! 🎉
