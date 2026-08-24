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

        public AuthController(AuthService authService, IConfiguration configuration)
        {
            _authService = authService;
            _configuration = configuration;
        }

        [HttpPost("register")]
        [EnableRateLimiting("auth")]
        public async Task<IActionResult> Register([FromBody] RegisterRequestDto request)
        {
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
                return Unauthorized(new { error = "Invalid credentials" });

            // Re-fetch to get rehashed password if legacy migrated
            user = await _authService.GetUserByEmail(request.Email);
            var token = GenerateJwtToken(user!);
            return Ok(new {message = "Login successful.", token, userName = user!.Name, userId = user.Id});
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
                new Claim(ClaimTypes.Role, user.RoleId.ToString())
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
