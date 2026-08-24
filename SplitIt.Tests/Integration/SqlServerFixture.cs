using DotNet.Testcontainers.Builders;
using Microsoft.EntityFrameworkCore;
using SplitIt.Infrastructure.Persistence;
using Testcontainers.MsSql;

namespace SplitIt.Tests.Integration;

/// <summary>
/// Fixture for real SQL Server via Testcontainers.
/// Skips tests gracefully if Docker is not available (CI compatibility).
/// </summary>
public class SqlServerFixture : IAsyncLifetime
{
    private MsSqlContainer? _container;
    public string ConnectionString => _container?.GetConnectionString() ?? "Server=(localdb)\\mssqllocaldb;Database=SplitItSkipped;Trusted_Connection=True;";
    public bool IsAvailable { get; private set; } = false;

    public async Task InitializeAsync()
    {
        try
        {
            _container = new MsSqlBuilder()
                .WithImage("mcr.microsoft.com/mssql/server:2022-latest")
                .WithPassword("Strong_Passw0rd123!")
                .WithPortBinding(1433, true)
                .WithWaitStrategy(Wait.ForUnixContainer().UntilPortIsAvailable(1433))
                .Build();
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            await _container.StartAsync(cts.Token);
            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseSqlServer(ConnectionString)
                .Options;
            using var ctx = new AppDbContext(options);
            await ctx.Database.MigrateAsync(cts.Token);
            IsAvailable = true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[SqlServerFixture] Docker not available or timeout, integration tests will be skipped: {ex.Message}");
            IsAvailable = false;
            _container = null;
        }
    }

    public async Task DisposeAsync()
    {
        if (_container != null && IsAvailable)
            await _container.DisposeAsync();
    }

    public AppDbContext CreateContext()
    {
        if (!IsAvailable || _container == null) throw new SkipException("Docker not available — skipping SQL Server integration test.");
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlServer(ConnectionString)
            .Options;
        return new AppDbContext(options);
    }
}

public class SkipException : Exception
{
    public SkipException(string msg) : base(msg) { }
}
