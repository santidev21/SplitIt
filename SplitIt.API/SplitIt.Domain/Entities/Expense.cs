using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SplitIt.Domain.Entities
{
    public class Expense
    {
        public int Id { get; set; }

        public string Title { get; set; } = string.Empty;

        public string? Note { get; set; }

        public decimal Amount { get; set; }

        public DateTime Date { get; set; }

        public int GroupId { get; set; }

        public Group? Group { get; set; }

        public int CreatedById { get; set; }

        public User? CreatedBy { get; set; }
        public int PaidById { get; set; }
        public User? PaidBy { get; set; }

        public ICollection<ExpenseShare> Shares { get; set; } = new List<ExpenseShare>();
        public bool IsPayment { get; set; } = false;

        /// <summary>
        /// True for the consolidated debts created by "simplify debts": the open shares they
        /// replace are closed and the debtor's share of this expense carries the balance.
        /// </summary>
        public bool IsSimplification { get; set; } = false;

        // Optimistic concurrency: prevents lost updates on concurrent edits.
        public byte[]? RowVersion { get; set; }
    }
}
