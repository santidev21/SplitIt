# ADR-005 — .NET 10 and Angular 22 baseline

- **Status:** Accepted
- **Date:** 2026-11-02
- **Supersedes:** [ADR-002](002-framework-baseline.md)

## Context

.NET 8 leaves support on 2026-11-10. The backend was already mixed (EF Core 9 on .NET 8
packages, SDK 9 image building for an 8.0 runtime), and Swashbuckle does not support the
Microsoft.OpenApi version used by ASP.NET Core 10.

## Options

1. Move to .NET 9 (STS) and upgrade again within a year.
2. Move to .NET 10 (LTS) and Angular 22 together, with Microsoft.AspNetCore.OpenApi + Scalar.
3. Stay on .NET 8 past its end of support.

## Decision

Use **.NET 10** (SDK pinned in `global.json`), **Angular 22** with `@angular/build`, TypeScript 6
and Node 24. API documentation is the native OpenAPI document (`/openapi/v1.json`) plus Scalar,
both Development-only. The upgrade landed as its own change; Karma/Jasmine is kept and a move to
Vitest is a separate decision.

## Consequences

- Supported LTS runtime until 2028; one stack across SplitIt and Bikontrol.
- Angular 22 makes OnPush the default; 20 components are explicitly pinned to
  `ChangeDetectionStrategy.Eager` and the `prefer-on-push` lint rule is a warning until they migrate.
- The upgrade surfaced `Forbid(message)` calls that returned 500 instead of 403; they now return 403.
