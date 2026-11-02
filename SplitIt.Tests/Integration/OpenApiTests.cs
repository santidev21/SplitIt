using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using System.Net;
using System.Text.Json;

namespace SplitIt.Tests.Integration;

/// <summary>
/// The OpenAPI document and the Scalar UI are Development-only: they must be served
/// locally (with the Bearer scheme) and must never be exposed in Production.
/// </summary>
[Collection(SplitIt.Tests.ProcessEnvironmentCollection.Name)]
public class OpenApiTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public OpenApiTests(WebApplicationFactory<Program> factory) => _factory = factory;

    private HttpClient CreateClient(string environment) =>
        _factory.WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment(environment);
            builder.ConfigureAppConfiguration((_, config) =>
            {
                config.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["JwtSettings:SecretKey"] = "TestSecretKey_That_Is_Long_Enough_For_HS256_64_chars_random_value_123456",
                    ["JwtSettings:Issuer"] = "https://test-issuer",
                    ["JwtSettings:Audience"] = "https://test-audience",
                    ["Cors:AllowedOrigins"] = "http://localhost:4200"
                });
            });
            builder.ConfigureServices(services =>
            {
                var descriptor = services.SingleOrDefault(d => d.ServiceType == typeof(DbContextOptions<SplitIt.Infrastructure.Persistence.AppDbContext>));
                if (descriptor != null) services.Remove(descriptor);

                services.AddDbContext<SplitIt.Infrastructure.Persistence.AppDbContext>(options =>
                    options.UseInMemoryDatabase($"OpenApiTests_{Guid.NewGuid()}"));
            });
        }).CreateClient();

    [Fact]
    public async Task Development_ServesOpenApiDocument_WithBearerScheme()
    {
        using var client = CreateClient("Development");

        var response = await client.GetAsync("/openapi/v1.json");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal("SplitIt API", document.RootElement.GetProperty("info").GetProperty("title").GetString());
        var bearer = document.RootElement.GetProperty("components").GetProperty("securitySchemes").GetProperty("Bearer");
        Assert.Equal("bearer", bearer.GetProperty("scheme").GetString());
        Assert.Equal("JWT", bearer.GetProperty("bearerFormat").GetString());
        Assert.True(document.RootElement.GetProperty("paths").EnumerateObject().Any());
    }

    [Fact]
    public async Task Production_DoesNotExposeOpenApiDocumentOrScalarUi()
    {
        // Program.cs validates these during CreateBuilder(), before the factory's
        // configuration callback runs, so they have to come from the environment.
        var variables = new Dictionary<string, string>
        {
            ["JwtSettings__SecretKey"] = "TestSecretKey_That_Is_Long_Enough_For_HS256_64_chars_random_value_123456",
            ["JwtSettings__Issuer"] = "https://test-issuer",
            ["JwtSettings__Audience"] = "https://test-audience",
            ["Cors__AllowedOrigins"] = "https://splitit.example.com",
            ["ConnectionStrings__DefaultConnection"] = "Server=(localdb)\\mssqllocaldb;Database=SplitItOpenApiTest;Trusted_Connection=True;TrustServerCertificate=True;"
        };
        var originals = variables.Keys.ToDictionary(k => k, Environment.GetEnvironmentVariable);
        try
        {
            foreach (var (key, value) in variables)
                Environment.SetEnvironmentVariable(key, value);

            using var client = CreateClient("Production");

            var document = await client.GetAsync("/openapi/v1.json");
            var ui = await client.GetAsync("/scalar");

            Assert.Equal(HttpStatusCode.NotFound, document.StatusCode);
            Assert.Equal(HttpStatusCode.NotFound, ui.StatusCode);
        }
        finally
        {
            foreach (var (key, value) in originals)
                Environment.SetEnvironmentVariable(key, value);
        }
    }
}
