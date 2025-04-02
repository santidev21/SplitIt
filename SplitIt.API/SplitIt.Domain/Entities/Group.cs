using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SplitIt.Domain.Entities
{
    public class Group
    {
        public int Id { get; set; }  
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public int CurrencyId { get; set; }  
        public Currency Currency { get; set; } 

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public bool AllowToDeleteExpenses { get; set; }

        public ICollection<GroupMember> GroupMembers { get; set; } = new List<GroupMember>();
    }
}
