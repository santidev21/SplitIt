using System.ComponentModel.DataAnnotations;

namespace SplitIt.Application.DTOs
{
    public class GoogleLoginDto
    {
        [Required]
        public string IdToken { get; set; } = string.Empty;

        /// <summary>
        /// Informed authorization (Ley 1581). Required only when the Google sign-in creates a new account.
        /// </summary>
        public bool AcceptTerms { get; set; }
    }
}
