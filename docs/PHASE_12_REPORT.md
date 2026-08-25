# Phase 12 Report — HTTPS: Let's Encrypt, 80→443, Renewal

## Goal
Automate the Let's Encrypt certificate lifecycle (renewal + Nginx reload) and validate the 80→443 redirect and ACME challenge path that were implemented in Phase 11.

## Scope (from master plan, `docs/PRODUCTION_AUDIT.md:370`)
```
Phase 12 HTTPS — Let's Encrypt, 80→443, renewal
```

Phase 11 delivered the Nginx reverse proxy, self-signed cert generation, manual Let's Encrypt issuance, HTTP→HTTPS 301 redirect, ACME HTTP-01 challenge path, TLS hardening, and security headers. Phase 12 adds **automated certificate renewal** with zero-downtime Nginx reload.

## Changed files

| File | Change |
|------|--------|
| `docker/proxy/certbot-renew.sh` | **New.** Automated renewal loop script. Performs initial issuance if certs are missing, then loops `certbot renew` every 12h. Uses `--deploy-hook` to write a sentinel file on successful renewal. |
| `docker/proxy/entrypoint.sh` | Added background watcher (every 60s) that checks for the renewal sentinel file in the shared `certbot_www` volume and runs `nginx -s reload` when found. |
| `docker-compose.yml` | Added `certbot-renewer` service (profile: `letsencrypt`, image: `certbot/certbot:latest`, mounts `certbot-renew.sh` + `certbot_certs` + `certbot_www` volumes, depends on `proxy:healthy`, resource limits 0.25 CPU / 64 MB). Updated `certbot` service with `DOMAIN` and `ACME_EMAIL` env vars. |
| `.env.example` | Added `COMPOSE_PROFILES` (commented, for enabling the renewer) and `RENEWAL_INTERVAL` (default 12h). |
| `docs/HTTPS.md` | Documented automated renewal workflow, reload mechanism diagram, manual renewal alternative, and new environment variables. |
| `SplitIt.Tests/Phase12HttpsTests.cs` | **New.** 22 tests validating: certbot-renew.sh content, entrypoint reload watcher, docker-compose certbot-renewer service config, `.env.example` variables, nginx 80→443 redirect, ACME path, TLS hardening, HSTS, `.dockerignore` cert exclusions, HTTPS docs. |
| `docs/PHASE_12_REPORT.md` | This report. |

## Test results

| Suite | Result |
|-------|-------|
| Backend `dotnet test` (Phase 12 filter) | **22 passed, 0 failed** |
| Backend `dotnet test` (full suite) | **114 passed, 4 failed** (pre-existing — see below) |
| Docker Compose config (default) | **Valid** (exit 0) |
| Docker Compose config (`--profile letsencrypt`) | **Valid** — certbot-renewer service present with correct volumes/network/resources |
| Docker Compose config (`--profile certbot`) | **Valid** — manual certbot service present |

### Pre-existing failures (not caused by Phase 12)
`CorsTests` (4 tests) fail with `JwtSettings:SecretKey is missing or too short`. The `WebApplicationFactory` sets the secret via `ConfigureAppConfiguration`, but `Program.cs:31` validates it during top-level statement execution before deferred configuration is applied. **This predates Phase 12** — no Phase 12 file touches `Program.cs`, `CorsTests.cs`, or any application code. Documented per project rules; not fixed.

### Phase 12 test details
| Test | Validates |
|------|-----------|
| `CertbotRenewScript_Exists` | Script file present and non-empty |
| `CertbotRenewScript_ContainsRenewalLoop` | `certbot renew`, `while true`, `sleep` present |
| `CertbotRenewScript_ContainsDeployHookSentinel` | `--deploy-hook` and `.reload-trigger` sentinel |
| `CertbotRenewScript_ContainsInitialIssuance` | `certonly`, `--webroot`, `--non-interactive` for first-time |
| `Entrypoint_ContainsReloadWatcher` | `.reload-trigger` and `nginx -s reload` in entrypoint |
| `Entrypoint_WatcherRunsInBackground` | Subshell `) &` and `sleep 60` |
| `Compose_HasCertbotRenewerService` | Service exists in docker-compose.yml |
| `Compose_CertbotRenewerHasLetsencryptProfile` | Profile `letsencrypt` set |
| `Compose_CertbotRenewerHasCorrectVolumes` | `certbot_certs`, `certbot_www`, `certbot-renew.sh` |
| `Compose_CertbotRenewerHasResourceLimits` | CPU and memory limits set |
| `Compose_CertbotRenewerDependsOnProxy` | `depends_on: proxy: service_healthy` |
| `EnvExample_DocumentsComposeProfiles` | `COMPOSE_PROFILES` and `letsencrypt` |
| `EnvExample_DocumentsRenewalInterval` | `RENEWAL_INTERVAL` variable |
| `NginxTemplate_HasHttpToHttpsRedirect` | `return 301 https://` present |
| `NginxTemplate_HasAcmeChallengePath` | `/.well-known/acme-challenge` present |
| `NginxTemplate_AcmePathHasNoRedirect` | ACME location block does NOT contain redirect |
| `HttpsDocs_DocumentAutomatedRenewal` | `certbot-renewer`, `sentinel`, `Phase 12` |
| `HttpsDocs_DocumentManualRenewal` | Manual `certbot renew` and `nginx -s reload` |
| `HttpsDocs_DocumentReloadMechanism` | `Automated reload` and `deploy-hook` |
| `SslParams_DisableObsoleteProtocols` | TLS 1.2/1.3 only, no 1.0/1.1 |
| `SslParams_HasHstsInSecurityHeaders` | HSTS with `max-age=31536000` and `preload` |
| `DockerIgnore_BlocksCertificateFiles` | `*.pem`, `*.key`, `*.crt` excluded |

## Docker verification

### Exposed ports (host)
```
80/tcp   -> proxy:8080  (HTTP → 301 HTTPS redirect + ACME challenge)
443/tcp  -> proxy:8443  (HTTPS application traffic)
```

### NOT exposed
- `1433/tcp` (SQL Server)
- `8080/tcp` (backend direct)
- `80/tcp` (frontend direct)

### Profiles
| Profile | Service | Purpose |
|---------|---------|---------|
| `certbot` | `splitit-certbot` | Manual one-off certbot operations (issuance, delete) |
| `letsencrypt` | `splitit-certbot-renewer` | Automated renewal loop (every 12h) + sentinel reload |

### Networks
- `splitit-frontend-net` — bridge (proxy, frontend, backend, certbot, certbot-renewer)
- `splitit-backend-net` — bridge **internal: true** (backend, sqlserver, db-init, migrator)

### Privileged containers / Docker socket
None.

### Resource limits (certbot-renewer)
- 0.25 CPU, 64 MB RAM.

## Renewal mechanism

```
certbot-renewer                  proxy (nginx)
     |                              |
     |-- certbot renew              |-- serving HTTPS traffic
     |-- on success:                |-- background watcher (60s loop)
     |   deploy-hook:               |   if /var/www/certbot/.reload-trigger:
     |   touch .reload-trigger      |     rm .reload-trigger
     |                              |     nginx -s reload (zero-downtime)
     v                              v
  certbot_www volume <--------- shared --------->
```

1. `certbot-renewer` runs `certbot renew` every 12 hours.
2. On successful renewal, certbot's `--deploy-hook` writes `.reload-trigger` to the shared `certbot_www` volume.
3. The proxy entrypoint's background watcher detects the sentinel within 60s, removes it, and reloads Nginx.
4. Nginx picks up the new certificate without dropping connections.

## Security implications

1. **No new exposed ports**: The certbot-renewer is an internal container; it does not publish any host ports. It communicates only via shared Docker volumes.
2. **No secrets in images**: The `certbot-renew.sh` script is a shell script mounted read-only; no certificates or private keys are baked into any image. `.dockerignore` continues to block `*.pem`, `*.key`, `*.crt`.
3. **Certbot-renewer runs as root** (default for `certbot/certbot` image): This is the standard certbot image behavior. The renewer only writes to `/etc/letsencrypt` and `/var/www/certbot` (both Docker volumes), not to the host filesystem. Resource limits are set (0.25 CPU, 64 MB).
4. **No authentication changes**: Phase 12 does not modify JWT, CORS, auth guard, or any application security logic.
5. **80→443 redirect** (from Phase 11) verified: All non-ACME HTTP traffic receives a `301` permanent redirect to HTTPS. The ACME challenge path (`/.well-known/acme-challenge/`) stays on HTTP and is NOT redirected, allowing certbot to validate domain ownership.
6. **HSTS**: `max-age=31536000; includeSubDomains; preload` remains active on all HTTPS responses.
7. **TLS hardening**: TLS 1.2/1.3 only, forward-secret ciphers, OCSP stapling — unchanged from Phase 11.
8. **Profile isolation**: The certbot-renewer only starts when `COMPOSE_PROFILES=letsencrypt` is set or `--profile letsencrypt` is passed. In self-signed mode (default for local testing), the renewer does not start.

## Remaining risks

1. **Initial issuance still manual**: The first Let's Encrypt certificate must be obtained manually (the proxy requires a cert to start in `letsencrypt` mode, creating a chicken-and-egg problem). The `certbot-renew.sh` script attempts initial issuance if certs are missing, but this requires the proxy to already be running and serving the ACME challenge path. Documented in `docs/HTTPS.md`.
2. **Sentinel file permissions**: The certbot-renewer (root) creates the `.reload-trigger` file. The proxy (nginx user, UID 101) removes it. This works because the `/var/www/certbot` directory is owned by nginx (initialized from the proxy image), and Unix directory write permission allows the owner to delete files regardless of file ownership. If the volume is first created by the certbot container (e.g., certbot starts before proxy), the directory may be root-owned and the watcher cannot delete the sentinel — nginx would reload every 60s (harmless but noisy). Mitigated by `depends_on: proxy: service_healthy` ensuring proxy starts first.
3. **Renewal loop not verified against real Let's Encrypt**: The automated renewal was validated via Docker Compose config and script content tests, but not against the real Let's Encrypt staging API (requires a public DNS domain). The script follows standard certbot patterns.
4. **Pre-existing CorsTests failure** (4 tests): `WebApplicationFactory` + `Program.cs` JWT validation timing issue. Unrelated to Phase 12.
5. **No renewal failure alerting**: If certbot renewal fails, the renewer logs the error but no alert is sent. Monitoring/alerting is Phase 20+ scope.
6. **Let's Encrypt rate limits**: If the renewer loops too quickly or multiple instances run, rate limits may be hit. The 12h interval and single-instance design mitigate this.

## Proposed commit message

```
feat: automate Let's Encrypt certificate renewal with zero-downtime Nginx reload (Phase 12)

- Add certbot-renewer service (profile: letsencrypt) running certbot renew every 12h
- Add certbot-renew.sh with initial issuance fallback and --deploy-hook sentinel
- Add background reload watcher in proxy entrypoint (checks shared volume every 60s)
- Add COMPOSE_PROFILES and RENEWAL_INTERVAL to .env.example
- Document automated + manual renewal workflows in docs/HTTPS.md
- Add 22 Phase12HttpsTests validating renewal automation, 80→443 redirect,
  ACME path, TLS hardening, HSTS, and certificate file exclusions
- Validate docker-compose config with letsencrypt and certbot profiles
```
