using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace SplitIt.Domain.Entities
{
    public class ExpenseShare
    {
        public int Id { get; set; }

        public int ExpenseId { get; set; }

        [JsonIgnore]
        public Expense? Expense { get; set; }

        public int UserId { get; set; }

        public User? User { get; set; }

        public decimal AmountOwed { get; set; }

        /// <summary>
        /// Amount of this share that has already been paid/settled so far.
        /// <see cref="AmountOwed"/> is immutable after creation; the outstanding
        /// balance of a share is always <c>AmountOwed - AmountPaid</c>. Partial
        /// payments increment this value instead of mutating the original amount,
        /// so the expense history always reconciles (sum of shares == expense amount).
        /// </summary>
        public decimal AmountPaid { get; set; } = 0;

        public bool IsSettled { get; set; } = false;
        public DateTime? SettledAt { get; set; }

        // Optimistic concurrency: protects partial payments against lost updates.
        public byte[]? RowVersion { get; set; }
    }

}
