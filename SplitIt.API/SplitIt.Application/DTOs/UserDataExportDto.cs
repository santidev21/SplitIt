using System;
using System.Collections.Generic;

namespace SplitIt.Application.DTOs
{
    /// <summary>Portable copy of a user's personal data (Ley 1581 access/portability right).</summary>
    public class UserDataExportDto
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
        public DateTime? ConsentAt { get; set; }
        public string? ConsentVersion { get; set; }
        public List<ExportGroupDto> Groups { get; set; } = new();
        public List<ExportExpenseDto> Expenses { get; set; } = new();
        public List<ExportFriendDto> Friends { get; set; } = new();
    }

    public class ExportGroupDto
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Role { get; set; } = string.Empty;
    }

    public class ExportExpenseDto
    {
        public int Id { get; set; }
        public int GroupId { get; set; }
        public string Title { get; set; } = string.Empty;
        public decimal Amount { get; set; }
        public DateTime Date { get; set; }
        public bool IsPayment { get; set; }
        public decimal MyOutstanding { get; set; }
        public bool? MyShareSettled { get; set; }
    }

    public class ExportFriendDto
    {
        public int UserId { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
    }
}
