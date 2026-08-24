using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using System.ComponentModel.DataAnnotations;

namespace SplitIt.Application.DTOs
{
    public class CreateExpenseDto
    {
        [Required, StringLength(100, MinimumLength = 1)]
        public string Title { get; set; } = string.Empty;

        [StringLength(500)]
        public string? Note { get; set; }

        [Range(0.01, 1000000, ErrorMessage = "Amount must be between 0.01 and 1,000,000")]
        public decimal Amount { get; set; }

        [Required]
        public DateTime Date { get; set; }

        [Range(1, int.MaxValue)]
        public int GroupId { get; set; }

        [Range(1, int.MaxValue)]
        public int PaidById { get; set; }

        [MinLength(1, ErrorMessage = "At least one participant required")]
        public List<ExpenseParticipantDto> Participants { get; set; } = new();
    }

    public class ExpenseParticipantDto
    {
        [Range(1, int.MaxValue)]
        public int UserId { get; set; }

        [Range(0.01, 1000000)]
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
