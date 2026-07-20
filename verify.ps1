# verify.ps1 — run after every change: builds the solution and runs all tests.
# Usage:  .\verify.ps1        (build + test)
#         .\verify.ps1 -SkipTests   (build only, faster)
param([switch]$SkipTests)

$ErrorActionPreference = "Stop"
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
