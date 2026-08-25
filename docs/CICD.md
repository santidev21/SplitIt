# SplitIt — CI/CD Documentation

> **Phase:** 13 — CI/CD
> **Date:** 2026-08-25
> **Status:** Production-ready

---

## 1. Overview

SplitIt uses GitHub Actions for continuous integration and deployment. The pipeline validates code quality, runs tests, scans for vulnerabilities, and deploys to production via SSH.

```
┌─────────────────────────────────────────────────────────────┐
│                      GitHub Actions                         │
│                                                             │
│  ┌─────────┐  ┌─────────┐  ┌─────────┐  ┌─────────────┐  │
│  │ Backend │  │Frontend │  │  E2E    │  │  Security   │  │
│  │ Tests   │  │ Build   │  │ Playwright│  │  Scan       │  │
│  └────┬────┘  └────┬────┘  └────┬────┘  └──────┬──────┘  │
│       │            │            │               │          │
│       └────────────┴────────────┴───────────────┘          │
│                          │                                  │
│                    ┌─────┴─────┐                           │
│                    │   Docker  │                           │
│                    │   Build   │                           │
│                    │  + Trivy  │                           │
│                    └─────┬─────┘                           │
│                          │                                  │
│                    ┌─────┴─────┐                           │
│                    │  Compose  │                           │
│                    │  Validate │                           │
│                    └─────┬─────┘                           │
└──────────────────────────┼──────────────────────────────────┘
                           │
                    ┌──────┴──────┐
                    │   Deploy    │
                    │   to VPS    │
                    └─────────────┘
```

## 2. Workflows

### 2.1 CI Workflow (`.github/workflows/ci.yml`)

**Trigger:** Push to `main`, Pull requests to `main`

| Job | Description | Dependencies |
|-----|-------------|--------------|
| `backend` | .NET restore/build/test with SQL Server service container | None |
| `frontend` | Angular build + Karma unit tests with coverage | None |
| `e2e` | Playwright E2E tests | `frontend` |
| `security` | Secrets detection, .env/.dockerignore validation | `backend`, `frontend` |
| `docker-build` | Build Docker images + Trivy HIGH/CRITICAL scan | `backend`, `frontend` |
| `docker-compose-validate` | Validate compose config (default, letsencrypt, certbot) | None |

**Key features:**
- SQL Server 2022 service container for integration tests
- Docker layer caching via GitHub Actions cache
- Artifact upload for test results and coverage reports
- Trivy fails on HIGH or CRITICAL vulnerabilities
- No secrets exposed in logs

### 2.2 Deploy Workflow (`.github/workflows/deploy.yml`)

**Trigger:** Push to `main` (after CI passes), Manual dispatch

| Input | Description | Default |
|-------|-------------|---------|
| `skip_tests` | Skip tests (emergency deploy only) | `false` |

**Deployment flow:**
1. Run tests (unless skipped)
2. SSH into VPS
3. Create backup of current deployment
4. Pull latest code
5. Validate docker compose config
6. Build and start containers
7. Wait for health checks (120s timeout)
8. Verify all services healthy
9. Cleanup old backups (keep last 3)

**Concurrency:** Only one production deployment at a time (`cancel-in-progress: false`)

## 3. Required GitHub Secrets

### Repository Secrets

| Secret | Description | Example |
|--------|-------------|---------|
| `VPS_SSH_PRIVATE_KEY` | SSH private key for VPS access | `-----BEGIN OPENSSH PRIVATE KEY-----...` |
| `VPS_HOST` | VPS hostname or IP | `splitit.yourdomain.com` |
| `VPS_USER` | SSH username (non-root) | `deploy` |

### Environment Secrets (Production)

| Secret | Description |
|--------|-------------|
| (None additional) | All production secrets remain on VPS |

**Security rules:**
- Never commit `.env` to Git
- Never copy production secrets to GitHub
- GitHub secrets contain only deployment credentials
- SSH key authentication only (no password auth)

## 4. VPS Configuration

### 4.1 SSH Setup

```bash
# On VPS: Create deploy user
sudo useradd -m -s /bin/bash deploy
sudo usermod -aG docker deploy

# On VPS: Configure SSH
sudo mkdir -p /home/deploy/.ssh
sudo touch /home/deploy/.ssh/authorized_keys
sudo chmod 700 /home/deploy/.ssh
sudo chmod 600 /home/deploy/.ssh/authorized_keys
sudo chown -R deploy:deploy /home/deploy/.ssh

# Add GitHub Actions public key
echo "ssh-ed25519 AAAA..." | sudo tee -a /home/deploy/.ssh/authorized_keys

# Disable password authentication
sudo sed -i 's/PasswordAuthentication yes/PasswordAuthentication no/' /etc/ssh/sshd_config
sudo systemctl restart sshd
```

### 4.2 Deployment Directory

```bash
# On VPS: Create deployment directory
sudo mkdir -p /opt/splitit
sudo chown deploy:deploy /opt/splitit

# Clone repository (first time only)
cd /opt/splitit
git clone https://github.com/your-org/split-it.git .

# Create production .env (NEVER commit this)
sudo nano /opt/splitit/.env
```

### 4.3 Production .env

The `.env` file on the VPS must contain:

```bash
# Domain / TLS
DOMAIN=splitit.yourdomain.com
TLS_MODE=letsencrypt
ACME_EMAIL=admin@yourdomain.com
PROXY_HTTP_PORT=80
PROXY_HTTPS_PORT=443

# SQL Server
DB_PASSWORD=<strong-sa-password>
DB_APP_USER=splitit_app
DB_APP_PASSWORD=<strong-app-password>
DB_MIGRATOR_USER=splitit_migrator
DB_MIGRATOR_PASSWORD=<strong-migrator-password>

# JWT
JWT_SECRET=<minimum-64-characters-random-secret>
JWT_ISSUER=https://splitit.yourdomain.com
JWT_AUDIENCE=https://splitit.yourdomain.com

# CORS
CORS_ALLOWED_ORIGINS=https://splitit.yourdomain.com
```

### 4.4 Firewall

```bash
# Allow only HTTP/HTTPS
sudo ufw allow 80/tcp
sudo ufw allow 443/tcp
sudo ufw allow 22/tcp  # SSH (restrict to known IPs if possible)
sudo ufw enable
```

## 5. Deployment Script

The `scripts/deploy.sh` script provides manual deployment control:

```bash
# Full deployment
./scripts/deploy.sh deploy

# Individual steps
./scripts/deploy.sh pull      # Pull latest code
./scripts/deploy.sh build     # Build Docker images
./scripts/deploy.sh up        # Start containers
./scripts/deploy.sh status    # Show container status
./scripts/deploy.sh logs      # Show container logs
./scripts/deploy.sh verify    # Verify all services healthy
./scripts/deploy.sh rollback  # Rollback to previous backup
```

## 6. Rollback Procedure

### Automatic Rollback (Deploy Script)

```bash
./scripts/deploy.sh rollback
```

This will:
1. Find the most recent backup in `/opt/splitit-backup-*`
2. Stop current containers
3. Replace deployment with backup
4. Start containers from backup

### Manual Rollback

```bash
cd /opt/splitit

# Stop current deployment
docker compose down

# Find backup
ls -dt /opt/splitit-backup-*

# Restore from backup
rm -rf /opt/splitit
cp -r /opt/splitit-backup-20260825-120000 /opt/splitit
cd /opt/splitit

# Start backup
docker compose up -d

# Verify
docker compose ps
```

### Git Rollback

```bash
cd /opt/splitit

# View recent commits
git log --oneline -10

# Reset to specific commit
git reset --hard <commit-hash>

# Rebuild and restart
docker compose build --no-cache
docker compose up -d
```

## 7. Failure Behavior

| Failure | Behavior | Recovery |
|---------|----------|----------|
| CI tests fail | Pipeline stops, no deploy | Fix tests, push again |
| Trivy HIGH/CRITICAL | Pipeline stops, no deploy | Update base images, rebuild |
| Docker compose invalid | Pipeline stops, no deploy | Fix compose file |
| SSH connection fails | Deploy job fails | Check VPS status, SSH config |
| Health check timeout | Deploy fails, rollback | Check container logs |
| Service unhealthy | Deploy fails, rollback | Fix service, redeploy |
| db-init fails | Deploy fails | Check DB credentials |
| Migrator fails | Deploy fails | Check migration scripts |

## 8. Security Considerations

### What is protected:
- No secrets in Git (`.env` excluded via `.gitignore` and `.dockerignore`)
- No secrets in GitHub Actions logs (`set +x` used where needed)
- SSH key authentication only (no password auth)
- Non-root deployment user on VPS
- Trivy scan blocks HIGH/CRITICAL vulnerabilities
- Secrets detection in CI prevents accidental commits

### What is NOT in scope:
- SQL Server port not exposed (internal network only)
- Backend port not exposed (only proxy is public)
- No `.env` committed to repository
- No production secrets in GitHub

### Audit trail:
- All deployments logged to `/var/log/splitit-deploy.log`
- GitHub Actions provides deployment history
- Docker container logs available via `docker compose logs`

## 9. Monitoring (Future)

CI/CD does not include monitoring. Future phases may add:
- Health check endpoints (already exist)
- Log aggregation
- Alerting on deployment failures
- Performance monitoring

## 10. Files

```
.github/workflows/
├── ci.yml                    # CI pipeline
└── deploy.yml                # Production deployment
scripts/
└── deploy.sh                 # VPS deployment script
docs/
├── CICD.md                   # This file
└── PHASE_13_REPORT.md        # Phase report
```
