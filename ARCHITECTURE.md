# Aura Video Studio - Architecture Overview

## Introduction

Aura Video Studio is a Windows 11 desktop application for creating AI-powered videos. The application follows a **web-based UI architecture** hosted inside native Windows shells, allowing cross-platform development while delivering a native Windows experience.

## Architecture Components

### 1. Aura.Core (Business Logic)
**Technology**: .NET 8 Class Library  
**Purpose**: Platform-agnostic business logic, models, and orchestration

**Key Components**:
- `Models/` - Data models (Brief, PlanSpec, Scene, etc.)
- `Hardware/` - Hardware detection and capability tiering
- `Orchestrator/` - Video generation pipeline orchestration
- `Rendering/` - FFmpeg plan building
- `Providers/` - Provider interfaces (LLM, TTS, Image, Video)
- `Dependencies/` - Dependency manager with SHA-256 verification

**Platforms**: Linux (dev/CI), Windows (production)

### 2. Aura.Providers (Provider Implementations)
**Technology**: .NET 8 Class Library  
**Purpose**: Concrete implementations of provider interfaces

**Free Providers** (no API keys):
- `RuleBasedLlmProvider` - Template-based script generation
- `WindowsTtsProvider` - Windows SAPI text-to-speech
- `FfmpegVideoComposer` - Local FFmpeg rendering
- Stock providers (Pexels, Pixabay, Unsplash)

**Pro Providers** (require API keys):
- `OpenAiLlmProvider` - GPT-4/3.5 via OpenAI API
- ElevenLabs/PlayHT TTS (planned)
- Azure OpenAI, Gemini (planned)

**Platforms**: Linux (dev/CI with mocks), Windows (full functionality)

### 3. Aura.Api (Backend API)
**Technology**: ASP.NET Core 8 Minimal API  
**Purpose**: RESTful API backend for the web UI

**Endpoints**:
- `GET /healthz` - Health check
- `GET /capabilities` - Hardware detection results
- `POST /plan` - Create timeline plan
- `POST /script` - Generate script from brief
- `POST /tts` - Synthesize narration
- `GET /downloads/manifest` - Dependency manifest
- `POST /settings/save`, `GET /settings/load` - Settings persistence

**Additional Planned**:
- `/assets/search`, `/assets/generate` - Asset management
- `/compose`, `/render` - Video composition and rendering
- `/queue` - Render queue management
- `/logs/stream` - Live log streaming (SSE)

**Configuration**:
- Runs on `http://127.0.0.1:5005` by default
- Uses Serilog for structured logging
- Integrates with Aura.Core and Aura.Providers

**Platforms**: Linux (dev/CI), Windows (production)

### 4. Aura.Web (Frontend UI)
**Technology**: React 18 + Vite + TypeScript + Fluent UI React  
**Purpose**: Modern web-based user interface

**Key Features**:
- Fluent UI React components for Windows 11 look and feel
- TypeScript for type safety
- Vite for fast development and optimized builds
- Proxy configuration to forward API calls to Aura.Api

**Planned Views**:
- Create Wizard (6 steps)
- Storyboard & Timeline Editor
- Render Queue
- Publish/Upload
- Settings & Hardware Profile
- Download Center

**Development**:
- `npm run dev` - Development server on port 5173
- `npm run build` - Production build to `dist/`

**Platforms**: Linux (dev/CI), Windows (production)

### 5. Aura.Host.Win (Windows Shells) - **Planned**
**Technology**: WinUI 3 (packaged) + WPF (portable)  
**Purpose**: Native Windows shells that host the web UI via WebView2

**Two Variants**:

#### 5a. WinUI 3 Packaged Shell (MSIX)
- Windows App SDK
- Mica window chrome
- WebView2 for hosting Aura.Web
- Starts Aura.Api as child process
- Waits for `/healthz` then navigates to `http://127.0.0.1:5005`

#### 5b. WPF Portable Shell (EXE/ZIP)
- Classic WPF window
- WebView2 control
- Same API-hosting logic as WinUI 3
- No Windows App SDK dependency
- Self-contained deployment

**Platforms**: Windows only

### 6. Aura.App (Current WinUI 3 App)
**Technology**: WinUI 3 + XAML  
**Purpose**: Original standalone WinUI 3 application

**Status**: Functional with ViewModels and XAML views. Will coexist with new web-based architecture as an alternative UI option.

## Data Flow

```
User Interaction
    ↓
Aura.Web (React UI)
    ↓ HTTP
Aura.Api (ASP.NET Core)
    ↓ In-process calls
Aura.Core (Business Logic)
    ↓
Aura.Providers (LLM, TTS, Video, etc.)
    ↓
External Services / Local Tools
```

## Deployment Scenarios

### Development (Linux/Windows)
```
Developer runs:
1. dotnet run --project Aura.Api  (Terminal 1)
2. npm run dev  (Terminal 2, in Aura.Web/)
3. Opens browser to http://localhost:5173
```

### Production - Portable ZIP (Only Supported Distribution)
```
User extracts:
- AuraVideoStudio_Portable_x64.zip to any folder

User runs:
- Launch.bat (starts API and opens browser)

How it works:
1. Launch.bat starts Aura.Api.exe
2. API serves Web UI from wwwroot/
3. Browser automatically opens to http://127.0.0.1:5005
4. Self-contained, no installation needed, no registry changes

Build command:
- .\scripts\packaging\build-portable.ps1
```

### ~~Production - MSIX Package~~ (Removed)
**No longer supported.** This repository has adopted a portable-only distribution policy.

### ~~Production - Setup EXE~~ (Removed)
**No longer supported.** This repository has adopted a portable-only distribution policy.
```

## Platform Strategy

### Linux (Development & CI)
- **Purpose**: Cross-platform development, automated testing
- **What Works**: Aura.Core, Aura.Api, Aura.Web, unit tests
- **What Doesn't**: Windows-specific providers (Windows TTS, hardware detection details)
- **Strategy**: Use mocks and stubs for Windows-only features

### Windows (Production)
- **Purpose**: Final deployment target
- **What Works**: Everything, including Windows shells, MSIX packaging, code signing
- **Requirements**: Windows 11 x64, .NET 8 Runtime, WebView2 Evergreen

## Build & CI Strategy

### ci-linux.yml
- Runs on `ubuntu-latest`
- Builds Aura.Core, Aura.Providers, Aura.Api
- Builds Aura.Web (npm install + build)
- Runs unit tests
- Starts API and tests basic functionality
- Produces build artifacts

### ci.yml (Standard CI)
- **Portable-Only Policy Guard**: Checks for prohibited MSIX/EXE files
- Runs on `windows-latest`
- Builds all .NET projects (Aura.Core, Aura.Providers, Aura.Api)
- Runs unit tests (92 passing)
- Runs E2E integration tests
- Fails pipeline if MSIX/EXE packaging files are detected
- Uploads test results

## Security Considerations

### API Keys
- Stored in `%LOCALAPPDATA%\Aura\settings.json`
- **Windows**: Encrypted with DPAPI (planned)
- **Linux dev**: Plaintext in `~/.aura-dev/` with warnings

### Code Signing
- ~~MSIX and EXE signing~~ (removed - portable-only distribution)
- Portable ZIP uses SHA-256 checksums for integrity verification
- No code signing required for portable distribution

### WebView2
- Uses Evergreen runtime (auto-updates)
- Sandboxed JavaScript execution
- HTTPS-only for external resources (API is local HTTP)

## Future Enhancements

1. **Server-Sent Events (SSE)** for live log streaming and render progress
2. **SignalR Hub** for real-time collaboration features
3. **Electron-based Linux/macOS versions** (if demand exists)
4. ~~**Microsoft Store submission**~~ (removed - portable-only policy)
5. **Auto-update mechanism** for portable distributions
6. **Telemetry and crash reporting** (with user opt-out)

## Directory Structure

```
aura-video-studio/
├── Aura.Core/              # Business logic (.NET 8)
├── Aura.Providers/         # Provider implementations (.NET 8)
├── Aura.Api/               # Backend API (ASP.NET Core 8)
├── Aura.Web/               # Frontend UI (React + Vite)
├── Aura.App/               # Original WinUI 3 app (legacy)
├── Aura.Tests/             # Unit tests
├── Aura.E2E/               # Integration tests
├── scripts/
│   ├── ffmpeg/             # FFmpeg binaries
│   ├── packaging/          # Build scripts (portable ZIP only)
│   └── cleanup/            # Cleanup scripts for portable-only policy
├── .github/workflows/
│   ├── ci-linux.yml        # Linux CI
│   └── ci.yml              # Standard CI with portable-only guard
└── artifacts/              # Build outputs (created during build)
    └── portable/
        ├── AuraVideoStudio_Portable_x64.zip
        └── checksum.txt
```

## Technology Stack Summary

| Component | Technology | Purpose |
|-----------|-----------|---------|
| Aura.Core | .NET 8 | Business logic |
| Aura.Providers | .NET 8 | Provider implementations |
| Aura.Api | ASP.NET Core 8 | Backend API |
| Aura.Web | React 18 + TypeScript | Frontend UI |
| UI Framework | Fluent UI React | Windows 11 design system |
| Build Tool | Vite | Fast bundling |
| Distribution | Portable ZIP | Self-contained, no installation |
| CI Guard | Bash/PowerShell | Enforces portable-only policy |
| Video | FFmpeg | Rendering engine |
| Audio | NAudio | DSP and mixing |
| Logging | Serilog | Structured logging |
| Testing | xUnit | Unit tests |

## Getting Started

### Prerequisites
- .NET 8 SDK
- Node.js 20+
- (Windows only) Visual Studio 2022 or Build Tools
- (Windows only) Windows 11 SDK

### Development Workflow

```bash
# 1. Clone repository
git clone https://github.com/Coffee285/aura-video-studio.git
cd aura-video-studio

# 2. Restore .NET dependencies
dotnet restore

# 3. Install npm dependencies
cd Aura.Web
npm install
cd ..

# 4. Start API (Terminal 1)
dotnet run --project Aura.Api

# 5. Start Web UI (Terminal 2)
cd Aura.Web
npm run dev

# 6. Open browser to http://localhost:5173
```

### Building for Production (Windows)

```powershell
# Run the build script
.\scripts\packaging\build-all.ps1

# Generate SBOM and attributions
.\scripts\packaging\generate-sbom.ps1

# Output in artifacts/windows/
```

## Support & Contact

- **Issues**: https://github.com/Coffee285/aura-video-studio/issues
- **Documentation**: See README.md and individual project READMEs
- **License**: See LICENSE file
