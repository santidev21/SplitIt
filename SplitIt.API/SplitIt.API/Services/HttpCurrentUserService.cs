using System.Security.Claims;
using SplitIt.Infrastructure.Services;

namespace SplitIt.API.Services
{
    public class HttpCurrentUserService : ICurrentUserService
    {
        private readonly IHttpContextAccessor _accessor;

        public HttpCurrentUserService(IHttpContextAccessor accessor)
        {
            _accessor = accessor;
        }

        public int? UserId
        {
            get
            {
                var claim = _accessor.HttpContext?.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                return int.TryParse(claim, out var id) ? id : null;
            }
        }

        public string? IpAddress => _accessor.HttpContext?.Connection?.RemoteIpAddress?.ToString();
    }
}
