# verify.ps1 — run after every change: builds the solution and runs all tests.
# Usage:  .\verify.ps1        (build + test)
#         .\verify.ps1 -SkipTests   (build only, faster)
param([switch]$SkipTests)

$ErrorActionPreference = "Stop"

# Locate dotnet: PATH first, then common install locations
if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) {
    $candidates = @(
        "$env:ProgramFiles\dotnet\dotnet.exe",
        "$env:LocalAppData\Microsoft\dotnet\dotnet.exe",
        "$env:USERPROFILE\.dotnet\dotnet.exe"
    )
    $found = $candidates | Where-Object { Test-Path $_ } | Select-Object -First 1
    if ($found) {
        $env:PATH = (Split-Path $found) + ";" + $env:PATH
        Write-Host "Using dotnet at $found" -ForegroundColor Yellow
    } else {
        Write-Host ".NET SDK not found. Install it with:" -ForegroundColor Red
        Write-Host "    winget install Microsoft.DotNet.SDK.10" -ForegroundColor Yellow
        Write-Host "Then close and reopen this terminal and rerun .\verify.ps1"
        exit 1
    }
}
$sw = [System.Diagnostics.Stopwatch]::StartNew()

Write-Host "==> Restoring..." -ForegroundColor Cyan
dotnet restore Quadro.sln
if ($LASTEXITCODE -ne 0) { Write-Host "RESTORE FAILED" -ForegroundColor Red; exit 1 }

Write-Host "==> Building (Release)..." -ForegroundColor Cyan
dotnet build Quadro.sln --no-restore -c Release
if ($LASTEXITCODE -ne 0) { Write-Host "BUILD FAILED" -ForegroundColor Red; exit 1 }

if (-not $SkipTests) {
    Write-Host "==> Running tests..." -ForegroundColor Cyan
    dotnet test Quadro.sln --no-build -c Release
    if ($LASTEXITCODE -ne 0) { Write-Host "TESTS FAILED" -ForegroundColor Red; exit 1 }
}

$sw.Stop()
Write-Host ("==> ALL GREEN in {0:mm\:ss}" -f $sw.Elapsed) -ForegroundColor Green
