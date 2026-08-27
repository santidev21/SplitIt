# SplitIt

**SplitIt** is a web application designed to help people manage shared expenses within groups. Whether you're on a trip with friends, splitting rent with roommates, or handling any shared bills, SplitIt simplifies the process of tracking expenses and settling debts fairly.

![Group Overview](docs/images/group-overview.png)
---

## Features

- Create and manage expense groups
- Add participants to each group
- Register shared expenses and specify who paid
- Automatically split expenses among members
- See how much each member owes or is owed
- Settle individual or total debts
- Authentication system with protected routes (JWT)

---

## Architecture

```
Internet → HTTPS → Reverse Proxy (nginx)
  └── splitit.yourdomain.com
        ├── /api/*  → Backend (.NET 8)
        ├── /health → Backend (.NET 8)
        └── /*      → Frontend (Angular)
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
| Proxy | nginx:alpine (HTTPS, HSTS, security headers) |
| CI/CD | GitHub Actions (test → build → Trivy scan → deploy) |
| Deploy | Docker Compose on VPS |

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
docker compose up -d
```
This starts all 5 services. The backend will be at `http://localhost:8080`, frontend at `http://localhost:80`.

### 4. Run without Docker (manual)

**Backend:**
```bash
cd SplitIt.API
dotnet restore
dotnet ef database update
dotnet run
```

**Frontend:**
```bash
cd split-it-ui
npm install
ng serve
```

---

## Running Tests

```bash
# Backend unit tests
dotnet test SplitIt.API/SplitIt.Back.sln

# Frontend unit tests
npm run test --prefix split-it-ui

# E2E tests (Playwright)
cd split-it-ui
npx playwright install chromium
npx playwright test
```

**Note:** Integration tests (Phase10DatabaseTests, HealthCheckTests, RateLimitingTests) require a running SQL Server instance and are excluded from CI.

---

## CI/CD Pipeline

The GitHub Actions workflow (`.github/workflows/ci.yml`) runs on push to `main`:

| Job | What it does |
|---|---|
| Backend Tests | .NET build + test + coverage |
| Frontend Build & Test | Angular build + unit tests + coverage |
| Playwright E2E | End-to-end browser tests |
| Security Scan | Checks for secrets in code, validates .gitignore/.dockerignore |
| Docker Build & Trivy | Builds images, scans for HIGH/CRITICAL vulnerabilities |
| Docker Compose Validate | Validates compose file syntax |
| Deploy to VPS | SSH into VPS, pulls latest, rebuilds, restarts containers |

**Required GitHub Environment Secrets** (production environment):
| Secret | Description |
|---|---|
| `VPS_SSH_PRIVATE_KEY` | Base64-encoded ED25519 private key for deploy |
| `VPS_HOST` | VPS IP address |
| `VPS_USER` | SSH user |

---

## Production Deployment

### VPS Setup (one-time)
```bash
# Install Docker
curl -fsSL https://get.docker.com | sh
sudo usermod -aG docker $USER

# Clone the repo
sudo mkdir -p /opt/splitit
sudo chown $USER:docker /opt/splitit
git clone https://github.com/santidev21/SplitIt.git /opt/splitit

# Configure environment
cd /opt/splitit
cp .env.example .env
# Edit .env with production secrets

# Create Docker network
docker network create splitit-frontend-net

# Start services
docker compose up -d
```

### Deploy Updates
Deploys happen automatically on push to `main` via GitHub Actions. Manual deploy:
```bash
cd /opt/splitit
./scripts/deploy.sh deploy
```

### Other Deploy Commands
```bash
./scripts/deploy.sh status    # Show container status
./scripts/deploy.sh logs      # Show recent logs
./scripts/deploy.sh rollback  # Rollback to last backup
./scripts/deploy.sh verify    # Verify all services healthy
```

---

## Security

- JWT tokens signed with HMAC-SHA256 (64+ char secret required in production)
- BCrypt password hashing (with automatic rehash on login)
- Rate limiting: 5 req/min on auth endpoints, 100 req/min on general API
- CORS restricted to configured origins only
- Docker containers run as non-root (except SQL Server on Docker Desktop Windows)
- Internal Docker network isolates database from external access
- No database ports exposed to host
- Trivy vulnerability scanning in CI
- Security headers: HSTS, CSP, X-Frame-Options, etc.

---

## Screenshots

### Add Group
![Add group](docs/images/add-group.png)

### Group Overview
![Group Overview](docs/images/group-overview.png)

### Add Expense Dialog
![Add Expense](docs/images/add-expense.png)

---

## Future Features
- [ ] Partial payments functionality
- [ ] Support alternative split methods (by amount or percentage)
- [ ] Email validation
- [ ] Group admin and application admin roles
