using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.Logging;
using System.Data;

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
        }
        else
        {
            _logger.LogInformation("Database is up to date. No migrations were applied.");
        }

        await EnsureCoreSchemaAsync(cancellationToken);
    }

    private async Task EnsureCoreSchemaAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Ensuring core application schema exists.");

        await _dbContext.Database.ExecuteSqlRawAsync(
            """
            CREATE TABLE IF NOT EXISTS `Products` (
                `Id` char(36) NOT NULL,
                `TenantId` char(36) NOT NULL,
                `CreatedAt` datetime(6) NOT NULL,
                `UpdatedAt` datetime(6) NULL,
                `Name` varchar(200) NOT NULL,
                `Description` varchar(2000) NULL,
                `Sku` varchar(50) NOT NULL,
                `Price` decimal(18,2) NOT NULL,
                `StockQuantity` int NOT NULL,
                `IsActive` tinyint(1) NOT NULL,
                CONSTRAINT `PK_Products` PRIMARY KEY (`Id`)
            );
            """,
            cancellationToken);

        await EnsureIndexExistsAsync(
            "IX_Products_TenantId",
            """
            CREATE INDEX `IX_Products_TenantId`
            ON `Products` (`TenantId`);
            """,
            cancellationToken);

        await EnsureIndexExistsAsync(
            "IX_Products_TenantId_Sku",
            """
            CREATE UNIQUE INDEX `IX_Products_TenantId_Sku`
            ON `Products` (`TenantId`, `Sku`);
            """,
            cancellationToken);
    }

    private async Task EnsureIndexExistsAsync(string indexName, string sql, CancellationToken cancellationToken)
    {
        if (await IndexExistsAsync(indexName, cancellationToken))
        {
            _logger.LogInformation("Index {IndexName} already exists. Skipping creation.", indexName);
            return;
        }

        await _dbContext.Database.ExecuteSqlRawAsync(sql, cancellationToken);
    }

    private async Task<bool> IndexExistsAsync(string indexName, CancellationToken cancellationToken)
    {
        var connection = _dbContext.Database.GetDbConnection();
        var databaseName = connection.Database;
        var shouldCloseConnection = connection.State != ConnectionState.Open;

        if (shouldCloseConnection)
        {
            await connection.OpenAsync(cancellationToken);
        }

        try
        {
            await using var command = connection.CreateCommand();
            command.CommandText =
                """
                SELECT COUNT(*)
                FROM information_schema.statistics
                WHERE table_schema = @databaseName
                  AND table_name = 'Products'
                  AND index_name = @indexName;
                """;

            var databaseParameter = command.CreateParameter();
            databaseParameter.ParameterName = "@databaseName";
            databaseParameter.Value = databaseName;
            command.Parameters.Add(databaseParameter);

            var indexParameter = command.CreateParameter();
            indexParameter.ParameterName = "@indexName";
            indexParameter.Value = indexName;
            command.Parameters.Add(indexParameter);

            var result = await command.ExecuteScalarAsync(cancellationToken);
            return Convert.ToInt32(result) > 0;
        }
        finally
        {
            if (shouldCloseConnection)
            {
                await connection.CloseAsync();
            }
        }
    }
}
