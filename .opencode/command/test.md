---
description: Run the full SplitIt test suites (backend + frontend unit tests).
---

Run both suites from the repo root and report failures per suite:

1. Backend (needs SQL Server only for `SplitIt.Tests/Integration/`; unit tests run standalone):
```bash
dotnet test SplitIt.API/SplitIt.Back.sln -c Release --collect:"XPlat Code Coverage" --settings coverlet.runsettings
```

2. Frontend (from `split-it-ui/`):
```bash
npm run test:ci
```

Extra input: $ARGUMENTS (e.g. a test name filter or a single suite: `backend` / `frontend`).

Do not fix failures unless asked — report which suite and which test failed.
