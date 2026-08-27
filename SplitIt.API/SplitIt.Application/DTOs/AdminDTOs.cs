namespace SplitIt.Application.DTOs
{
    public class UpdateUserRoleDto
    {
        public int RoleId { get; set; }
    }

    public class PromoteUserDto
    {
        [System.ComponentModel.DataAnnotations.Range(1, int.MaxValue)]
        public int UserId { get; set; }
    }

    public class SetUserActiveDto
    {
        public bool IsActive { get; set; }
    }

    public class UserAdminDto
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public int RoleId { get; set; }
        public string RoleName { get; set; } = string.Empty;
        public bool IsActive { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    public class GroupAdminDto
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public int CurrencyId { get; set; }
        public int MemberCount { get; set; }
        public int ExpenseCount { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    public class CreateCurrencyDto
    {
        [System.ComponentModel.DataAnnotations.Required]
        [System.ComponentModel.DataAnnotations.StringLength(100, MinimumLength = 1)]
        public string Name { get; set; } = string.Empty;

        [System.ComponentModel.DataAnnotations.Required]
        [System.ComponentModel.DataAnnotations.StringLength(10, MinimumLength = 1)]
        public string Symbol { get; set; } = string.Empty;
    }
}
