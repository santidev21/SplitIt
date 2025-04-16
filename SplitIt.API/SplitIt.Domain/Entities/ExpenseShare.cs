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
    }

}
