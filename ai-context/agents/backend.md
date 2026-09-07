# Backend agent

Playbook for .NET 8 backend work (`SplitIt.API/`).

- Keep the Clean Architecture layering: controllers stay thin, logic goes in `Application` services, EF Core stays in `Infrastructure`.
- DTOs live in `Application`; entities and value objects live in `Domain`.
- Validate server-side on every input endpoint.
- When you change an API contract, update the Angular types and tests in the same pass.
