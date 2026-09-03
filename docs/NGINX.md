# SplitIt Nginx Reverse Proxy

> **Architecture:** TLS termination, security headers, rate limiting, and routing are handled by the
> **`vps-gateway`** (private repo, `gateway` container). SplitIt has no proxy — it is a pure app behind the gateway.

## Architecture

```
Internet → vps-gateway (:80/:443) → splitit.santidev21.tech
  ├── /api/*     → splitit-backend:8080
  ├── /health/*  → splitit-backend:8080
  ├── /api/auth/* → splitit-backend:8080 (strict rate limit)
  └── /*         → splitit-frontend:80 (Angular SPA)
```

No SplitIt container publishes host ports.

## Routing Rules (handled by vps-gateway)

| Location | Destination | Note |
|----------|-------------|------|
| `/.well-known/acme-challenge/*` | `/var/www/certbot` | HTTP only, no redirect |
| `/health(/.*)?` | splitit-backend:8080 | Must not fall through to SPA |
| `/api/auth/*` | splitit-backend:8080 | Stricter rate limit (30r/m) |
| `/api/*` | splitit-backend:8080 | General API rate limit (100r/m) |
| static assets (`*.js`, `*.css`, …) | splitit-frontend:80 | Long cache headers |
| `/` catch-all | splitit-frontend:80 | Angular HTML5 history fallback |

## Network Connectivity

The gateway reaches SplitIt via `splitit-frontend-net` (external, owned by vps-gateway):
- `splitit-frontend` joins `splitit-frontend-net` → reachable as hostname `splitit-frontend`
- `splitit-backend` joins both `splitit-frontend-net` + `splitit-backend-net` (internal, DB only)

## Security Headers (applied by vps-gateway)

The gateway applies security headers per-site. SplitIt's CSP includes Google Identity Services for OAuth:
- `Content-Security-Policy: default-src 'self'; script-src 'self' 'unsafe-inline' https://accounts.google.com; ...`
- HSTS, nosniff, SAMEORIGIN, Referrer-Policy, Permissions-Policy, COOP, CORP

## Files (in vps-gateway repo)

- `sites-enabled/splitit.santidev21.tech.conf` — routing rules
- `snippets/ssl-params.conf` — TLS hardening
- `snippets/security-headers.conf` — security headers
- `snippets/trusted-proxies.conf` — optional CDN ranges
