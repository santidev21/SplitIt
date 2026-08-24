using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SplitIt.Application.DTOs;
using SplitIt.Infrastructure.Services;
using System.Security.Claims;

namespace SplitIt.API.Controllers
{
    [Route("api/admin")]
    [ApiController]
    [Authorize]
    public class AdminController : ControllerBase
    {
        private readonly UsersService _usersService;

        public AdminController(UsersService usersService)
        {
            _usersService = usersService;
        }

        private bool IsAdmin()
        {
            var role = User.FindFirst(ClaimTypes.Role)?.Value;
            return role == "1" || role == "2";
        }

        private bool IsSuperAdmin()
        {
            var role = User.FindFirst(ClaimTypes.Role)?.Value;
            return role == "1";
        }

        [HttpGet("users")]
        public async Task<IActionResult> GetAllUsers()
        {
            if (!IsAdmin()) return Forbid();
            var users = await _usersService.GetAllUsersAsync();
            return Ok(users);
        }

        [HttpPut("users/{userId}/role")]
        public async Task<IActionResult> UpdateUserRole(int userId, [FromBody] UpdateUserRoleDto dto)
        {
            if (!IsSuperAdmin()) return Forbid();
            var claim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(claim) || !int.TryParse(claim, out var requesterId)) return Unauthorized();
            try
            {
                await _usersService.UpdateUserRoleAsync(userId, dto.RoleId, requesterId);
                return Ok(new { message = "Role updated." });
            }
            catch (UnauthorizedAccessException ex) { return Forbid(ex.Message); }
            catch (ArgumentException ex) { return BadRequest(new { message = ex.Message }); }
            catch (KeyNotFoundException ex) { return NotFound(new { message = ex.Message }); }
        }
    }

    public class UpdateUserRoleDto
    {
        public int RoleId { get; set; }
    }
}
