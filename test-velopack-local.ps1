#Requires -Version 5.1
<#
.SYNOPSIS
    Lokale Velopack update-pipeline test voor QuadroApp.

.DESCRIPTION
    Dit script bouwt twee versies van QuadroApp (OldVersion en NewVersion),
    pakt ze met vpk, start een lokale HTTP-server en geeft stap-voor-stap
    instructies om de volledige update-flow (download + "Herstart nu") te testen.

    De update-URL in App.axaml.cs is bewust gesplitst met #if LOCAL_TEST:
      - Normaal (CI/release): https://github.com/anthonymohamedap/Quadro
      - Dit script          : http://localhost:<Port>  (via -p:DefineConstants=LOCAL_TEST)
    Terugzetten na de test: git checkout App.axaml.cs

.PARAMETER OldVersion
    Versienummer van de baseline-installatie (de "al geïnstalleerde" versie).
    Standaard: 1.0.3

.PARAMETER NewVersion
    Versienummer van de beschikbare update op de lokale server.
    Standaard: 1.0.4

.PARAMETER Port
    TCP-poort voor de lokale HTTP-server. Standaard: 8080

.EXAMPLE
    .\test-velopack-local.ps1
    .\test-velopack-local.ps1 -OldVersion 1.0.4 -NewVersion 1.0.5 -Port 9090

.NOTES
    Vereisten:
      - .NET 10 SDK        https://dot.net
      - vpk CLI            dotnet tool install -g vpk
      - Python 3.7+        https://python.org  (voor de HTTP-server)
        OF Node.js         https://nodejs.org  (als Python ontbreekt)
#>

[CmdletBinding()]
param(
    [string] $OldVersion = "1.0.3",
    [string] $NewVersion = "1.0.4",
    [int]    $Port       = 8080
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

# ─── Paden ────────────────────────────────────────────────────────────────────
$ProjectRoot = $PSScriptRoot
$ProjectFile = Join-Path $ProjectRoot "QuadroApp.csproj"
$PublishDir  = Join-Path $ProjectRoot "publish-local"
$ReleasesDir = Join-Path $ProjectRoot "releases-local"
$ServerUrl   = "http://localhost:$Port"

# ─── Kleurhulpfuncties ────────────────────────────────────────────────────────
function Write-Banner {
    Clear-Host
    Write-Host ""
    Write-Host "  ╔══════════════════════════════════════════════════════════════╗" -ForegroundColor Cyan
    Write-Host "  ║   QuadroApp — Lokale Velopack Update Test                   ║" -ForegroundColor Cyan
    Write-Host "  ║   $OldVersion  →  $NewVersion   via $ServerUrl$((' ' * (23 - $ServerUrl.Length)))║" -ForegroundColor Cyan
    Write-Host "  ╚══════════════════════════════════════════════════════════════╝" -ForegroundColor Cyan
    Write-Host ""
}

function Write-Step([string]$Num, [string]$Title) {
    Write-Host ""
    Write-Host "  ─────────────────────────────────────────────────────────────" -ForegroundColor DarkCyan
    Write-Host "  STAP $Num  $Title" -ForegroundColor Cyan
    Write-Host "  ─────────────────────────────────────────────────────────────" -ForegroundColor DarkCyan
}

function Write-Info  ([string]$Msg) { Write-Host "     $Msg" -ForegroundColor Gray }
function Write-Ok    ([string]$Msg) { Write-Host "  ✔  $Msg" -ForegroundColor Green }
function Write-Warn  ([string]$Msg) { Write-Host "  ⚠  $Msg" -ForegroundColor Yellow }
function Write-Err   ([string]$Msg) { Write-Host "  ✖  $Msg" -ForegroundColor Red }
function Write-Do    ([string]$Msg) { Write-Host "  👉 $Msg" -ForegroundColor Magenta }

function Invoke-Step([string]$Num, [string]$Title, [scriptblock]$Body) {
    Write-Step $Num $Title
    & $Body
    if ($LASTEXITCODE -and $LASTEXITCODE -ne 0) {
        Write-Err "Stap $Num mislukt (exit $LASTEXITCODE). Script gestopt."
        exit $LASTEXITCODE
    }
}

# ─── Banner ───────────────────────────────────────────────────────────────────
Write-Banner
Write-Info "Projectmap   : $ProjectRoot"
Write-Info "Publish-map  : $PublishDir"
Write-Info "Releases-map : $ReleasesDir"
Write-Info "Update-URL   : $ServerUrl"

# ═══════════════════════════════════════════════════════════════════════════════
# STAP 0 — Vereisten controleren
# ═══════════════════════════════════════════════════════════════════════════════
Invoke-Step "0" "Vereisten controleren" {

    # dotnet SDK
    $dotnetVersion = & dotnet --version 2>&1
    if ($LASTEXITCODE -ne 0) {
        Write-Err ".NET SDK niet gevonden. Installeer .NET 10 SDK via https://dot.net en probeer opnieuw."
        exit 1
    }
    Write-Ok ".NET SDK      : $dotnetVersion"

    # Projectbestand
    if (-not (Test-Path $ProjectFile)) {
        Write-Err "QuadroApp.csproj niet gevonden op: $ProjectFile"
        Write-Err "Voer dit script uit vanuit de projectmap."
        exit 1
    }
    Write-Ok "Projectbestand: $ProjectFile"

    # vpk CLI — probeer globale installatie, daarna dotnet tool run
    $script:VpkCmd = $null
    foreach ($candidate in @("vpk", "dotnet-vpk")) {
        $version = & $candidate --version 2>&1
        if ($LASTEXITCODE -eq 0) { $script:VpkCmd = $candidate; break }
    }

    if (-not $script:VpkCmd) {
        Write-Warn "vpk CLI niet gevonden — probeer nu te installeren..."
        & dotnet tool install -g vpk
        if ($LASTEXITCODE -ne 0) {
            Write-Err "vpk kon niet worden geïnstalleerd."
            Write-Err "Installeer handmatig: dotnet tool install -g vpk"
            Write-Err "Zorg daarna dat '%USERPROFILE%\.dotnet\tools' in uw PATH staat."
            exit 1
        }
        $script:VpkCmd = "vpk"
        Write-Ok "vpk geïnstalleerd."
    }
    $vpkVersion = & $script:VpkCmd --version 2>&1
    Write-Ok "vpk CLI       : $vpkVersion (opdracht: $script:VpkCmd)"

    # HTTP-serveroptie bepalen
    $script:ServerMode = $null
    foreach ($py in @("python", "python3", "py")) {
        $pyVersion = & $py --version 2>&1
        if ($LASTEXITCODE -eq 0) {
            $script:ServerMode = "python"
            $script:ServerExe  = $py
            Write-Ok "HTTP-server   : Python ($pyVersion)"
            break
        }
    }

    if (-not $script:ServerMode) {
        # Fallback: Node.js via npx http-server
        $nodeVersion = & node --version 2>&1
        if ($LASTEXITCODE -eq 0) {
            $script:ServerMode = "node"
            Write-Ok "HTTP-server   : Node.js $nodeVersion (npx http-server)"
        }
    }

    if (-not $script:ServerMode) {
        Write-Warn "Python en Node.js niet gevonden."
        Write-Warn "De server wordt NIET automatisch gestart."
        Write-Warn "Start hem zelf met een van:"
        Write-Info "  python -m http.server $Port --directory `"$ReleasesDir`""
        Write-Info "  npx http-server -p $Port `"$ReleasesDir`""
        Write-Info "  (of een andere statische bestandsserver op poort $Port)"
        $script:ServerMode = "manual"
    }
}

# ═══════════════════════════════════════════════════════════════════════════════
# STAP 1 — Uitvoermappen leegmaken en aanmaken
# ═══════════════════════════════════════════════════════════════════════════════
Invoke-Step "1" "Uitvoermappen klaarzetten" {
    foreach ($dir in @($PublishDir, $ReleasesDir)) {
        if (Test-Path $dir) {
            Write-Info "Verwijder bestaande map: $dir"
            Remove-Item $dir -Recurse -Force
        }
        New-Item -ItemType Directory $dir | Out-Null
        Write-Ok "Aangemaakt: $dir"
    }
}

# ═══════════════════════════════════════════════════════════════════════════════
# STAP 2 — Publiceren als v<OldVersion>  (de installatie-baseline)
# ═══════════════════════════════════════════════════════════════════════════════
Invoke-Step "2" "Publiceren als v$OldVersion  (installatie-baseline)" {
    Write-Info "dotnet publish met LOCAL_TEST define → update-URL = $ServerUrl"
    Write-Info "Dit is de versie die de gebruiker installeert vóór de update."
    Write-Host ""

    & dotnet publish $ProjectFile `
        -c Release `
        -r win-x64 `
        --self-contained `
        -p:PublishTrimmed=false `
        -p:PublishSingleFile=false `
        "-p:Version=$OldVersion" `
        "-p:DefineConstants=LOCAL_TEST;TRACE" `
        -o $PublishDir `
        --nologo

    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
    Write-Ok "Publish klaar voor v$OldVersion"
    Write-Info "Uitvoermap: $PublishDir"
}

# ═══════════════════════════════════════════════════════════════════════════════
# STAP 3 — Inpakken als v<OldVersion>  →  Setup.exe + full nupkg
# ═══════════════════════════════════════════════════════════════════════════════
Invoke-Step "3" "Inpakken als v$OldVersion  (maakt de Setup.exe aan)" {
    Write-Info "vpk pack maakt de installeerder aan die u eerst installeert."
    Write-Host ""

    & $script:VpkCmd pack `
        --packId      QuadroApp `
        --packVersion $OldVersion `
        --packDir     $PublishDir `
        --outputDir   $ReleasesDir `
        --mainExe     QuadroApp.exe

    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
    Write-Ok "v$OldVersion ingepakt."

    # Toon gegenereerde bestanden
    $files = Get-ChildItem $ReleasesDir -File
    foreach ($f in $files) {
        Write-Info ("  {0,-45}  {1,7} KB" -f $f.Name, [math]::Round($f.Length / 1KB, 1))
    }
}

# ═══════════════════════════════════════════════════════════════════════════════
# STAP 4 — Publiceren als v<NewVersion>  (de beschikbare update)
# ═══════════════════════════════════════════════════════════════════════════════
Invoke-Step "4" "Publiceren als v$NewVersion  (de update die klaarstaat)" {
    Write-Info "Zelfde broncode, hogere versie — vpk berekent automatisch een delta-patch."
    Write-Info "In een echte release bevat dit versie nieuwe functies of bugfixes."
    Write-Host ""

    & dotnet publish $ProjectFile `
        -c Release `
        -r win-x64 `
        --self-contained `
        -p:PublishTrimmed=false `
        -p:PublishSingleFile=false `
        "-p:Version=$NewVersion" `
        "-p:DefineConstants=LOCAL_TEST;TRACE" `
        -o $PublishDir `
        --nologo

    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
    Write-Ok "Publish klaar voor v$NewVersion"
}

# ═══════════════════════════════════════════════════════════════════════════════
# STAP 5 — Inpakken als v<NewVersion>  →  delta-nupkg + full nupkg
# ═══════════════════════════════════════════════════════════════════════════════
Invoke-Step "5" "Inpakken als v$NewVersion  (maakt de update-pakketten aan)" {
    Write-Info "vpk berekent een delta-patch t.o.v. v$OldVersion — alleen de diff wordt gedownload."
    Write-Host ""

    & $script:VpkCmd pack `
        --packId      QuadroApp `
        --packVersion $NewVersion `
        --packDir     $PublishDir `
        --outputDir   $ReleasesDir `
        --mainExe     QuadroApp.exe

    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
    Write-Ok "v$NewVersion ingepakt."

    # Toon alle gegenereerde bestanden
    Write-Host ""
    Write-Info "Inhoud van releases-map (wordt geserveerd door de HTTP-server):"
    $files = Get-ChildItem $ReleasesDir -File | Sort-Object Name
    foreach ($f in $files) {
        $marker = if ($f.Name -match "Setup") { "  ← installeer dit eerst" }
                  elseif ($f.Name -match "releases\.json") { "  ← Velopack leest dit manifest" }
                  elseif ($f.Name -match "$NewVersion") { "  ← dit is de update" }
                  else { "" }
        Write-Info ("  {0,-55}  {1,7} KB{2}" -f $f.Name, [math]::Round($f.Length / 1KB, 1), $marker)
    }
}

# ═══════════════════════════════════════════════════════════════════════════════
# STAP 6 — Lokale HTTP-server starten
# ═══════════════════════════════════════════════════════════════════════════════
Invoke-Step "6" "Lokale HTTP-server starten op poort $Port" {
    $script:ServerProcess = $null

    switch ($script:ServerMode) {

        "python" {
            Write-Info "Start: $script:ServerExe -m http.server $Port --directory `"$ReleasesDir`""
            $script:ServerProcess = Start-Process `
                -FilePath    $script:ServerExe `
                -ArgumentList @("-m", "http.server", "$Port", "--directory", $ReleasesDir) `
                -PassThru `
                -WindowStyle Minimized
            Start-Sleep -Seconds 2
        }

        "node" {
            Write-Info "Start: npx http-server -p $Port `"$ReleasesDir`""
            $script:ServerProcess = Start-Process `
                -FilePath    "npx" `
                -ArgumentList @("http-server", "-p", "$Port", "--cors", $ReleasesDir) `
                -PassThru `
                -WindowStyle Minimized
            Start-Sleep -Seconds 3
        }

        "manual" {
            Write-Warn "Start handmatig een HTTP-server op poort $Port:"
            Write-Host ""
            Write-Host "    python -m http.server $Port --directory `"$ReleasesDir`"" -ForegroundColor Yellow
            Write-Host ""
            Write-Do "Druk op Enter als de server draait..."
            $null = Read-Host
        }
    }

    # Snel testen of de server antwoord
    if ($script:ServerProcess -and -not $script:ServerProcess.HasExited) {
        try {
            $response = Invoke-WebRequest "$ServerUrl/releases.json" `
                -UseBasicParsing -TimeoutSec 4 -ErrorAction Stop
            Write-Ok "Server reageert op $ServerUrl  (HTTP $($response.StatusCode))"
            Write-Ok "releases.json bereikbaar — Velopack kan updaten."
        } catch {
            Write-Warn "Server reageert nog niet. Dit kan normaal zijn — ga toch verder."
            Write-Info "(Fout: $_)"
        }
    } elseif ($script:ServerProcess -and $script:ServerProcess.HasExited) {
        Write-Warn "Serverproces is direct gestopt (exitcode $($script:ServerProcess.ExitCode))."
        Write-Warn "Controleer of poort $Port al in gebruik is: netstat -an | findstr :$Port"
    }
}

# ═══════════════════════════════════════════════════════════════════════════════
# Instructies voor handmatige teststappen
# ═══════════════════════════════════════════════════════════════════════════════
Write-Host ""
Write-Host "  ╔══════════════════════════════════════════════════════════════════════╗" -ForegroundColor Green
Write-Host "  ║  ✅  SETUP KLAAR — volg onderstaande stappen om de test te doen    ║" -ForegroundColor Green
Write-Host "  ╚══════════════════════════════════════════════════════════════════════╝" -ForegroundColor Green

Write-Host ""
Write-Host "  ┌─ STAP A  Installeer de OUDE versie ($OldVersion) ─────────────────────" -ForegroundColor Yellow
Write-Do   "Open deze map en installeer het Setup-bestand:"
Write-Host ""
Write-Host "      $ReleasesDir" -ForegroundColor White
Write-Host ""
Write-Info "Bestandsnaam: QuadroApp-$OldVersion-Setup.exe  (of QuadroApp-win-Setup.exe)"
Write-Info "Doorloop de installatie-wizard. De app wordt geïnstalleerd via Velopack."

Write-Host ""
Write-Host "  ┌─ STAP B  Start de geïnstalleerde app ─────────────────────────────" -ForegroundColor Yellow
Write-Do   "Open QuadroApp vanuit het startmenu of de installatiemap."
Write-Info "De app meldt bij elke start via $ServerUrl of er een update beschikbaar is."
Write-Info "IsInstalled = true zodra de app via het installatieprogramma is gestart."

Write-Host ""
Write-Host "  ┌─ STAP C  Wacht op de toast-melding ───────────────────────────────" -ForegroundColor Yellow
Write-Do   "Na enkele seconden verschijnt bovenaan een blauwe kaart:"
Write-Host ""
Write-Host "      ┌────────────────────────────────┐" -ForegroundColor DarkGray
Write-Host "      │  Information                   │" -ForegroundColor DarkGray
Write-Host "      │  🔄 Update gedownload.         │" -ForegroundColor DarkGray
Write-Host "      │                   Herstart nu  │" -ForegroundColor DarkGray
Write-Host "      └────────────────────────────────┘" -ForegroundColor DarkGray
Write-Host ""
Write-Do   "Klik op 'Herstart nu' — de app herstart en toont versie $NewVersion."

Write-Host ""
Write-Host "  ┌─ STAP D  Opruimen na de test ──────────────────────────────────────" -ForegroundColor Yellow
Write-Do   "Druk hier op Enter om de HTTP-server te stoppen."
Write-Do   "Verwijder daarna de testmappen en herstel App.axaml.cs:"
Write-Host ""
Write-Host "      Remove-Item -Recurse -Force `"$PublishDir`"" -ForegroundColor DarkGray
Write-Host "      Remove-Item -Recurse -Force `"$ReleasesDir`"" -ForegroundColor DarkGray
Write-Host "      git checkout App.axaml.cs" -ForegroundColor DarkGray
Write-Host ""

# ─── Wacht tot de gebruiker klaar is ─────────────────────────────────────────
Write-Host "  Druk op Enter om de HTTP-server te stoppen en af te sluiten..." -ForegroundColor Cyan
$null = Read-Host

# ─── Server stoppen ───────────────────────────────────────────────────────────
if ($script:ServerProcess -and -not $script:ServerProcess.HasExited) {
    Stop-Process -Id $script:ServerProcess.Id -Force -ErrorAction SilentlyContinue
    Write-Ok "HTTP-server gestopt (PID $($script:ServerProcess.Id))."
}

Write-Host ""
Write-Host "  Test afgesloten." -ForegroundColor Cyan
Write-Host "  Vergeet niet: git checkout App.axaml.cs" -ForegroundColor DarkGray
Write-Host ""
