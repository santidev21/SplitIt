---
name: backend-test
description: Build and run the SplitIt .NET backend tests. Use when running backend tests, dotnet build, dotnet test, SplitIt.Tests, or checking backend coverage.
---

# Backend tests (.NET 8)

Solution: `SplitIt.API/SplitIt.Back.sln`. The test project lives at the repo root (`SplitIt.Tests/`) and is referenced from the solution.

Run from the repo root:

```bash
dotnet restore SplitIt.API/SplitIt.Back.sln
dotnet build SplitIt.API/SplitIt.Back.sln --no-restore -c Release
dotnet test SplitIt.API/SplitIt.Back.sln --no-build -c Release --collect:"XPlat Code Coverage" --settings coverlet.runsettings
```

Rules:
- Integration tests under `SplitIt.Tests/Integration/` need SQL Server (CI spins up `mcr.microsoft.com/mssql/server:2022-latest` on port 1433).
- Unit tests run without a database.
- Never change an API contract without updating the Angular types and their tests (see `api-contract`).
