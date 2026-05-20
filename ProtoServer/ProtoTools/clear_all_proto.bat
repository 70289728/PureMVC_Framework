@echo off
setlocal enabledelayedexpansion

rem ============================================================
rem Clear all generated proto C# files
rem ============================================================

set BASE_URL=E:\AllProject\PureMVC_And_Server\PureMVC_Framework

set DIRS=%BASE_URL%\ProtoServer\ProtoFiles\Base
set DIRS=%DIRS% %BASE_URL%\ProtoServer\ProtoFiles\HotUpdate
set DIRS=%DIRS% %BASE_URL%\Assets\Scripts\FrameworkAssembly\BaseProtoScripts
set DIRS=%DIRS% %BASE_URL%\Assets\Scripts\HotUpdateAssembly\HotUpdateProtoScripts
set DIRS=%DIRS% %BASE_URL%\ProtoServer\ProtoServer\ProtoScripts\Generated

for %%d in (%DIRS%) do (
    echo Cleaning: %%d
    del /q "%%d\*" 2>nul
    for /d %%i in ("%%d\*") do rmdir /s /q "%%i" 2>nul
)

echo All proto directories cleared.
pause
