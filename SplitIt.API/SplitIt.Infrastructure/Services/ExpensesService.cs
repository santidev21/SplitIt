    using Microsoft.EntityFrameworkCore;
using SplitIt.Application.DTOs;
using SplitIt.Domain.Entities;
using SplitIt.Infrastructure.Persistence;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SplitIt.Infrastructure.Services
{
    public class ExpensesService
    {
        private readonly AppDbContext _context;
        private readonly SettingsService? _settingsService;

        public ExpensesService(AppDbContext context, SettingsService? settingsService = null)
        {
            _context = context;
            _settingsService = settingsService;
        }

        /// <summary>
        /// True when <paramref name="value"/> has more significant decimal places than
        /// the currency allows (e.g. 100.259 for a 2-decimal currency, or 50.50 for a
        /// whole-unit currency). The check uses a scale multiplier, so it is exact for
        /// decimal values with a small, controlled number of places.
        /// </summary>
        private static bool HasExcessPrecision(decimal value, int decimalPlaces)
        {
            if (decimalPlaces <= 0)
                return value != decimal.Truncate(value);

            var scale = (decimal)Math.Pow(10, decimalPlaces);
            return value * scale != decimal.Round(value * scale);
        }

        public async Task<Expense> AddExpenseAsync(CreateExpenseDto request, int createdById)
        {
            // Ownership check: createdBy must be member of group
            var isMember = await _context.GroupMembers.AnyAsync(gm => gm.GroupId == request.GroupId && gm.UserId == createdById);
            if (!isMember)
                throw new UnauthorizedAccessException("User is not a member of the group.");

            // Validate group exists and load its currency scale (fixed at group creation)
            var group = await _context.Groups
                .Include(g => g.Currency)
                .FirstOrDefaultAsync(g => g.Id == request.GroupId);
            if (group == null)
                throw new KeyNotFoundException("Group not found.");

            var currencyDecimalPlaces = group.Currency?.DecimalPlaces ?? 2;

            // Validate PaidBy is member
            var paidByMember = await _context.GroupMembers.AnyAsync(gm => gm.GroupId == request.GroupId && gm.UserId == request.PaidById);
            if (!paidByMember)
                throw new ArgumentException("PaidBy user is not a member of the group.");

            if (request.Participants == null || request.Participants.Count == 0)
                throw new ArgumentException("At least one participant required.");
            if (request.Participants.Count > 50)
                throw new ArgumentException("Too many participants.");

            var maxAmount = _settingsService != null
                ? await _settingsService.GetValueAsync(SettingsService.MaxExpenseAmount, 1000000m)
                : 1000000m;
            if (request.Amount <= 0 || request.Amount > maxAmount)
                throw new ArgumentException($"Invalid amount. Amount must be between 0.01 and {maxAmount:0}.");
            if (HasExcessPrecision(request.Amount, currencyDecimalPlaces))
                throw new ArgumentException($"Amount cannot have more than {currencyDecimalPlaces} decimal place(s) for this currency.");

            // Validate participants are members, amounts are positive and use the currency scale
            var participantIds = request.Participants.Select(p => p.UserId).Distinct().ToList();
            var memberCount = await _context.GroupMembers.CountAsync(gm => gm.GroupId == request.GroupId && participantIds.Contains(gm.UserId));
            if (memberCount != participantIds.Count)
                throw new ArgumentException("One or more participants are not members of the group.");

            foreach (var p in request.Participants)
            {
                if (p.AmountOwed <= 0)
                    throw new ArgumentException("Participant amount must be positive.");
                if (HasExcessPrecision(p.AmountOwed, currencyDecimalPlaces))
                    throw new ArgumentException($"Participant amounts cannot have more than {currencyDecimalPlaces} decimal place(s) for this currency.");
            }

            // The participant shares must conserve the expense total exactly at the
            // currency's scale. Sums that differ (even by a cent) are rejected instead
            // of being persisted as a phantom debt.
            var sumOwed = request.Participants.Sum(p => p.AmountOwed);
            if (sumOwed != request.Amount)
                throw new ArgumentException($"Sum of participant amounts ({sumOwed}) must equal expense amount ({request.Amount}).");

            var expense = new Expense
            {
                GroupId = request.GroupId,
                Title = request.Title.Trim(),
                Amount = request.Amount,
                Date = request.Date.ToUniversalTime(),
                Note = request.Note?.Trim(),
                CreatedById = createdById,
                PaidById = request.PaidById,
            };

            // Atomic write: add the shares through the navigation and save once, so EF
            // wraps the expense + shares in a single implicit transaction. (No user-initiated
            // transaction here, which keeps the SQL Server retrying execution strategy usable.)
            foreach (var p in request.Participants)
            {
                expense.Shares.Add(new ExpenseShare
                {
                    UserId = p.UserId,
                    AmountOwed = p.AmountOwed
                });
            }

            await _context.Expense.AddAsync(expense);
            await _context.SaveChangesAsync();

            return expense;
        }

        public async Task<List<ExpenseDetailDto>> GetExpensesByGroupIdAsync(int groupId, int userId, bool showAll)
        {
            var expenses = await _context.Expense
                .Where(e => e.GroupId == groupId)
                .Include(e => e.PaidBy)
                .Include(e => e.Shares)
                .ThenInclude(ep => ep.User)
                .OrderByDescending(e => e.Date)
                .ToListAsync();

            if (!showAll)
            {
                expenses = expenses
                    .Where(e => e.PaidBy.Id == userId || e.Shares.Any(s => s.UserId == userId))
                    .ToList();
            }

            var expenseDetails = expenses.Select(expense => new ExpenseDetailDto
            {
                Id = expense.Id,
                Title = expense.Title,
                Amount = expense.Amount,
                PaidBy = expense.PaidBy.Name,
                Date = expense.Date,
                Note = expense.Note,
                IsPayment = expense.IsPayment,
                Participants = expense.Shares.Select(share => new ParticipantDto
                {
                    Name = share.User.Name,
                    Amount = share.AmountOwed
                }).ToList()
            }).ToList();

            
            return expenseDetails;
        }

        public async Task<List<DebtOwedByUserDto>> GetDebtsOwedByUserAsync(int userId, int groupId)
        {
            return await _context.ExpenseShare
                .Where(es => es.UserId == userId && !es.IsSettled && es.Expense.GroupId == groupId && es.Expense.PaidById != userId)
                .GroupBy(es => new { es.Expense.PaidById, es.Expense.PaidBy!.Name })
                .Select(group => new DebtOwedByUserDto
                {
                    CreditorUserId = group.Key.PaidById,
                    CreditorUserName = group.Key.Name,
                    // Outstanding = original owed minus what has already been paid (AmountOwed is immutable).
                    TotalAmountOwed = group.Sum(es => es.AmountOwed - es.AmountPaid)
                })
                .ToListAsync();
        }

        public async Task<List<DebtOwedToUserDto>> GetDebtsOwedToUserAsync(int userId, int groupId)
        {
            return await _context.ExpenseShare
                .Where(es => es.Expense.PaidById == userId && es.UserId != userId && !es.IsSettled && es.Expense.GroupId == groupId && es.UserId != userId)
                .GroupBy(es => new { es.UserId, es.User!.Name })
                .Select(group => new DebtOwedToUserDto
                {
                    DebtorUserId = group.Key.UserId,
                    DebtorUserName = group.Key.Name,
                    TotalAmountOwed = group.Sum(es => es.AmountOwed - es.AmountPaid)
                })
                .ToListAsync();
        }

        public async Task<FullDebtSummaryDto> GetFullDebtSummaryAsync(int userId, int groupId)
        {
            var debtsOwedByUser = await GetDebtsOwedByUserAsync(userId, groupId);
            var debtsOwedToUser = await GetDebtsOwedToUserAsync(userId, groupId);

            var adjustedDebtsOwedByUser = new List<DebtOwedByUserDto>(debtsOwedByUser);
            var adjustedDebtsOwedToUser = new List<DebtOwedToUserDto>(debtsOwedToUser);

            foreach (var debtBy in debtsOwedByUser)
            {
                var matchingDebtTo = adjustedDebtsOwedToUser
                    .FirstOrDefault(d => d.DebtorUserId == debtBy.CreditorUserId);

                if (matchingDebtTo == null)
                    continue;

                if (debtBy.TotalAmountOwed > matchingDebtTo.TotalAmountOwed)
                {
                    var newAmount = debtBy.TotalAmountOwed - matchingDebtTo.TotalAmountOwed;

                    adjustedDebtsOwedByUser
                        .First(d => d.CreditorUserId == debtBy.CreditorUserId)
                        .TotalAmountOwed = newAmount;

                    adjustedDebtsOwedToUser.Remove(matchingDebtTo);
                }
                else if (matchingDebtTo.TotalAmountOwed > debtBy.TotalAmountOwed)
                {
                    var newAmount = matchingDebtTo.TotalAmountOwed - debtBy.TotalAmountOwed;

                    adjustedDebtsOwedToUser
                        .First(d => d.DebtorUserId == matchingDebtTo.DebtorUserId)
                        .TotalAmountOwed = newAmount;

                    adjustedDebtsOwedByUser.RemoveAll(d => d.CreditorUserId == debtBy.CreditorUserId);
                }
                else
                {
                    adjustedDebtsOwedByUser.RemoveAll(d => d.CreditorUserId == debtBy.CreditorUserId);
                    adjustedDebtsOwedToUser.Remove(matchingDebtTo);
                }
            }

            return new FullDebtSummaryDto
            {
                DebtsOwedByUser = adjustedDebtsOwedByUser,
                DebtsOwedToUser = adjustedDebtsOwedToUser
            };
        }

        public async Task<decimal> GetRemainingDebtAsync(int payerUserId, int receiverUserId, int groupId)
        {
            // Net debt payer -> receiver, based on the outstanding balance of each share
            // (AmountOwed - AmountPaid). AmountOwed is never mutated by payments.
            var payerOwesReceiver = await _context.ExpenseShare
                .Where(es => !es.IsSettled && es.Expense.GroupId == groupId && es.UserId == payerUserId && es.Expense.PaidById == receiverUserId)
                .SumAsync(es => (decimal?)(es.AmountOwed - es.AmountPaid)) ?? 0;
            var receiverOwesPayer = await _context.ExpenseShare
                .Where(es => !es.IsSettled && es.Expense.GroupId == groupId && es.UserId == receiverUserId && es.Expense.PaidById == payerUserId)
                .SumAsync(es => (decimal?)(es.AmountOwed - es.AmountPaid)) ?? 0;
            return Math.Round(payerOwesReceiver - receiverOwesPayer, 2, MidpointRounding.AwayFromZero);
        }

        public async Task<int> SettleExpenseWithUser(int payerUserId, int receiverUserId, int groupId)
        {
            // Full settlement: settle all shares between the two users in the group (both directions net to zero)
            var groupExists = await _context.Groups.AnyAsync(g => g.Id == groupId);
            if (!groupExists)
                throw new KeyNotFoundException("Group not found.");

            var payerMember = await _context.GroupMembers.AnyAsync(gm => gm.GroupId == groupId && gm.UserId == payerUserId);
            var receiverMember = await _context.GroupMembers.AnyAsync(gm => gm.GroupId == groupId && gm.UserId == receiverUserId);
            if (!payerMember || !receiverMember)
                throw new UnauthorizedAccessException("One or both users are not members of the group.");

            var unsettledShares = await _context.ExpenseShare
            .Include(es => es.Expense)
            .Where(es =>
                !es.IsSettled && es.Expense.GroupId == groupId && (
                    (es.UserId == receiverUserId && es.Expense.PaidById == payerUserId) ||
                    (es.UserId == payerUserId && es.Expense.PaidById == receiverUserId)
                )
            )
            .ToListAsync();

            foreach (var share in unsettledShares)
            {
                share.AmountPaid = share.AmountOwed;
                share.IsSettled = true;
                share.SettledAt = DateTime.UtcNow;
            }

            await _context.SaveChangesAsync();

            return unsettledShares.Count;
        }

        public async Task<int> RegisterPayment(int payerUserId, int receiverUserId, int groupId, decimal amount)
        {
            // Monetary precision: round to 2 decimals away from zero
            amount = Math.Round(amount, 2, MidpointRounding.AwayFromZero);
            if (amount <= 0 || amount > 1000000)
                throw new ArgumentException("Invalid payment amount.");
            if (payerUserId == receiverUserId)
                throw new ArgumentException("Payer and receiver must be different.");

            var group = await _context.Groups
                .Include(g => g.Currency)
                .FirstOrDefaultAsync(g => g.Id == groupId) ?? throw new KeyNotFoundException("Group not found.");
            var currencyDecimalPlaces = group.Currency?.DecimalPlaces ?? 2;

            var payerMember = await _context.GroupMembers.AnyAsync(gm => gm.GroupId == groupId && gm.UserId == payerUserId);
            var receiverMember = await _context.GroupMembers.AnyAsync(gm => gm.GroupId == groupId && gm.UserId == receiverUserId);
            if (!payerMember || !receiverMember)
                throw new UnauthorizedAccessException("One or both users are not members of the group.");

            if (HasExcessPrecision(amount, currencyDecimalPlaces))
                throw new ArgumentException($"Payment amount cannot have more than {currencyDecimalPlaces} decimal place(s) for this currency.");

            var remainingDebt = await GetRemainingDebtAsync(payerUserId, receiverUserId, groupId);
            if (remainingDebt <= 0.009m)
                throw new ArgumentException("No debt to settle between these users in this group.");

            // A payment must never exceed the actual remaining debt, otherwise the
            // extra amount would be recorded as money transferred without reducing
            // any debt.
            if (amount > remainingDebt)
                throw new ArgumentException($"Payment {amount} exceeds remaining debt {remainingDebt}.");

            var expense = new Expense
            {
                GroupId = groupId,
                Title = "Debt Payment",
                Amount = amount,
                Date = DateTime.UtcNow,
                Note = "Payment",
                CreatedById = receiverUserId,
                PaidById = payerUserId,
                IsPayment = true,
            };

            var paymentShare = new ExpenseShare
            {
                UserId = receiverUserId,
                AmountOwed = amount,
                AmountPaid = amount,
                IsSettled = true,
                SettledAt = DateTime.UtcNow,
            };
            expense.Shares.Add(paymentShare);

            // Apply the payment to the payer's outstanding shares.
            // IMPORTANT: AmountOwed is immutable — we only increment AmountPaid, so the
            // expense history always reconciles (sum of shares == expense amount) even
            // after partial payments.
            var remainingPayment = amount;
            var shares = await _context.ExpenseShare
                .Include(es => es.Expense)
                .Where(es => !es.IsSettled && es.Expense.GroupId == groupId && es.UserId == payerUserId && es.Expense.PaidById == receiverUserId)
                .OrderBy(es => es.Expense.Date)
                .ThenBy(es => es.Id)
                .ToListAsync();

            foreach (var share in shares)
            {
                if (remainingPayment <= 0.009m) break;

                var outstanding = Math.Round(share.AmountOwed - share.AmountPaid, 2, MidpointRounding.AwayFromZero);
                if (outstanding <= 0.009m) continue;

                var applied = Math.Min(outstanding, remainingPayment);
                share.AmountPaid = Math.Round(share.AmountPaid + applied, 2, MidpointRounding.AwayFromZero);
                remainingPayment = Math.Round(remainingPayment - applied, 2, MidpointRounding.AwayFromZero);

                if (share.AmountPaid >= share.AmountOwed - 0.009m)
                {
                    share.AmountPaid = share.AmountOwed;
                    share.IsSettled = true;
                    share.SettledAt = DateTime.UtcNow;
                }
            }

            // Single SaveChanges => EF wraps the payment expense, its share and the
            // AmountPaid updates in one implicit transaction (retry-strategy friendly).
            await _context.Expense.AddAsync(expense);
            await _context.SaveChangesAsync();

            return paymentShare.Id;
        }
            
    }
}
