# SplitIt Project Context

This file is the working context for SplitIt. Keep it updated when architecture, routing, scripts, or conventions change. See [specs/](specs/) for detail docs.

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
├─ SplitIt.API/     # .NET solution (API, Application, Domain, Infrastructure, Shared, Tests)
├─ split-it-ui/     # Angular application (src/app, e2e)
├─ docker/          # Docker configs (backend, frontend, proxy, sqlserver)
├─ docs/            # Guides, reports, screenshots
├─ scripts/         # Deploy and helper scripts
├─ ai-context/      # This context
├─ .github/         # CI/CD workflows
├─ docker-compose.yml
└─ .env.example
```

## Backend Architecture
Clean Architecture layers: `API` (controllers, middleware) → `Application` (DTOs, services) → `Domain` (entities) → `Infrastructure` (EF Core, migrations). Docker adds `splitit-db-init` (least-privilege users) and `splitit-migrator` (runs migrations, then exits).

## Frontend Architecture
Angular 19 app in `split-it-ui/src/app`. Protected routes via JWT, admin panel behind SuperAdmin role, Material dialogs for groups/expenses.

## Working Rules For This Repo
- Prefer small, focused changes.
- Keep API contracts, frontend types, and tests aligned in the same pass.
- EF migrations live in `SplitIt.Infrastructure`; never edit applied migrations, add a new one.
- Run `npm install --legacy-peer-deps` in `split-it-ui`.
- Keep the root README and this file synchronized when behavior changes.
