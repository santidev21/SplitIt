---
name: security-review
description: Security audit checklist for SplitIt. Use when reviewing authentication, JWT/refresh tokens, authorization, input validation, secrets, CORS, SQL/EF queries, XSS, dependencies, or any security-sensitive change.
---

# Security review (SplitIt)

Read-only audit. Report findings with severity and `path:line` evidence. Never log or copy secret values — reference the variable name only.

## Secrets & config
- JWT key, DB passwords and connection strings live only in `.env` / `appsettings.Development.json` (both gitignored). Never in code, `appsettings.json`, Angular `environment.ts`, or git history.
- The Angular bundle is public: anything under `split-it-ui/src/environments/*` is visible to users — no secrets there, ever.
- `.example` templates contain placeholders only.
- Flag any committed secret as Critical; recommend rotation, not just removal. The old `SuperSecretKey...` in history is assumed compromised (see `docs/SECURITY.md`).

## Authentication & sessions
- Passwords hashed with `IPasswordHasher<User>` (PBKDF2); legacy SHA256 logins must rehash on success. Never stored or logged in plaintext.
- JWT: HS256 only (`ValidAlgorithms` + `OnTokenValidated` alg check), `ClockSkew = TimeSpan.Zero`, `ValidateIssuer/Audience/Lifetime`, `RequireHttpsMetadata = !IsDevelopment()`.
- Access token in `localStorage` is the accepted trade-off — it makes XSS prevention non-negotiable; report it as a known risk with a note, not a false Critical.
- Refresh tokens: stored as `TokenHash` (never raw), revocable/rotating, delivered as an HttpOnly cookie (`refresh_token`). Password-reset tokens are single-use and time-bound.
- Login/register/password-recovery/refresh are rate-limited (policy `auth`, 5/min/IP; HTTP 429).
- Password recovery responses don't reveal whether an email exists.
- Google OAuth: validate the `id_token` server-side (issuer, audience, expiry) — never trust the client payload.

## Authorization
- Every non-public endpoint has `[Authorize]` (or is intentionally anonymous — flag and confirm). `AdminController` is `[Authorize(Roles = "SuperAdmin,Admin")]` with SuperAdmin-only mutations.
- **Ownership checks**: a user can only read/modify groups, members, expenses, debts and settlements of groups they belong to. `groupService.IsUserMemberAsync(groupId, userId)` must gate every `groupId` endpoint. Missing check = Critical (BOLA/IDOR).
- Admin role checks use `RoleConstants`, not magic ints/strings. Flag fragile role comparisons.
- No endpoint can hard-delete or resurrect another user's data.

## Input validation
- Server-side validation on every input endpoint via DataAnnotations + `ModelState`/service checks; never rely on Angular validation.
- Validate ids, route params, enums and numeric ranges. Reject unknown/extra fields (mass assignment).
- Respect business limits (`SettingsService.MaxExpenseAmount`, `RegistrationEnabled`, max 50 members/participants per group).

## Data access
- EF Core parameterized LINQ only. Flag `FromSqlRaw`/`ExecuteSqlRaw` built by interpolation or concatenation.
- No over-fetching of sensitive columns; project to DTOs.
- Check that `catch { return Ok(...) }` blocks (see `AdminController`) don't swallow real failures into a false-success response.

## Errors & logging
- Production must not leak stack traces, SQL or internal messages. `GlobalExceptionHandler` returns generic `ProblemDetails` with `traceId`.
- Logs must not contain tokens, passwords or full PII beyond what's needed.
- Failed auth attempts are logged (without credentials).

## API surface
- CORS is an explicit allow-list from `Cors:AllowedOrigins`; production fails closed. No `AllowAnyOrigin` with credentials.
- HTTPS enforced in production; HSTS on. Swagger/health endpoints not verbose in production.
- Security headers present at the proxy (CSP, X-Content-Type-Options, X-Frame-Options/frame-ancestors, Referrer-Policy).
- Request body size limits and pagination to prevent abuse (`GetUsers`, `GetGroupMembers`).

## Frontend
- XSS: no `[innerHTML]`, `bypassSecurityTrustHtml/Url/ResourceUrl` on user-controlled data.
- Never build URLs/redirects from unvalidated query params (open redirect).
- SweetAlert2 content not used with raw HTML from the server.
- No secrets/API keys baked into the bundle.

## Dependencies
- Run `npm audit` / `dotnet list package --vulnerable` when dependencies change. Angular 19 is out of support — treat the pending upgrade as security work.
- Pinned versions; flag unexplained ranges on security-sensitive packages.

## Output format
For each finding, use:
```
[Severity: Critical|High|Medium|Low] Title
Where: path:line
Impact: what an attacker could do
Fix: concrete remediation
```
Order by severity. If a category is clean, say so explicitly — do not invent issues.
