using System.ComponentModel.DataAnnotations;

namespace SplitIt.Application.DTOs
{
    public class LoginRequestDto
    {
        [Required, EmailAddress, StringLength(100)]
        [RegularExpression(@"^[^@\s]+@[^@\s]+\.[^@\s]{2,}$",
            ErrorMessage = "Enter a valid email address.")]
        public string Email { get; set; } = string.Empty;

        [Required, StringLength(100, MinimumLength = 1)]
        public string Password { get; set; } = string.Empty;
    }
}
