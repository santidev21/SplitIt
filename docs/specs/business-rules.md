# Business Rules

Current behavior, not a report. Derived from the Phase 8 feature work; this is the durable spec.

## Partial payments

- A payment is an `Expense` with `IsPayment = true` plus a settled `ExpenseShare`.
- `GetRemainingDebtAsync(payer, receiver, groupId)` = net debt `payer→receiver` minus
  `receiver→payer`, rounded to 2 decimals (`MidpointRounding.AwayFromZero`).
- `RegisterPayment(payer, receiver, groupId, amount)` validates: `0 < amount <= remaining + 0.01`,
  `payer != receiver`, both are group members, group exists. Zero/negative and "no debt" are
  rejected.
- Distribution: the payment settles the payer's unsettled shares oldest-first (`Expense.Date`); a
  share is fully settled when `AmountOwed <= remainingPayment`, otherwise reduced by the remainder.
  Payments accumulate to an exact zero remaining.
- `POST /api/expenses/settle` handles direction swapping (tries `payer→receiver`, then reversed,
  uses `Math.Abs`) and returns `{ PaymentId, RemainingDebt, SettledCount }`.
  `GET /api/expenses/remaining-debt?otherUserId&groupId` feeds the UI.

## Split methods

- **Equal:** `perPerson = floor(total / count * 100) / 100`; the remainder (in cents) is distributed
  `+0.01` to the first members so the sum is exactly `total` (e.g. `100/3 → 33.34, 33.33, 33.33`).
- **Fixed:** only members with `amount > 0`; the sum must equal `total ± 0.01` or the split is
  rejected.
- **Percentage:** only members with `pct > 0`; the percentages must sum to `100 ± 0.01`, each
  `0–100`; `amountOwed = round(pct/100 * total, 2)`; the backend re-checks the sum.
- No negative or zero allocations.

## Monetary precision

- Amounts are `decimal(18,2)`; rounding is `MidpointRounding.AwayFromZero`; sum tolerances are
  `0.01`–`0.02`. The frontend uses JS numbers but rounds to 2 decimals — the backend is the source
  of truth.

## Group roles

- `creator > admin > member`.
- Only creator/admin change roles; only the creator promotes to admin; the creator's role and one's
  own role cannot be changed.
- Creator removes admin/member; admin removes member only; nobody removes the creator. Only the
  creator deletes the group.

## Application roles

- `1 super > 2 admin > 3 user` (seeded `Role` table).
- Only a super admin changes roles; `GET /api/admin/users` requires admin;
  `PUT /api/admin/users/{id}/role` requires super. The server never trusts a client-supplied role;
  `RoleId` is assigned server-side on register.

## Email

- Trimmed and lowercased; `[EmailAddress]`/length validated; duplicates are rejected
  case-insensitively (409).
