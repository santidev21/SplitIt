using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SplitIt.Application.DTOs
{
    public class DebtOwedToUserDto
    {
        public int DebtorUserId { get; set; }
        public string DebtorUserName { get; set; } = string.Empty;
        public decimal TotalAmountOwed { get; set; }
    }

    public class DebtOwedByUserDto
    {
        public int CreditorUserId { get; set; }
        public string CreditorUserName { get; set; } = string.Empty;
        public decimal TotalAmountOwed { get; set; }
    }

    public class FullDebtSummaryDto
    {
        public List<DebtOwedByUserDto> DebtsOwedByUser { get; set; } = new();
        public List<DebtOwedToUserDto> DebtsOwedToUser { get; set; } = new();
    }
}
