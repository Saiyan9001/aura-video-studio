# Troubleshooting Guide

This guide covers common issues and their solutions when using Aura Video Studio.

## Table of Contents

- [Script Generation Failures](#script-generation-failures)
- [Enum Errors (E303, E304)](#enum-errors-e303-e304)
- [Preflight Failures](#preflight-failures)
- [Provider Validation Issues](#provider-validation-issues)
- [Offline Mode](#offline-mode)
- [Diffusion Gating (NVIDIA GPU)](#diffusion-gating-nvidia-gpu)
- [Disk Space Issues](#disk-space-issues)
- [API and Network Issues](#api-and-network-issues)
- [Video Rendering Issues](#video-rendering-issues)

---

## Script Generation Failures

### Error: "Failed to generate script"

**Cause**: The script provider encountered an error or returned empty content.

**Solutions**:
1. Check your internet connection (required for free rule-based provider)
2. Verify the topic is specific and well-defined
3. Try a shorter target duration
4. Check the API logs in `logs/aura-api-[date].log`

**Error Code**: E300 - Script provider failed

### Error: Script generation timeout

**Cause**: The script generation took too long or network connection dropped.

**Solutions**:
1. Check your internet connection
2. Try again - the service may be temporarily busy
3. Reduce target duration
4. Consider using a pro provider with API key

**Error Code**: E301 - Script timeout

### Error: "Script generation returned empty result"

**Cause**: The provider completed but didn't return any content.

**Solutions**:
1. Verify your topic is clear and appropriate
2. Check if the provider service is operational
3. Try a different topic or briefing
4. Review logs for provider-specific errors

**Error Code**: E302 - Empty script result

---

## Enum Errors (E303, E304)

### Error E303: "Invalid Brief"

**Cause**: Required fields are missing or invalid in your project brief.

**Common Issues**:
- Empty topic field
- Topic contains only whitespace
- Invalid characters in topic

**Solutions**:
1. Ensure **Topic** field is filled with valid text
2. Use descriptive topics (minimum 3 characters)
3. Avoid special characters: `< > : " / \ | ? *`

**Example Valid Topic**: "How to brew pour-over coffee"

### Error E304: "Invalid Plan"

**Cause**: Target duration is outside the acceptable range.

**Valid Range**: 0.5 to 120 minutes

**Solutions**:
1. Check target duration is between 0.5 and 120 minutes
2. For very short videos, use 0.5 minutes minimum
3. For longer content, break into multiple videos
4. Ensure duration is a positive number

**Example Valid Durations**:
- Short: 0.5 - 2 minutes
- Standard: 2 - 10 minutes
- Long: 10 - 30 minutes

---

## Preflight Failures

Preflight checks run before video generation to ensure all requirements are met.

### FFmpeg Not Found

**Symptoms**:
- "FFmpeg executable not found"
- Render button is disabled

**Solutions**:
1. Go to **Downloads** page
2. Click **Install** next to FFmpeg
3. Wait for download to complete
4. Verify installation in **Settings** → **Local Providers**
5. Click **Test** next to FFmpeg

Alternative: Manual installation
1. Download FFmpeg from https://www.gyan.dev/ffmpeg/builds/
2. Extract to a folder (e.g., `C:\Tools\ffmpeg`)
3. Go to **Settings** → **Local Providers**
4. Set paths:
   - FFmpeg Path: `C:\Tools\ffmpeg\bin\ffmpeg.exe`
   - FFprobe Path: `C:\Tools\ffmpeg\bin\ffprobe.exe`
5. Click **Save** and **Test**

### Output Directory Not Writable

**Symptoms**:
- "Cannot write to output directory"
- Permission denied errors

**Solutions**:
1. Check the output directory in **Settings** → **Local Providers**
2. Default: `%USERPROFILE%\Videos\AuraVideoStudio\`
3. Ensure you have write permissions
4. Try a different directory if needed
5. Run as Administrator if necessary

### Insufficient Disk Space

**Symptoms**:
- Preflight warning about disk space
- Render fails partway through

**Solutions**:
1. Check available space on target drive
2. Required space: ~500 MB per minute of video
3. Clear temp files: `%TEMP%\Aura\`
4. Change output directory to drive with more space
5. Delete old project files from Library

---

## Provider Validation Issues

### Provider Connection Failed

**Symptoms**:
- Red X or error icon next to provider
- "Connection test failed"

**For Stable Diffusion**:
1. Ensure WebUI is running (`webui-user.bat`)
2. Check URL: `http://127.0.0.1:7860`
3. Open the URL in browser to verify
4. Look for firewall blocking port 7860
5. Review WebUI console for errors

**For Ollama**:
1. Ensure Ollama service is running
2. Check URL: `http://127.0.0.1:11434`
3. Test with: `curl http://127.0.0.1:11434/api/version`
4. Restart Ollama service if needed

**For FFmpeg**:
1. Verify executable path is correct
2. Test in Command Prompt: `ffmpeg -version`
3. Re-download if file is corrupted
4. Check antivirus didn't quarantine files

### API Key Invalid

**Symptoms**:
- "API key invalid or expired"
- 401 Unauthorized errors

**Solutions**:
1. Verify API key is correctly copied (no extra spaces)
2. Check key hasn't expired
3. Verify billing/credits on provider's website
4. Regenerate key if necessary
5. Update key in **Settings** → **API Keys**

### Provider Rate Limited

**Symptoms**:
- "Rate limit exceeded"
- 429 Too Many Requests

**Solutions**:
1. Wait 60 seconds and try again
2. Upgrade to pro plan on provider's site
3. Use free providers as fallback
4. Space out render jobs
5. Check provider's rate limits

---

## Offline Mode

### Enabling Offline Mode

Offline mode uses only local resources - no internet required.

**To Enable**:
1. Go to **Settings** → **System**
2. Toggle **Offline Mode** ON
3. Limitations:
   - No cloud script generation
   - No stock image downloads
   - No API-based TTS
   - Local providers only

**Requirements for Offline**:
- FFmpeg installed locally
- Windows TTS voices available
- Local image assets or Stable Diffusion
- Ollama for local LLM (optional)

### Offline Mode Troubleshooting

**Script Generation Not Available**:
- Install Ollama for local LLM generation
- Or prepare scripts manually

**No Images Available**:
- Use local image folder
- Set up Stable Diffusion WebUI
- Or use slide deck style (text only)

**TTS Voices Limited**:
- Windows TTS uses built-in voices
- Install additional voice packs from Windows settings
- Or use pre-recorded audio files

---

## Diffusion Gating (NVIDIA GPU)

Stable Diffusion requires an NVIDIA GPU with 6GB+ VRAM.

### "Stable Diffusion Not Available"

**Cause**: Hardware doesn't meet requirements.

**Requirements**:
- NVIDIA GPU (GTX 1660 Ti or better)
- 6GB+ VRAM
- CUDA-compatible drivers

**Check Your GPU**:
1. Open **Settings** → **System Profile**
2. Click **Auto-Detect**
3. View detected GPU information

**If You Have NVIDIA GPU But It's Not Detected**:
1. Update NVIDIA drivers from https://www.nvidia.com/drivers
2. Verify CUDA is installed
3. Restart application
4. Re-run hardware detection

**If You Don't Have NVIDIA GPU**:
- Use stock images (Pixabay/Pexels/Unsplash)
- Use slide deck with text
- Consider cloud rendering services (coming soon)

### WebUI Won't Start

**Solutions**:
1. Ensure Python 3.10.6 is installed
2. Run `webui-user.bat` from Command Prompt
3. Check for error messages in console
4. Review `webui-user.log` file
5. Reinstall WebUI if corrupted

### Out of VRAM Errors

**Symptoms**:
- "CUDA out of memory"
- WebUI crashes during generation

**Solutions**:
1. Close other GPU-intensive applications
2. Lower resolution settings in WebUI
3. Use `--medvram` or `--lowvram` flags
4. Reduce batch size to 1
5. Restart WebUI between generations

---

## Disk Space Issues

### Insufficient Space During Render

**Symptoms**:
- Render fails at 60-80% progress
- "No space left on device" error

**Required Space**:
- Video output: ~500 MB per minute
- Temp files: ~200 MB per minute
- Audio cache: ~50 MB
- Total: ~750 MB per minute of video

**Solutions**:
1. Check available space: `%USERPROFILE%\Videos\AuraVideoStudio\`
2. Clean temp directory: `%TEMP%\Aura\`
3. Delete old projects from Library
4. Move output directory to larger drive
5. Compress or archive old videos

### Cache Directory Full

**Symptoms**:
- Application slow or unresponsive
- Disk space warnings

**Solutions**:
1. Go to **Settings** → **Cache**
2. Click **Clear Cache**
3. Set cache size limit (default: 5 GB)
4. Enable auto-cleanup of old files
5. Manually delete: `%LOCALAPPDATA%\Aura\cache\`

---

## API and Network Issues

### API Won't Start

**Symptoms**:
- Console window closes immediately
- "Port 5005 already in use"

**Solutions**:
1. Check if another Aura instance is running
2. Kill process using port 5005:
   ```powershell
   netstat -ano | findstr :5005
   taskkill /PID <process_id> /F
   ```
3. Change port in `appsettings.json` (advanced)
4. Restart computer to clear stuck processes
5. Check antivirus/firewall settings

### Web UI Won't Load

**Symptoms**:
- Browser shows 404 error
- "Cannot GET /"

**Solutions**:
1. Verify API is running (console window open)
2. Wait 10-15 seconds for full startup
3. Refresh browser page (F5)
4. Check `wwwroot` folder exists in `Api\wwwroot\`
5. Try different browser
6. Clear browser cache

### CORS Errors in Console

**Symptoms**:
- API requests fail
- Console shows CORS policy errors

**Solutions**:
1. Ensure web UI is accessed via `http://127.0.0.1:5005`
2. Not `localhost` or other IP addresses
3. Clear browser cache and cookies
4. Restart both API and browser
5. Check firewall isn't blocking requests

---

## Video Rendering Issues

### Render Fails Immediately

**Solutions**:
1. Verify FFmpeg is installed and working
2. Check output directory is writable
3. Ensure sufficient disk space
4. Review logs: `logs/aura-api-[date].log`
5. Test FFmpeg manually: `ffmpeg -version`

### Render Stalls or Hangs

**Solutions**:
1. Check Task Manager for CPU/disk usage
2. Verify FFmpeg process is running
3. Check disk isn't full
4. Kill and restart render if needed
5. Try lower quality preset

### Poor Video Quality

**Solutions**:
1. Use higher bitrate preset (4K, 1440p)
2. Check source images are high resolution
3. Verify audio quality in TTS settings
4. Enable NVENC for better encoding (NVIDIA GPU)
5. Increase audio bitrate in settings

### Audio Out of Sync

**Solutions**:
1. Regenerate TTS with correct timing
2. Verify scene durations match script
3. Check audio sample rate (48 kHz recommended)
4. Re-render with same frame rate
5. Report issue with logs for investigation

---

## Getting More Help

### Check Logs
Most errors are logged with details:
- API logs: `logs/aura-api-[date].log`
- WebUI logs: Browser DevTools → Console
- System logs: Event Viewer → Application

### Report Issues
If problems persist:
1. Collect error messages and logs
2. Note your system specifications
3. Document steps to reproduce
4. Open issue on GitHub with details

### Related Documentation
- [Quick Start Guide](./QuickStart.md)
- [Keyboard Shortcuts](./KeyboardShortcuts.md)
- [Local Providers Setup](../LOCAL_PROVIDERS_SETUP.md)
- [Portable Guide](../PORTABLE.md)

---

## Common Error Codes Reference

| Code | Title | Description | Solution |
|------|-------|-------------|----------|
| E300 | Script Provider Failed | Provider error during generation | Check logs, verify connection |
| E301 | Script Timeout | Generation took too long | Check connection, reduce duration |
| E302 | Empty Script | Provider returned no content | Try different topic, check provider |
| E303 | Invalid Brief | Missing or invalid topic | Fill required fields correctly |
| E304 | Invalid Plan | Duration out of range | Use 0.5-120 minutes |

---

**Last Updated**: December 2024  
**For**: Aura Video Studio Portable Edition
