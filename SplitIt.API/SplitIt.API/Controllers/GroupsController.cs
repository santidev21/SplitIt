﻿using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SplitIt.Application.DTOs;
using SplitIt.Domain.Entities;
using SplitIt.Infrastructure.Services;
using System.Security.Claims;

namespace SplitIt.API.Controllers
{
    [Route("api/groups")]
    [ApiController]
    public class GroupsController : ControllerBase
    {
        private readonly AuthService _authService;
        private readonly GroupService _groupService;
        private readonly IConfiguration _configuration;


        public GroupsController(AuthService authService, GroupService groupService, IConfiguration configuration)
        {
            _authService = authService;
            _groupService = groupService;
            _configuration = configuration;
        }

        
        [HttpPost("create")]
        [Authorize]
        public async Task<IActionResult> CreateGroup([FromBody] CreateGroupDto request)
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userIdClaim))
                return Unauthorized();

            int userId = int.Parse(userIdClaim);

            string name = request.Name;
            string description = request.Description;
            List<int> members = new List<int> { userId }.Concat(request.Members).ToList();
            bool allowToDeleteExpenses = request.AllowToDeleteExpenses;
            int currencyId = request.CurrencyId;

            var groupId = await _groupService.CreateGroup(name, description, allowToDeleteExpenses, currencyId, userId);

            bool result = await _groupService.AddGroupMembers(groupId, members, userId);
            return Ok(new { Message = "Group created correctly.", GroupId = groupId });
        }

        [HttpGet("user/{userId}")]
        [Authorize]
        public async Task<IActionResult> GetGroupsForUser(int userId)
        {
            var loggedUserId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            var loggedUserRole = User.FindFirst(ClaimTypes.Role)?.Value;

            // If it's not the same user and not a "super" role, access is not allowed
            if (loggedUserId != userId.ToString() && loggedUserRole != "1")
            {
                return Forbid();
            }

            var groups = await _groupService.GetGroupsForUserAsync(userId);
            return Ok(groups);
        }

        [HttpGet("{groupId}/members")]
        [Authorize]
        public async Task<IActionResult> GetGroupMembers(int groupId)
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userIdClaim))
                return Unauthorized();

            int userId = int.Parse(userIdClaim);

            var members = await _groupService.GetGroupMembersAsync(groupId, userId);
            return Ok(members);
        }

        [HttpGet("{groupId}/details")]
        [Authorize]
        public async Task<IActionResult> getGroupDetails(int groupId)
        {
            // Get the ID of the currently logged-in user
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userIdClaim))
                return Unauthorized();


            var groupDetails = await _groupService.GetGroupDetails(groupId);
            return Ok(groupDetails);
        }

        [HttpGet("{groupId}/userrole")]
        [Authorize]
        public async Task<IActionResult> GetUserGroupRoleAsync(int groupId)
        {
            // Get the ID of the currently logged-in user
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userIdClaim))
                return Unauthorized();

            int userId = int.Parse(userIdClaim);
            var userRole = await _groupService.GetUserGroupRoleAsync(groupId, userId);

            if (userRole == null)
                return NotFound("User does not belong to this group.");

            return Ok(new { role = userRole });
        }
        // TODO: OpenAPI documentation.
    }
}
