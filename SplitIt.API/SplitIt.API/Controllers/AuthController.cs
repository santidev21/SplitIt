using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.IdentityModel.Tokens;
using SplitIt.Application.DTOs;
using SplitIt.Domain.Entities;
using SplitIt.Infrastructure.Services;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

namespace SplitIt.API.Controllers
{
    [Route("api/auth")]
    [ApiController]
    public class AuthController : ControllerBase
    {
        private readonly AuthService _authService;
        private readonly IConfiguration _configuration;
        private readonly SettingsService? _settingsService;

        public AuthController(AuthService authService, IConfiguration configuration, SettingsService? settingsService = null)
        {
            _authService = authService;
            _configuration = configuration;
            _settingsService = settingsService;
        }

        [HttpPost("register")]
        [EnableRateLimiting("auth")]
        public async Task<IActionResult> Register([FromBody] RegisterRequestDto request)
        {
            // Global toggle managed from the admin panel
            if (_settingsService != null && !await _settingsService.GetValueAsync(SettingsService.RegistrationEnabled, true))
                return BadRequest(new { message = "Registration is currently disabled. Contact an administrator." });

            // Generic response to avoid enumeration timing is handled via constant-time-ish flow
            bool success = await _authService.RegisterUser(request.Name, request.Email, request.Password);
            if (!success)
                return Conflict(new { message = "Unable to register. If the email is already registered, try logging in." });

            var user = await _authService.GetUserByEmail(request.Email);
            if (user == null)
                return StatusCode(500, new { message = "An error occurred while retrieving the user." });

            var token = GenerateJwtToken(user);

            return Ok(new
            {
                message = "Registration successful.",
                token,
                userName = user.Name,
                userId = user.Id
            });
        }

        [HttpPost("login")]
        [EnableRateLimiting("auth")]
        public async Task<IActionResult> Login([FromBody] LoginRequestDto request)
        {
            var user = await _authService.GetUserByEmail(request.Email);
            if (user == null || !await _authService.ValidateUser(request.Email, request.Password))
                return Unauthorized(new { message = "Incorrect email or password." });

            // Re-fetch to get rehashed password if legacy migrated
            user = await _authService.GetUserByEmail(request.Email);
            var token = GenerateJwtToken(user!);
            return Ok(new {message = "Login successful.", token, userName = user!.Name, userId = user.Id});
        }

        [HttpPost("forgot-password")]
        [EnableRateLimiting("auth")]
        public async Task<IActionResult> ForgotPassword([FromBody] ForgotPasswordDto request)
        {
            var token = await _authService.GenerateResetTokenAsync(request.Email);
            // Always return success to prevent email enumeration
            return Ok(new { message = "If the email exists, a reset token has been generated. Contact your administrator." });
        }

        [HttpPost("reset-password")]
        [EnableRateLimiting("auth")]
        public async Task<IActionResult> ResetPassword([FromBody] ResetPasswordDto request)
        {
            if (string.IsNullOrWhiteSpace(request.Token) || string.IsNullOrWhiteSpace(request.NewPassword))
                return BadRequest(new { message = "Token and new password are required." });

            if (request.NewPassword.Length < 8)
                return BadRequest(new { message = "Password must be at least 8 characters." });

            var success = await _authService.ResetPasswordAsync(request.Token, request.NewPassword);
            if (!success)
                return BadRequest(new { message = "Invalid or expired token." });

            return Ok(new { message = "Password has been reset successfully. You can now log in." });
        }

        private string GenerateJwtToken(User user)
        {
            var jwtSettings = _configuration.GetSection("JwtSettings");
            var secret = jwtSettings["SecretKey"] ?? throw new InvalidOperationException("JwtSettings:SecretKey missing");
            var key = Encoding.UTF8.GetBytes(secret);
            var expirationStr = jwtSettings["ExpirationInMinutes"];
            var expiration = int.TryParse(expirationStr, out var exp) ? exp : 60;

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
    }
}
