<#
.SYNOPSIS
  SplitIt — database backup / verify / restore (Windows + Docker Desktop).

.DESCRIPTION
  Equivalent of scripts/db-backup.sh for native Windows development.
  Prefer the root scripts: `npm run db:backup`, `npm run db:backup:list`, `npm run db:restore`.

.PARAMETER Action
  backup (default) | list | verify | restore

.PARAMETER File
  In-container .bak path, required for verify/restore.

.EXAMPLE
  powershell -File scripts/db-backup.ps1 -Action backup
  powershell -File scripts/db-backup.ps1 -Action list
  powershell -File scripts/db-backup.ps1 -Action restore -File /var/opt/mssql/backups/splitit_SplitItDb_20260917_030000.bak

.NOTES
  Env vars: DB_PASSWORD (or .env), DB_CONTAINER, DB_NAME, BACKUP_DIR, RETENTION, RCLONE_REMOTE.
  See docs/BACKUPS.md.
#>
param(
  [ValidateSet('backup', 'list', 'verify', 'restore')]
  [string]$Action = 'backup',
  [string]$File
)

$ErrorActionPreference = 'Stop'

$repoRoot    = Split-Path -Parent $PSScriptRoot
$dbContainer = if ($env:DB_CONTAINER) { $env:DB_CONTAINER } else { 'splitit-db' }
$dbName      = if ($env:DB_NAME) { $env:DB_NAME } else { 'SplitItDb' }
$backupDir   = if ($env:BACKUP_DIR) { $env:BACKUP_DIR } else { '/var/opt/mssql/backups' }
$retention   = if ($env:RETENTION) { [int]$env:RETENTION } else { 4 }
$rcloneRemote = $env:RCLONE_REMOTE

function Write-Log { param([string]$Message) Write-Host ("[{0}] {1}" -f [DateTime]::UtcNow.ToString('yyyy-MM-ddTHH:mm:ssZ'), $Message) }
function Fail { param([string]$Message) Write-Error ("ERROR: " + $Message); exit 1 }

if (-not (Get-Command docker -ErrorAction SilentlyContinue)) { Fail 'docker not found on PATH.' }

$running = (& docker inspect -f '{{.State.Running}}' $dbContainer 2>$null)
if ($LASTEXITCODE -ne 0 -or $running -ne 'true') {
  Fail "container '$dbContainer' is not running (start it with: npm run db:up)."
}

$dbPassword = $env:DB_PASSWORD
if ([string]::IsNullOrWhiteSpace($dbPassword)) {
  $envFile = Join-Path $repoRoot '.env'
  if (Test-Path $envFile) {
    $line = Get-Content $envFile | Where-Object { $_ -match '^DB_PASSWORD=' } | Select-Object -First 1
    if ($line) { $dbPassword = $line.Substring($line.IndexOf('=') + 1) }
  }
}
if ([string]::IsNullOrWhiteSpace($dbPassword)) { Fail 'DB_PASSWORD not set (export it or define it in .env).' }

$sqlcmd = '/opt/mssql-tools18/bin/sqlcmd'
$trust = '-C'
& docker exec $dbContainer test -x $sqlcmd *> $null
if ($LASTEXITCODE -ne 0) { $sqlcmd = '/opt/mssql-tools/bin/sqlcmd'; $trust = '' }
& docker exec $dbContainer test -x $sqlcmd *> $null
if ($LASTEXITCODE -ne 0) { Fail "sqlcmd not found in container '$dbContainer'." }

function Invoke-Sql {
  param([string]$Query)
  $argv = @('exec', $dbContainer, $sqlcmd, '-S', 'localhost', '-U', 'sa', '-P', $dbPassword)
  if ($trust) { $argv += $trust }
  $argv += @('-b', '-Q', $Query)
  & docker @argv
  if ($LASTEXITCODE -ne 0) { Fail "sqlcmd failed (exit $LASTEXITCODE)." }
}

function Invoke-ContainerShell {
  param([string]$Command)
  & docker exec $dbContainer sh -c $Command
}

switch ($Action) {
  'backup' {
    Invoke-Sql "IF DB_ID(N'$dbName') IS NULL THROW 51000, 'Database $dbName not found', 1;"
    Invoke-ContainerShell "mkdir -p $backupDir" | Out-Null

    $ts   = [DateTime]::UtcNow.ToString('yyyyMMdd_HHmmss')
    $path = "$backupDir/splitit_${dbName}_${ts}.bak"
    Write-Log "Backing up [$dbName] -> $path"
    Invoke-Sql "BACKUP DATABASE [$dbName] TO DISK = N'$path' WITH INIT, FORMAT, COMPRESSION, CHECKSUM, NAME = N'SplitIt $ts';"

    Write-Log 'Verifying backup (RESTORE VERIFYONLY)...'
    Invoke-Sql "RESTORE VERIFYONLY FROM DISK = N'$path';"
    Write-Log "Backup OK: $path"

    Write-Log "Retention: keeping the newest $retention backup(s)..."
    Invoke-ContainerShell "ls -1t $backupDir/splitit_*.bak 2>/dev/null | tail -n +$($retention + 1) | xargs -r rm -f"

    if (-not [string]::IsNullOrWhiteSpace($rcloneRemote)) {
      if (-not (Get-Command rclone -ErrorAction SilentlyContinue)) { Fail 'RCLONE_REMOTE is set but rclone is not installed.' }
      $tmp = Join-Path $env:TEMP ("splitit-backup-" + [Guid]::NewGuid().ToString('N'))
      New-Item -ItemType Directory -Path $tmp | Out-Null
      try {
        & docker cp "${dbContainer}:$path" $tmp | Out-Null
        $localFile = Join-Path $tmp (Split-Path -Leaf $path)
        & rclone copy $localFile $rcloneRemote --checksum --no-traverse
        if ($LASTEXITCODE -ne 0) { Fail 'rclone copy failed.' }
        Write-Log "Off-site copy uploaded to $rcloneRemote"
      }
      finally { Remove-Item -Recurse -Force $tmp -ErrorAction SilentlyContinue }
    }
    else {
      Write-Log 'Off-site copy SKIPPED (RCLONE_REMOTE not set) — backups only live on this host. See docs/BACKUPS.md.'
    }
  }

  'list' {
    Invoke-ContainerShell "ls -lht $backupDir/splitit_*.bak 2>/dev/null"
  }

  'verify' {
    if ([string]::IsNullOrWhiteSpace($File)) { Fail 'usage: -Action verify -File <file-in-container>' }
    Invoke-Sql "RESTORE VERIFYONLY FROM DISK = N'$File';"
    Write-Log "Backup verified OK: $File"
  }

  'restore' {
    if ([string]::IsNullOrWhiteSpace($File)) { Fail 'usage: -Action restore -File <file-in-container>' }
    Write-Log "WARNING: restoring will overwrite [$dbName]."
    Invoke-Sql "ALTER DATABASE [$dbName] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; RESTORE DATABASE [$dbName] FROM DISK = N'$File' WITH REPLACE; ALTER DATABASE [$dbName] SET MULTI_USER;"
    Write-Log "Restore OK from $File"
  }
}
