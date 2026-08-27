namespace SplitIt.Application.DTOs
{
    public class GroupDetailDTO
    {
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public bool AllowToDeleteExpenses { get; set; }
        public int CurrencyId { get; set; }
    }

    public class UpdateGroupDto
    {
        [System.ComponentModel.DataAnnotations.Required, System.ComponentModel.DataAnnotations.StringLength(200, MinimumLength = 2)]
        public string Name { get; set; } = string.Empty;

        [System.ComponentModel.DataAnnotations.Required, System.ComponentModel.DataAnnotations.StringLength(500, MinimumLength = 1)]
        public string Description { get; set; } = string.Empty;

        public bool AllowToDeleteExpenses { get; set; }
    }
}
