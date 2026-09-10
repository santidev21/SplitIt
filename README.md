# SplitIt

![License](https://img.shields.io/badge/license-MIT-blue)
![.NET](https://img.shields.io/badge/.NET-8-purple)
![Angular](https://img.shields.io/badge/Angular-19-red)

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
| `splitit-frontend` | Angular 19 via nginx (internal, not exposed publicly) |

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
| Frontend | Angular 19, Angular Material, SCSS, Bootstrap |
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
├── SplitIt.API/       # .NET solution (API, Application, Domain, Infrastructure, Shared, Tests)
├── split-it-ui/       # Angular 19 frontend (src/app, e2e)
├── docker/            # Docker configs (backend, frontend, proxy, sqlserver)
├── docs/              # Guides, reports, and screenshots
├── scripts/           # Deploy and helper scripts
├── ai-context/        # Project context for AI work
├── .github/           # CI/CD workflows
├── docker-compose.yml
└── .env.example
```

---

## Getting Started (Local Development)

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
# Edit .env with your local settings
```

### 3. Run with Docker (recommended)
```bash
# Start all services (local bridge networks, debug ports)
docker compose -f docker-compose.yml -f docker-compose.local.yml up --build
```
This starts all 5 services. Backend at `http://localhost:8080`, frontend at `http://localhost:80`.

### 4. Run without Docker (manual)

**Requirements:** SQL Server running locally or via Docker.

```bash
# Option A: Start just SQL Server via Docker
docker run -e "ACCEPT_EULA=Y" -e "SA_PASSWORD=YourStrong!Password123" \
  -p 1433:1433 --name splitit-sql -d mcr.microsoft.com/mssql/server:2022-latest

# Option B: Use your local SQL Server instance
```

**Backend:**
```bash
cd SplitIt.API

# Configure appsettings.Development.json with your connection string:
# "DefaultConnection": "Server=localhost;Database=SplitIt_Dev;Trusted_Connection=True;TrustServerCertificate=True"

dotnet restore
dotnet ef database update --project SplitIt.Infrastructure --startup-project SplitIt.API
dotnet run
```
Backend starts at `http://localhost:5120`. Swagger at `http://localhost:5120/swagger`.

**Frontend:**
```bash
cd split-it-ui
npm install --legacy-peer-deps
npm start
```
Frontend starts at `http://localhost:4200`, auto-proxies API calls to `localhost:5120`.

### 5. Create your first admin user

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

[ai-context/](ai-context/) is the canonical project context for AI-assisted work (architecture snapshot, specs, agents, and skills).

---

## To Do

- [x] Fix Google OAuth sign-up for new users on mobile — creating the account fails on mobile and only works after creating it on desktop first.
- [x] Fix i18n on the group dashboard: "¡Estás todo liquidado!" is not translated to English when the app is in EN.
- [x] Fix i18n on the add-expense dialog: "Pagado por You" hardcodes English "You" — it should be localized ("Tú" in ES).

---

## License

MIT — see [LICENSE](LICENSE).
