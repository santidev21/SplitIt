---
name: db-migrations
description: Add and apply EF Core migrations for SplitIt. Use when creating or applying SQL Server migrations in SplitIt.Infrastructure.
---

# EF Core migrations

Migrations live in `SplitIt.Infrastructure`. Run from the repo root:

```bash
# Add a migration
dotnet ef migrations add YourMigrationName --project SplitIt.Infrastructure --startup-project SplitIt.API

# Apply locally
dotnet ef database update --project SplitIt.Infrastructure --startup-project SplitIt.API
```

Rules:
- Never edit an applied migration — add a new one.
- In Docker, `splitit-migrator` applies migrations automatically on startup.
