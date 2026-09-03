# SplitIt — Docker Containerization & Security Architecture

## Overview

SplitIt uses a hardened multi-container architecture orchestrated via Docker Compose. The services run isolated across private bridge networks to ensure the SQL Server database is never exposed to the host network or the Internet.

```
                    INTERNET
                       │
                :80 / :443
                       │
           ┌───────────▼───────────┐
           │   vps-gateway         │  (nginx, TLS, security headers, rate limiting)
           │   [separate repo]     │  sites-enabled/splitit.santidev21.tech.conf
           └───────────┬───────────┘
                       │ (splitit-frontend-net, external)
                       ▼
             ┌───────────────────┐
             │  splitit-frontend │ (Angular SPA, port 80 internal)
             └─────────┬─────────┘
                       │ (splitit-frontend-net)
                       ▼
             ┌───────────────────┐
             │  splitit-backend  │ (.NET 8 Web API, port 8080 internal)
             └─────────┬─────────┘
                       │ (splitit-backend-net: internal=true)
                       ▼
             ┌───────────────────┐
             │    splitit-db     │ (SQL Server 2022)
             └───────────────────┘
  Startup order: sqlserver (healthy) → db-init (create users) → migrator (EF Migrate) → backend → frontend
```

---

## Network Architecture & Isolation

To support hosting multiple projects cleanly on the same VPS, networks are explicitly named:

1. **`splitit-frontend-net`** (external):
   - Bridge network connecting the VPS gateway to SplitIt's frontend and backend containers.
   - Declared as `external: true` — the network is **owned by the `vps-gateway`** compose stack.
   - SplitIt attaches to it so the gateway can route traffic to its containers.
2. **`splitit-backend-net`** (`internal: true`):
   - Private, isolated internal network connecting `.NET 8 Web API` to `SQL Server`.
   - Outbound and inbound external traffic is blocked by Docker daemon. SQL Server port `1433` is **not** exposed to the host machine.

---

## Container Security Controls

| Security Control | Implementation | Purpose |
| :--- | :--- | :--- |
| **Multi-Stage Builds** | `node:20-alpine` & `dotnet/sdk:8.0` build stages → `nginx:1.27-alpine` & `aspnet:8.0-alpine` runtimes | Reduces runtime image size and removes compilers/SDK attack surface |
| **Non-Root Execution** | Backend: `USER $APP_UID` (UID 1654)<br>Frontend: `USER nginx`<br>SQL Server: `user: "0:0"` root **exception** — image defaults to `mssql` but fails on Windows volume (see below) | Prevents breakout where possible; SQL Server mitigated via network isolation |
| **Health Checks** | `GET /health/live` (liveness, no DB) vs `GET /health/ready` (readiness, checks DbContext) + `sqlcmd` for DB | Enables automatic restart only on real liveness failure; readiness reflects DB availability |
| **Resource Limits** | Restricted CPU and RAM quotas per container (VPS 2 vCPU/4-8GB) | Prevents DoS and VPS memory exhaustion |
| **Data Persistence** | Volumes `splitit_sqlserver_data` + `splitit_dataprotection_keys` | Protects DB and DataProtection keys across `docker compose down/up` |

### SQL Server Non-Root Exception — Documented
`mcr.microsoft.com/mssql/server:2022-latest` image history shows `USER mssql` (non-root, UID 10001) by default. However, verification on Docker Desktop Windows 29.7.2 with named volume `splitit_sqlserver_data:/var/opt/mssql/data` fails when running as `mssql`:
```
ERROR: BootstrapSystemDataDirectories() 0x80070005 Access is denied
```
**Decision: keep `user: "0:0"` (root) for `sqlserver` service** — documented exception, stability preferred over artificial non-root.
**Mitigations compensating for root:** `internal:true` network (`splitit-backend-net`), no host `1433` exposure, `privileged:false`, resource limits (`1.5 CPU/2GB`), dedicated least-privilege `splitit_app` for API, `db-init` one-shot creates app user and never uses `sa` at runtime.

### Database Least-Privilege
| Principal | Purpose | Permissions | Used By |
| :--- | :--- | :--- | :--- |
| `sa` (`${DB_PASSWORD}`) | Bootstrap only | `sysadmin` — **never in API** | `db-init` only |
| `splitit_migrator` | EF Core migrations (one-shot) | `db_datareader` + `db_datawriter` + `db_ddladmin` — **not `db_owner`** | `migrator` service |
| `splitit_app` | API runtime | `db_datareader` + `db_datawriter` **only (no DDL, no owner)** | `backend` |

### DataProtection Persistence
ASP.NET Core DataProtection keys stored at `/home/app/.aspnet/DataProtection-Keys` via `PersistKeysToFileSystem` (`Program.cs`). Mounted as volume `splitit_dataprotection_keys` so keys survive `docker compose down/up` without regeneration.

---

## Environment Configuration

Copy `.env.example` to `.env` before running Docker Compose:

```bash
cp .env.example .env
```

| Variable | Description | Example |
| :--- | :--- | :--- |
| `DB_PASSWORD` | Strong SA password — **init only**, not used by API | `SecretPass12345!Secure` |
| `DB_APP_USER` | App DB login (default `splitit_app`) — no DDL | `splitit_app` |
| `DB_APP_PASSWORD` | App password, must differ from SA/Migrator in prod | `SecretAppPass12345!Secure` |
| `DB_MIGRATOR_USER` | Migrator login (default `splitit_migrator`) — with DDL | `splitit_migrator` |
| `DB_MIGRATOR_PASSWORD` | Migrator password, must differ from SA/App in prod | `SecretMigrPass12345!Secure` |
| `JWT_SECRET` | 64+ char random string for JWT signing | `RandomBase64Key...` |
| `JWT_ISSUER` | Valid token issuer domain | `https://splitit.example.com` |
| `JWT_AUDIENCE` | Valid token audience domain | `https://splitit.example.com` |
| `CORS_ALLOWED_ORIGINS` | Permitted frontend origins | `https://splitit.example.com` |
| `GOOGLE_CLIENT_ID` | Google OAuth client ID | `xxx.apps.googleusercontent.com` |

---

## Local Development

```bash
docker compose -f docker-compose.yml -f docker-compose.local.yml up --build
```

This runs SplitIt locally with:
- Local bridge networks (no external dependencies)
- Debug ports published to `localhost`
- sqlserver `user: "0:0"` for Docker Desktop Windows

## Production Deploy

Automatic on push to `main` via GitHub Actions. Manual:

```bash
cd /opt/splitit
./scripts/deploy.sh deploy
```

---

## Operations & Commands

### 1. Build and Start Containers
```bash
docker compose up -d --build
```

### 2. Verify Container Health
```bash
docker compose ps
```

### 3. View Logs
```bash
docker compose logs -f
docker compose logs -f backend
```

### 4. Stop Services
```bash
docker compose down
```

### 5. Verify Database Isolation
```bash
docker ps
```
Ensure `splitit-db` shows **NO** published ports under the `PORTS` column.
