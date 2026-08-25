using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using System.Net;

namespace SplitIt.Tests;

public class CorsTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public CorsTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory.WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Production");
            builder.ConfigureAppConfiguration((_, config) =>
            {
                config.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["JwtSettings:SecretKey"] = "TestSecretKey_That_Is_Long_Enough_For_HS256_64_chars_random_value_123456",
                    ["JwtSettings:Issuer"] = "https://test-issuer",
                    ["JwtSettings:Audience"] = "https://test-audience",
                    ["ConnectionStrings:DefaultConnection"] = "Server=(localdb)\\mssqllocaldb;Database=SplitItCorsTest;Trusted_Connection=True;TrustServerCertificate=True;",
                    ["Cors:AllowedOrigins"] = "https://splitit.yourdomain.com"
                });
            });
        });
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
        Assert.Contains("POST", response.Headers.GetValues("Access-Control-Allow-Methods"));
    }

    [Fact]
    public async Task Preflight_DisallowedOrigin_IsBlocked()
    {
        using var client = _factory.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Options, "/api/auth/login");
        request.Headers.Add("Origin", "https://evil.example.com");
        request.Headers.Add("Access-Control-Request-Method", "POST");

        var response = await client.SendAsync(request);

        // When origin is not allowed, ASP.NET CORS returns 204 without Access-Control-Allow-Origin.
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
        using var scopedFactory = _factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureAppConfiguration((_, config) =>
            {
                config.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Cors:AllowedOrigins"] = ""
                });
            });
        });

        using var client = scopedFactory.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Options, "/api/auth/login");
        request.Headers.Add("Origin", "https://any-origin.example.com");
        request.Headers.Add("Access-Control-Request-Method", "POST");

        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.False(response.Headers.Contains("Access-Control-Allow-Origin"));
    }
}
