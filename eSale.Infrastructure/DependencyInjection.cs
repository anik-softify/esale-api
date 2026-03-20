using eSale.Domain.Modules.Products.Interfaces;
using eSale.Infrastructure.Modules.Products;
using eSale.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace eSale.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException(
                "Database connection string 'DefaultConnection' is missing. " +
                "Set it via environment variable 'ConnectionStrings__DefaultConnection'.");

        services.AddDbContextPool<AppDbContext>(options =>
            options.UseMySql(connectionString, ServerVersion.AutoDetect(connectionString)));

        // Register repositories — one line per module, easy to find
        services.AddScoped<IProductRepository, ProductRepository>();
        services.AddScoped<DbInitializer>();

        return services;
    }
}
