@echo off
REM One Pace Jellyfin Plugin Build Script for Windows

echo Building One Pace Jellyfin Plugin...
echo.

REM Check if .NET SDK is installed
where dotnet >nul 2>nul
if %ERRORLEVEL% NEQ 0 (
    echo Error: .NET SDK not found. Please install .NET 8.0 SDK.
    echo Download from: https://dotnet.microsoft.com/download
    pause
    exit /b 1
)

REM Check .NET version
echo Using .NET SDK version:
dotnet --version
echo.

REM Clean previous builds
echo Cleaning previous builds...
dotnet clean --verbosity quiet

REM Restore dependencies
echo Restoring dependencies...
dotnet restore

REM Build in Release mode
echo Building in Release mode...
dotnet build -c Release

if %ERRORLEVEL% EQU 0 (
    echo.
    echo ✅ Build successful!
    echo.
    echo Plugin DLL location:
    echo   %CD%\JellyfinPlugin.OnePace\bin\Release\net8.0\JellyfinPlugin.OnePace.dll
    echo.
    echo To install:
    echo 1. Copy the DLL to your Jellyfin plugins directory:
    echo    %%AppData%%\Jellyfin\Server\plugins\OnePace\
    echo.
    echo 2. Restart Jellyfin
    echo.
    pause
) else (
    echo.
    echo ❌ Build failed. Check the errors above.
    pause
    exit /b 1
)
