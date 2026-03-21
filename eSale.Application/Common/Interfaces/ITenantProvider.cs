namespace eSale.Application.Common.Interfaces;

/// <summary>
/// Resolves the current tenant and tenant database connection from the request context.
/// Infrastructure implements this (e.g., from HTTP header, JWT claim, subdomain).
/// </summary>
public interface ITenantProvider
{
    Guid GetTenantId();
    string GetConnectionString();
}
