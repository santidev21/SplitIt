using Microsoft.EntityFrameworkCore;
using SplitIt.Domain.Entities;
using SplitIt.Infrastructure.Persistence;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace SplitIt.Infrastructure.Services
{
    public class AuthService
    {
        private readonly AppDbContext _context;

        public AuthService(AppDbContext context)
        {
            _context = context;
        }

        public async Task<bool> RegisterUser(string name, string email, string password)
        {
            if (await _context.Users.AnyAsync(u => u.Email == email))
                return false; // Check if user exist

            string passwordHash = HashPassword(password);

            User user = new User() { Name = name, Email = email, PasswordHash = passwordHash , RoleId = 3};

            _context.Add(user);
            await _context.SaveChangesAsync();
            return true;
        }

        public async Task<bool> ValidateUser(string email, string password)
        {
            var user = await _context.Users.FirstOrDefaultAsync(u => u.Email == email);
            if (user == null) return false;

            return VerifyPassword(password, user.PasswordHash);
        }

        public async Task<User?> GetUserByEmail(string email)
        {
            return await _context.Users.FirstOrDefaultAsync(u => u.Email == email);
        }

        private string HashPassword(string password)
        {
            using var sha256 = SHA256.Create();
            byte[] bytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(password));
            return Convert.ToBase64String(bytes);
        }

        private bool VerifyPassword(string password, string storedHash)
        {
            string hashInput = HashPassword(password);
            return hashInput == storedHash;
        }
    }
}
