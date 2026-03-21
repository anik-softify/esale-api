using eSale.Application.Common.Interfaces;

namespace eSale.Api.Middleware;

/// <summary>
/// Reads TenantId and ConnectionString set by TenantMiddleware from HttpContext.Items.
/// Registered as Scoped so each request gets its own instance.
/// </summary>
public class TenantProvider : ITenantProvider
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public TenantProvider(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    public Guid GetTenantId()
    {
        var context = _httpContextAccessor.HttpContext;

        if (context is null)
            throw new InvalidOperationException("Tenant context is unavailable outside the current request.");

        if (context.Items.TryGetValue("TenantId", out var tenantObj) && tenantObj is Guid tenantId)
            return tenantId;

        throw new InvalidOperationException("TenantId not found in request context.");
    }

    public string GetConnectionString()
    {
        var context = _httpContextAccessor.HttpContext;

        if (context is null)
            throw new InvalidOperationException("Tenant context is unavailable outside the current request.");

        if (context.Items.TryGetValue("TenantConnectionString", out var connObj) && connObj is string connectionString)
            return connectionString;

        throw new InvalidOperationException("Tenant connection string not found in request context.");
    }
}
