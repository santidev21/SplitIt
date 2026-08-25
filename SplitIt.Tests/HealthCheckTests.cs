using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SplitIt.Infrastructure.Persistence;

namespace SplitIt.Tests;

public class HealthCheckTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public HealthCheckTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory.WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Testing");
        });
    }

    [Fact]
    public async Task Health_Live_Returns_Healthy()
    {
        var client = _factory.CreateClient();
        var resp = await client.GetAsync("/health/live");
        Assert.Equal(System.Net.HttpStatusCode.OK, resp.StatusCode);
        var body = await resp.Content.ReadAsStringAsync();
        Assert.Equal("Healthy", body);
    }

    [Fact]
    public async Task Health_Ready_Returns_Status_Reflecting_Db()
    {
        var client = _factory.CreateClient();
        var resp = await client.GetAsync("/health/ready");
        // Ready should return either Healthy (200) if DB available, or Unhealthy (503) if not — but never 404 and body indicates status
        Assert.True(resp.StatusCode == System.Net.HttpStatusCode.OK || resp.StatusCode == System.Net.HttpStatusCode.ServiceUnavailable,
            $"Unexpected status {resp.StatusCode}");
        var body = await resp.Content.ReadAsStringAsync();
        Assert.True(body == "Healthy" || body == "Unhealthy", $"Unexpected body {body}");
    }

    [Fact]
    public async Task Health_Aggregate_Returns_Status()
    {
        var client = _factory.CreateClient();
        var resp = await client.GetAsync("/health");
        Assert.True(resp.StatusCode == System.Net.HttpStatusCode.OK || resp.StatusCode == System.Net.HttpStatusCode.ServiceUnavailable,
            $"Unexpected status {resp.StatusCode}");
        var body = await resp.Content.ReadAsStringAsync();
        Assert.True(body == "Healthy" || body == "Unhealthy");
    }
}
