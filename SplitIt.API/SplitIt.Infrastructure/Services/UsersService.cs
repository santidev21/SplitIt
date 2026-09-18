
using Microsoft.AspNetCore.Identity;
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
        private readonly IPasswordHasher<User> _passwordHasher;

        public UsersService(AppDbContext context, IPasswordHasher<User>? passwordHasher = null)
        {
            _context = context;
            _passwordHasher = passwordHasher ?? new PasswordHasher<User>();
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

        /// <summary>
        /// Returns a portable copy of the user's personal data (Ley 1581 access/portability right).
        /// </summary>
        public async Task<UserDataExportDto> ExportUserDataAsync(int userId)
        {
            var user = await _context.Users.FirstOrDefaultAsync(u => u.Id == userId)
                ?? throw new KeyNotFoundException("User not found.");

            var groups = await _context.GroupMembers
                .Where(gm => gm.UserId == userId)
                .Select(gm => new ExportGroupDto { Id = gm.GroupId, Name = gm.Group.Name, Role = gm.Role })
                .ToListAsync();

            var expenses = await _context.Expense
                .Where(e => e.CreatedById == userId || e.PaidById == userId || e.Shares.Any(s => s.UserId == userId))
                .OrderByDescending(e => e.Date)
                .Select(e => new ExportExpenseDto
                {
                    Id = e.Id,
                    GroupId = e.GroupId,
                    Title = e.Title,
                    Amount = e.Amount,
                    Date = e.Date,
                    IsPayment = e.IsPayment,
                    MyOutstanding = e.Shares.Where(s => s.UserId == userId).Select(s => (decimal?)(s.AmountOwed - s.AmountPaid)).FirstOrDefault() ?? 0,
                    MyShareSettled = e.Shares.Where(s => s.UserId == userId).Select(s => (bool?)s.IsSettled).FirstOrDefault()
                })
                .ToListAsync();

            var friends = await _context.Friendships
                .Where(f => f.Status == "accepted" && (f.RequesterId == userId || f.AddresseeId == userId))
                .Select(f => new ExportFriendDto
                {
                    UserId = f.RequesterId == userId ? f.AddresseeId : f.RequesterId,
                    Name = f.RequesterId == userId ? f.Addressee.Name : f.Requester.Name,
                    Email = f.RequesterId == userId ? f.Addressee.Email : f.Requester.Email
                })
                .ToListAsync();

            return new UserDataExportDto
            {
                Id = user.Id,
                Name = user.Name,
                Email = user.Email,
                CreatedAt = user.CreatedAt,
                ConsentAt = user.ConsentAt,
                ConsentVersion = user.ConsentVersion,
                Groups = groups,
                Expenses = expenses,
                Friends = friends
            };
        }

        /// <summary>
        /// Suppression right (Ley 1581): revokes sessions and anonymizes the account while
        /// preserving financial records (kept under a legal/accounting basis).
        /// </summary>
        public async Task DeleteAccountAsync(int userId, string password)
        {
            var user = await _context.Users.FirstOrDefaultAsync(u => u.Id == userId)
                ?? throw new KeyNotFoundException("User not found.");
            if (!user.IsActive)
                throw new ArgumentException("Account is already deleted.");

            var verification = _passwordHasher.VerifyHashedPassword(user, user.PasswordHash, password);
            if (verification == PasswordVerificationResult.Failed)
                throw new UnauthorizedAccessException("Incorrect password.");

            var activeTokens = await _context.RefreshTokens
                .Where(r => r.UserId == userId && r.RevokedAt == null)
                .ToListAsync();
            foreach (var token in activeTokens)
                token.RevokedAt = DateTime.UtcNow;

            // Anonymize PII; keep the row so group balances and audit references stay intact.
            user.Name = "Deleted user";
            user.Email = $"deleted+{user.Id}@deleted.splitit.local";
            user.PasswordHash = string.Empty;
            user.IsActive = false;
            user.ConsentIp = null;
            user.DeletedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();
        }
    }
}
