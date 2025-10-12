# Stable Diffusion Setup Guide

Complete guide for setting up Stable Diffusion locally for image generation in Aura Video Studio.

## Overview

Stable Diffusion enables high-quality, AI-generated images for your videos without cloud APIs or subscriptions. This guide covers installation, configuration, and troubleshooting.

## System Requirements

### Minimum Requirements
- **GPU**: NVIDIA GPU with CUDA support
- **VRAM**: 6GB (for SD 1.5)
- **RAM**: 8GB system RAM
- **Storage**: 15GB free space
- **OS**: Windows 11 x64 or Linux (Ubuntu 20.04+)

### Recommended Requirements
- **GPU**: NVIDIA RTX 3060 or better
- **VRAM**: 12GB+ (for SDXL)
- **RAM**: 16GB system RAM
- **Storage**: 50GB free space (for multiple models)

### Hardware Gating and Pre-Installation

**Important**: Aura now allows you to install engines even if your current hardware doesn't meet the requirements. This is useful for:
- **Planning ahead**: Install engines now, use them when you upgrade your hardware
- **Shared systems**: Install on a machine without NVIDIA GPU, then copy to another machine
- **Learning**: Explore the tools and prepare configurations before hardware upgrades

**What happens when you install without meeting requirements:**
1. The engine will install successfully
2. You can configure settings and explore the interface
3. The engine **will not auto-start** (requires manual start and will likely fail)
4. A clear warning is shown explaining the hardware limitation
5. Once you upgrade your hardware, the engine will work automatically

**Hardware Detection:**
- Aura automatically detects your GPU using nvidia-smi
- Cards show a ⚠️ warning if requirements aren't met
- You'll see messages like "Requires NVIDIA GPU" or "Requires 6GB VRAM (detected: 4GB)"
- The "Install anyway (for later)" button allows installation despite warnings

### Checking Your GPU

#### Windows
```powershell
nvidia-smi
```

#### Linux
```bash
nvidia-smi
```

Look for:
- GPU name
- VRAM total (Memory-Usage)
- CUDA Version

## Installation Methods

### Method 1: Automatic Installation (Recommended)

1. **Open Aura Video Studio**
2. **Go to Settings → Download Center → Engines**
3. **Find "Stable Diffusion WebUI"**
4. **Click "Install"**
   - Downloads A1111 WebUI portable
   - Installs to `%LOCALAPPDATA%\Aura\Tools\stable-diffusion-webui\`
   - Downloads SD 1.5 base model automatically
5. **Wait for installation to complete** (5-10 minutes)
6. **Click "Start"** to launch the engine

### Method 2: Manual Installation

#### Windows

1. **Download A1111 WebUI**
   ```powershell
   cd %LOCALAPPDATA%\Aura\Tools
   git clone https://github.com/AUTOMATIC1111/stable-diffusion-webui.git
   cd stable-diffusion-webui
   ```

2. **Run the installer**
   ```powershell
   .\webui.bat --api --nowebui
   ```

3. **Download models** (optional)
   - SD 1.5: https://huggingface.co/runwayml/stable-diffusion-v1-5
   - SDXL: https://huggingface.co/stabilityai/stable-diffusion-xl-base-1.0
   - Place in `models\Stable-diffusion\`

#### Linux

1. **Install dependencies**
   ```bash
   sudo apt update
   sudo apt install python3 python3-venv git wget
   ```

2. **Clone WebUI**
   ```bash
   mkdir -p ~/.local/share/aura/tools
   cd ~/.local/share/aura/tools
   git clone https://github.com/AUTOMATIC1111/stable-diffusion-webui.git
   cd stable-diffusion-webui
   ```

3. **Launch with API**
   ```bash
   ./webui.sh --api --nowebui
   ```

## Configuration

### Port Configuration
Default port: **7860**

To change:
1. **In Aura**: Settings → Engines → SD WebUI → Port
2. **Manual**: Edit launch arguments `--port 7861`

### Model Selection

#### SD 1.5 (Faster, Less VRAM)
- **Use when**: VRAM < 12GB
- **Generation time**: 10-30 seconds
- **Quality**: Good
- **Resolution**: 512x512 native

#### SDXL (Better Quality, More VRAM)
- **Use when**: VRAM >= 12GB
- **Generation time**: 30-60 seconds
- **Quality**: Excellent
- **Resolution**: 1024x1024 native

Aura automatically selects the best model based on your VRAM.

### Sampler and Steps

Aura uses optimized defaults:
- **Sampler**: DPM++ 2M Karras (fast, high quality)
- **Steps**: 20 (SD 1.5) or 30 (SDXL)

To override, edit in Settings → Engines → SD Advanced.

### Negative Prompts
Aura automatically adds quality-boosting negative prompts:
```
blurry, low quality, distorted, watermark, text, logo
```

## Managed vs Attached Mode

### Managed Mode (Default)
- Aura starts and stops SD WebUI automatically
- Logs are captured and displayed in UI
- Health checks ensure availability
- **Recommended for most users**

### Attached Mode (Advanced)
- You start SD WebUI manually
- Aura connects to existing instance
- Useful for:
  - Custom launch arguments
  - Debugging
  - Running multiple tools with same SD instance

To use attached mode:
1. Start SD WebUI manually with `--api` flag
2. In Aura: Settings → Engines → SD WebUI → Mode: **Attached**
3. Set correct URL (e.g., `http://127.0.0.1:7860`)

## Performance Optimization

### Speed Optimizations

1. **Use xformers** (faster attention)
   ```bash
   --xformers
   ```

2. **Enable half precision** (less VRAM)
   ```bash
   --medvram  # or --lowvram for very low VRAM
   ```

3. **Batch generation**
   - Generate multiple images per scene
   - Pick best result automatically

### Quality Optimizations

1. **Increase steps** (slower but better)
   - SD 1.5: 30-50 steps
   - SDXL: 40-60 steps

2. **Use better samplers**
   - DPM++ SDE Karras (slower, best quality)
   - Euler a (faster, creative)

3. **Add LoRAs** (style customization)
   - Place in `models/Lora/`
   - Reference in prompts: `<lora:name:0.8>`

## Troubleshooting

### SD WebUI Won't Start

#### Check GPU
```powershell
nvidia-smi
```
- Ensure GPU is detected
- Check VRAM is available
- Update drivers if needed

#### Check Logs
```
%LOCALAPPDATA%\Aura\logs\tools\stable-diffusion-webui.log
```

Common errors:
- **Out of memory**: Reduce batch size or use `--medvram`
- **CUDA error**: Update NVIDIA drivers
- **Port in use**: Change port in settings

### Generation Fails

#### Not Enough VRAM
- Switch to SD 1.5 instead of SDXL
- Add `--medvram` or `--lowvram` flag
- Reduce resolution (512x512 or 768x768)
- Close other GPU applications

#### Model Not Found
- Download model manually
- Place in `models\Stable-diffusion\`
- Restart SD WebUI
- Check model name in Aura settings

### Slow Generation

- **Expected times**:
  - SD 1.5: 10-30 seconds (6GB VRAM)
  - SDXL: 30-60 seconds (12GB VRAM)
- **Slower than expected?**
  - Close background apps
  - Check GPU usage: `nvidia-smi`
  - Reduce steps in settings
  - Use faster sampler (Euler a)

### Black Images

Usually caused by:
1. **NSFW filter triggered** - adjust prompt
2. **CUDA out of memory** - use `--medvram`
3. **Model corrupted** - re-download model

## Model Management

### Where Models Are Stored

Aura stores models in organized directories:

```
%LOCALAPPDATA%\Aura\Tools\stable-diffusion-webui\models\
├── Stable-diffusion\  (Base and refiner models)
├── VAE\               (VAE models)
└── Lora\              (LoRA models)
```

### Models & Voices Manager

Aura includes a built-in **Models & Voices Manager** to help you manage your AI models:

**Features:**
- View all installed models with exact file locations
- Install models from built-in catalogs
- Attach external model folders (read-only or read/write)
- Verify model checksums
- Open model folders directly from UI
- See model provenance and metadata

**Accessing the Manager:**
1. Open Download Center
2. Navigate to Engines tab
3. Click on Stable Diffusion or ComfyUI card
4. Click "Models & Voices" button

### Using Your Own Model Collections

If you already have a collection of Stable Diffusion models elsewhere on your system, you don't need to copy them:

1. Open Models & Voices Manager
2. Click "Add External Folder"
3. Select your existing model directory
4. Choose read-only (safe) or read/write
5. Models will be indexed and available immediately

**Benefits:**
- No duplicate files
- Use models from multiple locations
- Keep your organized structure
- Easy to manage

### Downloading Additional Models

#### From Aura UI
1. Download Center → Engines → Stable Diffusion
2. Click "Models & Voices"
3. Browse available models
4. Click "Install" on desired model
5. Model will be downloaded and verified

#### HuggingFace
1. Go to https://huggingface.co/models
2. Search for "stable diffusion"
3. Download `.safetensors` or `.ckpt` file
4. Either:
   - Place in `%LOCALAPPDATA%\Aura\Tools\stable-diffusion-webui\models\Stable-diffusion\`
   - Or use "Add External Folder" to index your download location

#### Civitai (Community Models)
1. Go to https://civitai.com/
2. Browse models (anime, realistic, etc.)
3. Download model file
4. Use Models & Voices Manager to add or install
5. **Note**: Check license restrictions

### Model File Formats
- `.safetensors` - **Recommended** (safe, efficient)
- `.ckpt` - Legacy format (still works)
- `.pt` - PyTorch format (some LoRAs)

### Path Conventions

**Default Paths:**
- Base Models: `%LOCALAPPDATA%\Aura\Tools\stable-diffusion-webui\models\Stable-diffusion\`
- VAE: `%LOCALAPPDATA%\Aura\Tools\stable-diffusion-webui\models\VAE\`
- LoRA: `%LOCALAPPDATA%\Aura\Tools\stable-diffusion-webui\models\Lora\`

**External Paths:**
- Can be anywhere on your system
- Indexed through Models & Voices Manager
- Paths are stored in Aura's configuration
- Files remain in original location

## Advanced Features

### VAE (Image Enhancement)
Improve color/detail:
1. Download VAE (e.g., `vae-ft-mse-840000-ema-pruned.safetensors`)
2. Place in `models\VAE\`
3. Select in WebUI settings

### ControlNet (Precise Control)
Coming soon - allows image-to-image with pose/depth control.

### Image-to-Image
Use existing images as starting point:
1. Place reference images in project folder
2. Aura will automatically use i2i mode
3. Adjust denoising strength (0.3-0.7)

## Security and Privacy

### Local Processing
- All generation happens on your PC
- No data sent to external services
- Models run entirely offline

### Model Safety
- Use models from trusted sources
- Verify checksums when possible
- Check licenses for commercial use

### Network Binding
SD WebUI binds to `127.0.0.1` only:
- Not accessible from other devices
- Safe for local use
- Use `--listen` flag only if needed

## Updating SD WebUI

### Automatic (Recommended)
1. Go to Settings → Download Center → Engines
2. If update available, click **Update**

### Manual
```bash
cd %LOCALAPPDATA%\Aura\Tools\stable-diffusion-webui
git pull
```

## Uninstalling

### From Aura
1. Settings → Download Center → Engines
2. Click **Remove** on SD WebUI
3. Confirm deletion

### Manual Cleanup
```powershell
Remove-Item -Recurse -Force "%LOCALAPPDATA%\Aura\Tools\stable-diffusion-webui"
```

## Resources

- [A1111 WebUI Wiki](https://github.com/AUTOMATIC1111/stable-diffusion-webui/wiki)
- [Stable Diffusion Subreddit](https://www.reddit.com/r/StableDiffusion/)
- [Model Collections](https://civitai.com/)
- [Prompt Guide](https://prompthero.com/stable-diffusion-prompts)

## Support

For issues specific to Aura integration:
- [GitHub Issues](https://github.com/Coffee285/aura-video-studio/issues)
- Tag: `engine:stable-diffusion`
