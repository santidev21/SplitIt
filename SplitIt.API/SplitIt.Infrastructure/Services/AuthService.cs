using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using SplitIt.Domain.Entities;
using SplitIt.Infrastructure.Persistence;

namespace SplitIt.Infrastructure.Services
{
    public class AuthService
    {
        private readonly AppDbContext _context;
        private readonly IPasswordHasher<User> _passwordHasher;

        public AuthService(AppDbContext context, IPasswordHasher<User> passwordHasher)
        {
            _context = context;
            _passwordHasher = passwordHasher;
        }

        // For DI without IPasswordHasher in tests: fallback
        public AuthService(AppDbContext context) : this(context, new PasswordHasher<User>()) { }

        public async Task<bool> RegisterUser(string name, string email, string password)
        {
            var normalizedEmail = email.Trim().ToLowerInvariant();
            if (await _context.Users.AnyAsync(u => u.Email.ToLower() == normalizedEmail))
                return false;

            var user = new User { Name = name.Trim(), Email = normalizedEmail, RoleId = RoleConstants.User };
            user.PasswordHash = _passwordHasher.HashPassword(user, password);

            _context.Add(user);
            await _context.SaveChangesAsync();
            return true;
        }

        public async Task<bool> ValidateUser(string email, string password)
        {
            var user = await GetUserByEmail(email);
            if (user == null) return false;
            if (!user.IsActive) return false;

            var result = _passwordHasher.VerifyHashedPassword(user, user.PasswordHash, password);
            if (result == PasswordVerificationResult.Success || result == PasswordVerificationResult.SuccessRehashNeeded)
            {
                if (result == PasswordVerificationResult.SuccessRehashNeeded)
                {
                    user.PasswordHash = _passwordHasher.HashPassword(user, password);
                    await _context.SaveChangesAsync();
                }
                return true;
            }

            return false;
        }

        public async Task<User?> GetUserByEmail(string email)
        {
            var normalized = email.Trim().ToLowerInvariant();
            return await _context.Users.FirstOrDefaultAsync(u => u.Email.ToLower() == normalized);
        }

        public async Task<string?> GenerateResetTokenAsync(string email)
        {
            var user = await GetUserByEmail(email);
            if (user == null || !user.IsActive) return null;

            var existingTokens = await _context.PasswordResetTokens
                .Where(t => t.UserId == user.Id && !t.Used)
                .ToListAsync();
            _context.PasswordResetTokens.RemoveRange(existingTokens);

            var code = Random.Shared.Next(100000, 999999).ToString();
            var resetToken = new PasswordResetToken
            {
                UserId = user.Id,
                Token = code,
                ExpiresAt = DateTime.UtcNow.AddMinutes(15),
                Used = false
            };

            _context.PasswordResetTokens.Add(resetToken);
            await _context.SaveChangesAsync();

            return code;
        }

        public async Task<bool> ResetPasswordAsync(string token, string newPassword)
        {
            var resetToken = await _context.PasswordResetTokens
                .Include(t => t.User)
                .FirstOrDefaultAsync(t => t.Token == token && !t.Used);

            if (resetToken == null) return false;
            if (resetToken.ExpiresAt < DateTime.UtcNow) return false;

            var user = resetToken.User;
            user.PasswordHash = _passwordHasher.HashPassword(user, newPassword);
            resetToken.Used = true;

            await _context.SaveChangesAsync();
            return true;
        }
    }
}
