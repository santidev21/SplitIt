# SplitIt Project Context

This file is the working context for SplitIt. Keep it updated when architecture, routing, scripts, or conventions change. See [docs/specs/](docs/specs/) for detail docs.

## Project Snapshot
SplitIt is a shared-expense manager with:
- Angular 19 frontend (Material, SCSS, Bootstrap)
- .NET 8 backend using Clean Architecture (API, Application, Domain, Infrastructure, Shared)
- SQL Server 2022 persistence via EF Core (DB always in Docker, loopback-only `:1433` locally)
- Root `package.json` orchestrates local dev (`dev`, `dev:ui/dev:api`, `db:*`, `docker:dev` scripts)
- JWT authentication (HMAC-SHA256), BCrypt password hashing
- Friends system, group admin, admin panel, partial payments
- Real-time notifications for friend requests
- Frontend unit tests, backend tests, Playwright e2e

## Repository Layout
```text
SplitIt/
├─ SplitIt.API/     # .NET solution (API, Application, Domain, Infrastructure, Shared)
├─ SplitIt.Tests/   # Backend tests (referenced from SplitIt.API/SplitIt.Back.sln)
├─ split-it-ui/     # Angular application (src/app, e2e)
├─ docker/          # Docker configs (backend, frontend, proxy, sqlserver)
├─ docs/            # Guides, reports, screenshots, specs (docs/specs/)
├─ scripts/         # Deploy, helper and local-dev orchestration scripts (run-splitit.mjs)
├─ package.json     # Root orchestration scripts (dev, db:*, docker:dev, build, test)
├─ .opencode/       # AI home: agent/, command/, skills/ (tracked; local plugin scaffold ignored)
├─ .github/         # CI/CD workflows
├─ docker-compose.yml
├─ docker-compose.local.yml
├─ opencode.json    # opencode config: instructions, MCP, permissions
└─ .env.example
```

## Backend Architecture
Clean Architecture layers: `API` (controllers, middleware) → `Application` (DTOs, services) → `Domain` (entities) → `Infrastructure` (EF Core, migrations). Docker adds `splitit-db-init` (least-privilege users) and `splitit-migrator` (runs migrations, then exits).

## Frontend Architecture
Angular 19 app in `split-it-ui/src/app`. Protected routes via JWT, admin panel behind SuperAdmin role, Material dialogs for groups/expenses.

## Commands (run from repo root via root scripts unless noted)
- Both: `npm run dev` (DB in Docker + API + frontend, hot reload) · `npm run build` · `npm run test` (see `/test`)
- Single side: `npm run dev:api` · `npm run dev:ui`
- Backend (from `split-it-ui/`): `npm install --legacy-peer-deps` (`npm run setup` from root)
- E2E (from `split-it-ui/`): `npx playwright test` · `npm run e2e:fullstack` (see `run-e2e`)
- Migrations: `npm run db:migrate` (= `dotnet ef database update --project SplitIt.API/SplitIt.Infrastructure --startup-project SplitIt.API/SplitIt.API`) · `npm run db:migration:add -- <Name>` (see `db-migrations`, or `/migrate`)
- DB: `npm run db:up` (SQL Server on `127.0.0.1:1433`, loopback-only) · `npm run db:down`
- Docker: `npm run docker:dev` (= `docker compose -f docker-compose.yml -f docker-compose.local.yml up --build`) (see `docker-dev`)
- Shortcuts: `/test` (both suites) · `/e2e` · `/migrate`

## Ports
| Context | Backend | Frontend | DB |
|---|---|---|---|
| Native dev (`npm run dev`) | `http://localhost:5120` (Swagger at `/swagger`) | `http://localhost:4200` (proxies `/api` → 5120) | `127.0.0.1:1433` (docker, same volume) |
| Docker local (`npm run docker:dev`) | `http://localhost:8090` | `http://localhost:80` | internal only |

## AI Setup
- `.opencode/` is the AI home (tracked in git): `skills/` (task playbooks in `SKILL.md` format), `agent/` (per-area playbooks: backend, frontend, reviewer), `command/` (shortcuts: /test, /migrate, /e2e). Local plugin scaffold (`node_modules`, `package.json`) is ignored.
- `opencode.json` holds instructions, MCP servers and permissions. Skills, agents and commands need no config — opencode auto-discovers `.opencode/`.
- `AGENTS.md` is the single source of truth; `docs/specs/` holds details.

## Working Rules For This Repo
- Prefer small, focused changes.
- Keep API contracts, frontend types, and tests aligned in the same pass.
- EF migrations live in `SplitIt.Infrastructure`; never edit applied migrations, add a new one (`npm run db:migration:add -- <Name>` then `npm run db:migrate`).
- DB always runs in Docker — ALWAYS use both `-f` flags: `docker compose -f docker-compose.yml -f docker-compose.local.yml …`.
- Native `dotnet run` takes the SA password from `.env` (`DB_PASSWORD`) via the root scripts; prefer `npm run dev*` over per-folder commands.
- Run `npm install --legacy-peer-deps` in `split-it-ui` (`npm run setup` from root).
- Keep the root README and this file synchronized when behavior changes.
