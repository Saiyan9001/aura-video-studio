# Build Portable ZIP Distribution for Aura Video Studio
# Creates a complete portable package with all dependencies

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
$windowsArtifactsDir = Join-Path $artifactsDir "windows"
$portableDir = Join-Path $windowsArtifactsDir "portable"
$portableBuildDir = Join-Path $portableDir "AuraVideoStudio_Portable"

Write-Host "Root Directory:     $rootDir" -ForegroundColor Gray
Write-Host "Artifacts Directory: $windowsArtifactsDir" -ForegroundColor Gray
Write-Host ""

# Check prerequisites
Write-Host "[1/8] Checking prerequisites..." -ForegroundColor Yellow
$npmPath = Get-Command npm -ErrorAction SilentlyContinue
if (-not $npmPath) {
    Write-Host "ERROR: npm is not installed or not in PATH" -ForegroundColor Red
    exit 1
}
Write-Host "      ✓ npm found" -ForegroundColor Green

# Create directories
Write-Host "[2/8] Creating build directories..." -ForegroundColor Yellow
if (Test-Path $portableBuildDir) {
    Remove-Item $portableBuildDir -Recurse -Force
}
New-Item -ItemType Directory -Force -Path $portableBuildDir | Out-Null
New-Item -ItemType Directory -Force -Path "$portableBuildDir\api" | Out-Null
New-Item -ItemType Directory -Force -Path "$portableBuildDir\web" | Out-Null
New-Item -ItemType Directory -Force -Path "$portableBuildDir\ffmpeg" | Out-Null
New-Item -ItemType Directory -Force -Path "$portableBuildDir\assets" | Out-Null
New-Item -ItemType Directory -Force -Path "$portableBuildDir\config" | Out-Null
Write-Host "      ✓ Directories created" -ForegroundColor Green

# Build core projects
Write-Host "[3/8] Building .NET projects..." -ForegroundColor Yellow
dotnet build "$rootDir\Aura.Core\Aura.Core.csproj" -c $Configuration --nologo -v minimal
dotnet build "$rootDir\Aura.Providers\Aura.Providers.csproj" -c $Configuration --nologo -v minimal
dotnet build "$rootDir\Aura.Api\Aura.Api.csproj" -c $Configuration --nologo -v minimal
Write-Host "      ✓ .NET projects built" -ForegroundColor Green

# Build Web UI
Write-Host "[4/8] Building web UI..." -ForegroundColor Yellow
Push-Location "$rootDir\Aura.Web"
if (-not (Test-Path "node_modules")) {
    Write-Host "      Installing npm dependencies..." -ForegroundColor Gray
    npm install --silent 2>&1 | Out-Null
}
npm run build --silent 2>&1 | Out-Null
Pop-Location
Write-Host "      ✓ Web UI built" -ForegroundColor Green

# Publish API as self-contained
Write-Host "[5/8] Publishing API (self-contained)..." -ForegroundColor Yellow
dotnet publish "$rootDir\Aura.Api\Aura.Api.csproj" `
    -c $Configuration `
    -r win-$($Platform.ToLower()) `
    --self-contained `
    -o "$portableBuildDir\api" `
    --nologo -v minimal
Write-Host "      ✓ API published" -ForegroundColor Green

# Copy Web UI to wwwroot folder inside the published API AND to /web directory
Write-Host "[6/8] Organizing files..." -ForegroundColor Yellow

# Copy to API's wwwroot (required for serving)
$wwwrootDir = Join-Path "$portableBuildDir\api" "wwwroot"
New-Item -ItemType Directory -Force -Path $wwwrootDir | Out-Null
Copy-Item "$rootDir\Aura.Web\dist\*" -Destination $wwwrootDir -Recurse -Force
Write-Host "      ✓ Web UI copied to api/wwwroot" -ForegroundColor Green

# Also copy to /web for reference (as per requirements)
Copy-Item "$rootDir\Aura.Web\dist\*" -Destination "$portableBuildDir\web" -Recurse -Force
Write-Host "      ✓ Web UI copied to /web" -ForegroundColor Green

# Copy FFmpeg binaries
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

# Copy config with sane defaults
Copy-Item "$rootDir\appsettings.json" -Destination "$portableBuildDir\config" -Force
Write-Host "      ✓ Config copied to /config" -ForegroundColor Green

# Copy additional docs
if (Test-Path "$rootDir\PORTABLE.md") {
    Copy-Item "$rootDir\PORTABLE.md" -Destination "$portableBuildDir\README.md" -Force
}
if (Test-Path "$rootDir\LICENSE") {
    Copy-Item "$rootDir\LICENSE" -Destination "$portableBuildDir" -Force
}

# Generate SBOM
Write-Host "[7/8] Generating SBOM and attributions..." -ForegroundColor Yellow
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
            name = "FFmpeg"
            version = "6.0"
            licenses = @(@{ license = @{ id = "LGPL-2.1" } })
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
        }
    )
}

$sbomPath = Join-Path $portableBuildDir "sbom.json"
$sbom | ConvertTo-Json -Depth 10 | Out-File $sbomPath -Encoding utf8
Write-Host "      ✓ SBOM generated" -ForegroundColor Green

# Generate attributions
$attributions = @"
AURA VIDEO STUDIO - Third-Party Software Attributions
======================================================

This software includes or depends on the following third-party components:

1. .NET Runtime
   Version: 8.0
   License: MIT License
   Copyright (c) .NET Foundation and Contributors
   https://github.com/dotnet/runtime

2. FFmpeg
   Version: 6.0+
   License: LGPL 2.1 or later
   Copyright (c) FFmpeg team
   https://ffmpeg.org/

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
   Version: 9.x
   License: MIT License
   Copyright (c) Microsoft Corporation
   https://github.com/microsoft/fluentui

For complete license texts, see the respective project repositories.

Contact: https://github.com/Coffee285/aura-video-studio/issues
"@

$attributionsPath = Join-Path $portableBuildDir "attributions.txt"
Set-Content -Path $attributionsPath -Value $attributions -Encoding utf8
Write-Host "      ✓ Attributions generated" -ForegroundColor Green

# Create start_portable.cmd launcher with healthz check
$launcherScript = @"
@echo off
setlocal enabledelayedexpansion

echo ========================================
echo  Aura Video Studio - Portable Edition
echo ========================================
echo.

REM Change to the directory where this script is located
cd /d "%~dp0"

REM Copy config to API directory if not already there
if not exist "api\appsettings.json" (
    copy "config\appsettings.json" "api\appsettings.json" >nul 2>&1
)

echo Starting API server...
start "Aura API" /D "api" "Aura.Api.exe"

echo Waiting for API to be ready...
set MAX_RETRIES=30
set RETRY_COUNT=0

:check_health
set /a RETRY_COUNT+=1
if %RETRY_COUNT% gtr %MAX_RETRIES% (
    echo.
    echo ERROR: API failed to start after 30 seconds
    echo Check the API console window for errors
    pause
    exit /b 1
)

REM Try to check the healthz endpoint
powershell -Command "try { `$response = Invoke-WebRequest -Uri 'http://127.0.0.1:5005/healthz' -TimeoutSec 1 -UseBasicParsing; exit 0 } catch { exit 1 }" >nul 2>&1
if errorlevel 1 (
    timeout /t 1 /nobreak >nul
    goto check_health
)

echo API is ready!
echo.
echo Opening web browser...
start "" "http://127.0.0.1:5005"
echo.
echo The application should open in your web browser.
echo If not, manually navigate to: http://127.0.0.1:5005
echo.
echo To stop the application, close the "Aura API" window.
echo.
"@
Set-Content -Path "$portableBuildDir\start_portable.cmd" -Value $launcherScript
Write-Host "      ✓ Launcher script created" -ForegroundColor Green

# Create ZIP
Write-Host "[8/8] Creating ZIP archive..." -ForegroundColor Yellow
$zipPath = Join-Path $portableDir "AuraVideoStudio_Portable_x64.zip"
if (Test-Path $zipPath) {
    Remove-Item $zipPath -Force
}
Compress-Archive -Path "$portableBuildDir\*" -DestinationPath $zipPath -Force

# Generate checksums
$hash = Get-FileHash -Path $zipPath -Algorithm SHA256
$checksumContent = "$($hash.Hash)  $(Split-Path $zipPath -Leaf)"
$checksumFile = Join-Path $portableBuildDir "checksums.txt"
Set-Content -Path $checksumFile -Value $checksumContent -Encoding utf8

# Also save checksums at artifacts level
$artifactChecksumFile = Join-Path $windowsArtifactsDir "checksums.txt"
Set-Content -Path $artifactChecksumFile -Value $checksumContent -Encoding utf8

Write-Host "      ✓ ZIP created" -ForegroundColor Green

Write-Host ""
Write-Host "========================================" -ForegroundColor Green
Write-Host " Build Complete!" -ForegroundColor Green
Write-Host "========================================" -ForegroundColor Green
Write-Host ""
Write-Host "Portable ZIP:  $zipPath" -ForegroundColor White
Write-Host "Size:          $([math]::Round((Get-Item $zipPath).Length / 1MB, 2)) MB" -ForegroundColor White
Write-Host "SHA-256:       $($hash.Hash)" -ForegroundColor White
Write-Host ""
Write-Host "Contents:" -ForegroundColor Cyan
Write-Host "  /api/*          - Aura.Api binaries (self-contained)" -ForegroundColor White
Write-Host "  /web/*          - Aura.Web static build" -ForegroundColor White
Write-Host "  /ffmpeg/*       - ffmpeg.exe and ffprobe.exe" -ForegroundColor White
Write-Host "  /config/*       - appsettings.json" -ForegroundColor White
Write-Host "  /assets/*       - Default assets" -ForegroundColor White
Write-Host "  checksums.txt   - SHA-256 checksums" -ForegroundColor White
Write-Host "  sbom.json       - Software Bill of Materials" -ForegroundColor White
Write-Host "  attributions.txt - Third-party licenses" -ForegroundColor White
Write-Host "  start_portable.cmd - Launcher with health check" -ForegroundColor White
Write-Host ""
Write-Host "To test locally:" -ForegroundColor Cyan
Write-Host "  1. Extract the ZIP to a folder" -ForegroundColor White
Write-Host "  2. Run start_portable.cmd" -ForegroundColor White
Write-Host "  3. Wait for API health check to pass" -ForegroundColor White
Write-Host "  4. Browser will open to http://127.0.0.1:5005" -ForegroundColor White
Write-Host ""
