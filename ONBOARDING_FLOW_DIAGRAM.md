# Onboarding + Diagnostics Flow Diagram

## Enhanced First-Run Wizard Flow

```
┌──────────────────────────────────────────────────────────────────────────┐
│                        STEP 0: MODE SELECTION                             │
├──────────────────────────────────────────────────────────────────────────┤
│  Choose your mode:                                                        │
│  ○ Free-Only (Rule-based, Windows TTS, Stock images)                     │
│  ○ Local (Ollama, Piper, Stable Diffusion)                              │
│  ○ Pro (OpenAI, ElevenLabs, Stability AI)                               │
└──────────────────────────────────────────────────────────────────────────┘
                                    ↓
┌──────────────────────────────────────────────────────────────────────────┐
│                      STEP 1: HARDWARE DETECTION                           │
├──────────────────────────────────────────────────────────────────────────┤
│  Detecting GPU...                                                         │
│  ✓ GPU: NVIDIA RTX 3080                                                  │
│  ✓ VRAM: 10GB                                                            │
│  ✓ Can run Stable Diffusion locally!                                     │
└──────────────────────────────────────────────────────────────────────────┘
                                    ↓
┌──────────────────────────────────────────────────────────────────────────┐
│                 STEP 2: INSTALL REQUIRED COMPONENTS                       │
├──────────────────────────────────────────────────────────────────────────┤
│                                                                           │
│  ✓ FFmpeg (Video encoding) [Required]                                   │
│    Path: C:\Tools\ffmpeg                                                 │
│                                                                           │
│  Ollama (Local AI) [Optional]                                            │
│  ┌────────────────────────────────────────────────────────┐             │
│  │  [Install]  [Use Existing...]  [Skip for now]         │ ← NEW!      │
│  └────────────────────────────────────────────────────────┘             │
│                                                                           │
│  Stable Diffusion WebUI [Optional]                                       │
│  ┌────────────────────────────────────────────────────────┐             │
│  │  [Install]  [Use Existing...]  [Skip for now]         │ ← NEW!      │
│  └────────────────────────────────────────────────────────┘             │
│                                                                           │
│  Piper TTS [Optional]                                                    │
│  [Skipped]                                                               │
│                                                                           │
└──────────────────────────────────────────────────────────────────────────┘
                                    ↓
                        Click "Use Existing..."
                                    ↓
┌──────────────────────────────────────────────────────────────────────────┐
│                   PATH PICKER DIALOG (NEW!)                               │
├──────────────────────────────────────────────────────────────────────────┤
│  Use Existing Installation                                                │
│                                                                           │
│  Enter the path to your existing Stable Diffusion installation:          │
│                                                                           │
│  Installation Path:                                                       │
│  ┌──────────────────────────────────────────────────────┐               │
│  │ C:\stable-diffusion-webui                            │               │
│  └──────────────────────────────────────────────────────┘               │
│                                                                           │
│  Provide the full path to the installation directory                     │
│  or executable.                                                           │
│                                                                           │
│                              [Cancel]  [Use This Path]                   │
└──────────────────────────────────────────────────────────────────────────┘
                                    ↓
┌──────────────────────────────────────────────────────────────────────────┐
│                    STEP 3: VALIDATION & DEMO                              │
├──────────────────────────────────────────────────────────────────────────┤
│  ✅ All Set!                                                              │
│                                                                           │
│  Your system is ready to create amazing videos.                          │
│  Let's create your first project!                                        │
│                                                                           │
│  [Create My First Video]  [Go to Settings]                               │
│                                                                           │
│  ┌────────────────────────────────────────────────────────┐             │
│  │ 📁 Where are my files? (NEW!)                          │             │
│  ├────────────────────────────────────────────────────────┤             │
│  │ Here's where your engines and models are stored:       │             │
│  │                                                         │             │
│  │ FFmpeg (Video encoding)                                │             │
│  │ C:\Users\User\AppData\Local\Aura\Tools\ffmpeg         │             │
│  │                              [Open Folder]             │ ← NEW!     │
│  │                                                         │             │
│  │ Stable Diffusion WebUI                                 │             │
│  │ C:\stable-diffusion-webui                              │             │
│  │              [Open Folder]  [Open Web UI]              │ ← NEW!     │
│  │                                                         │             │
│  │ 💡 Tip: To add models, place them in                   │             │
│  │ [install]/models/Stable-diffusion/                     │             │
│  └────────────────────────────────────────────────────────┘             │
└──────────────────────────────────────────────────────────────────────────┘
```

## Download Failure → Self-Healing Flow

```
┌──────────────────────────────────────────────────────────────────────────┐
│                         ENGINE INSTALLATION                               │
├──────────────────────────────────────────────────────────────────────────┤
│  Installing Stable Diffusion WebUI...                                    │
│                                                                           │
│  ❌ Download failed!                                                      │
│  ⚠️ Installation failed: 404 Not Found                                   │
│                                                                           │
│  ℹ️  Why did this fail? Show diagnostics  ← Click here                  │
└──────────────────────────────────────────────────────────────────────────┘
                                    ↓
┌──────────────────────────────────────────────────────────────────────────┐
│               DOWNLOAD DIAGNOSTICS DIALOG (NEW!)                          │
├──────────────────────────────────────────────────────────────────────────┤
│  Download Diagnostics - Stable Diffusion WebUI                           │
│                                                                           │
│  ┌────────────────────────────────────────────────────────┐             │
│  │ Error Code: E-DL-404                                   │             │
│  │                                                         │             │
│  │ 404 Not Found: The download URL is no longer          │             │
│  │ available. The file may have been moved or removed    │             │
│  │ from the server.                                       │             │
│  └────────────────────────────────────────────────────────┘             │
│                                                                           │
│  Failed URL: https://github.com/AUTOMATIC1111/...                        │
│                                                                           │
│  ┌────────────────────────────────────────────────────────┐             │
│  │ Fix Options:                                           │             │
│  │                                                         │             │
│  │ 📁 Use Existing Installation                           │ ← Option 1  │
│  │ If you already have SD installed elsewhere, point     │             │
│  │ to its location.                                       │             │
│  │                                                         │             │
│  │ Installation Path:                                     │             │
│  │ [C:\stable-diffusion-webui                    ]        │             │
│  │ [Use This Path]                                        │             │
│  │                                                         │             │
│  │ ─────────────────────────────────────────────          │             │
│  │                                                         │             │
│  │ 🔗 Try Custom Download URL                             │ ← Option 2  │
│  │ Use an alternative mirror or download URL if the      │             │
│  │ default one is unavailable.                            │             │
│  │                                                         │             │
│  │ Custom URL:                                            │             │
│  │ [https://huggingface.co/...                   ]        │             │
│  │ [Download from This URL]                               │             │
│  │                                                         │             │
│  │ ─────────────────────────────────────────────          │             │
│  │                                                         │             │
│  │ 📄 Install from Local File                             │ ← Option 3  │
│  │ If you've already downloaded the file manually,       │             │
│  │ install it from your local disk.                       │             │
│  │                                                         │             │
│  │ Local File Path:                                       │             │
│  │ [C:\Downloads\sd-webui.zip                    ]        │             │
│  │ [Install from Local File]                              │             │
│  │                                                         │             │
│  │ ─────────────────────────────────────────────          │             │
│  │                                                         │             │
│  │ 🔧 Retry with Repair                                   │ ← Option 4  │
│  │ Clean up partial downloads, re-verify checksums,      │             │
│  │ and try downloading again.                             │             │
│  │                                                         │             │
│  │ [Retry with Repair]                                    │             │
│  └────────────────────────────────────────────────────────┘             │
│                                                                           │
│  Install path: C:\Users\...\Aura\Engines\stable-diffusion-webui         │
│                                                          [Close]         │
└──────────────────────────────────────────────────────────────────────────┘
```

## Error Code Reference (NEW!)

Each error code now has a clear explanation:

| Code | Meaning | What to do |
|------|---------|------------|
| **E-DL-404** | File not found on server | Use existing install, custom URL, or local file |
| **E-DL-CHECKSUM** | Downloaded file corrupted | Retry with repair to re-download |
| **E-HEALTH-TIMEOUT** | Engine won't respond | Check configuration, try manual start |
| **E-DL-NETWORK** | Cannot connect to server | Check internet/firewall, use local file |
| **E-DL-DISK-SPACE** | Not enough free space | Free up space or change install location |
| **E-DL-PERMISSION** | Cannot write to directory | Run as admin or choose different location |

## Key Improvements Summary

### Before This PR
```
User tries to install → Download fails → Wizard blocked
User doesn't know where files are
User doesn't know what to do about errors
```

### After This PR
```
User tries to install → Download fails → Self-healing options offered
                                        ↓
                        ┌───────────────┴───────────────┐
                        │                               │
                Use existing       OR      Custom URL   OR   Local file
                installation                                           
                        │                               │
                        └───────────────┬───────────────┘
                                        ↓
                              Installation succeeds
                                        ↓
              "Where are my files?" shows all locations
                    with Open Folder and Web UI buttons
```

## Documentation Integration

The flow is fully documented in:

1. **User-facing**: `docs/ENGINES.md` - "Where Are My Files?" section
2. **Developer**: `ONBOARDING_DIAGNOSTICS_IMPLEMENTATION.md` - Full technical details
3. **Visual**: This file - Flow diagrams and UI mockups

All three work together to provide a complete picture of the new functionality.
