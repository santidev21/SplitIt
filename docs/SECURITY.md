# SplitIt — Security Documentation (Phase 0.5)

> Fecha: 2026-08-24
> Estado: Seguridad crítica remediada (no production-ready completo, pero sin 🔴 CRITICAL abiertos)

---

## 1. Secrets Management

### 1.1 Hallazgo original
`appsettings.json` contenía `JwtSettings:SecretKey = SuperSecretKey123456789101112131415` y `ConnectionStrings:DefaultConnection` con `Trusted_Connection=True`. Ambos committeados en `27c8715` y por tanto **comprometidos para siempre** en el historial de git.

### 1.2 Remediación
- `SplitIt.API/SplitIt.API/appsettings.json:1` ahora vacío (`""`) — solo placeholders.
- Nuevos templates: `appsettings.Development.json.example` y `appsettings.Production.json.example`.
- `.env.example` en raíz + `split-it-ui/.env.example`.
- `split-it-ui/src/environments/environment.prod.ts.example` añadido.
- `.gitignore:22` corregido: ya no ignora `package-lock.json` (builds reproducibles), añade `.env` exclusions con `!.env.example`.
- `SplitIt.API/Program.cs:17` ahora falla en Producción si `SecretKey` <32 chars o vacío, con mensaje explícito.
- `SplitIt.API/DependencyInjection.cs:9` warn si `ConnectionStrings:DefaultConnection` vacío.
- `Cors:AllowedOrigins` ahora vía env `Cors__AllowedOrigins`.

### 1.3 Rotación & Git History
> **Se asumen comprometidos.** El secret antiguo debe rotarse y **NO reutilizarse**.

**Pasos recomendados:**
1. Generar nuevo secret: `openssl rand -base64 64` (Linux) o en PowerShell: `[Convert]::ToBase64String((1..64 | % {Get-Random -Max 256}))`
2. Setear en servidor/VPS vía env var `JwtSettings__SecretKey` y local via `dotnet user-secrets set "JwtSettings:SecretKey" "<NEW>"` o `appsettings.Development.json` (no trackeado).
3. Limpiar historial **solo si repo es privado y se coordina con colaboradores**: usar `git filter-repo` o `BFG Repo-Cleaner` para reescribir `appsettings.json` histórico, luego `git push --force` y notificar a todos de re-clonar. Ver `https://docs.github.com/en/authentication/keeping-your-account-and-data-secure/removing-sensitive-data-from-a-repository`.
4. Activar GitHub Secret Scanning: `Settings → Code security → Secret scanning` + `Push protection`.
5. Añadir `gitleaks` pre-commit: `npm: gitleaks` o `pre-commit hook: gitleaks protect --staged`. CI escanea con `gitleaks detect --source . --redact`.

### 1.4 Secret Scanning
- CI debe correr `gitleaks detect --source . --no-git --redact` y fail en `exit !=0`.
- Docker scan (Trivy) también detecta secrets en images.

---

## 2. Password Hashing

### 2.1 Antes
`SplitIt.Infrastructure/Services/AuthService.cs:49` usaba `SHA256(password) → Base64` sin salt, sin work factor. Rápido, rainbow tables triviales (CWE-327).

### 2.2 Ahora
- Paquete `Microsoft.AspNetCore.Identity.EntityFrameworkCore 8.0.15` añadido a `SplitIt.Infrastructure.csproj:10`.
- `AuthService` usa `IPasswordHasher<User>` (`PasswordHasher<User>` por defecto):
  - Algoritmo: PBKDF2-HMAC-SHA256, 128-bit salt, 100.000+ iteraciones, formato Identity V3 (incluye versionado).
  - Nuevos usuarios: `HashPassword` vía `IPasswordHasher`.
  - Login: `VerifyHashedPassword` con soporte `SuccessRehashNeeded`.
- **Migración legacy:**
  ```
  Login → VerifyHashedPassword → if fail → check Legacy SHA256 (44-char Base64) → if match → rehash con PasswordHasher → SaveChanges → success
  ```
  Helpers: `IsLegacySha256Hash`, `VerifyLegacySha256` en `AuthService.cs:62`.

- Tests: `AuthServicePasswordHashingTests` (ver `SplitIt.Tests`) cubren registro, verificación PBKDF2 y migración legacy.

### 2.3 Recomendación futura
Evaluar `Argon2id` (libsodium) si se requiere mayor resistencia GPU; `PasswordHasher` es aceptado como estándar OWASP y suficiente para 2026. Si se migra a Argon2, usar wrapper `IPasswordHasher` custom.

---

## 3. JWT

### 3.1 Cambios en `SplitIt.API/Program.cs:42` + `AuthController.cs:59`
- Unificado a `Encoding.UTF8.GetBytes` (antes ASCII vs UTF8 dispar).
- `SecretKey` validado: min 32 chars, 64+ recomendado, distinto por ambiente, vía env var.
- `TokenValidationParameters`:
  - `ValidateIssuerSigningKey=true`, `ValidateIssuer=true`, `ValidateAudience=true`, `ValidateLifetime=true`, `RequireSignedTokens=true`, `RequireExpirationTime=true`
  - `ClockSkew = TimeSpan.Zero` (antes default 5m → ventana replay)
  - `ValidAlgorithms = [HmacSha256]` + evento `OnTokenValidated` rechaza `alg != HS256` (mitiga `alg:none`).
  - `ValidIssuer/ValidAudience` desde config, obligatorios en Prod.
- `RequireHttpsMetadata = !IsDevelopment()` (antes siempre false).
- Claims: `sub`, `NameIdentifier`, `Email`, `Jti`, `Role` (int RoleId). `exp` via `ExpirationInMinutes` parseado con TryParse.
- Tests: valid token, expired, tampered signature, wrong issuer/audience, missing token (ver `JwtValidationTests`).

### 3.2 Expiración
`ExpirationInMinutes=60` (1h). Sin refresh token aún — logout es client-side. Fase futura debe añadir refresh rotation HttpOnly.

---

## 4. JWT Storage — Decisión

### 4.1 Opciones evaluadas
| Criterio | Option A: localStorage (actual) | Option B: HttpOnly Secure SameSite cookie |
|---|---|---|
| **XSS risk** | Alto si hay XSS (JS puede leer) | Bajo (JS no lee) |
| **CSRF risk** | Nulo (Auth header manual) | Alto si no hay CSRF token (cookie enviada auto) |
| **Angular compat** | Simple, interceptor añade header | Requiere `withCredentials:true`, backend `Set-Cookie`, CORS `AllowCredentials`, CSRF double-submit |
| **SSR** | Funciona | Más complejo |
| **Logout** | Client-side remove | Server-side invalidate |

### 4.2 Decisión Phase 0.5
**Mantener Option A (localStorage) + endurecer XSS**, por:
- Angular SPA sin SSR activo, sin backend cookie infra aún.
- CSRF con cookies añadiría complejidad y riesgo si se implementa mal antes de tener `Antiforgery`.
- La amenaza principal es XSS vía `Title`/`Note`/`Description` — se mitiga con CSP + sanitización + validation.

**Mitigaciones implementadas:**
- `AuthGuard` corr. + interceptor limpia 401.
- DTO validation `[StringLength(500)]` en `Note`/`Description` reduce payload XSS pero no sustituye output encoding.
- Fase 11 (Nginx) añadirá `Content-Security-Policy: default-src 'self'` y Angular sanitiza por defecto (`DomSanitizer`).
- Logs nunca incluyen token.

**Roadmap:** Migrar a `HttpOnly Secure SameSite=Strict` + `RefreshToken` + `/api/auth/refresh` + CSRF `XSRF-TOKEN` en Fase 8 si se alarga sesión.

---

## 5. Authorization (BOLA/IDOR)

### 5.1 Antes
Ningún endpoint verificaba membership: `GET /groups/{id}/members`, `/details`, `/userrole`, `GET /expenses/{groupId}/expenses`, `POST /expenses/add`, `GET /debt-summary`, `POST /settle` permitían acceso cross-group.

### 5.2 Ahora
- Nuevo método `GroupService.IsUserMemberAsync(groupId, userId):bool` (`GroupService.cs:100`).
- Todos los controllers que reciben `groupId` verifican:
  ```csharp
  if (!await _groupService.IsUserMemberAsync(groupId, userId)) return Forbid();
  ```
  Afectados:
  - `GroupsController.cs:67,81,95` → `GetGroupMembers`, `getGroupDetails`, `GetUserGroupRoleAsync`
  - `ExpensesController.cs:45,59,75` → `GetGroupExpenses`, `GetFullDebtSummary`, `SettleExpenseWithUser`
- `ExpensesService.AddExpenseAsync` valida `CreatedById` member, `PaidById` member, `Participants` subset de `GroupMembers`, `sum(AmountOwed)==Amount`.
- `ExpensesService.SettleExpenseWithUser` y `RegisterPayment` ahora reciben `groupId` y filtran `es.Expense.GroupId == groupId` (fix cross-group).
- Tests: `BolaTests` con 2 users, 2 groups — userA no puede leer grupo de userB.

---

## 6. Settlement Cross-Group Bug

**Bug:** `ExpensesService.cs:165` settle ignoraba `groupId`, liquidaba deudas entre dos users en **todos** los grupos.

**Fix:** Firma cambiada a `SettleExpenseWithUser(payerUserId, receiverUserId, groupId)` + `RegisterPayment` valida `groupId` + query scoped `es.Expense.GroupId == groupId`. `ExpensesController.cs:86` ahora pasa `dto.GroupId`.

**Test:** `SettlementCrossGroupTests` crea GroupA (A owes B $100) y GroupB (A owes B $50), settle GroupA → GroupB permanece intacto.

---

## 7. CORS

- Antes: `AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod()` en `Program.cs:93`.
- Ahora: `Program.cs:98` lee `Cors:AllowedOrigins` (o `Cors__AllowedOrigins`) comma-separated.
  - Si configurado → `WithOrigins(allowedOrigins).AllowCredentials()`.
  - Si vacío + Development → solo `http://localhost:4200` + `https://localhost:4200`.
  - Si vacío + Production → **deny all** (fail closed) — debe configurarse.
- `UseCors("AppCors")` antes de `UseAuthentication`.

---

## 8. Mass Assignment

- DTOs ahora explícitos con `[Required]`/`[Range]`/`[StringLength]` — no bind a Entity directa.
- `RegisterRequestDto` no expone `RoleId` (siempre `RoleId=3` hardcoded server-side).
- `CreateGroupDto` ignora `Role` — `AddGroupMembers` setea `"creator"` solo si `memberId == creatorId`.
- `CreateExpenseDto` no permite cliente setear `CreatedById` — se deriva de JWT `NameIdentifier`.
- Tests: `MassAssignmentTests` intentan enviar `RoleId` extra — ignorado.

---

## 9. Input Validation

Todos los DTOs con DataAnnotations (`SplitIt.Application/DTOs/*.cs:1`):
- `RegisterRequestDto`: `Name 2..100`, `EmailAddress`, `Password 8..100`
- `CreateGroupDto`: `Name 2..200`, `Description 1..500`, `CurrencyId Range`
- `CreateExpenseDto`: `Title 1..100`, `Note 0..500`, `Amount 0.01..1M`, `Date required`, `PaidById Range`, `Participants MinLength 1`
- `RegisterPaymentDto`: `PayerUserId Range`, `Amount 0.01..1M`
- Backend valida también en services: `sum == total ±0.02`, `participants member`, `max 50 members/participants`, `dates UTC`.

---

## 10. Exception Handling

- Nuevo `SplitIt.API/Middleware/GlobalExceptionHandler.cs:1` (`IExceptionHandler` .NET 8).
- Registrado `AddExceptionHandler<GlobalExceptionHandler>()` + `AddProblemDetails()` + `UseExceptionHandler()` en `Program.cs:45`.
- Prod nunca devuelve stack traces, SQL errors, paths — solo `ProblemDetails {status, title, detail, traceId}`. `traceId` correlaciona con logs.
- Dev sí incluye `exception.Message` para debugging; logs siempre con `LogError(exception, TraceId)`.

---

## 11. Rate Limiting

- `AddRateLimiter` en `Program.cs:52`:
  - Policy `"auth"`: `FixedWindow 5/min/IP` (partition por `RemoteIp` o `Host`) para `POST /api/auth/login` y `/register` (`[EnableRateLimiting("auth")]` en `AuthController.cs:27,44`).
  - Policy `"fixed"`: `100/min` general (no aplicado global aún, listo para `[EnableRateLimiting("fixed")]` en endpoints sensibles).
- `OnRejected` devuelve `429 {message: "Too many requests..."}` JSON.

---

## 12. Logging

- Logs no incluyen `Password`, `JWT`, `ConnectionString`.
- `GlobalExceptionHandler` usa `ILogger` con `TraceId` + `Path`.
- HSTS en prod (`UseHsts()`).

---

## 13. Remaining Risks (Phase 0.5)

- **XSS residual:** localStorage sigue vulnerable si hay XSS almacenado. Mitigado pero no eliminado hasta CSP + HttpOnly futuro.
- **No refresh token rotation / revocation:** stolen JWT válido 60m.
- **No pagination:** `GetGroupMembers` y `GetUsers` sin paginación — DoS vía large groups (limit 50 mitiga pero no paginación).
- **DB backup/restore no implementado** — fuera de alcance Phase 0.5.
- **Container/Docker hardening no implementado** — Fase 9.

---

## 14. Verification Checklist Phase 0.5

- [x] `appsettings.json` sin secretos
- [x] `SHA256` eliminado, `PasswordHasher` + legacy rehash
- [x] `authGuard` corr.
- [x] BOLA checks en todos `groupId` endpoints
- [x] Settlement scoped por `groupId`
- [x] CORS sin `AllowAnyOrigin` en prod
- [x] JWT `ClockSkew=0`, `UTF8`, `RequireHttpsMetadata`, alg validation
- [x] JWT storage decisión documentada
- [x] DTO validation
- [x] Global exception handler sin leak
- [x] Rate limiting auth 5/min

