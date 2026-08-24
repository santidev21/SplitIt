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
        private readonly GroupService _groupService;

        private readonly IConfiguration _configuration;

        public ExpensesController(ExpensesService expensesService, GroupService groupService, IConfiguration configuration)
        {
            _expensesService = expensesService;
            _groupService = groupService;
            _configuration = configuration;
        }

        [HttpPost("add")]
        [Authorize]
        public async Task<IActionResult> AddExpense([FromBody] CreateExpenseDto request)
        {
            if (!ModelState.IsValid)
                return ValidationProblem(ModelState);

            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userIdClaim) || !int.TryParse(userIdClaim, out var createdById))
                return Unauthorized();

            try
            {
                var expense = await _expensesService.AddExpenseAsync(request, createdById);
                return CreatedAtAction(nameof(AddExpense), new { id = expense.Id }, expense);
            }
            catch (UnauthorizedAccessException ex)
            {
                return Forbid(ex.Message);
            }
            catch (ArgumentException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(new { message = ex.Message });
            }
        }

        [HttpGet("{groupId}/expenses")]
        [Authorize]
        public async Task<IActionResult> GetGroupExpenses(int groupId, [FromQuery] bool showAll = false)
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userIdClaim) || !int.TryParse(userIdClaim, out var userId))
                return Unauthorized();

            if (!await _groupService.IsUserMemberAsync(groupId, userId))
                return Forbid();

            var expenses = await _expensesService.GetExpensesByGroupIdAsync(groupId, userId, showAll);
            return Ok(expenses);
        }

        [HttpGet("debt-summary")]
        [Authorize]
        public async Task<IActionResult> GetFullDebtSummary([FromQuery] int groupId)
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userIdClaim) || !int.TryParse(userIdClaim, out var userId))
                return Unauthorized();

            if (!await _groupService.IsUserMemberAsync(groupId, userId))
                return Forbid();

            var summary = await _expensesService.GetFullDebtSummaryAsync(userId, groupId);

            return Ok(summary);
        }

        [HttpPost("settle")]
        [Authorize]
        public async Task<IActionResult> SettleExpenseWithUser([FromBody] RegisterPaymentDto dto)
        {
            if (!ModelState.IsValid)
                return ValidationProblem(ModelState);

            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userIdClaim) || !int.TryParse(userIdClaim, out var receiverUserId))
                return Unauthorized();

            try
            {
                var expenseDetailsId = await _expensesService.RegisterPayment(dto.PayerUserId, receiverUserId, dto.GroupId, dto.Amount);
                int settledCount = await _expensesService.SettleExpenseWithUser(dto.PayerUserId, receiverUserId, dto.GroupId);

                if (settledCount == 0)
                    return NotFound(new { message = "No unsettled debts found." });

                return Ok(new { SettledCount = settledCount });
            }
            catch (UnauthorizedAccessException ex)
            {
                return Forbid(ex.Message);
            }
            catch (ArgumentException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(new { message = ex.Message });
            }
        }
    }
}
