# add-migration.ps1 - voegt een EF Core migratie toe (US-30 workflow).
# Gebruik:  .\Scripts\add-migration.ps1 AddGebruikers
param([Parameter(Mandatory=$true)][string]$Naam)

$ErrorActionPreference = "Stop"

$repoRoot = Split-Path $PSScriptRoot -Parent
Set-Location $repoRoot

# Locate dotnet (zelfde logica als verify.ps1)
if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) {
    $candidates = @(
        "$env:ProgramFiles\dotnet\dotnet.exe",
        "$env:LocalAppData\Microsoft\dotnet\dotnet.exe",
        "$env:USERPROFILE\.dotnet\dotnet.exe"
    )
    $found = $candidates | Where-Object { Test-Path $_ } | Select-Object -First 1
    if ($found) { $env:PATH = (Split-Path $found) + ";" + $env:PATH }
    else { Write-Host ".NET SDK niet gevonden." -ForegroundColor Red; exit 1 }
}
if (Test-Path "$env:USERPROFILE\.dotnet\tools") {
    $env:PATH = "$env:USERPROFILE\.dotnet\tools;" + $env:PATH
}

# dotnet-ef versie moet exact matchen met de EF Core packages (9.0.9).
# 'dotnet tool update' weigert een downgrade ("requested version is lower than
# existing version") wanneer er al een nieuwere dotnet-ef (bv. 10.x) globaal staat.
# Daarom bij een mismatch verwijderen en op de juiste versie herinstalleren.
$efVersion = "9.0.9"
$ef = dotnet tool list --global | Select-String "dotnet-ef"
if (-not $ef) {
    dotnet tool install --global dotnet-ef --version $efVersion
    if ($LASTEXITCODE -ne 0) { exit 1 }
}
elseif ($ef -notmatch [regex]::Escape($efVersion)) {
    dotnet tool uninstall --global dotnet-ef | Out-Null
    dotnet tool install --global dotnet-ef --version $efVersion
    if ($LASTEXITCODE -ne 0) { exit 1 }
}

Write-Host "Migratie '$Naam' genereren..." -ForegroundColor Cyan
dotnet ef migrations add $Naam --project (Join-Path $repoRoot "QuadroApp.csproj")
if ($LASTEXITCODE -ne 0) { Write-Host "MISLUKT" -ForegroundColor Red; exit 1 }

Write-Host "Klaar. Controleer het gegenereerde bestand in Migrations/ en draai .\verify.ps1" -ForegroundColor Green
