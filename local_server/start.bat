@echo off
cd /d %~dp0

echo ========================================
echo   Hot Update Local Test Server
echo ========================================
echo.

call python --version >nul 2>&1
if errorlevel 1 goto nopython

set PORT=8080
if not "%1"=="" set PORT=%1

set VERSION=
if not "%2"=="" set VERSION=%2

powershell -Command "Get-NetTCPConnection -LocalPort %PORT% -ErrorAction SilentlyContinue | ForEach-Object { Stop-Process -Id $_.OwningProcess -Force }" 2>nul

echo Starting server on port %PORT%...
if not "%VERSION%"=="" echo Serving version: %VERSION%
echo.

if "%VERSION%"=="" (
    call python server.py %PORT%
) else (
    call python server.py %PORT% %VERSION%
)

echo.
echo Server stopped.
goto end

:nopython
echo [ERROR] Python not found.

:end
echo.
echo Press any key to close...
pause >nul
