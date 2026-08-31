using Google.Apis.Auth;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.IdentityModel.Tokens;
using SplitIt.Application.DTOs;
using SplitIt.Domain.Entities;
using SplitIt.Infrastructure.Services;

namespace SplitIt.API.Controllers
{
    [Route("api/auth")]
    [ApiController]
    public class AuthController : ControllerBase
    {
        private readonly AuthService _authService;
        private readonly TokenService _tokenService;
        private readonly IConfiguration _configuration;
        private readonly SettingsService? _settingsService;

        public AuthController(AuthService authService, TokenService tokenService, IConfiguration configuration, SettingsService? settingsService = null)
        {
            _authService = authService;
            _tokenService = tokenService;
            _configuration = configuration;
            _settingsService = settingsService;
        }

        [HttpPost("register")]
        [EnableRateLimiting("auth")]
        public async Task<IActionResult> Register([FromBody] RegisterRequestDto request)
        {
            if (_settingsService != null && !await _settingsService.GetValueAsync(SettingsService.RegistrationEnabled, true))
                return BadRequest(new { message = "Registration is currently disabled. Contact an administrator." });

            bool success = await _authService.RegisterUser(request.Name, request.Email, request.Password);
            if (!success)
                return Conflict(new { message = "Unable to register. If the email is already registered, try logging in." });

            var user = await _authService.GetUserByEmail(request.Email);
            if (user == null)
                return StatusCode(500, new { message = "An error occurred while retrieving the user." });

            var accessToken = _tokenService.GenerateAccessToken(user);
            var refreshResult = await _tokenService.IssueRefreshTokenAsync(user);
            SetRefreshTokenCookie(refreshResult.TokenHash);

            return Ok(new
            {
                message = "Registration successful.",
                token = accessToken,
                userName = user.Name,
                userId = user.Id
            });
        }

        [HttpPost("login")]
        [EnableRateLimiting("auth")]
        public async Task<IActionResult> Login([FromBody] LoginRequestDto request)
        {
            var user = await _authService.GetUserByEmail(request.Email);
            if (user == null || !await _authService.ValidateUser(request.Email, request.Password))
                return Unauthorized(new { message = "Incorrect email or password." });

            user = await _authService.GetUserByEmail(request.Email);
            var accessToken = _tokenService.GenerateAccessToken(user!);
            var refreshResult = await _tokenService.IssueRefreshTokenAsync(user!);
            SetRefreshTokenCookie(refreshResult.TokenHash);

            return Ok(new { message = "Login successful.", token = accessToken, userName = user!.Name, userId = user.Id });
        }

        [HttpPost("google")]
        [EnableRateLimiting("auth")]
        public async Task<IActionResult> GoogleLogin([FromBody] GoogleLoginDto request)
        {
            if (_settingsService != null && !await _settingsService.GetValueAsync(SettingsService.RegistrationEnabled, true))
                return BadRequest(new { message = "Registration is currently disabled. Contact an administrator." });

            var googleClientId = _configuration["Google:ClientId"];
            if (string.IsNullOrWhiteSpace(googleClientId))
                return StatusCode(500, new { message = "Google sign-in is not configured." });

            GoogleJsonWebSignature.Payload payload;
            try
            {
                var settings = new GoogleJsonWebSignature.ValidationSettings
                {
                    Audience = new[] { googleClientId }
                };
                payload = await GoogleJsonWebSignature.ValidateAsync(request.IdToken, settings);
            }
            catch (InvalidJwtException)
            {
                return Unauthorized(new { message = "Invalid Google token." });
            }

            if (payload.EmailVerified != true)
                return Unauthorized(new { message = "Google email is not verified." });

            var email = payload.Email.Trim().ToLowerInvariant();
            var user = await _authService.GetUserByEmail(email);

            if (user == null)
            {
                var randomPassword = Convert.ToBase64String(System.Security.Cryptography.RandomNumberGenerator.GetBytes(32));
                var registered = await _authService.RegisterUser(payload.Name ?? email, email, randomPassword);
                if (!registered)
                    return Conflict(new { message = "Unable to create account." });

                user = await _authService.GetUserByEmail(email);
                if (user == null)
                    return StatusCode(500, new { message = "An error occurred while retrieving the user." });
            }

            var accessToken = _tokenService.GenerateAccessToken(user);
            var refreshResult = await _tokenService.IssueRefreshTokenAsync(user);
            SetRefreshTokenCookie(refreshResult.TokenHash);

            return Ok(new { message = "Google login successful.", token = accessToken, userName = user.Name, userId = user.Id });
        }

        [HttpPost("refresh")]
        [EnableRateLimiting("auth")]
        public async Task<IActionResult> Refresh()
        {
            var rawToken = Request.Cookies["refresh_token"];
            if (string.IsNullOrEmpty(rawToken))
                return Unauthorized(new { message = "No refresh token." });

            var result = await _tokenService.RotateRefreshTokenAsync(rawToken);
            if (result == null)
                return Unauthorized(new { message = "Invalid or expired refresh token." });

            var accessToken = _tokenService.GenerateAccessToken(result.Value.user);
            SetRefreshTokenCookie(result.Value.newRefreshToken.TokenHash);

            return Ok(new { token = accessToken });
        }

        [HttpPost("logout")]
        public async Task<IActionResult> Logout()
        {
            var rawToken = Request.Cookies["refresh_token"];
            if (!string.IsNullOrEmpty(rawToken))
                await _tokenService.RevokeRefreshTokenAsync(rawToken);

            ClearRefreshTokenCookie();
            return Ok(new { message = "Logged out." });
        }

        [HttpPost("forgot-password")]
        [EnableRateLimiting("auth")]
        public async Task<IActionResult> ForgotPassword([FromBody] ForgotPasswordDto request)
        {
            try
            {
                await _authService.GenerateResetTokenAsync(request.Email);
            }
            catch
            {
            }
            return Ok(new { message = "If the email exists, a reset code has been generated. Contact your administrator." });
        }

        [HttpPost("reset-password")]
        [EnableRateLimiting("auth")]
        public async Task<IActionResult> ResetPassword([FromBody] ResetPasswordDto request)
        {
            if (string.IsNullOrWhiteSpace(request.Token) || string.IsNullOrWhiteSpace(request.NewPassword))
                return BadRequest(new { message = "Token and new password are required." });

            if (request.NewPassword.Length < 8)
                return BadRequest(new { message = "Password must be at least 8 characters." });

            var success = await _authService.ResetPasswordAsync(request.Token, request.NewPassword);
            if (!success)
                return BadRequest(new { message = "Invalid or expired token." });

            return Ok(new { message = "Password has been reset successfully. You can now log in." });
        }

        [HttpPost("verify-reset-code")]
        [EnableRateLimiting("auth")]
        public async Task<IActionResult> VerifyResetCode([FromBody] VerifyResetCodeDto request)
        {
            if (request.NewPassword.Length < 8)
                return BadRequest(new { message = "Password must be at least 8 characters." });

            var success = await _authService.ResetPasswordAsync(request.Code, request.NewPassword);
            if (!success)
                return BadRequest(new { message = "Invalid or expired code." });

            return Ok(new { message = "Password has been reset successfully. You can now log in." });
        }

        private void SetRefreshTokenCookie(string token)
        {
            var expirationDays = _configuration.GetValue<int>("JwtSettings:RefreshTokenExpirationInDays");
            if (expirationDays <= 0) expirationDays = 30;

            var cookieOptions = new CookieOptions
            {
                HttpOnly = true,
                Secure = !HttpContext.Request.Host.Host.Contains("localhost"),
                SameSite = SameSiteMode.Lax,
                Path = "/",
                MaxAge = TimeSpan.FromDays(expirationDays),
            };
            Response.Cookies.Append("refresh_token", token, cookieOptions);
        }

        private void ClearRefreshTokenCookie()
        {
            Response.Cookies.Delete("refresh_token", new CookieOptions
            {
                Path = "/",
                HttpOnly = true,
                Secure = !HttpContext.Request.Host.Host.Contains("localhost"),
                SameSite = SameSiteMode.Lax,
            });
        }
    }
}
