using eSale.Application.Common.BackgroundJobs;
using eSale.Application.Common.Caching;
using eSale.Application.Common.Interfaces;
using eSale.Domain.Common.Interfaces;
using eSale.Domain.Modules.Auth.Entities;
using eSale.Domain.Modules.Products.Interfaces;
using eSale.Domain.Modules.Tenants.Interfaces;
using eSale.Infrastructure.BackgroundJobs;
using eSale.Infrastructure.Caching;
using eSale.Infrastructure.Modules.Auth;
using eSale.Infrastructure.Modules.Products;
using eSale.Infrastructure.Modules.Tenants;
using eSale.Infrastructure.Persistence;
using Hangfire;
using Hangfire.MySql;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace eSale.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var runHangfireServer = configuration.GetValue("Infrastructure:RunHangfireServer", true);
        var connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException(
                "Database connection string 'DefaultConnection' is missing. " +
                "Set it via environment variable 'ConnectionStrings__DefaultConnection'.");
        var redisConnectionString = configuration.GetConnectionString("Redis");

        // Detect MySQL server version once for reuse
        var serverVersion = ServerVersion.AutoDetect(connectionString);

        // Central database: Tenant registry + Hangfire only
        services.AddDbContext<CentralDbContext>(options =>
            options.UseMySql(connectionString, serverVersion));

        // Tenant database: dynamic connection per request (includes Identity tables)
        services.AddScoped<AppDbContext>(sp =>
        {
            var tenantProvider = sp.GetRequiredService<ITenantProvider>();
            var connStr = tenantProvider.GetConnectionString();

            if (string.IsNullOrWhiteSpace(connStr))
            {
                throw new InvalidOperationException(
                    "Tenant connection string is required for AppDbContext. " +
                    "Ensure the request has a valid tenant context.");
            }

            var optionsBuilder = new DbContextOptionsBuilder<AppDbContext>();
            optionsBuilder.UseMySql(connStr, serverVersion);

            return new AppDbContext(optionsBuilder.Options, tenantProvider);
        });

        // Identity now uses tenant-scoped AppDbContext
        // Email uniqueness is enforced per tenant database naturally
        services.AddIdentity<ApplicationUser, IdentityRole>(options =>
        {
            options.Password.RequireDigit = true;
            options.Password.RequiredLength = 6;
            options.Password.RequireNonAlphanumeric = false;
            options.Password.RequireUppercase = true;
            options.Password.RequireLowercase = true;
            options.User.RequireUniqueEmail = true;
        })
        .AddEntityFrameworkStores<AppDbContext>()
        .AddDefaultTokenProviders();

        // Redis / memory cache
        if (string.IsNullOrWhiteSpace(redisConnectionString))
        {
            services.AddDistributedMemoryCache();
        }
        else
        {
            services.AddStackExchangeRedisCache(options =>
            {
                options.Configuration = redisConnectionString;
                options.InstanceName = "esale:";
            });
        }

        // Hangfire (central database)
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

        // Repositories and services
        services.AddScoped<IProductRepository, ProductRepository>();
        services.AddScoped<IUnitOfWork, UnitOfWork>();
        services.AddScoped<ITenantRepository, TenantRepository>();
        services.AddScoped<ITenantConnectionResolver, TenantConnectionResolver>();
        services.AddScoped<ICacheService, RedisCacheService>();
        services.AddScoped<IEmailJobService, EmailJobService>();
        services.AddScoped<IJwtTokenService, JwtTokenService>();

        // Database initializers
        services.AddScoped<DbInitializer>();
        services.AddScoped<TenantDbInitializer>();
        services.AddScoped<ITenantDbInitializer>(sp => sp.GetRequiredService<TenantDbInitializer>());

        return services;
    }
}
