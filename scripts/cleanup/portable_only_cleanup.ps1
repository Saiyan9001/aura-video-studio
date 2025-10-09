# Portable-Only Cleanup Script
# Removes all MSIX/EXE packaging files and artifacts from the repository

param(
    [switch]$DryRun,
    [switch]$Verbose
)

$ErrorActionPreference = "Stop"

Write-Host ""
Write-Host "========================================" -ForegroundColor Cyan
Write-Host " Aura Video Studio - Portable Only Cleanup" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

if ($DryRun) {
    Write-Host "DRY RUN MODE - No files will be deleted" -ForegroundColor Yellow
    Write-Host ""
}

$rootDir = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
$deletedCount = 0
$errorCount = 0

function Remove-ItemSafely {
    param(
        [string]$Path,
        [string]$Description
    )
    
    if (Test-Path $Path) {
        if ($DryRun) {
            Write-Host "  [DRY RUN] Would delete: $Description" -ForegroundColor Yellow
            if ($Verbose) {
                Write-Host "            Path: $Path" -ForegroundColor Gray
            }
        } else {
            try {
                Remove-Item -Path $Path -Recurse -Force
                Write-Host "  ✓ Deleted: $Description" -ForegroundColor Green
                if ($Verbose) {
                    Write-Host "            Path: $Path" -ForegroundColor Gray
                }
                $script:deletedCount++
            } catch {
                Write-Host "  ✗ Failed to delete: $Description" -ForegroundColor Red
                Write-Host "    Error: $($_.Exception.Message)" -ForegroundColor Red
                $script:errorCount++
            }
        }
    } else {
        if ($Verbose) {
            Write-Host "  ⊘ Not found: $Description" -ForegroundColor Gray
            Write-Host "            Path: $Path" -ForegroundColor Gray
        }
    }
}

Write-Host "[1/4] Cleaning up packaging scripts..." -ForegroundColor Yellow

# Remove Inno Setup installer script
Remove-ItemSafely -Path (Join-Path $rootDir "scripts\packaging\setup.iss") `
    -Description "Inno Setup installer script (setup.iss)"

# Remove SBOM generation script
Remove-ItemSafely -Path (Join-Path $rootDir "scripts\packaging\generate-sbom.ps1") `
    -Description "SBOM generation script (generate-sbom.ps1)"

# Check for any remaining MSIX/installer related files in scripts/packaging
$packagingDir = Join-Path $rootDir "scripts\packaging"
if (Test-Path $packagingDir) {
    Get-ChildItem -Path $packagingDir -Filter "*msix*" -File -ErrorAction SilentlyContinue | ForEach-Object {
        Remove-ItemSafely -Path $_.FullName -Description "MSIX-related file: $($_.Name)"
    }
    Get-ChildItem -Path $packagingDir -Filter "*inno*" -File -ErrorAction SilentlyContinue | ForEach-Object {
        Remove-ItemSafely -Path $_.FullName -Description "Inno Setup file: $($_.Name)"
    }
    Get-ChildItem -Path $packagingDir -Filter "*setup*" -File -ErrorAction SilentlyContinue | ForEach-Object {
        if ($_.Name -ne "setup.iss") {  # Already handled above
            Remove-ItemSafely -Path $_.FullName -Description "Setup-related file: $($_.Name)"
        }
    }
    Get-ChildItem -Path $packagingDir -Filter "*installer*" -File -ErrorAction SilentlyContinue | ForEach-Object {
        Remove-ItemSafely -Path $_.FullName -Description "Installer-related file: $($_.Name)"
    }
}

Write-Host ""
Write-Host "[2/4] Cleaning up MSIX artifacts..." -ForegroundColor Yellow

# Remove Package.appxmanifest from Aura.App
Remove-ItemSafely -Path (Join-Path $rootDir "Aura.App\Package.appxmanifest") `
    -Description "WinUI 3 package manifest (Package.appxmanifest)"

# Remove any .cer files
Get-ChildItem -Path $rootDir -Filter "*.cer" -File -ErrorAction SilentlyContinue | ForEach-Object {
    Remove-ItemSafely -Path $_.FullName -Description "Certificate file: $($_.Name)"
}

# Remove any .appx* files
Get-ChildItem -Path $rootDir -Filter "*.appx*" -File -ErrorAction SilentlyContinue | ForEach-Object {
    Remove-ItemSafely -Path $_.FullName -Description "APPX file: $($_.Name)"
}

# Remove any .msix* files
Get-ChildItem -Path $rootDir -Filter "*.msix*" -File -ErrorAction SilentlyContinue | ForEach-Object {
    Remove-ItemSafely -Path $_.FullName -Description "MSIX file: $($_.Name)"
}

# Remove any .msixbundle files
Get-ChildItem -Path $rootDir -Filter "*.msixbundle" -File -ErrorAction SilentlyContinue | ForEach-Object {
    Remove-ItemSafely -Path $_.FullName -Description "MSIX bundle file: $($_.Name)"
}

Write-Host ""
Write-Host "[3/4] Cleaning up GitHub workflows..." -ForegroundColor Yellow

# Remove ci-windows.yml (builds MSIX)
Remove-ItemSafely -Path (Join-Path $rootDir ".github\workflows\ci-windows.yml") `
    -Description "Windows CI workflow (ci-windows.yml)"

Write-Host ""
Write-Host "[4/4] Cleaning up artifacts directories..." -ForegroundColor Yellow

# Remove artifacts directories
$artifactsDir = Join-Path $rootDir "artifacts"
if (Test-Path $artifactsDir) {
    Remove-ItemSafely -Path (Join-Path $artifactsDir "windows\msix") `
        -Description "MSIX artifacts directory"
    Remove-ItemSafely -Path (Join-Path $artifactsDir "windows\exe") `
        -Description "EXE artifacts directory"
}

Write-Host ""
Write-Host "========================================" -ForegroundColor Cyan
Write-Host " Cleanup Complete" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

if ($DryRun) {
    Write-Host "DRY RUN SUMMARY:" -ForegroundColor Yellow
    Write-Host "  No files were actually deleted" -ForegroundColor Yellow
} else {
    Write-Host "SUMMARY:" -ForegroundColor Green
    Write-Host "  Files deleted: $deletedCount" -ForegroundColor White
    if ($errorCount -gt 0) {
        Write-Host "  Errors encountered: $errorCount" -ForegroundColor Red
    }
}
Write-Host ""
