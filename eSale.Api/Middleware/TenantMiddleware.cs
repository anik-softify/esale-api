using eSale.Application.Common.Interfaces;
using System.Security.Claims;

namespace eSale.Api.Middleware;

/// <summary>
/// Resolves the current tenant for every non-excluded route.
/// All routes (including /api/account) now require a tenant because
/// Identity is tenant-scoped (Database-per-Tenant).
/// </summary>
public class TenantMiddleware
{
    private readonly RequestDelegate _next;

    private static readonly string[] ExcludedPrefixes =
    [
        "/api/tenants",
        "/hangfire"
    ];

    public TenantMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var path = context.Request.Path.Value ?? string.Empty;

        if (IsExcluded(path))
        {
            await _next(context);
            return;
        }

        if (!TryResolveTenantId(context, out var tenantId))
        {
            throw new BadHttpRequestException("Missing or invalid tenant identifier.");
        }

        // Resolve connection string for ALL tenant routes (including auth)
        var resolver = context.RequestServices.GetRequiredService<ITenantConnectionResolver>();
        var connectionString = await resolver.GetConnectionStringAsync(tenantId, context.RequestAborted);

        context.Items["TenantId"] = tenantId;
        context.Items["TenantConnectionString"] = connectionString;

        await _next(context);
    }

    private static bool TryResolveTenantId(HttpContext context, out Guid tenantId)
    {
        tenantId = Guid.Empty;

        Guid? headerTenantId = null;
        if (context.Request.Headers.TryGetValue("X-Tenant-Id", out var tenantHeader)
            && Guid.TryParse(tenantHeader, out var parsedHeaderTenantId))
        {
            headerTenantId = parsedHeaderTenantId;
        }

        Guid? claimTenantId = null;
        var claimValue = context.User.FindFirstValue("tenantId");
        if (Guid.TryParse(claimValue, out var parsedClaimTenantId))
        {
            claimTenantId = parsedClaimTenantId;
        }

        // If both header and JWT claim are present, they must match
        if (headerTenantId.HasValue && claimTenantId.HasValue && headerTenantId.Value != claimTenantId.Value)
        {
            throw new UnauthorizedAccessException("Authenticated tenant does not match the requested tenant.");
        }

        if (headerTenantId.HasValue)
        {
            tenantId = headerTenantId.Value;
            return true;
        }

        if (claimTenantId.HasValue)
        {
            tenantId = claimTenantId.Value;
            return true;
        }

        return false;
    }

    private static bool IsExcluded(string path)
    {
        foreach (var prefix in ExcludedPrefixes)
        {
            if (path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                return true;
        }
        return false;
    }
}
