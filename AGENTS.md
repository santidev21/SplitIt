# SplitIt Project Context

This file is the working context for SplitIt. Keep it updated when architecture, routing, scripts, or conventions change. See [docs/specs/](docs/specs/) for detail docs.

## Project Snapshot
SplitIt is a shared-expense manager with:
- Angular 19 frontend (Material, SCSS, Bootstrap)
- .NET 8 backend using Clean Architecture (API, Application, Domain, Infrastructure, Shared)
- SQL Server 2022 persistence via EF Core
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
├─ scripts/         # Deploy and helper scripts
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

## Commands (from repo root unless noted)
- Backend: `dotnet build SplitIt.API/SplitIt.Back.sln -c Release` · `dotnet test SplitIt.API/SplitIt.Back.sln -c Release` (see `backend-test` skill)
- Frontend (from `split-it-ui/`): `npm install --legacy-peer-deps` · `npm test` · `npm run test:ci` · `npm run build` (see `frontend-test`)
- E2E (from `split-it-ui/`): `npx playwright test` · `npm run e2e:fullstack` (see `run-e2e`)
- Migrations: `dotnet ef migrations add <Name> --project SplitIt.Infrastructure --startup-project SplitIt.API` (see `db-migrations`, or `/migrate`)
- Docker: `docker compose -f docker-compose.yml -f docker-compose.local.yml up --build` (see `docker-dev`)
- Shortcuts: `/test` (both suites) · `/e2e` · `/migrate`

## Ports
| Context | Backend | Frontend |
|---|---|---|
| Manual dev | `http://localhost:5120` (Swagger at `/swagger`) | `http://localhost:4200` (proxies `/api` → 5120) |
| Docker local | `http://localhost:8080` | `http://localhost:80` |

## AI Setup
- `.opencode/` is the AI home (tracked in git): `skills/` (task playbooks in `SKILL.md` format), `agent/` (per-area playbooks: backend, frontend, reviewer), `command/` (shortcuts: /test, /migrate, /e2e). Local plugin scaffold (`node_modules`, `package.json`) is ignored.
- `opencode.json` holds instructions, MCP servers and permissions. Skills, agents and commands need no config — opencode auto-discovers `.opencode/`.
- `AGENTS.md` is the single source of truth; `docs/specs/` holds details.

## Working Rules For This Repo
- Prefer small, focused changes.
- Keep API contracts, frontend types, and tests aligned in the same pass.
- EF migrations live in `SplitIt.Infrastructure`; never edit applied migrations, add a new one.
- Run `npm install --legacy-peer-deps` in `split-it-ui`.
- Keep the root README and this file synchronized when behavior changes.
