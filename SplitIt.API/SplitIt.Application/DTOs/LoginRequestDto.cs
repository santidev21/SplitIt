using System.ComponentModel.DataAnnotations;

namespace SplitIt.Application.DTOs
{
    public class LoginRequestDto
    {
        [Required, EmailAddress, StringLength(100)]
        public string Email { get; set; } = string.Empty;

        [Required, StringLength(100, MinimumLength = 1)]
        public string Password { get; set; } = string.Empty;
    }
}
