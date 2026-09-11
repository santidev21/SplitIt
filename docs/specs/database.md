# Database

- SQL Server 2022, accessed via EF Core (`SplitIt.Infrastructure`).
- Local dev connection (Docker SQL Server on `127.0.0.1:1433`, SA password from `.env` `DB_PASSWORD`, injected by the root `npm run dev*` scripts; see `appsettings.Development.json`):
  `Server=localhost,1433;Database=SplitIt_Dev;User Id=sa;Password=<DB_PASSWORD>;TrustServerCertificate=True`
- In Docker, `splitit-db-init` creates least-privilege users on first run and `splitit-migrator` applies EF migrations before the backend starts.
- Never edit an applied migration — add a new one. See [db-migrations](../../.opencode/skills/db-migrations/SKILL.md).
