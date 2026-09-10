---
description: Add or apply an EF Core migration for SplitIt.
---

Follow the `db-migrations` skill. From the repo root:

Add a migration (name from $ARGUMENTS, or ask for one):
```bash
dotnet ef migrations add $ARGUMENTS --project SplitIt.Infrastructure --startup-project SplitIt.API
```

Apply locally:
```bash
dotnet ef database update --project SplitIt.Infrastructure --startup-project SplitIt.API
```

Rules: never edit an applied migration — add a new one. In Docker, `splitit-migrator` applies them automatically.
