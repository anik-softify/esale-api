using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace eSale.Infrastructure.Persistence;

public sealed class DbInitializer
{
    private readonly AppDbContext _dbContext;
    private readonly ILogger<DbInitializer> _logger;

    public DbInitializer(AppDbContext dbContext, ILogger<DbInitializer> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    public async Task ApplyMigrationsAsync(CancellationToken cancellationToken = default)
    {
        if ((await _dbContext.Database.GetPendingMigrationsAsync(cancellationToken)).Any())
        {
            _logger.LogInformation("Applying pending database migrations.");
            await _dbContext.Database.MigrateAsync(cancellationToken);
            return;
        }

        _logger.LogInformation("Database is up to date. No migrations were applied.");
    }
}
