# add-migration.ps1 - voegt een EF Core migratie toe voor BEIDE providers (US-30/US-41 workflow).
# Gebruik:  .\Scripts\add-migration.ps1 AddGebruikers
#
# Sinds US-41 leeft de EF-laag (AppDbContext) in QuadroApp.Data en heeft elke provider een eigen
# migratieset met dezelfde naam:
#   1. SQLite     -> QuadroApp.Data/Migrations              (design-time: AppDbContextFactory)
#   2. PostgreSQL -> QuadroApp.Migrations.Npgsql/Migrations (design-time: NpgsqlDesignTimeFactory,
#      leest QUADRO_PG_DESIGN_CS; 'migrations add' heeft GEEN draaiende database nodig)
#
# Let op bij een pure datamigratie (model ongewijzigd): dotnet ef herschrijft soms
# AppDbContextModelSnapshot.cs met enkel herordende entiteiten. Dat is ruis en mag je terugdraaien
# (git checkout -- <pad>/AppDbContextModelSnapshot.cs); de drift-guard
# (ModelDriftTests/MigrationSafetyTests) blijft groen.
#
# Migratie ongedaan maken (in beide projecten):
#   dotnet ef migrations remove --project QuadroApp.Data --startup-project QuadroApp.Data
#   dotnet ef migrations remove --project QuadroApp.Migrations.Npgsql --startup-project QuadroApp.Migrations.Npgsql
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

# Dummy design-time connection string voor Npgsql; een bestaande waarde wordt gerespecteerd.
if (-not $env:QUADRO_PG_DESIGN_CS) {
    $env:QUADRO_PG_DESIGN_CS = "Host=localhost;Database=x;Username=x;Password=x"
}

Write-Host "[1/2] SQLite-migratie '$Naam' genereren (QuadroApp.Data)..." -ForegroundColor Cyan
dotnet ef migrations add $Naam --project QuadroApp.Data --startup-project QuadroApp.Data
if ($LASTEXITCODE -ne 0) { Write-Host "MISLUKT (SQLite)" -ForegroundColor Red; exit 1 }

Write-Host "[2/2] PostgreSQL-migratie '$Naam' genereren (QuadroApp.Migrations.Npgsql)..." -ForegroundColor Cyan
dotnet ef migrations add $Naam --project QuadroApp.Migrations.Npgsql --startup-project QuadroApp.Migrations.Npgsql
if ($LASTEXITCODE -ne 0) {
    Write-Host "MISLUKT (Npgsql). De SQLite-migratie staat er al; verwijder die met:" -ForegroundColor Red
    Write-Host "    dotnet ef migrations remove --project QuadroApp.Data --startup-project QuadroApp.Data" -ForegroundColor Yellow
    exit 1
}

Write-Host "Klaar. Controleer de gegenereerde bestanden in QuadroApp.Data/Migrations/ en QuadroApp.Migrations.Npgsql/Migrations/ en draai .\verify.ps1" -ForegroundColor Green
