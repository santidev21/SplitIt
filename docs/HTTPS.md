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

#### Automated renewal (recommended — Phase 12)

The `certbot-renewer` service runs `certbot renew` in a loop every 12 hours. On successful renewal, it writes a sentinel file to the shared `certbot_www` volume. The proxy entrypoint watches for this sentinel and reloads Nginx automatically — no manual intervention or `docker exec` required.

To enable:
1. Set `TLS_MODE=letsencrypt` in `.env`.
2. Uncomment `COMPOSE_PROFILES=letsencrypt` in `.env`.
3. Ensure the initial certificate has been issued (see above).
4. Start everything:
   ```bash
   docker compose up -d
   ```

The renewer will start alongside the proxy and handle renewals indefinitely.

#### Manual renewal (alternative)

If you prefer not to run the renewer container:
```bash
docker compose run --rm certbot renew
```

Then reload Nginx:
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

## Automated reload mechanism (Phase 12)

When the `certbot-renewer` successfully renews a certificate, certbot's `--deploy-hook` writes a sentinel file (`.reload-trigger`) to the shared `certbot_www` volume. The proxy entrypoint runs a background watcher that checks for this sentinel every 60 seconds. When found, the watcher removes it and runs `nginx -s reload`, picking up the new certificate without dropping connections or restarting the container.

```
certbot-renewer                  proxy
     |                              |
     |-- certbot renew             |-- nginx serving traffic
     |-- deploy-hook:              |-- background watcher (every 60s)
     |   touch .reload-trigger     |   if .reload-trigger exists:
     |                              |     rm .reload-trigger
     |                              |     nginx -s reload
     v                              v
  certbot_www volume <--------- shared --------->
```

## Environment variables

| Variable | Example | Purpose |
|----------|---------|---------|
| `DOMAIN` | `splitit.example.com` | Domain for TLS and ACME |
| `TLS_MODE` | `letsencrypt` or `selfsigned` | Certificate source |
| `ACME_EMAIL` | `admin@example.com` | Let's Encrypt contact / ToS |
| `PROXY_HTTP_PORT` | `80` | Host HTTP port |
| `PROXY_HTTPS_PORT` | `443` | Host HTTPS port |
| `COMPOSE_PROFILES` | `letsencrypt` | Enables certbot-renewer service |
| `RENEWAL_INTERVAL` | `12h` | Certbot renewal check interval |
