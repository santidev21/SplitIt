#!/bin/bash
set -euo pipefail

# SplitIt Production Deployment Script
# Usage: ./deploy.sh [pull|build|up|deploy|status|rollback|logs|verify]

DEPLOY_DIR="/opt/splitit"
BACKUP_DIR="/opt/splitit-backup-$(date +%Y%m%d-%H%M%S)"
LOG_FILE="/tmp/splitit-deploy.log"
MAX_BACKUPS=5

log() {
    echo "[$(date '+%Y-%m-%d %H:%M:%S')] $1" | tee -a "$LOG_FILE"
}

error_exit() {
    log "ERROR: $1"
    exit 1
}

# --- FIX 3: Validate .env exists before any deployment action ---
validate_env() {
    log "Validating .env file..."
    if [ ! -f "$DEPLOY_DIR/.env" ]; then
        error_exit ".env file not found at $DEPLOY_DIR/.env. Copy .env.example to .env and configure production secrets."
    fi
    # Check that critical variables are not placeholder values
    if grep -q "CHANGE_ME" "$DEPLOY_DIR/.env" 2>/dev/null; then
        error_exit ".env contains CHANGE_ME placeholder values. Configure real production secrets."
    fi
    log ".env validated."
}

# Validate docker compose configuration
validate_config() {
    log "Validating docker compose configuration..."
    cd "$DEPLOY_DIR"
    docker compose config --quiet || error_exit "docker compose config validation failed"
    log "Configuration valid."
}

# --- FIX 6: Protect VPS working tree ---
validate_clean_worktree() {
    log "Checking VPS working tree..."
    cd "$DEPLOY_DIR"
    # Check for uncommitted content changes (file mode changes from chmod are ignored)
    LINES_CHANGED=$(git diff --numstat HEAD 2>/dev/null | awk '{sum += $1 + $2} END {print sum+0}')
    if [ "$LINES_CHANGED" -gt 0 ]; then
        error_exit "VPS has uncommitted changes in $DEPLOY_DIR. Commit or discard them before deploying."
    fi
    # Check for untracked files that aren't in .gitignore
    UNTRACKED=$(git ls-files --others --exclude-standard 2>/dev/null | head -5)
    if [ -n "$UNTRACKED" ]; then
        log "WARNING: Untracked files found (may be normal for .env):"
        echo "$UNTRACKED" | while read -r f; do log "  $f"; done
    fi
    # Ensure .env is not tracked by git
    if git ls-files --error-unmatch .env 2>/dev/null; then
        error_exit ".env is tracked by git on VPS. Run: git rm --cached .env"
    fi
    log "Working tree validated."
}

# Validate Docker is available
validate_docker() {
    log "Checking Docker..."
    docker info >/dev/null 2>&1 || error_exit "Docker is not running or not accessible"
    log "Docker available."
}

# --- FIX 7: Backup with restrictive permissions ---
backup() {
    if [ -d "$DEPLOY_DIR" ]; then
        log "Creating backup at $BACKUP_DIR..."
        cp -r "$DEPLOY_DIR" "$BACKUP_DIR"
        chmod 700 "$BACKUP_DIR"
        log "Backup created with permissions 700."
        # Cleanup old backups, keep MAX_BACKUPS
        BACKUP_COUNT=$(ls -dt /opt/splitit-backup-* 2>/dev/null | wc -l)
        if [ "$BACKUP_COUNT" -gt "$MAX_BACKUPS" ]; then
            log "Rotating backups (keeping last $MAX_BACKUPS)..."
            ls -dt /opt/splitit-backup-* 2>/dev/null | tail -n +$((MAX_BACKUPS + 1)) | xargs rm -rf 2>/dev/null || true
        fi
    fi
}

# --- FIX 9: Database backup before migration ---
backup_database() {
    log "Attempting database backup before migration..."
    # Check if sqlserver container is running and healthy
    DB_STATUS=$(docker inspect --format='{{.State.Health.Status}}' splitit-db 2>/dev/null || echo "not_found")
    if [ "$DB_STATUS" != "healthy" ]; then
        log "WARNING: SQL Server not healthy (status: $DB_STATUS). Skipping database backup."
        return 0
    fi

    # Extract credentials from .env (without logging them)
    local SA_PASSWORD
    SA_PASSWORD=$(grep -E "^DB_PASSWORD=" "$DEPLOY_DIR/.env" | cut -d'=' -f2-)
    if [ -z "$SA_PASSWORD" ]; then
        log "WARNING: DB_PASSWORD not found in .env. Skipping database backup."
        return 0
    fi

    # Create backup inside the sqlserver container
    local BACKUP_PATH="/var/opt/mssql/data/backup_pre_deploy_$(date +%Y%m%d_%H%M%S).bak"
    if docker exec splitit-db /opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -P "$SA_PASSWORD" -C -Q \
        "BACKUP DATABASE [SplitItDb] TO DISK = N'$BACKUP_PATH' WITH INIT, FORMAT, COMPRESSION" \
        >/dev/null 2>&1; then
        log "Database backup created: $BACKUP_PATH"
    else
        # Try alternate tools path
        if docker exec splitit-db /opt/mssql-tools/bin/sqlcmd -S localhost -U sa -P "$SA_PASSWORD" -Q \
            "BACKUP DATABASE [SplitItDb] TO DISK = N'$BACKUP_PATH' WITH INIT, FORMAT, COMPRESSION" \
            >/dev/null 2>&1; then
            log "Database backup created: $BACKUP_PATH"
        else
            log "WARNING: Database backup failed. Continuing deployment (migration is idempotent)."
        fi
    fi
    # Clean old backups inside container (keep last 5)
    docker exec splitit-db sh -c "ls -t /var/opt/mssql/data/backup_pre_deploy_*.bak 2>/dev/null | tail -n +6 | xargs rm -f 2>/dev/null" || true
}

# Pull latest code
pull() {
    log "Pulling latest code..."
    cd "$DEPLOY_DIR"
    git fetch origin main
    # Check if there are local changes before resetting
    if ! git diff --quiet HEAD origin/main 2>/dev/null; then
        log "Local branch differs from origin/main. Resetting..."
    fi
    git reset --hard origin/main
    log "Code updated."
}

# Build containers
build() {
    log "Building containers..."
    cd "$DEPLOY_DIR"
    docker compose build --no-cache
    log "Build complete."
}

# Start/update containers
up() {
    log "Starting containers..."
    cd "$DEPLOY_DIR"
    docker compose up -d --remove-orphans
    log "Containers started."
}

# Wait for health checks
wait_healthy() {
    log "Waiting for services to become healthy..."
    local TIMEOUT=120
    local INTERVAL=5
    local ELAPSED=0

    while [ $ELAPSED -lt $TIMEOUT ]; do
        HEALTHY=$(docker compose ps --format json 2>/dev/null | grep -c '"healthy"' || true)

        if [ "$HEALTHY" -ge 3 ]; then
            log "All services healthy ($HEALTHY services)"
            break
        fi

        log "Waiting... ($ELAPSED/$TIMEOUT seconds) - $HEALTHY services healthy"
        sleep $INTERVAL
        ELAPSED=$((ELAPSED + INTERVAL))
    done

    if [ $ELAPSED -ge $TIMEOUT ]; then
        log "WARNING: Timeout waiting for health checks"
        docker compose ps
        docker compose logs --tail=50
        error_exit "Health check timeout"
    fi
}

# Verify deployment
verify() {
    log "Verifying deployment..."

    # Check db-init
    DB_INIT_STATUS=$(docker inspect --format='{{.State.Status}}' splitit-db-init 2>/dev/null || echo "not_found")
    if [ "$DB_INIT_STATUS" != "exited" ]; then
        error_exit "db-init did not complete (status: $DB_INIT_STATUS)"
    fi

    # Check migrator
    MIGRATOR_STATUS=$(docker inspect --format='{{.State.Status}}' splitit-migrator 2>/dev/null || echo "not_found")
    if [ "$MIGRATOR_STATUS" != "exited" ]; then
        error_exit "migrator did not complete (status: $MIGRATOR_STATUS)"
    fi

    # Check backend
    BACKEND_HEALTH=$(docker inspect --format='{{.State.Health.Status}}' splitit-backend 2>/dev/null || echo "not_found")
    if [ "$BACKEND_HEALTH" != "healthy" ]; then
        error_exit "backend is not healthy (status: $BACKEND_HEALTH)"
    fi

    # Check frontend
    FRONTEND_HEALTH=$(docker inspect --format='{{.State.Health.Status}}' splitit-frontend 2>/dev/null || echo "not_found")
    if [ "$FRONTEND_HEALTH" != "healthy" ]; then
        error_exit "frontend is not healthy (status: $FRONTEND_HEALTH)"
    fi

    log "All services verified healthy."
}

# Show deployment status
status() {
    cd "$DEPLOY_DIR"
    docker compose ps
    echo ""
    echo "Recent logs:"
    docker compose logs --tail=10
}

# --- FIX 4: Safe rollback with validation ---
rollback() {
    log "=== Starting rollback ==="

    LATEST_BACKUP=$(ls -dt /opt/splitit-backup-* 2>/dev/null | head -1)
    if [ -z "$LATEST_BACKUP" ]; then
        error_exit "No backup found for rollback"
    fi

    log "Rolling back to $LATEST_BACKUP..."

    # Validate backup contains required files
    for REQUIRED_FILE in "docker-compose.yml" ".env"; do
        if [ ! -f "$LATEST_BACKUP/$REQUIRED_FILE" ]; then
            error_exit "Backup validation failed: missing $REQUIRED_FILE in $LATEST_BACKUP"
        fi
    done

    # Stop current containers (preserves volumes)
    cd "$DEPLOY_DIR"
    docker compose down || true

    # Atomic swap: move current to .broken, copy backup, then remove .broken
    if [ -d "${DEPLOY_DIR}.broken" ]; then
        rm -rf "${DEPLOY_DIR}.broken"
    fi
    mv "$DEPLOY_DIR" "${DEPLOY_DIR}.broken"
    cp -r "$LATEST_BACKUP" "$DEPLOY_DIR"
    chmod 700 "$DEPLOY_DIR"

    cd "$DEPLOY_DIR"
    if ! docker compose up -d --remove-orphans; then
        log "CRITICAL: Rollback containers failed to start. Attempting to restore from .broken..."
        rm -rf "$DEPLOY_DIR"
        mv "${DEPLOY_DIR}.broken" "$DEPLOY_DIR"
        cd "$DEPLOY_DIR"
        docker compose up -d --remove-orphans || error_exit "ROLLBACK FAILED: Both new and old deployments are down"
        error_exit "Rollback failed, but previous deployment restored"
    fi

    # Verify rollback health
    if wait_healthy && verify; then
        log "=== Rollback successful ==="
        rm -rf "${DEPLOY_DIR}.broken"
    else
        log "CRITICAL: Rollback health check failed. Previous deployment may still be available."
        error_exit "Rollback verification failed"
    fi
}

# Show logs
logs() {
    cd "$DEPLOY_DIR"
    docker compose logs --tail=100
}

# --- FIX 8: Deploy with automatic rollback on failure ---
deploy() {
    log "=== Starting full deployment ==="
    validate_docker
    validate_env
    validate_config
    validate_clean_worktree
    backup
    backup_database
    pull
    build
    up

    # Health check with automatic rollback on failure
    if ! wait_healthy; then
        log "Deployment health check failed. Attempting automatic rollback..."
        if rollback; then
            error_exit "Deployment failed and automatic rollback succeeded. Previous deployment restored."
        else
            error_exit "CRITICAL: Deployment failed AND automatic rollback failed. Manual intervention required."
        fi
    fi

    if ! verify; then
        log "Deployment verification failed. Attempting automatic rollback..."
        if rollback; then
            error_exit "Deployment verification failed and automatic rollback succeeded."
        else
            error_exit "CRITICAL: Verification failed AND automatic rollback failed. Manual intervention required."
        fi
    fi

    log "=== Deployment successful ==="
    status
}

# Main
case "${1:-deploy}" in
    pull) pull ;;
    build) build ;;
    up) up ;;
    deploy) deploy ;;
    status) status ;;
    rollback) rollback ;;
    logs) logs ;;
    verify) verify ;;
    *)
        echo "Usage: $0 [pull|build|up|deploy|status|rollback|logs|verify]"
        exit 1
        ;;
esac
