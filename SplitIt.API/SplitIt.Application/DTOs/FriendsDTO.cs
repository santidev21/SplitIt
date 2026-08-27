using System.ComponentModel.DataAnnotations;

namespace SplitIt.Application.DTOs
{
    public class SendFriendRequestDto
    {
        /// <summary>Target user id (preferred). Either UserId or Email must be provided.</summary>
        public int? UserId { get; set; }

        /// <summary>Target user email (alternative lookup). Either UserId or Email must be provided.</summary>
        [EmailAddress]
        public string? Email { get; set; }
    }

    public class RespondFriendRequestDto
    {
        [Required]
        public bool Accept { get; set; }
    }

    public class FriendDto
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
    }

    public class FriendRequestDto
    {
        public int FriendshipId { get; set; }
        public int UserId { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
    }

    public class SearchUserDto
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
    }
}
