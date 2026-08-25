# SplitIt HTTPS / TLS

## Two modes

| Mode | Env var | Use case |
|------|---------|----------|
| `selfsigned` | `TLS_MODE=selfsigned` | Local development / CI testing only |
| `letsencrypt` | `TLS_MODE=letsencrypt` | Production |

## Self-signed (local)

On first start the proxy entrypoint generates:
- A 2048-bit RSA self-signed certificate (`nginx.crt` / `nginx.key`)
- 2048-bit DH parameters (`dhparam.pem`)

All are stored in the Docker volume `splitit_nginx_certs`.

```bash
# Start with self-signed certs
docker compose up -d
```

Browsers will show a certificate warning; Playwright E2E tests use `ignoreHTTPSErrors: true` for this mode.

## Let's Encrypt (production)

### Prerequisites
1. A public DNS A/AAAA record pointing to the VPS IP.
2. Ports 80 and 443 open to the Internet.
3. `DOMAIN` and `ACME_EMAIL` set in `.env`.

### Initial issuance
```bash
docker compose run --rm certbot certonly \
  --webroot -w /var/www/certbot \
  --email $ACME_EMAIL \
  -d $DOMAIN \
  --agree-tos --no-eff-email
```

### Renewal
```bash
docker compose run --rm certbot renew
```

Recommended: run the renewal command via cron (`certbot` service is already defined with profile `certbot`):
```bash
# Cron example (twice daily)
0 */12 * * * cd /path/to/splitit && docker compose run --rm certbot renew --quiet
```

The proxy reads certificates from `/etc/letsencrypt/live/$DOMAIN/` (mounted read-only). Nginx must be reloaded after renewal:
```bash
docker compose exec proxy nginx -s reload
```

> **Important:** Do not automate destructive cert operations (e.g., `certbot delete`) without human review.

## TLS configuration

- **Protocols:** TLS 1.2 and TLS 1.3 only.
- **Ciphers:** ECDHE with AES-GCM and ChaCha20-Poly1305; no CBC, RC4, 3DES, MD5, SHA1.
- **DH parameters:** 2048-bit minimum (required for production; generated automatically for local).
- **HSTS:** `max-age=31536000; includeSubDomains; preload`
- **OCSP stapling:** enabled (harmless if unreachable).

## Certificate security

- Certificates and private keys live **outside** the Git repository.
- They are **not** copied into Docker images.
- They are persisted via Docker volumes (`nginx_certs`, `certbot_certs`).
- `.gitignore` and `.dockerignore` explicitly block `*.pem`, `*.key`, `*.crt`, `certs/`.

## Environment variables

| Variable | Example | Purpose |
|----------|---------|---------|
| `DOMAIN` | `splitit.example.com` | Domain for TLS and ACME |
| `TLS_MODE` | `letsencrypt` or `selfsigned` | Certificate source |
| `ACME_EMAIL` | `admin@example.com` | Let's Encrypt contact / ToS |
| `PROXY_HTTP_PORT` | `80` | Host HTTP port |
| `PROXY_HTTPS_PORT` | `443` | Host HTTPS port |
