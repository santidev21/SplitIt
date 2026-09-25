# SplitIt

![License](https://img.shields.io/badge/license-MIT-blue)
![.NET](https://img.shields.io/badge/.NET-8-purple)
![Angular](https://img.shields.io/badge/Angular-21-red)

**SplitIt** is a web application designed to help people manage shared expenses within groups. Whether you're on a trip with friends, splitting rent with roommates, or handling any shared bills, SplitIt simplifies the process of tracking expenses and settling debts fairly.

![Group Overview](docs/images/group-overview.png)
---

## Features

- Create and manage expense groups
- Add participants to each group
- Register shared expenses and specify who paid
- Automatically split expenses among members (equal, by amount, or by percentage)
- See how much each member owes or is owed
- Settle individual or total debts (including partial payments)
- Friends system: send/accept/reject friend requests, search users
- Admin panel: manage users, roles, settings, currencies
- Group admin: edit group, promote/demote/remove members, invite friends
- Authentication system with protected routes (JWT)
- Real-time notifications for friend requests
- Form validation with inline error messages

---

## Architecture

```
Internet → vps-gateway (:80/:443, private repo)
  └── splitit.santidev21.tech
        ├── /api/*  → splitit-backend (.NET 8)
        ├── /health → splitit-backend (.NET 8)
        └── /*      → splitit-frontend (Angular)
```

**Docker services:**
| Service | Description |
|---|---|
| `splitit-db` | SQL Server 2022 |
| `splitit-db-init` | Creates least-privilege DB users on first run |
| `splitit-migrator` | Runs EF Core migrations then exits |
| `splitit-backend` | .NET 8 API (internal, not exposed publicly) |
| `splitit-frontend` | Angular 21 via nginx (internal, not exposed publicly) |

**Backend Clean Architecture:**
- **`SplitIt.API`** → Controllers, Middleware, Program.cs
- **`SplitIt.Application`** → DTOs, Application Services
- **`SplitIt.Domain`** → Entities, Value Objects, Domain Logic
- **`SplitIt.Infrastructure`** → EF Core, Migrations, External services
- **`SplitIt.Shared`** → Cross-cutting concerns

---

## Tech Stack

| Layer | Technology |
|---|---|
| Frontend | Angular 21, Angular Material, SCSS, Bootstrap |
| Backend | .NET 8 Web API (C#) |
| Database | SQL Server 2022 (EF Core) |
| Auth | JWT (HMAC-SHA256) |
| Gateway | nginx via [vps-gateway](https://github.com/santidev21/vps-gateway) (HTTPS, HSTS, security headers) |
| CI/CD | GitHub Actions (test → build → Trivy scan → deploy) |
| Deploy | Docker Compose on VPS |

---

## Project Structure

```
SplitIt/
├── SplitIt.API/       # .NET solution (API, Application, Domain, Infrastructure, Shared)
├── SplitIt.Tests/     # Backend tests (referenced from the solution)
├── split-it-ui/       # Angular 21 frontend (src/app, e2e)
├── docker/            # Docker configs (backend, frontend, proxy, sqlserver)
├── docs/              # Guides, reports, screenshots, specs (docs/specs/)
├── scripts/           # Deploy and helper scripts
├── .opencode/         # AI home: agent/, command/, skills/
├── .github/           # CI/CD workflows
├── opencode.json      # opencode config (instructions, MCP, permissions)
├── docker-compose.yml
└── .env.example
```

---

## Local Development

The database (SQL Server 2022) **always runs in Docker** — loopback-only (`127.0.0.1:1433`), never exposed externally. Only where the app itself runs changes:

- `npm run docker:dev` → everything (DB + API + frontend) in Docker, closest to prod.
- `npm run dev` → DB in Docker, API + frontend native (`dotnet run` / `npm start`) with hot reload, against the **same** DB volume.

### Prerequisites
- Node.js 20+
- .NET 8 SDK
- Docker Desktop (with WSL 2)

### 1. Clone the repo
```bash
git clone https://github.com/santidev21/SplitIt.git
cd SplitIt
```

### 2. Set up environment
```bash
cp .env.example .env
# Fill DB_PASSWORD (SA), DB_APP_PASSWORD, DB_MIGRATOR_PASSWORD,
# JWT_SECRET (64+ chars), GOOGLE_CLIENT_ID
```

Frontend first-time setup:
```bash
npm run setup   # npm install --legacy-peer-deps in split-it-ui
```

### 3. Run everything in Docker (closest to prod)
```bash
npm run docker:dev
# = docker compose -f docker-compose.yml -f docker-compose.local.yml up --build
```
Starts all 5 services. API at `http://localhost:8090`, frontend at `http://localhost:80`.

```bash
# Stop (data persists in the splitit_sqlserver_data volume)
docker compose -f docker-compose.yml -f docker-compose.local.yml down

# Wipe the DB and start clean
docker compose -f docker-compose.yml -f docker-compose.local.yml down -v
```

### 4. Run natively with hot reload
```bash
npm run dev
```
Starts SQL Server in Docker, applies EF migrations, then runs the API (`http://localhost:5120`, Swagger at `/swagger`) and the frontend (`http://localhost:4200`, proxies `/api` → 5120).

Single side:
```bash
npm run dev:api   # API only (:5120)
npm run dev:ui    # frontend only (:4200)
```

### 5. Database & migrations

```bash
npm run db:up       # start SQL Server only (127.0.0.1:1433, loopback-only)
npm run db:down     # stop it (data persists in the volume)
npm run db:migrate  # apply EF migrations (dotnet ef database update)
# New migration:
npm run db:migration:add -- <Name>
```

Native `dotnet run` takes the SA password from `.env` (`DB_PASSWORD`), so it always matches the Docker SQL Server. On first run the root scripts create `SplitIt.API/SplitIt.API/appsettings.Development.json` (gitignored) from the committed `.example` template. In the Docker flow, migrations run via the one-shot `splitit-migrator` container instead.

### Commands

| Command | Purpose |
|---|---|
| `npm run dev` | DB (Docker) + API + frontend with hot reload |
| `npm run dev:ui` / `npm run dev:api` | Frontend / API only |
| `npm run db:up` / `npm run db:down` | Start / stop SQL Server in Docker |
| `npm run db:migrate` | Apply EF migrations |
| `npm run docker:dev` | Full stack in Docker (like prod) |
| `npm run build` / `npm run test` | Build / test frontend + backend |

### 6. Create your first admin user

After registering a user, promote them to SuperAdmin via SQL:
```sql
-- Connect to SplitIt_Dev database
UPDATE Users SET RoleId = 1 WHERE Email = 'your@email.com';
```
Or use the admin panel (requires SuperAdmin role):
```bash
curl -X POST http://localhost:5120/api/admin/promote \
  -H "Authorization: Bearer <superadmin_token>" \
  -H "Content-Type: application/json" \
  -d '{"userId": 2}'
```

---

## Deployment

Deploys happen automatically on push to `main` via GitHub Actions. For VPS setup and manual deploy commands, see [docs/DEPLOYMENT.md](docs/DEPLOYMENT.md).

---

## Screenshots

### Login view
![Login view](docs/images/login.png)

### Add Group
![Add group](docs/images/add-group.png)

### Group Overview
![Group Overview](docs/images/group-overview.png)

### Add Expense Dialog
![Add Expense](docs/images/add-expense.png)

---

## Security

- JWT auth with HMAC-SHA256 signed tokens (64+ char secret required in production)
- BCrypt password hashing (with automatic rehash on login)
- Rate limiting enforced at the gateway
- CORS restricted to configured origins only
- Database isolated on an internal Docker network, no DB ports exposed
- Security headers (HSTS, CSP, X-Frame-Options) applied by the gateway

---

## AI Context

- [AGENTS.md](AGENTS.md) — project snapshot (stack, layout, commands, working rules)
- [opencode.json](opencode.json) — instructions, MCP servers and permissions
- [.opencode/agent/](.opencode/agent/) — per-area playbooks (backend, frontend, reviewer)
- [.opencode/skills/](.opencode/skills/) — task playbooks (migrations, e2e, tests, docker, contracts, i18n, …)
- [.opencode/command/](.opencode/command/) — shortcuts (`/test`, `/migrate`, `/e2e`)
- [docs/specs/](docs/specs/) — architecture, auth, database detail specs

---

## To Do

- [x] Fix Google OAuth sign-up for new users on mobile — creating the account fails on mobile and only works after creating it on desktop first.
- [x] Fix i18n on the group dashboard: "¡Estás todo liquidado!" is not translated to English when the app is in EN.
- [x] Fix i18n on the add-expense dialog: "Pagado por You" hardcodes English "You" — it should be localized ("Tú" in ES).

### Follow-ups (2026-09 audit)

- [ ] **Currency precision & exact totals** (implemented in `AddCurrencyDecimalPlaces`): money is now validated at the group currency's decimal scale (COP 0, USD 2) and expense shares must conserve the total exactly; payments may not exceed the remaining debt. Remaining work: verify the migration applied to every environment (`npm run db:migrate` / migrator) and that existing data is reconciled (`scripts/db-integrity-audit.sql`).
- [ ] **Group authorization** (global roles removed from group management): application `Admin`/`SuperAdmin` no longer override group permissions. Verify no stored super-admin workflows, UI flows, or support docs depend on the old behavior (see `docs/AUDIT_2026-09.md`).
- [ ] **Payment retry safety / audit atomicity**: `AppDbContext` writes audit rows in a second save; a failure there can report a committed payment as failed, and `POST /api/expenses/settle` has no idempotency key. Investigate making audit+payment atomic and add a retry-safe idempotency mechanism.
- [ ] **Reset-flow contract**: `POST /api/auth/reset-password` (no email) and `POST /api/auth/verify-reset-code` (email-bound) enforce different rules on the same 6-digit code. Decide the intended contract, then align routes and tests.
- [ ] **API authorization regression tests**: add HTTP-level tests (WebApplicationFactory + SQL Server) covering cross-group reads/writes and forbidden group-role actions (current coverage is service-level only).
- [ ] **Balance display precision**: summary and payment amounts now use the group currency's decimals. Verify rendering for USD cents and COP whole units across the dashboard (added component specs).
- [ ] **Dependency & secret scans in CI**: address dev/test advisory backlog (`npm audit` 18 dev-only, `SSH.NET` transitive in `SplitIt.Tests`), add `npm audit`/`dotnet list package --vulnerable` gates, and replace the grep-based secret check with a maintained scanner.
- [ ] **Update remaining docs**: `docs/SECURITY.md` still describes localStorage JWT and no refresh tokens; `docs/specs/auth.md` says BCrypt; `docs/TESTING.md` counts are stale. Align them with current source.

---

## License

MIT — see [LICENSE](LICENSE).
