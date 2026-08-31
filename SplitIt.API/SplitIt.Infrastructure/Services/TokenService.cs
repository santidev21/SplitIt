using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using SplitIt.Domain.Entities;
using SplitIt.Infrastructure.Persistence;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;

namespace SplitIt.Infrastructure.Services
{
    public class TokenService
    {
        private readonly AppDbContext _context;
        private readonly IConfiguration _configuration;

        public TokenService(AppDbContext context, IConfiguration configuration)
        {
            _context = context;
            _configuration = configuration;
        }

        public string GenerateAccessToken(User user)
        {
            var jwtSettings = _configuration.GetSection("JwtSettings");
            var secret = jwtSettings["SecretKey"] ?? throw new InvalidOperationException("JwtSettings:SecretKey missing");
            var key = Encoding.UTF8.GetBytes(secret);
            var expirationStr = jwtSettings["ExpirationInMinutes"];
            var expiration = int.TryParse(expirationStr, out var exp) ? exp : 15;

            var claims = new List<Claim>
            {
                new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
                new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
                new Claim(JwtRegisteredClaimNames.Email, user.Email),
                new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
                new Claim(ClaimTypes.Role, RoleConstants.GetName(user.RoleId))
            };

            var token = new JwtSecurityToken(
                issuer: jwtSettings["Issuer"],
                audience: jwtSettings["Audience"],
                claims: claims,
                expires: DateTime.UtcNow.AddMinutes(expiration),
                signingCredentials: new SigningCredentials(new SymmetricSecurityKey(key), SecurityAlgorithms.HmacSha256)
            );

            return new JwtSecurityTokenHandler().WriteToken(token);
        }

        public static string GenerateRandomToken()
        {
            var bytes = RandomNumberGenerator.GetBytes(64);
            return Convert.ToBase64String(bytes).Replace("+", "-").Replace("/", "_").TrimEnd('=');
        }

        public static string HashToken(string token)
        {
            var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(token));
            return Convert.ToBase64String(bytes);
        }

        public async Task<RefreshToken> IssueRefreshTokenAsync(User user)
        {
            var expirationDays = _configuration.GetValue<int>("JwtSettings:RefreshTokenExpirationInDays");
            if (expirationDays <= 0) expirationDays = 30;

            var rawToken = GenerateRandomToken();
            var refreshToken = new RefreshToken
            {
                UserId = user.Id,
                TokenHash = HashToken(rawToken),
                ExpiresAt = DateTime.UtcNow.AddDays(expirationDays),
            };

            _context.RefreshTokens.Add(refreshToken);
            await _context.SaveChangesAsync();

            refreshToken.TokenHash = rawToken;
            return refreshToken;
        }

        public async Task<(User user, RefreshToken newRefreshToken)?> RotateRefreshTokenAsync(string rawToken)
        {
            var hash = HashToken(rawToken);
            var existing = await _context.RefreshTokens
                .FirstOrDefaultAsync(r => r.TokenHash == hash);

            if (existing == null)
                return null;

            if (existing.RevokedAt != null)
            {
                await RevokeAllUserRefreshTokensAsync(existing.UserId);
                return null;
            }

            if (existing.ExpiresAt < DateTime.UtcNow)
                return null;

            var user = await _context.Users.FindAsync(existing.UserId);
            if (user == null || !user.IsActive)
                return null;

            var newRawToken = GenerateRandomToken();
            var newHash = HashToken(newRawToken);

            existing.RevokedAt = DateTime.UtcNow;
            existing.ReplacedByTokenHash = newHash;

            var expirationDays = _configuration.GetValue<int>("JwtSettings:RefreshTokenExpirationInDays");
            if (expirationDays <= 0) expirationDays = 30;

            var newRefreshToken = new RefreshToken
            {
                UserId = user.Id,
                TokenHash = newHash,
                ExpiresAt = DateTime.UtcNow.AddDays(expirationDays),
            };

            _context.RefreshTokens.Add(newRefreshToken);

            var expiredTokens = await _context.RefreshTokens
                .Where(r => r.UserId == user.Id && r.ExpiresAt < DateTime.UtcNow.AddDays(-1))
                .ToListAsync();
            _context.RefreshTokens.RemoveRange(expiredTokens);

            await _context.SaveChangesAsync();

            newRefreshToken.TokenHash = newRawToken;
            return (user, newRefreshToken);
        }

        public async Task RevokeRefreshTokenAsync(string rawToken)
        {
            var hash = HashToken(rawToken);
            var token = await _context.RefreshTokens
                .FirstOrDefaultAsync(r => r.TokenHash == hash && r.RevokedAt == null);

            if (token != null)
            {
                token.RevokedAt = DateTime.UtcNow;
                await _context.SaveChangesAsync();
            }
        }

        private async Task RevokeAllUserRefreshTokensAsync(int userId)
        {
            var tokens = await _context.RefreshTokens
                .Where(r => r.UserId == userId && r.RevokedAt == null)
                .ToListAsync();

            foreach (var t in tokens)
                t.RevokedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();
        }
    }
}
