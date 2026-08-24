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
            if (string.IsNullOrEmpty(userIdClaim) || !int.TryParse(userIdClaim, out var currentUserId))
                return Unauthorized();

            try
            {
                // Determine payer/receiver correctly for partial payments (handle both directions)
                var payer = dto.PayerUserId;
                var receiver = currentUserId;
                var remaining = await _expensesService.GetRemainingDebtAsync(payer, receiver, dto.GroupId);
                if (remaining <= 0.009m)
                {
                    // Try swapped direction (current user may be payer)
                    var swapped = await _expensesService.GetRemainingDebtAsync(receiver, payer, dto.GroupId);
                    if (swapped > 0.009m)
                    {
                        payer = currentUserId;
                        receiver = dto.PayerUserId;
                        remaining = swapped;
                    }
                }

                // Validate amount not empty and use absolute
                var amount = Math.Abs(dto.Amount);
                amount = Math.Round(amount, 2, MidpointRounding.AwayFromZero);
                if (amount <= 0) return BadRequest(new { message = "Invalid amount." });

                var paymentId = await _expensesService.RegisterPayment(payer, receiver, dto.GroupId, amount);
                var remainingAfter = await _expensesService.GetRemainingDebtAsync(payer, receiver, dto.GroupId);
                return Ok(new { PaymentId = paymentId, RemainingDebt = remainingAfter, SettledCount = 1 });
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

        [HttpGet("remaining-debt")]
        [Authorize]
        public async Task<IActionResult> GetRemainingDebt([FromQuery] int otherUserId, [FromQuery] int groupId)
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userIdClaim) || !int.TryParse(userIdClaim, out var currentUserId))
                return Unauthorized();

            if (!await _groupService.IsUserMemberAsync(groupId, currentUserId))
                return Forbid();

            var remaining = await _expensesService.GetRemainingDebtAsync(otherUserId, currentUserId, groupId);
            // Also try opposite direction if no debt in that direction
            if (remaining <= 0)
                remaining = await _expensesService.GetRemainingDebtAsync(currentUserId, otherUserId, groupId);
            return Ok(new { RemainingDebt = Math.Abs(remaining) });
        }
    }
}
