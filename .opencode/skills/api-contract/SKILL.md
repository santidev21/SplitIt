---
name: api-contract
description: Keep the .NET API and Angular client in sync when a contract changes. Use when changing DTOs, controllers, endpoints, Angular models, services calling /api, or response shapes.
---

# API contract sync

SplitIt has no codegen — contracts are synced by hand. When a backend DTO, controller route, or response shape changes, do all of these in the same pass:

1. Backend: DTO in `SplitIt.Application`, controller in `SplitIt.API`, server-side validation on the endpoint.
2. Frontend: matching TypeScript type/interface and the Angular service that calls `/api/*`.
3. Tests: backend test in `SplitIt.Tests/` and frontend `.spec.ts` covering the new shape.
4. Docs: update `ai-context/specs/` and `AGENTS.md` if behavior changed.

Checklist before finishing:
- `dotnet build SplitIt.API/SplitIt.Back.sln -c Release` passes.
- `npm run build -- --configuration production` (from `split-it-ui/`) passes.
- No hardcoded response strings in the UI — use the i18n dictionaries (see `i18n`).
