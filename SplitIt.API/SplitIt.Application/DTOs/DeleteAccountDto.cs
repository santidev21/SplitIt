using System.ComponentModel.DataAnnotations;

namespace SplitIt.Application.DTOs
{
    public class DeleteAccountDto
    {
        /// <summary>Current password, required to confirm the deletion of the account.</summary>
        [Required, StringLength(100, MinimumLength = 1)]
        public string Password { get; set; } = string.Empty;
    }
}
