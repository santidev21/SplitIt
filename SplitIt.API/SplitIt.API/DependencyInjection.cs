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
                options.UseSqlServer(connectionString));

            return services;
        }
    }
}
