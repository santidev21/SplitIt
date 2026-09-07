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
