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
           │  splitit-backend  │ (.NET 8 Web API, runtime as splitit_app)
           └─────────┬─────────┘
                     │ (splitit-backend-net: internal=true)
                     ▼
           ┌───────────────────┐
           │   splitit-migrator│ (one-shot, as splitit_migrator, db_ddladmin)
           └─────────┬─────────┘
                     │ depends_on migrator completed
                     ▼
           ┌───────────────────┐
           │    splitit-db     │ (SQL Server 2022)
           └───────────────────┘
  Startup order: sqlserver (healthy) → db-init (create users) → migrator (EF Migrate) → backend → frontend
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

### Database Least-Privilege (Phase 10)
| Principal | Purpose | Permissions | Used By |
| :--- | :--- | :--- | :--- |
| `sa` (`${DB_PASSWORD}`) | Bootstrap only | `sysadmin` — **never in API** | `db-init` only |
| `splitit_migrator` (`${DB_MIGRATOR_USER/PASSWORD}`) | EF Core migrations (one-shot) | `db_datareader` + `db_datawriter` + `db_ddladmin` — **not `db_owner`** | `migrator` service (`dotnet SplitIt.API.dll --migrate`) |
| `splitit_app` (`${DB_APP_USER/PASSWORD}`) | API runtime | `db_datareader` + `db_datawriter` **only (no DDL, no owner)** | `backend` (`splitit-backend`) |
Init logic: `docker/sqlserver/init-db-users.sh` idempotently creates both LOGIN/USER, ensures `SplitItDb` exists. `backend` no longer runs `Database.Migrate()` — see `Program.cs:213` migrator branch.

### Migration Strategy (Phase 10)
- **Previous (unsafe):** `Program.cs:267` `db.Database.Migrate()` on every API startup — multiple instances race, requires SA/ddl in runtime.
- **Current (safe):** Dedicated `migrator` service in `docker-compose.yml:55` — `depends_on: db-init completed` → runs `dotnet SplitIt.API.dll --migrate` with migrator credentials, applies pending migrations via `Database.Migrate()` then exits 0. `backend` `depends_on: migrator completed_successfully` so API never starts before DB is ready. Running `migrator` twice is idempotent (`Pending 0` → `No migrations applied`). Failure exits 1, visible in `docker compose ps` and logs.
- Migrations audited: 8 deterministic migrations (`20250401024634_InitialCreate` → `20250524200603_ChangeExpenseDateToDateTime`), all use `HasColumnType("decimal(18,2)")` for money, no `float`, `HasIndex` unique on `Users.Email`, FK `Cascade`/`Restrict` validated.

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
| `DB_APP_USER` | App DB login (default `splitit_app`) — no DDL | `splitit_app` |
| `DB_APP_PASSWORD` | App password, must differ from SA/Migrator in prod | `SecretAppPass12345!Secure` |
| `DB_MIGRATOR_USER` | Migrator login (default `splitit_migrator`) — with DDL | `splitit_migrator` |
| `DB_MIGRATOR_PASSWORD` | Migrator password, must differ from SA/App in prod | `SecretMigrPass12345!Secure` |
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
