namespace eSale.Application.Common.Interfaces;

public interface ITenantDbInitializer
{
    Task InitializeTenantDatabaseAsync(Guid tenantId, CancellationToken cancellationToken = default);
}
