# Architecture

## Traffic Flow
```
Internet → vps-gateway (:80/:443)
  └── splitit.santidev21.tech
        ├── /api/*  → splitit-backend (.NET 8)
        ├── /health → splitit-backend (.NET 8)
        └── /*      → splitit-frontend (Angular via nginx)
```

## Docker Services
| Service | Description |
|---|---|
| `splitit-db` | SQL Server 2022 |
| `splitit-db-init` | Creates least-privilege DB users on first run |
| `splitit-migrator` | Runs EF Core migrations then exits |
| `splitit-backend` | .NET 8 API (internal, not exposed publicly) |
| `splitit-frontend` | Angular 19 via nginx (internal, not exposed publicly) |

## Backend Layers (Clean Architecture)
- `SplitIt.API` → Controllers, Middleware, Program.cs
- `SplitIt.Application` → DTOs, Application Services
- `SplitIt.Domain` → Entities, Value Objects, Domain Logic
- `SplitIt.Infrastructure` → EF Core, Migrations, External services
- `SplitIt.Shared` → Cross-cutting concerns
