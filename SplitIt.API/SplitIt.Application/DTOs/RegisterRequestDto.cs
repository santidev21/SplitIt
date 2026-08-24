using System.ComponentModel.DataAnnotations;

namespace SplitIt.Application.DTOs
{
    public class RegisterRequestDto
    {
        [Required, StringLength(100, MinimumLength = 2)]
        public string Name { get; set; } = string.Empty;

        [Required, EmailAddress, StringLength(100)]
        public string Email { get; set; } = string.Empty;

        [Required, StringLength(100, MinimumLength = 8)]
        public string Password { get; set; } = string.Empty;
    }
}
