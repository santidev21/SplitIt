using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using SplitIt.Domain.Entities;
using SplitIt.Infrastructure.Persistence;
using System.Security.Cryptography;
using System.Text;

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

            var user = new User { Name = name.Trim(), Email = normalizedEmail, RoleId = 3 };
            user.PasswordHash = _passwordHasher.HashPassword(user, password);

            _context.Add(user);
            await _context.SaveChangesAsync();
            return true;
        }

        public async Task<bool> ValidateUser(string email, string password)
        {
            var user = await GetUserByEmail(email);
            if (user == null) return false;

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

            // Legacy SHA256 migration path
            if (IsLegacySha256Hash(user.PasswordHash) && VerifyLegacySha256(password, user.PasswordHash))
            {
                user.PasswordHash = _passwordHasher.HashPassword(user, password);
                await _context.SaveChangesAsync();
                return true;
            }

            return false;
        }

        public async Task<User?> GetUserByEmail(string email)
        {
            var normalized = email.Trim().ToLowerInvariant();
            return await _context.Users.FirstOrDefaultAsync(u => u.Email.ToLower() == normalized);
        }

        // Legacy helpers — kept for migration only, not used for new hashes
        private static bool IsLegacySha256Hash(string hash)
        {
            // Legacy was Base64(SHA256(password)) → 44 chars, no Identity prefix ($)
            return !string.IsNullOrEmpty(hash) && !hash.StartsWith("AQAAAA") && hash.Length == 44;
        }

        private static string HashLegacySha256(string password)
        {
            using var sha256 = SHA256.Create();
            byte[] bytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(password));
            return Convert.ToBase64String(bytes);
        }

        private static bool VerifyLegacySha256(string password, string storedHash)
        {
            return HashLegacySha256(password) == storedHash;
        }
    }
}
