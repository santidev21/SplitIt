#!/usr/bin/env bash
# =============================================================================
# SplitIt — database backup / verify / restore (Linux + Docker, e.g. the VPS)
#
# Actions:
#   db-backup.sh backup              create + verify a backup, apply retention,
#                                    optional off-site copy (default action)
#   db-backup.sh list                list backups in the backup volume
#   db-backup.sh verify <file>       run RESTORE VERIFYONLY on a .bak
#   db-backup.sh restore <file>      restore a .bak over SplitItDb (destructive)
#
# Config (env vars, all optional except DB_PASSWORD):
#   DB_PASSWORD     SA password (falls back to DB_PASSWORD= in repo .env)
#   DB_CONTAINER    default: splitit-db
#   DB_NAME         default: SplitItDb
#   BACKUP_DIR      in-container path, default: /var/opt/mssql/backups
#   RETENTION       number of newest backups to keep, default: 4
#   RCLONE_REMOTE   e.g. b2:splitit-backups ; empty = skip off-site copy
#
# See docs/BACKUPS.md. Weekly cron: scripts/install-backup-cron.sh
# =============================================================================
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "$SCRIPT_DIR/.." && pwd)"

ACTION="${1:-backup}"
BACKUP_FILE="${2:-}"

DB_CONTAINER="${DB_CONTAINER:-splitit-db}"
DB_NAME="${DB_NAME:-SplitItDb}"
BACKUP_DIR="${BACKUP_DIR:-/var/opt/mssql/backups}"
RETENTION="${RETENTION:-4}"
RCLONE_REMOTE="${RCLONE_REMOTE:-}"

log()  { printf '[%s] %s\n' "$(date -u +%Y-%m-%dT%H:%M:%SZ)" "$*"; }
fail() { printf '[%s] ERROR: %s\n' "$(date -u +%Y-%m-%dT%H:%M:%SZ)" "$*" >&2; exit 1; }

# --- Preconditions -----------------------------------------------------------
command -v docker >/dev/null 2>&1 || fail "docker not found on PATH."
docker inspect -f '{{.State.Running}}' "$DB_CONTAINER" 2>/dev/null | grep -q true \
  || fail "container '$DB_CONTAINER' is not running (start it with: npm run db:up / docker compose up -d sqlserver)."

if [[ -z "${DB_PASSWORD:-}" && -f "$REPO_ROOT/.env" ]]; then
  DB_PASSWORD="$(grep -E '^DB_PASSWORD=' "$REPO_ROOT/.env" | head -1 | cut -d'=' -f2- || true)"
fi
[[ -n "${DB_PASSWORD:-}" ]] || fail "DB_PASSWORD not set (export it or define it in $REPO_ROOT/.env)."

# sqlcmd path differs between mssql-tools and mssql-tools18 (-C = trust self-signed cert).
SQLCMD="/opt/mssql-tools18/bin/sqlcmd"; TRUST="-C"
if ! docker exec "$DB_CONTAINER" test -x "$SQLCMD" >/dev/null 2>&1; then
  SQLCMD="/opt/mssql-tools/bin/sqlcmd"; TRUST=""
fi
docker exec "$DB_CONTAINER" test -x "$SQLCMD" >/dev/null 2>&1 || fail "sqlcmd not found in container '$DB_CONTAINER'."

run_sql() {
  docker exec "$DB_CONTAINER" "$SQLCMD" -S localhost -U sa -P "$DB_PASSWORD" $TRUST -b -Q "$1"
}

case "$ACTION" in
  backup)
    run_sql "IF DB_ID(N'$DB_NAME') IS NULL THROW 51000, 'Database $DB_NAME not found', 1;" >/dev/null
    docker exec "$DB_CONTAINER" mkdir -p "$BACKUP_DIR"

    TS="$(date -u +%Y%m%d_%H%M%S)"
    FILE="$BACKUP_DIR/splitit_${DB_NAME}_${TS}.bak"
    log "Backing up [$DB_NAME] -> $FILE"
    run_sql "BACKUP DATABASE [$DB_NAME] TO DISK = N'$FILE' WITH INIT, FORMAT, COMPRESSION, CHECKSUM, NAME = N'SplitIt $TS';" >/dev/null

    log "Verifying backup (RESTORE VERIFYONLY)..."
    run_sql "RESTORE VERIFYONLY FROM DISK = N'$FILE';" >/dev/null
    log "Backup OK: $FILE"

    log "Retention: keeping the newest $RETENTION backup(s)..."
    docker exec "$DB_CONTAINER" sh -c \
      "ls -1t $BACKUP_DIR/splitit_*.bak 2>/dev/null | tail -n +$((RETENTION + 1)) | xargs -r rm -f"

    if [[ -n "$RCLONE_REMOTE" ]]; then
      command -v rclone >/dev/null 2>&1 || fail "RCLONE_REMOTE is set but rclone is not installed."
      TMP_DIR="$(mktemp -d)"
      trap 'rm -rf "$TMP_DIR"' EXIT
      docker cp "$DB_CONTAINER:$FILE" "$TMP_DIR/" >/dev/null
      rclone copy "$TMP_DIR/$(basename "$FILE")" "$RCLONE_REMOTE" --checksum --no-traverse
      log "Off-site copy uploaded to $RCLONE_REMOTE"
    else
      log "Off-site copy SKIPPED (RCLONE_REMOTE not set) — backups only live on this host. See docs/BACKUPS.md."
    fi
    ;;

  list)
    docker exec "$DB_CONTAINER" sh -c "ls -lht $BACKUP_DIR/splitit_*.bak 2>/dev/null" \
      || log "No backups found in $BACKUP_DIR."
    ;;

  verify)
    [[ -n "$BACKUP_FILE" ]] || fail "usage: db-backup.sh verify <file-in-container>"
    run_sql "RESTORE VERIFYONLY FROM DISK = N'$BACKUP_FILE';" >/dev/null
    log "Backup verified OK: $BACKUP_FILE"
    ;;

  restore)
    [[ -n "$BACKUP_FILE" ]] || fail "usage: db-backup.sh restore <file-in-container>"
    log "WARNING: restoring will overwrite [$DB_NAME]."
    run_sql "ALTER DATABASE [$DB_NAME] SET SINGLE_USER WITH ROLLBACK IMMEDIATE;
             RESTORE DATABASE [$DB_NAME] FROM DISK = N'$BACKUP_FILE' WITH REPLACE;
             ALTER DATABASE [$DB_NAME] SET MULTI_USER;"
    log "Restore OK from $BACKUP_FILE"
    ;;

  *)
    fail "Unknown action '$ACTION' (use: backup | list | verify | restore)."
    ;;
esac
