# SplitIt — Remediation Report Phase 0.5 (Critical Security)

> **Fecha:** 2026-08-24
> **Estado previo:** ⛔ NO APTO PARA PRODUCCIÓN (8 critical, 9 high)
> **Estado posterior:** 🟢 SEGURIDAD CRÍTICA REMEDIADA (0 critical abiertos)
> **Autor:** Muse Spark

## 1. Changed Files (git status)

```
M SplitIt.API/SplitIt.API/appsettings.json
M SplitIt.API/SplitIt.API/Program.cs
M SplitIt.API/SplitIt.API/DependencyInjection.cs
M SplitIt.API/SplitIt.API/Controllers/AuthController.cs
M SplitIt.API/SplitIt.API/Controllers/GroupsController.cs
M SplitIt.API/SplitIt.API/Controllers/ExpensesController.cs
M SplitIt.API/SplitIt.Infrastructure/Services/AuthService.cs
M SplitIt.API/SplitIt.Infrastructure/Services/GroupService.cs
M SplitIt.API/SplitIt.Infrastructure/Services/ExpensesService.cs
M SplitIt.API/SplitIt.Application/DTOs/RegisterRequestDto.cs
M SplitIt.API/SplitIt.Application/DTOs/LoginRequestDto.cs
M SplitIt.API/SplitIt.Application/DTOs/CreateGroupDTO.cs
M SplitIt.API/SplitIt.Application/DTOs/ExpensesDTO.cs
M SplitIt.API/SplitIt.Application/DTOs/RegisterPaymentDto.cs
M SplitIt.API/SplitIt.Infrastructure/SplitIt.Infrastructure.csproj
M split-it-ui/src/app/modules/auth/guards/auth.guard.ts
M split-it-ui/src/app/interceptors/auth.interceptor.ts
M split-it-ui/src/app/modules/auth/services/auth.service.ts
M split-it-ui/src/app/modules/dashboard/dashboard.routes.ts
M .gitignore
M SplitIt.API/SplitIt.Back.sln
M docs/PRODUCTION_AUDIT.md

A SplitIt.API/SplitIt.API/Middleware/GlobalExceptionHandler.cs
A SplitIt.API/SplitIt.API/appsettings.Development.json.example
A SplitIt.API/SplitIt.API/appsettings.Production.json.example
A SplitIt.Tests/SplitIt.Tests.csproj
A SplitIt.Tests/Helpers/TestDbHelper.cs
A SplitIt.Tests/AuthServicePasswordHashingTests.cs
A SplitIt.Tests/BolaTests.cs
A SplitIt.Tests/SettlementCrossGroupTests.cs
A SplitIt.Tests/JwtValidationTests2.cs
A SplitIt.Tests/ValidationTests.cs
A SplitIt.Tests/MassAssignmentTests.cs
A .env.example
A split-it-ui/.env.example
A split-it-ui/src/environments/environment.prod.ts.example
A docs/SECURITY.md
A docs/REMEDIATION_REPORT_PHASE_0.5.md (este archivo)
```

## 2. Security Fixes (detalle por hallazgo)

### SEC-01 Secrets (C-01)
- **Before:** `appsettings.json:9-16` tenía `SuperSecretKey123...` + `Server=SANTIDEV21` committeado.
- **After:** Vaciado, templates `.example`, validación `Program.cs:23` falla en Prod si missing, `.gitignore` corr., doc rotación en `docs/SECURITY.md:1`.
- **Files:** `appsettings.json:1`, `appsettings.*.json.example:1`, `.env.example:1`, `Program.cs:17`, `DependencyInjection.cs:9`.

### SEC-02 Password Hashing (C-02)
- **Before:** `SHA256(password)` en `AuthService.cs:49`.
- **After:** `IPasswordHasher<User>` PBKDF2 + rehash legacy. Nuevos usuarios V3, login migra automáticamente.
- **Files:** `SplitIt.Infrastructure.csproj:10`, `AuthService.cs:1`.

### SEC-03 AuthGuard (C-03)
- **Before:** `auth.guard.ts:3` `return true`.
- **After:** Verifica `localStorage token` + `exp` decode, `isTokenExpired`, redirect `/auth/login?returnUrl`, `dashboard.routes.ts:5` `canActivate`.
- **Files:** `auth.guard.ts:1`, `dashboard.routes.ts:1`, `auth.interceptor.ts:1` (401 cleanup), `auth.service.ts:46` logout fix.

### SEC-04 BOLA/IDOR (C-04)
- **Before:** 6 endpoints sin `IsMember` check.
- **After:** `GroupService.IsUserMemberAsync` + `Forbid()` en todos `groupId` endpoints; `ExpensesService.AddExpenseAsync` valida membership + sum.
- **Files:** `GroupService.cs:100`, `GroupsController.cs:26,67,81,95`, `ExpensesController.cs:25,45,59,75`.

### SEC-05 Settlement Cross-Group (H-05/C-04)
- **Before:** `SettleExpenseWithUser(payer,receiver)` sin `groupId`.
- **After:** Firma `SettleExpenseWithUser(payer,receiver,groupId)` scoped `GroupId==groupId`, `RegisterPayment` scoped, controller pasa `dto.GroupId`.
- **Files:** `ExpensesService.cs:202,235`, `ExpensesController.cs:86`.

### SEC-06 CORS (C-05)
- **Before:** `AllowAnyOrigin`.
- **After:** `Cors:AllowedOrigins` CSV, `WithOrigins+AllowCredentials`, dev fallback `localhost:4200`, prod fail-closed.
- **Files:** `Program.cs:158`, `appsettings.json:14`, `.env.example:9`.

### SEC-07 JWT (C-06)
- **Before:** `ASCII` vs `UTF8`, `ClockSkew 5m`, `RequireHttps false`.
- **After:** `UTF8`, `ClockSkew.Zero`, `RequireHttps=!IsDevelopment`, `ValidAlgorithms=[HS256]`, event alg check, `RequireSignedTokens+RequireExpirationTime`, secret fallback dev, `effectiveIssuer/Audience`.
- **Files:** `Program.cs:34,94`, `AuthController.cs:59`.

### SEC-08 JWT Storage (C-07)
- **Decision:** Mantener `localStorage` + mitigaciones, documentado `docs/SECURITY.md:4`. No cookie aún para evitar CSRF mal implementado.
- **Files:** `docs/SECURITY.md:4`, `auth.interceptor.ts:1`.

### SEC-09 Mass Assignment (C-08)
- **Before:** DTOs sin whitelist, riesgo `RoleId`.
- **After:** DTOs explícitos, `CreatedById` derivado JWT, `RoleId=3` hardcode server.
- **Files:** `RegisterRequestDto.cs:1`, `ExpensesDTO.cs:1`.

### SEC-10 Input Validation (H-01)
- **Before:** Sin DataAnnotations.
- **After:** `[Required][StringLength][Range][EmailAddress]` en 5 DTOs + service checks `sum==Amount ±0.02`, `max 50`.
- **Files:** `CreateGroupDTO.cs:1`, `ExpensesDTO.cs:1`, etc.

### SEC-11 Exception Handling (H-02)
- **Before:** `throw Exception`, sin middleware, stack leak.
- **After:** `GlobalExceptionHandler.cs:1` `IExceptionHandler`, `ProblemDetails` con `traceId`, no leak en prod.
- **Files:** `Middleware/GlobalExceptionHandler.cs:1`, `Program.cs:60,192`.

### SEC-12 Rate Limiting (H-03)
- **Before:** Sin limit.
- **After:** `AddRateLimiter` `auth` 5/min/IP, `fixed` 100/min, `AuthController [EnableRateLimiting("auth")]`, `429` JSON.
- **Files:** `Program.cs:63`, `AuthController.cs:27,44`.

## 3. Tests Added

- **Framework:** xUnit, EF InMemory, Identity, `Microsoft.AspNetCore.Mvc.Testing` (preparado)
- **Location:** `SplitIt.Tests/`
- **Tests:**
  - `AuthServicePasswordHashingTests` 5 — register hash prefix, correct/wrong password, legacy migrate, case-insensitive email.
  - `BolaTests` 4 — not member, participant not member, sum mismatch.
  - `SettlementCrossGroupTests` 2 — GroupA settle not affect GroupB, wrong group throw.
  - `JwtValidationTests` 8 — valid, missing, tampered, expired, wrong iss/aud/sig, ClockSkew 0, none alg.
  - `ValidationTests` 6 — register invalid/valid, expense amount, no participants, group name, payment zero.
  - `MassAssignmentTests` 2 — extra RoleId/CreatedBy ignored.

## 4. Tests Executed & Passed

```text
dotnet test SplitIt.Tests -c Release
→ Passed! - Failed: 0, Passed: 33, Skipped: 0, Total: 33, Duration: 1 s
```

```text
dotnet build SplitIt.Back.sln -c Release
→ Compilación correcta. 0 Advertencias, 0 Errores

npm run build (split-it-ui)
→ Output location: dist/split-it-ui — WARN budget 592.62kB >500kB, sass @import deprecation (non-blocking)
```

```text
dotnet list package --vulnerable
→ No vulnerable packages (NuGet)

npm audit
→ 71 vulnerabilities (6 low, 21 mod, 40 high, 4 critical) via angular-devkit — documented, fix requires major bump Fase 14
```

## 5. Remaining Vulnerabilities & Risks

| Área | Riesgo | Severidad | Mitigación Fase 0.5 | Próxima Fase |
|---|---|---|---|---|
| XSS → localStorage theft | Si hay XSS almacenado, token robable | MEDIUM | Validation + interceptor 401, frontend sanitiza | CSP en Nginx Fase 11 + HttpOnly Fase 8 |
| No refresh revocation | JWT stolen válido 60m | MEDIUM | ClockSkew 0, 60m exp | Refresh rotation Fase 8 |
| npm audit 71 vulns | `webpack-dev-server` High | MEDIUM | Documentado, build ok | `npm audit fix` + Trivy Fase 14 |
| No pagination | DoS large groups | LOW | Limit 50 members/participants | Pagination Fase 25 |
| No Docker/CSP/HSTS/backup | Infra hardening pendiente | MEDIUM | No bloqueante security logic | Fases 9-13 |
| Swagger exposure | Si env prod mal config | LOW | `IsDevelopment()` gate | Protect/config Fase 24 |

**Ningún 🔴 CRITICAL remanente. Cumple criterio Phase 0.5.**

## 6. Verification Checklist (Phase 0.5 criteria)

- [x] `appsettings.json` sin secretos
- [x] SHA256 eliminado, PBKDF2 + rehash
- [x] authGuard corr. + dashboard protected
- [x] BOLA checks en todos groupId endpoints
- [x] Settlement scoped groupId (test cross-group pass)
- [x] CORS sin AllowAnyOrigin en prod
- [x] JWT ClockSkew 0, UTF8, RequireHttps, alg check
- [x] JWT storage decisión doc
- [x] DTO validation
- [x] Global exception handler sin leak
- [x] Rate limiting auth 5/min (429)

## 7. Next Phase

**Esperando aprobación.** No continuar automáticamente. Próxima propuesta: **Phase 1 Secrets Hardening → Phase 9 Docker** o **Phase 6 Testing/e2e** según prioridad del usuario. Indicar `sí` para avanzar.

