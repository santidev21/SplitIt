using System.ComponentModel.DataAnnotations;

namespace SplitIt.Application.DTOs
{
    public class InviteMemberDto
    {
        [Range(1, int.MaxValue)]
        public int UserId { get; set; }
    }
}
