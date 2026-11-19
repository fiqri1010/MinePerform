# ==============================================================================
# MinePerform - Windows PowerShell 1-Click Build & Launch Script
# ==============================================================================

Write-Host "======================================================================" -ForegroundColor Cyan
Write-Host "   MinePerform Local Build & Run (PC Avalonia Windows x64)" -ForegroundColor Cyan
Write-Host "======================================================================" -ForegroundColor Cyan
Write-Host ""

# 1. Cek dotnet CLI
if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) {
    Write-Host "[ERROR] .NET SDK tidak ditemukan! Silakan instal .NET 9 SDK dari https://dotnet.microsoft.com/" -ForegroundColor Red
    exit 1
}

$dotnetVer = dotnet --version
Write-Host "[INFO] Terdeteksi .NET SDK versi: $dotnetVer" -ForegroundColor Green

# 2. Jalankan Tests
Write-Host "`n[1/3] Menjalankan Unit Tests..." -ForegroundColor Yellow
dotnet test MinePerform.sln -c Release --nologo

# 3. Publish Windows x64
Write-Host "`n[2/3] Membangun PC.AvaloniaApp..." -ForegroundColor Yellow
$aotSuccess = $false
try {
    Write-Host "Mencoba Native AOT..." -ForegroundColor DarkGray
    dotnet publish src/PC.AvaloniaApp/PC.AvaloniaApp.csproj -c Release -r win-x64 --self-contained true -o publish/win-x64 --nologo
    if ($LASTEXITCODE -eq 0) {
        $aotSuccess = $true
    }
} catch {
    $aotSuccess = $false
}

if (-not $aotSuccess) {
    Write-Host "`n[INFO] Beralih otomatis ke Standalone Self-Contained (PublishAot=false)..." -ForegroundColor Yellow
    dotnet publish src/PC.AvaloniaApp/PC.AvaloniaApp.csproj -c Release -r win-x64 --self-contained true -p:PublishAot=false -o publish/win-x64 --nologo
    if ($LASTEXITCODE -ne 0) {
        Write-Host "[ERROR] Build gagal!" -ForegroundColor Red
        exit 1
    }
}

Write-Host "`n======================================================================" -ForegroundColor Green
Write-Host "[3/3] SUKSES! Aplikasi siap dijalankan di:" -ForegroundColor Green
Write-Host "$PSScriptRoot\publish\win-x64\MinePerform.PC.exe" -ForegroundColor White
Write-Host "======================================================================" -ForegroundColor Green

# Buka folder & tawarkan jalankan langsung
Start-Process "explorer.exe" "$PSScriptRoot\publish\win-x64"
$runChoice = Read-Host "`nJalankan MinePerform.PC.exe sekarang? (Y/n)"
if ($runChoice -eq "" -or $runChoice -match "^[yY]") {
    Start-Process "$PSScriptRoot\publish\win-x64\MinePerform.PC.exe"
}
