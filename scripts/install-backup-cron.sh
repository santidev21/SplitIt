#!/usr/bin/env bash
# =============================================================================
# SplitIt — install the weekly database backup cron job (VPS / Linux host).
#
# Default schedule: Sunday 03:00 UTC, retaining the 4 newest backups.
# Override with BACKUP_CRON / BACKUP_LOG / RETENTION / RCLONE_REMOTE.
#
#   sudo -E RCLONE_REMOTE=b2:splitit-backups ./scripts/install-backup-cron.sh
#
# Idempotent: replaces the previous SplitIt backup entry.
# =============================================================================
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
MARKER="# splitit-db-backup"
SCHEDULE="${BACKUP_CRON:-0 3 * * 0}"
LOG_FILE="${BACKUP_LOG:-/var/log/splitit-backup.log}"

command -v crontab >/dev/null 2>&1 || {
  echo "crontab not found. Install cron (e.g. apt-get install cron) or schedule manually — see docs/BACKUPS.md." >&2
  exit 1
}

chmod +x "$SCRIPT_DIR/db-backup.sh" 2>/dev/null || true

# Pass through optional off-site config to the cron environment.
ENV_PREFIX=""
[[ -n "${RCLONE_REMOTE:-}" ]] && ENV_PREFIX="RCLONE_REMOTE=${RCLONE_REMOTE} "
[[ -n "${RETENTION:-}" ]]     && ENV_PREFIX="${ENV_PREFIX}RETENTION=${RETENTION} "

CRON_LINE="${SCHEDULE} ${ENV_PREFIX}${SCRIPT_DIR}/db-backup.sh backup >> ${LOG_FILE} 2>&1 ${MARKER}"

TMP_FILE="$(mktemp)"
trap 'rm -f "$TMP_FILE"' EXIT
crontab -l 2>/dev/null | grep -vF "$MARKER" > "$TMP_FILE" || true
printf '%s\n' "$CRON_LINE" >> "$TMP_FILE"
crontab "$TMP_FILE"

echo "Installed SplitIt backup cron entry:"
echo "  $CRON_LINE"
echo
echo "Verify with: crontab -l"
echo "Logs:        tail -f $LOG_FILE"
