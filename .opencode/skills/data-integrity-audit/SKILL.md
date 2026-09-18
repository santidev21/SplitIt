---
name: data-integrity-audit
description: Audit SplitIt for data-loss and data-consistency risks. Use when touching entities, deletes, cascade rules, migrations, money/settlement logic, or when asked about edge cases, backups, traceability or "que no se dañe la data".
---

# Data integrity audit (SplitIt)

Read-only by default. Report findings as `[Severity] Title — path:line — impact — fix`. This skill complements `security-review`: security is about attackers, this is about **losing or corrupting real users' financial data**.

## 1. Destructive operations
- Find every hard delete: `.Remove(` / `.RemoveRange(` / `ExecuteDelete`. Known spots: `GroupService.DeleteGroupAsync`, `GroupService.RemoveMemberAsync`, `FriendshipsService.RemoveFriendAsync`, `AdminController.DeleteCurrency/DeletePasswordResetToken`, `AuthService`/`TokenService` token cleanup.
- Check `AppDbContext.OnModelCreating` delete behaviours. `Expense` and `ExpenseShare` cascade from `Group` — deleting a group destroys its entire financial history with no recovery.
- Verdict rule: any user-triggered delete of financial records without soft delete + backup is **High/Critical** for a production app.
- Expected direction: soft delete (`IsDeleted`/`DeletedAt`/`DeletedBy`) + `HasQueryFilter`, and/or restrict cascades on financial data.

## 2. Domain invariants (must always hold)
Verify with `scripts/db-integrity-audit.sql` (read-only) and in code:
- `Expense.Amount > 0`; `ExpenseShare.AmountOwed > 0`.
- For non-payment expenses: `SUM(ExpenseShare.AmountOwed) == Expense.Amount` (±0.01) and at least one share.
- Every `ExpenseShare.UserId` is a `GroupMember` of `Expense.GroupId`.
- Every `Expense.CreatedById` and `Expense.PaidById` is a `GroupMember` of `Expense.GroupId`.
- `(GroupId, UserId)` in `GroupMembers` is unique — **confirm the unique index exists**; if not, duplicate membership is possible.
- `Friendship` is symmetric/normalized — confirm only one row per pair (the index is `(RequesterId, AddresseeId)`; a reverse duplicate `B→A` is only prevented by app logic, not the DB).
- `IsSettled == true` ⟺ `SettledAt != null`.
- `Expense.Date` is not in the future; all timestamps are UTC.
- No orphan `ExpenseShare` / `Expense` rows (FK integrity).
- A `GroupMember` removed from a group may still own `ExpenseShare` rows — check debt-summary queries don't break or silently drop that debt.

## 3. Money & rounding
- `decimal(18,2)` everywhere; never `double`/`float`.
- Split logic (`SplitCalculator`, equal/amount/percentage) must be deterministic and end with `sum(shares) == total`; no lost/gained cents.
- Settlement/payment flows scoped by `groupId` and idempotent (a retry must not double-pay).
- `SettingsService.MaxExpenseAmount` and the 50-member/participant caps are enforced server-side.

## 4. Concurrency
- Concurrent edits (two users adding to the same group) can overwrite each other. Check for optimistic concurrency tokens (`rowversion` in SQL Server) on editable entities; absence is a Medium/High finding for money-write paths.

## 5. Traceability (Ley 1581)
- There is no audit trail today. Recommended: an `AuditLog` (entity, entityId, action create/update/delete, actorUserId, timestamp UTC, changed fields JSON, IP).
- Deletions and role/active changes must be attributable to an actor.
- Password-reset and refresh-token handling must not leak or retain raw secrets.

## 6. Backup & recovery dependency
- Data safeguards in code are not a substitute for backups. Confirm `scripts/db-backup.sh` exists, runs on a schedule, keeps a bounded retention, verifies the `.bak`, and stores copies **off the DB volume**.
- A restore drill must be documented and reproducible (`docs/BACKUPS.md`).
- Pre-migration backups must happen before any schema change.

## 7. Test coverage for safety
- Each invariant above should have a regression test in `SplitIt.Tests/`.
- Required negatives: cross-group settle, duplicate membership, remove-member-with-debt, delete-group cascade, concurrent update, rounding boundaries (`0.01`, `0.005`), retry/idempotency.

## Output format
```
[Severity: Critical|High|Medium|Low] Title
Where: path:line
Impact: what data is lost/corrupted and under what action
Fix: concrete remediation (soft delete / constraint / transaction / test)
```
Order by severity. State explicitly which invariants pass. If the data layer is clean, say so — do not invent issues.
