using Microsoft.AspNetCore.Mvc;
using SplitIt.API.DTOs;
using SplitIt.Infrastructure.Services;

namespace SplitIt.API.Controllers
{
    [Route("api/auth")]
    [ApiController]
    public class AuthController : ControllerBase
    {
        private readonly AuthService _authService;

        public AuthController(AuthService authService)
        {
            _authService = authService;
        }

        [HttpPost("register")]
        public async Task<IActionResult> Register([FromBody] RegisterRequestDto request)
        {
            bool success = await _authService.RegisterUser(request.Name, request.Email, request.Password);
            if (!success)
                return BadRequest("The user already exists!");

            return Ok("Successfully registered.");
        }

        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] LoginRequestDto request)
        {
            bool success = await _authService.ValidateUser(request.Email, request.Password);
            if (!success)
                return BadRequest("Incorrect credentials.");

            return Ok("Login successful.");
        }
    }
}
