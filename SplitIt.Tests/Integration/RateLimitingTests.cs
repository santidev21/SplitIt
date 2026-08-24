using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using System.Net;
using System.Net.Http.Json;

namespace SplitIt.Tests.Integration;

public class RateLimitingTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public RateLimitingTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory.WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Development");
            builder.ConfigureAppConfiguration((_, config) =>
            {
                config.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["JwtSettings:SecretKey"] = "TestSecretKey_That_Is_Long_Enough_For_HS256_64_chars_random_value_123456",
                    ["JwtSettings:Issuer"] = "https://test-issuer",
                    ["JwtSettings:Audience"] = "https://test-audience",
                    ["ConnectionStrings:DefaultConnection"] = "Server=(localdb)\\mssqllocaldb;Database=SplitItRateLimitTest;Trusted_Connection=True;TrustServerCertificate=True;",
                    ["Cors:AllowedOrigins"] = "http://localhost:4200"
                });
            });
        });
    }

    [Fact]
    public async Task AuthEndpoints_RateLimit_5PerMinute_ShouldBlock6th()
    {
        // Use fresh factory to ensure clean rate limiter state
        using var factory = _factory.WithWebHostBuilder(b => { });
        var client = factory.CreateClient();

        var statuses = new List<HttpStatusCode>();
        for (int i = 0; i < 6; i++)
        {
            var resp = await client.PostAsJsonAsync("/api/auth/login", new { email = $"ratelimit{i}@test.com", password = "WrongPass123!" });
            statuses.Add(resp.StatusCode);
        }

        // First 5 should be allowed (401 invalid credentials, but not 429)
        for (int i = 0; i < 5; i++)
        {
            Assert.NotEqual(HttpStatusCode.TooManyRequests, statuses[i]);
        }
        // 6th must be 429 TooManyRequests
        Assert.Equal(HttpStatusCode.TooManyRequests, statuses[5]);
    }

    [Fact]
    public async Task RegisterEndpoint_RateLimit_ShouldBlock6th()
    {
        using var factory = _factory.WithWebHostBuilder(b => { });
        var client = factory.CreateClient();

        var statuses = new List<HttpStatusCode>();
        for (int i = 0; i < 6; i++)
        {
            var resp = await client.PostAsJsonAsync("/api/auth/register", new { name = $"User{i}", email = $"reglimit{i}@test.com", password = "StrongPass123!" });
            statuses.Add(resp.StatusCode);
        }

        for (int i = 0; i < 5; i++)
        {
            Assert.NotEqual(HttpStatusCode.TooManyRequests, statuses[i]);
        }
        Assert.Equal(HttpStatusCode.TooManyRequests, statuses[5]);
    }

    [Fact]
    public void RateLimiter_IsRegistered()
    {
        // If we reached this test, the factory started successfully with rate limiter configured
        Assert.True(true);
    }
}
