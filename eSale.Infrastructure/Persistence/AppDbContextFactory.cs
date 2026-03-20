using eSale.Application.Common.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;

namespace eSale.Infrastructure.Persistence;

public sealed class AppDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext(string[] args)
    {
        var basePath = Directory.GetCurrentDirectory();
        var apiPath = Path.Combine(basePath, "..", "eSale.Api");

        var configuration = new ConfigurationBuilder()
            .SetBasePath(Directory.Exists(apiPath) ? apiPath : basePath)
            .AddJsonFile("appsettings.json", optional: true)
            .AddJsonFile("appsettings.Development.json", optional: true)
            .AddEnvironmentVariables()
            .Build();

        var connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException(
                "Database connection string 'DefaultConnection' is missing. " +
                "Set it via environment variable 'ConnectionStrings__DefaultConnection' before running EF commands.");

        var optionsBuilder = new DbContextOptionsBuilder<AppDbContext>();
        optionsBuilder.UseMySql(connectionString, ServerVersion.AutoDetect(connectionString));

        return new AppDbContext(optionsBuilder.Options, new DesignTimeTenantProvider());
    }

    private sealed class DesignTimeTenantProvider : ITenantProvider
    {
        public Guid GetTenantId() => Guid.Empty;
    }
}
