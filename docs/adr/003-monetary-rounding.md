# ADR-003 — Monetary rounding and split distribution

- **Status:** Accepted
- **Date:** 2026-08-24

## Context

Splitting an amount across participants rarely divides evenly (`100/3`), and floating point makes
"the parts must add up to the whole" a real requirement for a money feature.

## Options

1. Let each client compute shares — simple, but two clients can disagree and totals drift.
2. Compute and validate on the server with integer cents and a defined rounding rule; the client
   mirrors the logic but never owns it.

## Decision

Amounts are `decimal(18,2)`; rounding is `MidpointRounding.AwayFromZero`; sum tolerances are
`0.01`–`0.02`. The **backend is the source of truth** for every split; the frontend mirrors the
logic, and the backend re-validates the sum on submit.

## Consequences

- Every split sums to exactly the total; the odd cent goes to the first members by a fixed rule.
- The frontend can never be trusted to be the only validator.
