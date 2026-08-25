# SplitIt — Docker Containerization & Security Architecture

## Overview

SplitIt uses a hardened multi-container architecture orchestrated via Docker Compose. The services run isolated across private bridge networks to ensure the SQL Server database is never exposed to the host network or the Internet.

```
                  INTERNET
                     │
                     ▼ HTTP / HTTPS (:80 / :443)
           ┌───────────────────┐
           │ splitit-frontend  │ (Nginx + Angular SPA)
           └─────────┬─────────┘
                     │ (splitit-frontend-net)
                     ▼
           ┌───────────────────┐
           │  splitit-backend  │ (.NET 8 Web API)
           └─────────┬─────────┘
                     │ (splitit-backend-net: internal=true)
                     ▼
           ┌───────────────────┐
           │    splitit-db     │ (SQL Server 2022)
           └───────────────────┘
```

---

## Network Architecture & Isolation

To support hosting multiple projects cleanly on the same VPS, networks are explicitly named:

1. **`splitit-frontend-net`**:
   - Bridge network connecting the Nginx frontend / reverse proxy to the .NET 8 Web API.
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
Setup FAILED copying system data file 'C:\templatedata\master.mdf' to '/var/opt/mssql/data/master.mdf': 5(Access is denied.)
```
Volume is created with root ownership; `mssql` lacks write permission. Fixing would require a privileged chown init container or host directory with correct ownership, adding complexity and fragility. **Decision: keep `user: "0:0"` (root) for `sqlserver` service** — documented exception, stability preferred over artificial non-root.
**Mitigations compensating for root:** `internal:true` network (`splitit-backend-net`), no host `1433` exposure (`Ports: {"1433/tcp":null}`), `privileged:false`, resource limits (`1.5 CPU/2GB`), dedicated least-privilege `splitit_app` for API (see below), `db-init` one-shot creates app user and never uses `sa` at runtime.

### Database Least-Privilege
| Principal | Purpose | Permissions |
| :--- | :--- | :--- |
| `sa` (`${DB_PASSWORD}`) | Initial admin only, used by `db-init` one-shot container to create `splitit_app` | `sysadmin` (server) — **never in API connection string** |
| `splitit_app` (`${DB_APP_USER}`/`${DB_APP_PASSWORD}`) | API runtime (`ConnectionStrings__DefaultConnection`) | `db_datareader` + `db_datawriter` + `db_ddladmin` on `SplitItDb` — **not `db_owner`** — sufficient for EF Core migrations (DDL) and CRUD |
Init logic: `docker/sqlserver/init-app-user.sh` idempotently creates LOGIN/USER, grants roles, and ensures `SplitItDb` exists before `backend` starts (`depends_on: db-init: service_completed_successfully`).

### DataProtection Persistence
ASP.NET Core DataProtection keys stored at `/home/app/.aspnet/DataProtection-Keys` via `PersistKeysToFileSystem` (`Program.cs`). Mounted as volume `splitit_dataprotection_keys` so keys survive `docker compose down/up` without regeneration. Volume not in Git (`.dockerignore`/`.gitignore`).

### Backups
Out of scope for Phase 9. Documented as Phase 15. Manual `tar` of volume is not production backup.

---

## Environment Configuration

Copy `.env.example` to `.env` before running Docker Compose:

```bash
cp .env.example .env
```

| Variable | Description | Example |
| :--- | :--- | :--- |
| `DB_PASSWORD` | Strong SA password — **init only**, not used by API | `SecretPass12345!Secure` |
| `DB_APP_USER` | Dedicated app DB login (default `splitit_app`) | `splitit_app` |
| `DB_APP_PASSWORD` | Strong app user password, must differ from SA in prod | `SecretAppPass12345!Secure` |
| `JWT_SECRET` | 64+ char random string for JWT signing | `RandomBase64Key...` |
| `JWT_ISSUER` | Valid token issuer domain | `https://splitit.example.com` |
| `JWT_AUDIENCE` | Valid token audience domain | `https://splitit.example.com` |
| `CORS_ALLOWED_ORIGINS` | Permitted frontend origins | `https://splitit.example.com` |
| `FRONTEND_PORT` | Published HTTP port on VPS host | `80` |

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
*Expected Output:*
```text
NAME               STATUS                  PORTS
splitit-db         Up (healthy)            
splitit-backend    Up (healthy)            
splitit-frontend   Up (healthy)            0.0.0.0:80->80/tcp
```

### 3. View Logs
```bash
# All services
docker compose logs -f

# Specific service
docker compose logs -f backend
```

### 4. Stop Services
```bash
docker compose down
```

### 5. Verify Database Isolation (Port Scan)
```bash
docker ps
```
Ensure `splitit-db` shows **NO** published ports under the `PORTS` column.
