# ADR-001 — Store the JWT in `localStorage`

- **Status:** Accepted
- **Date:** 2026-08-24

## Context

The Angular SPA must attach a JWT to API calls and keep the session across reloads. The token needs
a client-side home that survives navigation.

## Options

1. `localStorage` — simplest; readable by any script on the page, so a successful XSS can steal it.
2. `HttpOnly` cookie — not readable by JS (XSS cannot exfiltrate it) but introduces CSRF and forces
   `SameSite` + anti-CSRF handling.

## Decision

Keep the token in `localStorage`. Treat it as an accepted, documented risk and make XSS prevention
non-negotiable: strict input validation, a restrictive CSP at the proxy, and no `innerHTML` on
untrusted data.

## Consequences

- A stored XSS could exfiltrate the token; the compensating control is strict XSS prevention plus
  CSP, not the storage mechanism.
- Moving to `HttpOnly` cookies later must add CSRF protection in the same change — never do half of
  it.
