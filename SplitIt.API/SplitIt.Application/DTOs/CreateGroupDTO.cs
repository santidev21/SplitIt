using System.ComponentModel.DataAnnotations;

namespace SplitIt.Application.DTOs
{
    public class CreateGroupDto
    {
        [Required, StringLength(200, MinimumLength = 2)]
        public string Name { get; set; } = string.Empty;

        [Required, StringLength(500, MinimumLength = 1)]
        public string Description { get; set; } = string.Empty;

        public List<int> Members { get; set; } = new();

        public bool AllowToDeleteExpenses { get; set; } = false;

        [Range(1, int.MaxValue)]
        public int CurrencyId { get; set; }
    }
}
