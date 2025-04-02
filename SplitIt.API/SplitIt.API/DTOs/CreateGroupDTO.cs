namespace SplitIt.API.DTOs
{
    public class CreateGroupDto
    {
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public List<int> Members { get; set; } = new();
        public bool AllowToDeleteExpenses { get; set; } = false;
        public int CurrencyId { get; set; } 
    }
}
