using Microsoft.EntityFrameworkCore;
using SplitIt.Infrastructure.Persistence;

namespace SplitIt.Tests.Helpers;

public static class TestDbHelper
{
    public static AppDbContext CreateInMemoryContext(string dbName = "")
    {
        if (string.IsNullOrEmpty(dbName)) dbName = Guid.NewGuid().ToString();
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: dbName)
            .Options;
        return new AppDbContext(options);
    }
}
