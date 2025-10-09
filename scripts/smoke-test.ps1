# Smoke Test for Aura Video Studio
# Quick test to verify basic functionality (10-15 seconds)

param(
    [string]$ApiPath = "Aura.Api\bin\Release\net8.0\Aura.Api.exe",
    [int]$TimeoutSeconds = 30
)

$ErrorActionPreference = "Stop"

Write-Host ""
Write-Host "========================================" -ForegroundColor Cyan
Write-Host " Aura Video Studio - Smoke Test" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

$rootDir = Split-Path -Parent $PSScriptRoot

# Check if API executable exists
$apiExePath = Join-Path $rootDir $ApiPath
if (-not (Test-Path $apiExePath)) {
    Write-Host "ERROR: API executable not found at: $apiExePath" -ForegroundColor Red
    exit 1
}

Write-Host "API Path: $apiExePath" -ForegroundColor Gray
Write-Host ""

# Start the API server
Write-Host "[1/4] Starting API server..." -ForegroundColor Yellow
$apiProcess = Start-Process -FilePath $apiExePath -WorkingDirectory (Split-Path $apiExePath) -PassThru -WindowStyle Hidden

Write-Host "      API process started (PID: $($apiProcess.Id))" -ForegroundColor Green

# Wait for health check
Write-Host "[2/4] Waiting for API health check..." -ForegroundColor Yellow
$healthCheckUrl = "http://127.0.0.1:5005/api/healthz"
$maxRetries = $TimeoutSeconds
$retryCount = 0
$healthy = $false

while ($retryCount -lt $maxRetries) {
    Start-Sleep -Seconds 1
    $retryCount++
    
    try {
        $response = Invoke-WebRequest -Uri $healthCheckUrl -UseBasicParsing -TimeoutSec 2
        if ($response.StatusCode -eq 200) {
            $content = $response.Content | ConvertFrom-Json
            if ($content.status -eq "healthy") {
                $healthy = $true
                Write-Host "      ✓ API is healthy after $retryCount seconds" -ForegroundColor Green
                break
            }
        }
    }
    catch {
        # Ignore errors and retry
    }
}

if (-not $healthy) {
    Write-Host "      ✗ API failed to become healthy after $TimeoutSeconds seconds" -ForegroundColor Red
    Stop-Process -Id $apiProcess.Id -Force -ErrorAction SilentlyContinue
    exit 1
}

# Test capabilities endpoint
Write-Host "[3/4] Testing capabilities endpoint..." -ForegroundColor Yellow
try {
    $capabilitiesUrl = "http://127.0.0.1:5005/api/capabilities"
    $response = Invoke-WebRequest -Uri $capabilitiesUrl -UseBasicParsing -TimeoutSec 5
    if ($response.StatusCode -eq 200) {
        $capabilities = $response.Content | ConvertFrom-Json
        Write-Host "      ✓ Capabilities endpoint working" -ForegroundColor Green
        Write-Host "      Tier: $($capabilities.tier)" -ForegroundColor Gray
    } else {
        throw "Unexpected status code: $($response.StatusCode)"
    }
}
catch {
    Write-Host "      ✗ Failed to test capabilities: $($_.Exception.Message)" -ForegroundColor Red
    Stop-Process -Id $apiProcess.Id -Force -ErrorAction SilentlyContinue
    exit 1
}

# Test script generation (quick generate with Free mode)
Write-Host "[4/4] Testing quick script generation..." -ForegroundColor Yellow
try {
    $scriptUrl = "http://127.0.0.1:5005/api/script"
    $requestBody = @{
        topic = "Smoke Test Video"
        audience = "General"
        goal = "Test"
        tone = "Neutral"
        language = "English"
        aspect = "Widescreen16x9"
        targetDurationMinutes = 0.25
        pacing = "Conversational"
        density = "Balanced"
        style = "Documentary"
    } | ConvertTo-Json
    
    $response = Invoke-RestMethod -Uri $scriptUrl -Method Post -Body $requestBody -ContentType "application/json" -TimeoutSec 10
    
    if ($response.success -and $response.script) {
        Write-Host "      ✓ Script generation working" -ForegroundColor Green
        Write-Host "      Script length: $($response.script.Length) characters" -ForegroundColor Gray
    } else {
        throw "Script generation returned unexpected response"
    }
}
catch {
    Write-Host "      ✗ Failed to test script generation: $($_.Exception.Message)" -ForegroundColor Red
    Stop-Process -Id $apiProcess.Id -Force -ErrorAction SilentlyContinue
    exit 1
}

# Clean up
Write-Host ""
Write-Host "Stopping API server..." -ForegroundColor Yellow
Stop-Process -Id $apiProcess.Id -Force -ErrorAction SilentlyContinue
Write-Host "      ✓ API stopped" -ForegroundColor Green

Write-Host ""
Write-Host "========================================" -ForegroundColor Green
Write-Host " Smoke Test Passed!" -ForegroundColor Green
Write-Host "========================================" -ForegroundColor Green
Write-Host ""
Write-Host "All basic functionality tests passed successfully." -ForegroundColor White
Write-Host "Total time: ~$retryCount seconds" -ForegroundColor Gray
Write-Host ""

exit 0
