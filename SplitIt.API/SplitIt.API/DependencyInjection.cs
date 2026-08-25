using Microsoft.EntityFrameworkCore;
using SplitIt.Infrastructure.Persistence;
using System;

namespace SplitIt.API
{
    public static class DependencyInjection
    {
        public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
        {
            var connectionString = configuration.GetConnectionString("DefaultConnection");
            if (string.IsNullOrWhiteSpace(connectionString))
            {
                // In dev, EF will fail on first DB call with clear message; in prod builder already throws for missing config elsewhere
                Console.WriteLine("WARNING: ConnectionStrings:DefaultConnection is empty. Set via env ConnectionStrings__DefaultConnection.");
            }

            services.AddDbContext<AppDbContext>(options =>
                options.UseSqlServer(connectionString, sqlOpts =>
                {
                    // Production resilience: transient retry for SQL Server (3 retries, 2s delay)
                    sqlOpts.EnableRetryOnFailure(
                        maxRetryCount: 3,
                        maxRetryDelay: TimeSpan.FromSeconds(2),
                        errorNumbersToAdd: null);
                    // Command timeout 30s default, keep as is for monetary transactions
                    sqlOpts.CommandTimeout(30);
                }));

            return services;
        }
    }
}
