using System.IO;
using System.Threading;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using SplitIt.API.Middleware;

namespace SplitIt.Tests;

public class GlobalExceptionHandlerTests
{
    private sealed class FakeHostEnvironment : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = "Production";
        public string ApplicationName { get; set; } = "SplitIt.Tests";
        public string ContentRootPath { get; set; } = string.Empty;
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }

    private static async Task<(bool handled, int status, string body)> RunAsync(Exception ex, bool isDev = false)
    {
        var handler = new GlobalExceptionHandler(NullLogger<GlobalExceptionHandler>.Instance, new FakeHostEnvironment
        {
            EnvironmentName = isDev ? "Development" : "Production"
        });
        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();

        var handled = await handler.TryHandleAsync(context, ex, CancellationToken.None);

        context.Response.Body.Seek(0, SeekOrigin.Begin);
        using var reader = new StreamReader(context.Response.Body);
        var body = await reader.ReadToEndAsync();
        return (handled, context.Response.StatusCode, body);
    }

    [Fact]
    public async Task ConcurrencyConflict_MapsTo409()
    {
        var (handled, status, body) = await RunAsync(new DbUpdateConcurrencyException("conflict"));
        Assert.True(handled);
        Assert.Equal(409, status);
        Assert.Contains("modified by someone else", body);
    }

    [Fact]
    public async Task BusinessRule_SurfacesMessage()
    {
        var (handled, status, body) = await RunAsync(new ArgumentException("Invalid currency."));
        Assert.True(handled);
        Assert.Equal(400, status);
        Assert.Contains("Invalid currency.", body);
    }

    [Fact]
    public async Task UnexpectedException_DoesNotLeakDetailsInProduction()
    {
        var (handled, status, body) = await RunAsync(new InvalidOperationException("secret stack detail"), isDev: false);
        Assert.True(handled);
        Assert.Equal(500, status);
        Assert.DoesNotContain("secret stack detail", body);
    }
}
