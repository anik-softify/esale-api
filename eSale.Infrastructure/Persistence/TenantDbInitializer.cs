using eSale.Application.Common.Interfaces;
using eSale.Domain.Modules.Tenants.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using MySqlConnector;

namespace eSale.Infrastructure.Persistence;

/// <summary>
/// Creates and initializes individual tenant databases.
/// </summary>
public sealed class TenantDbInitializer : ITenantDbInitializer
{
    private readonly ITenantRepository _tenantRepository;
    private readonly ITenantConnectionResolver _connectionResolver;
    private readonly IConfiguration _configuration;
    private readonly ILogger<TenantDbInitializer> _logger;

    public TenantDbInitializer(
        ITenantRepository tenantRepository,
        ITenantConnectionResolver connectionResolver,
        IConfiguration configuration,
        ILogger<TenantDbInitializer> logger)
    {
        _tenantRepository = tenantRepository;
        _connectionResolver = connectionResolver;
        _configuration = configuration;
        _logger = logger;
    }

    /// <summary>
    /// Initializes all active tenant databases at application startup.
    /// </summary>
    public async Task InitializeAllTenantsAsync(CancellationToken cancellationToken = default)
    {
        var tenants = await _tenantRepository.GetAllActiveAsync(cancellationToken);
        _logger.LogInformation("Initializing databases for {Count} active tenants.", tenants.Count);

        foreach (var tenant in tenants)
        {
            await InitializeTenantDatabaseAsync(tenant.Id, cancellationToken);
        }
    }

    /// <summary>
    /// Creates and migrates a single tenant's database.
    /// </summary>
    public async Task InitializeTenantDatabaseAsync(Guid tenantId, CancellationToken cancellationToken = default)
    {
        var connectionString = await _connectionResolver.GetConnectionStringAsync(tenantId, cancellationToken);

        // Extract database name to create it if it doesn't exist
        var builder = new MySqlConnectionStringBuilder(connectionString);
        var databaseName = builder.Database;

        _logger.LogInformation("Initializing tenant database: {DatabaseName}", databaseName);

        // Create the database using the admin/central connection
        var adminConnectionString = _configuration.GetConnectionString("DefaultConnection")!;
        var adminBuilder = new MySqlConnectionStringBuilder(adminConnectionString);
        adminBuilder.Database = string.Empty; // Connect without a specific database

        await using (var connection = new MySqlConnection(adminBuilder.ConnectionString))
        {
            await connection.OpenAsync(cancellationToken);
            await using var command = connection.CreateCommand();
            command.CommandText = $"CREATE DATABASE IF NOT EXISTS `{databaseName}` CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci;";
            await command.ExecuteNonQueryAsync(cancellationToken);
        }

        // Build a temporary AppDbContext and ensure schema exists
        var serverVersion = ServerVersion.AutoDetect(connectionString);
        var optionsBuilder = new DbContextOptionsBuilder<AppDbContext>();
        optionsBuilder.UseMySql(connectionString, serverVersion);

        var tenantProvider = new StartupTenantProvider(tenantId, connectionString);
        await using var dbContext = new AppDbContext(optionsBuilder.Options, tenantProvider);
        await dbContext.Database.EnsureCreatedAsync(cancellationToken);

        _logger.LogInformation("Tenant database {DatabaseName} is ready.", databaseName);
    }

    /// <summary>
    /// Minimal tenant provider used during startup initialization.
    /// </summary>
    private sealed class StartupTenantProvider(Guid tenantId, string connectionString) : ITenantProvider
    {
        public Guid GetTenantId() => tenantId;
        public string GetConnectionString() => connectionString;
    }
}
