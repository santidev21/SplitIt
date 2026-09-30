# ADR-004 — Deployment topology (single VPS behind `vps-gateway`)

- **Status:** Accepted
- **Date:** 2026-09-14

## Context

SplitIt runs on one VPS shared with other projects. TLS, routing and the public surface must be
consistent across projects, and the database must stay unreachable from the internet.

## Options

1. Expose each app directly with its own TLS and port — repeated TLS config, more open ports,
   inconsistent headers.
2. One reverse-proxy gateway terminates TLS and routes by `server_name`; each app publishes no host
   ports and attaches to a shared network.

## Decision

Deploy behind the **`vps-gateway`** reverse proxy (path `docs/specs/architecture.md`). The gateway
owns :80/:443 and TLS; SplitIt attaches to `splitit-net` (gateway ingress) and keeps SQL Server on
`splitit-internal-net` (`internal: true`), never publishing host ports.

## Consequences

- No per-app TLS or public ports; headers/rate limits are configured per site at the gateway.
- Every new project follows the same `vps-gateway` procedure (DNS → cert → site → reload).
- Testing against the real routing requires the gateway, so local dev uses the app's own proxy
  instead.
