---
name: docker-dev
description: Run SplitIt locally with Docker Compose. Use when starting services with docker compose, docker-compose.local.yml, splitit-db, splitit-backend, splitit-frontend, or debugging local ports.
---

# Local Docker

Run from the repo root:

```bash
# Full local stack (bridge networks, debug ports)
docker compose -f docker-compose.yml -f docker-compose.local.yml up --build

# Validate only
docker compose config --quiet

# Tear down
docker compose -f docker-compose.yml -f docker-compose.local.yml down
```

Local ports:
- Backend: `http://localhost:8080` (Swagger at `/swagger`)
- Frontend: `http://localhost:80`
- SQL Server: internal to the compose network (`splitit-db`)

Services (`docker-compose.yml`): `splitit-db` (SQL Server 2022) → `splitit-db-init` (least-privilege users, first run) → `splitit-migrator` (EF migrations, runs then exits) → `splitit-backend` + `splitit-frontend`.

Rules:
- Manual (no-Docker) dev uses `http://localhost:5120` (backend) and `http://localhost:4200` (frontend, proxies `/api` to 5120 via `proxy.conf.json`).
- Never bake secrets into images — `.env` and certs are excluded via `.dockerignore`.
