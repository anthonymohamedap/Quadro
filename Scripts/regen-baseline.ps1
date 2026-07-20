# regen-baseline.ps1 — US-30: generates the Baseline migration from the current model.
# Run from the repo root AFTER the old Migrations/ files have been deleted:
#   .\Scripts\regen-baseline.ps1
param()

$ErrorActionPreference = "Stop"

# dotnet-ef beschikbaar?
$ef = dotnet tool list --global | Select-String "dotnet-ef"
if (-not $ef) {
    Write-Host "dotnet-ef installeren..." -ForegroundColor Cyan
    dotnet tool install --global dotnet-ef
    if ($LASTEXITCODE -ne 0) { exit 1 }
}

if (Test-Path "Migrations") {
    $existing = Get-ChildItem "Migrations" -Filter "*.cs" -ErrorAction SilentlyContinue
    if ($existing) {
        Write-Host "Migrations/ bevat nog .cs-bestanden — eerst opruimen (git rm Migrations/*.cs)." -ForegroundColor Red
        exit 1
    }
}

Write-Host "Baseline-migratie genereren..." -ForegroundColor Cyan
dotnet ef migrations add Baseline --project QuadroApp.csproj
if ($LASTEXITCODE -ne 0) { Write-Host "MISLUKT" -ForegroundColor Red; exit 1 }

Write-Host "Klaar. Nu: .\verify.ps1" -ForegroundColor Green
