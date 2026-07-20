# set-db-secret.ps1 - stores the PostgreSQL password DPAPI-encrypted in
# %LOCALAPPDATA%\QuadroApp\db.secret. Run once per PC, per Windows account
# that runs QuadroApp. The password never touches appsettings.json.
#
# Usage:  .\scripts\set-db-secret.ps1
param()

$dataDir = Join-Path $env:LOCALAPPDATA "QuadroApp"
New-Item -ItemType Directory -Force -Path $dataDir | Out-Null
$path = Join-Path $dataDir "db.secret"

$secure = Read-Host "PostgreSQL wachtwoord voor gebruiker 'quadro'" -AsSecureString
$plain = [Runtime.InteropServices.Marshal]::PtrToStringAuto(
    [Runtime.InteropServices.Marshal]::SecureStringToBSTR($secure))

Add-Type -AssemblyName System.Security
$entropy = [Text.Encoding]::UTF8.GetBytes("QuadroApp.v1")
$encrypted = [Security.Cryptography.ProtectedData]::Protect(
    [Text.Encoding]::UTF8.GetBytes($plain), $entropy,
    [Security.Cryptography.DataProtectionScope]::CurrentUser)

[IO.File]::WriteAllBytes($path, $encrypted)
Write-Host "Secret opgeslagen in $path (alleen dit Windows-account kan het lezen)." -ForegroundColor Green
Write-Host "Zet in appsettings.json: Password=__SECRET__"
