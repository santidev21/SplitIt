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
            // Validate currency exists
            var currencyExists = await _context.Currencies.AnyAsync(c => c.Id == currencyId);
            if (!currencyExists)
                throw new ArgumentException("Invalid currency.");

            Group group = new Group()
            {
                Name = name.Trim(),
                Description = description.Trim(),
                CurrencyId = currencyId,
                CreatedAt = DateTime.UtcNow,
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
                Id = gm.GroupId,
                Name = gm.Group.Name,
                Description = gm.Group.Description,
                Role = gm.Role
            })
            .ToListAsync();
            // TODO: Try using Automapper
            return groups;
        }

        public async Task<List<MemberDto>> GetGroupMembersAsync(int groupId, int currentUserId)
        {
            var group = await _context.Groups
                .Include(g => g.GroupMembers)
                .ThenInclude(gm => gm.User)
                .FirstOrDefaultAsync(g => g.Id == groupId);

            if (group == null)
                throw new KeyNotFoundException("Group not found");

            return group.GroupMembers
                .Select(m => new MemberDto
                {
                    Id = m.UserId,
                    Name = m.UserId == currentUserId ? "You" : m.User.Name
                })
                .ToList();
        }

        public async Task<string?> GetUserGroupRoleAsync(int groupId, int userId)
        {
            var membership = await _context.GroupMembers
                .FirstOrDefaultAsync(gm => gm.GroupId == groupId && gm.UserId == userId);

            return membership?.Role;
        }

        public async Task<bool> IsUserMemberAsync(int groupId, int userId)
        {
            return await _context.GroupMembers.AnyAsync(gm => gm.GroupId == groupId && gm.UserId == userId);
        }

        public async Task<bool> IsUserAdminOrCreatorAsync(int groupId, int userId)
        {
            var role = await GetUserGroupRoleAsync(groupId, userId);
            return role == "creator" || role == "admin";
        }

        public async Task<bool> IsUserCreatorAsync(int groupId, int userId)
        {
            var role = await GetUserGroupRoleAsync(groupId, userId);
            return role == "creator";
        }

        public async Task UpdateMemberRoleAsync(int groupId, int targetUserId, string newRole, int requesterId)
        {
            newRole = newRole.ToLowerInvariant();
            if (newRole != "admin" && newRole != "member")
                throw new ArgumentException("Invalid role. Allowed: admin, member.");

            var requesterRole = await GetUserGroupRoleAsync(groupId, requesterId);
            if (requesterRole != "creator" && requesterRole != "admin")
                throw new UnauthorizedAccessException("Only group creator or admin can change roles.");

            var target = await _context.GroupMembers.FirstOrDefaultAsync(gm => gm.GroupId == groupId && gm.UserId == targetUserId);
            if (target == null)
                throw new KeyNotFoundException("Target user is not a member of the group.");

            if (target.Role == "creator")
                throw new ArgumentException("Cannot change role of group creator.");

            if (targetUserId == requesterId)
                throw new ArgumentException("Cannot change your own role.");

            // Admin cannot promote to admin? Actually creator can promote member to admin, admin can demote admin to member? Let's allow creator->admin, admin->member demotion, but admin cannot promote to admin (only creator can)
            if (newRole == "admin" && requesterRole != "creator")
                throw new UnauthorizedAccessException("Only group creator can promote to admin.");

            target.Role = newRole;
            await _context.SaveChangesAsync();
        }

        public async Task RemoveMemberAsync(int groupId, int targetUserId, int requesterId)
        {
            var requesterRole = await GetUserGroupRoleAsync(groupId, requesterId);
            if (requesterRole != "creator" && requesterRole != "admin")
                throw new UnauthorizedAccessException("Only creator or admin can remove members.");

            var target = await _context.GroupMembers.FirstOrDefaultAsync(gm => gm.GroupId == groupId && gm.UserId == targetUserId);
            if (target == null)
                throw new KeyNotFoundException("Target user not found in group.");

            if (target.Role == "creator")
                throw new ArgumentException("Cannot remove group creator. Delete the group instead.");

            if (target.Role == "admin" && requesterRole != "creator")
                throw new UnauthorizedAccessException("Only creator can remove an admin.");

            _context.GroupMembers.Remove(target);
            await _context.SaveChangesAsync();
        }

        public async Task DeleteGroupAsync(int groupId, int requesterId)
        {
            var isCreator = await IsUserCreatorAsync(groupId, requesterId);
            if (!isCreator)
                throw new UnauthorizedAccessException("Only group creator can delete the group.");

            var group = await _context.Groups.FirstOrDefaultAsync(g => g.Id == groupId);
            if (group == null)
                throw new KeyNotFoundException("Group not found.");

            _context.Groups.Remove(group);
            await _context.SaveChangesAsync();
        }

        public async Task<GroupDetailDTO> GetGroupDetails(int groupId)
        {
            var group = await _context.Groups
                .FirstOrDefaultAsync(g => g.Id == groupId);

            if (group == null)
                throw new KeyNotFoundException("Group not found");

            return new GroupDetailDTO()
            {
                Name = group.Name,
                Description = group.Description,
            };
        }
    }
}
