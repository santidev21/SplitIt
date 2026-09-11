---
name: docker-dev
description: Run SplitIt locally with Docker Compose. Use when starting services with docker compose, docker-compose.local.yml, splitit-db, splitit-backend, splitit-frontend, or debugging local ports.
---

# Local Docker

Prefer the root scripts (`npm run docker:dev`, `npm run db:up`, `npm run dev`). Run from the repo root:

```bash
# Full local stack (bridge networks, debug ports)
npm run docker:dev
# = docker compose -f docker-compose.yml -f docker-compose.local.yml up --build

# Validate only
docker compose config --quiet

# Tear down
docker compose -f docker-compose.yml -f docker-compose.local.yml down
```

Local ports:
- Backend: `http://localhost:8090` (Swagger at `/swagger`; container listens on 8080, host 8090 avoids clash with Bikontrol API)
- Frontend: `http://localhost:80`
- SQL Server: `127.0.0.1:1433` (loopback only; same `splitit_sqlserver_data` volume native dev uses)

Services (`docker-compose.yml`): `splitit-db` (SQL Server 2022) → `splitit-db-init` (least-privilege users, first run) → `splitit-migrator` (EF migrations, runs then exits) → `splitit-backend` + `splitit-frontend`.

Rules:
- Native (no-Docker app) dev uses `npm run dev` (DB in Docker + `dotnet run` backend `:5120` + `ng serve` frontend `:4200`, proxies `/api` to 5120 via `proxy.conf.json`); single side via `dev:api` / `dev:ui`.
- Never bake secrets into images — `.env` and certs are excluded via `.dockerignore`.
