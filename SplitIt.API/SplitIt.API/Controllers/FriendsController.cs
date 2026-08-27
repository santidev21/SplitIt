using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SplitIt.Application.DTOs;
using SplitIt.Infrastructure.Services;
using System.Security.Claims;

namespace SplitIt.API.Controllers
{
    [Route("api/friends")]
    [ApiController]
    [Authorize]
    public class FriendsController : ControllerBase
    {
        private readonly FriendshipsService _friendshipsService;

        public FriendsController(FriendshipsService friendshipsService)
        {
            _friendshipsService = friendshipsService;
        }

        private int CurrentUserId()
        {
            var claim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            return int.TryParse(claim, out var id) ? id : 0;
        }

        [HttpGet]
        public async Task<IActionResult> GetFriends()
        {
            var friends = await _friendshipsService.GetFriendsAsync(CurrentUserId());
            return Ok(friends);
        }

        [HttpGet("requests")]
        public async Task<IActionResult> GetRequests()
        {
            var userId = CurrentUserId();
            var incoming = await _friendshipsService.GetIncomingRequestsAsync(userId);
            var sent = await _friendshipsService.GetSentRequestsAsync(userId);
            return Ok(new { incoming, sent });
        }

        [HttpPost("request")]
        public async Task<IActionResult> SendRequest([FromBody] SendFriendRequestDto dto)
        {
            try
            {
                await _friendshipsService.SendRequestAsync(CurrentUserId(), dto.UserId, dto.Email);
                return Ok(new { message = "Friend request sent." });
            }
            catch (ArgumentException ex) { return BadRequest(new { message = ex.Message }); }
            catch (KeyNotFoundException ex) { return NotFound(new { message = ex.Message }); }
        }

        [HttpPost("{friendshipId}/respond")]
        public async Task<IActionResult> Respond(int friendshipId, [FromBody] RespondFriendRequestDto dto)
        {
            try
            {
                await _friendshipsService.RespondAsync(friendshipId, CurrentUserId(), dto.Accept);
                return Ok(new { message = dto.Accept ? "Friend request accepted." : "Friend request rejected." });
            }
            catch (ArgumentException ex) { return BadRequest(new { message = ex.Message }); }
            catch (KeyNotFoundException ex) { return NotFound(new { message = ex.Message }); }
            catch (UnauthorizedAccessException ex) { return Forbid(ex.Message); }
        }

        [HttpDelete("{friendUserId}")]
        public async Task<IActionResult> RemoveFriend(int friendUserId)
        {
            try
            {
                await _friendshipsService.RemoveFriendAsync(CurrentUserId(), friendUserId);
                return Ok(new { message = "Friend removed." });
            }
            catch (KeyNotFoundException ex) { return NotFound(new { message = ex.Message }); }
        }

        [HttpGet("search")]
        public async Task<IActionResult> Search([FromQuery] string q)
        {
            try
            {
                var results = await _friendshipsService.SearchUsersAsync(q, CurrentUserId());
                return Ok(results);
            }
            catch (ArgumentException ex) { return BadRequest(new { message = ex.Message }); }
        }
    }
}
