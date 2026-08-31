using System.ComponentModel.DataAnnotations;

namespace SplitIt.Application.DTOs
{
    public class GoogleLoginDto
    {
        [Required]
        public string IdToken { get; set; } = string.Empty;
    }
}
