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
                CreatedById = createdById
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
    }
}
