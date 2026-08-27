using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SplitIt.Infrastructure.Services;

namespace SplitIt.API.Controllers
{
    /// <summary>
    /// Public (anonymous) endpoint used by the register form to know if
    /// registration is enabled and what the max expense amount is.
    /// </summary>
    [Route("api/settings")]
    [ApiController]
    public class SettingsController : ControllerBase
    {
        private readonly SettingsService _settingsService;

        public SettingsController(SettingsService settingsService)
        {
            _settingsService = settingsService;
        }

        [HttpGet("public")]
        [AllowAnonymous]
        public async Task<IActionResult> GetPublicSettings()
        {
            var registrationEnabled = await _settingsService.GetValueAsync(SettingsService.RegistrationEnabled, true);
            var maxExpenseAmount = await _settingsService.GetValueAsync(SettingsService.MaxExpenseAmount, 1000000m);
            return Ok(new { registrationEnabled, maxExpenseAmount });
        }
    }
}
