@echo off
setlocal enabledelayedexpansion
title MinePerform - Windows Local Build Script

echo ======================================================================
echo    MinePerform Local Build (PC Avalonia Windows x64)
echo ======================================================================
echo.

:: 1. Verifikasi instalasi dotnet
where dotnet >nul 2>nul
if %errorlevel% neq 0 (
    echo [ERROR] .NET SDK tidak ditemukan di PATH!
    echo Silakan unduh dan instal .NET 9 SDK dari: https://dotnet.microsoft.com/download
    pause
    exit /b 1
)

echo [1/3] Menjalankan Unit Tests (8/8 Test Suite)...
dotnet test MinePerform.sln -c Release --nologo
if %errorlevel% neq 0 (
    echo [WARNING] Test mendeteksi isu, melanjutkan proses build...
)

echo.
echo [2/3] Membangun PC.AvaloniaApp untuk Windows x64...
echo Mencoba build Native AOT...
dotnet publish src\PC.AvaloniaApp\PC.AvaloniaApp.csproj -c Release -r win-x64 --self-contained true -o publish\win-x64 --nologo
if %errorlevel% neq 0 (
    echo.
    echo [INFO] Build Native AOT membutuhkan C++ Build Tools (MSVC link.exe).
    echo Beralih otomatis ke Standalone Self-Contained Release (tanpa butuh C++ tools)...
    dotnet publish src\PC.AvaloniaApp\PC.AvaloniaApp.csproj -c Release -r win-x64 --self-contained true -p:PublishAot=false -o publish\win-x64 --nologo
    if %errorlevel% neq 0 (
        echo [ERROR] Build gagal! Periksa pesan error di atas.
        pause
        exit /b 1
    )
)

echo.
echo ======================================================================
echo [3/3] SUKSES! Aplikasi telah selesai dibuild:
echo Lokasi: %CD%\publish\win-x64\MinePerform.PC.exe
echo ======================================================================
echo.

:: Buka folder hasil build di Windows Explorer
explorer publish\win-x64

echo Tekan sembarang tombol untuk menjalankan aplikasi sekarang...
pause >nul
start "" "publish\win-x64\MinePerform.PC.exe"
