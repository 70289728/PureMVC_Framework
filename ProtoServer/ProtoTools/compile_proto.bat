@echo off
setlocal enabledelayedexpansion

rem ============================================================
rem Proto Compiler — generates C# from .proto files
rem 
rem Proto source structure:
rem   ProtoFiles/Base/       → generates to Unity FrameworkAssembly + Server
rem   ProtoFiles/HotUpdate/   → generates to Unity HotUpdateAssembly + Server
rem
rem Output:
rem   Base      → Assets\Scripts\FrameworkAssembly\BaseProtoScripts
rem   HotUpdate → Assets\Scripts\HotUpdateAssembly\ProtoScripts
rem   Server    → ProtoServer\ProtoServer\ProtoScripts\Generated (both layers)
rem ============================================================

set SCRIPT_DIR=E:\AllProject\PureMVC_And_Server\PureMVC_Framework\ProtoServer\ProtoFiles

rem ── Output paths ──
set BASE_UNITY_OUT=E:\AllProject\PureMVC_And_Server\PureMVC_Framework\Assets\Scripts\FrameworkAssembly\BaseProtoScripts
set HOT_UNITY_OUT=E:\AllProject\PureMVC_And_Server\PureMVC_Framework\Assets\Scripts\HotUpdateAssembly\HotUpdateProtoScripts
set SERVER_OUT=E:\AllProject\PureMVC_And_Server\PureMVC_Framework\ProtoServer\ProtoServer\ProtoScripts\Generated

rem check protoc
where protoc >nul 2>nul
if errorlevel 1 (
    echo Error: protoc compiler not found in PATH
    echo Please install Protocol Buffers compiler and add it to PATH
    pause
    exit /b 1
)

rem create and clean output dirs
for %%d in ("%BASE_UNITY_OUT%" "%HOT_UNITY_OUT%" "%SERVER_OUT%") do (
    if not exist %%d mkdir %%d
    echo Cleaning: %%d
    del /q "%%d\*" 2>nul
    for /d %%i in ("%%d\*") do rmdir /s /q "%%i" 2>nul
)

rem ── Compile Base proto files ──
echo.
echo === Compiling Base proto files ===
set BASE_SRC=%SCRIPT_DIR%\Base
for %%f in ("%BASE_SRC%\*.proto") do (
    echo   %%~nxf
    protoc --proto_path="%BASE_SRC%" --csharp_out="%BASE_UNITY_OUT%" "%%f"
    if !errorlevel! neq 0 (
        echo Error compiling %%~nxf
        pause & exit /b 1
    )
    rem Server also gets Base proto output
    protoc --proto_path="%BASE_SRC%" --csharp_out="%SERVER_OUT%" "%%f"
    if !errorlevel! neq 0 (
        echo Error compiling %%~nxf for server
        pause & exit /b 1
    )
)

rem ── Compile HotUpdate proto files ──
echo.
echo === Compiling HotUpdate proto files ===
set HOT_SRC=%SCRIPT_DIR%\HotUpdate
for %%f in ("%HOT_SRC%\*.proto") do (
    echo   %%~nxf
    protoc --proto_path="%HOT_SRC%" --proto_path="%BASE_SRC%" --csharp_out="%HOT_UNITY_OUT%" "%%f"
    if !errorlevel! neq 0 (
        echo Error compiling %%~nxf
        pause & exit /b 1
    )
    rem Server also gets HotUpdate proto output
    protoc --proto_path="%HOT_SRC%" --proto_path="%BASE_SRC%" --csharp_out="%SERVER_OUT%" "%%f"
    if !errorlevel! neq 0 (
        echo Error compiling %%~nxf for server
        pause & exit /b 1
    )
)

echo.
echo All proto files compiled successfully!
echo   Base Unity  : %BASE_UNITY_OUT%
echo   Hot  Unity  : %HOT_UNITY_OUT%
echo   Server      : %SERVER_OUT%
pause
