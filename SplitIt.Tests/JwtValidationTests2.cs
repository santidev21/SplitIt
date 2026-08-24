using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;

namespace SplitIt.Tests;

public class JwtValidationTests
{
    private const string Secret = "TestSecretKey_That_Is_Long_Enough_For_HS256_64_chars_long_random_value_123456";
    private const string Issuer = "https://test-issuer";
    private const string Audience = "https://test-audience";

    private string GenerateToken(string secret, string issuer, string audience, DateTime? expires = null, string alg = SecurityAlgorithms.HmacSha256)
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secret));
        var creds = new SigningCredentials(key, alg);
        var token = new JwtSecurityToken(
            issuer: issuer,
            audience: audience,
            claims: new[] { new Claim(ClaimTypes.NameIdentifier, "1"), new Claim(ClaimTypes.Role, "3") },
            expires: expires ?? DateTime.UtcNow.AddMinutes(60),
            signingCredentials: creds
        );
        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    private TokenValidationParameters GetValidationParams(string secret, string issuer, string audience, bool validateLifetime = true)
    {
        return new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secret)),
            ValidateIssuer = true,
            ValidIssuer = issuer,
            ValidateAudience = true,
            ValidAudience = audience,
            ValidateLifetime = validateLifetime,
            ClockSkew = TimeSpan.Zero,
            RequireSignedTokens = true,
            RequireExpirationTime = true,
            ValidAlgorithms = new[] { SecurityAlgorithms.HmacSha256 }
        };
    }

    [Fact]
    public void ValidToken_ShouldValidate()
    {
        var token = GenerateToken(Secret, Issuer, Audience);
        var handler = new JwtSecurityTokenHandler();
        var principal = handler.ValidateToken(token, GetValidationParams(Secret, Issuer, Audience), out _);
        Assert.NotNull(principal);
        Assert.Equal("1", principal.FindFirst(ClaimTypes.NameIdentifier)?.Value);
    }

    [Fact]
    public void MissingToken_ShouldFail()
    {
        var handler = new JwtSecurityTokenHandler();
        Assert.ThrowsAny<Exception>(() => { handler.ValidateToken("", GetValidationParams(Secret, Issuer, Audience), out var _); });
        Assert.ThrowsAny<Exception>(() => { handler.ValidateToken((string)null!, GetValidationParams(Secret, Issuer, Audience), out var _); });
    }

    [Fact]
    public void TamperedToken_ShouldFail()
    {
        var valid = GenerateToken(Secret, Issuer, Audience);
        // Corrupt payload (middle) to ensure signature mismatch
        var parts = valid.Split('.');
        var payload = parts[1];
        var tamperedPayload = payload.Substring(0, payload.Length / 2) + "XXXX" + payload.Substring(payload.Length / 2 + 4);
        var tampered = $"{parts[0]}.{tamperedPayload}.{parts[2]}";
        var handler = new JwtSecurityTokenHandler();
        Assert.ThrowsAny<Exception>(() => handler.ValidateToken(tampered, GetValidationParams(Secret, Issuer, Audience), out _));
    }

    [Fact]
    public void ExpiredToken_ShouldFail()
    {
        var expired = GenerateToken(Secret, Issuer, Audience, expires: DateTime.UtcNow.AddMinutes(-5));
        var handler = new JwtSecurityTokenHandler();
        Assert.Throws<SecurityTokenExpiredException>(() => handler.ValidateToken(expired, GetValidationParams(Secret, Issuer, Audience), out _));
    }

    [Fact]
    public void WrongIssuer_ShouldFail()
    {
        var token = GenerateToken(Secret, "https://wrong-issuer", Audience);
        var handler = new JwtSecurityTokenHandler();
        Assert.Throws<SecurityTokenInvalidIssuerException>(() => handler.ValidateToken(token, GetValidationParams(Secret, Issuer, Audience), out _));
    }

    [Fact]
    public void WrongAudience_ShouldFail()
    {
        var token = GenerateToken(Secret, Issuer, "https://wrong-audience");
        var handler = new JwtSecurityTokenHandler();
        Assert.Throws<SecurityTokenInvalidAudienceException>(() => handler.ValidateToken(token, GetValidationParams(Secret, Issuer, Audience), out _));
    }

    [Fact]
    public void WrongSignature_ShouldFail()
    {
        var token = GenerateToken("WrongSecretKey_Different_Value_That_Is_Also_Long_Enough_For_Testing_1234567890", Issuer, Audience);
        var handler = new JwtSecurityTokenHandler();
        Assert.ThrowsAny<SecurityTokenException>(() => handler.ValidateToken(token, GetValidationParams(Secret, Issuer, Audience), out _));
    }

    [Fact]
    public void ClockSkew_Zero_ShouldRejectSlightlyExpired()
    {
        // Token expired 1 sec ago — with ClockSkew.Zero should fail, with 5min would pass
        var token = GenerateToken(Secret, Issuer, Audience, expires: DateTime.UtcNow.AddSeconds(-1));
        var handler = new JwtSecurityTokenHandler();
        Assert.Throws<SecurityTokenExpiredException>(() => handler.ValidateToken(token, GetValidationParams(Secret, Issuer, Audience), out _));
    }

    [Fact]
    public void NoneAlgorithm_ShouldFail()
    {
        var header = Convert.ToBase64String(Encoding.UTF8.GetBytes("{\"alg\":\"none\",\"typ\":\"JWT\"}")).TrimEnd('=').Replace('+', '-').Replace('/', '_');
        var payloadJson = "{\"sub\":\"1\",\"exp\":" + DateTimeOffset.UtcNow.AddMinutes(60).ToUnixTimeSeconds() + ",\"iss\":\"" + Issuer + "\",\"aud\":\"" + Audience + "\"}";
        var payload = Convert.ToBase64String(Encoding.UTF8.GetBytes(payloadJson)).TrimEnd('=').Replace('+', '-').Replace('/', '_');
        var noneToken = $"{header}.{payload}.";
        var handler = new JwtSecurityTokenHandler();
        Assert.ThrowsAny<Exception>(() => handler.ValidateToken(noneToken, GetValidationParams(Secret, Issuer, Audience), out _));
    }
}
