using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SplitIt.Domain.Entities
{
    public class User
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string PasswordHash { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public bool IsActive { get; set; } = true;

        public int RoleId { get; set; }
        public Role Role { get; set; }

        // Consent (Ley 1581): proof of the authorization given at registration.
        public DateTime? ConsentAt { get; set; }
        public string? ConsentVersion { get; set; }
        public string? ConsentIp { get; set; }

        // Set when the account is deleted (anonymized, not physically removed).
        public DateTime? DeletedAt { get; set; }
    }
}
