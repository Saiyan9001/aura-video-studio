# Local TTS Setup Guide

Complete guide for setting up local text-to-speech engines (Piper and Mimic3) in Aura Video Studio.

## Overview

Local TTS engines provide high-quality, offline voice synthesis without cloud APIs or recurring costs. Choose between:
- **Piper**: Ultra-fast, lightweight, perfect for quick iterations
- **Mimic3**: Higher quality, more natural voices, slightly slower

## Comparison Matrix

| Feature | Windows SAPI | Piper | Mimic3 | ElevenLabs |
|---------|-------------|-------|---------|------------|
| **Quality** | Good | Very Good | Excellent | Best |
| **Speed** | Fast | Very Fast | Medium | Slow |
| **Offline** | ✅ Yes | ✅ Yes | ✅ Yes | ❌ No |
| **Cost** | Free | Free | Free | Paid |
| **Voices** | ~10 | 50+ | 100+ | 1000+ |
| **Languages** | Limited | 20+ | 30+ | 50+ |
| **Setup** | None | Easy | Medium | Account |

## Piper TTS

### System Requirements
- **RAM**: 512MB per voice
- **Storage**: 50-200MB per voice
- **CPU**: Any modern CPU (no GPU needed)
- **OS**: Windows, Linux, macOS

### Installation

#### Automatic (Recommended)
1. **Open Aura Video Studio**
2. **Go to Settings → Download Center → Engines**
3. **Find "Piper TTS"**
4. **Click "Install"**
5. **Download voice models** (next section)

#### Manual Installation

**Windows:**
```powershell
# Download Piper binary
$url = "https://github.com/rhasspy/piper/releases/latest/download/piper_windows_amd64.zip"
Invoke-WebRequest -Uri $url -OutFile piper.zip
Expand-Archive piper.zip -DestinationPath "%LOCALAPPDATA%\Aura\Tools\piper"
```

**Linux:**
```bash
# Download Piper binary
wget https://github.com/rhasspy/piper/releases/latest/download/piper_linux_x86_64.tar.gz
tar -xzf piper_linux_x86_64.tar.gz -C ~/.local/share/aura/tools/piper
chmod +x ~/.local/share/aura/tools/piper/piper
```

### Voice Models

#### Where Voices Are Stored

```
%LOCALAPPDATA%\Aura\Tools\piper\voices\
```

Each voice requires two files:
- `.onnx` - The voice model file
- `.onnx.json` - Voice configuration

#### Models & Voices Manager

Aura includes a built-in **Models & Voices Manager** for Piper voices:

**Features:**
- View all installed voices with exact file locations
- Install voices from built-in catalogs
- Attach external voice folders (for existing voice collections)
- Play voice samples
- Set default voice
- Verify voice file integrity

**Accessing the Manager:**
1. Open Download Center
2. Navigate to Engines tab
3. Click on Piper TTS card
4. Click "Models & Voices" button

#### Using Your Own Voice Collections

If you have Piper voices stored elsewhere:

1. Open Models & Voices Manager
2. Click "Add External Folder"
3. Select your voice directory
4. Voices will be indexed immediately
5. No files are moved or copied

**Benefits:**
- No duplicate files
- Use voices from multiple locations
- Keep your organized structure
- Share voices across applications

#### Popular English Voices

| Voice | Quality | Size | Speed | Style |
|-------|---------|------|-------|-------|
| `en_US-lessac-medium` | ⭐⭐⭐⭐ | 63MB | Fast | Neutral, clear |
| `en_US-amy-medium` | ⭐⭐⭐⭐ | 63MB | Fast | Warm, friendly |
| `en_US-ryan-high` | ⭐⭐⭐⭐⭐ | 101MB | Medium | Professional |
| `en_GB-alba-medium` | ⭐⭐⭐⭐ | 63MB | Fast | British accent |

#### Downloading Voices

**From Aura UI:**
1. Download Center → Engines → Piper
2. Click "Models & Voices"
3. Browse available voices
4. Click **Install** on desired voice
5. Voice will be downloaded and verified

**Manual Download:**
```powershell
# Example: Download en_US-lessac-medium
$voice = "en_US-lessac-medium"
$baseUrl = "https://huggingface.co/rhasspy/piper-voices/resolve/main/en/en_US/lessac/medium"
Invoke-WebRequest -Uri "$baseUrl/en_US-lessac-medium.onnx" -OutFile "en_US-lessac-medium.onnx"
Invoke-WebRequest -Uri "$baseUrl/en_US-lessac-medium.onnx.json" -OutFile "en_US-lessac-medium.onnx.json"
```

Place files in: `%LOCALAPPDATA%\Aura\Tools\piper\voices\`

Or use Models & Voices Manager to add the directory containing your downloads.

#### All Supported Languages
English, Spanish, French, German, Dutch, Italian, Polish, Russian, Ukrainian, Chinese, Japanese, Korean, Vietnamese, and more.

Full list: https://github.com/rhasspy/piper#voices

### Path Conventions

**Default Paths:**
- Piper Voices: `%LOCALAPPDATA%\Aura\Tools\piper\voices\`
- Mimic3 Voices: `%LOCALAPPDATA%\Aura\Tools\mimic3\voices\`

**External Paths:**
- Can be anywhere on your system
- Indexed through Models & Voices Manager
- Paths are stored in Aura's configuration
- Files remain in original location
- Changes to external folders are reflected automatically

### Configuration

#### Setting Default Voice
1. Settings → Engines → Piper
2. **Default Voice**: Select from dropdown
3. Save

#### Performance Tuning
Piper is already optimized, but you can:
- Use `low` quality voices for faster synthesis (smaller files)
- Use `high` quality voices for best quality (larger files, slightly slower)

### Usage in Wizard

1. Create new video
2. **Step 3: Voice and Narration**
3. **Provider**: Select "Piper (Local)"
4. **Voice**: Choose from installed voices
5. **Rate**: 0.8-1.2 (normal speaking speed)
6. Continue workflow

### Testing Piper

**Command Line Test:**
```powershell
cd "%LOCALAPPDATA%\Aura\Tools\piper"
echo "Hello, this is a test." | .\piper.exe --model .\voices\en_US-lessac-medium.onnx --output_file test.wav
```

Play `test.wav` to verify installation.

## Mimic3 TTS

### System Requirements
- **RAM**: 2-4GB
- **Storage**: 500MB + voice models (200MB each)
- **CPU**: Modern CPU (multi-core recommended)
- **OS**: Windows, Linux, macOS

### Installation

#### Automatic (Recommended)
1. **Open Aura Video Studio**
2. **Go to Settings → Download Center → Engines**
3. **Find "Mimic3 TTS"**
4. **Click "Install"**
5. **Wait for installation** (downloads Python environment)

#### Manual Installation

**Windows (using conda):**
```powershell
# Install Mimic3
conda create -n mimic3 python=3.9
conda activate mimic3
pip install mycroft-mimic3-tts[all]
```

**Linux:**
```bash
# Install Mimic3
python3 -m venv ~/.local/share/aura/tools/mimic3/venv
source ~/.local/share/aura/tools/mimic3/venv/bin/activate
pip install mycroft-mimic3-tts[all]
```

### Running Mimic3 Server

**Automatic (via Aura):**
1. Settings → Download Center → Engines
2. Click **Start** on Mimic3

**Manual:**
```bash
mimic3-server --port 59125
```

Server will be available at: `http://127.0.0.1:59125`

### Voice Models

#### High-Quality English Voices

| Voice | Quality | Size | Speed | Style |
|-------|---------|------|-------|-------|
| `en_UK/apope_low` | ⭐⭐⭐⭐⭐ | 250MB | Medium | British, professional |
| `en_US/vctk_low` | ⭐⭐⭐⭐ | 200MB | Fast | American, neutral |
| `en_US/hifi-tts_low` | ⭐⭐⭐⭐⭐ | 280MB | Slow | Very natural |

#### Downloading Voices

**From Aura:**
1. Settings → Engines → Mimic3 → Manage Voices
2. Browse and download

**Automatic Download:**
Mimic3 downloads voices on first use automatically.

**Manual Download:**
```bash
mimic3-download "en_US/vctk_low"
```

### Configuration

#### Port Configuration
Default: **59125**

To change:
1. Settings → Engines → Mimic3 → Port
2. Restart Mimic3

#### Voice Selection
1. Settings → Engines → Mimic3 → Default Voice
2. Choose from available voices
3. Save

### Usage in Wizard

1. Create new video
2. **Step 3: Voice and Narration**
3. **Provider**: Select "Mimic3 (Local)"
4. **Voice**: Choose voice (e.g., `en_US/vctk_low`)
5. Continue workflow

### Testing Mimic3

**HTTP Test:**
```powershell
# Test synthesis
$text = "Hello, this is a test."
$voice = "en_US/vctk_low"
Invoke-WebRequest -Method POST -Uri "http://127.0.0.1:59125/api/tts?voice=$voice" -Body $text -OutFile test.wav
```

**Browser Test:**
Visit: `http://127.0.0.1:59125`

## Comparison: Piper vs Mimic3

### When to Use Piper
- ✅ Need fast synthesis (100x real-time)
- ✅ Limited RAM/CPU
- ✅ Quick iterations during editing
- ✅ Simple, reliable setup

### When to Use Mimic3
- ✅ Need best quality
- ✅ Publishing final videos
- ✅ Have 4GB+ RAM available
- ✅ Okay with slower synthesis

### Quality Comparison
Listen to samples: https://github.com/rhasspy/piper#samples

Generally:
- **Piper high quality** ≈ **Mimic3 low quality**
- **Mimic3 medium/high** > **Piper**
- **ElevenLabs** > **Mimic3 high**

## Troubleshooting

### Piper Issues

#### Voice Not Found
```
Error: Voice model not found
```
**Solution:**
1. Check voice file exists in `%LOCALAPPDATA%\Aura\Tools\piper\voices\`
2. Both `.onnx` and `.onnx.json` files required
3. Re-download voice if corrupted

#### Piper Binary Not Executable (Linux)
```bash
chmod +x ~/.local/share/aura/tools/piper/piper
```

#### Synthesis Fails
- Check Piper logs: `%LOCALAPPDATA%\Aura\logs\tools\piper.log`
- Verify text encoding (UTF-8)
- Try shorter text samples

### Mimic3 Issues

#### Server Won't Start
**Port in use:**
```bash
# Check port
netstat -ano | findstr :59125  # Windows
lsof -i :59125  # Linux

# Kill process or change port
```

**Python errors:**
```bash
# Reinstall Mimic3
pip uninstall mycroft-mimic3-tts
pip install mycroft-mimic3-tts[all]
```

#### Voice Download Fails
- Check internet connection
- Try manual download:
  ```bash
  mimic3-download "en_US/vctk_low"
  ```

#### Slow Synthesis
- Expected: ~10x real-time
- If slower:
  - Close background apps
  - Use `low` quality voice
  - Check CPU usage

### Quality Issues

#### Robotic Voice
- Try different voice model
- Increase quality level (medium → high)
- Adjust speech rate in Aura settings

#### Mispronunciations
- Use SSML markup (planned feature)
- Try alternative voice
- Edit text phonetically

## Advanced Usage

### Custom Voice Training (Piper)

Coming soon - train custom voices from audio samples.

### SSML Support

Planned support for:
- Emphasis: `<emphasis>important</emphasis>`
- Breaks: `<break time="500ms"/>`
- Prosody: `<prosody rate="slow">text</prosody>`

### Multi-Language Support

Both engines support multiple languages per project:
1. Install voice models for each language
2. Specify language per scene
3. Aura auto-switches voices

## Performance Benchmarks

Tested on: AMD Ryzen 5 5600X, 16GB RAM

| Engine | Speed | 100 words |
|--------|-------|-----------|
| Windows SAPI | 5x real-time | 12 seconds |
| Piper (medium) | 100x real-time | 0.6 seconds |
| Piper (high) | 50x real-time | 1.2 seconds |
| Mimic3 (low) | 10x real-time | 6 seconds |
| Mimic3 (high) | 5x real-time | 12 seconds |

## Resources

### Piper
- [GitHub](https://github.com/rhasspy/piper)
- [Voice Samples](https://rhasspy.github.io/piper-samples/)
- [All Voices List](https://huggingface.co/rhasspy/piper-voices)

### Mimic3
- [GitHub](https://github.com/MycroftAI/mimic3)
- [Documentation](https://mycroft-ai.gitbook.io/docs/mycroft-technologies/mimic-tts/mimic-3)
- [Voice Models](https://github.com/MycroftAI/mimic3-voices)

## Support

For Aura integration issues:
- [GitHub Issues](https://github.com/Coffee285/aura-video-studio/issues)
- Tag: `engine:tts` or `engine:piper` or `engine:mimic3`
