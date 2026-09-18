---
name: dotnet-best-practices
description: Ensure .NET 8 / C# code meets SplitIt best practices. Use when writing or reviewing backend code in SplitIt.API/.
---

# .NET/C# best practices (SplitIt)

Applies to `SplitIt.API/` — .NET 8, EF Core 9 (SqlServer), xUnit, SQL Server.

## Architecture & layering
- Keep the Clean Architecture layering: thin controllers, business logic in `SplitIt.Infrastructure/Services`, EF Core confined to `Infrastructure/Persistence`.
- DTOs live in `SplitIt.Application/DTOs`; domain entities live in `SplitIt.Domain/Entities`. Never bind a request directly to an entity or return an entity from a controller.
- Manual mapping is the current pattern (no AutoMapper/MediatR) — stay consistent and keep mapping explicit.
- `SplitIt.Shared` is currently empty; don't add logic there without a reason.

## Async
- Async all the way: return `Task`/`Task<T>`, no `.Result`/`.Wait()`, no `async void`.
- Accept and flow `CancellationToken` on I/O paths where practical.
- Use `ConfigureAwait(false)` in library/service code where appropriate.

## Data access
- EF Core parameterized LINQ only. Never build SQL by string concatenation/interpolation.
- Use `AsNoTracking()` for read-only queries; project to DTOs instead of over-fetching.
- Keep multi-step writes (settlement/payment) inside a transaction so failures don't leave partial state.
- Watch for N+1 and missing indexes on hot filters (`ExpenseShare(UserId, IsSettled)`, `Expense(GroupId)`).
- Respect delete behaviour in `AppDbContext.OnModelCreating` — know which relations cascade before deleting.

## Validation & errors
- Validate server-side on every input endpoint (DataAnnotations + service-level checks). Never trust the client.
- Throw specific exceptions (`ArgumentException`, `UnauthorizedAccessException`, `KeyNotFoundException`) and let `GlobalExceptionHandler` translate them. Don't swallow exceptions into a false-success response.
- No stack traces, SQL or internal messages in production responses.

## DI & services
- Register services with correct lifetimes (`AddScoped` for per-request work). Constructor injection; validate nulls where it matters.
- Prefer interfaces for services you need to mock, but match the existing style of the file.

## Security
- Never log or hardcode secrets, tokens or connection strings.
- Use `IPasswordHasher<User>` for passwords — never a custom hash.
- Enforce ownership (`IsUserMemberAsync`) on every endpoint that takes a `groupId`.

## Testing
- xUnit in `SplitIt.Tests/`; unit tests use EF InMemory via `Helpers/TestDbHelper.cs`, integration tests live in `SplitIt.Tests/Integration/`.
- Follow Arrange-Act-Assert. Cover success and failure/error paths, and authorization negatives.
- Run `dotnet test SplitIt.API/SplitIt.Back.sln` — don't claim green without running it.

## Quality
- SOLID, meaningful names, methods focused and cohesive.
- Standardise time on UTC (`DateTime.UtcNow`), never `DateTime.Now`.
- Money is `decimal`; keep rounding deterministic and validate `sum(shares) == total`.
