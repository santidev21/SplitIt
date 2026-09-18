using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using SplitIt.Infrastructure.Persistence;
using SplitIt.Infrastructure.Services;

namespace SplitIt.Tests.Helpers;

public static class TestDbHelper
{
    public static AppDbContext CreateInMemoryContext(string dbName = "", ICurrentUserService? currentUser = null)
    {
        if (string.IsNullOrEmpty(dbName)) dbName = Guid.NewGuid().ToString();
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: dbName)
            // The in-memory store does not support transactions; the app uses them
            // for atomic multi-step writes. Ignore the warning so tests can exercise
            // the same code paths as SQL Server.
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        return new AppDbContext(options, currentUser);
    }
}
