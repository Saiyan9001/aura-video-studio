# Build Portable ZIP Distribution for Aura Video Studio
# Produces a single portable ZIP artifact with everything needed to run

param(
    [string]$Configuration = "Release",
    [string]$Platform = "x64"
)

$ErrorActionPreference = "Stop"

Write-Host ""
Write-Host "========================================" -ForegroundColor Cyan
Write-Host " Aura Video Studio - Portable ZIP Builder" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""
Write-Host "Configuration: $Configuration" -ForegroundColor White
Write-Host "Platform:      $Platform" -ForegroundColor White
Write-Host ""

# Set paths
$scriptDir = $PSScriptRoot
$rootDir = Split-Path -Parent (Split-Path -Parent $scriptDir)
$artifactsDir = Join-Path $rootDir "artifacts"
$portableDir = Join-Path $artifactsDir "windows\portable"
$portableBuildDir = Join-Path $portableDir "AuraVideoStudio_Portable_x64"

Write-Host "Root Directory:     $rootDir" -ForegroundColor Gray
Write-Host "Artifacts Directory: $artifactsDir" -ForegroundColor Gray
Write-Host "Build Directory:    $portableBuildDir" -ForegroundColor Gray
Write-Host ""

# Clean and create directories
Write-Host "[1/9] Creating build directories..." -ForegroundColor Yellow
if (Test-Path $portableBuildDir) {
    Remove-Item $portableBuildDir -Recurse -Force
}
New-Item -ItemType Directory -Force -Path $portableBuildDir | Out-Null
New-Item -ItemType Directory -Force -Path "$portableBuildDir\api" | Out-Null
New-Item -ItemType Directory -Force -Path "$portableBuildDir\web" | Out-Null
New-Item -ItemType Directory -Force -Path "$portableBuildDir\ffmpeg" | Out-Null
New-Item -ItemType Directory -Force -Path "$portableBuildDir\config" | Out-Null
New-Item -ItemType Directory -Force -Path "$portableBuildDir\assets" | Out-Null
Write-Host "      ✓ Directories created" -ForegroundColor Green

# Build core projects
Write-Host "[2/9] Building .NET projects..." -ForegroundColor Yellow
dotnet build "$rootDir\Aura.Core\Aura.Core.csproj" -c $Configuration --nologo -v minimal
if ($LASTEXITCODE -ne 0) { throw "Build failed for Aura.Core" }
dotnet build "$rootDir\Aura.Providers\Aura.Providers.csproj" -c $Configuration --nologo -v minimal
if ($LASTEXITCODE -ne 0) { throw "Build failed for Aura.Providers" }
dotnet build "$rootDir\Aura.Api\Aura.Api.csproj" -c $Configuration --nologo -v minimal
if ($LASTEXITCODE -ne 0) { throw "Build failed for Aura.Api" }
Write-Host "      ✓ .NET projects built" -ForegroundColor Green

# Build Web UI
Write-Host "[3/9] Building web UI..." -ForegroundColor Yellow
Push-Location "$rootDir\Aura.Web"
if (-not (Test-Path "node_modules")) {
    Write-Host "      Installing npm dependencies..." -ForegroundColor Gray
    npm install --silent 2>&1 | Out-Null
}
npm run build --silent 2>&1 | Out-Null
if ($LASTEXITCODE -ne 0) { 
    Pop-Location
    throw "Web UI build failed" 
}
Pop-Location
Write-Host "      ✓ Web UI built" -ForegroundColor Green

# Publish API as self-contained
Write-Host "[4/9] Publishing API (self-contained)..." -ForegroundColor Yellow
dotnet publish "$rootDir\Aura.Api\Aura.Api.csproj" `
    -c $Configuration `
    -r win-$($Platform.ToLower()) `
    --self-contained `
    -o "$portableBuildDir\api" `
    --nologo -v minimal
if ($LASTEXITCODE -ne 0) { throw "API publish failed" }
Write-Host "      ✓ API published to /api" -ForegroundColor Green

# Copy Web UI to /web
Write-Host "[5/9] Copying web UI to /web..." -ForegroundColor Yellow
Copy-Item "$rootDir\Aura.Web\dist\*" -Destination "$portableBuildDir\web" -Recurse -Force
Write-Host "      ✓ Web UI copied to /web" -ForegroundColor Green

# Copy FFmpeg binaries
Write-Host "[6/9] Copying FFmpeg binaries..." -ForegroundColor Yellow
if (Test-Path "$rootDir\scripts\ffmpeg\ffmpeg.exe") {
    Copy-Item "$rootDir\scripts\ffmpeg\ffmpeg.exe" -Destination "$portableBuildDir\ffmpeg" -Force
    Write-Host "      ✓ ffmpeg.exe copied" -ForegroundColor Green
} else {
    Write-Host "      ⚠ ffmpeg.exe not found" -ForegroundColor Yellow
}
if (Test-Path "$rootDir\scripts\ffmpeg\ffprobe.exe") {
    Copy-Item "$rootDir\scripts\ffmpeg\ffprobe.exe" -Destination "$portableBuildDir\ffmpeg" -Force
    Write-Host "      ✓ ffprobe.exe copied" -ForegroundColor Green
} else {
    Write-Host "      ⚠ ffprobe.exe not found" -ForegroundColor Yellow
}

# Copy assets (if any exist)
Write-Host "[7/9] Copying assets..." -ForegroundColor Yellow
if (Test-Path "$rootDir\assets") {
    Copy-Item "$rootDir\assets\*" -Destination "$portableBuildDir\assets" -Recurse -Force -ErrorAction SilentlyContinue
    Write-Host "      ✓ Assets copied" -ForegroundColor Green
} else {
    Write-Host "      ℹ No assets directory found (optional)" -ForegroundColor Gray
}

# Copy config
Write-Host "[8/9] Copying configuration..." -ForegroundColor Yellow
if (Test-Path "$rootDir\appsettings.json") {
    # Update appsettings.json for portable deployment
    $config = Get-Content "$rootDir\appsettings.json" -Raw | ConvertFrom-Json
    $config.Providers.Video.FfmpegPath = "ffmpeg/ffmpeg.exe"
    $config | ConvertTo-Json -Depth 10 | Set-Content "$portableBuildDir\config\appsettings.json"
    Write-Host "      ✓ Configuration copied to /config" -ForegroundColor Green
} else {
    Write-Host "      ⚠ appsettings.json not found" -ForegroundColor Yellow
}

# Copy documentation
if (Test-Path "$rootDir\PORTABLE.md") {
    Copy-Item "$rootDir\PORTABLE.md" -Destination "$portableBuildDir\README.md" -Force
}
if (Test-Path "$rootDir\LICENSE") {
    Copy-Item "$rootDir\LICENSE" -Destination "$portableBuildDir\LICENSE" -Force
}

# Create start_portable.cmd launcher script
Write-Host "[9/9] Creating launcher and metadata files..." -ForegroundColor Yellow
$launcherScript = @"
@echo off
REM Aura Video Studio - Portable Edition Launcher
REM This script starts the API server and waits for it to be ready before opening the browser

echo ========================================
echo  Aura Video Studio - Portable Edition
echo ========================================
echo.

REM Change to the directory where this script is located
cd /d "%~dp0"

REM Start the API server in a new window
echo [1/3] Starting API server...
start "Aura Video Studio API" /D "%~dp0api" "%~dp0api\Aura.Api.exe" --urls "http://127.0.0.1:5005"

REM Wait for the API to be ready by checking the health endpoint
echo [2/3] Waiting for API to be ready...
set /a attempts=0
set /a maxAttempts=30

:checkHealth
timeout /t 1 /nobreak >nul
set /a attempts+=1

REM Use PowerShell to check the health endpoint
powershell -NoProfile -Command "try { $response = Invoke-WebRequest -Uri 'http://127.0.0.1:5005/healthz' -UseBasicParsing -TimeoutSec 1; exit 0 } catch { exit 1 }" >nul 2>&1
if %errorlevel% equ 0 (
    echo [3/3] API is ready!
    goto openBrowser
)

if %attempts% geq %maxAttempts% (
    echo.
    echo ERROR: API did not start within 30 seconds
    echo Please check the API server window for error messages.
    echo.
    pause
    exit /b 1
)

goto checkHealth

:openBrowser
echo.
echo Opening web browser...
start "" "http://127.0.0.1:5005"
echo.
echo ========================================
echo  Aura Video Studio is now running!
echo ========================================
echo.
echo Web UI: http://127.0.0.1:5005
echo.
echo To stop the application:
echo   - Close the "Aura Video Studio API" window
echo   - Or press Ctrl+C in that window
echo.
"@
Set-Content -Path "$portableBuildDir\start_portable.cmd" -Value $launcherScript -Encoding ASCII

# Generate checksums.txt
$checksumLines = @()
Get-ChildItem $portableBuildDir -Recurse -File | ForEach-Object {
    $relativePath = $_.FullName.Substring($portableBuildDir.Length + 1)
    $hash = Get-FileHash -Path $_.FullName -Algorithm SHA256
    $checksumLines += "$($hash.Hash)  $relativePath"
}
$checksumLines | Sort-Object | Out-File "$portableBuildDir\checksums.txt" -Encoding utf8

# Generate SBOM
$sbom = @{
    bomFormat = "CycloneDX"
    specVersion = "1.4"
    version = 1
    metadata = @{
        timestamp = (Get-Date -Format "o")
        component = @{
            type = "application"
            name = "Aura Video Studio"
            version = "1.0.0"
            description = "AI-powered video creation tool - Portable Edition"
        }
    }
    components = @(
        @{
            type = "library"
            name = ".NET Runtime"
            version = "8.0"
            licenses = @(@{ license = @{ id = "MIT" } })
        },
        @{
            type = "library"
            name = "ASP.NET Core"
            version = "8.0"
            licenses = @(@{ license = @{ id = "MIT" } })
        },
        @{
            type = "library"
            name = "React"
            version = "18.2"
            licenses = @(@{ license = @{ id = "MIT" } })
        },
        @{
            type = "library"
            name = "Fluent UI React"
            version = "9.47"
            licenses = @(@{ license = @{ id = "MIT" } })
        }
    )
}
$sbom | ConvertTo-Json -Depth 10 | Out-File "$portableBuildDir\sbom.json" -Encoding utf8

# Generate attributions.txt
$attributions = @"
AURA VIDEO STUDIO - Third-Party Software Attributions
======================================================

This software includes or depends on the following third-party components:

1. .NET Runtime
   Version: 8.0
   License: MIT License
   Copyright (c) .NET Foundation and Contributors
   https://github.com/dotnet/runtime

2. FFmpeg (if included)
   Version: 6.0+
   License: LGPL 2.1 or later
   Copyright (c) FFmpeg team
   https://ffmpeg.org/
   Note: FFmpeg is licensed under the LGPL 2.1 and its source code is available at https://ffmpeg.org/

3. ASP.NET Core
   Version: 8.0
   License: MIT License
   Copyright (c) .NET Foundation and Contributors
   https://github.com/dotnet/aspnetcore

4. React
   Version: 18.2
   License: MIT License
   Copyright (c) Facebook, Inc. and its affiliates
   https://reactjs.org/

5. Fluent UI React
   Version: 9.47
   License: MIT License
   Copyright (c) Microsoft Corporation
   https://github.com/microsoft/fluentui

For complete license texts, see the respective project repositories.

Additional licenses for bundled assets:
- Default assets: CC0 1.0 Universal (Public Domain) where applicable

Contact: https://github.com/Coffee285/aura-video-studio/issues
"@
Set-Content -Path "$portableBuildDir\attributions.txt" -Value $attributions -Encoding utf8

Write-Host "      ✓ Launcher and metadata files created" -ForegroundColor Green

# Create ZIP
Write-Host ""
Write-Host "Creating portable ZIP..." -ForegroundColor Yellow
$zipPath = Join-Path $portableDir "AuraVideoStudio_Portable_x64.zip"
if (Test-Path $zipPath) {
    Remove-Item $zipPath -Force
}

# Ensure parent directory exists
New-Item -ItemType Directory -Force -Path $portableDir | Out-Null

# Create the ZIP from the build directory contents
Compress-Archive -Path "$portableBuildDir\*" -DestinationPath $zipPath -Force

# Generate checksum for the ZIP file
$zipHash = Get-FileHash -Path $zipPath -Algorithm SHA256
$zipChecksumFile = Join-Path $portableDir "AuraVideoStudio_Portable_x64.zip.sha256"
"$($zipHash.Hash)  AuraVideoStudio_Portable_x64.zip" | Out-File $zipChecksumFile -Encoding utf8

Write-Host ""
Write-Host "========================================" -ForegroundColor Green
Write-Host " Build Complete!" -ForegroundColor Green
Write-Host "========================================" -ForegroundColor Green
Write-Host ""
Write-Host "Portable ZIP:  $zipPath" -ForegroundColor White
Write-Host "Size:          $([math]::Round((Get-Item $zipPath).Length / 1MB, 2)) MB" -ForegroundColor White
Write-Host "SHA-256:       $($zipHash.Hash)" -ForegroundColor White
Write-Host ""
Write-Host "Contents:" -ForegroundColor Cyan
Write-Host "  /api/*                  - Backend API binaries" -ForegroundColor White
Write-Host "  /web/*                  - Frontend UI files" -ForegroundColor White
Write-Host "  /ffmpeg/*               - FFmpeg binaries" -ForegroundColor White
Write-Host "  /assets/*               - Default assets" -ForegroundColor White
Write-Host "  /config/appsettings.json - Configuration" -ForegroundColor White
Write-Host "  /start_portable.cmd     - Launcher script" -ForegroundColor White
Write-Host "  /checksums.txt          - File checksums" -ForegroundColor White
Write-Host "  /sbom.json              - Software Bill of Materials" -ForegroundColor White
Write-Host "  /attributions.txt       - License attributions" -ForegroundColor White
Write-Host ""
Write-Host "To test locally:" -ForegroundColor Cyan
Write-Host "  1. Extract $zipPath" -ForegroundColor White
Write-Host "  2. Run start_portable.cmd" -ForegroundColor White
Write-Host "  3. Browser will open to http://127.0.0.1:5005" -ForegroundColor White
Write-Host ""
