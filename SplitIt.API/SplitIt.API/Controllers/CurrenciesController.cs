using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SplitIt.Domain.Entities;
using SplitIt.Infrastructure.Services;
using System.Text.RegularExpressions;

namespace SplitIt.API.Controllers
{
    [Route("api/currencies")]
    [ApiController]
    public class CurrenciesController : ControllerBase
    {
        private readonly CurrenciesService _currenciesService;
        private readonly IConfiguration _configuration;

        public CurrenciesController(CurrenciesService currenciesService, IConfiguration configuration)
        {
            _currenciesService = currenciesService;
            _configuration = configuration;
        }

        [HttpGet("getAll")]
        [Authorize]
        public async Task<IActionResult> GetAll()
        {
            var currencies = await _currenciesService.GetAllAsync();
            return Ok(currencies);
        }
    }
}
