# Phase 9 — Docker Containerization Report

## Status: COMPLETED

All Phase 9 deliverables have been implemented and validated.

---

## 1. Network Architecture

Networks have been isolated and custom-named to avoid conflicts with other projects hosted on the same VPS:

```text
INTERNET
   │
   ▼ :80 / :443
┌──────────────────┐
│ splitit-frontend │ (Nginx + Angular SPA)
└────────┬─────────┘
         │ (splitit-frontend-net)
         ▼
┌──────────────────┐
│ splitit-backend  │ (.NET 8 Web API)
└────────┬─────────┘
         │ (splitit-backend-net: internal=true)
         ▼
┌──────────────────┐
│   splitit-db     │ (SQL Server 2022)
└──────────────────┘
```

- **`splitit-frontend-net`**: Bridge network connecting frontend reverse proxy and API.
- **`splitit-backend-net`**: Fully internal bridge network (`internal: true`). SQL Server is accessible **only** to `splitit-backend`. Port `1433` is **not** exposed to the host machine.

---

## 2. Security Implementations

1. **Multi-Stage Build**:
   - Backend: `dotnet/sdk:8.0` → `aspnet:8.0-alpine`.
   - Frontend: `node:20-alpine` → `nginx:1.27-alpine`.
2. **Non-Root Container Execution**:
   - Backend runs as `USER $APP_UID` (UID 1654).
   - Frontend runs as `USER nginx`.
3. **Health Probes**:
   - Backend: HTTP probe on `http://localhost:8080/health`.
   - Frontend: HTTP probe on `http://localhost:80/`.
   - SQL Server: `sqlcmd` query probe (`SELECT 1`).
4. **Resource Constraints**:
   - SQL Server: 1.5 CPUs, 2 GB RAM.
   - Backend API: 1.0 CPU, 512 MB RAM.
   - Frontend Nginx: 0.5 CPU, 128 MB RAM.

---

## 3. Files Created

- `docker/backend/Dockerfile`
- `docker/frontend/Dockerfile`
- `docker/frontend/nginx.conf`
- `docker-compose.yml`
- `.dockerignore`
- `.env.example`
- `docs/DOCKER.md`
- `docs/PHASE_9_REPORT.md`

---

## 4. Verification

- `docker compose config`: Executed and validated successfully.
- `dotnet test`: 78 passed, 0 failed.
- Angular build: Succeeded without errors (`dist/split-it-ui/browser`).
