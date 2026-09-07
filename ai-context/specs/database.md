# Database

- SQL Server 2022, accessed via EF Core (`SplitIt.Infrastructure`).
- Local dev connection (see `appsettings.Development.json`):
  `Server=localhost;Database=SplitIt_Dev;Trusted_Connection=True;TrustServerCertificate=True`
- In Docker, `splitit-db-init` creates least-privilege users on first run and `splitit-migrator` applies EF migrations before the backend starts.
- Never edit an applied migration — add a new one. See [skills/db-migrations.md](../skills/db-migrations.md).
