#!/bin/bash
set -euo pipefail

# SplitIt Production Deployment Script
# Usage: ./deploy.sh [pull|build|up|status|rollback|logs]

DEPLOY_DIR="/opt/splitit"
BACKUP_DIR="/opt/splitit-backup-$(date +%Y%m%d-%H%M%S)"
LOG_FILE="/var/log/splitit-deploy.log"

log() {
    echo "[$(date '+%Y-%m-%d %H:%M:%S')] $1" | tee -a "$LOG_FILE"
}

error_exit() {
    log "ERROR: $1"
    exit 1
}

# Validate docker compose configuration
validate_config() {
    log "Validating docker compose configuration..."
    cd "$DEPLOY_DIR"
    docker compose config --quiet || error_exit "docker compose config validation failed"
    log "Configuration valid."
}

# Backup current deployment
backup() {
    if [ -d "$DEPLOY_DIR" ]; then
        log "Creating backup at $BACKUP_DIR..."
        cp -r "$DEPLOY_DIR" "$BACKUP_DIR"
        log "Backup created."
    fi
}

# Pull latest code
pull() {
    log "Pulling latest code..."
    cd "$DEPLOY_DIR"
    git fetch origin main
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
    TIMEOUT=120
    INTERVAL=5
    ELAPSED=0

    while [ $ELAPSED -lt $TIMEOUT ]; do
        HEALTHY=$(docker compose ps --format json 2>/dev/null | grep -c '"healthy"' || true)

        if [ "$HEALTHY" -ge 4 ]; then
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

    # Check proxy
    PROXY_HEALTH=$(docker inspect --format='{{.State.Health.Status}}' splitit-proxy 2>/dev/null || echo "not_found")
    if [ "$PROXY_HEALTH" != "healthy" ]; then
        error_exit "proxy is not healthy (status: $PROXY_HEALTH)"
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

# Rollback to previous version
rollback() {
    LATEST_BACKUP=$(ls -dt /opt/splitit-backup-* 2>/dev/null | head -1)
    if [ -z "$LATEST_BACKUP" ]; then
        error_exit "No backup found for rollback"
    fi

    log "Rolling back to $LATEST_BACKUP..."
    cd "$DEPLOY_DIR"
    docker compose down
    rm -rf "$DEPLOY_DIR"
    cp -r "$LATEST_BACKUP" "$DEPLOY_DIR"
    cd "$DEPLOY_DIR"
    docker compose up -d --remove-orphans
    log "Rollback complete."
}

# Show logs
logs() {
    cd "$DEPLOY_DIR"
    docker compose logs --tail=100
}

# Full deployment
deploy() {
    log "=== Starting full deployment ==="
    validate_config
    backup
    pull
    build
    up
    wait_healthy
    verify
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
