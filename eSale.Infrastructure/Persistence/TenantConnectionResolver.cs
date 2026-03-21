using eSale.Application.Common.Interfaces;
using eSale.Domain.Modules.Tenants.Interfaces;
using Microsoft.Extensions.Configuration;

namespace eSale.Infrastructure.Persistence;

public sealed class TenantConnectionResolver : ITenantConnectionResolver
{
    private readonly ITenantRepository _tenantRepository;
    private readonly IConfiguration _configuration;

    public TenantConnectionResolver(ITenantRepository tenantRepository, IConfiguration configuration)
    {
        _tenantRepository = tenantRepository;
        _configuration = configuration;
    }

    public async Task<string> GetConnectionStringAsync(Guid tenantId, CancellationToken cancellationToken = default)
    {
        var tenant = await _tenantRepository.GetByIdAsync(tenantId, cancellationToken);

        if (tenant is null || !tenant.IsActive)
            throw new InvalidOperationException($"Tenant '{tenantId}' not found or inactive.");

        if (!string.IsNullOrWhiteSpace(tenant.ConnectionString))
            return tenant.ConnectionString;

        // Build connection string by convention: replace Database in the template
        var template = _configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("DefaultConnection is missing.");

        return ReplaceDatabaseName(template, tenant.DatabaseName);
    }

    private static string ReplaceDatabaseName(string connectionString, string databaseName)
    {
        // Replace Database=xxx; with Database=tenantDbName;
        var parts = connectionString.Split(';', StringSplitOptions.RemoveEmptyEntries);
        var result = new List<string>();

        foreach (var part in parts)
        {
            var trimmed = part.Trim();
            if (trimmed.StartsWith("Database=", StringComparison.OrdinalIgnoreCase))
            {
                result.Add($"Database={databaseName}");
            }
            else
            {
                result.Add(trimmed);
            }
        }

        return string.Join(";", result) + ";";
    }
}
