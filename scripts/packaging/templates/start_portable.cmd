@echo off
REM Aura Video Studio Portable Launcher
REM This script starts the API server and waits for health check before launching the shell

echo =========================================
echo  Aura Video Studio - Portable Edition
echo =========================================
echo.

REM Change to the script directory
cd /d "%~dp0"

REM Start the API server in the background
echo Starting API server...
start "Aura Video Studio API" /D "api" "Aura.Api.exe"

REM Wait for the API to become healthy
echo Waiting for API to be ready...
set MAX_RETRIES=30
set RETRY_COUNT=0

:WAIT_LOOP
timeout /t 1 /nobreak >nul
set /a RETRY_COUNT+=1

REM Check if API is healthy using curl (if available) or PowerShell
where curl >nul 2>nul
if %ERRORLEVEL% EQU 0 (
    curl -s -o nul -w "%%{http_code}" http://127.0.0.1:5005/api/healthz | findstr "200" >nul 2>nul
    if %ERRORLEVEL% EQU 0 (
        echo API is ready!
        goto LAUNCH_UI
    )
) else (
    REM Fallback to PowerShell if curl is not available
    powershell -Command "try { $r = Invoke-WebRequest -Uri 'http://127.0.0.1:5005/api/healthz' -UseBasicParsing -TimeoutSec 2; exit ($r.StatusCode -eq 200 ? 0 : 1) } catch { exit 1 }" >nul 2>nul
    if %ERRORLEVEL% EQU 0 (
        echo API is ready!
        goto LAUNCH_UI
    )
)

if %RETRY_COUNT% GEQ %MAX_RETRIES% (
    echo.
    echo ERROR: API failed to start after %MAX_RETRIES% seconds
    echo Please check the API console window for errors
    echo.
    pause
    exit /b 1
)

goto WAIT_LOOP

:LAUNCH_UI
echo.
echo Opening application...

REM Check if WPF shell exists, otherwise open browser
if exist "AuraVideoStudio.exe" (
    start "" "AuraVideoStudio.exe"
) else (
    start "" "http://127.0.0.1:5005"
    echo Application opened in your web browser
)

echo.
echo The application is now running.
echo To stop the application, close the "Aura Video Studio API" console window.
echo.
