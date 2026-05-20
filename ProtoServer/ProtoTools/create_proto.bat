@echo off
setlocal enabledelayedexpansion

rem ============================================================
rem Create a new .proto file
rem ============================================================

set SCRIPT_DIR=E:\AllProject\PureMVC_And_Server\PureMVC_Framework\ProtoServer\ProtoFiles

echo Select proto layer:
echo   [1] Base
echo   [2] HotUpdate
set /p "LAYER_CHOICE=Choice (1/2): "

if "!LAYER_CHOICE!"=="1" (
    set TARGET_DIR=%SCRIPT_DIR%\Base
    set IMPORT=network_module.proto
)
if "!LAYER_CHOICE!"=="2" (
    set TARGET_DIR=%SCRIPT_DIR%\HotUpdate
    set IMPORT=network_module.proto
)
if "!TARGET_DIR!"=="" (
    echo Invalid choice
    pause
    exit /b
)

set /p "FILE_NAME=please input proto file name: "
if "!FILE_NAME!"=="" set FILE_NAME=my_proto

rem Ensure .proto extension
if not "!FILE_NAME:~-6!"==".proto" set FILE_NAME=!FILE_NAME!.proto

rem Create dir if missing
if not exist "!TARGET_DIR!" mkdir "!TARGET_DIR!"

set FULL_PATH=!TARGET_DIR!\!FILE_NAME!
if exist "!FULL_PATH!" (
    set /p "OVERWRITE=the file exists, overwrite? (y/N): "
    if /i not "!OVERWRITE!"=="y" (
        echo Operation cancelled
        pause
        exit /b
    )
)

rem Extract base name and capitalize first letter
set BASE_NAME=!FILE_NAME:.proto=!
set FIRST_CHAR=!BASE_NAME:~0,1!
set REST=!BASE_NAME:~1!

echo syntax = "proto3";> "!FULL_PATH!"
echo.>> "!FULL_PATH!"
echo import "!IMPORT!";>> "!FULL_PATH!"
echo.>> "!FULL_PATH!"
echo message !BASE_NAME!C2S {>> "!FULL_PATH!"
echo.>> "!FULL_PATH!"
echo }>> "!FULL_PATH!"
echo.>> "!FULL_PATH!"
echo message !BASE_NAME!S2C {>> "!FULL_PATH!"
echo    S2CResult result = 1;>> "!FULL_PATH!"
echo }>> "!FULL_PATH!"

echo Proto file created: !FULL_PATH!
type "!FULL_PATH!"
pause
