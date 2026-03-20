using eSale.Application.Common.Interfaces;

namespace eSale.Api.Middleware;

/// <summary>
/// Reads TenantId set by TenantMiddleware from HttpContext.Items.
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

        // Allow app startup tasks such as migrations to create the DbContext
        // before an HTTP request exists.
        if (context is null)
            return Guid.Empty;

        if (context.Items.TryGetValue("TenantId", out var tenantObj) && tenantObj is Guid tenantId)
            return tenantId;

        throw new InvalidOperationException("TenantId not found in request context.");
    }
}
