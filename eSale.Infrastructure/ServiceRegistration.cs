using eSale.Application.Common.BackgroundJobs;
using eSale.Application.Common.Caching;
using eSale.Domain.Modules.Products.Interfaces;
using eSale.Infrastructure.BackgroundJobs;
using eSale.Infrastructure.Caching;
using eSale.Infrastructure.Modules.Products;
using eSale.Infrastructure.Persistence;
using Hangfire;
using Hangfire.MySql;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace eSale.Infrastructure;

public static class ServiceRegistration
{
    public static IServiceCollection AddInfrastructureProduction(this IServiceCollection services, IConfiguration configuration)
    {
        var runHangfireServer = configuration.GetValue("Infrastructure:RunHangfireServer", true);
        var connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException(
                "Database connection string 'DefaultConnection' is missing. " +
                "Set it via environment variable 'ConnectionStrings__DefaultConnection'.");

        var redisConnectionString = configuration.GetConnectionString("Redis")
            ?? throw new InvalidOperationException(
                "Redis connection string 'Redis' is missing. " +
                "Set it via environment variable 'ConnectionStrings__Redis'.");

        services.AddDbContextPool<AppDbContext>(options =>
            options.UseMySql(connectionString, ServerVersion.AutoDetect(connectionString)));

        services.AddStackExchangeRedisCache(options =>
        {
            options.Configuration = redisConnectionString;
            options.InstanceName = "esale:";
        });

        services.AddHangfire(configurationBuilder => configurationBuilder
            .SetDataCompatibilityLevel(CompatibilityLevel.Version_180)
            .UseSimpleAssemblyNameTypeSerializer()
            .UseRecommendedSerializerSettings()
            .UseStorage(new MySqlStorage(connectionString, new MySqlStorageOptions
            {
                QueuePollInterval = TimeSpan.FromSeconds(15),
                JobExpirationCheckInterval = TimeSpan.FromHours(1),
                CountersAggregateInterval = TimeSpan.FromMinutes(5),
                PrepareSchemaIfNecessary = true,
                DashboardJobListLimit = 50000,
                TransactionTimeout = TimeSpan.FromMinutes(1)
            })));

        if (runHangfireServer)
        {
            services.AddHangfireServer();
        }

        services.AddScoped<IProductRepository, ProductRepository>();
        services.AddScoped<DbInitializer>();
        services.AddScoped<ICacheService, RedisCacheService>();
        services.AddScoped<IEmailJobService, EmailJobService>();

        return services;
    }
}
