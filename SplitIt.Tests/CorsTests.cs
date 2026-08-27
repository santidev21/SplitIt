using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using System.Net;

namespace SplitIt.Tests;

/// <summary>
/// CORS fail-closed and allowed-origin tests under Production environment.
///
/// Program.cs validates JwtSettings:SecretKey and reads Cors:AllowedOrigins
/// during WebApplication.CreateBuilder() — BEFORE WebApplicationFactory's
/// WithWebHostBuilder.ConfigureAppConfiguration is applied (that callback is
/// deferred to builder.Build()).  To make configuration available at that
/// early stage, we set environment variables (JwtSettings__SecretKey, etc.)
/// which WebApplicationBuilder reads immediately during construction.
/// </summary>
public class CorsTests : IClassFixture<WebApplicationFactory<Program>>, IDisposable
{
    private readonly WebApplicationFactory<Program> _factory;

    private const string TestSecretKey = "TestSecretKey_That_Is_Long_Enough_For_HS256_64_chars_random_value_123456";
    private const string TestIssuer = "https://test-issuer";
    private const string TestAudience = "https://test-audience";
    private const string TestConnStr = "Server=(localdb)\\mssqllocaldb;Database=SplitItCorsTest;Trusted_Connection=True;TrustServerCertificate=True;";
    private const string TestAllowedOrigin = "https://splitit.yourdomain.com";

    private readonly string? _origSecret;
    private readonly string? _origIssuer;
    private readonly string? _origAudience;
    private readonly string? _origCors;
    private readonly string? _origConnStr;

    public CorsTests(WebApplicationFactory<Program> factory)
    {
        _origSecret = Environment.GetEnvironmentVariable("JwtSettings__SecretKey");
        _origIssuer = Environment.GetEnvironmentVariable("JwtSettings__Issuer");
        _origAudience = Environment.GetEnvironmentVariable("JwtSettings__Audience");
        _origCors = Environment.GetEnvironmentVariable("Cors__AllowedOrigins");
        _origConnStr = Environment.GetEnvironmentVariable("ConnectionStrings__DefaultConnection");

        Environment.SetEnvironmentVariable("JwtSettings__SecretKey", TestSecretKey);
        Environment.SetEnvironmentVariable("JwtSettings__Issuer", TestIssuer);
        Environment.SetEnvironmentVariable("JwtSettings__Audience", TestAudience);
        Environment.SetEnvironmentVariable("Cors__AllowedOrigins", TestAllowedOrigin);
        Environment.SetEnvironmentVariable("ConnectionStrings__DefaultConnection", TestConnStr);

        _factory = factory.WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Production");
        });
    }

    public void Dispose()
    {
        RestoreEnv("JwtSettings__SecretKey", _origSecret);
        RestoreEnv("JwtSettings__Issuer", _origIssuer);
        RestoreEnv("JwtSettings__Audience", _origAudience);
        RestoreEnv("Cors__AllowedOrigins", _origCors);
        RestoreEnv("ConnectionStrings__DefaultConnection", _origConnStr);
    }

    private static void RestoreEnv(string key, string? original)
    {
        if (original is null)
            Environment.SetEnvironmentVariable(key, null);
        else
            Environment.SetEnvironmentVariable(key, original);
    }

    [Fact]
    public async Task Preflight_AllowedOrigin_IsAllowed()
    {
        using var client = _factory.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Options, "/api/auth/login");
        request.Headers.Add("Origin", "https://splitit.yourdomain.com");
        request.Headers.Add("Access-Control-Request-Method", "POST");
        request.Headers.Add("Access-Control-Request-Headers", "Content-Type,Authorization");

        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Equal("https://splitit.yourdomain.com", response.Headers.GetValues("Access-Control-Allow-Origin").FirstOrDefault());
        Assert.Contains(response.Headers.GetValues("Access-Control-Allow-Methods"), v => v.Contains("POST"));
    }

    [Fact]
    public async Task Preflight_DisallowedOrigin_IsBlocked()
    {
        using var client = _factory.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Options, "/api/auth/login");
        request.Headers.Add("Origin", "https://evil.example.com");
        request.Headers.Add("Access-Control-Request-Method", "POST");

        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.False(response.Headers.Contains("Access-Control-Allow-Origin"));
    }

    [Fact]
    public async Task ActualRequest_DisallowedOrigin_DoesNotExposeAllowOrigin()
    {
        using var client = _factory.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/currencies");
        request.Headers.Add("Origin", "https://evil.example.com");

        var response = await client.SendAsync(request);

        Assert.False(response.Headers.Contains("Access-Control-Allow-Origin"));
    }

    [Fact]
    public async Task Production_EmptyAllowedOrigins_FailClosed()
    {
        Environment.SetEnvironmentVariable("Cors__AllowedOrigins", "");
        try
        {
            using var scopedFactory = _factory.WithWebHostBuilder(builder =>
            {
                builder.UseEnvironment("Production");
            });

            using var client = scopedFactory.CreateClient();
            using var request = new HttpRequestMessage(HttpMethod.Options, "/api/auth/login");
            request.Headers.Add("Origin", "https://any-origin.example.com");
            request.Headers.Add("Access-Control-Request-Method", "POST");

            var response = await client.SendAsync(request);

            Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
            Assert.False(response.Headers.Contains("Access-Control-Allow-Origin"));
        }
        finally
        {
            Environment.SetEnvironmentVariable("Cors__AllowedOrigins", TestAllowedOrigin);
        }
    }
}
