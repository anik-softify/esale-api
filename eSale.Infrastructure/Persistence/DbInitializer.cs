using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace eSale.Infrastructure.Persistence;

/// <summary>
/// Initializes the central database (tenant registry + shared platform tables).
/// </summary>
public sealed class DbInitializer
{
    private readonly CentralDbContext _centralDbContext;
    private readonly ILogger<DbInitializer> _logger;

    public DbInitializer(CentralDbContext centralDbContext, ILogger<DbInitializer> logger)
    {
        _centralDbContext = centralDbContext;
        _logger = logger;
    }

    public async Task ApplyMigrationsAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Ensuring central database exists and is up to date.");
        await _centralDbContext.Database.EnsureCreatedAsync(cancellationToken);
        _logger.LogInformation("Central database is ready.");
    }
}
