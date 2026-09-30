# ADR-002 — Angular 21 and .NET 8 baseline

- **Status:** Accepted
- **Date:** 2026-09-21

## Context

The project needs a frontend and backend stack that a small team can move fast with and still
deploy safely to a single VPS.

## Options

1. Track the newest framework releases aggressively.
2. Stay one or two releases behind on a supported LTS baseline and upgrade in dedicated changes.

## Decision

Use **Angular 21** (Material, SCSS) and **.NET 8** with Clean Architecture. Treat framework
upgrades as their own dedicated change, never mixed into a feature.

## Consequences

- Angular 19 is out of support; an upgrade past the current release is planned work, not a surprise.
- Features stay reviewable because a diff is either a feature or an upgrade, never both.
