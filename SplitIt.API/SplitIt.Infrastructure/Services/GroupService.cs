using Microsoft.EntityFrameworkCore;
using SplitIt.Application.DTOs;
using SplitIt.Domain.Entities;
using SplitIt.Infrastructure.Persistence;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SplitIt.Infrastructure.Services
{
    public class GroupService
    {
        private readonly AppDbContext _context;

        public GroupService(AppDbContext context)
        {
            _context = context;
        }
        public async Task<int> CreateGroup(string name, string description, bool allowToDeleteExpenses, int currencyId, int userId)
        {
            Group group = new Group()
            {
                Name = name,
                Description = description,
                CurrencyId = currencyId,
                CreatedAt = DateTime.Now,
            };
            _context.Groups.Add(group);
            await _context.SaveChangesAsync();

            return group.Id;
        }

        // Function to add members to the group. 
        // If a createdBy value is provided, it means this is a new group, and this user will be its creator.
        public async Task<bool> AddGroupMembers(int groupId, List<int> userMembers, int? creatorId)
        {
            List<GroupMember> members = new List<GroupMember>();

            foreach (int memberId in userMembers)
            {
                members.Add(new GroupMember()
                {
                    GroupId = groupId,
                    UserId = memberId,
                    Role = (creatorId.HasValue && creatorId.Value == memberId) ? "creator" : "member",
                });
            }

            _context.GroupMembers.AddRange(members);
            await _context.SaveChangesAsync();
            return true;
        }

        public async Task<List<UserGroupDto>> GetGroupsForUserAsync(int userId)
        {
            var groups = await _context.GroupMembers
            .Where(gm => gm.UserId == userId)
            .Select(gm => new UserGroupDto
            {
                Id = gm.Id,
                Name = gm.Group.Name,
                Description = gm.Group.Description,
                Role = gm.Role
            })
            .ToListAsync();
            // TODO: Try using Automapper
            return groups;
        }
        
    }
}
