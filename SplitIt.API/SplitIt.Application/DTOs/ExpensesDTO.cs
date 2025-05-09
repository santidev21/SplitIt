﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SplitIt.Application.DTOs
{
    public class CreateExpenseDto
    {
        public string Title { get; set; } = string.Empty;

        public string? Note { get; set; }

        public decimal Amount { get; set; }

        public DateTime Date { get; set; }

        public int GroupId { get; set; }
        public int PaidById { get; set; }

        public List<ExpenseParticipantDto> Participants { get; set; } = new();
    }

    public class ExpenseParticipantDto
    {
        public int UserId { get; set; }

        public decimal AmountOwed { get; set; }
    }

    public class ParticipantDto
    {
        public string Name { get; set; } = string.Empty;
        public decimal Amount { get; set; }
    }

    public class ExpenseDetailDto
    {
        public int Id { get; set; }
        public string Title { get; set; } = string.Empty;
        public decimal Amount { get; set; }
        public string PaidBy { get; set; } = string.Empty;
        public DateTime Date { get; set; }
        public string? Note { get; set; }
        public List<ParticipantDto> Participants { get; set; } = new();
    }
}
