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
    }
}
