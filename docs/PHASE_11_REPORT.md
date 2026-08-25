# Phase 11 Report — HTTPS, Nginx Reverse Proxy & Production Security Headers

## Goal
Implement production HTTPS and reverse-proxy architecture without weakening the existing security model.

## Changed files

| File | Change |
|------|--------|
| `docker-compose.yml` | Added `proxy` and `certbot` services; removed `frontend` port publication; added TLS volumes; updated health checks |
| `docker/proxy/Dockerfile` | New production Nginx reverse-proxy image (non-root, alpine) |
| `docker/proxy/nginx.conf.template` | Templated Nginx config with routing, rate limits, TLS |
| `docker/proxy/entrypoint.sh` | Runtime config rendering + self-signed cert generation |
| `docker/proxy/snippets/*.conf` | Security headers, SSL params, trusted proxies |
| `docker/frontend/nginx.conf` | Simplified to pure static server; removed duplicated security headers |
| `SplitIt.API/SplitIt.API/Program.cs` | Added `ForwardedHeaders` middleware (private ranges only) |
| `SplitIt.Tests/CorsTests.cs` | New CORS fail-closed tests |
| `split-it-ui/e2e/fullstack/docker-https-fullstack.spec.ts` | New HTTPS E2E suite through Nginx |
| `split-it-ui/e2e/fullstack/docker-fullstack.spec.ts` | Updated to use HTTPS through Nginx |
| `.env.example` | Added `DOMAIN`, `TLS_MODE`, `ACME_EMAIL`, `PROXY_HTTP_PORT`, `PROXY_HTTPS_PORT` |
| `.gitignore` / `.dockerignore` | Added certificate and key exclusions |
| `scripts/generate-local-ssl.*` | Helper scripts for local TLS pre-generation |
| `docs/NGINX.md`, `docs/HTTPS.md`, `docs/PHASE_11_REPORT.md` | Documentation |

## Test results

| Suite | Result |
|-------|--------|
| Backend `dotnet test` | **92 passed, 0 failed** |
| Frontend `npm run build` (production) | **Success** |
| Angular unit tests (`npm test`) | **Skipped** — Chrome not available in this environment |
| Playwright full suite | **66 passed, 0 failed** |
| Docker Compose validation | **Valid** |

### Playwright highlights
- HTTP → HTTPS redirect: pass
- ACME challenge path stays HTTP (no redirect): pass
- Health endpoints return `text/plain` (not `index.html`): pass
- Security headers present on all HTTPS responses: pass
- Registration, login, authenticated API, protected routes, groups, expenses, settlement, BOLA, logout: all pass

## Docker verification

### Exposed ports (host)
```
80/tcp   -> proxy:8080
443/tcp  -> proxy:8443
```

### NOT exposed
- `1433/tcp` (SQL Server)
- `8080/tcp` (backend direct)
- `80/tcp` (frontend direct)

### Networks
- `splitit-frontend-net` — bridge (proxy, frontend, backend)
- `splitit-backend-net` — bridge **internal: true** (backend, sqlserver, db-init, migrator)

### Privileged containers
None.

### Host network / Docker socket
Not used.

## TLS verification

- TLS 1.2/1.3 only; obsolete protocols disabled.
- HSTS header present.
- Self-signed profile works for local testing.
- Let's Encrypt profile ready for production (requires DNS + `certbot` run).

## CORS verification

- Backend remains fail-closed: no `AllowAnyOrigin()`.
- Production `.env.example` sets `CORS_ALLOWED_ORIGINS=https://splitit.yourdomain.com` only.
- New `CorsTests.cs` verifies allowed origin, blocked origin, and empty-origin fail-closed behavior.

## Trivy vulnerability scan

| Image | HIGH / CRITICAL |
|-------|-----------------|
| splitit-backend | **0** |
| splitit-frontend | **0** |
| splitit-proxy | **0** |

## Security headers

All active on HTTPS responses:
- Strict-Transport-Security
- X-Content-Type-Options
- X-Frame-Options
- Referrer-Policy
- Permissions-Policy
- Cross-Origin-Opener-Policy
- Cross-Origin-Resource-Policy
- Content-Security-Policy (compatible with Angular production build)

## Secrets / certificate audit

- `.env` is ignored by Git.
- No certificate, private key, or password files are tracked.
- No secrets are baked into Docker images.

## Remaining risks / limitations

1. **Rate limiting on localhost tests:** Because all requests from the host share the same Docker gateway IP, Nginx rate limits can be exhausted during rapid repeated E2E runs. Production (one IP per real client) is not affected.
2. **Self-signed certificate warning:** Local browsers/tests must accept or ignore the warning; this is expected and documented.
3. **Angular unit tests:** Could not be executed in this environment due to missing Chrome installation; this is an environment limitation, not a code regression.
4. **Let's Encrypt renewal:** Requires operator to configure cron or manual renewal workflow; documented in `docs/HTTPS.md`.
5. **CSP `style-src 'unsafe-inline'`:** Required for Angular Material/Bootstrap compatibility. The application does not use inline scripts.

## Proposed commit message

```
feat: implement HTTPS reverse proxy and production security headers (Phase 11)

- Add nginx:alpine reverse proxy as the only Internet-facing container
- Terminate TLS 1.2/1.3 with hardened cipher suite and HSTS
- Support Let's Encrypt (production) and self-signed (local testing) modes
- Route /api/* and /health/* to backend; serve Angular SPA catch-all
- Apply security headers including CSP compatible with Angular production build
- Implement nginx-level rate limiting for auth, API, and static traffic
- Add ForwardedHeaders middleware with strict KnownNetworks
- Add CORS fail-closed tests
- Add HTTPS full-stack E2E tests through Nginx
- Remove frontend port exposure; only proxy exposes 80/443
- Document architecture in docs/NGINX.md and docs/HTTPS.md
```

## Next step

Wait for explicit approval before committing or starting Phase 12.
