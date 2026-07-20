# backup-postgres.ps1 — US-34: daily PostgreSQL backup via pg_dump.
# Schedule on PC 1 with Windows Task Scheduler (daily, e.g. 12:30):
#   powershell -File "C:\...\Scripts\backup-postgres.ps1" -BackupDir "D:\QuadroBackups"
#
# Password comes from the QUADRO_DB_PASSWORD environment variable or the
# standard %APPDATA%\postgresql\pgpass.conf — never hardcode it here.
param(
    [string]$BackupDir = "$env:LOCALAPPDATA\QuadroApp\Backups",
    [string]$DbHost = "localhost",
    [string]$DbName = "quadrodb",
    [string]$DbUser = "quadro",
    [int]$RetentionDays = 30
)

$ErrorActionPreference = "Stop"
New-Item -ItemType Directory -Force -Path $BackupDir | Out-Null

if ($env:QUADRO_DB_PASSWORD) { $env:PGPASSWORD = $env:QUADRO_DB_PASSWORD }

$stamp = Get-Date -Format "yyyyMMdd"
$target = Join-Path $BackupDir "quadrodb-backup-$stamp.dump"

& pg_dump -h $DbHost -U $DbUser -d $DbName -F c -f $target
if ($LASTEXITCODE -ne 0) { Write-Host "pg_dump FAILED" -ForegroundColor Red; exit 1 }
Write-Host "Backup: $target" -ForegroundColor Green

# Retention
Get-ChildItem $BackupDir -Filter "quadrodb-backup-*.dump" |
    Where-Object { $_.LastWriteTime -lt (Get-Date).AddDays(-$RetentionDays) } |
    Remove-Item -Force
