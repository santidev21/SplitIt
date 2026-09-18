using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using SplitIt.Infrastructure.Persistence;

namespace SplitIt.Tests.Helpers;

public static class TestDbHelper
{
    public static AppDbContext CreateInMemoryContext(string dbName = "")
    {
        if (string.IsNullOrEmpty(dbName)) dbName = Guid.NewGuid().ToString();
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: dbName)
            // The in-memory store does not support transactions; the app uses them
            // for atomic multi-step writes. Ignore the warning so tests can exercise
            // the same code paths as SQL Server.
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        return new AppDbContext(options);
    }
}
