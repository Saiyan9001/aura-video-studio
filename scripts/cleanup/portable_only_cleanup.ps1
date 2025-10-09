# Portable-Only Cleanup Script
# Removes all MSIX/EXE packaging artifacts and related files
# Run this script to enforce portable-only distribution policy

param(
    [switch]$WhatIf = $false
)

$ErrorActionPreference = "Stop"

Write-Host "=== Aura Video Studio - Portable-Only Cleanup ===" -ForegroundColor Cyan
Write-Host ""

if ($WhatIf) {
    Write-Host "Running in WhatIf mode - no files will be deleted" -ForegroundColor Yellow
    Write-Host ""
}

$rootDir = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
$deletedCount = 0
$patterns = @()

# Define patterns to search and delete
$searchPatterns = @(
    "*msix*",
    "*inno*",
    "*setup.iss",
    "*installer*",
    "*.appx*",
    "*.msixbundle*",
    "*.cer"
)

# Search in scripts/packaging/
Write-Host "Searching in scripts/packaging/..." -ForegroundColor Yellow
$packagingDir = Join-Path $rootDir "scripts\packaging"
if (Test-Path $packagingDir) {
    foreach ($pattern in $searchPatterns) {
        $files = Get-ChildItem -Path $packagingDir -Filter $pattern -Recurse -ErrorAction SilentlyContinue
        foreach ($file in $files) {
            $relativePath = $file.FullName.Substring($rootDir.Length + 1)
            Write-Host "  Found: $relativePath" -ForegroundColor Red
            $patterns += $relativePath
            
            if (-not $WhatIf) {
                Remove-Item -Path $file.FullName -Force
                $deletedCount++
                Write-Host "  Deleted: $relativePath" -ForegroundColor Green
            }
        }
    }
}

# Search in root directory for artifacts
Write-Host ""
Write-Host "Searching in root directory..." -ForegroundColor Yellow
foreach ($pattern in $searchPatterns) {
    $files = Get-ChildItem -Path $rootDir -Filter $pattern -ErrorAction SilentlyContinue
    foreach ($file in $files) {
        $relativePath = $file.FullName.Substring($rootDir.Length + 1)
        Write-Host "  Found: $relativePath" -ForegroundColor Red
        $patterns += $relativePath
        
        if (-not $WhatIf) {
            Remove-Item -Path $file.FullName -Force
            $deletedCount++
            Write-Host "  Deleted: $relativePath" -ForegroundColor Green
        }
    }
}

# Remove workflows that reference MSIX/EXE
Write-Host ""
Write-Host "Checking GitHub workflows..." -ForegroundColor Yellow
$workflowDir = Join-Path $rootDir ".github\workflows"
if (Test-Path $workflowDir) {
    $workflowsToCheck = @("ci.yml", "ci-windows.yml")
    
    foreach ($workflow in $workflowsToCheck) {
        $workflowPath = Join-Path $workflowDir $workflow
        if (Test-Path $workflowPath) {
            $content = Get-Content -Path $workflowPath -Raw
            $hasMsixRef = $content -match "msix|MSIX|AppxBundle|UapAppx"
            
            if ($hasMsixRef) {
                $relativePath = $workflowPath.Substring($rootDir.Length + 1)
                Write-Host "  Found MSIX/EXE references in: $relativePath" -ForegroundColor Red
                $patterns += $relativePath
                
                if (-not $WhatIf) {
                    Remove-Item -Path $workflowPath -Force
                    $deletedCount++
                    Write-Host "  Deleted: $relativePath" -ForegroundColor Green
                }
            }
        }
    }
}

Write-Host ""
if ($WhatIf) {
    Write-Host "=== WhatIf Mode: No files were deleted ===" -ForegroundColor Yellow
    Write-Host "Found $($patterns.Count) files/patterns that would be deleted" -ForegroundColor White
} else {
    Write-Host "=== Cleanup Complete ===" -ForegroundColor Cyan
    Write-Host "Deleted $deletedCount files" -ForegroundColor Green
}

Write-Host ""
Write-Host "Files matching MSIX/EXE patterns:" -ForegroundColor Cyan
if ($patterns.Count -eq 0) {
    Write-Host "  None found - repository is clean!" -ForegroundColor Green
} else {
    $patterns | ForEach-Object { Write-Host "  - $_" -ForegroundColor White }
}

Write-Host ""
Write-Host "Distribution Policy: Portable-only ZIP distribution" -ForegroundColor Cyan
Write-Host "See scripts/packaging/build-portable.ps1 for building portable distribution" -ForegroundColor White
Write-Host ""
