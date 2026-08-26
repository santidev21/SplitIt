# SplitIt Nginx Reverse Proxy

> **Note (Phase 14/OPTION A):** The `splitit-proxy` container has been removed from the Docker Compose stack.
> TLS termination, security headers, rate limiting, and routing are now handled by the VPS reverse-proxy
> (`reverse-proxy` container in the `portfolio` compose stack). This document describes the configuration
> that was previously used internally and is now replicated in the VPS Nginx configuration for
> `splitit.santidev21.tech`.

## Role

The `splitit-proxy` container is the **only** Internet-facing service. It terminates TLS, applies security headers, enforces rate limits, and routes traffic to the Angular frontend or the .NET backend.

## Architecture

```
Internet
   |
   | HTTPS :443  or  HTTP :80 (ACME / redirect)
   v
 splitit-proxy (Nginx)
   |-- /api/*     --> backend:8080
   |-- /health/*  --> backend:8080
   |-- /*         --> frontend:80 (Angular SPA)
```

No other container publishes host ports.

## Ports

| Host | Container | Purpose |
|------|-----------|---------|
| 80   | 8080      | ACME HTTP-01 challenge + HTTP→HTTPS redirect |
| 443  | 8443      | HTTPS application traffic |

Backend (`8080/tcp`) and SQL Server (`1433/tcp`) are **not** published to the host.

## Security

### Non-root execution
- The container runs as the `nginx` user (UID 101).
- Nginx listens on unprivileged ports `8080/8443`; Docker maps them to host `80/443`.

### Rate limiting
| Zone   | Rate   | Burst | Applied to |
|--------|--------|-------|------------|
| auth   | 30r/m  | 50    | `/api/auth/*` |
| api    | 100r/m | 20    | `/api/*` |
| general| 200r/m | 50    | Static assets, SPA, health |

### Security headers (all HTTPS responses)
- `Strict-Transport-Security: max-age=31536000; includeSubDomains; preload`
- `X-Content-Type-Options: nosniff`
- `X-Frame-Options: SAMEORIGIN`
- `Referrer-Policy: strict-origin-when-cross-origin`
- `Permissions-Policy: accelerometer=(), camera=(), ...`
- `Cross-Origin-Opener-Policy: same-origin`
- `Cross-Origin-Resource-Policy: same-origin`
- `Content-Security-Policy: default-src 'self'; script-src 'self'; style-src 'self' 'unsafe-inline' https://fonts.googleapis.com; font-src 'self' https://fonts.gstatic.com; img-src 'self' data: blob:; connect-src 'self'; frame-ancestors 'self'; base-uri 'self'; form-action 'self';`

> **CSP note:** `style-src 'unsafe-inline'` is required because Angular Material and Bootstrap inject small inline style blocks at runtime. The application does **not** use inline scripts, so `script-src` remains strict.

### Real IP handling
Only RFC1918 private ranges are trusted by default:
- `10.0.0.0/8`, `172.16.0.0/12`, `192.168.0.0/16`, `127.0.0.1/32`

Do not blindly trust arbitrary `X-Forwarded-For` headers. If you place the proxy behind a CDN, mount a `trusted-proxies.conf` with the CDN ranges.

### Upstream headers
All proxied requests include:
- `X-Forwarded-For`
- `X-Forwarded-Proto`
- `X-Forwarded-Host`
- `X-Forwarded-Port`
- `X-Real-IP`

The backend `ForwardedHeaders` middleware trusts only the same private ranges, so `RemoteIpAddress` reflects the original client.

## Routing rules

| Location | Destination | Note |
|----------|-------------|------|
| `/.well-known/acme-challenge/*` | `/var/www/certbot` | HTTP only, no redirect |
| `/health(/.*)?` | backend | Must not fall through to SPA |
| `/api/auth/*` | backend | Stricter rate limit |
| `/api/*` | backend | General API rate limit |
| static assets (`*.js`, `*.css`, …) | frontend | Long cache headers |
| `/` catch-all | frontend | Angular HTML5 history fallback |

## Files

- `docker/proxy/Dockerfile` — image build
- `docker/proxy/nginx.conf.template` — main config template rendered by `envsubst`
- `docker/proxy/entrypoint.sh` — runtime cert generation / config rendering
- `docker/proxy/snippets/security-headers.conf` — security headers
- `docker/proxy/snippets/ssl-params.conf` — TLS hardening
- `docker/proxy/snippets/trusted-proxies.conf` — optional CDN ranges
