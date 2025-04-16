using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SplitIt.Application.DTOs;
using SplitIt.Infrastructure.Services;
using System.Security.Claims;

namespace SplitIt.API.Controllers
{
    [Route("api/expenses")]
    [ApiController]
    public class ExpensesController : ControllerBase
    {
        private readonly ExpensesService _expensesService;

        private readonly IConfiguration _configuration;

        public ExpensesController(ExpensesService expensesService, IConfiguration configuration)
        {
            _expensesService = expensesService;
            _configuration = configuration;
        }

        [HttpPost("add")]
        [Authorize]
        public async Task<IActionResult> AddExpense([FromBody] CreateExpenseDto request)
        {
            if (request.GroupId <= 0 || string.IsNullOrEmpty(request.Title) || request.Amount <= 0 || request.Participants.Count == 0)
            {
                return BadRequest("Invalid data.");
            }

            // Get the ID of the currently logged-in user
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userIdClaim))
                return Unauthorized();

            int createdById = int.Parse(userIdClaim);

            var expense = await _expensesService.AddExpenseAsync(request, createdById);
            return CreatedAtAction(nameof(AddExpense), new { id = expense.Id }, expense);
        }
    }
}
