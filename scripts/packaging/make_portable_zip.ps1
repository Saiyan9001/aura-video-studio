# Make Portable ZIP for Aura Video Studio
# Produces a single portable ZIP artifact with everything needed to run

param(
    [string]$Configuration = "Release",
    [string]$Platform = "x64"
)

$ErrorActionPreference = "Stop"

Write-Host ""
Write-Host "========================================" -ForegroundColor Cyan
Write-Host " Aura Video Studio - Portable ZIP" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""
Write-Host "Configuration: $Configuration" -ForegroundColor White
Write-Host "Platform:      $Platform" -ForegroundColor White
Write-Host ""

# Set paths
$scriptDir = $PSScriptRoot
$rootDir = Split-Path -Parent (Split-Path -Parent $scriptDir)
$artifactsDir = Join-Path $rootDir "artifacts"
$windowsDir = Join-Path $artifactsDir "windows"
$portableDir = Join-Path $windowsDir "portable"
$portableBuildDir = Join-Path $portableDir "AuraVideoStudio_Portable"

Write-Host "Root Directory:     $rootDir" -ForegroundColor Gray
Write-Host "Artifacts Directory: $portableDir" -ForegroundColor Gray
Write-Host ""

# Clean and create directories
Write-Host "[1/8] Creating build directories..." -ForegroundColor Yellow
if (Test-Path $portableBuildDir) {
    Remove-Item $portableBuildDir -Recurse -Force
}
New-Item -ItemType Directory -Force -Path $portableBuildDir | Out-Null
Write-Host "      ✓ Directories created" -ForegroundColor Green

# Build core projects
Write-Host "[2/8] Building .NET projects..." -ForegroundColor Yellow
dotnet build "$rootDir\Aura.Core\Aura.Core.csproj" -c $Configuration --nologo -v minimal
if ($LASTEXITCODE -ne 0) { throw "Failed to build Aura.Core" }
dotnet build "$rootDir\Aura.Providers\Aura.Providers.csproj" -c $Configuration --nologo -v minimal
if ($LASTEXITCODE -ne 0) { throw "Failed to build Aura.Providers" }
dotnet build "$rootDir\Aura.Api\Aura.Api.csproj" -c $Configuration --nologo -v minimal
if ($LASTEXITCODE -ne 0) { throw "Failed to build Aura.Api" }
Write-Host "      ✓ .NET projects built" -ForegroundColor Green

# Build Web UI
Write-Host "[3/8] Building web UI..." -ForegroundColor Yellow
Push-Location "$rootDir\Aura.Web"
if (-not (Test-Path "node_modules")) {
    Write-Host "      Installing npm dependencies..." -ForegroundColor Gray
    npm install --silent 2>&1 | Out-Null
}
npm run build --silent 2>&1 | Out-Null
if ($LASTEXITCODE -ne 0) { 
    Pop-Location
    throw "Failed to build web UI" 
}
Pop-Location
Write-Host "      ✓ Web UI built" -ForegroundColor Green

# Publish API as self-contained
Write-Host "[4/8] Publishing API (self-contained)..." -ForegroundColor Yellow
$apiDir = Join-Path $portableBuildDir "api"
dotnet publish "$rootDir\Aura.Api\Aura.Api.csproj" `
    -c $Configuration `
    -r win-$($Platform.ToLower()) `
    --self-contained `
    -o $apiDir `
    --nologo -v minimal
if ($LASTEXITCODE -ne 0) { throw "Failed to publish API" }
Write-Host "      ✓ API published" -ForegroundColor Green

# Copy Web UI to wwwroot folder inside the published API
Write-Host "[5/8] Copying web UI to wwwroot..." -ForegroundColor Yellow
$wwwrootDir = Join-Path $apiDir "wwwroot"
New-Item -ItemType Directory -Force -Path $wwwrootDir | Out-Null
Copy-Item "$rootDir\Aura.Web\dist\*" -Destination $wwwrootDir -Recurse -Force
Write-Host "      ✓ Web UI copied to wwwroot" -ForegroundColor Green

# Copy additional files
Write-Host "[6/8] Copying additional files..." -ForegroundColor Yellow

# Copy FFmpeg (if available)
$ffmpegDir = Join-Path $portableBuildDir "ffmpeg"
New-Item -ItemType Directory -Force -Path $ffmpegDir | Out-Null
if (Test-Path "$rootDir\scripts\ffmpeg\ffmpeg.exe") {
    Copy-Item "$rootDir\scripts\ffmpeg\ffmpeg.exe" -Destination $ffmpegDir -Force
    Write-Host "      ✓ ffmpeg.exe copied" -ForegroundColor Green
} else {
    Write-Host "      ⚠ ffmpeg.exe not found" -ForegroundColor Yellow
}
if (Test-Path "$rootDir\scripts\ffmpeg\ffprobe.exe") {
    Copy-Item "$rootDir\scripts\ffmpeg\ffprobe.exe" -Destination $ffmpegDir -Force
    Write-Host "      ✓ ffprobe.exe copied" -ForegroundColor Green
} else {
    Write-Host "      ⚠ ffprobe.exe not found" -ForegroundColor Yellow
}

# Copy assets (CC0 packs, LUTs, fonts) if they exist
$assetsDir = Join-Path $portableBuildDir "assets"
if (Test-Path "$rootDir\assets") {
    Copy-Item "$rootDir\assets" -Destination $assetsDir -Recurse -Force
    Write-Host "      ✓ Assets copied" -ForegroundColor Green
} else {
    New-Item -ItemType Directory -Force -Path $assetsDir | Out-Null
    Write-Host "      ⓘ No assets directory found" -ForegroundColor Gray
}

# Copy config with sane defaults
$configDir = Join-Path $portableBuildDir "config"
New-Item -ItemType Directory -Force -Path $configDir | Out-Null
if (Test-Path "$rootDir\appsettings.json") {
    Copy-Item "$rootDir\appsettings.json" -Destination $configDir -Force
    Write-Host "      ✓ appsettings.json copied" -ForegroundColor Green
}

# Copy documentation
if (Test-Path "$rootDir\PORTABLE.md") {
    Copy-Item "$rootDir\PORTABLE.md" -Destination "$portableBuildDir\README.md" -Force
    Write-Host "      ✓ README copied" -ForegroundColor Green
}
if (Test-Path "$rootDir\LICENSE") {
    Copy-Item "$rootDir\LICENSE" -Destination $portableBuildDir -Force
    Write-Host "      ✓ LICENSE copied" -ForegroundColor Green
}

# Create start_portable.cmd launcher script
$launcherScript = Get-Content "$scriptDir\templates\start_portable.cmd" -Raw
Set-Content -Path "$portableBuildDir\start_portable.cmd" -Value $launcherScript
Write-Host "      ✓ start_portable.cmd created" -ForegroundColor Green

# Create a simple WPF shell placeholder (AuraVideoStudio.exe)
# For now, we'll just create a batch file renamed as .exe stub
# In a full implementation, this would be a proper WPF application
Write-Host "      ⓘ WPF shell not implemented (use start_portable.cmd)" -ForegroundColor Gray

# Generate SBOM and attributions
Write-Host "[7/8] Generating SBOM and attributions..." -ForegroundColor Yellow
$sbomScript = Join-Path $scriptDir "generate-sbom.ps1"
if (Test-Path $sbomScript) {
    # Run SBOM generation to a temp location and copy files
    $tempSbomDir = Join-Path ([System.IO.Path]::GetTempPath()) "aura-sbom-$(Get-Random)"
    New-Item -ItemType Directory -Force -Path $tempSbomDir | Out-Null
    
    & $sbomScript -OutputDir $tempSbomDir
    
    # Copy generated files to portable build
    if (Test-Path "$tempSbomDir\sbom.json") {
        Copy-Item "$tempSbomDir\sbom.json" -Destination $portableBuildDir -Force
        Write-Host "      ✓ sbom.json generated" -ForegroundColor Green
    }
    if (Test-Path "$tempSbomDir\attributions.txt") {
        Copy-Item "$tempSbomDir\attributions.txt" -Destination $portableBuildDir -Force
        Write-Host "      ✓ attributions.txt generated" -ForegroundColor Green
    }
    
    # Clean up temp directory
    Remove-Item $tempSbomDir -Recurse -Force -ErrorAction SilentlyContinue
} else {
    Write-Host "      ⚠ generate-sbom.ps1 not found, skipping SBOM generation" -ForegroundColor Yellow
}

# Create ZIP
Write-Host "[8/8] Creating ZIP archive..." -ForegroundColor Yellow
$zipPath = Join-Path $portableDir "AuraVideoStudio_Portable_x64.zip"
if (Test-Path $zipPath) {
    Remove-Item $zipPath -Force
}
Compress-Archive -Path "$portableBuildDir\*" -DestinationPath $zipPath -Force

# Generate checksums.txt
$hash = Get-FileHash -Path $zipPath -Algorithm SHA256
$checksumFile = Join-Path $portableBuildDir "checksums.txt"
"$($hash.Hash)  $(Split-Path $zipPath -Leaf)" | Out-File $checksumFile -Encoding utf8

# Also save checksums.txt at the portable dir level
$checksumFileTop = Join-Path $portableDir "checksums.txt"
"$($hash.Hash)  $(Split-Path $zipPath -Leaf)" | Out-File $checksumFileTop -Encoding utf8

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
Write-Host "  /api/*                - Aura.Api binaries" -ForegroundColor White
Write-Host "  /api/wwwroot/*        - Aura.Web static build" -ForegroundColor White
Write-Host "  /ffmpeg/ffmpeg.exe    - FFmpeg binary" -ForegroundColor White
Write-Host "  /ffmpeg/ffprobe.exe   - FFprobe binary" -ForegroundColor White
Write-Host "  /assets/*             - Default packs, LUTs, fonts" -ForegroundColor White
Write-Host "  /config/appsettings.json - Configuration" -ForegroundColor White
Write-Host "  /start_portable.cmd   - Launcher script" -ForegroundColor White
Write-Host "  /checksums.txt        - SHA256 checksums" -ForegroundColor White
Write-Host "  /sbom.json            - Software Bill of Materials" -ForegroundColor White
Write-Host "  /attributions.txt     - License attributions" -ForegroundColor White
Write-Host ""
Write-Host "To test locally:" -ForegroundColor Cyan
Write-Host "  1. Extract the ZIP to a folder" -ForegroundColor White
Write-Host "  2. Run start_portable.cmd" -ForegroundColor White
Write-Host "  3. Application will open in your browser" -ForegroundColor White
Write-Host ""
