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

    public class SimplifyDebtsDto
    {
        [System.ComponentModel.DataAnnotations.Range(1, int.MaxValue)]
        public int GroupId { get; set; }
    }

    public class SimplifiedTransferDto
    {
        public int FromUserId { get; set; }
        public string FromUserName { get; set; } = string.Empty;
        public int ToUserId { get; set; }
        public string ToUserName { get; set; } = string.Empty;
        public decimal Amount { get; set; }
    }

    /// <summary>
    /// Result of a debt simplification (preview or applied). <c>CurrentTransferCount</c> is
    /// the number of member pairs with a non-zero net debt today.
    /// </summary>
    public class DebtSimplificationDto
    {
        public int CurrentTransferCount { get; set; }
        public int SimplifiedTransferCount { get; set; }
        public bool CanSimplify { get; set; }
        public bool Applied { get; set; }
        public List<SimplifiedTransferDto> Transfers { get; set; } = new();
    }
}
