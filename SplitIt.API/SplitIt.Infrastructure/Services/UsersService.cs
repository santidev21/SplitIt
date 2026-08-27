
using Microsoft.EntityFrameworkCore;
using SplitIt.Domain.Entities;
using SplitIt.Infrastructure.Persistence;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using SplitIt.Application.DTOs;

namespace SplitIt.Infrastructure.Services
{
    public class UsersService
    {
        private readonly AppDbContext _context;

        public UsersService(AppDbContext context)
        {
            _context = context;
        }

        public async Task<List<UserDto>> GetUsersAsync(string currentUserId)
        {
            return await _context.Users.Where(u => u.Id.ToString() != currentUserId)
                .Select(u => new UserDto
                {
                    Id = u.Id,
                    Name = u.Name,
                    Email = u.Email
                })
                .ToListAsync();
        }

        public async Task<List<UserDto>> GetAllUsersAsync()
        {
            return await _context.Users
                .Select(u => new UserDto
                {
                    Id = u.Id,
                    Name = u.Name,
                    Email = u.Email
                })
                .ToListAsync();
        }

        public async Task<bool> IsUserAdminAsync(int userId)
        {
            var user = await _context.Users.FirstOrDefaultAsync(u => u.Id == userId);
            if (user == null) return false;
            return user.RoleId == RoleConstants.SuperAdmin || user.RoleId == RoleConstants.Admin;
        }

        public async Task UpdateUserRoleAsync(int targetUserId, int newRoleId, int requesterId)
        {
            var requester = await _context.Users.FirstOrDefaultAsync(u => u.Id == requesterId);
            if (requester == null || requester.RoleId != RoleConstants.SuperAdmin)
                throw new UnauthorizedAccessException("Only super admin can change roles.");

            if (newRoleId < RoleConstants.SuperAdmin || newRoleId > RoleConstants.User)
                throw new ArgumentException("Invalid role.");

            var target = await _context.Users.FirstOrDefaultAsync(u => u.Id == targetUserId);
            if (target == null) throw new KeyNotFoundException("User not found.");

            if (target.Id == requesterId)
                throw new ArgumentException("Cannot change your own role.");

            target.RoleId = newRoleId;
            await _context.SaveChangesAsync();
        }
    }
}
