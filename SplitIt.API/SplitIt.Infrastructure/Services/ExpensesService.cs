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

        public ExpensesService(AppDbContext context)
        {
            _context = context;
        }

        public async Task<Expense> AddExpenseAsync(CreateExpenseDto request, int createdById)
        {
            var expense = new Expense
            {
                GroupId = request.GroupId,
                Title = request.Title,
                Amount = request.Amount,
                Date = request.Date,
                Note = request.Note,
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
    }
}
