using System.ComponentModel.DataAnnotations;

namespace SplitIt.Application.DTOs
{
    public class UpdateGroupMemberRoleDto
    {
        [Required]
        [RegularExpression("^(admin|member)$", ErrorMessage = "Role must be admin or member")]
        public string Role { get; set; } = string.Empty;
    }
}
