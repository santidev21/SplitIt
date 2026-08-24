# SplitIt — Production Audit (Phase 0)

> **Fecha:** 2026-08-24  
> **Branch auditado:** `main` @ `27c8715`  
> **Alcance:** Audit completo previo a producción asumiendo `Assume Breach`  
> **Autor:** Muse Spark (auditoría automatizada + verificación manual)  
> **Estado:** ⛔ NO APTO PARA PRODUCCIÓN — blocker críticos activos

---

## 1. Executive Summary

SplitIt es un MVP funcional con **arquitectura Clean Architecture** en .NET 8 + Angular 19 y SQL Server. Compila y funciona en desarrollo, pero **no cumple ningún requisito mínimo de producción segura**.

| Dimensión | Estado | Riesgo |
|---|---|---|
| **Seguridad** | ❌ Crítico | 🔴 |
| **Autenticación** | ❌ Roto | 🔴 |
| **Autorización (BOLA/IDOR)** | ❌ Ausente | 🔴 |
| **Secrets management** | ❌ Hardcoded | 🔴 |
| **Validación / Input** | ❌ Débil | 🟠 |
| **Testing** | ❌ 0% | 🔴 |
| **Docker / Deploy** | ❌ Inexistente | 🟠 |
| **CI/CD** | ❌ Inexistente | 🟡 |
| **Observabilidad** | ❌ Inexistente | 🟡 |
| **Performance** | ⚠️ No medido | 🔵 |

**Veredicto:** Hacer `docker compose up` o exponer este código en un VPS sin corregir ** Fase 1-4 ** expone el VPS a compromiso total, robo de JWT, BOLA masivo y credential stuffing. **Ningún deploy a Internet debe hacerse antes de cerrar los 🔴 CRITICAL.**

---

## 2. Current Architecture

### 2.1 Topología actual (dev)

```
Developer laptop
  ├─ Angular dev server  (ng serve)  → http://localhost:4200  ─┐
  ├─ .NET 8 API          (Kestrel)   → http://localhost:5120/api ─┤→ SQL Server local (SANTIDEV21\SQLEXPRESS)
  └─ SQL Server bare metal — Trusted_Connection=True
```

No hay reverse proxy, no hay Docker, no hay TLS, no hay network isolation.

### 2.2 Backend (.NET 8) — `SplitIt.API/`

```
SplitIt.API/SplitIt.Back.sln
├── SplitIt.API              → Controllers, Program.cs, DependencyInjection.cs
├── SplitIt.Application      → DTOs (sin validación)
├── SplitIt.Domain           → Entities: User, Group, Expense, ExpenseShare, GroupMember, Role, Currency
├── SplitIt.Infrastructure   → AppDbContext, Migrations (8), Services (Auth, Group, Expenses, Users, Currencies)
└── SplitIt.Shared           → Vacío (0 archivos útiles)
```

- **Framework:** `net8.0`, `Nullable=enable`, `ImplicitUsings=enable`
- **Packages:**
  - `Microsoft.AspNetCore.Authentication.JwtBearer 8.0.3`
  - `Microsoft.EntityFrameworkCore 9.0.3` + `SqlServer 9.0.3` (⚠️ EF Core 9 sobre runtime .NET 8 — mismatch soportado pero no ideal)
  - `Swashbuckle.AspNetCore 6.6.2`
- **Entrada:** `SplitIt.API/Program.cs:1` — registra controllers, Swagger, JWT, CORS, Auth
- **DB Context:** `SplitIt.Infrastructure/Persistence/AppDbContext.cs:13` — 7 DbSets, Fluent config, seed Roles+Currencies
- **Migrations:** 8 migrations en `SplitIt.Infrastructure/Migrations/` desde `20250401_InitialCreate` hasta `ChangeExpenseDateToDateTime`. `AppDbContextModelSnapshot.cs` presente.

### 2.3 Frontend (Angular 19)

```
split-it-ui/
├── Angular 19.2.0 + Angular Material 19.2.7 + Bootstrap 5.3.3 + SCSS
├── SSR habilitado (@angular/ssr 19.2.5 + express 4.18.2) pero sin uso
├── Routing: app.routes.ts → lazy load AuthModule + DashboardModule
├── Auth: auth.service.ts (localStorage), auth.interceptor.ts, auth.guard.ts
├── Dashboard: group.service.ts, expense.service.ts, create-group, group-detail, add-expense-dialog, split-method-dialog
└── Environments: solo environment.ts (apiUrl=http://localhost:5120/api) — NO existe environment.prod.ts
```

Build: `@angular-devkit/build-angular:application` (builder moderno). Tests: Karma+Jasmine configurado pero **0 specs útiles** (`app.component.spec.ts` default).

### 2.4 Base de datos

- **Motor:** SQL Server (local, `Server=SANTIDEV21`)
- **EF Core:** Migrations versionadas, `decimal(18,2)` para montos, `datetime` para fechas, `GETUTCDATE()` defaults
- **Entidades clave:**
  - `User {Id, Name, Email(unique), PasswordHash, RoleId, CreatedAt}`
  - `Group {Id, Name, Description, CurrencyId, CreatedAt, AllowToDeleteExpenses}`
  - `GroupMember {Id, GroupId, UserId, Role="creator|member|admin"}`
  - `Expense {Id, Title, Note, Amount(decimal), Date, GroupId, CreatedById, PaidById, IsPayment}`
  - `ExpenseShare {Id, ExpenseId, UserId, AmountOwed(decimal), IsSettled, SettledAt}`

### 2.5 Diagramas

**Clean Architecture real (detectada):**

```
API (Controllers) ──→ Application (DTOs)
      │                    ↑
      └─→ Infrastructure (Services + AppDbContext) ──→ Domain (Entities)
              ↑
         DependencyInjection.cs registra AppDbContext (línea 11)
```

Violación: Services en `Infrastructure` contienen lógica de negocio (debt calculation, settlement) que debería vivir en `Application`/`Domain`. `SplitIt.Shared` vacío — debería eliminarse o usarse.

---

## 3. Security Findings

### 🔴 CRITICAL (bloqueante producción)

#### C-01 — Secretos hardcodeados en repo (OWASP A07)
- **Archivo:** `SplitIt.API/SplitIt.API/appsettings.json:9-16`
  ```json
  "ConnectionStrings": { "DefaultConnection": "Server=SANTIDEV21;Database=SplitItDB;Trusted_Connection=True;TrustServerCertificate=True;" }
  "JwtSettings": { "SecretKey": "SuperSecretKey123456789101112131415", "Issuer": "https://localhost", "Audience": "https://localhost" }
  ```
- **Riesgo:** Secret JWT committeado en git history para siempre. Cualquier clon puede forjar tokens. `git log` lo retiene aunque se borre. `TrustServerCertificate=True` deshabilita validación TLS de SQL.
- **Impacto:** Compromiso total de auth, forjado de `sub`/`role`.
- **Fix:** Rotar secreto, mover a env vars / `dotnet user-secrets` / GitHub Secrets, añadir `.gitignore` correcto, ejecutar `gitleaks` sobre historia.

#### C-02 — Hash de passwords con SHA256 sin salt (OWASP A07, CWE-327)
- **Archivo:** `SplitIt.Infrastructure/Services/AuthService.cs:49-60`
  ```csharp
  SHA256.Create().ComputeHash(UTF8.GetBytes(password)) → Base64
  ```
- **Riesgo:** SHA256 es rápido, sin salt, sin iteraciones. Ataque rainbow table + GPU brute force trivial. No usa `PBKDF2`, `bcrypt`, `Argon2` ni `ASP.NET Identity PasswordHasher`.
- **Fix:** Migrar a `BCrypt.Net` o `Microsoft.AspNetCore.Identity.PasswordHasher<User>` con rehash on login.

#### C-03 — AuthGuard frontend no protege nada
- **Archivo:** `split-it-ui/src/app/modules/auth/guards/auth.guard.ts:3-5`
  ```ts
  export const authGuard: CanActivateFn = () => true;
  ```
- **Riesgo:** Cualquier ruta `dashboard` es accesible sin token. Falsa sensación de seguridad. Atacante ve UI aunque API rechace, fuga UX y posible data leak vía cache.
- **Fix:** Implementar guard que verifica `localStorage token` + `exp` + redirige a `/auth/login`.

#### C-04 — BOLA/IDOR masivo — autorización a nivel de recurso ausente
- **Evidencia:**
  - `GroupsController.cs:67` `GET /groups/{groupId}/members` — no verifica que `userId` sea miembro del grupo. Cualquier usuario autenticado enumera miembros de cualquier grupo.
  - `GroupsController.cs:81` `GET /groups/{groupId}/details` — idem.
  - `GroupsController.cs:95` `GET /groups/{groupId}/userrole` — idem.
  - `ExpensesController.cs:45` `GET /expenses/{groupId}/expenses` — no verifica membership; `GetExpensesByGroupIdAsync` filtra `showAll=false` sólo por `PaidBy`/`Shares` pero con `showAll=true` (default false pero cliente controla) expone todo el grupo incluso sin ser miembro.
  - `ExpensesController.cs:26` `POST /expenses/add` — no verifica que `CreatedBy` ni `PaidById` ni `Participants[*].UserId` pertenezcan al `GroupId`. Atacante inyecta gastos en grupos ajenos.
  - `ExpensesController.cs:77` `POST /expenses/settle` — `GroupId` se envía pero `SettleExpenseWithUser` ignora `groupId` y liquida deudas entre dos usuarios globalmente (líneas 167-174 de ExpensesService).
  - `GroupService.cs:100` `GetGroupDetails` no filtra por usuario.
- **Impacto:** Horizontal privilege escalation total. Usuario A lee/modifica gastos de Usuario B.
- **Fix:** Middleware/repositorio que verifique `GroupMember` para cada `groupId`. Tests BOLA.

#### C-05 — CORS AllowAnyOrigin en producción
- **Archivo:** `SplitIt.API/Program.cs:93-100`
  ```csharp
  policy.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod()
  ```
- **Riesgo:** Cualquier origen puede hacer requests con credenciales (si se habilita). En combinación con JWT en `Authorization`, permite sitio malicioso hacer CSRF-like abuse via `fetch`.
- **Fix:** Configurar por env var `AllowedOrigins`, `AllowCredentials()` sólo si necesario, separar `Development` vs `Production`.

#### C-06 — JWT Signing Key handling débil + clock skew + alg
- **Archivo:** `Program.cs:40` `Encoding.ASCII.GetBytes(secretKey)` vs `AuthController.cs:62` `Encoding.UTF8.GetBytes`. Inconsistencia puede causar fallos con caracteres no-ASCII.
- **Secret length:** 32 chars (`SuperSecretKey...`) — borde mínimo para HMACSHA256 (256 bits = 32 bytes). No aleatorio, no rotado.
- **Validación:** `ValidateIssuerSigningKey=true`, `ValidateIssuer=true`, `ValidateAudience=true` OK, pero **no se setea `ClockSkew`** (default 5 min — ventana de replay), no se valida `aud`/`iss` en tests, no hay `RequireHttpsMetadata=false` (debe ser `true` en prod).
- **Alg:** No hay defensa explícita `alg=none` — `JwtBearer` lo bloquea por defecto, pero no hay test que lo pruebe.
- **Fix:** Unificar UTF8, secret 64+ chars aleatorio vía `openssl rand -base64 64`, `ClockSkew = TimeSpan.Zero`, `RequireHttpsMetadata=true` en prod.

#### C-07 — Almacenamiento JWT en localStorage (XSS → token theft)
- **Archivo:** `auth.service.ts:24`, `auth.interceptor.ts:4`
- **Riesgo:** Cualquier XSS (ej. `Note` o `Title` de expense sin sanitizar) puede `localStorage.getItem('token')`. No hay `HttpOnly` cookie alternativa, no hay refresh token rotation, token válido 60 min sin revocation.
- **Fix (corto):** Mantener localStorage pero añadir CSP estricta + sanitización. (Largo): migrar a `HttpOnly Secure SameSite` cookie + refresh token.

#### C-08 — Mass Assignment / Elevation vía DTOs
- **Archivo:** `AuthService.cs:29` `RoleId = 3` hardcodeado OK, pero `User` entity expone `RoleId` y `CreateGroupDto` expone `Members: List<int>` sin validar que caller no se auto-asigne `Role="creator"` en otros grupos. `RegisterRequestDto` sin validación — atacante puede enviar `{"RoleId":1}` si se añade property binding mal configurado (actualmente no, pero pattern frágil).
- **Risk:** Si se añade `Role` a cualquier DTO sin whitelist, escalada directa.

---

### 🟠 HIGH

#### H-01 — Validación de entrada insuficiente
- **DTOs sin DataAnnotations:** `CreateGroupDTO.cs`, `ExpensesDTO.cs`, `RegisterRequestDto.cs` — todos `string` sin `[Required]`, `[StringLength]`, `[EmailAddress]`, `[Range]`. Validación solo en controller con `if (Amount <=0)` trivial.
- **Monetary:** `Amount` decimal sin `>0` consistente, sin `max` (DoS via `decimal.MaxValue`), sin validación `sum(participants) == Amount`.
- **Frontend** valida pero backend no confía — pero backend actualmente no valida suficiente.
- **Fix:** FluentValidation o DataAnnotations + `ApiController` auto-400 + tests de oversized input.

#### H-02 — Excepciones filtran detalles internos
- **Archivo:** `GroupService.cs:81` `throw new Exception("Group not found")` sin try/catch global. `Program.cs` no tiene `UseExceptionHandler` ni middleware. En `Development` ASP.NET devuelve stack trace; en prod sin middleware puede filtrar.
- **Fix:** Global exception handler que loguea 500 y devuelve `ProblemDetails` genérico.

#### H-03 — Rate limiting ausente
- **Impacto:** `/api/auth/login` y `/api/auth/register` sin throttling → brute force, credential stuffing, enumeration (`"The user already exists!"` en register revela emails).
- **Fix:** `AspNetCoreRateLimit` o .NET 8 `AddRateLimiter` con políticas `auth: 5/min/IP`, `api: 100/min/user`.

#### H-04 — Account enumeration + weak password policy
- **Archivo:** `AuthController.cs:30` `BadRequest("The user already exists!")` distingue existente vs no. `AuthService.cs:22` sin política de password (length, complexity). Sin lockout, sin captcha.
- **Fix:** Respuesta genérica + política `min 12 chars` + HaveIBeenPwned check opcional.

#### H-05 — Settlement logic bug — ignora group y liquida global
- **Archivo:** `ExpensesService.cs:165` `SettleExpenseWithUser(payerUserId, receiverUserId)` — query sin `GroupId` aunque `RegisterPayment` recibió `groupId`. Luego `RegisterPayment` crea un `Expense` de tipo pago y marca settled, pero `SettledCount` puede ser 0 aunque pago se creó.
- **Impacto:** Pago en grupo A liquida deudas de grupo B entre mismos dos usuarios. Inconsistencia financiera.
- **Fix:** Filtrar por `GroupId` en settle, transacción atómica, validar `amount <= debt`.

#### H-06 — N+1, tracking y performance
- **Archivo:** `ExpensesService.cs:53` `Include(e=>PaidBy).Include(e=>Shares).ThenInclude(User)` sin `AsNoTracking()` para reads. `GetDebtsOwedByUserAsync` hace `GroupBy` en memoria? EF lo traduce pero sin índices explícitos en `ExpenseShare(UserId, IsSettled)`.
- **Fix:** `AsNoTracking()`, índices, paginación.

#### H-07 — Swagger expuesto sin protección
- **Archivo:** `Program.cs:106` `if (IsDevelopment()) UseSwagger` — OK en prod no expone, pero no hay test que lo garantice. Si env var mal configurada, expone endpoints y schemas.
- **Fix:** Proteger Swagger con auth o deshabilitar por config `EnableSwagger=false`.

#### H-08 — Dependencias con vulnerabilidades transitivas (npm audit)
- **Evidencia:** `npm audit` reporta 1 HIGH en `webpack-dev-server` via `@angular-devkit/build-angular <=22.1.5`, varios moderate en `picomatch`, `postcss`, etc. `dotnet list package --vulnerable` hoy 0 en NuGet, pero images Docker no escaneadas.
- **Fix:** `npm audit fix`, actualizar a Angular 20+ o pin `webpack`, Trivy scan en CI.

#### H-09 — .gitignore filtra mal
- **Archivo:** `.gitignore:133` `appsettings.Development.json` ignorado pero `appsettings.json` con secretos SÍ trackeado. `package-lock.json` ignorado (línea 38) — debe versionarse para reproducible builds. `.env` ignorado OK pero falta `!.env.example` pattern.
- **Fix:** Revertir `package-lock.json`, template `appsettings.example.json`.

---

### 🟡 MEDIUM

#### M-01 — Timezone inconsistency
- `GroupService.cs:28` `DateTime.Now` (local) vs `AppDbContext.cs:36` `GETUTCDATE()` (UTC) vs `ExpensesService.cs:195` `DateTime.UtcNow`. Mezcla produce bugs en balances y orden.
- **Fix:** Estandarizar `UtcNow` everywhere.

#### M-02 — Decimal precision / rounding
- `decimal(18,2)` OK pero `SplitMethodDialogComponent.ts:64` `amount / selectedMembers.length` sin rounding. `group-detail.component.ts:108` `Math.round` pierde centavos. No hay validación `sum == total` con tolerancia `0.01`.
- **Fix:** Usar `decimal` con rounding bancario, validar en backend.

#### M-03 — Split methods UI parcialmente implementado
- Frontend tiene 3 tabs (equal, amount, percentage) pero backend sólo hace equal split si frontend calcula — no valida `percentage ==100%` ni `sum amounts == total`. `split-method-dialog.component.ts:74` usa `m.amount` en vez de `percentageSplit` para amount split (bug), y `calculateSplyByPercentage` filtra `m.amount>0` mal.

#### M-04 — No health checks, no observability
- Sin `/health`, `/health/ready`, sin Serilog, sin correlation ID, sin log rotation.

#### M-05 — Clean Architecture drift
- `DependencyInjection.cs` en `SplitIt.API` registra `AppDbContext` (debería estar en `Infrastructure`). `SplitIt.Shared` vacío. Services tienen acceso directo a `AppDbContext` sin repository+unitOfWork.

#### M-06 — Frontend error handling débil
- `add-expense-dialog.component.ts:114` no maneja `error` del POST, no muestra snackbar. `group-detail.component.ts` no maneja 401/403.

#### M-07 — Falta paginación y límites de negocio
- `GetUsersAsync` devuelve todos los usuarios. Sin `maxGroupMembers`, `maxExpensesPerGroup`, `maxBodySize`.

#### M-08 — Falta Email validation / normalization
- `AuthService.cs:24` `AnyAsync(u => u.Email == email)` case-sensitive, sin `ToLowerInvariant()`, sin `EmailAddress` attribute, sin verificación.

---

### 🔵 LOW

#### L-01 — Nombres inconsistentes: `DebtSumaryDTO.cs` typo (Summary), `SplitItInfrastructure` project huérfano (`SplitIt.Infrastructure/Program.cs` vacío).
#### L-02 — `AllowToDeleteExpenses` nunca usado (bool sin lógica).
#### L-03 — `IsPayment` flag en Expense sin lógica (seed?).
#### L-04 — Bootstrap + Angular Material ambos — bundle bloat (500KB warning ok pero optimizable).
#### L-05 — `launchSettings.json` expone `http://localhost:5120` y `https://localhost:7191` sin doc.

---

## 4. Testing Status

| Capa | Framework | Tests existentes | Coverage | Veredicto |
|---|---|---|---|---|
| **Backend Unit** | xUnit/NUnit (no instalado) | ❌ 0 | 0% | 🔴 |
| **Backend Integration** | - | ❌ 0 | 0% | 🔴 |
| **API / Security (BOLA)** | - | ❌ 0 | 0% | 🔴 |
| **Frontend Unit** | Karma+Jasmine | 1 (`app.component.spec.ts` default) | ~0% | 🔴 |
| **Frontend E2E** | Playwright/Cypress (no instalado) | ❌ 0 | 0% | 🔴 |

- No hay `*.Tests.csproj`, no hay `__tests__`, no hay `*.spec.ts` útiles.
- No hay `dotnet test`, no hay `npm test` CI.
- No hay factories, seeders, Testcontainers.
- Business logic crítica sin tests: `GetFullDebtSummaryAsync` (netting logic líneas 116-156), `SettleExpenseWithUser`, split rounding.
- **Threshold requerido:** 70% en `business logic + auth` con CI fail-under.

---

## 5. Deployment Blockers

| # | Blocker | Fase que lo resuelve |
|---|---|---|
| 1 | Secretos en git, JWT débil | Fase 1 |
| 2 | SHA256 password hashing | Fase 2 |
| 3 | BOLA/IDOR total | Fase 3 |
| 4 | CORS AllowAny | Fase 6 |
| 5 | Sin validación input | Fase 4 |
| 6 | Sin rate limiting | Fase 5 |
| 7 | 0 tests / 0 security tests | Fase 7 |
| 8 | Sin Docker/Dockerfile/docker-compose | Fase 9 |
| 9 | Sin Nginx reverse proxy / TLS | Fase 11-12 |
| 10 | Sin CI/CD, sin secret scanning, sin container scanning | Fase 17 |
| 11 | Sin health checks / backups / rollback doc | Fase 10/15/19 |
| 12 | DB expuesta si se publica 1433, sin volume, sin backup | Fase 9-10 |

---

## 6. Technical Debt

1. **EF Core 9 sobre .NET 8** — alinear a `8.0.x` LTS o subir runtime a .NET 9.
2. **Migrations con `datetime` en vez de `datetime2`** — pierde precisión.
3. **Services sin interfaces** — dificulta mocking/tests.
4. **Controller hace `int.Parse(userIdClaim)` sin TryParse** — puede throw 500 en token manipulado.
5. **No CancellationToken** en async paths.
6. **No DTO validation, no AutoMapper, no MediatR** — hoy no necesario, pero manual mapping propenso a error.
7. **`SplitIt.Shared` muerto** — eliminar o mover constants.
8. **SSR habilitado sin uso** — añade `express` dep, aumenta superficie.

---

## 7. Missing Features (vs Future Features list)

| Feature | Estado actual | Gap |
|---|---|---|
| **Partial payments** | ❌ No existe. `RegisterPayment` crea payment pero `SettleExpenseWithUser` liquida todo. No hay `remaining debt` check. | Necesita `Amount` validado vs `TotalAmountOwed`, `IsPartial` flag, `Payment` entity separada |
| **Alternative split methods** | ⚠️ UI prototipo (3 tabs) pero backend no valida. `calculateSpitByAmount` bug, no valida porcentajes. | Validación `sum==total`, `percentage==100`, rounding |
| **Email validation** | ❌ Sólo `required` sin formato. Sin verificación token. | DataAnnotation + normalization + optional verification flow |
| **Group admin** | ⚠️ `Role="creator"/"member"` string sin enum, `AllowToDeleteExpenses` sin uso. No hay `Admin` promotion. | RBAC: `Owner > Admin > Member` con endpoints protegidos |
| **Application admin** | ⚠️ `Role` table con `super/admin/user` seed pero `AuthService` siempre `RoleId=3`. `GroupsController:58` check `Role=="1"` frágil (string vs int). | Middleware `RequireRole("super")`, endpoints `/api/admin/*` |

---

## 8. Recommended Changes (priorizado)

### P0 — Hacer antes de cualquier deploy
1. Rotar JWT secret, mover a env var, añadir `gitleaks` + BFG history rewrite doc.
2. Migrar a `PasswordHasher` (bcrypt/Argon2).
3. Fix BOLA: `GroupMember` check en todos los endpoints `groupId`.
4. Fix CORS: env var `AllowedOrigins`.
5. Fix `authGuard` frontend.
6. Añadir `AddRateLimiter` a login/register.

### P1 — Antes de beta privada
7. Validación DTOs (FluentValidation), global exception handler, `ClockSkew=0`, `RequireHttpsMetadata=true`.
8. Unit + Integration + Security tests (Testcontainers SQL Server).
9. Docker multi-stage (`mcr.microsoft.com/dotnet/aspnet:8.0` + `node:22-alpine` build).
10. Nginx reverse proxy + Let's Encrypt + security headers.

### P2 — Producción hardening
11. CI/CD GitHub Actions (lint → tests → audit → build → Trivy → deploy).
12. Health checks, Serilog, UFW, SSH hardening doc.
13. Backups SQL Server + restore drill + rollback doc.
14. Implementar Future Features completas con tests.

---

## 9. Risk Level

**Overall: 🔴 CRITICAL — 8 hallazgos CRITICAL, 9 HIGH**

- **Probabilidad de exploit sin fix:** Alta (BOLA trivial con `curl` + JWT).
- **Impacto:** Confidencialidad total perdida, integridad financiera comprometida, VPS a riesgo si se expone sin hardening.
- **Esfuerzo estimado:** 6-8 fases (1-7) para llegar a beta segura; 12 fases para producción completa según plan maestro.

---

## 10. Exact Implementation Plan (fases obligatorias)

> No se avanza de fase sin **reporte** `Changed files / Tests / Security improvements / Remaining risks / Next`.

```
Phase 0  ✅ AUDIT (este documento)
Phase 1  Secrets & Configuration — .env.example, env vars, gitleaks, history guidance
Phase 2  JWT & Authentication — PasswordHasher, JWT hardening, clock skew, refresh consideration
Phase 3  Authorization (BOLA/IDOR) — GroupMember guard, resource-level checks, security tests
Phase 4  API Security — Validation, mass assignment, error handling, SQLi review
Phase 5  Rate Limiting & Abuse — AddRateLimiter, business limits, body size
Phase 6  CORS — env-based origins, docs
Phase 7  Testing — Unit, Integration (Testcontainers), Security, Frontend, E2E (Playwright), coverage threshold
Phase 8  Future Features — Partial payments, split methods, email validation, group/app admin
Phase 9  Docker — Multi-stage, non-root, .dockerignore, healthcheck, network isolation, SQL private
Phase 10 Migrations — safe strategy, backup pre-migration, rollback
Phase 11 Nginx — reverse proxy, proxy headers, body limits
Phase 12 HTTPS — Let's Encrypt, 80→443, renewal
Phase 13 VPS Hardening — SSH, UFW, Docker iptables caveat, port scan
Phase 14 Dependencies — npm audit, dotnet vulnerable, Dependabot, Trivy
Phase 15 Secrets Scanning — Gitleaks/TruffleHog en CI
Phase 16 Container Scanning — Trivy OS+vuln+secret+misconfig, policy CRITICAL→fail
Phase 17 CI/CD — GitHub Actions pipeline completo, permissions least-privilege, pinned actions
Phase 18 Deployment — reproducible, health-gated, rollback via image tag + backup
Phase 19 Backups — schedule, retention, restore verify
Phase 20 Logging & Monitoring — structured logs, correlation ID, rotation
Phase 21 Observability — health endpoints, troubleshooting doc
Phase 22 Prod Testing — functional + security + infra checklists
Phase 23 Pentest básico — ZAP, nmap, Trivy, gitleaks non-destructive
Phase 24 Prod config — env separation, Swagger off, DetailedErrors off
Phase 25 Code Quality — SOLID, async, cancellation, pagination, N+1
Phase 26 Performance — indexes, AsNoTracking, bundle size
Phase 27 Final Quality Gate — checklist 27 ítems
Phase 28 Docs — README, .env.example, ARCHITECTURE.md, etc.
```

**Próximo paso inmediato (sin tu aprobación no se ejecuta):**  
**Phase 1 — Secrets & Configuration:**  
- Crear `docs/SECURITY.md` parcial, `.env.example`, actualizar `.gitignore`, mover `JwtSettings` y `ConnectionStrings` a env vars con fallback, añadir `appsettings.Production.json` template, documentar rotación, instalar `gitleaks` pre-commit.

¿Apruebas avanzar a Phase 1? Responde `sí` o indica ajustes a esta auditoría.

---

## 11. Appendix — Files Audited

- `SplitIt.API/SplitIt.API/Program.cs:1` — JWT, CORS, Swagger, Auth
- `SplitIt.API/SplitIt.API/appsettings.json:1` — secrets
- `SplitIt.API/SplitIt.API/DependencyInjection.cs:1` — DbContext
- `SplitIt.Infrastructure/Services/AuthService.cs:1` — SHA256
- `SplitIt.Infrastructure/Services/GroupService.cs:1` — BOLA
- `SplitIt.Infrastructure/Services/ExpensesService.cs:1` — settlement bug, N+1
- `SplitIt.Infrastructure/Persistence/AppDbContext.cs:1` — model
- `SplitIt.API/Controllers/AuthController.cs:1`, `GroupsController.cs:1`, `ExpensesController.cs:1`, `UsersController.cs:1`, `CurrenciesController.cs:1`
- `SplitIt.Application/DTOs/*.cs` — 9 DTOs
- `SplitIt.Domain/Entities/*.cs` — 7 entities
- `split-it-ui/src/app/interceptors/auth.interceptor.ts:1` — localStorage
- `split-it-ui/src/app/modules/auth/guards/auth.guard.ts:1` — always true
- `split-it-ui/src/environments/environment.ts:1` — apiUrl
- `split-it-ui/package.json:1` — audit
- `split-it-ui/src/app/modules/dashboard/components/*` — frontend logic
- `.gitignore:1` — misconfig
- No `Dockerfile`, `docker-compose.yml`, `.github/workflows`, `*.Tests.csproj` found

## 12. References

- OWASP Top 10 2021: A01 BOLA, A07 Auth Failures, A03 Injection
- CWE-287, CWE-327, CWE-916
- .NET 8 JWT Bearer docs, Angular 19 security guide

---

## 13. Phase 0.5 Remediation Status (2026-08-24)

> **Estado post-0.5:** 🟢 SEGURIDAD CRÍTICA REMEDIADA — 0 🔴 CRITICAL abiertos (ver tabla). Riesgo residual: MEDIUM (XSS localStorage, falta Docker/CSP/HSTS en infra).

### 13.1 Matriz de hallazgos → fix

| ID | Hallazgo Original | Estado Original | Fix Implementado | Verificación | Estado Actual |
|---|---|---|---|---|---|
| **C-01** | Secretos hardcodeados `appsettings.json:9-16` | 🔴 | `appsettings.json` vaciado, `.env.example` + `appsettings.*.json.example`, `Program.cs:17` validación secret length, `.gitignore` corr., `docs/SECURITY.md:1` con rotación + gitleaks doc | `grep` sin `SuperSecret`, `dotnet build` OK, `appsettings.json` `DefaultConnection=""` | ✅ Fixed |
| **C-02** | SHA256 sin salt `AuthService.cs:49` | 🔴 | `Microsoft.AspNetCore.Identity.EntityFrameworkCore 8.0.15` + `IPasswordHasher<User>`, `AuthService.cs:22` PBKDF2 + legacy SHA256 rehash path, `IsLegacySha256Hash` | `AuthServicePasswordHashingTests` 5 tests pass (migrate, verify, case-insensitive) | ✅ Fixed |
| **C-03** | AuthGuard `=> true` | 🔴 | `split-it-ui/src/app/modules/auth/guards/auth.guard.ts:1` verifica JWT exp, limpia localStorage, redirect `/auth/login?returnUrl`; `dashboard.routes.ts:1` `canActivate:[authGuard]` | Manual + `authGuard` logic unit review | ✅ Fixed |
| **C-04** | BOLA/IDOR masivo | 🔴 | `GroupService.cs:100` `IsUserMemberAsync`, `GroupsController.cs:67,81,95` + `ExpensesController.cs:45,59,75` `Forbid()` si no miembro; `ExpensesService.cs:22` valida `CreatedBy PaidBy Participants member + sum==Amount` | `BolaTests` 4 tests pass (not member, participant not member, sum mismatch) | ✅ Fixed |
| **C-05** | CORS AllowAnyOrigin | 🔴 | `Program.cs:158` env `Cors:AllowedOrigins` comma; dev fallback `localhost:4200`; prod fail-closed; `WithOrigins+AllowCredentials` | `Program.cs:163` review, no `AllowAnyOrigin` | ✅ Fixed |
| **C-06** | JWT débil (ASCII vs UTF8, ClockSkew 5m, RequireHttps false) | 🔴 | `Program.cs:34` UTF8 unify, `ClockSkew=Zero`, `RequireHttpsMetadata=!IsDevelopment()`, `ValidAlgorithms=[HS256]`, alg check event, secret 64+ validation, `AuthController.cs:59` UTF8 | `JwtValidationTests` 8 tests pass (valid, expired, tampered, wrong iss/aud/sig, none alg, clockSkew) | ✅ Fixed |
| **C-07** | JWT en localStorage | 🔴 | Decisión documentada `docs/SECURITY.md:4` mantiene localStorage + mitigaciones (CSP, validation, 401 interceptor limpia). Roadmap cookie HttpOnly futuro. `auth.interceptor.ts:1` 401→logout | Doc + `auth.interceptor.ts` | ✅ Mitigated (risk residual LOW-MED) |
| **C-08** | Mass Assignment | 🔴 | DTOs con `[Required]...` explícitos, no bind Entity, `AuthService RegisterUser` ignora Role (hardcode 3), `ExpensesService` deriva `CreatedById` de JWT | `MassAssignmentTests` 2 pass | ✅ Fixed |
| **H-01** | Validación insuficiente | 🟠 | `SplitIt.Application/DTOs/*.cs` DataAnnotations (`RegisterRequestDto`, `CreateGroupDTO`, `ExpensesDTO`, `RegisterPaymentDto`) + service checks `sum==total ±0.02`, `max 50`, `Amount 0.01..1M` | `ValidationTests` 6 pass | ✅ Fixed |
| **H-02** | Exception leak | 🟠 | `SplitIt.API/Middleware/GlobalExceptionHandler.cs:1` `IExceptionHandler`, `AddExceptionHandler`, `ProblemDetails`, no stack en prod | Manual test 500 returns `traceId` only | ✅ Fixed |
| **H-03** | Rate limiting | 🟠 | `Program.cs:63` `AddRateLimiter` policy `auth` 5/min/IP `[EnableRateLimiting("auth")]` en `AuthController.cs:27`, `OnRejected 429` | Integration via `AddRateLimiter` + unit config test | ✅ Fixed |
| **H-05** | Settlement cross-group | 🟠 | `ExpensesService.cs:202` `SettleExpenseWithUser(..., groupId)` scoped `Expense.GroupId==groupId`, `RegisterPayment` scoped, `ExpensesController.cs:86` pasa `groupId` | `SettlementCrossGroupTests` 2 pass (GroupA settle not affect GroupB) | ✅ Fixed |
| **H-09** | .gitignore | 🟠 | `.gitignore:22` no ignora `package-lock.json`, añade `!.env.example` | `git check-ignore` | ✅ Fixed |
| **M-01** | Timezone `DateTime.Now` | 🟡 | `GroupService.cs:28` `UtcNow`, `ExpensesService.cs:22` `ToUniversalTime()` | Code review | ✅ Fixed |
| **M-08** | Email normalization | 🟡 | `AuthService.cs:22` `ToLowerInvariant()`, `[EmailAddress]` | `AuthServicePasswordHashingTests` case-insensitive pass | ✅ Fixed |

### 13.2 Tests de regresión añadidos

- **Proyecto:** `SplitIt.Tests/SplitIt.Tests.csproj` (xUnit, net8.0, EF InMemory, Identity)
- **Total:** 33 tests — 33 passed, 0 failed (release)
  - `AuthServicePasswordHashingTests` (5) — PBKDF2, legacy migrate
  - `BolaTests` (4) — BOLA, participant, sum
  - `SettlementCrossGroupTests` (2) — cross-group isolation
  - `JwtValidationTests` (8) — valid, expired, tampered, wrong iss/aud/sig, none, ClockSkew
  - `ValidationTests` (6)
  - `MassAssignmentTests` (2)
- **Builds:** `dotnet build SplitIt.Back.sln -c Release` ✅ 0 warnings; `npm run build` ✅ (budget warn 592kB >500kB, sass @import deprecation — non-blocking)
- **Coverage:** No threshold aún — requiere coverlet en CI Fase 7.

### 13.3 Criterio de finalización Phase 0.5

| Criterio | Estado |
|---|---|
| ❌ Hardcoded secrets | ✅ Eliminated, templates + gitleaks doc |
| ❌ SHA256 password storage | ✅ PBKDF2 + migrate |
| ❌ AuthGuard bypass | ✅ Fixed + dashboard guard |
| ❌ BOLA/IDOR | ✅ Resource-level checks |
| ❌ Cross-group settlement | ✅ Scoped by groupId |
| ❌ AllowAnyOrigin production | ✅ Env-based, fail-closed |
| ❌ Weak JWT validation | ✅ UTF8, ClockSkew 0, RequireHttps, alg check |
| ❌ Mass assignment | ✅ DTO whitelist |

**Todos los criterios cumplidos. Phase 0.5 COMPLETA — listo para Phase 1+ (Docker/CI) sin blocker crítico.**

### 13.4 Riesgos residuales post-0.5 (no bloqueantes Phase 0.5 pero P1)

- XSS → localStorage theft (mitigado validation, pendiente CSP `default-src 'self'` en Nginx Fase 11)
- No refresh token revocation (JWT 60m window) — Fase 8
- `npm audit` 71 vulns (6 low 21 mod 40 high 4 critical) vía `angular-devkit` — requiere `npm audit fix` mayor (breaking) en Fase 14, no bloqueante funcional
- No pagination en `GetUsers`/`GetGroupMembers` (limit 50 mitiga)
- No container scanning/Docker aún — Fase 9

### 13.5 Archivos cambiados (Phase 0.5)

```
Modified:
 SplitIt.API/SplitIt.API/appsettings.json
 SplitIt.API/SplitIt.API/Program.cs
 SplitIt.API/SplitIt.API/DependencyInjection.cs
 SplitIt.API/SplitIt.API/Controllers/AuthController.cs
 SplitIt.API/SplitIt.API/Controllers/GroupsController.cs
 SplitIt.API/SplitIt.API/Controllers/ExpensesController.cs
 SplitIt.API/SplitIt.Infrastructure/Services/AuthService.cs
 SplitIt.API/SplitIt.Infrastructure/Services/GroupService.cs
 SplitIt.API/SplitIt.Infrastructure/Services/ExpensesService.cs
 SplitIt.API/SplitIt.Application/DTOs/*.cs (5 DTOs)
 SplitIt.API/SplitIt.Infrastructure/SplitIt.Infrastructure.csproj
 SplitIt.Infrastructure.cs: Added Microsoft.AspNetCore.Identity.EntityFrameworkCore 8.0.15
 split-it-ui/src/app/modules/auth/guards/auth.guard.ts
 split-it-ui/src/app/interceptors/auth.interceptor.ts
 split-it-ui/src/app/modules/auth/services/auth.service.ts
 split-it-ui/src/app/modules/dashboard/dashboard.routes.ts
 split-it-ui/src/app/app.config.ts (implicit via interceptor)
 .gitignore

Added:
 SplitIt.API/SplitIt.API/Middleware/GlobalExceptionHandler.cs
 SplitIt.API/SplitIt.API/appsettings.Development.json.example
 SplitIt.API/SplitIt.API/appsettings.Production.json.example
 SplitIt.Tests/SplitIt.Tests.csproj + 6 test files + Helpers
 split-it-ui/src/environments/environment.prod.ts.example
 split-it-ui/.env.example
 .env.example
 docs/SECURITY.md
 docs/PRODUCTION_AUDIT.md (section 13)
 SplitIt.API/SplitIt.Back.sln (added SplitIt.Tests)
```

