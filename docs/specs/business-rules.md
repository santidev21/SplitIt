# Business Rules

Current behavior, not a report. Derived from the Phase 8 feature work; this is the durable spec.

## Partial payments

- A payment is an `Expense` with `IsPayment = true` plus a settled `ExpenseShare`.
- `GetRemainingDebtAsync(payer, receiver, groupId)` = net debt `payer→receiver` minus
  `receiver→payer`, rounded to 2 decimals (`MidpointRounding.AwayFromZero`).
- `RegisterPayment(payer, receiver, groupId, amount)` validates: `0 < amount <= remaining`,
  `payer != receiver`, both are group members, group exists, and the amount has no more decimals
  than the group currency allows (excess precision is rejected, never rounded). Zero/negative and
  "no debt" are rejected.
- Distribution: `AmountOwed` is immutable; payments only advance `AmountPaid`. Because `remaining`
  is a net figure, the receiver's open debt to the payer is first offset on both sides, then the
  payment settles the payer's shares oldest-first (`Expense.Date`). A share is settled when
  `AmountPaid` reaches `AmountOwed`. Paying the full net amount leaves no open share in either
  direction.
- `POST /api/expenses/settle` handles direction swapping (tries `payer→receiver`, then reversed,
  uses `Math.Abs`) and returns `{ PaymentId, RemainingDebt, SettledCount }`.
  `GET /api/expenses/remaining-debt?otherUserId&groupId` feeds the UI.

## Debt simplification

- Goal: settle the group with the fewest transfers while every member keeps exactly the same net
  balance (e.g. A owes B 200 and B owes C 200 → A pays C 200).
- Net balance per member = open debt shares owed to them − open debt shares they owe (a payer's own
  share is never a debt). `DebtSimplifier` turns balances into a transfer plan: exact minimum (bitmask
  search over zero-sum subgroups) for up to 16 members with a non-zero balance, greedy above that;
  always at most `N − 1` transfers, computed in integer cents.
- `GET /api/expenses/simplify-preview?groupId` (any member) returns the plan without writing.
  `POST /api/expenses/simplify { groupId }` (creator/admin only, it changes who owes whom for
  everyone) applies it atomically: all open debt shares are closed (`AmountPaid = AmountOwed`) and
  one `Expense` with `IsSimplification = true` is created per transfer, paid by the creditor with a
  single share owed by the debtor. Nothing is written when the plan would not reduce the number of
  transfers (current count = member pairs with a non-zero net debt).
- Consolidated debts behave like any other debt: partial and full payments, and new expenses stack
  on top. Simplification records are hidden from the expense list and shown in the payments history.

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
