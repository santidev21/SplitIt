using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using SplitIt.Domain.Entities;
using SplitIt.Infrastructure.Persistence;
using System.Security.Cryptography;

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

        public async Task<bool> RegisterUser(string name, string email, string password,
            bool acceptTerms = false, string? consentVersion = null, string? consentIp = null)
        {
            var normalizedEmail = email.Trim().ToLowerInvariant();
            if (await _context.Users.AnyAsync(u => u.Email.ToLower() == normalizedEmail))
                return false;

            var user = new User { Name = name.Trim(), Email = normalizedEmail, RoleId = RoleConstants.User };
            user.PasswordHash = _passwordHasher.HashPassword(user, password);

            if (acceptTerms)
            {
                // Proof of the authorization (Ley 1581).
                user.ConsentAt = DateTime.UtcNow;
                user.ConsentVersion = consentVersion;
                user.ConsentIp = consentIp;
            }

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

            var code = RandomNumberGenerator.GetInt32(100000, 1000000).ToString();
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

        public async Task<bool> ResetPasswordAsync(string token, string newPassword, string? email = null)
        {
            var resetToken = await _context.PasswordResetTokens
                .Include(t => t.User)
                .FirstOrDefaultAsync(t => t.Token == token && !t.Used);

            if (resetToken == null) return false;
            if (resetToken.ExpiresAt < DateTime.UtcNow) return false;

            // Bind the code to the account: the requester must also know the email.
            if (!string.IsNullOrWhiteSpace(email))
            {
                var normalized = email.Trim().ToLowerInvariant();
                if (resetToken.User == null ||
                    !string.Equals(resetToken.User.Email.ToLowerInvariant(), normalized, StringComparison.Ordinal))
                    return false;
            }

            var user = resetToken.User;
            user.PasswordHash = _passwordHasher.HashPassword(user, newPassword);
            resetToken.Used = true;

            // A password change invalidates all active sessions.
            var activeTokens = await _context.RefreshTokens
                .Where(r => r.UserId == user.Id && r.RevokedAt == null)
                .ToListAsync();
            foreach (var activeToken in activeTokens)
                activeToken.RevokedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();
            return true;
        }
    }
}
