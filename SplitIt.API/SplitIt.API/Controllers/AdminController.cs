using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SplitIt.Application.DTOs;
using SplitIt.Domain.Entities;
using SplitIt.Infrastructure.Persistence;
using SplitIt.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace SplitIt.API.Controllers
{
    [Route("api/admin")]
    [ApiController]
    [Authorize(Roles = "SuperAdmin,Admin")]
    public class AdminController : ControllerBase
    {
        private readonly UsersService _usersService;
        private readonly AppDbContext _context;
        private readonly SettingsService _settingsService;
        private readonly CurrenciesService _currenciesService;

        public AdminController(UsersService usersService, AppDbContext context, SettingsService settingsService, CurrenciesService currenciesService)
        {
            _usersService = usersService;
            _context = context;
            _settingsService = settingsService;
            _currenciesService = currenciesService;
        }

        private bool IsSuperAdmin()
        {
            var role = User.FindFirst(ClaimTypes.Role)?.Value;
            return role == nameof(RoleConstants.SuperAdmin);
        }

        [HttpGet("stats")]
        public async Task<IActionResult> GetStats()
        {
            var stats = new
            {
                TotalUsers = await _context.Users.CountAsync(),
                ActiveUsers = await _context.Users.CountAsync(u => u.IsActive),
                TotalGroups = await _context.Groups.CountAsync(),
                TotalExpenses = await _context.Expense.CountAsync(e => !e.IsPayment),
                TotalPayments = await _context.Expense.CountAsync(e => e.IsPayment)
            };
            return Ok(stats);
        }

        [HttpGet("users")]
        public async Task<IActionResult> GetUsers([FromQuery] string? q = null, [FromQuery] int page = 1, [FromQuery] int pageSize = 20)
        {
            if (page < 1) page = 1;
            if (pageSize < 1 || pageSize > 100) pageSize = 20;

            var query = _context.Users.AsQueryable();
            if (!string.IsNullOrWhiteSpace(q))
            {
                var term = q.Trim().ToLower();
                query = query.Where(u => u.Name.ToLower().Contains(term) || u.Email.ToLower().Contains(term));
            }

            var total = await query.CountAsync();
            var items = await query
                .OrderBy(u => u.Id)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(u => new UserAdminDto
                {
                    Id = u.Id,
                    Name = u.Name,
                    Email = u.Email,
                    RoleId = u.RoleId,
                    RoleName = RoleConstants.GetName(u.RoleId),
                    IsActive = u.IsActive,
                    CreatedAt = u.CreatedAt
                })
                .ToListAsync();

            return Ok(new { items, total, page, pageSize });
        }

        [HttpPut("users/{userId}/role")]
        [Authorize(Roles = "SuperAdmin")]
        public async Task<IActionResult> UpdateUserRole(int userId, [FromBody] UpdateUserRoleDto dto)
        {
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

        [HttpPost("promote")]
        [Authorize(Roles = "SuperAdmin")]
        public async Task<IActionResult> PromoteUser([FromBody] PromoteUserDto dto)
        {
            var claim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(claim) || !int.TryParse(claim, out var requesterId)) return Unauthorized();
            try
            {
                await _usersService.UpdateUserRoleAsync(dto.UserId, RoleConstants.Admin, requesterId);
                return Ok(new { message = "User promoted to Admin." });
            }
            catch (UnauthorizedAccessException ex) { return Forbid(ex.Message); }
            catch (ArgumentException ex) { return BadRequest(new { message = ex.Message }); }
            catch (KeyNotFoundException ex) { return NotFound(new { message = ex.Message }); }
        }

        [HttpPut("users/{userId}/active")]
        public async Task<IActionResult> SetUserActive(int userId, [FromBody] SetUserActiveDto dto)
        {
            var claim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(claim) || !int.TryParse(claim, out var requesterId)) return Unauthorized();
            if (userId == requesterId)
                return BadRequest(new { message = "You cannot deactivate your own account." });

            var user = await _context.Users.FirstOrDefaultAsync(u => u.Id == userId);
            if (user == null) return NotFound(new { message = "User not found." });

            user.IsActive = dto.IsActive;
            await _context.SaveChangesAsync();
            return Ok(new { message = dto.IsActive ? "User activated." : "User deactivated." });
        }

        [HttpGet("groups")]
        public async Task<IActionResult> GetGroups([FromQuery] string? q = null)
        {
            var query = _context.Groups.AsQueryable();
            if (!string.IsNullOrWhiteSpace(q))
            {
                var term = q.Trim().ToLower();
                query = query.Where(g => g.Name.ToLower().Contains(term));
            }

            var groups = await query
                .OrderByDescending(g => g.CreatedAt)
                .Take(100)
                .Select(g => new GroupAdminDto
                {
                    Id = g.Id,
                    Name = g.Name,
                    Description = g.Description,
                    CurrencyId = g.CurrencyId,
                    MemberCount = g.GroupMembers.Count,
                    ExpenseCount = g.Expenses.Count,
                    CreatedAt = g.CreatedAt
                })
                .ToListAsync();

            return Ok(groups);
        }

        [HttpGet("settings")]
        public async Task<IActionResult> GetSettings()
        {
            var settings = await _settingsService.GetAllAsync();
            return Ok(settings);
        }

        [HttpPut("settings")]
        [Authorize(Roles = "SuperAdmin")]
        public async Task<IActionResult> UpdateSettings([FromBody] Dictionary<string, string> settings)
        {
            var allowedKeys = new HashSet<string> { SettingsService.RegistrationEnabled, SettingsService.MaxExpenseAmount };
            foreach (var (key, value) in settings)
            {
                if (!allowedKeys.Contains(key))
                    return BadRequest(new { message = $"Unknown setting '{key}'." });

                if (key == SettingsService.MaxExpenseAmount)
                {
                    if (!decimal.TryParse(value, out var max) || max <= 0 || max > 100000000)
                        return BadRequest(new { message = "MaxExpenseAmount must be a positive number." });
                }
                else if (key == SettingsService.RegistrationEnabled)
                {
                    if (!bool.TryParse(value, out _))
                        return BadRequest(new { message = "RegistrationEnabled must be true or false." });
                }

                await _settingsService.SetValueAsync(key, value);
            }
            return Ok(new { message = "Settings updated." });
        }

        [HttpPost("currencies")]
        [Authorize(Roles = "SuperAdmin")]
        public async Task<IActionResult> CreateCurrency([FromBody] CreateCurrencyDto dto)
        {
            if (!ModelState.IsValid) return ValidationProblem(ModelState);
            var exists = await _context.Currencies.AnyAsync(c => c.Name.ToLower() == dto.Name.ToLower());
            if (exists) return Conflict(new { message = "A currency with that name already exists." });

            var currency = new Currency { Name = dto.Name.Trim(), Symbol = dto.Symbol.Trim() };
            _context.Currencies.Add(currency);
            await _context.SaveChangesAsync();
            return Ok(currency);
        }

        [HttpDelete("currencies/{currencyId}")]
        [Authorize(Roles = "SuperAdmin")]
        public async Task<IActionResult> DeleteCurrency(int currencyId)
        {
            var currency = await _context.Currencies.FirstOrDefaultAsync(c => c.Id == currencyId);
            if (currency == null) return NotFound(new { message = "Currency not found." });

            var inUse = await _context.Groups.AnyAsync(g => g.CurrencyId == currencyId);
            if (inUse) return Conflict(new { message = "Currency is in use by one or more groups." });

            _context.Currencies.Remove(currency);
            await _context.SaveChangesAsync();
            return Ok(new { message = "Currency deleted." });
        }
    }
}
