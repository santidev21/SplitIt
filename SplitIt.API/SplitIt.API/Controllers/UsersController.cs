using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SplitIt.Application.DTOs;
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

        private int CurrentUserId()
        {
            var claim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            return int.TryParse(claim, out var id) ? id : 0;
        }

        /// <summary>Access/portability right: download a copy of the user's data (Ley 1581).</summary>
        [HttpGet("me/export")]
        [Authorize]
        public async Task<IActionResult> ExportMyData()
        {
            var userId = CurrentUserId();
            if (userId == 0) return Unauthorized();
            var data = await _usersService.ExportUserDataAsync(userId);
            return Ok(data);
        }

        /// <summary>Suppression right: anonymize and deactivate the account (Ley 1581).</summary>
        [HttpDelete("me")]
        [Authorize]
        public async Task<IActionResult> DeleteMyAccount([FromBody] DeleteAccountDto dto)
        {
            if (!ModelState.IsValid) return ValidationProblem(ModelState);

            var userId = CurrentUserId();
            if (userId == 0) return Unauthorized();

            try
            {
                await _usersService.DeleteAccountAsync(userId, dto.Password);
                return Ok(new { message = "Account deleted." });
            }
            catch (UnauthorizedAccessException ex) { return Unauthorized(new { message = ex.Message }); }
            catch (ArgumentException ex) { return BadRequest(new { message = ex.Message }); }
            catch (KeyNotFoundException ex) { return NotFound(new { message = ex.Message }); }
        }
    }
}
