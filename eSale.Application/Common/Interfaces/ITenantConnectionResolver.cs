namespace eSale.Application.Common.Interfaces;

public interface ITenantConnectionResolver
{
    Task<string> GetConnectionStringAsync(Guid tenantId, CancellationToken cancellationToken = default);
}
