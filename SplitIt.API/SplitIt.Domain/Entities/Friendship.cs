using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SplitIt.Domain.Entities
{
    public class Friendship
    {
        public int Id { get; set; }

        public int RequesterId { get; set; }
        public User Requester { get; set; }

        public int AddresseeId { get; set; }
        public User Addressee { get; set; }

        // "pending" or "accepted"
        public string Status { get; set; } = "pending";

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? RespondedAt { get; set; }
    }
}
