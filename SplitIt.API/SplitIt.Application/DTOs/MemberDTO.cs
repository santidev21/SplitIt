namespace SplitIt.Application.DTOs
{
    public class MemberDto
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Role { get; set; } = "member";
        public string Email { get; set; } = string.Empty;
    }
}
