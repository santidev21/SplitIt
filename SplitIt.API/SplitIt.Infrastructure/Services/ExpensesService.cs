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

    }
}
