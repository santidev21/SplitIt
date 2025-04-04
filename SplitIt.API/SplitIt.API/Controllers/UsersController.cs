using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SplitIt.Infrastructure.Services;
using System.Security.Claims;

namespace SplitIt.API.Controllers
{
    [Route("api/users")]
    [ApiController]
    public class UsersController : ControllerBase
    {
        private readonly UsersService _usersService;
        private readonly IConfiguration _configuration;


        public UsersController(UsersService usersService, IConfiguration configuration)
        {
            _usersService = usersService;
            _configuration = configuration;
        }

            [HttpGet]
            [Authorize]
            public async Task<IActionResult> GetUsers()
            {
                var currentUserId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

                if (string.IsNullOrEmpty(currentUserId))
                    return Unauthorized(new { message = "Invalid user session." });

                var users = await _usersService.GetUsersAsync(currentUserId);

                return Ok(users);
            }
    }
}
