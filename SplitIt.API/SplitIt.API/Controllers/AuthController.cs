using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;
using SplitIt.API.DTOs;
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
        public async Task<IActionResult> Register([FromBody] RegisterRequestDto request)
        {
            bool success = await _authService.RegisterUser(request.Name, request.Email, request.Password);
            if (!success)
                return BadRequest(new { message = "The user already exists!" });

            var token = GenerateJwtToken(request.Email);

            return Ok(new
            {
                message = "Registration successful.",
                token,
                user = new { request.Name, request.Email }
            });
        }

        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] LoginRequestDto request)
        {
            var user = await _authService.GetUserByEmail(request.Email);
            if (user == null || !await _authService.ValidateUser(request.Email, request.Password))
                return Unauthorized(new { error = "Invalid credentials" });

            var token = GenerateJwtToken(request.Email);
            return Ok(new {message = "Login successful.", token, userName = user.Name});
        }

        private string GenerateJwtToken(string email)
        {
            var jwtSettings = _configuration.GetSection("JwtSettings");
            var key = Encoding.UTF8.GetBytes(jwtSettings["SecretKey"]);
            var expiration = int.Parse(jwtSettings["ExpirationInMinutes"]);

            var claims = new List<Claim>
            {
                new Claim(JwtRegisteredClaimNames.Sub, email),
                new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
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
