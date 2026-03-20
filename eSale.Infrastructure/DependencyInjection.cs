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
        var connectionString = configuration.GetConnectionString("DefaultConnection");

        services.AddDbContext<AppDbContext>(options =>
            options.UseMySQL(connectionString!));

        // Register repositories — one line per module, easy to find
        services.AddScoped<IProductRepository, ProductRepository>();

        return services;
    }
}
