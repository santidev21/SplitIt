# Plan: SplitIt -> .NET 10 + Angular 22

Goal: same stack as Bikontrol (.NET 10, Angular 22) so the CV, README and portfolio are consistent.
Reference implementation: `../Bikontrol` (copy its `global.json`, CI `dotnet-version`/`node-version`,
Dockerfile base images and package versions as the starting point).

Deadline: .NET 8 leaves support on 2026-11-10. Do the backend phases first; Angular can follow.

## Starting state (verified 2026-11-02)

- 5 projects on `net8.0`; ASP.NET packages 8.0.31; **EF Core already 9.0.20** (mixed); Swashbuckle 6.9.0.
- `docker/backend/Dockerfile`: build `sdk:9.0`, runtime `aspnet:8.0-alpine` (also mixed).
- CI: `dotnet-version: 8.0.x` in `ci.yml`, `mutation.yml`, `sonarcloud.yml`; `node-version: 22`.
- Frontend: Angular 21.2, `@angular-devkit/build-angular`, Karma + Jasmine, TypeScript 5.9, Material 21.
- Active branch is `feat/debt-simplification`: merge or park it before starting.
- Bikontrol reference: `global.json` SDK 10.0.401, EF Core 10.0.13, Npgsql 10.0.3, Angular 22.2, `@angular/build`,
  Vitest 4, TypeScript 6.0, Node 24, `Microsoft.AspNetCore.OpenApi`.

## Phase 0 - Prep (no behaviour change)

1. Merge or stash `feat/debt-simplification`; start from a clean, green `main`.
2. `git switch -c chore/upgrade-net10`.
3. Record a baseline: `npm run test` (backend + frontend) and `npm run build` pass; note coverage numbers.
4. Add `global.json` pinning the .NET 10 SDK (copy Bikontrol's).

## Phase 1 - Backend to .NET 10

1. `TargetFramework` -> `net10.0` in all 5 projects (or centralise in `Directory.Build.props`).
2. Bump packages together: ASP.NET Core / Identity / JwtBearer / Mvc.Testing / DataProtection -> 10.0.x,
   EF Core + SqlServer + Design + Tools -> 10.0.x, Microsoft.Extensions.* -> 10.0.x,
   HealthChecks.EntityFrameworkCore -> 10.0.x, `System.Security.Cryptography.*` (drop if now in the shared framework).
3. **Swagger decision**: Swashbuckle 6.9 is not compatible with the Microsoft.OpenApi 2.x that ASP.NET 10 uses.
   Either move to the current Swashbuckle major or to `Microsoft.AspNetCore.OpenApi` like Bikontrol (preferred: consistency).
   Check `Program.cs` lines ~164 and ~282 (`AddSwaggerGen`, `UseSwagger`).
4. `dotnet build -warnaserror` and fix new analyzer warnings.
5. EF Core 10 check: `dotnet ef migrations has-pending-model-changes`. It must report no changes; if it does,
   inspect before creating any migration (SQL Server JSON/type mapping defaults changed across EF versions).
6. `dotnet test` (SQL Server Testcontainers suite, BOLA/CORS/auth tests must stay green). Run Stryker once if time allows.

## Phase 2 - Docker and CI

1. `docker/backend/Dockerfile`: `sdk:10.0` + `aspnet:10.0-alpine` (check the tag exists; keep the same alpine hardening).
2. `.github/workflows/ci.yml`, `mutation.yml`, `sonarcloud.yml`: `dotnet-version: 10.0.x`.
3. `docker compose -f docker-compose.yml -f docker-compose.local.yml up --build`, then smoke test login, create group,
   add expense, settle debt, debt simplification.

## Phase 3 - Frontend to Angular 22

Do one major at a time, never skip.

1. `cd split-it-ui && npx ng update @angular/core@22 @angular/cli@22 @angular/cdk@22 @angular/material@22`.
2. Move builders from `@angular-devkit/build-angular` to `@angular/build` (`ng update` offers the migration).
3. Align TypeScript with what Angular 22 supports (Bikontrol uses ~6.0), update `angular-eslint`, `typescript-eslint`.
4. Node 24 in `docker/frontend/Dockerfile` and `ci.yml` (Bikontrol already uses it).
5. Run `npm run lint`, `npm run build`, `npm run test:ci`, Playwright e2e.
6. Optional, separate PR: replace Karma/Jasmine with Vitest like Bikontrol. Do not mix it with the version bump.

## Phase 4 - Close the known gaps (they affect what the README can claim)

- HTTP-level authorization regression tests (cross-group reads/writes) - listed in the README "Follow-ups".
- Dependency and secret scans in CI (`npm audit`, `dotnet list package --vulnerable`, Gitleaks), as in Bikontrol.

## Phase 5 - Docs and showcase

- README: badges (.NET 10, Angular 22), Tech Stack table, Prerequisites, "Follow-ups" section.
- `AGENTS.md`/`CLAUDE.md` and `docs/` version mentions: `grep -rn "net8\|.NET 8\|Angular 21"`.
- Portfolio (`../santidev21-portfolio-v2/src/assets/data/projects.json`): add the versions to the SplitIt card.
- CV: use the final SplitIt text from the CV notes.

## Deploy and rollback

- Take and verify a DB backup first; the migrator runs EF migrations on deploy.
- Merge to `main` only with all required checks green; watch the deploy health check.
- Rollback: redeploy the previous image tag; no schema change is expected from this upgrade.

## Done when

- `grep -rn "net8" .` has no hits outside the changelog; all CI jobs green on .NET 10 / Node 24.
- `https://splitit.santidev21.tech/health` is OK after deploy and the smoke flow passes.
