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

        public async Task<Expense> AddExpenseAsync(CreateExpenseDto request, int createdById)
        {
            // Ownership check: createdBy must be member of group
            var isMember = await _context.GroupMembers.AnyAsync(gm => gm.GroupId == request.GroupId && gm.UserId == createdById);
            if (!isMember)
                throw new UnauthorizedAccessException("User is not a member of the group.");

            // Validate group exists
            var groupExists = await _context.Groups.AnyAsync(g => g.Id == request.GroupId);
            if (!groupExists)
                throw new KeyNotFoundException("Group not found.");

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

            // Validate participants are members and amounts >0
            var participantIds = request.Participants.Select(p => p.UserId).Distinct().ToList();
            var memberCount = await _context.GroupMembers.CountAsync(gm => gm.GroupId == request.GroupId && participantIds.Contains(gm.UserId));
            if (memberCount != participantIds.Count)
                throw new ArgumentException("One or more participants are not members of the group.");

            var sumOwed = request.Participants.Sum(p => p.AmountOwed);
            if (Math.Abs(sumOwed - request.Amount) > 0.02m)
                throw new ArgumentException($"Sum of participant amounts ({sumOwed}) does not match expense amount ({request.Amount}).");

            foreach (var p in request.Participants)
            {
                if (p.AmountOwed <= 0)
                    throw new ArgumentException("Participant amount must be positive.");
            }

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
            await _context.Expense.AddAsync(expense);
            await _context.SaveChangesAsync();

            var participants = request.Participants.Select(p => new ExpenseShare
            {
                UserId = p.UserId,
                ExpenseId = expense.Id,
                AmountOwed = p.AmountOwed
            }).ToList();

            await _context.ExpenseShare.AddRangeAsync(participants);
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
                    TotalAmountOwed = group.Sum(es => es.AmountOwed)
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
                    TotalAmountOwed = group.Sum(es => es.AmountOwed)
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
            // Net debt payer -> receiver
            var payerOwesReceiver = await _context.ExpenseShare
                .Where(es => !es.IsSettled && es.Expense.GroupId == groupId && es.UserId == payerUserId && es.Expense.PaidById == receiverUserId)
                .SumAsync(es => (decimal?)es.AmountOwed) ?? 0;
            var receiverOwesPayer = await _context.ExpenseShare
                .Where(es => !es.IsSettled && es.Expense.GroupId == groupId && es.UserId == receiverUserId && es.Expense.PaidById == payerUserId)
                .SumAsync(es => (decimal?)es.AmountOwed) ?? 0;
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

            var groupExists = await _context.Groups.AnyAsync(g => g.Id == groupId);
            if (!groupExists)
                throw new KeyNotFoundException("Group not found.");

            var payerMember = await _context.GroupMembers.AnyAsync(gm => gm.GroupId == groupId && gm.UserId == payerUserId);
            var receiverMember = await _context.GroupMembers.AnyAsync(gm => gm.GroupId == groupId && gm.UserId == receiverUserId);
            if (!payerMember || !receiverMember)
                throw new UnauthorizedAccessException("One or both users are not members of the group.");

            var remainingDebt = await GetRemainingDebtAsync(payerUserId, receiverUserId, groupId);
            if (remainingDebt <= 0.009m)
                throw new ArgumentException("No debt to settle between these users in this group.");

            if (amount > remainingDebt + 0.01m)
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

            await _context.Expense.AddAsync(expense);
            await _context.SaveChangesAsync();

            var expenseDetails = new ExpenseShare
            {
                UserId = receiverUserId,
                ExpenseId = expense.Id,
                AmountOwed = amount,
                IsSettled = true,
                SettledAt = DateTime.UtcNow,
            };

            await _context.ExpenseShare.AddRangeAsync(expenseDetails);
            await _context.SaveChangesAsync();

            // Apply partial settlement to existing debts (payer owes receiver)
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
                if (share.AmountOwed <= remainingPayment + 0.01m)
                {
                    remainingPayment = Math.Round(remainingPayment - share.AmountOwed, 2, MidpointRounding.AwayFromZero);
                    share.IsSettled = true;
                    share.SettledAt = DateTime.UtcNow;
                }
                else
                {
                    share.AmountOwed = Math.Round(share.AmountOwed - remainingPayment, 2, MidpointRounding.AwayFromZero);
                    remainingPayment = 0;
                }
            }

            // If still remaining (due to rounding or netting with opposite direction), also check opposite direction netting?
            // For now, if remainingPayment >0 and no payer->receiver shares left, it may be that receiver owes payer (net negative) — but we already validated net >0, so this shouldn't happen.

            await _context.SaveChangesAsync();

            return expenseDetails.Id;
        }
            
    }
}
