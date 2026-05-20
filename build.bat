@echo off
setlocal enabledelayedexpansion

echo ========================================
echo   PureMVC Framework - Build Pipeline
echo ========================================
echo.

REM ============================================
REM Configuration
REM ============================================
set UNITY_PATH=C:\Program Files\Unity\Hub\Editor\2022.3.0f1\Editor\Unity.exe
set PROJECT_PATH=%~dp0
set LOG_DIR=%PROJECT_PATH%Logs\Build

REM ============================================
REM Parse arguments
REM ============================================
set PLATFORM=%1
set VERSION_FLAG=%2

if "%PLATFORM%"=="" (
    echo Usage: build.bat [platform] [version_flag]
    echo.
    echo Platforms:
    echo   windows   - Build for Windows (StandaloneWindows64^)
    echo   android   - Build for Android
    echo   all       - Build all platforms
    echo.
    echo Version Flags:
    echo   inc       - Increment version before build (default^)
    echo   keep      - Keep current version
    echo.
    echo Examples:
    echo   build.bat windows inc
    echo   build.bat android keep
    echo   build.bat all inc
    echo.
    pause
    exit /b 1
)

if "%VERSION_FLAG%"=="" set VERSION_FLAG=inc

REM ============================================
REM Validate Unity path
REM ============================================
if not exist "%UNITY_PATH%" (
    echo [ERROR] Unity not found at: %UNITY_PATH%
    echo Please update UNITY_PATH in build.bat to match your Unity installation.
    pause
    exit /b 1
)

REM ============================================
REM Create log directory
REM ============================================
if not exist "%LOG_DIR%" mkdir "%LOG_DIR%"

REM ============================================
REM Determine build method
REM ============================================
set METHOD=
set PLATFORM_NAME=

if /i "%PLATFORM%"=="windows" (
    if /i "%VERSION_FLAG%"=="inc" (
        set METHOD=ProjectBuilder.BuildWindows
    ) else (
        set METHOD=ProjectBuilder.BuildWindowsKeepVersion
    )
    set PLATFORM_NAME=Windows
)

if /i "%PLATFORM%"=="android" (
    if /i "%VERSION_FLAG%"=="inc" (
        set METHOD=ProjectBuilder.BuildAndroid
    ) else (
        set METHOD=ProjectBuilder.BuildAndroidKeepVersion
    )
    set PLATFORM_NAME=Android
)

if /i "%PLATFORM%"=="all" (
    set METHOD=ProjectBuilder.BuildAll
    set PLATFORM_NAME=All
)

if "%METHOD%"=="" (
    echo [ERROR] Unknown platform: %PLATFORM%
    echo Valid platforms: windows, android, all
    pause
    exit /b 1
)

REM ============================================
REM Read current version for display
REM ============================================
set VERSION_FILE=%PROJECT_PATH%ProjectSettings\version.txt
if exist "%VERSION_FILE%" (
    set /p CURRENT_VERSION=<"%VERSION_FILE%"
    echo Current version: !CURRENT_VERSION!
) else (
    echo [WARNING] version.txt not found, will use default 1.0.0
)

REM ============================================
REM Build
REM ============================================
set LOG_FILE=%LOG_DIR%\build_%PLATFORM%_%date:~0,4%%date:~5,2%%date:~8,2%_%time:~0,2%%time:~3,2%%time:~6,2%.log
set LOG_FILE=%LOG_FILE: =0%

echo.
echo Platform: %PLATFORM_NAME%
echo Method:   %METHOD%
echo Log:      %LOG_FILE%
echo.
echo Starting build...
echo.

"%UNITY_PATH%" ^
    -batchmode ^
    -quit ^
    -projectPath "%PROJECT_PATH%" ^
    -executeMethod %METHOD% ^
    -logFile "%LOG_FILE%" ^
    -nographics

set BUILD_RESULT=%errorlevel%

echo.
if %BUILD_RESULT% equ 0 (
    echo ========================================
    echo   BUILD SUCCESS
    echo ========================================
    echo.
    echo Output: %PROJECT_PATH%Builds\%PLATFORM_NAME%\
    echo Log:    %LOG_FILE%
    echo.
) else (
    echo ========================================
    echo   BUILD FAILED (exit code: %BUILD_RESULT%^)
    echo ========================================
    echo.
    echo Check log for details: %LOG_FILE%
    echo.
)

pause
exit /b %BUILD_RESULT%
