using System.ComponentModel.DataAnnotations;

namespace SplitIt.Application.DTOs
{
    public class RegisterRequestDto
    {
        [Required, StringLength(100, MinimumLength = 2)]
        public string Name { get; set; } = string.Empty;

        [Required, EmailAddress, StringLength(100)]
        [RegularExpression(@"^[^@\s]+@[^@\s]+\.[^@\s]{2,}$",
            ErrorMessage = "Enter a valid email address.")]
        public string Email { get; set; } = string.Empty;

        [Required, StringLength(100, MinimumLength = 8)]
        public string Password { get; set; } = string.Empty;

        /// <summary>
        /// Informed authorization (Ley 1581). Must be explicitly true — the UI checkbox is not pre-checked.
        /// </summary>
        [Range(typeof(bool), "true", "true", ErrorMessage = "You must accept the Privacy Policy and Terms to register.")]
        public bool AcceptTerms { get; set; }
    }
}
