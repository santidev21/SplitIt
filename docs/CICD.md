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

### 2.1 Unified Pipeline (`.github/workflows/ci.yml`)

**Trigger:** Push to `main`, Pull requests to `main`

| Job | Description | Dependencies | Runs on PR? |
|-----|-------------|--------------|-------------|
| `backend` | .NET restore/build/test with SQL Server | None | Yes |
| `frontend` | Angular build + Karma tests with coverage | None | Yes |
| `e2e` | Playwright E2E tests | `frontend` | Yes |
| `security` | Secrets detection, .env/.dockerignore validation | `backend`, `frontend` | Yes |
| `docker-build` | Build Docker images + Trivy HIGH/CRITICAL scan | `backend`, `frontend` | Yes |
| `docker-compose-validate` | Validate compose config | None | Yes |
| `deploy` | SSH to VPS and run deploy.sh | **All above** | **No** (push to main only) |

**Key features:**
- SQL Server 2022 service container for integration tests
- Docker layer caching via GitHub Actions cache
- Artifact upload for test results and coverage reports
- Trivy fails on HIGH or CRITICAL vulnerabilities
- No secrets exposed in logs

**E2E tests:**
- **Mocked E2E** (43 tests): Run in CI against a static SPA served by `npx serve`. No real backend required. Uses `page.route` to mock API calls.
- **Full-stack E2E** (8 tests): Require a real deployed stack (Nginx, backend, SQL Server). Run manually against the VPS or local Docker deployment via `npm run e2e:fullstack`. Excluded from CI via `testIgnore` in `playwright.config.ts`.
- **Deploy is gated:** only runs after ALL CI jobs pass on push to main
- Deploy uses `environment: production` for secret access
- Emergency manual deploy: SSH to VPS and run `./scripts/deploy.sh deploy`

**Concurrency:** Only one production deployment at a time (`cancel-in-progress: false`)

**Deployment flow (when deploy job runs):**
1. All CI tests/builds/validations pass
2. SSH into VPS using production environment secrets
3. Execute `deploy.sh deploy` which:
   - Validates .env and docker compose config
   - Creates backup of current deployment
   - Backs up database (if SQL Server healthy)
   - Pulls latest code (`git fetch + reset`)
   - Builds Docker images
   - Starts containers
   - Waits for health checks (120s timeout)
   - Verifies all services healthy
   - Cleanup old backups (keep last 5)

## 3. Required GitHub Secrets

### Environment Secrets (Production)

| Secret | Description | Example |
|--------|-------------|---------|
| `VPS_SSH_PRIVATE_KEY` | SSH private key for VPS access (no passphrase) | `-----BEGIN OPENSSH PRIVATE KEY-----...` |
| `VPS_HOST` | VPS hostname or IP | `2.25.112.139` |
| `VPS_USER` | SSH username (non-root) | `santidev21` |

All production secrets (database passwords, JWT keys) remain exclusively on the VPS in `/opt/splitit/.env`. They are NEVER copied to GitHub.

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
